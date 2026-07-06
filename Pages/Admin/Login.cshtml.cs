using AccountManagement.Helpers;
using System.DirectoryServices.AccountManagement;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AccountManagement.Pages.Admin;

public class LoginModel : PageModel
{
    private static readonly object s_lock = new();
    private static string AdminFile => Path.Combine(AppContext.BaseDirectory, "App_Data", "admins.dat");
    private const string CookieName = "AdminAutoLogin";
    private const int CookieDays = 7;
    private static readonly byte[] s_cookieKey = SHA256.HashData(Encoding.UTF8.GetBytes("GARCHINA@2026_AutoLoginSecretKey"));

    [BindProperty]
    public string? EmployeeId { get; set; }

    [BindProperty]
    public string? Password { get; set; }

    [BindProperty]
    public string? ReturnUrl { get; set; }

    [BindProperty]
    public bool RememberMe { get; set; }

    public string? ErrorMessage { get; set; }

    public IActionResult OnGet(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;

        // 自动登录检查
        if (TryAutoLogin(out var empId))
        {
            HttpContext.Session.SetString("AdminLoggedIn", "true");
            HttpContext.Session.SetString("AdminEmployeeId", empId);

            using var ctx = new PrincipalContext(ContextType.Domain);
            using var user = UserPrincipal.FindByIdentity(ctx, IdentityType.SamAccountName, empId);
            var dn = empId;
            if (user != null && !string.IsNullOrEmpty(user.DisplayName))
                dn = $"{empId} | {user.DisplayName}";
            HttpContext.Session.SetString("AdminDisplayName", dn);

            WriteLoginLog(empId);

            if (!string.IsNullOrEmpty(returnUrl))
                return LocalRedirect(returnUrl);
            return RedirectToPage("/Admin/UserAdmin");
        }

        return Page();
    }

    public IActionResult OnGetLogout()
    {
        HttpContext.Session.Clear();
        HttpContext.Response.Cookies.Delete(CookieName);
        return RedirectToPage("/Admin/Login");
    }

    public IActionResult OnPost()
    {
        if (string.IsNullOrWhiteSpace(EmployeeId) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "请输入员工号和密码。";
            return Page();
        }

        if (!IsAdmin(EmployeeId))
        {
            ErrorMessage = "您没有管理员权限。";
            return Page();
        }

        try
        {
            using var context = new PrincipalContext(ContextType.Domain);
            if (!context.ValidateCredentials(EmployeeId, Password))
            {
                ErrorMessage = "密码不正确。";
                return Page();
            }

            using var user = UserPrincipal.FindByIdentity(context, IdentityType.SamAccountName, EmployeeId);
            var displayName = EmployeeId;
            if (user != null && !string.IsNullOrEmpty(user.DisplayName))
                displayName = $"{EmployeeId} | {user.DisplayName}";

            HttpContext.Session.SetString("AdminLoggedIn", "true");
            HttpContext.Session.SetString("AdminEmployeeId", EmployeeId);
            HttpContext.Session.SetString("AdminDisplayName", displayName);

            // 记住登录
            if (RememberMe)
                SetAutoLoginCookie(EmployeeId);

            WriteLoginLog(EmployeeId);

            if (!string.IsNullOrEmpty(ReturnUrl))
                return LocalRedirect(ReturnUrl);

            return RedirectToPage("/Admin/UserAdmin");
        }
        catch (Exception ex)
        {
            ErrorMessage = "登录验证失败：" + ex.Message;
            return Page();
        }
    }

    private static void WriteLoginLog(string employeeId)
    {
        try
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "App_Data");
            Directory.CreateDirectory(dir);
            var line = $"{TimeHelper.BeijingNow:yyyy-MM-dd HH:mm:ss} | 管理员登录 | 账号: {employeeId}{Environment.NewLine}";
            lock (s_lock)
            {
                FileProtection.AppendAllText(Path.Combine(dir, "audit.dat"), line);
            }
        }
        catch { }
    }

    // ---- 自动登录 Cookie ----

    private void SetAutoLoginCookie(string employeeId)
    {
        var expiry = DateTime.UtcNow.AddDays(CookieDays).Ticks.ToString();
        var payload = $"{employeeId}|{expiry}";
        var signature = Convert.ToBase64String(HMACSHA256.HashData(s_cookieKey, Encoding.UTF8.GetBytes(payload)));
        var cookieValue = $"{employeeId}|{expiry}|{signature}";

        HttpContext.Response.Cookies.Append(CookieName, cookieValue, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(CookieDays)
        });
    }

    private bool TryAutoLogin(out string employeeId)
    {
        employeeId = "";
        var cookie = HttpContext.Request.Cookies[CookieName];
        if (string.IsNullOrEmpty(cookie)) return false;

        var parts = cookie.Split('|');
        if (parts.Length != 3) return false;

        var empId = parts[0];
        var expStr = parts[1];
        var sig = parts[2];

        if (!long.TryParse(expStr, out var expiryTicks)) return false;
        if (DateTime.UtcNow.Ticks > expiryTicks) return false;

        var payload = $"{empId}|{expStr}";
        var expectedSig = Convert.ToBase64String(HMACSHA256.HashData(s_cookieKey, Encoding.UTF8.GetBytes(payload)));
        if (sig != expectedSig) return false;

        if (!IsAdmin(empId)) return false;

        employeeId = empId;
        return true;
    }

    // ---- 管理员列表管理（无缓存，每次读文件） ----

    public static bool IsAdmin(string employeeId)
    {
        var admins = LoadAdminList();
        return admins.Contains(employeeId);
    }

    public static List<string> LoadAdminList()
    {
        lock (s_lock)
        {
            try
            {
                var dir = Path.GetDirectoryName(AdminFile);
                if (dir != null) Directory.CreateDirectory(dir);

                if (FileProtection.Exists(AdminFile))
                {
                    var json = FileProtection.ReadAllText(AdminFile);
                    return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string> { "15005035" };
                }
            }
            catch { }
            return new List<string> { "15005035" };
        }
    }

    public static void SaveAdminList(List<string> list)
    {
        lock (s_lock)
        {
            try
            {
                var dir = Path.GetDirectoryName(AdminFile);
                if (dir != null) Directory.CreateDirectory(dir);
                FileProtection.WriteAllText(AdminFile, JsonSerializer.Serialize(list));
            }
            catch { }
        }
    }

    public static void AddAdmin(string employeeId)
    {
        lock (s_lock)
        {
            var list = LoadAdminList();
            if (!list.Contains(employeeId))
            {
                list.Add(employeeId);
                list.Sort();
                SaveAdminList(list);
            }
        }
    }

    public static void RemoveAdmin(string employeeId)
    {
        if (employeeId == "15005035") return; // 不能移除超级管理员
        lock (s_lock)
        {
            var list = LoadAdminList();
            list.Remove(employeeId);
            SaveAdminList(list);
        }
    }
}
