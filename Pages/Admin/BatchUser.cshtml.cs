using System.DirectoryServices.AccountManagement;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using AccountManagement.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AccountManagement.Pages.Admin;

public class BatchUserModel : PageModel
{
    private readonly ILogger<BatchUserModel> _logger;
    private readonly IConfiguration _configuration;

    public BatchUserModel(ILogger<BatchUserModel> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public bool IsAuthenticated { get; set; }
    public string? CurrentEmployeeId { get; set; }
    public string? CurrentDisplayName { get; set; }

    [BindProperty]
    public string? InputIds { get; set; }

    public List<string> Results { get; set; } = new();
    public string? ResultMessage { get; set; }
    public string? ErrorMessage { get; set; }
    public int SuccessCount { get; set; }
    public int FailCount { get; set; }

    public void OnGet()
    {
        CheckAuth();
    }

    public IActionResult OnPost(string action)
    {
        CheckAuth();
        if (!IsAuthenticated) return RedirectToPage("/Admin/Login");

        if (string.IsNullOrWhiteSpace(InputIds))
        {
            ErrorMessage = "请输入至少一个员工号。";
            return Page();
        }

        var ids = InputIds.Split(new[] { '\n', '\r', ' ', ',', ';', '\t' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim()).Where(s => s.Length > 0).Distinct().ToList();

        if (ids.Count == 0)
        {
            ErrorMessage = "未识别到有效的员工号。";
            return Page();
        }

        Results = new List<string>();
        SuccessCount = 0;
        FailCount = 0;

        if (action == "search") BatchSearch(ids);
        else if (action == "enable") BatchEnable(ids);
        else if (action == "disable") BatchDisable(ids);
        else if (action == "reset") BatchReset(ids);

        if (ResultMessage != null) TempData["ResultMessage"] = ResultMessage;
        if (ErrorMessage != null) TempData["ErrorMessage"] = ErrorMessage;
        if (Results.Count > 0) TempData["BatchResults"] = string.Join("\n", Results);
        TempData["InputIds"] = InputIds;
        TempData["SuccessCount"] = SuccessCount;
        TempData["FailCount"] = FailCount;

        return RedirectToPage();
    }

    private void CheckAuth()
    {
        var loggedIn = HttpContext.Session.GetString("AdminLoggedIn");
        if (loggedIn == "true")
        {
            IsAuthenticated = true;
            CurrentEmployeeId = HttpContext.Session.GetString("AdminEmployeeId");
            CurrentDisplayName = HttpContext.Session.GetString("AdminDisplayName");
        }

        if (TempData["BatchResults"] is string br) Results = br.Split('\n').ToList();
        if (TempData["ResultMessage"] is string rm) ResultMessage = rm;
        if (TempData["ErrorMessage"] is string em) ErrorMessage = em;
        if (TempData["InputIds"] is string ii) InputIds = ii;
        if (TempData["SuccessCount"] is int sc) SuccessCount = sc;
        if (TempData["FailCount"] is int fc) FailCount = fc;
    }

    private void BatchSearch(List<string> ids)
    {
        Results.Add($"查询结果 ({DateTime.Now:HH:mm:ss})");
        Results.Add(new string('-', 60));
        foreach (var id in ids)
        {
            try
            {
                using var ctx = new PrincipalContext(ContextType.Domain);
                using var user = UserPrincipal.FindByIdentity(ctx, IdentityType.SamAccountName, id);
                if (user == null) { Results.Add($"{id} — 未找到"); FailCount++; }
                else { Results.Add($"{id} | {user.DisplayName} | {(user.Enabled==true?"启用":"禁用")} | {(user.IsAccountLockedOut()?"锁定":"正常")} | {user.EmailAddress}"); SuccessCount++; }
            }
            catch (Exception ex) { Results.Add($"{id} — 错误: {ex.Message}"); FailCount++; }
        }
        ResultMessage = $"查询完成: 成功 {SuccessCount}, 失败 {FailCount}";
    }

    private void BatchEnable(List<string> ids)
    {
        Results.Add($"启用结果 ({DateTime.Now:HH:mm:ss})");
        Results.Add(new string('-', 60));
        foreach (var id in ids)
        {
            try
            {
                using var ctx = new PrincipalContext(ContextType.Domain);
                using var user = UserPrincipal.FindByIdentity(ctx, IdentityType.SamAccountName, id);
                if (user == null) { Results.Add($"{id} — 未找到"); FailCount++; }
                else { user.Enabled = true; user.Save(); Results.Add($"{id} — 已启用"); SuccessCount++; }
            }
            catch (Exception ex) { Results.Add($"{id} — 错误: {ex.Message}"); FailCount++; }
        }
        ResultMessage = $"启用完成: 成功 {SuccessCount}, 失败 {FailCount}";
    }

    private void BatchDisable(List<string> ids)
    {
        Results.Add($"禁用结果 ({DateTime.Now:HH:mm:ss})");
        Results.Add(new string('-', 60));
        foreach (var id in ids)
        {
            try
            {
                using var ctx = new PrincipalContext(ContextType.Domain);
                using var user = UserPrincipal.FindByIdentity(ctx, IdentityType.SamAccountName, id);
                if (user == null) { Results.Add($"{id} — 未找到"); FailCount++; }
                else { user.Enabled = false; user.Save(); Results.Add($"{id} — 已禁用"); SuccessCount++; }
            }
            catch (Exception ex) { Results.Add($"{id} — 错误: {ex.Message}"); FailCount++; }
        }
        ResultMessage = $"禁用完成: 成功 {SuccessCount}, 失败 {FailCount}";
    }

    private void BatchReset(List<string> ids)
    {
        Results.Add($"密码重置结果 ({DateTime.Now:HH:mm:ss})");
        Results.Add(new string('-', 60));
        var resetList = new List<(string Id, string Name, string Pwd)>();

        foreach (var id in ids)
        {
            try
            {
                using var ctx = new PrincipalContext(ContextType.Domain);
                using var user = UserPrincipal.FindByIdentity(ctx, IdentityType.SamAccountName, id);
                if (user == null) { Results.Add($"{id} — 未找到"); FailCount++; continue; }

                if (user.IsAccountLockedOut()) { user.UnlockAccount(); user.Save(); }

                var pwd = GenerateRandomPassword();
                PasswordHelper.SetPasswordWithNotification(id, pwd);

                resetList.Add((id, user.DisplayName, pwd));
                Results.Add($"{id} | {user.DisplayName} | 新密码: {pwd}");
                SuccessCount++;
            }
            catch (Exception ex) { Results.Add($"{id} — 错误: {ex.Message}"); FailCount++; }
        }

        if (resetList.Count > 0)
        {
            var list = resetList;
            _ = Task.Run(() => { try { SendBatchEmail(list); } catch { } });
        }
        ResultMessage = $"重置完成: 成功 {SuccessCount}, 失败 {FailCount}";
    }

    private static string GenerateRandomPassword()
    {
        const string upper = "ABCDEFGHIJKMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnpqrstuvwxyz";
        const string numbers = "23456789";
        const string symbols = "@$%&*,./";
        const string all = upper + lower + numbers + symbols;
        var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        var chars = new char[12];
        chars[0] = upper[GetRand(rng, upper.Length)]; chars[1] = lower[GetRand(rng, lower.Length)];
        chars[2] = numbers[GetRand(rng, numbers.Length)]; chars[3] = symbols[GetRand(rng, symbols.Length)];
        for (int i = 4; i < 12; i++) chars[i] = all[GetRand(rng, all.Length)];
        for (int i = 11; i > 0; i--) { int j = GetRand(rng, i + 1); (chars[i], chars[j]) = (chars[j], chars[i]); }
        return new string(chars);
    }
    private static int GetRand(System.Security.Cryptography.RandomNumberGenerator r, int m) { var b = new byte[4]; r.GetBytes(b); return (int)(BitConverter.ToUInt32(b, 0) % (uint)m); }

    private void SendBatchEmail(List<(string Id, string Name, string Pwd)> list)
    {
        ServicePointManager.ServerCertificateValidationCallback = (_, _, _, _) => true;
        var srv = _configuration["EmailSettings:SmtpServer"] ?? "";
        var port = int.Parse(_configuration["EmailSettings:SmtpPort"] ?? "587");
        var from = _configuration["EmailSettings:FromAddress"] ?? "";
        var to = _configuration["EmailSettings:ToAddress"] ?? "";
        var cc = _configuration["EmailSettings:CcAddress"] ?? "";
        var user = _configuration["EmailSettings:Username"] ?? "";
        var pass = _configuration["EmailSettings:Password"] ?? "";

        var rows = new System.Text.StringBuilder();
        foreach (var (id, name, pwd) in list)
            rows.AppendLine($"{id} | {name} | 新密码: {pwd}");

        var body = $@"以下用户密码已批量重置：

{rows}

此密码适用于 GARCHINA 系统认证、China OA 系统、GARCHINA VPN、Workday 请休假系统。
请通知相关用户尽快修改密码。密码有效期 90 天。

此邮件由系统自动发送，请勿回复。";

        using var c = new SmtpClient(srv, port) { EnableSsl = true, Credentials = new NetworkCredential(user, pass) };
        using var m = new MailMessage(from, to) { Subject = "[IT信息] 批量密码重置通知", Body = body, BodyEncoding = System.Text.Encoding.UTF8 };
        m.CC.Add(cc);
        c.Send(m);
    }

    private void SendEmail(string empId, string pwd, string toEmail, string displayName)
    {
        ServicePointManager.ServerCertificateValidationCallback = (_, _, _, _) => true;
        var srv = _configuration["EmailSettings:SmtpServer"] ?? "";
        var port = int.Parse(_configuration["EmailSettings:SmtpPort"] ?? "587");
        var from = _configuration["EmailSettings:FromAddress"] ?? "";
        var to = _configuration["EmailSettings:ToAddress"] ?? "";
        var cc = _configuration["EmailSettings:CcAddress"] ?? "";
        var user = _configuration["EmailSettings:Username"] ?? "";
        var pass = _configuration["EmailSettings:Password"] ?? "";

        var body = $@"尊敬的用户，

您的 GARCHINA 账号 {empId} 的密码已重置。
新密码为：{pwd}

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

此邮件由系统自动发送，请勿回复。";

        using var c = new SmtpClient(srv, port) { EnableSsl = true, Credentials = new NetworkCredential(user, pass) };
        using var m = new MailMessage(from, to) { Subject = "[IT信息] 用户AD 账号密码重置通知", Body = body, BodyEncoding = System.Text.Encoding.UTF8 };
        m.CC.Add(cc);
        c.Send(m);
    }
}
