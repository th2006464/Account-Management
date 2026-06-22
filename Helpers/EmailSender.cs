using System.Net;
using System.Net.Mail;
using System.Text;
using AccountManagement.Models;

namespace AccountManagement.Helpers;

/// <summary>
/// 集中邮件发送器。从 EmailConfigHelper 读取配置，所有发送方法均为 fire-and-forget。
/// </summary>
public static class EmailSender
{
    /// <summary>
    /// 发送邮件（底层方法）
    /// </summary>
    private static void Send(EmailConfigData config, string to, string cc, string subject, string body)
    {
        try
        {
            ServicePointManager.ServerCertificateValidationCallback = (s, c, ch, e) => true;

            using var client = new SmtpClient(config.SmtpServer, config.SmtpPort)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(config.Username, config.Password)
            };

            using var msg = new MailMessage
            {
                From = new MailAddress(config.FromAddress),
                Subject = subject,
                Body = body,
                BodyEncoding = Encoding.UTF8,
                IsBodyHtml = false
            };

            // To: 逗号分隔多个地址
            if (!string.IsNullOrWhiteSpace(to))
            {
                foreach (var addr in to.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    msg.To.Add(addr.Trim());
            }

            // Cc: 逗号分隔多个地址
            if (!string.IsNullOrWhiteSpace(cc))
            {
                foreach (var addr in cc.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    msg.CC.Add(addr.Trim());
            }

            client.Send(msg);
        }
        catch
        {
            // 邮件发送失败不阻塞主流程
        }
    }

    /// <summary>
    /// 发送测试邮件 — 使用指定的 SMTP 参数和收件地址，模板内容为测试占位数据
    /// </summary>
    public static void SendTest(string notifyType, string toAddress, string smtpServer, int smtpPort,
        string fromAddress, string username, string password)
    {
        var config = new EmailConfigData
        {
            SmtpServer = smtpServer,
            SmtpPort = smtpPort,
            FromAddress = fromAddress,
            Username = username,
            Password = password
        };

        string subject;
        string body;

        switch (notifyType)
        {
            case "SelfPasswordChange":
                body = $@"尊敬的用户，

您的 GARCHINA 账号 12345678 密码已更新。
新密码为：Test@2025Pwd

此密码适用于：
- GARCHINA 系统认证
- China OA 系统
- GARCHINA VPN
- Workday 请休假系统

（此为测试邮件，请忽略具体内容。）

如有问题，请联系中国区 IT 部门：
邮箱：CN_IT_Support@sinarmas-agri.com

此邮件由系统自动发送，请勿回复。";
                subject = "[测试] 用户AD账号密码更新通知";
                break;

            case "AdminPasswordReset":
                body = $@"尊敬的用户，

您的 GARCHINA 账号 12345678 的密码已重置。
新密码为：Test@2025Pwd

（此为测试邮件，由管理员从Config页面触发。）

此邮件由系统自动发送，请勿回复。";
                subject = "[测试] 用户AD 账号密码重置通知";
                break;

            case "BatchPasswordReset":
                body = @"以下用户密码已批量重置：

12345678 | 测试用户A | 新密码: Test@2025A
87654321 | 测试用户B | 新密码: Test@2025B

（此为测试邮件，请忽略具体内容。）

此邮件由系统自动发送，请勿回复。";
                subject = "[测试] 批量密码重置通知";
                break;

            case "NewUserCreate":
                body = @"尊敬的用户，

您的 GARCHINA 账号 12345678 已创建。
姓名: 测试用户(Test.User)
邮箱: test.user@garchina.com
密码: Test@2025Pwd

（此为测试邮件，请忽略具体内容。）

此邮件由系统自动发送，请勿回复。";
                subject = "[测试] 新用户AD账号创建通知";
                break;

            case "OnboardRequest":
                body = @"[新入职申请]

申请编号: REQ-2025-001
中文名: 测试用户
英文名: Test.User
员工编号: 12345678
手机号: 13800000000
所属区域: 华东销售上海所
申请邮箱: 是
直接上级邮箱: manager@company.com
开通VPN: 否
提交时间: 2025-01-01 09:00:00

（此为测试邮件，请忽略具体内容。）

此邮件由系统自动发送，请勿回复。";
                subject = "[测试] 新入职申请 - 测试用户(Test.User)";
                break;

            case "OnboardApproval":
                body = @"尊敬的用户，

您的 GARCHINA 账号已创建，信息如下：
姓名: 测试用户(Test.User)
员工号: 12345678
手机号: 13800000000
所属区域: 华东销售上海所
企业邮箱: test.user@garchina.com
密码: Test@2025Pwd

（此为测试邮件，请忽略具体内容。）

此邮件由系统自动发送，请勿回复。";
                subject = "[测试] 新用户AD账号创建通知";
                break;

            default:
                body = "未知通知类型的测试邮件。";
                subject = "[测试] 未知类型";
                break;
        }

        Send(config, toAddress, "", subject, body);
    }

    private static EmailConfigData LoadConfig()
    {
        return EmailConfigHelper.LoadConfig();
    }

    // ═══════════════════════════════════════════
    // 1. 自助密码修改 — 发给用户本人
    // ═══════════════════════════════════════════
    public static void SendSelfPasswordChange(string employeeId, string newPassword, string toEmail)
    {
        var config = LoadConfig();
        var body = $@"尊敬的用户，

您的 GARCHINA 账号 {employeeId} 密码已更新。
新密码为：{newPassword}

此密码适用于：
- GARCHINA 系统认证
- China OA 系统
- GARCHINA VPN
- Workday 请休假系统

特别注意：
1. 复制粘贴密码时，请先粘贴到记事本，检查是否有空格。
2. 输入密码后，点击密码框旁的小眼睛图标确认输入正确。
3. 请尽快更新默认密码，密码更新后会自动同步至邮箱系统。
4. ChinaOA、Workday系统登录时请注意用户名格式。

如有问题，请联系中国区 IT 部门：
邮箱：CN_IT_Support@sinarmas-agri.com

此邮件由系统自动发送，请勿回复。";

        Send(config, toEmail, "", "[IT信息] 用户AD账号密码更新通知", body);
    }

    // ═══════════════════════════════════════════
    // 2. 管理员密码重置 — 发给管理员
    // ═══════════════════════════════════════════
    public static void SendAdminPasswordReset(string employeeId, string newPassword, string displayName)
    {
        var config = LoadConfig();
        var target = config.Notifications.GetValueOrDefault("AdminPasswordReset") ?? new();
        var body = $@"尊敬的用户，

您的 GARCHINA 账号 {employeeId} 的密码已重置。
新密码为：{newPassword}

此密码适用于：
- GARCHINA 系统认证
- China OA 系统
- GARCHINA VPN
- Workday 请休假系统

特别注意：
1. 复制粘贴密码时，请先粘贴到记事本，检查是否有空格。
2. 输入密码后，点击密码框旁的小眼睛图标确认输入正确。
3. 请尽快更新默认密码，密码更新后会自动同步至邮箱系统。
4. ChinaOA、Workday系统登录时请注意用户名格式。

如有问题，请联系中国区 IT 部门：
邮箱：CN_IT_Support@sinarmas-agri.com

此邮件由系统 [10.95.0.62] 自动发送，请勿回复。";

        Send(config, target.To, target.Cc, "[IT信息] 用户AD 账号密码重置通知", body);
    }

    // ═══════════════════════════════════════════
    // 3. 批量密码重置 — 发给管理员
    // ═══════════════════════════════════════════
    public static void SendBatchPasswordReset(List<(string Id, string Name, string Pwd)> list)
    {
        var config = LoadConfig();
        var target = config.Notifications.GetValueOrDefault("BatchPasswordReset") ?? new();
        var sb = new StringBuilder();
        sb.AppendLine("以下用户密码已批量重置：");
        sb.AppendLine();
        foreach (var item in list)
            sb.AppendLine($"{item.Id} | {item.Name} | 新密码: {item.Pwd}");
        sb.AppendLine();
        sb.AppendLine("此密码适用于 GARCHINA 系统认证、China OA 系统、GARCHINA VPN、Workday 请休假系统。");
        sb.AppendLine("请通知相关用户尽快修改密码。密码有效期 90 天。");
        sb.AppendLine();
        sb.AppendLine("此邮件由系统自动发送，请勿回复。");

        Send(config, target.To, target.Cc, "[IT信息] 批量密码重置通知", sb.ToString());
    }

    // ═══════════════════════════════════════════
    // 4. 新用户创建 — 发给管理员
    // ═══════════════════════════════════════════
    public static void SendNewUserCreated(string cnName, string enName, string employeeId, string newPassword, string emailAddr)
    {
        var config = LoadConfig();
        var target = config.Notifications.GetValueOrDefault("NewUserCreate") ?? new();
        var body = $@"尊敬的用户，

您的 GARCHINA 账号 {employeeId} 已创建。
姓名: {cnName}({enName})
邮箱: {emailAddr}
密码: {newPassword}

此账号适用于：
- GARCHINA 系统认证
- China OA 系统
- GARCHINA VPN
- Workday 请休假系统

特别注意：
1. 请尽快登录并修改密码。
2. 密码有效期 90 天。

如有问题，请联系中国区 IT 部门：
邮箱：CN_IT_Support@sinarmas-agri.com

此邮件由系统自动发送，请勿回复。";

        Send(config, target.To, target.Cc, "[IT信息] 新用户AD账号创建通知", body);
    }

    // ═══════════════════════════════════════════
    // 5. 入职申请通知 — 发给管理员
    // ═══════════════════════════════════════════
    public static void SendOnboardRequest(OnboardRequest req)
    {
        var config = LoadConfig();
        var target = config.Notifications.GetValueOrDefault("OnboardRequest") ?? new();
        var sb = new StringBuilder();
        sb.AppendLine("[新入职申请]");
        sb.AppendLine();
        sb.AppendLine($"申请编号: {req.Id}");
        sb.AppendLine($"中文名: {req.CnName}");
        sb.AppendLine($"英文名: {req.EnName}");
        sb.AppendLine($"员工编号: {req.EmployeeId}");
        sb.AppendLine($"手机号: {req.Mobile}");
        sb.AppendLine($"所属区域: {req.Region}");
        sb.AppendLine($"申请邮箱: {req.NeedEmail}");
        if (req.NeedEmail == "是" && !string.IsNullOrWhiteSpace(req.ManagerEmail))
            sb.AppendLine($"直接上级邮箱: {req.ManagerEmail}");
        if (!string.IsNullOrWhiteSpace(req.ContactEmail))
            sb.AppendLine($"回传邮箱: {req.ContactEmail}");
        var vpnInfo = req.NeedVpn == "是" ? "是" : "否";
        sb.AppendLine($"开通VPN: {vpnInfo}");
        sb.AppendLine($"提交时间: {req.SubmitTime}");
        sb.AppendLine();
        sb.AppendLine("请登录管理员页面进行审批：https://www.garchina.com/account/Admin/Request");
        sb.AppendLine();
        sb.AppendLine("此邮件由系统自动发送，请勿回复。");

        Send(config, target.To, target.Cc, $"[IT信息] 新入职申请 - {req.CnName}({req.EnName})", sb.ToString());
    }

    // ═══════════════════════════════════════════
    // 6. 入职审批通知 — 发给管理员，可选发给申请人
    // ═══════════════════════════════════════════
    public static void SendOnboardApproval(string cnName, string enName, string employeeId,
        string newPassword, string emailAddr, string mobile, string region, string contactEmail)
    {
        var config = LoadConfig();
        var target = config.Notifications.GetValueOrDefault("OnboardApproval") ?? new();

        var body = $@"尊敬的用户，

您的 GARCHINA 账号已创建，信息如下：
姓名: {cnName}({enName})
员工号: {employeeId}
手机号: {mobile}
所属区域: {region}
企业邮箱: {emailAddr}
密码: {newPassword}

此账号适用于 GARCHINA 系统认证、China OA 系统、GARCHINA VPN、Workday 请休假系统。
请尽快登录并修改密码。密码有效期 90 天。

邮箱账号需要雅加达邮箱管理团队创建，请留意后续邮件。

如有问题，请联系中国区 IT 部门：CN_IT_Support@sinarmas-agri.com
此邮件由系统自动发送，请勿回复。";

        // 发给管理员
        Send(config, target.To, target.Cc, "[IT信息] 新用户AD账号创建通知", body);

        // 如果提供了回传邮箱且不在管理员列表中，额外发一份给申请人
        if (!string.IsNullOrWhiteSpace(contactEmail))
        {
            var toList = target.To.Split(',').Select(x => x.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!toList.Contains(contactEmail))
            {
                Send(config, contactEmail, "", "[IT信息] 新用户AD账号创建通知", body);
            }
        }
    }
}
