using System.DirectoryServices;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AccountManagement.Pages.Admin;

public class ReportModel : PageModel
{
    public bool IsAuthenticated { get; set; }
    public string? CurrentEmployeeId { get; set; }
    public string? CurrentDisplayName { get; set; }

    public List<string>? ReportResults { get; set; }
    public List<string>? ChartData { get; set; }
    public string? ResultMessage { get; set; }
    public string? ErrorMessage { get; set; }

    private static string? s_csvData;
    private static string? s_csvFileName;

    // 缓存机制：12小时有效
    private static List<UserRecord>? s_cache;
    private static DateTime s_cacheTime = DateTime.MinValue;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(12);

    public void OnGet(int? expiry = null)
    {
        CheckAuth();
        if (!IsAuthenticated) return;

        var allOus = new List<(string Path, string Label)>
        {
            ("OU=hcm,OU=garchina,DC=garchina,DC=com", "HCM"),
            ("OU=food,OU=garchina,DC=garchina,DC=com", "食品"),
            ("OU=gar,OU=garchina,DC=garchina,DC=com", "粮油"),
        };

        if (expiry == 7) QueryPasswordExpiry(allOus, 7);
        else if (expiry == 30) QueryPasswordExpiry(allOus, 30);
        else if (expiry == 60) QueryPasswordExpiry(allOus, 60);
    }

    public IActionResult OnPost(string action)
    {
        CheckAuth();
        if (!IsAuthenticated) return RedirectToPage("/Admin/Login");

        if (action == "download")
        {
            if (s_csvData == null) return Page();
            var bytes = System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(s_csvData)).ToArray();
            return File(bytes, "text/csv", s_csvFileName ?? "report.csv");
        }

        var allOus = new List<(string Path, string Label)>
        {
            ("OU=hcm,OU=garchina,DC=garchina,DC=com", "HCM"),
            ("OU=food,OU=garchina,DC=garchina,DC=com", "食品"),
            ("OU=gar,OU=garchina,DC=garchina,DC=com", "粮油"),
        };

        if (action == "queryHcm")
            QueryOUs(allOus.Where(o => o.Label == "HCM").ToList());
        else if (action == "queryFood")
            QueryOUs(allOus.Where(o => o.Label == "食品").ToList());
        else if (action == "queryGar")
            QueryOUs(allOus.Where(o => o.Label == "粮油").ToList());
        else if (action == "queryAll")
            QueryOUs(allOus);
        else if (action == "queryPwd7")
            QueryPasswordExpiry(allOus, 7);
        else if (action == "queryPwd30")
            QueryPasswordExpiry(allOus, 30);
        else if (action == "queryPwd60")
            QueryPasswordExpiry(allOus, 60);
        else if (action == "queryPwdChanged7")
            QueryPasswordChanged(allOus, 7);

        return Page();
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

    private const int MaxResults = 3000;

    private List<UserRecord> GetCachedUsers()
    {
        if (s_cache != null && DateTime.Now - s_cacheTime < CacheDuration)
            return s_cache;

        var allOus = new List<(string Path, string Label)>
        {
            ("OU=hcm,OU=garchina,DC=garchina,DC=com", "HCM"),
            ("OU=food,OU=garchina,DC=garchina,DC=com", "食品"),
            ("OU=gar,OU=garchina,DC=garchina,DC=com", "粮油"),
        };

        s_cache = new List<UserRecord>();
        foreach (var (ouPath, ouLabel) in allOus)
        {
            try
            {
                var users = QuerySingleOU(ouPath, ouLabel, 10000);
                s_cache.AddRange(users);
            }
            catch { }
        }
        s_cacheTime = DateTime.Now;
        return s_cache;
    }

    private void QueryOUs(List<(string Path, string Label)> ous)
    {
        ReportResults = new List<string>();
        ChartData = new List<string>();

        var allCached = GetCachedUsers();
        var ouLabels = new HashSet<string>(ous.Select(o => o.Label));
        var allUsers = allCached.Where(u => ouLabels.Contains(u.OuLabel)).ToList();

        if (allUsers.Count == 0)
        {
            ReportResults.Add("未查询到用户。");
            return;
        }

        var grandTotal = allUsers.Count;

        // 动态计算列宽（状态放在最左侧）
        int wStatus = Math.Max(4, allUsers.Max(u => u.Enabled.Length));
        int wSam = Math.Max(4, allUsers.Max(u => u.SamAccountName.Length));
        int wName = Math.Max(6, allUsers.Max(u => u.DisplayName.Length));
        int wEmpId = Math.Max(6, allUsers.Max(u => u.EmployeeId.Length));
        int wPwdSet = Math.Max(10, allUsers.Max(u => u.PwdLastSet.Length));
        int wMail = Math.Max(4, allUsers.Max(u => u.Mail.Length));

        var gap = 2;
        var sepLen = wStatus + wSam + wName + wEmpId + wPwdSet + wMail + gap * 7;
        var header = "状态".PadRight(wStatus + gap) + "账号".PadRight(wSam + gap) + "显示名".PadRight(wName + gap) + "员工号".PadRight(wEmpId + gap) + "密码设置时间".PadRight(wPwdSet + gap) + "邮箱";
        var sep = new string('-', sepLen);

        ReportResults.Add($"查询时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}    用户总数: {grandTotal}");
        ReportResults.Add(sep);
        ReportResults.Add(header);
        ReportResults.Add(sep);

        foreach (var user in allUsers)
        {
            ReportResults.Add(
                user.Enabled.PadRight(wStatus + gap) +
                user.SamAccountName.PadRight(wSam + gap) +
                user.DisplayName.PadRight(wName + gap) +
                user.EmployeeId.PadRight(wEmpId + gap) +
                user.PwdLastSet.PadRight(wPwdSet + gap) +
                user.Mail
            );
        }

        ReportResults.Add(sep);

        // 可视化图表
        ChartData.Add("=== 各 OU 用户统计 ===");
        ChartData.Add("");
        int maxCount = ous.Select(o => allUsers.Count(u => u.OuLabel == o.Label)).Max();
        int barMaxWidth = 40;

        // 计算标签最大宽度
        int labelW = ous.Max(o => o.Label.Length);

        foreach (var (_, ouLabel) in ous)
        {
            var count = allUsers.Count(u => u.OuLabel == ouLabel);
            var pct = grandTotal > 0 ? (double)count / grandTotal * 100 : 0;
            var barLen = maxCount > 0 ? (int)((double)count / maxCount * barMaxWidth) : 0;
            var bar = new string('|', barLen);
            ChartData.Add($"{ouLabel.PadRight(labelW)}  {bar.PadRight(barMaxWidth)}  {count.ToString().PadRight(5)}  {pct,5:F1}%");
        }
        ChartData.Add("");
        ChartData.Add($"合计: {grandTotal} 人");

        GenerateCsv(allUsers);
        ResultMessage = $"查询完成，共 {grandTotal} 个用户。";
    }

    private void QueryPasswordExpiry(List<(string Path, string Label)> ous, int withinDays)
    {
        const int maxPwdAge = 90;
        var now = DateTime.Now;
        var ouLabels = new HashSet<string>(ous.Select(o => o.Label));
        var allCached = GetCachedUsers().Where(u => ouLabels.Contains(u.OuLabel) && u.Enabled == "启用").ToList();
        var allUsers = new List<UserRecord>();

        foreach (var u in allCached)
        {
            if (string.IsNullOrEmpty(u.PwdLastSetRaw) || u.PwdLastSetRaw == "0") continue;
            if (!long.TryParse(u.PwdLastSetRaw, out var ticks) || ticks <= 0) continue;

            var pwdSetDate = DateTime.FromFileTimeUtc(ticks);
            var daysSinceSet = (now - pwdSetDate).TotalDays;
            var daysRemaining = maxPwdAge - daysSinceSet;

            if (daysRemaining > 0 && daysRemaining <= withinDays)
            {
                u.PwdDaysRemaining = (int)daysRemaining;
                u.PwdLastSet = pwdSetDate.ToString("yyyy-MM-dd");
                allUsers.Add(u);
            }
        }

        allUsers = allUsers.OrderBy(u => u.PwdDaysRemaining).ToList();

        ReportResults = new List<string>();
        ChartData = new List<string>();

        if (allUsers.Count == 0)
        {
            ReportResults.Add($"未发现密码将在 {withinDays} 天内到期的用户。");
            return;
        }

        int wStatus = 4;
        int wSam = Math.Max(4, allUsers.Max(u => u.SamAccountName.Length));
        int wName = Math.Max(6, allUsers.Max(u => u.DisplayName.Length));
        int wEmpId = Math.Max(6, allUsers.Max(u => u.EmployeeId.Length));
        int wPwdSet = Math.Max(10, allUsers.Max(u => u.PwdLastSet.Length));
        int wDays = 6;
        int wMail = Math.Max(4, allUsers.Max(u => u.Mail.Length));
        var gap = 2;
        var sepLen = wStatus + wSam + wName + wEmpId + wPwdSet + wDays + wMail + gap * 8;
        var header = "状态".PadRight(wStatus + gap) + "账号".PadRight(wSam + gap) + "显示名".PadRight(wName + gap) + "员工号".PadRight(wEmpId + gap) + "密码设置时间".PadRight(wPwdSet + gap) + "剩余天数".PadRight(wDays + gap) + "邮箱";
        var sep = new string('-', sepLen);

        ReportResults.Add($"查询时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}    密码 {withinDays} 天内到期用户数: {allUsers.Count}");
        ReportResults.Add(sep);
        ReportResults.Add(header);
        ReportResults.Add(sep);

        foreach (var user in allUsers)
        {
            ReportResults.Add(
                user.Enabled.PadRight(wStatus + gap) +
                user.SamAccountName.PadRight(wSam + gap) +
                user.DisplayName.PadRight(wName + gap) +
                user.EmployeeId.PadRight(wEmpId + gap) +
                user.PwdLastSet.PadRight(wPwdSet + gap) +
                $"{user.PwdDaysRemaining}天".PadRight(wDays + gap) +
                user.Mail
            );
        }
        ReportResults.Add(sep);

        // 统计图表
        int maxCount = ous.Select(o => allUsers.Count(u => u.OuLabel == o.Label)).Max();
        int barMaxWidth = 40;
        int labelW = ous.Max(o => o.Label.Length);

        ChartData.Add($"=== 各 OU 密码 {withinDays} 天内到期统计 ===");
        ChartData.Add("");
        foreach (var (_, ouLabel) in ous)
        {
            var count = allUsers.Count(u => u.OuLabel == ouLabel);
            var barLen = maxCount > 0 ? (int)((double)count / maxCount * barMaxWidth) : 0;
            var bar = new string('|', barLen);
            ChartData.Add($"{ouLabel.PadRight(labelW)}  {bar.PadRight(barMaxWidth)}  {count.ToString().PadRight(5)}");
        }
        ChartData.Add("");
        ChartData.Add($"合计: {allUsers.Count} 人");

        GenerateCsv(allUsers);
        ResultMessage = $"查询完成，共 {allUsers.Count} 个用户密码将在 {withinDays} 天内到期。";
    }

    private void GenerateCsv(List<UserRecord> users)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("状态,账号,显示名,员工号,密码设置时间,邮箱,OU");
        foreach (var u in users)
        {
            sb.AppendLine($"\"{u.Enabled}\",\"{u.SamAccountName}\",\"{u.DisplayName}\",\"{u.EmployeeId}\",\"{u.PwdLastSet}\",\"{u.Mail}\",\"{u.OuLabel}\"");
        }
        s_csvData = sb.ToString();
        s_csvFileName = $"用户报表_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
    }

    private void QueryPasswordChanged(List<(string Path, string Label)> ous, int withinDays)
    {
        var now = DateTime.Now;
        var cutoff = now.AddDays(-withinDays);
        var ouLabels = new HashSet<string>(ous.Select(o => o.Label));
        var allCached = GetCachedUsers().Where(u => ouLabels.Contains(u.OuLabel)).ToList();
        var allUsers = new List<UserRecord>();

        foreach (var u in allCached)
        {
            if (string.IsNullOrEmpty(u.PwdLastSetRaw) || u.PwdLastSetRaw == "0") continue;
            if (!long.TryParse(u.PwdLastSetRaw, out var ticks) || ticks <= 0) continue;

            var pwdDate = DateTime.FromFileTimeUtc(ticks);
            if (pwdDate < cutoff) continue;

            u.PwdLastSet = pwdDate.ToString("yyyy-MM-dd");
            allUsers.Add(u);
        }

        allUsers = allUsers.OrderByDescending(u => u.PwdLastSet).ToList();
        BuildChangedReport(allUsers, withinDays);
    }

    private void BuildChangedReport(List<UserRecord> allUsers, int withinDays)
    {
        ReportResults = new List<string>();
        ChartData = new List<string>();

        if (allUsers.Count == 0)
        {
            ReportResults.Add($"最近 {withinDays} 天内没有用户更新密码。");
            return;
        }

        int wStatus = 4;
        int wSam = Math.Max(4, allUsers.Max(u => u.SamAccountName.Length));
        int wName = Math.Max(6, allUsers.Max(u => u.DisplayName.Length));
        int wEmpId = Math.Max(6, allUsers.Max(u => u.EmployeeId.Length));
        int wPwdSet = Math.Max(10, allUsers.Max(u => u.PwdLastSet.Length));
        int wMail = Math.Max(4, allUsers.Max(u => u.Mail.Length));
        var gap = 2;
        var sepLen = wStatus + wSam + wName + wEmpId + wPwdSet + wMail + gap * 6;
        var header = "状态".PadRight(wStatus + gap) + "账号".PadRight(wSam + gap) + "显示名".PadRight(wName + gap) + "员工号".PadRight(wEmpId + gap) + "密码设置时间".PadRight(wPwdSet + gap) + "邮箱";
        var sep = new string('-', sepLen);

        ReportResults.Add($"最近 {withinDays} 天更新密码用户数: {allUsers.Count}");
        ReportResults.Add(sep);
        ReportResults.Add(header);
        ReportResults.Add(sep);

        foreach (var user in allUsers)
        {
            ReportResults.Add(
                user.Enabled.PadRight(wStatus + gap) +
                user.SamAccountName.PadRight(wSam + gap) +
                user.DisplayName.PadRight(wName + gap) +
                user.EmployeeId.PadRight(wEmpId + gap) +
                user.PwdLastSet.PadRight(wPwdSet + gap) +
                user.Mail
            );
        }
        ReportResults.Add(sep);

        int maxCount = allUsers.GroupBy(u => u.OuLabel).Max(g => g.Count());
        int barMaxWidth = 40;
        int labelW = allUsers.Select(u => u.OuLabel).Distinct().Max(l => l.Length);
        ChartData.Add("=== 各 OU 最近更新密码统计 ===");
        ChartData.Add("");
        foreach (var g in allUsers.GroupBy(u => u.OuLabel))
        {
            var cnt = g.Count();
            var barLen = maxCount > 0 ? (int)((double)cnt / maxCount * barMaxWidth) : 0;
            var bar = new string('|', barLen);
            ChartData.Add($"{g.Key.PadRight(labelW)}  {bar.PadRight(barMaxWidth)}  {cnt.ToString().PadRight(5)}");
        }
        ChartData.Add("");
        ChartData.Add($"合计: {allUsers.Count} 人");

        GenerateCsv(allUsers);
        ResultMessage = $"查询完成，最近 {withinDays} 天共 {allUsers.Count} 个用户更新密码。";
    }

    private List<UserRecord> QuerySingleOU(string ouPath, string ouLabel, int maxResults = MaxResults)
    {
        var users = new List<UserRecord>();
        using var searchRoot = new DirectoryEntry($"LDAP://{ouPath}");
        using var searcher = new DirectorySearcher(searchRoot)
        {
            Filter = "(&(objectCategory=person)(objectClass=user))",
            SearchScope = SearchScope.Subtree,
            PageSize = 1000,
            SizeLimit = maxResults
        };

        searcher.PropertiesToLoad.AddRange(new[]
        {
            "sAMAccountName", "displayName", "employeeID", "mail", "userAccountControl", "pwdLastSet"
        });

        searcher.Sort.PropertyName = "sAMAccountName";
        searcher.Sort.Direction = SortDirection.Ascending;

        foreach (SearchResult result in searcher.FindAll())
        {
            var uac = GetProp(result, "userAccountControl");
            var pwdLastSet = GetProp(result, "pwdLastSet");
            var pwdRaw = result.Properties.Contains("pwdLastSet") && result.Properties["pwdLastSet"].Count > 0
                ? result.Properties["pwdLastSet"][0]?.ToString() ?? "" : "";
            users.Add(new UserRecord
            {
                SamAccountName = GetProp(result, "sAMAccountName"),
                DisplayName = GetProp(result, "displayName"),
                EmployeeId = GetProp(result, "employeeID"),
                Mail = GetProp(result, "mail"),
                Enabled = uac != "-" && (int.Parse(uac) & 2) == 0 ? "启用" : "禁用",
                OuLabel = ouLabel,
                PwdLastSet = pwdLastSet,
                PwdLastSetRaw = pwdRaw
            });
        }

        return users;
    }

    private static string GetProp(SearchResult result, string name)
    {
        if (result.Properties.Contains(name) && result.Properties[name].Count > 0)
        {
            var val = result.Properties[name][0];
            if (val is long ticks && name == "pwdLastSet")
                return DateTime.FromFileTimeUtc(ticks).ToString("yyyy-MM-dd");
            if (val is long lastLogon && name == "lastLogonTimestamp")
                return DateTime.FromFileTimeUtc(lastLogon).ToString("yyyy-MM-dd");
            return val.ToString() ?? "-";
        }
        return "-";
    }

    private class UserRecord
    {
        public string SamAccountName { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string EmployeeId { get; set; } = "";
        public string Mail { get; set; } = "";
        public string Enabled { get; set; } = "";
        public string OuLabel { get; set; } = "";
        public int PwdDaysRemaining { get; set; }
        public string PwdLastSet { get; set; } = "";
        public string PwdLastSetRaw { get; set; } = "";
    }
}
