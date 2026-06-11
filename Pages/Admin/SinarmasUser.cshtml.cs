using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AccountManagement.Pages.Admin;

public class SinarmasUserModel : PageModel
{
    public bool IsAuthenticated { get; set; }
    public string? CurrentEmployeeId { get; set; }
    public string? CurrentDisplayName { get; set; }

    [BindProperty]
    public string? SearchEmployeeId { get; set; }

    public string? UserDetail { get; set; }
    public string? ResultMessage { get; set; }
    public string? ErrorMessage { get; set; }

    public void OnGet()
    {
        CheckAuth();
        if (!IsAuthenticated) return;

        if (TempData["UserDetail"] is string ud) UserDetail = ud;
        if (TempData["ResultMessage"] is string rm) ResultMessage = rm;
        if (TempData["ErrorMessage"] is string em) ErrorMessage = em;
        if (TempData["SearchEmployeeId"] is string se) SearchEmployeeId = se;
    }

    public IActionResult OnPost(string action)
    {
        CheckAuth();
        if (!IsAuthenticated) return RedirectToPage("/Admin/Login");

        if (action == "search")
            SearchUser();
        else if (action == "unlock")
            UnlockUser();

        if (UserDetail != null) TempData["UserDetail"] = UserDetail;
        if (ResultMessage != null) TempData["ResultMessage"] = ResultMessage;
        if (ErrorMessage != null) TempData["ErrorMessage"] = ErrorMessage;
        if (SearchEmployeeId != null) TempData["SearchEmployeeId"] = SearchEmployeeId;

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

    private void SearchUser()
    {
        if (string.IsNullOrWhiteSpace(SearchEmployeeId))
        {
            ErrorMessage = "请输入员工号。";
            return;
        }

        try
        {
            var result = RunWithTimeout(() => QuerySinarmasUser(SearchEmployeeId), TimeSpan.FromSeconds(10));

            if (result.Timeout)
            {
                ErrorMessage = "查询超时（10秒），sinarmas-agri.com 域控制器可能不可达。";
                return;
            }

            if (result.Error != null)
            {
                ErrorMessage = $"查询失败：{result.Error}";
                return;
            }

            if (result.Lines == null || result.Lines.Count == 0)
            {
                ErrorMessage = $"[sinarmas-agri.com] 未找到员工号 '{SearchEmployeeId}' 对应的用户。";
                return;
            }

            UserDetail = string.Join(Environment.NewLine, result.Lines);
            ResultMessage = "查询成功。";
        }
        catch (Exception ex)
        {
            ErrorMessage = "查询失败：" + ex.Message;
        }
    }

    private void UnlockUser()
    {
        if (string.IsNullOrWhiteSpace(SearchEmployeeId))
        {
            ErrorMessage = "请输入员工号。";
            return;
        }

        try
        {
            var result = RunWithTimeout(() =>
            {
                using var context = new PrincipalContext(ContextType.Domain, "sinarmas-agri.com");
                using var user = UserPrincipal.FindByIdentity(context, IdentityType.SamAccountName, SearchEmployeeId);
                if (user == null) return ("[sinarmas-agri.com] 未找到该用户。", false);

                if (!user.IsAccountLockedOut())
                    return ("账号未锁定，无需解锁。", false);

                user.UnlockAccount();
                user.Save();
                return ("账号已成功解锁！", true);
            }, TimeSpan.FromSeconds(10));

            if (result.Timeout)
            {
                ErrorMessage = "操作超时（10秒），sinarmas-agri.com 域控制器可能不可达。";
                return;
            }

            ResultMessage = result.Message;
        }
        catch (Exception ex)
        {
            ErrorMessage = "解锁失败：" + ex.Message;
        }
    }

    private static QueryResult QuerySinarmasUser(string employeeId)
    {
        using var context = new PrincipalContext(ContextType.Domain, "sinarmas-agri.com");
        using var user = UserPrincipal.FindByIdentity(context, IdentityType.SamAccountName, employeeId);
        if (user == null)
            return new QueryResult { Lines = null };

        var entry = (DirectoryEntry)user.GetUnderlyingObject();
        var lines = new List<string>
        {
            $"=== sinarmas-agri.com 用户信息 ===",
            $"显示名称: {user.DisplayName}",
            $"员工号: {user.EmployeeId}",
            $"启用状态: {(user.Enabled == true ? "是" : "否")}",
            $"密码上次设置: {(user.LastPasswordSet?.ToString("yyyy-MM-dd HH:mm:ss") ?? "未知")}",
            $"上次登录时间: {(user.LastLogon?.ToString("yyyy-MM-dd HH:mm:ss") ?? "未知")}",
            $"是否锁定: {(user.IsAccountLockedOut() ? "是 (已锁定)" : "否 (正常)")}",
            $"密码永不过期: {(user.PasswordNeverExpires ? "是" : "否")}",
            $"邮箱: {user.EmailAddress}"
        };

        // 所属组
        try
        {
            var groups = user.GetGroups().OfType<GroupPrincipal>().Select(g => g.SamAccountName).OrderBy(n => n).ToList();
            if (groups.Count > 0)
            {
                lines.Add("所属组:");
                foreach (var g in groups)
                    lines.Add($"  - {g}");
            }
        }
        catch
        {
            lines.Add("所属组: (无法获取)");
        }

        return new QueryResult { Lines = lines };
    }

    private static (string? Message, bool Timeout) RunWithTimeout(Func<(string, bool)> action, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource();
        var task = Task.Run(action, cts.Token);
        if (task.Wait(timeout))
        {
            var r = task.Result;
            return (r.Item1, false);
        }
        cts.Cancel();
        return (null, true);
    }

    private static QueryResult RunWithTimeout(Func<QueryResult> action, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource();
        var task = Task.Run(action, cts.Token);
        if (task.Wait(timeout))
        {
            return task.Result;
        }
        cts.Cancel();
        return new QueryResult { Timeout = true };
    }

    private class QueryResult
    {
        public List<string>? Lines { get; set; }
        public string? Error { get; set; }
        public bool Timeout { get; set; }
    }
}
