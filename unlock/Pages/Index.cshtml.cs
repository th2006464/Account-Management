using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Unlock.Pages;

public class IndexModel : PageModel
{
    [BindProperty]
    public string? SearchEmployeeId { get; set; }

    public string? UserDetail { get; set; }
    public string? ResultMessage { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ResultDomain { get; set; }

    public void OnGet()
    {
        if (TempData["UserDetail"] is string ud) UserDetail = ud;
        if (TempData["ResultMessage"] is string rm) ResultMessage = rm;
        if (TempData["ErrorMessage"] is string em) ErrorMessage = em;
        if (TempData["SearchEmployeeId"] is string se) SearchEmployeeId = se;
        if (TempData["ResultDomain"] is string rd) ResultDomain = rd;
    }

    public IActionResult OnPost(string domain)
    {
        if (string.IsNullOrWhiteSpace(SearchEmployeeId))
        {
            ErrorMessage = "请输入员工号。";
            TempData["ErrorMessage"] = ErrorMessage;
            return RedirectToPage();
        }

        try
        {
            bool isGarchina = domain == "garchina";
            string domainName = isGarchina ? "garchina.com" : "sinarmas-agri.com";
            TimeSpan timeout = isGarchina ? TimeSpan.FromSeconds(15) : TimeSpan.FromSeconds(55);

            var result = RunWithTimeout(() => QueryAndUnlock(SearchEmployeeId, domainName), timeout);

            if (result.Timeout)
                ErrorMessage = $"查询超时，{domainName} 域控制器可能不可达。";
            else if (result.Error != null)
                ErrorMessage = result.Error;
            else
            {
                UserDetail = string.Join(Environment.NewLine, result.Lines);
                ResultDomain = isGarchina ? "GARCHINA" : "Sinarmas";
                ResultMessage = result.UnlockMessage ?? "查询成功。";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = "查询失败：" + ex.Message;
        }

        TempData["UserDetail"] = UserDetail;
        TempData["ResultMessage"] = ResultMessage;
        TempData["ErrorMessage"] = ErrorMessage;
        TempData["SearchEmployeeId"] = SearchEmployeeId;
        TempData["ResultDomain"] = ResultDomain;

        return RedirectToPage();
    }

    private static QueryResult QueryAndUnlock(string employeeId, string domainName)
    {
        using var context = new PrincipalContext(ContextType.Domain, domainName);
        using var user = UserPrincipal.FindByIdentity(context, IdentityType.SamAccountName, employeeId);
        if (user == null)
            return new QueryResult { Error = $"[{domainName}] 未找到员工号 '{employeeId}' 对应的用户。" };

        var entry = (DirectoryEntry)user.GetUnderlyingObject();
        var lines = new List<string>
        {
            $"=== {domainName} 用户信息 ===",
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

        // 自动解锁
        string? unlockMsg = null;
        if (user.IsAccountLockedOut())
        {
            try
            {
                user.UnlockAccount();
                user.Save();
                unlockMsg = "账号已锁定，已自动解锁。";
            }
            catch (Exception ex)
            {
                unlockMsg = $"账号已锁定，解锁失败：{ex.Message}";
            }
        }

        return new QueryResult { Lines = lines, UnlockMessage = unlockMsg };
    }

    private static QueryResult RunWithTimeout(Func<QueryResult> action, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource();
        var task = Task.Run(action, cts.Token);
        if (task.Wait(timeout))
            return task.Result;

        cts.Cancel();
        return new QueryResult { Timeout = true };
    }

    private class QueryResult
    {
        public List<string> Lines { get; set; } = new();
        public string? Error { get; set; }
        public string? UnlockMessage { get; set; }
        public bool Timeout { get; set; }
    }
}
