using System.DirectoryServices;
using System.Text.Json;
using AccountManagement.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AccountManagement.Pages.Admin;

public class DashboardModel : PageModel
{
    public bool IsAuthenticated { get; set; }
    public string? CurrentEmployeeId { get; set; }
    public string? CurrentDisplayName { get; set; }

    // 总览统计
    public int TotalUsers { get; set; }
    public int EnabledUsers { get; set; }
    public int DisabledUsers { get; set; }
    public int LockedUsers { get; set; }

    // OU 分布
    public List<OuStat> OuStats { get; set; } = new();
    public int TotalOuUsers { get; set; }

    // 密码到期
    public int Expire7Days { get; set; }
    public int Expire30Days { get; set; }
    public int Expire60Days { get; set; }

    // 近期操作
    public int TodayResets { get; set; }
    public int TodayOffboards { get; set; }
    public int TodayCreates { get; set; }
    public int PendingRequests { get; set; }

    // 审计日志摘要
    public List<string> RecentLogs { get; set; } = new();

    // 新增面板数据
    public List<WeeklyTrend> WeeklyTrends { get; set; } = new();
    public int TotalAdmins { get; set; }
    public int TotalOnboardRequests { get; set; }
    public int TotalGroupsAdded { get; set; }
    public DateTime? LastRestart { get; set; }
    public int OuDisabledUsers { get; set; }
    public int Recent30NewUsers { get; set; }
    public List<OuStat> NewUserByOu { get; set; } = new();
    public int Recent30Resets { get; set; }
    public int Recent30SelfResets { get; set; }

    public string? DashboardError { get; set; }

    public void OnGet()
    {
        CheckAuth();
        if (!IsAuthenticated) return;

        try
        {
            LoadDashboardData();
        }
        catch (Exception ex)
        {
            DashboardError = "仪表盘数据加载失败：" + ex.Message;
        }
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

    private void LoadDashboardData()
    {
        var ous = new List<(string Path, string Label)>
        {
            ("OU=hcm,OU=garchina,DC=garchina,DC=com", "HCM"),
            ("OU=food,OU=garchina,DC=garchina,DC=com", "食品"),
            ("OU=gar,OU=garchina,DC=garchina,DC=com", "粮油"),
        };

        var now = TimeHelper.BeijingNow;
        foreach (var (ouPath, ouLabel) in ous)
        {
            try
            {
                using var searchRoot = new DirectoryEntry($"LDAP://{ouPath}");
                using var searcher = new DirectorySearcher(searchRoot)
                {
                    Filter = "(&(objectCategory=person)(objectClass=user))",
                    SearchScope = SearchScope.Subtree,
                    PageSize = 1000
                };
                searcher.PropertiesToLoad.AddRange(new[] { "userAccountControl", "pwdLastSet" });

                int ouTotal = 0, ouEnabled = 0, ouLocked = 0, ouExp7 = 0, ouExp30 = 0, ouExp60 = 0;

                foreach (SearchResult r in searcher.FindAll())
                {
                    ouTotal++;
                    int uac = 0;
                    if (r.Properties.Contains("userAccountControl") && r.Properties["userAccountControl"].Count > 0)
                        uac = (int)r.Properties["userAccountControl"][0];

                    bool enabled = (uac & 2) == 0;
                    bool locked = (uac & 16) != 0;  // UF_LOCKOUT

                    if (enabled) ouEnabled++;
                    if (locked) ouLocked++;

                    // 密码到期计算
                    if (enabled && r.Properties.Contains("pwdLastSet") && r.Properties["pwdLastSet"].Count > 0
                        && r.Properties["pwdLastSet"][0] is long pwdTicks && pwdTicks > 0)
                    {
                        var pwdDate = DateTime.FromFileTimeUtc(pwdTicks);
                        var daysRemaining = 90 - (now - pwdDate).TotalDays;
                        if (daysRemaining > 0)
                        {
                            if (daysRemaining <= 7) ouExp7++;
                            if (daysRemaining <= 30) ouExp30++;
                            if (daysRemaining <= 60) ouExp60++;
                        }
                    }
                }

                TotalUsers += ouTotal;
                EnabledUsers += ouEnabled;
                LockedUsers += ouLocked;
                Expire7Days += ouExp7;
                Expire30Days += ouExp30;
                Expire60Days += ouExp60;

                OuStats.Add(new OuStat
                {
                    Label = ouLabel,
                    Total = ouTotal,
                    Enabled = ouEnabled
                });
            }
            catch { }
        }

        DisabledUsers = TotalUsers - EnabledUsers;
        OuDisabledUsers = DisabledUsers;
        TotalOuUsers = TotalUsers;
        foreach (var os in OuStats)
            os.Percentage = TotalOuUsers > 0 ? (double)os.Total / TotalOuUsers * 100 : 0;

        // 管理员数量
        try
        {
            TotalAdmins = LoginModel.LoadAdminList().Count;
        }
        catch { TotalAdmins = 0; }

        // 入职待审批 + 总数
        try
        {
            var reqFile = Path.Combine(AppContext.BaseDirectory, "App_Data", "onboard_requests.dat");
            if (System.IO.File.Exists(reqFile))
            {
                var models = JsonSerializer.Deserialize<List<AccountManagement.Models.OnboardRequest>>(
                    Helpers.FileProtection.ReadAllText(reqFile));
                PendingRequests = models?.Count(r => r.Status == null) ?? 0;
                TotalOnboardRequests = models?.Count ?? 0;
            }
        }
        catch { }

        // 今日操作统计 + 7日趋势
        try
        {
            var auditFile = Path.Combine(AppContext.BaseDirectory, "App_Data", "audit.dat");
            if (System.IO.File.Exists(auditFile))
            {
                var lines = Helpers.FileProtection.ReadAllText(auditFile).Split('\n');
                var today = now.ToString("yyyy-MM-dd");

                var weeklyMap = new Dictionary<string, WeeklyTrend>();
                for (int i = 6; i >= 0; i--)
                {
                    var d = now.AddDays(-i).ToString("yyyy-MM-dd");
                    weeklyMap[d] = new WeeklyTrend { Date = now.AddDays(-i).ToString("MM/dd") };
                }

                foreach (var line in lines.Reverse())
                {
                    if (line.StartsWith(today))
                    {
                        if (line.Contains("| 密码重置 |")) TodayResets++;
                        else if (line.Contains("| 离职处理 |")) TodayOffboards++;
                        else if (line.Contains("| 创建用户 |")) TodayCreates++;
                        else if (line.Contains("| 加用户组 |")) TotalGroupsAdded++;
                    }
                    foreach (var kvp in weeklyMap)
                    {
                        if (line.StartsWith(kvp.Key))
                        {
                            if (line.Contains("| 密码重置 |") || line.Contains("| 密码更新 |")) kvp.Value.Resets++;
                            else if (line.Contains("| 创建用户 |") || line.Contains("| 入职审批-批准 |")) kvp.Value.Creates++;
                            else if (line.Contains("| 离职处理 |")) kvp.Value.Offboards++;
                            else if (line.Contains("| 管理员登录 |")) kvp.Value.Logins++;
                        }
                    }
                }
                WeeklyTrends = weeklyMap.Values.ToList();
                RecentLogs = lines.Where(l => !string.IsNullOrWhiteSpace(l)).Reverse().Take(10).ToList();
            }
        }
        catch { }

        // 最近30日入职统计（食品+粮油）
        var cutoffDate = DateTime.UtcNow.AddDays(-30).ToString("yyyyMMddHHmmss.0Z");
        var onboardOus = new List<(string Path, string Label)>
        {
            ("OU=food,OU=garchina,DC=garchina,DC=com", "食品"),
            ("OU=gar,OU=garchina,DC=garchina,DC=com", "粮油"),
        };
        foreach (var (ouPath, ouLabel) in onboardOus)
        {
            try
            {
                using var sr = new DirectoryEntry($"LDAP://{ouPath}");
                using var ds = new DirectorySearcher(sr)
                {
                    Filter = $"(&(objectCategory=person)(objectClass=user)(whenCreated>={cutoffDate}))",
                    SearchScope = SearchScope.Subtree,
                    PageSize = 1000
                };
                var count = ds.FindAll().Count;
                Recent30NewUsers += count;
                NewUserByOu.Add(new OuStat { Label = ouLabel, Total = count });
            }
            catch { }
        }
        foreach (var os in NewUserByOu)
            os.Percentage = Recent30NewUsers > 0 ? (double)os.Total / Recent30NewUsers * 100 : 0;

        // 30日统计
        var cutoff30 = now.AddDays(-30).ToString("yyyy-MM-dd");
        var auditPath = Path.Combine(AppContext.BaseDirectory, "App_Data", "audit.dat");
        if (System.IO.File.Exists(auditPath))
        {
            var allLines = Helpers.FileProtection.ReadAllText(auditPath).Split('\n');
            foreach (var line in allLines)
            {
                if (line.Length < 10) continue;
                if (string.Compare(line[..10], cutoff30) < 0) continue;
                if (line.Contains("| 密码重置 |")) Recent30Resets++;
                else if (line.Contains("| 密码更新 |")) Recent30SelfResets++;
            }
        }

        LastRestart = TimeHelper.BeijingNow.AddHours(-Environment.TickCount64 / 3600000.0);
    }

    public class OuStat
    {
        public string Label { get; set; } = "";
        public int Total { get; set; }
        public int Enabled { get; set; }
        public double Percentage { get; set; }
        public int BarWidth => Math.Max(1, (int)(Percentage / 2));
    }

    public class WeeklyTrend
    {
        public string Date { get; set; } = "";
        public int Resets { get; set; }
        public int Creates { get; set; }
        public int Offboards { get; set; }
        public int Logins { get; set; }
        public int Total => Resets + Creates + Offboards + Logins;
        public int MaxBar => Math.Max(1, Total);
    }
}
