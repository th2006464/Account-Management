using AccountManagement.Helpers;
using System.DirectoryServices;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AccountManagement.Pages.Admin;

public class NewUserModel : PageModel
{
    private readonly ILogger<NewUserModel> _logger;
    private readonly IConfiguration _configuration;

    private static readonly object s_lock = new();
    private static List<string>? s_createHistory;
    private static List<string>? s_emailStatus;

    private static string StoragePath => Path.Combine(AppContext.BaseDirectory, "App_Data");
    private static string CreateHistoryFile => Path.Combine(StoragePath, "create_history.dat");
    private static string NewUserEmailStatusFile => Path.Combine(StoragePath, "newuser_email_status.dat");

    public NewUserModel(ILogger<NewUserModel> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public bool IsAuthenticated { get; set; }
    public string? CurrentEmployeeId { get; set; }
    public string? CurrentDisplayName { get; set; }

    [BindProperty]
    public string? CnName { get; set; }

    [BindProperty]
    public string? EnName { get; set; }

    [BindProperty]
    public string? EmployeeId { get; set; }

    [BindProperty]
    public string? Mobile { get; set; }

    public string? ResultMessage { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string>? CreateResults { get; set; }
    public List<string>? CreateHistory { get; set; }
    public List<string>? EmailStatus { get; set; }

    public void OnGet()
    {
        CheckAuth();
        if (!IsAuthenticated) return;
        CreateHistory = LoadCreateHistory();
        EmailStatus = LoadEmailStatus();

        if (TempData["CreateResults"] is string cr)
            CreateResults = new List<string> { cr };
        if (TempData["ResultMessage"] is string rm)
            ResultMessage = rm;
        if (TempData["ErrorMessage"] is string em)
            ErrorMessage = em;
    }

    public IActionResult OnPost(string action)
    {
        CheckAuth();
        if (!IsAuthenticated) return RedirectToPage();
        CreateHistory = LoadCreateHistory();
        EmailStatus = LoadEmailStatus();

        if (action == "clear")
        {
            ClearCreateHistory();
            return RedirectToPage();
        }

        if (string.IsNullOrWhiteSpace(CnName) || string.IsNullOrWhiteSpace(EnName) || string.IsNullOrWhiteSpace(EmployeeId))
        {
            ErrorMessage = "用户信息不完整，请填写所有必填字段。";
            return Page();
        }

        CreateUser();

        if (CreateResults is { Count: > 0 })
            TempData["CreateResults"] = string.Join(Environment.NewLine, CreateResults);
        if (ResultMessage != null)
            TempData["ResultMessage"] = ResultMessage;
        if (ErrorMessage != null)
            TempData["ErrorMessage"] = ErrorMessage;

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

    private void CreateUser()
    {
        CreateResults = new List<string>();
        var enName = EnName!.Trim().ToLower();
        var cnName = CnName!.Trim();
        var employeeId = EmployeeId!.Trim();
        var mobile = Mobile?.Trim() ?? "";
        var emailAddr = $"{enName}@sinarmas-agri.com";
        var ouPath = _configuration["AdSettings:NewUserOU"] ?? "OU=TESTOU,DC=garchina,DC=com";
        var domain = _configuration["AdSettings:Domain"] ?? "garchina.com";

        // 解析英文名 First.Last
        var nameParts = enName.Split('.');
        var givenName = nameParts.Length > 0 ? nameParts[0] : enName;
        var surname = nameParts.Length > 1 ? nameParts[1] : "";

        var password = GenerateRandomPassword();

        try
        {
            using var ouEntry = new DirectoryEntry($"LDAP://{ouPath}");
            using var newUser = ouEntry.Children.Add($"CN={enName}", "user");

            newUser.Properties["sAMAccountName"].Value = employeeId;
            newUser.Properties["userPrincipalName"].Value = $"{enName}@{domain}";
            newUser.Properties["givenName"].Value = givenName;
            newUser.Properties["sn"].Value = surname;
            newUser.Properties["displayName"].Value = $"{cnName}({enName})";
            newUser.Properties["description"].Value = cnName;
            newUser.Properties["mail"].Value = emailAddr;
            newUser.Properties["employeeID"].Value = employeeId;
            newUser.Properties["telephoneNumber"].Value = mobile;
            newUser.Properties["pager"].Value = "O365";
            newUser.CommitChanges();

            // 重新获取用户并设置密码
            using var pwdUser = new DirectoryEntry(newUser.Path);
            pwdUser.Invoke("SetPassword", new object[] { password });

            // 启用账号（UF_NORMAL_ACCOUNT）
            pwdUser.Properties["userAccountControl"].Value = 512;
            pwdUser.CommitChanges();

            var now = TimeHelper.BeijingNow.ToString("yyyy-MM-dd HH:mm:ss");
            CreateResults.Add($"用户创建成功！");
            CreateResults.Add($"中文名: {cnName}");
            CreateResults.Add($"英文名: {enName}");
            CreateResults.Add($"员工号: {employeeId}");
            CreateResults.Add($"邮箱: {emailAddr}");
            CreateResults.Add($"手机号: {mobile}");
            CreateResults.Add($"新密码: {password}");

            AddCreateHistory($"{now} | 创建用户: {employeeId} | {cnName}({enName}) | 邮箱: {emailAddr} | 密码: {password}");
            CreateHistory = LoadCreateHistory();

            // 异步发送邮件
            var en = enName;
            var cn = cnName;
            var emp = employeeId;
            var pwd = password;
            var email = emailAddr;
            _ = Task.Run(() =>
            {
                try
                {
                    EmailSender.SendNewUserCreated(cn, en, emp, pwd, email);
                    AddEmailStatus($"{TimeHelper.BeijingNow:yyyy-MM-dd HH:mm:ss} | 邮件发送成功 | {cn}({en})");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "创建用户邮件发送失败");
                    AddEmailStatus($"{TimeHelper.BeijingNow:yyyy-MM-dd HH:mm:ss} | 邮件发送失败: {ex.Message} | {cn}({en})");
                }
            });

            CreateResults.Add("邮件正在后台发送...");
            WriteAuditLog($"创建用户 | 操作人: {CurrentEmployeeId} | 账号: {employeeId} | {cnName}({enName}) | 密码: {password}");
            ResultMessage = "用户创建完成。";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建用户失败");
            ErrorMessage = "创建用户失败：" + ex.Message;
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

    // ---- 审计日志（永不清除） ----

    private static string AuditLogPath => Path.Combine(StoragePath, "audit.dat");

    private static void WriteAuditLog(string entry)
    {
        try
        {
            Directory.CreateDirectory(StoragePath);
            var line = $"{TimeHelper.BeijingNow:yyyy-MM-dd HH:mm:ss} | {entry}{Environment.NewLine}";
            lock (s_lock)
            {
                FileProtection.AppendAllText(AuditLogPath, line);
            }
        }
        catch { }
    }

    private void ClearCreateHistory()
    {
        lock (s_lock)
        {
            s_createHistory = new List<string>();
            s_emailStatus = new List<string>();
            try
            {
                if (FileProtection.Exists(CreateHistoryFile))
                    FileProtection.Delete(CreateHistoryFile);
                if (FileProtection.Exists(NewUserEmailStatusFile))
                    FileProtection.Delete(NewUserEmailStatusFile);
            }
            catch { }
        }
        CreateHistory = new List<string>();
        EmailStatus = new List<string>();
        ResultMessage = "创建记录已清空。";
    }

    // ---- 文件持久化 ----

    private static List<string> LoadCreateHistory()
    {
        lock (s_lock)
        {
            if (s_createHistory != null)
                return s_createHistory.ToList();

            try
            {
                if (FileProtection.Exists(CreateHistoryFile))
                {
                    var json = FileProtection.ReadAllText(CreateHistoryFile);
                    s_createHistory = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
                }
                else
                {
                    s_createHistory = new List<string>();
                }
            }
            catch
            {
                s_createHistory = new List<string>();
            }
            return s_createHistory.ToList();
        }
    }

    private static void AddCreateHistory(string entry)
    {
        lock (s_lock)
        {
            s_createHistory ??= new List<string>();
            s_createHistory.Insert(0, entry);
            if (s_createHistory.Count > 200)
                s_createHistory.RemoveRange(200, s_createHistory.Count - 200);

            try
            {
                Directory.CreateDirectory(StoragePath);
                FileProtection.WriteAllText(CreateHistoryFile, JsonSerializer.Serialize(s_createHistory));
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
                if (FileProtection.Exists(NewUserEmailStatusFile))
                {
                    var json = FileProtection.ReadAllText(NewUserEmailStatusFile);
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
                FileProtection.WriteAllText(NewUserEmailStatusFile, JsonSerializer.Serialize(s_emailStatus));
            }
            catch { }
        }
    }
}
