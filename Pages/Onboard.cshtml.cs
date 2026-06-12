using AccountManagement.Helpers;
using System.Net;
using System.Net.Mail;
using System.Text.Json;
using System.Threading.Tasks;
using AccountManagement.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AccountManagement.Pages;

public class OnboardModel : PageModel
{
    private readonly IConfiguration _configuration;

    public OnboardModel(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [BindProperty]
    public string? CnName { get; set; }

    [BindProperty]
    public string? EnName { get; set; }

    [BindProperty]
    public string? EmployeeId { get; set; }

    [BindProperty]
    public string? Mobile { get; set; }

    [BindProperty]
    public string? NeedEmail { get; set; } = "否";

    [BindProperty]
    public string? Region { get; set; }

    [BindProperty]
    public string? ContactEmail { get; set; }

    [BindProperty]
    public string? ManagerEmail { get; set; }

    [BindProperty]
    public string? NeedVpn { get; set; } = "否";

    [BindProperty]
    public bool VpnSap { get; set; }

    [BindProperty]
    public bool VpnTpm { get; set; }

    public string? ResultMessage { get; set; }
    public string? ErrorMessage { get; set; }

    private static string RequestFile => Path.Combine(AppContext.BaseDirectory, "App_Data", "onboard_requests.dat");
    private static readonly object s_lock = new();

    private static string GenerateRequestId()
    {
        var today = DateTime.Now.ToString("yyyyMMdd");
        lock (s_lock)
        {
            var all = LoadAllRequestsUnlocked();
            var todayCount = all.Count(r => r.Id.StartsWith(today));
            return $"{today}{(todayCount + 1):D3}";
        }
    }

    private static List<OnboardRequest> LoadAllRequestsUnlocked()
    {
        try
        {
            if (FileProtection.Exists(RequestFile))
            {
                var json = FileProtection.ReadAllText(RequestFile);
                return JsonSerializer.Deserialize<List<OnboardRequest>>(json) ?? new List<OnboardRequest>();
            }
        }
        catch { }
        return new List<OnboardRequest>();
    }

    public void OnGet()
    {
        if (TempData["ResultMessage"] is string rm) ResultMessage = rm;
        if (TempData["ErrorMessage"] is string em) ErrorMessage = em;
    }

    public IActionResult OnPost()
    {
        if (string.IsNullOrWhiteSpace(CnName) || string.IsNullOrWhiteSpace(EnName) || string.IsNullOrWhiteSpace(EmployeeId))
        {
            ErrorMessage = "请填写所有必填字段（中文名、英文名、员工编号）。";
            return Page();
        }

        var enName = EnName!.Trim();
        if (!enName.Contains('.') || enName.Contains(' '))
        {
            ErrorMessage = "英文名格式不正确，必须包含点号且不能包含空格（如 Sanfeng.Zhang）。";
            return Page();
        }

        if (!string.IsNullOrWhiteSpace(ContactEmail) && !IsValidEmail(ContactEmail))
        {
            ErrorMessage = "信息回传邮箱地址格式不正确。";
            return Page();
        }

        var today = DateTime.Now.ToString("yyyyMMdd");
        var empId = EmployeeId!.Trim();
        var request = new OnboardRequest
        {
            Id = $"{today}-{empId}",
            CnName = CnName!.Trim(),
            EnName = enName,
            EmployeeId = empId,
            Mobile = (Mobile ?? "").Trim(),
            NeedEmail = NeedEmail ?? "否",
            Region = (Region ?? "").Trim(),
            ContactEmail = (ContactEmail ?? "").Trim(),
            ManagerEmail = (ManagerEmail ?? "").Trim(),
            NeedVpn = (NeedVpn ?? "否").Trim(),
            VpnSap = VpnSap,
            VpnTpm = VpnTpm
        };

        SaveRequest(request);
        WriteAuditLog($"入职申请 | {request.CnName}({request.EnName}) | 员工号: {request.EmployeeId} | 区域: {request.Region}");

        // 异步发送邮件通知管理员
        var req = request;
        _ = Task.Run(() =>
        {
            try { SendNotificationEmail(req); }
            catch { }
        });

        ResultMessage = $"提交成功！申请编号: {request.Id}，请等待管理员审批。\n"
            + "如有更多需求，请联系中国区IT团队：\n"
            + "CN IT Support <CN_IT_Support@sinarmas-agri.com>";

        TempData["ResultMessage"] = ResultMessage;
        return RedirectToPage("/onboard");
    }

    private static void SaveRequest(OnboardRequest request)
    {
        lock (s_lock)
        {
            var list = LoadAllRequests();
            list.Insert(0, request);
            try
            {
                var dir = Path.GetDirectoryName(RequestFile);
                if (dir != null) Directory.CreateDirectory(dir);
                FileProtection.WriteAllText(RequestFile, JsonSerializer.Serialize(list));
            }
            catch { }
        }
    }

    public static List<OnboardRequest> LoadAllRequests()
    {
        lock (s_lock)
        {
            var list = LoadAllRequestsUnlocked();
            return list.Where(r => !string.IsNullOrEmpty(r.Id) && r.Id.Length >= 11).ToList();
        }
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

    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email && email.Contains('@') && email.Contains('.');
        }
        catch
        {
            return false;
        }
    }

    private void SendNotificationEmail(OnboardRequest req)
    {
        ServicePointManager.ServerCertificateValidationCallback = (_, _, _, _) => true;
        var smtpServer = _configuration["EmailSettings:SmtpServer"] ?? "";
        var smtpPort = int.Parse(_configuration["EmailSettings:SmtpPort"] ?? "587");
        var from = _configuration["EmailSettings:FromAddress"] ?? "";
        var to = _configuration["EmailSettings:ToAddress"] ?? "";
        var cc = _configuration["EmailSettings:CcAddress"] ?? "";
        var user = _configuration["EmailSettings:Username"] ?? "";
        var pass = _configuration["EmailSettings:Password"] ?? "";

        var managerInfo = req.NeedEmail == "是" && !string.IsNullOrWhiteSpace(req.ManagerEmail)
            ? $"直接上级邮箱: {req.ManagerEmail}\n" : "";
        var contactInfo = !string.IsNullOrWhiteSpace(req.ContactEmail)
            ? $"回传邮箱: {req.ContactEmail}\n" : "";
        var vpnInfo = req.NeedVpn == "是"
            ? $"开通VPN: 是 (SAP:{(req.VpnSap ? "是" : "否")} | TPM:{(req.VpnTpm ? "是" : "否")})\n" : "开通VPN: 否\n";

        var body = $@"[新入职申请]

申请编号: {req.Id}
中文名: {req.CnName}
英文名: {req.EnName}
员工编号: {req.EmployeeId}
手机号: {req.Mobile}
所属区域: {req.Region}
申请邮箱: {req.NeedEmail}
{managerInfo}{contactInfo}{vpnInfo}提交时间: {req.SubmitTime}

请登录管理员页面进行审批：https://www.garchina.com/account/Admin/Request

此邮件由系统自动发送，请勿回复。";

        using var client = new SmtpClient(smtpServer, smtpPort) { EnableSsl = true, Credentials = new NetworkCredential(user, pass) };
        using var msg = new MailMessage(from, to)
        {
            Subject = $"[IT信息] 新入职申请 - {req.CnName}({req.EnName})",
            Body = body,
            BodyEncoding = System.Text.Encoding.UTF8
        };
        msg.CC.Add(cc);
        client.Send(msg);
    }

    public static void SaveAllRequests(List<OnboardRequest> list)
    {
        lock (s_lock)
        {
            try
            {
                var dir = Path.GetDirectoryName(RequestFile);
                if (dir != null) Directory.CreateDirectory(dir);
                FileProtection.WriteAllText(RequestFile, JsonSerializer.Serialize(list));
            }
            catch { }
        }
    }

    public static void UpdateRequest(string id, string status, string reviewer, string? password = null)
    {
        lock (s_lock)
        {
            var list = LoadAllRequests();
            var req = list.FirstOrDefault(r => r.Id == id);
            if (req != null)
            {
                req.Status = status;
                req.ReviewedBy = reviewer;
                req.ReviewTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                if (password != null) req.NewPassword = password;
                FileProtection.WriteAllText(RequestFile, JsonSerializer.Serialize(list));
            }
        }
    }
}
