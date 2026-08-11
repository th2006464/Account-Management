using System.DirectoryServices.AccountManagement;
using AccountManagement.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AccountManagement.Pages.Admin;

public class EditUserModel : PageModel
{
    private readonly ILogger<EditUserModel> _logger;

    /// <summary>统一邮箱后缀</summary>
    private const string EmailSuffix = "golden-agri.com";

    public EditUserModel(ILogger<EditUserModel> logger)
    {
        _logger = logger;
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
    public int SkipCount { get; set; }

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
        SkipCount = 0;

        if (action == "updateemail") BatchUpdateEmail(ids);

        if (ResultMessage != null) TempData["ResultMessage"] = ResultMessage;
        if (ErrorMessage != null) TempData["ErrorMessage"] = ErrorMessage;
        if (Results.Count > 0) TempData["EditUserResults"] = string.Join("\n", Results);
        TempData["InputIds"] = InputIds;
        TempData["SuccessCount"] = SuccessCount;
        TempData["FailCount"] = FailCount;
        TempData["SkipCount"] = SkipCount;

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

        if (TempData["EditUserResults"] is string br) Results = br.Split('\n').ToList();
        if (TempData["ResultMessage"] is string rm) ResultMessage = rm;
        if (TempData["ErrorMessage"] is string em) ErrorMessage = em;
        if (TempData["InputIds"] is string ii) InputIds = ii;
        if (TempData["SuccessCount"] is int sc) SuccessCount = sc;
        if (TempData["FailCount"] is int fc) FailCount = fc;
        if (TempData["SkipCount"] is int sk) SkipCount = sk;
    }

    /// <summary>
    /// 批量更新邮箱：保留 @ 前缀部分，后缀统一替换为 golden-agri.com；
    /// 邮箱为空或已是 golden-agri.com 的用户跳过不更新。
    /// </summary>
    private void BatchUpdateEmail(List<string> ids)
    {
        Results.Add($"邮箱更新结果 ({TimeHelper.BeijingNow:HH:mm:ss})");
        Results.Add(new string('-', 60));
        foreach (var id in ids)
        {
            try
            {
                using var ctx = new PrincipalContext(ContextType.Domain);
                using var user = UserPrincipal.FindByIdentity(ctx, IdentityType.SamAccountName, id);
                if (user == null) { Results.Add($"{id} — 未找到"); FailCount++; continue; }

                var oldEmail = user.EmailAddress?.Trim() ?? "";
                if (string.IsNullOrEmpty(oldEmail))
                {
                    Results.Add($"{id} | {user.DisplayName} | 邮箱为空，跳过");
                    SkipCount++;
                    continue;
                }

                var at = oldEmail.LastIndexOf('@');
                if (at <= 0 || at == oldEmail.Length - 1)
                {
                    Results.Add($"{id} | {user.DisplayName} | 邮箱格式异常: {oldEmail}");
                    FailCount++;
                    continue;
                }

                var newEmail = $"{oldEmail[..at]}@{EmailSuffix}";
                if (string.Equals(oldEmail, newEmail, StringComparison.OrdinalIgnoreCase))
                {
                    Results.Add($"{id} | {user.DisplayName} | 邮箱已是 {EmailSuffix}，无需更新");
                    SkipCount++;
                    continue;
                }

                user.EmailAddress = newEmail;
                user.Save();
                Results.Add($"{id} | {user.DisplayName} | 邮箱: {oldEmail} → {newEmail}");
                SuccessCount++;
            }
            catch (Exception ex) { Results.Add($"{id} — 错误: {ex.Message}"); FailCount++; }
        }
        ResultMessage = $"邮箱更新完成: 成功 {SuccessCount}, 失败 {FailCount}, 跳过 {SkipCount}";
    }
}
