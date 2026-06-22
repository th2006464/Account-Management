namespace AccountManagement.Models;

/// <summary>
/// 单类邮件通知的收件人配置
/// </summary>
public class EmailNotifyTarget
{
    public string To { get; set; } = "";   // 逗号分隔多个地址
    public string Cc { get; set; } = "";   // 逗号分隔多个地址
}

/// <summary>
/// 邮箱全局配置 — 存储于 App_Data/email_config.dat
/// </summary>
public class EmailConfigData
{
    // SMTP 服务器
    public string SmtpServer { get; set; } = "";
    public int SmtpPort { get; set; } = 587;
    public string FromAddress { get; set; } = "";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";

    /// <summary>
    /// 各通知类型的收件配置。Key 为通知类型标识。
    /// </summary>
    public Dictionary<string, EmailNotifyTarget> Notifications { get; set; } = new()
    {
        ["SelfPasswordChange"] = new(),    // 自助密码修改
        ["AdminPasswordReset"] = new(),    // 管理员密码重置
        ["BatchPasswordReset"] = new(),    // 批量密码重置
        ["NewUserCreate"] = new(),         // 新用户创建
        ["OnboardRequest"] = new(),        // 入职申请
        ["OnboardApproval"] = new(),       // 入职审批
    };
}
