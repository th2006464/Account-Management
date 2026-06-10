using System.DirectoryServices.AccountManagement;
using System.Runtime.InteropServices;

namespace AccountManagement.Helpers;

/// <summary>
/// 使用 NetUserSetInfo API 设置密码，可触发 PCNSSVC 多域密码同步服务。
/// </summary>
public static class PasswordHelper
{
    [DllImport("netapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int NetUserSetInfo(
        [MarshalAs(UnmanagedType.LPWStr)] string serverName,
        [MarshalAs(UnmanagedType.LPWStr)] string userName,
        int level,
        ref USER_INFO_1003 buf,
        out int paramError);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct USER_INFO_1003
    {
        [MarshalAs(UnmanagedType.LPWStr)]
        public string usri1003_password;
    }

    /// <summary>
    /// 通过 NetUserSetInfo API 设置域用户密码，触发 PCNSSVC 多域同步。
    /// </summary>
    /// <param name="username">sAMAccountName</param>
    /// <param name="newPassword">新密码（明文）</param>
    /// <exception cref="InvalidOperationException">密码设置失败时抛出</exception>
    public static void SetPasswordWithNotification(string username, string newPassword)
    {
        using var context = new PrincipalContext(ContextType.Domain);
        var server = context.ConnectedServer;
        if (string.IsNullOrEmpty(server))
            throw new InvalidOperationException("无法获取域控制器名称。");

        var serverPath = $"\\\\{server}";
        var info = new USER_INFO_1003 { usri1003_password = newPassword };

        int result = NetUserSetInfo(serverPath, username, 1003, ref info, out int paramError);

        if (result != 0)
        {
            throw new InvalidOperationException(
                $"NetUserSetInfo 调用失败。错误代码: {result}，参数错误: {paramError}");
        }
    }
}
