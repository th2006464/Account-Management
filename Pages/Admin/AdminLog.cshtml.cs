using AccountManagement.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AccountManagement.Pages.Admin;

public class AdminLogModel : PageModel
{
    private const int PageSize = 200;

    public bool IsAuthenticated { get; set; }
    public string? CurrentEmployeeId { get; set; }
    public string? CurrentDisplayName { get; set; }

    public List<string> AllLogs { get; set; } = new();
    public List<string> PageLogs { get; set; } = new();
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public int TotalCount { get; set; }

    public string AuditLogPath => AuditLogFile;

    private static string AuditLogFile => Path.Combine(AppContext.BaseDirectory, "App_Data", "audit.dat");

    public void OnGet(int page = 1)
    {
        CheckAuth();
        if (!IsAuthenticated) return;

        CurrentPage = page < 1 ? 1 : page;
        LoadLogs();
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

        if (CurrentEmployeeId != "15005035")
            IsAuthenticated = false;
    }

    private void LoadLogs()
    {
        // 合并所有审计日志
        try
        {
            if (FileProtection.Exists(AuditLogFile))
            {
                var lines = FileProtection.ReadAllLines(AuditLogFile);
                AllLogs = lines.Reverse().ToList();
            }
        }
        catch { }

        // 合并邮件发送状态
        try
        {
            var emails = new List<(string, string)>(); // (时间前缀, 内容)

            var resetEmailFile = Path.Combine(AppContext.BaseDirectory, "App_Data", "email_status.dat");
            if (FileProtection.Exists(resetEmailFile))
            {
                var json = FileProtection.ReadAllText(resetEmailFile);
                var list = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json);
                if (list != null)
                    foreach (var item in list)
                        emails.Add((item, "[密码重置]"));
            }

            var createEmailFile = Path.Combine(AppContext.BaseDirectory, "App_Data", "newuser_email_status.dat");
            if (FileProtection.Exists(createEmailFile))
            {
                var json = FileProtection.ReadAllText(createEmailFile);
                var list = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json);
                if (list != null)
                    foreach (var item in list)
                        emails.Add((item, "[创建用户]"));
            }

            foreach (var (item, tag) in emails)
            {
                AllLogs.Add($"{item} | {tag}");
            }
        }
        catch { }

        // 按时间倒序排列
        AllLogs = AllLogs.OrderByDescending(x => x).ToList();

        TotalCount = AllLogs.Count;
        TotalPages = (int)Math.Ceiling((double)TotalCount / PageSize);
        if (CurrentPage > TotalPages) CurrentPage = TotalPages;
        if (CurrentPage < 1) CurrentPage = 1;

        var skip = (CurrentPage - 1) * PageSize;
        PageLogs = AllLogs.Skip(skip).Take(PageSize).ToList();
    }
}
