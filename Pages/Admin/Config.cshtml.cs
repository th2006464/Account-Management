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

    private static string LogoPath => Path.Combine(AppContext.BaseDirectory, "wwwroot", "logo.png");

    public void OnGet()
    {
        CheckAuth();
        if (!IsAuthenticated) return;
    }

    public async Task<IActionResult> OnPost(IFormFile? logoFile)
    {
        CheckAuth();
        if (!IsAuthenticated) return RedirectToPage("/Admin/Login");

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
