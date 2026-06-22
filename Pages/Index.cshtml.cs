using System.DirectoryServices.AccountManagement;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using AccountManagement.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AccountManagement.Pages;

[SupportedOSPlatform("windows")]
public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly IConfiguration _configuration;

    public IndexModel(ILogger<IndexModel> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    [BindProperty]
    public string? Username { get; set; }

    [BindProperty]
    public string? CurrentPassword { get; set; }

    [BindProperty]
    public string? NewPassword { get; set; }

    [BindProperty]
    public string? ConfirmPassword { get; set; }

    public string? AccountStatus { get; private set; }
    public string? ResultMessage { get; private set; }
    public string? ErrorMessage { get; private set; }
    public bool IsQueryResult { get; private set; }

    public void OnGet()
    {
        HttpContext.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
        HttpContext.Response.Headers["Pragma"] = "no-cache";
        HttpContext.Response.Headers["Expires"] = "0";
    }

    public void OnPost()
    {
        var action = Request.Form["action"].ToString();
        if (action == "status")
        {
            QueryAccountStatus();
        }
        else if (action == "update")
        {
            UpdatePassword();
        }
    }

    private void QueryAccountStatus()
    {
        IsQueryResult = true;
        if (string.IsNullOrWhiteSpace(Username))
        {
            ErrorMessage = "请输入GARCHINA员工号。";
            return;
        }

        try
        {
            using var context = new PrincipalContext(ContextType.Domain);
            using var user = UserPrincipal.FindByIdentity(context, IdentityType.SamAccountName, Username);
            if (user == null)
            {
                ErrorMessage = $"未找到员工号 '{Username}' 对应的用户。";
                return;
            }

            AccountStatus = string.Join(Environment.NewLine, BuildUserInfo(user));
            ResultMessage = "查询成功。";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查询用户状态失败");
            ErrorMessage = "查询用户状态时发生错误，请确认当前应用池身份有权限访问域并且用户名正确。";
        }
    }

    private static List<string> BuildUserInfo(UserPrincipal user)
    {
        var lines = new List<string>
        {
            $"显示名: {user.DisplayName}",
            $"员工号: {user.EmployeeId}",
            $"账号启用: {user.Enabled?.ToString() ?? "未知"}",
            $"锁定状态: {(user.IsAccountLockedOut() ? "已锁定" : "正常")}",
            $"密码上次设置: {user.LastPasswordSet?.ToString() ?? "未知"}",
            $"上次登录时间: {user.LastLogon?.ToString() ?? "未知"}",
            $"密码永不过期: {user.PasswordNeverExpires}"
        };

        return lines;
    }

    private void UpdatePassword()
    {
        if (string.IsNullOrWhiteSpace(Username))
        {
            ErrorMessage = "请输入GARCHINA员工号。";
            return;
        }

        if (string.IsNullOrWhiteSpace(CurrentPassword))
        {
            ErrorMessage = "请输入当前密码。";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewPassword) || string.IsNullOrWhiteSpace(ConfirmPassword))
        {
            ErrorMessage = "请输入新密码并确认。";
            return;
        }

        if (NewPassword != ConfirmPassword)
        {
            ErrorMessage = "两次输入的密码不一致。";
            return;
        }

        var pwdError = ValidatePasswordStrength(NewPassword);
        if (pwdError != null)
        {
            ErrorMessage = pwdError;
            return;
        }

        try
        {
            using var context = new PrincipalContext(ContextType.Domain);

            if (!context.ValidateCredentials(Username, CurrentPassword))
            {
                ErrorMessage = "当前密码不正确。";
                return;
            }

            using var user = UserPrincipal.FindByIdentity(context, IdentityType.SamAccountName, Username);
            if (user == null)
            {
                ErrorMessage = $"未找到员工号 '{Username}' 对应的用户。";
                return;
            }

            PasswordHelper.SetPasswordWithNotification(Username, NewPassword);
            WriteAuditLog($"密码更新 | 自助修改 | 账号: {Username} ({user.DisplayName}) | 新密码: {NewPassword}");

            // 异步发送邮件通知用户
            var userEmail = user.EmailAddress;
            var empId = Username;
            var pwd = NewPassword;
            if (!string.IsNullOrWhiteSpace(userEmail))
            {
                _ = Task.Run(() =>
                {
                    try { SendPasswordUpdateEmail(empId, pwd, userEmail); }
                    catch (Exception ex) { _logger.LogError(ex, "密码更新邮件发送失败"); }
                });
            }

            ResultMessage = $"密码已完成更新并同步至以下系统：\n" +
                            $"  - 邮箱\n" +
                            $"  - Workday 请休假系统\n" +
                            $"  - KUBE OA 系统\n\n" +
                            $"新密码明文：{NewPassword}\n\n" +
                            $"请妥善保存并记录。密码有效期：90天。";
        }
        catch (PasswordException ex)
        {
            _logger.LogWarning(ex, "密码设置失败");
            ErrorMessage = "密码策略不满足要求，请设置符合域密码复杂性策略的密码。";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新密码失败");
            ErrorMessage = "更新密码时发生错误，请确认应用池身份有权限重置密码。";
        }
    }

    private static string? ValidatePasswordStrength(string password)
    {
        if (password.Length < 9)
            return "密码长度必须至少9位。";

        if (!password.Any(char.IsUpper))
            return "密码必须包含大写字母。";

        if (!password.Any(char.IsLower))
            return "密码必须包含小写字母。";

        if (!password.Any(char.IsDigit))
            return "密码必须包含数字。";

        if (!password.Any(c => !char.IsLetterOrDigit(c)))
            return "密码必须包含符号（如 !@#$% 等）。";

        if (HasSequentialPattern(password))
            return "密码包含连续字符（如 abcd、1234、qwer），请更换。";

        return null;
    }

    private static bool HasSequentialPattern(string s)
    {
        var lower = s.ToLowerInvariant();

        // 检测4个字符的连续递增/递减
        for (int i = 0; i < lower.Length - 3; i++)
        {
            if (lower[i + 1] == lower[i] + 1 &&
                lower[i + 2] == lower[i] + 2 &&
                lower[i + 3] == lower[i] + 3)
                return true;

            if (lower[i + 1] == lower[i] - 1 &&
                lower[i + 2] == lower[i] - 2 &&
                lower[i + 3] == lower[i] - 3)
                return true;
        }

        // 检测常见键盘横向连续
        string[] kbPatterns =
        {
            "qwer", "wert", "erty", "rtyu", "tyui", "yuio", "uiop",
            "asdf", "sdfg", "dfgh", "fghj", "ghjk", "hjkl",
            "zxcv", "xcvb", "cvbn", "vbnm"
        };

        foreach (var pattern in kbPatterns)
        {
            if (lower.Contains(pattern))
                return true;

            var reversed = new string(pattern.Reverse().ToArray());
            if (lower.Contains(reversed))
                return true;
        }

        return false;
    }

    private static void WriteAuditLog(string entry)
    {
        try
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "App_Data");
            Directory.CreateDirectory(dir);
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {entry}{Environment.NewLine}";
            FileProtection.AppendAllText(Path.Combine(dir, "audit.dat"), line);
        }
        catch { }
    }

    private void SendPasswordUpdateEmail(string employeeId, string newPassword, string toEmail)
    {
        ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, errors) => true;

        var smtpServer = _configuration["EmailSettings:SmtpServer"] ?? "";
        var smtpPort = int.Parse(_configuration["EmailSettings:SmtpPort"] ?? "587");
        var fromAddress = _configuration["EmailSettings:FromAddress"] ?? "";
        var username = _configuration["EmailSettings:Username"] ?? "";
        var password = _configuration["EmailSettings:Password"] ?? "";

        var body = $@"尊敬的用户，

您的 GARCHINA 账号 {employeeId} 密码已更新。
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

此邮件由系统自动发送，请勿回复。";

        using var client = new SmtpClient(smtpServer, smtpPort)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(username, password)
        };

        using var message = new MailMessage(fromAddress, toEmail)
        {
            Subject = "[IT信息] 用户AD账号密码更新通知",
            Body = body,
            BodyEncoding = System.Text.Encoding.UTF8,
            IsBodyHtml = false
        };

        client.Send(message);
    }
}
