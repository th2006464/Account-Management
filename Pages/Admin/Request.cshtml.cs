using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Net;
using System.Net.Mail;
using System.Text.Json;
using System.Threading.Tasks;
using AccountManagement.Helpers;
using AccountManagement.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AccountManagement.Pages.Admin;

public class RequestModel : PageModel
{
    private readonly ILogger<RequestModel> _logger;
    private readonly IConfiguration _configuration;

    public RequestModel(ILogger<RequestModel> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public bool IsAuthenticated { get; set; }
    public string? CurrentEmployeeId { get; set; }
    public string? CurrentDisplayName { get; set; }

    public List<OnboardRequest> PendingRequests { get; set; } = new();
    public List<OnboardRequest> ProcessedRequests { get; set; } = new();
    public string? ResultMessage { get; set; }
    public string? ErrorMessage { get; set; }

    public void OnGet()
    {
        CheckAuth();
        if (!IsAuthenticated) return;

        if (TempData["ResultMessage"] is string rm) ResultMessage = rm;
        if (TempData["ErrorMessage"] is string em) ErrorMessage = em;

        LoadRequests();
    }

    [BindProperty]
    public string? EditId { get; set; }
    [BindProperty] public string? EditCnName { get; set; }
    [BindProperty] public string? EditEnName { get; set; }
    [BindProperty] public string? EditEmployeeId { get; set; }
    [BindProperty] public string? EditMobile { get; set; }
    [BindProperty] public string? EditNeedEmail { get; set; }
    [BindProperty] public string? EditRegion { get; set; }
    [BindProperty] public string? EditContactEmail { get; set; }
    [BindProperty] public string? EditManagerEmail { get; set; }

    public IActionResult OnPost(string action)
    {
        CheckAuth();
        if (!IsAuthenticated) return RedirectToPage("/Admin/Login");

        if (action == "edit")
        {
            EditRequest();
            LoadRequests();
            if (ResultMessage != null) TempData["ResultMessage"] = ResultMessage;
            return RedirectToPage();
        }

        // action 格式: "approve_20260610001" 或 "reject_20260610001"
        if (!string.IsNullOrEmpty(action))
        {
            var idx = action.IndexOf('_');
            if (idx > 0)
            {
                var act = action[..idx];
                var reqId = action[(idx + 1)..];

                if (act == "approve")
                    ApproveRequest(reqId);
                else if (act == "reject")
                    RejectRequest(reqId);
            }
        }

        LoadRequests();
        if (ResultMessage != null) TempData["ResultMessage"] = ResultMessage;
        if (ErrorMessage != null) TempData["ErrorMessage"] = ErrorMessage;

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

    private void LoadRequests()
    {
        var all = OnboardModel.LoadAllRequests();
        PendingRequests = all.Where(r => r.Status == null).ToList();
        ProcessedRequests = all.Where(r => r.Status != null).OrderByDescending(r => r.ReviewTime).Take(50).ToList();
    }

    private void EditRequest()
    {
        if (string.IsNullOrEmpty(EditId)) { ErrorMessage = "缺少申请编号。"; return; }

        var all = OnboardModel.LoadAllRequests();
        var req = all.FirstOrDefault(r => r.Id == EditId);
        if (req == null) { ErrorMessage = "未找到该申请。"; return; }

        req.CnName = EditCnName ?? req.CnName;
        req.EnName = EditEnName ?? req.EnName;
        req.EmployeeId = EditEmployeeId ?? req.EmployeeId;
        req.Mobile = EditMobile ?? req.Mobile;
        req.NeedEmail = EditNeedEmail ?? req.NeedEmail;
        req.Region = EditRegion ?? req.Region;
        req.ContactEmail = EditContactEmail ?? req.ContactEmail;
        req.ManagerEmail = EditManagerEmail ?? req.ManagerEmail;

        OnboardModel.SaveAllRequests(all);
        ResultMessage = $"已保存: {req.CnName}({req.EnName}) 的申请信息。";
    }

    private void ApproveRequest(string requestId)
    {
        var all = OnboardModel.LoadAllRequests();
        var req = all.FirstOrDefault(r => r.Id == requestId);
        if (req == null) { ErrorMessage = "未找到该申请。"; return; }

        try
        {
            var password = GenerateRandomPassword();
            var ouPath = _configuration["AdSettings:NewUserOU"] ?? "OU=TESTOU,DC=garchina,DC=com";
            var domain = _configuration["AdSettings:Domain"] ?? "garchina.com";
            var enName = req.EnName.ToLower();
            var emailAddr = $"{enName}@sinarmas-agri.com";
            var nameParts = enName.Split('.');
            var givenName = nameParts[0];
            var surname = nameParts.Length > 1 ? nameParts[1] : "";

            using var ouEntry = new DirectoryEntry($"LDAP://{ouPath}");
            using var newUser = ouEntry.Children.Add($"CN={enName}", "user");

            newUser.Properties["sAMAccountName"].Value = req.EmployeeId;
            newUser.Properties["userPrincipalName"].Value = $"{enName}@{domain}";
            newUser.Properties["givenName"].Value = givenName;
            newUser.Properties["sn"].Value = surname;
            newUser.Properties["displayName"].Value = $"{req.CnName}({enName})";
            newUser.Properties["description"].Value = req.CnName;
            newUser.Properties["mail"].Value = emailAddr;
            newUser.Properties["employeeID"].Value = req.EmployeeId;
            if (!string.IsNullOrWhiteSpace(req.Mobile))
                newUser.Properties["telephoneNumber"].Value = req.Mobile;
            newUser.Properties["pager"].Value = "O365";
            newUser.CommitChanges();

            using var pwdUser = new DirectoryEntry(newUser.Path);
            pwdUser.Invoke("SetPassword", new object[] { password });
            pwdUser.Properties["userAccountControl"].Value = 512;
            pwdUser.CommitChanges();

            OnboardModel.UpdateRequest(requestId, "approved", CurrentEmployeeId!, password);
            WriteAuditLog($"入职审批-批准 | 操作人: {CurrentEmployeeId} | {req.CnName}({enName}) | 员工号: {req.EmployeeId}");

            // 异步发邮件
            var en = enName;
            var cn = req.CnName;
            var emp = req.EmployeeId;
            var pwd = password;
            _ = Task.Run(() =>
            {
                try { SendCreateEmail(cn, en, emp, pwd, emailAddr); }
                catch (Exception ex) { _logger.LogError(ex, "入职邮件发送失败"); }
            });

            ResultMessage = $"已批准: {req.CnName}({enName})，密码已生成。";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "审批创建用户失败");
            ErrorMessage = "创建用户失败：" + ex.Message;
        }
    }

    private void RejectRequest(string requestId)
    {
        var all = OnboardModel.LoadAllRequests();
        var req = all.FirstOrDefault(r => r.Id == requestId);
        if (req == null) { ErrorMessage = "未找到该申请。"; return; }

        OnboardModel.UpdateRequest(requestId, "rejected", CurrentEmployeeId!);
        WriteAuditLog($"入职审批-取消 | 操作人: {CurrentEmployeeId} | {req.CnName}({req.EnName}) | 员工号: {req.EmployeeId}");
        ResultMessage = $"已取消: {req.CnName}({req.EnName}) 的入职申请。";
    }

    private static string GenerateRandomPassword()
    {
        const string upper = "ABCDEFGHIJKMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnpqrstuvwxyz";
        const string numbers = "23456789";
        const string symbols = "@$%&*,./";
        const string all = upper + lower + numbers + symbols;
        const int length = 12;
        var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        var chars = new char[length];
        chars[0] = upper[GetRandomInt(rng, upper.Length)];
        chars[1] = lower[GetRandomInt(rng, lower.Length)];
        chars[2] = numbers[GetRandomInt(rng, numbers.Length)];
        chars[3] = symbols[GetRandomInt(rng, symbols.Length)];
        for (int i = 4; i < length; i++) chars[i] = all[GetRandomInt(rng, all.Length)];
        for (int i = length - 1; i > 0; i--) { int j = GetRandomInt(rng, i + 1); (chars[i], chars[j]) = (chars[j], chars[i]); }
        return new string(chars);
    }

    private static int GetRandomInt(System.Security.Cryptography.RandomNumberGenerator rng, int max)
    {
        var bytes = new byte[4]; rng.GetBytes(bytes);
        return (int)(BitConverter.ToUInt32(bytes, 0) % (uint)max);
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

    private void SendCreateEmail(string cnName, string enName, string employeeId, string newPassword, string emailAddr)
    {
        ServicePointManager.ServerCertificateValidationCallback = (_, _, _, _) => true;
        var smtpServer = _configuration["EmailSettings:SmtpServer"] ?? "";
        var smtpPort = int.Parse(_configuration["EmailSettings:SmtpPort"] ?? "587");
        var from = _configuration["EmailSettings:FromAddress"] ?? "";
        var to = _configuration["EmailSettings:ToAddress"] ?? "";
        var cc = _configuration["EmailSettings:CcAddress"] ?? "";
        var user = _configuration["EmailSettings:Username"] ?? "";
        var pass = _configuration["EmailSettings:Password"] ?? "";

        var body = $@"尊敬的用户，

您的 GARCHINA 账号 {employeeId} 已创建。
姓名: {cnName}({enName})
邮箱: {emailAddr}
密码: {newPassword}

此账号适用于 GARCHINA 系统认证、China OA 系统、GARCHINA VPN、Workday 请休假系统。
请尽快登录并修改密码。密码有效期 90 天。

如有问题，请联系中国区 IT 部门：CN_IT_Support@sinarmas-agri.com
此邮件由系统自动发送，请勿回复。";

        using var client = new SmtpClient(smtpServer, smtpPort) { EnableSsl = true, Credentials = new NetworkCredential(user, pass) };
        using var msg = new MailMessage(from, to) { Subject = "[IT信息] 新用户AD账号创建通知", Body = body, BodyEncoding = System.Text.Encoding.UTF8 };
        msg.CC.Add(cc);
        client.Send(msg);
    }
}
