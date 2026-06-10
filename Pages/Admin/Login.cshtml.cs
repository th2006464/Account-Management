using System.DirectoryServices.AccountManagement;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AccountManagement.Pages.Admin;

public class LoginModel : PageModel
{
    private static readonly object s_lock = new();
    private static string AdminFile => Path.Combine(AppContext.BaseDirectory, "App_Data", "admins.json");

    [BindProperty]
    public string? EmployeeId { get; set; }

    [BindProperty]
    public string? Password { get; set; }

    public string? ErrorMessage { get; set; }

    public void OnGet()
    {
    }

    public IActionResult OnGetLogout()
    {
        HttpContext.Session.Clear();
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

            HttpContext.Session.SetString("AdminLoggedIn", "true");
            HttpContext.Session.SetString("AdminEmployeeId", EmployeeId);

            WriteLoginLog(EmployeeId);

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
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | 管理员登录 | 账号: {employeeId}{Environment.NewLine}";
            lock (s_lock)
            {
                System.IO.File.AppendAllText(Path.Combine(dir, "audit.log"), line);
            }
        }
        catch { }
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

                if (System.IO.File.Exists(AdminFile))
                {
                    var json = System.IO.File.ReadAllText(AdminFile);
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
                System.IO.File.WriteAllText(AdminFile, JsonSerializer.Serialize(list));
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
