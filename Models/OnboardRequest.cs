namespace AccountManagement.Models;

public class OnboardRequest
{
    public string Id { get; set; } = "";
    public string CnName { get; set; } = "";
    public string EnName { get; set; } = "";
    public string EmployeeId { get; set; } = "";
    public string Mobile { get; set; } = "";
    public string NeedEmail { get; set; } = "否";
    public string Region { get; set; } = "";
    public string ContactEmail { get; set; } = "";
    public string ManagerEmail { get; set; } = "";
    public string SubmitTime { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    public string? Status { get; set; } // null=pending, "approved", "rejected"
    public string? ReviewedBy { get; set; }
    public string? ReviewTime { get; set; }
    public string? NewPassword { get; set; }
}
