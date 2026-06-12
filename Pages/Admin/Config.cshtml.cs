using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AccountManagement.Pages.Admin;

public class ConfigModel : PageModel
{
    public bool IsAuthenticated { get; set; }
    public string? CurrentEmployeeId { get; set; }
    public string? CurrentDisplayName { get; set; }
    public string? ResultMessage { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string>? AdminList { get; set; }

    [BindProperty]
    public string? AddAdminId { get; set; }

    [BindProperty]
    public string? RemoveAdminId { get; set; }

    private static string LogoPath => Path.Combine(AppContext.BaseDirectory, "wwwroot", "logo.png");

    public void OnGet()
    {
        CheckAuth();
        if (!IsAuthenticated) return;
        AdminList = LoginModel.LoadAdminList();
    }

    public async Task<IActionResult> OnPost(string? action, IFormFile? logoFile)
    {
        CheckAuth();
        if (!IsAuthenticated) return RedirectToPage("/Admin/Login");
        AdminList = LoginModel.LoadAdminList();

        // 管理员操作
        if (action == "addAdmin" && CurrentEmployeeId == "15005035")
        {
            if (!string.IsNullOrWhiteSpace(AddAdminId) && AddAdminId.Length == 8 && AddAdminId.All(char.IsDigit))
            {
                LoginModel.AddAdmin(AddAdminId);
                ResultMessage = $"已添加管理员: {AddAdminId}";
                AdminList = LoginModel.LoadAdminList();
            }
            else
                ErrorMessage = "员工号格式不正确。";
            return Page();
        }

        if (action == "removeAdmin" && CurrentEmployeeId == "15005035")
        {
            if (RemoveAdminId == "15005035")
                ErrorMessage = "不能移除超级管理员。";
            else if (!string.IsNullOrWhiteSpace(RemoveAdminId))
            {
                LoginModel.RemoveAdmin(RemoveAdminId);
                ResultMessage = $"已移除管理员: {RemoveAdminId}";
                AdminList = LoginModel.LoadAdminList();
            }
            return Page();
        }

        // Logo 上传
        if (CurrentEmployeeId != "15005035")
        {
            ErrorMessage = "仅超级管理员可操作。";
            return Page();
        }

        if (logoFile == null || logoFile.Length == 0)
        {
            ErrorMessage = "请选择要上传的图片文件。";
            return Page();
        }

        if (logoFile.Length > 2 * 1024 * 1024)
        {
            ErrorMessage = "文件大小不能超过 2MB。";
            return Page();
        }

        var ext = Path.GetExtension(logoFile.FileName).ToLower();
        if (ext != ".png" && ext != ".jpg" && ext != ".jpeg" && ext != ".gif")
        {
            ErrorMessage = "仅支持 PNG、JPG、GIF 格式的图片。";
            return Page();
        }

        try
        {
            var dir = Path.GetDirectoryName(LogoPath);
            if (dir != null) Directory.CreateDirectory(dir);

            using var stream = new FileStream(LogoPath, FileMode.Create);
            await logoFile.CopyToAsync(stream);

            ResultMessage = "Logo 已更新，刷新页面即可看到效果。";
        }
        catch (Exception ex)
        {
            ErrorMessage = "保存失败：" + ex.Message;
        }

        return Page();
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
    }
}
