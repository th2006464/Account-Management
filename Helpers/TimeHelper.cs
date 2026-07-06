namespace AccountManagement.Helpers;

/// <summary>
/// 北京时间辅助类。所有面向用户的日期时间显示都应使用此类。
/// </summary>
public static class TimeHelper
{
    private static readonly TimeZoneInfo BeijingTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");

    /// <summary>当前北京时间</summary>
    public static DateTime BeijingNow =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, BeijingTimeZone);

    /// <summary>将 DateTime 转为北京时间（AD 时间默认按 UTC 处理）</summary>
    public static DateTime ToBeijingTime(DateTime dateTime)
    {
        if (dateTime.Kind == DateTimeKind.Utc)
            return TimeZoneInfo.ConvertTimeFromUtc(dateTime, BeijingTimeZone);
        if (dateTime.Kind == DateTimeKind.Local)
            return TimeZoneInfo.ConvertTime(dateTime, BeijingTimeZone);
        // Unspecified — AD 属性返回的通常是 UTC，按 UTC 转换
        return TimeZoneInfo.ConvertTimeFromUtc(dateTime, BeijingTimeZone);
    }

    /// <summary>将可空 DateTime 转为北京时间字符串</summary>
    public static string ToBeijingTimeString(DateTime? dateTime, string format = "yyyy-MM-dd HH:mm:ss")
    {
        if (dateTime == null) return "未知";
        return ToBeijingTime(dateTime.Value).ToString(format);
    }
}
