using System.DirectoryServices.AccountManagement;
using System.Runtime.Versioning;
using AccountManagement.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AccountManagement.Pages;

[SupportedOSPlatform("windows")]
public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(ILogger<IndexModel> logger)
    {
        _logger = logger;
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
            $"密码上次设置: {TimeHelper.ToBeijingTimeString(user.LastPasswordSet)}",
            $"上次登录时间: {TimeHelper.ToBeijingTimeString(user.LastLogon)}",
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
                    try { EmailSender.SendSelfPasswordChange(empId, pwd, userEmail); }
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
            return "密码包含连续或重复字符（如 abcd、1234、111），请更换。";

        return null;
    }

    private static bool HasSequentialPattern(string s)
    {
        var lower = s.ToLowerInvariant();

        // 检测3个连续相同字符（如 111、aaa、@@@）
        for (int i = 0; i < lower.Length - 2; i++)
        {
            if (lower[i] == lower[i + 1] &&
                lower[i] == lower[i + 2])
                return true;
        }

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
        string[] kbHorizPatterns =
        {
            "qwer", "wert", "erty", "rtyu", "tyui", "yuio", "uiop",
            "asdf", "sdfg", "dfgh", "fghj", "ghjk", "hjkl",
            "zxcv", "xcvb", "cvbn", "vbnm"
        };

        foreach (var pattern in kbHorizPatterns)
        {
            if (lower.Contains(pattern))
                return true;

            var reversed = new string(pattern.Reverse().ToArray());
            if (lower.Contains(reversed))
                return true;
        }

        // 检测常见键盘纵向连续（如 1qaz、2wsx 等）
        string[] kbVertPatterns =
        {
            "1qaz", "2wsx", "3edc", "4rfv", "5tgb", "6yhn", "7ujm"
        };

        foreach (var pattern in kbVertPatterns)
        {
            if (lower.Contains(pattern))
                return true;

            var reversed = new string(pattern.Reverse().ToArray());
            if (lower.Contains(reversed))
                return true;
        }

        // 检测3位及以上整词重复（如 abcabc、123123、qweqwe）
        for (int len = 3; len <= 4; len++)
        {
            for (int i = 0; i <= lower.Length - len * 2; i++)
            {
                bool match = true;
                for (int j = 0; j < len; j++)
                {
                    if (lower[i + j] != lower[i + len + j])
                    { match = false; break; }
                }
                if (match) return true;
            }
        }

        return false;
    }

    private static void WriteAuditLog(string entry)
    {
        try
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "App_Data");
            Directory.CreateDirectory(dir);
            var line = $"{TimeHelper.BeijingNow:yyyy-MM-dd HH:mm:ss} | {entry}{Environment.NewLine}";
            FileProtection.AppendAllText(Path.Combine(dir, "audit.dat"), line);
        }
        catch { }
    }

}
