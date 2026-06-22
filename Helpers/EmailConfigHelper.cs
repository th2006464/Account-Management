using System.Text.Json;
using AccountManagement.Models;

namespace AccountManagement.Helpers;

/// <summary>
/// 邮箱配置的读取与持久化。首次使用时从 appsettings.json 初始化默认值。
/// </summary>
public static class EmailConfigHelper
{
    private static readonly object s_lock = new();

    private static string DataFile => Path.Combine(AppContext.BaseDirectory, "App_Data", "email_config.dat");

    /// <summary>
    /// 加载邮箱配置。首次调用时若文件不存在，从 IConfiguration 初始化并保存。
    /// </summary>
    public static EmailConfigData LoadConfig(IConfiguration? configuration = null)
    {
        lock (s_lock)
        {
            if (FileProtection.Exists(DataFile))
            {
                try
                {
                    var json = FileProtection.ReadAllText(DataFile);
                    return JsonSerializer.Deserialize<EmailConfigData>(json) ?? new EmailConfigData();
                }
                catch
                {
                    // 文件损坏，回退到默认
                }
            }

            // 首次初始化：从 appsettings.json 读取 SMTP 和收件地址
            var config = new EmailConfigData();
            if (configuration != null)
            {
                config.SmtpServer = configuration["EmailSettings:SmtpServer"] ?? "";
                config.SmtpPort = int.TryParse(configuration["EmailSettings:SmtpPort"], out var port) ? port : 587;
                config.FromAddress = configuration["EmailSettings:FromAddress"] ?? "";
                config.Username = configuration["EmailSettings:Username"] ?? "";
                config.Password = configuration["EmailSettings:Password"] ?? "";

                var defaultTo = configuration["EmailSettings:ToAddress"] ?? "";
                var defaultCc = configuration["EmailSettings:CcAddress"] ?? "";

                // 所有管理类通知默认使用相同的收件人
                foreach (var key in config.Notifications.Keys.ToList())
                {
                    if (key == "SelfPasswordChange")
                    {
                        // 自助修改不发管理员，留空
                        config.Notifications[key] = new EmailNotifyTarget { To = "", Cc = "" };
                    }
                    else if (key == "OnboardApproval")
                    {
                        // 入职审批通知给申请人，不设默认管理员
                        config.Notifications[key] = new EmailNotifyTarget { To = "", Cc = "" };
                    }
                    else
                    {
                        config.Notifications[key] = new EmailNotifyTarget { To = defaultTo, Cc = defaultCc };
                    }
                }
            }

            // 保存初始配置
            SaveConfig(config);
            return config;
        }
    }

    /// <summary>
    /// 保存邮箱配置
    /// </summary>
    public static void SaveConfig(EmailConfigData config)
    {
        lock (s_lock)
        {
            var dir = Path.GetDirectoryName(DataFile);
            if (dir != null) Directory.CreateDirectory(dir);
            FileProtection.WriteAllText(DataFile, JsonSerializer.Serialize(config));
        }
    }
}
