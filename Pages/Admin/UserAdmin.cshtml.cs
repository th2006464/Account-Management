using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Net;
using System.Net.Mail;
using System.Text.Json;
using System.Threading.Tasks;
using AccountManagement.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AccountManagement.Pages.Admin;

public class UserAdminModel : PageModel
{
    private readonly ILogger<UserAdminModel> _logger;
    private readonly IConfiguration _configuration;

    private static readonly object s_lock = new();
    private static List<string>? s_resetHistory;
    private static List<string>? s_emailStatus;
    private static List<string>? s_offboardHistory;
    private static List<string>? s_groupHistory;

    private static string StoragePath => Path.Combine(AppContext.BaseDirectory, "App_Data");
    private static string HistoryFile => Path.Combine(StoragePath, "reset_history.dat");
    private static string EmailStatusFile => Path.Combine(StoragePath, "email_status.dat");
    private static string OffboardHistoryFile => Path.Combine(StoragePath, "offboard_history.dat");
    private static string GroupHistoryFile => Path.Combine(StoragePath, "group_history.dat");
    private static string AuditLogFile => Path.Combine(StoragePath, "audit.dat");

    public UserAdminModel(ILogger<UserAdminModel> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public bool IsAuthenticated { get; set; }
    public string? CurrentEmployeeId { get; set; }
    public string? CurrentDisplayName { get; set; }

    [BindProperty]
    public string? SearchEmployeeId { get; set; }

    [BindProperty]
    public string? EmployeeName { get; set; }

    public string? UserDetail { get; set; }
    public string? ResultMessage { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string>? UnlockResults { get; set; }
    public List<string>? ResetResults { get; set; }
    public List<string>? OffboardResults { get; set; }
    [BindProperty]
    public string? AddAdminEmployeeId { get; set; }

    [BindProperty]
    public string? RemoveAdminEmployeeId { get; set; }

    [BindProperty]
    public string? GroupName { get; set; }

    public List<string>? AdminList { get; set; }
    public List<string>? ResetHistory { get; set; }
    public List<string>? OffboardHistory { get; set; }
    public List<string>? EmailStatus { get; set; }
    public List<string>? OperationHistory { get; set; }
    public List<string>? EmailHistory { get; set; }

    public void OnGet()
    {
        CheckAuth();
        if (!IsAuthenticated) return;
        ResetHistory = LoadHistory();
        OffboardHistory = LoadOffboardHistory();
        EmailStatus = LoadEmailStatus();
        AdminList = LoginModel.LoadAdminList();
        BuildCombinedLists();

        if (TempData["ResetResults"] is string rr)
            ResetResults = new List<string> { rr };
        if (TempData["UnlockResults"] is string ur)
            UnlockResults = new List<string> { ur };
        if (TempData["OffboardResults"] is string or)
            OffboardResults = new List<string> { or };
        if (TempData["ResultMessage"] is string rm)
            ResultMessage = rm;
        if (TempData["ErrorMessage"] is string em)
            ErrorMessage = em;
        if (TempData["SearchEmployeeId"] is string se)
            SearchEmployeeId = se;
        if (TempData["EmployeeName"] is string en)
            EmployeeName = en;
    }

    public IActionResult OnPost(string action)
    {
        CheckAuth();
        if (!IsAuthenticated) return RedirectToPage();
        ResetHistory = LoadHistory();
        OffboardHistory = LoadOffboardHistory();
        EmailStatus = LoadEmailStatus();
        AdminList = LoginModel.LoadAdminList();
        BuildCombinedLists();

        if (action == "search")
        {
            SearchUser();
            return Page();
        }
        else if (action == "unlock")
        {
            UnlockAllUsers();
        }
        else if (action == "reset")
        {
            ResetPasswordAndEmail();
        }
        else if (action == "offboard")
        {
            OffboardUser();
        }
        else if (action == "clearHistory")
        {
            ClearResetHistory();
        }
        else if (action == "addAdmin")
        {
            if (CurrentEmployeeId != "15005035") { ErrorMessage = "仅超级管理员可操作。"; }
            else if (!string.IsNullOrWhiteSpace(AddAdminEmployeeId) && AddAdminEmployeeId.Length == 8 && AddAdminEmployeeId.All(char.IsDigit))
            {
                LoginModel.AddAdmin(AddAdminEmployeeId);
                WriteAuditLog($"管理员授权 | 操作人: {CurrentEmployeeId} | 添加管理员: {AddAdminEmployeeId}");
                ResultMessage = $"已添加管理员: {AddAdminEmployeeId}";
            }
            else
            {
                ErrorMessage = "员工号格式不正确。";
            }
        }
        else if (action == "addToGroup")
        {
            AddUserToGroup();
            return Page();
        }
        else if (action == "removeAdmin")
        {
            if (CurrentEmployeeId != "15005035") { ErrorMessage = "仅超级管理员可操作。"; }
            else if (RemoveAdminEmployeeId == "15005035") { ErrorMessage = "不能移除超级管理员。"; }
            else if (!string.IsNullOrWhiteSpace(RemoveAdminEmployeeId))
            {
                LoginModel.RemoveAdmin(RemoveAdminEmployeeId);
                WriteAuditLog($"管理员授权 | 操作人: {CurrentEmployeeId} | 移除管理员: {RemoveAdminEmployeeId}");
                ResultMessage = $"已移除管理员: {RemoveAdminEmployeeId}";
            }
        }

        if (ResetResults is { Count: > 0 })
            TempData["ResetResults"] = string.Join(Environment.NewLine, ResetResults);
        if (UnlockResults is { Count: > 0 })
            TempData["UnlockResults"] = string.Join(Environment.NewLine, UnlockResults);
        if (OffboardResults is { Count: > 0 })
            TempData["OffboardResults"] = string.Join(Environment.NewLine, OffboardResults);
        if (ResultMessage != null)
            TempData["ResultMessage"] = ResultMessage;
        if (ErrorMessage != null)
            TempData["ErrorMessage"] = ErrorMessage;
        if (SearchEmployeeId != null)
            TempData["SearchEmployeeId"] = SearchEmployeeId;
        if (EmployeeName != null)
            TempData["EmployeeName"] = EmployeeName;

        return RedirectToPage();
    }

    private void CheckAuth()
    {
        var loggedIn = HttpContext.Session.GetString("AdminLoggedIn");
        if (loggedIn == "true")
        {
            IsAuthenticated = true;
            CurrentEmployeeId = HttpContext.Session.GetString("AdminEmployeeId");
            CurrentDisplayName = HttpContext.Session.GetString("AdminDisplayName") ?? CurrentEmployeeId;
        }
    }

    private bool ValidateEmployeeId()
    {
        if (string.IsNullOrWhiteSpace(SearchEmployeeId))
        {
            ErrorMessage = "请输入员工号。";
            return false;
        }
        if (SearchEmployeeId.Length != 8 || !SearchEmployeeId.All(char.IsDigit))
        {
            ErrorMessage = "员工号有误，请输入8位数字。";
            return false;
        }
        return true;
    }

    private void SearchUser()
    {
        if (string.IsNullOrWhiteSpace(SearchEmployeeId))
        {
            ErrorMessage = "请输入搜索关键字。";
            return;
        }

        try
        {
            using var context = new PrincipalContext(ContextType.Domain);
            using var searchRoot = new DirectoryEntry($"LDAP://{context.ConnectedServer}");
            using var searcher = new DirectorySearcher(searchRoot)
            {
                PageSize = 50,
                SizeLimit = 50
            };

            var keyword = SearchEmployeeId!.Trim();
            bool isNumeric = keyword.All(char.IsDigit);

            if (isNumeric)
                searcher.Filter = $"(|(sAMAccountName=*{keyword}*)(employeeID=*{keyword}*))";
            else
                searcher.Filter = $"(|(sAMAccountName=*{keyword}*)(displayName=*{keyword}*)(userPrincipalName=*{keyword}*)(givenName=*{keyword}*)(sn=*{keyword}*))";

            searcher.PropertiesToLoad.AddRange(new[]
            {
                "sAMAccountName","displayName","employeeID","mail","userPrincipalName",
                "userAccountControl","pwdLastSet","lastLogonTimestamp",
                "telephoneNumber","mobile","description"
            });
            searcher.Sort.PropertyName = "sAMAccountName";

            using var results = searcher.FindAll();
            if (results.Count == 0)
            {
                ErrorMessage = $"未找到匹配 '{keyword}' 的用户。";
                return;
            }

            var allLines = new List<string>();
            foreach (SearchResult result in results)
            {
                using var user = UserPrincipal.FindByIdentity(context, IdentityType.SamAccountName, (string)result.Properties["sAMAccountName"][0]);
                if (user == null) continue;

                var entry = (DirectoryEntry)user.GetUnderlyingObject();
                var lines = new List<string>
                {
                    $"用户名: {user.SamAccountName}",
                    $"显示名: {user.DisplayName}",
                    $"员工号: {user.EmployeeId}",
                    $"UPN: {user.UserPrincipalName}",
                    $"邮箱: {user.EmailAddress}",
                    $"电话: {GetProp(entry, "telephoneNumber")}",
                    $"手机: {GetProp(entry, "mobile")}",
                    $"账号启用: {(user.Enabled == true ? "是" : "否")}",
                    $"账号锁定: {(user.IsAccountLockedOut() ? "是 (已锁定)" : "否 (正常)")}",
                    $"账号过期时间: {(user.AccountExpirationDate?.ToString("yyyy-MM-dd HH:mm:ss") ?? "永不过期")}",
                    $"密码上次设置: {(user.LastPasswordSet?.ToString("yyyy-MM-dd HH:mm:ss") ?? "未知")}",
                    $"上次登录时间: {(user.LastLogon?.ToString("yyyy-MM-dd HH:mm:ss") ?? "未知")}",
                    $"密码永不过期: {(user.PasswordNeverExpires ? "是" : "否")}"
                };

                if (!user.PasswordNeverExpires && user.LastPasswordSet.HasValue)
                {
                    var maxPwdAge = GetDomainMaxPasswordAge(context);
                    if (maxPwdAge.HasValue && maxPwdAge.Value > 0)
                    {
                        var expireDate = user.LastPasswordSet.Value.AddTicks(-maxPwdAge.Value);
                        var remaining = expireDate - DateTime.Now;
                        if (remaining.TotalDays < 0)
                            lines.Add($"密码已过期: {Math.Abs((int)remaining.TotalDays)} 天前已过期 ({expireDate:yyyy-MM-dd HH:mm:ss})");
                        else
                            lines.Add($"密码过期时间: {expireDate:yyyy-MM-dd HH:mm:ss} (剩余 {(int)remaining.TotalDays} 天)");
                    }
                }

                try
                {
                    var groups = user.GetGroups().OfType<GroupPrincipal>().Select(g => g.SamAccountName).OrderBy(n => n).ToList();
                    if (groups.Count > 0)
                    {
                        lines.Add("所属组:");
                        foreach (var g in groups) lines.Add($"  - {g}");
                    }
                }
                catch { lines.Add("所属组: (无法获取)"); }

                allLines.AddRange(lines);
                if (results.Count > 1) allLines.Add(new string('-', 40));
            }

            EmployeeName = "";
            UserDetail = string.Join(Environment.NewLine, allLines);
            ResultMessage = $"查询完成，找到 {results.Count} 个用户（最多显示50个）。";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "管理员查询用户失败");
            ErrorMessage = "查询失败：" + ex.Message;
        }
    }

    private void UnlockAllUsers()
    {
        UnlockResults = new List<string>();
        try
        {
            using var context = new PrincipalContext(ContextType.Domain);
            using var searchRoot = new DirectoryEntry($"LDAP://{context.ConnectedServer}");
            using var searcher = new DirectorySearcher(searchRoot)
            {
                Filter = "(&(objectCategory=person)(objectClass=user)(lockoutTime>=1))",
                PageSize = 1000
            };
            var lockedUsers = new List<UserPrincipal>();
            foreach (SearchResult result in searcher.FindAll())
            {
                var user = UserPrincipal.FindByIdentity(context, IdentityType.SamAccountName, (string)result.Properties["sAMAccountName"][0]);
                if (user != null)
                    lockedUsers.Add(user);
            }

            if (lockedUsers.Count == 0)
            {
                UnlockResults.Add("当前域内没有被锁定的账号。");
                ResultMessage = "解锁完成。";
                return;
            }

            foreach (var user in lockedUsers)
            {
                try
                {
                    user.UnlockAccount();
                    user.Save();
                    UnlockResults.Add($"已解锁: {user.SamAccountName} ({user.DisplayName})");
                }
                catch (Exception ex)
                {
                    UnlockResults.Add($"解锁失败: {user.SamAccountName} - {ex.Message}");
                }
            }

            UnlockResults.Add("");
            UnlockResults.Add($"共处理 {lockedUsers.Count} 个锁定账号。");
            WriteAuditLog($"解锁账号 | 操作人: {CurrentEmployeeId} | 共解锁 {lockedUsers.Count} 个账号");
            ResultMessage = "解锁完成。";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解锁操作失败");
            UnlockResults.Add($"操作失败：{ex.Message}");
        }
    }

    private static string GetProp(DirectoryEntry entry, string name)
    {
        return entry.Properties.Contains(name) && entry.Properties[name]?.Value != null
            ? entry.Properties[name].Value.ToString() ?? "-"
            : "-";
    }

    private static long? GetDomainMaxPasswordAge(PrincipalContext context)
    {
        try
        {
            using var rootDse = new DirectoryEntry("LDAP://rootDSE");
            var domainDN = rootDse.Properties["defaultNamingContext"]?.Value?.ToString();
            if (string.IsNullOrEmpty(domainDN)) return null;

            using var domainEntry = new DirectoryEntry($"LDAP://{domainDN}");
            if (domainEntry.Properties.Contains("maxPwdAge"))
            {
                var val = domainEntry.Properties["maxPwdAge"]?.Value;
                if (val is long ticks && ticks != 0 && ticks != -1)
                    return ticks;
            }
        }
        catch { }
        return null;
    }

    private void ResetPasswordAndEmail()
    {
        ResetResults = new List<string>();
        if (!ValidateEmployeeId()) return;

        try
        {
            using var context = new PrincipalContext(ContextType.Domain);
            using var user = UserPrincipal.FindByIdentity(context, IdentityType.SamAccountName, SearchEmployeeId);

            if (user == null)
            {
                ErrorMessage = $"未找到员工号 '{SearchEmployeeId}' 对应的用户。";
                return;
            }

            if (user.Enabled != true)
            {
                ErrorMessage = $"账号 '{SearchEmployeeId}' 当前为禁用状态，无法重置。";
                return;
            }

            if (user.IsAccountLockedOut())
            {
                user.UnlockAccount();
                user.Save();
                ResetResults.Add("账号已解锁。");
            }

            var newPassword = GenerateRandomPassword();
            PasswordHelper.SetPasswordWithNotification(SearchEmployeeId, newPassword);

            var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            ResetResults.Add($"员工号: {SearchEmployeeId}");
            ResetResults.Add($"显示名: {user.DisplayName}");
            ResetResults.Add($"新密码: {newPassword}");
            ResetResults.Add("");

            AddHistory($"{now} | 账号: {SearchEmployeeId} ({user.DisplayName}) | 密码: {newPassword}");
            ResetHistory = LoadHistory();

            var empId = SearchEmployeeId;
            var empName = user.DisplayName;
            var pwd = newPassword;
            _ = Task.Run(() =>
            {
                try
                {
                    SendEmail(empId, pwd, empName);
                    AddEmailStatus($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | 邮件发送成功 | 账号: {empId}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "邮件发送失败");
                    AddEmailStatus($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | 邮件发送失败: {ex.Message} | 账号: {empId}");
                }
            });

            ResetResults.Add("邮件正在后台发送...");
            WriteAuditLog($"密码重置 | 操作人: {CurrentEmployeeId} | 账号: {SearchEmployeeId} ({user.DisplayName}) | 新密码: {newPassword}");
            ResultMessage = "密码重置完成。";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "重置密码失败");
            ErrorMessage = "重置密码失败：" + ex.Message;
        }
    }

    private static string GenerateRandomPassword()
    {
        const string upper = "ABCDEFGHIJKMNPQRSTUVWXYZ";   // 排除 L, O
        const string lower = "abcdefghijkmnpqrstuvwxyz";   // 排除 l, o
        const string numbers = "23456789";                  // 排除 0, 1
        const string symbols = "@$%&*,./";
        const string all = upper + lower + numbers + symbols;
        const int length = 12;

        var rng = System.Security.Cryptography.RandomNumberGenerator.Create();

        var chars = new char[length];
        chars[0] = upper[GetRandomInt(rng, upper.Length)];
        chars[1] = lower[GetRandomInt(rng, lower.Length)];
        chars[2] = numbers[GetRandomInt(rng, numbers.Length)];
        chars[3] = symbols[GetRandomInt(rng, symbols.Length)];

        for (int i = 4; i < length; i++)
            chars[i] = all[GetRandomInt(rng, all.Length)];

        for (int i = length - 1; i > 0; i--)
        {
            int j = GetRandomInt(rng, i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars);
    }

    private static int GetRandomInt(System.Security.Cryptography.RandomNumberGenerator rng, int max)
    {
        var bytes = new byte[4];
        rng.GetBytes(bytes);
        return (int)(BitConverter.ToUInt32(bytes, 0) % (uint)max);
    }

    private void SendEmail(string employeeId, string newPassword, string displayName)
    {
        ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, errors) => true;

        var smtpServer = _configuration["EmailSettings:SmtpServer"] ?? "";
        var smtpPort = int.Parse(_configuration["EmailSettings:SmtpPort"] ?? "587");
        var fromAddress = _configuration["EmailSettings:FromAddress"] ?? "";
        var toAddress = _configuration["EmailSettings:ToAddress"] ?? "";
        var ccAddress = _configuration["EmailSettings:CcAddress"] ?? "";
        var username = _configuration["EmailSettings:Username"] ?? "";
        var password = _configuration["EmailSettings:Password"] ?? "";

        var body = $@"尊敬的用户，

您的 GARCHINA 账号 {employeeId} 的密码已重置。
新密码为：{newPassword}

此密码适用于：
- GARCHINA 系统认证
- China OA 系统
- GARCHINA VPN
- Workday 请休假系统

特别注意：
1. 复制粘贴密码时，请先粘贴到记事本，检查是否有空格。
2. 输入密码后，点击密码框旁的小眼睛图标确认输入正确。
3. 请尽快更新默认密码，密码更新后会自动同步至邮箱系统。
4. ChinaOA、Workday系统登录时请注意用户名格式。

如有问题，请联系中国区 IT 部门：
邮箱：CN_IT_Support@sinarmas-agri.com

此邮件由系统 [10.95.0.62] 自动发送，请勿回复。";

        using var client = new SmtpClient(smtpServer, smtpPort)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(username, password)
        };

        using var message = new MailMessage(fromAddress, toAddress)
        {
            Subject = "[IT信息] 用户AD 账号密码重置通知",
            Body = body,
            BodyEncoding = System.Text.Encoding.UTF8,
            IsBodyHtml = false
        };
        message.CC.Add(ccAddress);

        client.Send(message);
    }

    private void OffboardUser()
    {
        OffboardResults = new List<string>();
        if (!ValidateEmployeeId()) return;

        try
        {
            // 按 employeeID 或 sAMAccountName 搜索
            using var context = new PrincipalContext(ContextType.Domain);
            using var searchRoot = new DirectoryEntry($"LDAP://{context.ConnectedServer}");
            using var searcher = new DirectorySearcher(searchRoot)
            {
                Filter = $"(|(employeeID={SearchEmployeeId})(sAMAccountName={SearchEmployeeId}))"
            };
            searcher.PropertiesToLoad.AddRange(new[] { "sAMAccountName", "displayName", "description", "mail", "userPrincipalName", "employeeID", "userAccountControl" });

            var result = searcher.FindOne();
            if (result == null)
            {
                ErrorMessage = $"未找到员工号 '{SearchEmployeeId}' 对应的用户。";
                return;
            }

            var sam = (string)result.Properties["sAMAccountName"][0];
            var displayName = result.Properties.Contains("displayName") && result.Properties["displayName"].Count > 0
                ? (string)result.Properties["displayName"][0] : sam;
            var enabled = true;
            if (result.Properties.Contains("userAccountControl"))
            {
                var uac = (int)result.Properties["userAccountControl"][0];
                enabled = (uac & 2) == 0; // UF_ACCOUNTDISABLE = 2
            }

            if (!enabled)
            {
                var desc = result.Properties.Contains("description") && result.Properties["description"].Count > 0
                    ? (string)result.Properties["description"][0] : "（无描述）";
                OffboardResults.Add($"【已关闭】{displayName} ({sam})");
                OffboardResults.Add($"          {desc}");
                ResultMessage = "该账号已处于关闭状态。";
                return;
            }

            // 执行离职操作
            using var userEntry = result.GetDirectoryEntry();
            var now = DateTime.Now.ToString("yyyy-MM-dd");

            // 更新描述
            var oldDesc = result.Properties.Contains("description") && result.Properties["description"].Count > 0
                ? (string)result.Properties["description"][0] : "";
            var newDesc = string.IsNullOrEmpty(oldDesc) ? $"离职 {now}" : (oldDesc.Contains("离职") ? oldDesc : $"{oldDesc} 离职 {now}");
            userEntry.Properties["description"].Value = newDesc;

            // 修改 UPN
            userEntry.Properties["userPrincipalName"].Value = $"{sam}.x@garchina.com";

            // 修改邮箱
            if (result.Properties.Contains("mail") && result.Properties["mail"].Count > 0)
            {
                var oldMail = (string)result.Properties["mail"][0];
                userEntry.Properties["mail"].Value = oldMail.Replace("@", ".x@");
            }
            else
            {
                userEntry.Properties["mail"].Value = "邮箱已断开连接";
            }

            userEntry.CommitChanges();

            // 禁用账号
            using var user = UserPrincipal.FindByIdentity(context, IdentityType.SamAccountName, sam);
            if (user != null)
            {
                user.Enabled = false;
                user.Save();
            }

            OffboardResults.Add($"【成功关闭】{displayName} ({sam})  离职 {now}");

            var history = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | 离职: {sam} | {displayName} | {now}";
            AddOffboardHistory(history);
            WriteAuditLog($"离职处理 | 操作人: {CurrentEmployeeId} | 账号: {sam} ({displayName})");

            ResultMessage = "离职处理完成。";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "离职处理失败");
            ErrorMessage = "离职处理失败：" + ex.Message;
        }
    }

    private void BuildCombinedLists()
    {
        var ops = new List<string>();
        if (ResetHistory != null)
            foreach (var r in ResetHistory)
                ops.Add($"密码重置 | {r}");
        if (OffboardHistory != null)
            foreach (var o in OffboardHistory)
                ops.Add($"离职处理 | {o}");
        var groupHist = LoadGroupHistory();
        foreach (var g in groupHist)
            ops.Add($"加用户组 | {g}");
        OperationHistory = ops.OrderByDescending(x => x).ToList();

        var emails = new List<string>();
        if (EmailStatus != null)
            foreach (var e in EmailStatus)
                emails.Add($"密码重置 | {e}");
        // NewUser email status is separate, but the UserAdmin doesn't track it directly
        // We could load it here too
        EmailHistory = emails.OrderByDescending(x => x).ToList();
    }

    // ---- 审计日志（永不清除） ----

    private static void WriteAuditLog(string entry)
    {
        try
        {
            Directory.CreateDirectory(StoragePath);
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {entry}{Environment.NewLine}";
            lock (s_lock)
            {
                FileProtection.AppendAllText(AuditLogFile, line);
            }
        }
        catch { }
    }

    // ---- 文件持久化 ----

    private static List<string> LoadOffboardHistory()
    {
        lock (s_lock)
        {
            if (s_offboardHistory != null)
                return s_offboardHistory.ToList();

            try
            {
                if (FileProtection.Exists(OffboardHistoryFile))
                {
                    var json = FileProtection.ReadAllText(OffboardHistoryFile);
                    s_offboardHistory = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
                }
                else
                {
                    s_offboardHistory = new List<string>();
                }
            }
            catch
            {
                s_offboardHistory = new List<string>();
            }
            return s_offboardHistory.ToList();
        }
    }

    private static List<string> LoadGroupHistory()
    {
        lock (s_lock)
        {
            if (s_groupHistory != null)
                return s_groupHistory.ToList();

            try
            {
                if (FileProtection.Exists(GroupHistoryFile))
                {
                    var json = FileProtection.ReadAllText(GroupHistoryFile);
                    s_groupHistory = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
                }
                else
                {
                    s_groupHistory = new List<string>();
                }
            }
            catch
            {
                s_groupHistory = new List<string>();
            }
            return s_groupHistory.ToList();
        }
    }

    private static void AddGroupHistory(string entry)
    {
        lock (s_lock)
        {
            s_groupHistory ??= new List<string>();
            s_groupHistory.Insert(0, entry);
            if (s_groupHistory.Count > 200)
                s_groupHistory.RemoveRange(200, s_groupHistory.Count - 200);

            try
            {
                Directory.CreateDirectory(StoragePath);
                FileProtection.WriteAllText(GroupHistoryFile, JsonSerializer.Serialize(s_groupHistory));
            }
            catch { }
        }
    }

    private static void AddOffboardHistory(string entry)
    {
        lock (s_lock)
        {
            s_offboardHistory ??= new List<string>();
            s_offboardHistory.Insert(0, entry);
            if (s_offboardHistory.Count > 200)
                s_offboardHistory.RemoveRange(200, s_offboardHistory.Count - 200);

            try
            {
                Directory.CreateDirectory(StoragePath);
                FileProtection.WriteAllText(OffboardHistoryFile, JsonSerializer.Serialize(s_offboardHistory));
            }
            catch { }
        }
    }

    private static List<string> LoadHistory()
    {
        lock (s_lock)
        {
            if (s_resetHistory != null)
                return s_resetHistory.ToList();

            try
            {
                if (FileProtection.Exists(HistoryFile))
                {
                    var json = FileProtection.ReadAllText(HistoryFile);
                    s_resetHistory = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
                }
                else
                {
                    s_resetHistory = new List<string>();
                }
            }
            catch
            {
                s_resetHistory = new List<string>();
            }
            return s_resetHistory.ToList();
        }
    }

    private static void AddHistory(string entry)
    {
        lock (s_lock)
        {
            s_resetHistory ??= new List<string>();
            s_resetHistory.Insert(0, entry);
            if (s_resetHistory.Count > 200)
                s_resetHistory.RemoveRange(200, s_resetHistory.Count - 200);

            try
            {
                Directory.CreateDirectory(StoragePath);
                FileProtection.WriteAllText(HistoryFile, JsonSerializer.Serialize(s_resetHistory));
            }
            catch { }
        }
    }

    private static List<string> LoadEmailStatus()
    {
        lock (s_lock)
        {
            if (s_emailStatus != null)
                return s_emailStatus.ToList();

            try
            {
                if (FileProtection.Exists(EmailStatusFile))
                {
                    var json = FileProtection.ReadAllText(EmailStatusFile);
                    s_emailStatus = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
                }
                else
                {
                    s_emailStatus = new List<string>();
                }
            }
            catch
            {
                s_emailStatus = new List<string>();
            }
            return s_emailStatus.ToList();
        }
    }

    private static void AddEmailStatus(string entry)
    {
        lock (s_lock)
        {
            s_emailStatus ??= new List<string>();
            s_emailStatus.Insert(0, entry);
            if (s_emailStatus.Count > 100)
                s_emailStatus.RemoveRange(100, s_emailStatus.Count - 100);

            try
            {
                Directory.CreateDirectory(StoragePath);
                FileProtection.WriteAllText(EmailStatusFile, JsonSerializer.Serialize(s_emailStatus));
            }
            catch { }
        }
    }

    private void AddUserToGroup()
    {
        if (!ValidateEmployeeId()) return;
        if (string.IsNullOrWhiteSpace(GroupName))
        {
            ErrorMessage = "请输入用户组名称。";
            return;
        }

        try
        {
            using var context = new PrincipalContext(ContextType.Domain);
            using var user = UserPrincipal.FindByIdentity(context, IdentityType.SamAccountName, SearchEmployeeId);
            if (user == null)
            {
                ErrorMessage = $"未找到员工号 '{SearchEmployeeId}' 对应的用户。";
                return;
            }

            using var group = GroupPrincipal.FindByIdentity(context, GroupName);
            if (group == null)
            {
                ErrorMessage = $"未找到用户组 '{GroupName}'。";
                return;
            }

            if (user.IsMemberOf(group))
            {
                ErrorMessage = $"用户 '{SearchEmployeeId}' 已在用户组 '{GroupName}' 中。";
                return;
            }

            group.Members.Add(user);
            group.Save();

            ResultMessage = $"已将用户 '{SearchEmployeeId}' 添加到用户组 '{GroupName}'。";
            var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            AddGroupHistory($"{now} | 账号: {SearchEmployeeId} ({user.DisplayName}) | 加入组: {GroupName}");
            WriteAuditLog($"加用户组 | 操作人: {CurrentEmployeeId} | 账号: {SearchEmployeeId} | 组: {GroupName}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "添加用户组失败");
            ErrorMessage = "添加用户组失败：" + ex.Message;
        }
    }

    private void ClearResetHistory()
    {
        lock (s_lock)
        {
            s_resetHistory = new List<string>();
            s_emailStatus = new List<string>();
            s_offboardHistory = new List<string>();
            s_groupHistory = new List<string>();
            try
            {
                if (FileProtection.Exists(HistoryFile))
                    FileProtection.Delete(HistoryFile);
                if (FileProtection.Exists(EmailStatusFile))
                    FileProtection.Delete(EmailStatusFile);
                if (FileProtection.Exists(OffboardHistoryFile))
                    FileProtection.Delete(OffboardHistoryFile);
                if (FileProtection.Exists(GroupHistoryFile))
                    FileProtection.Delete(GroupHistoryFile);
            }
            catch { }
        }
        ResetHistory = new List<string>();
        EmailStatus = new List<string>();
        OffboardHistory = new List<string>();
        ResultMessage = "记录已清空。";
    }
}
