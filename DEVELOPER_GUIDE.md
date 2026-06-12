# 技术规范文档 — ASP.NET Core 企业内部工具开发指南

基于 AccountManagement 项目的实战经验总结，适用于 AD 管理类、配置管理类内部系统。

---

## 1. 项目架构

```
项目根目录/
├── Helpers/           # 工具类（加密、AD操作封装）
├── Models/            # 数据模型（POCO类）
├── Pages/             # Razor Pages 页面（一个功能一个页面）
│   ├── Shared/        # 共享布局（_Layout、_AdminLayout）
│   └── Admin/         # 管理后台页面组
├── wwwroot/           # 静态资源（CSS、JS、图片）
├── appsettings.json   # 配置文件（SMTP、OU、域名等）
└── Program.cs         # 应用入口
```

**原则：一个功能一个页面，代码和 UI 一一对应。**

---

## 2. 数据存储规范

### 2.1 存储位置

所有数据文件存放于 `App_Data/` 目录（运行时自动创建）：

```csharp
private static string DataFile => Path.Combine(AppContext.BaseDirectory, "App_Data", "config.dat");
```

**禁止在 `wwwroot/` 下存放任何敏感文件。**

### 2.2 文件加密

敏感数据必须加密存储，直接引用 `FileProtection` 工具类：

```csharp
// 读取（自动解密）
var json = FileProtection.ReadAllText(DataFile);
var list = JsonSerializer.Deserialize<List<MyModel>>(json);

// 写入（自动加密）
FileProtection.WriteAllText(DataFile, JsonSerializer.Serialize(list));
```

**加密方式：AES-256，密钥硬编码在 `FileProtection.cs` 中。**

### 2.3 并发安全

所有文件读写必须加 `lock` 保护：

```csharp
private static readonly object s_lock = new();

public static List<MyModel> LoadData()
{
    lock (s_lock)
    {
        if (FileProtection.Exists(DataFile))
        {
            var json = FileProtection.ReadAllText(DataFile);
            return JsonSerializer.Deserialize<List<MyModel>>(json) ?? new();
        }
        return new();
    }
}

public static void SaveData(List<MyModel> list)
{
    lock (s_lock)
    {
        var dir = Path.GetDirectoryName(DataFile);
        if (dir != null) Directory.CreateDirectory(dir);
        FileProtection.WriteAllText(DataFile, JsonSerializer.Serialize(list));
    }
}
```

**不使用数据库**，JSON 文件足够。如果数据量大（> 100MB），再考虑 SQLite。

---

## 3. 配置管理

### 3.1 appsettings.json

环境相关的配置放这里，代码中通过 `IConfiguration` 读取：

```json
{
  "EmailSettings": {
    "SmtpServer": "mail.example.com",
    "SmtpPort": 587,
    "FromAddress": "it@example.com"
  },
  "AdSettings": {
    "NewUserOU": "OU=Users,DC=domain,DC=com"
  }
}
```

```csharp
public class MyModel : PageModel
{
    private readonly IConfiguration _configuration;
    public MyModel(IConfiguration configuration) { _configuration = configuration; }

    void DoSomething()
    {
        var server = _configuration["EmailSettings:SmtpServer"];
    }
}
```

### 3.2 运行时配置（管理员可修改）

需要管理员在 UI 中修改的配置存 JSON 文件（如管理员列表 `admins.dat`），读取时不用缓存：

```csharp
// 正确：每次都读文件，保证管理员操作立即生效
public static List<string> LoadAdminList()
{
    lock (s_lock)
    {
        var json = FileProtection.ReadAllText(AdminFile);
        return JsonSerializer.Deserialize<List<string>>(json) ?? new();
    }
}

// 错误：用静态变量缓存会导致新增管理员不生效
// private static List<string> _cache;
// if (_cache != null) return _cache;  ← 不要这样做
```

---

## 4. AD 操作规范

### 4.1 域控发现

**不需要配置任何 IP 地址**，使用无参构造函数自动通过 DNS SRV 记录发现：

```csharp
using var context = new PrincipalContext(ContextType.Domain); // 自动发现当前域 DC
```

跨域查询：

```csharp
using var context = new PrincipalContext(ContextType.Domain, "other-domain.com"); // 自动发现目标域 DC
```

**部署位置：任意加域服务器即可，不需要部署在域控上。**

### 4.2 密码操作

修改密码必须使用 `NetUserSetInfo` API（而非 `user.SetPassword()`），才能触发 PCNSSVC 多域同步：

```csharp
// 正确方式（封装在 PasswordHelper 中）
PasswordHelper.SetPasswordWithNotification(username, newPassword);

// 错误方式（不会触发 PCNS）
// user.SetPassword(newPassword);
```

### 4.3 慢操作保护

跨域查询等可能超时的操作，用 `Task.Run` + 超时包装：

```csharp
using var cts = new CancellationTokenSource();
var task = Task.Run(() => QueryAd(), cts.Token);
if (task.Wait(TimeSpan.FromSeconds(10)))
    return task.Result;
else
{
    cts.Cancel();
    return "查询超时";
}
```

---

## 5. 页面开发规范

### 5.1 表单提交

**必须使用 PRG 模式（Post-Redirect-Get）**，防止刷新重复提交：

```csharp
public IActionResult OnPost()
{
    // ... 业务处理 ...

    TempData["ResultMessage"] = "操作成功";
    return RedirectToPage(); // 重定向到 GET，而不是 return Page()
}

public void OnGet()
{
    if (TempData["ResultMessage"] is string msg)
        ResultMessage = msg; // GET 时恢复消息
}
```

大数据集不要通过 TempData 传递（Cookie 限制 4KB），改用静态缓存或直接 `return Page()`。

### 5.2 前端防重复点击

```javascript
var submitting = false;
form.addEventListener('submit', function(e) {
    if (submitting) { e.preventDefault(); return; }
    submitting = true;
    setTimeout(function() {
        form.querySelectorAll('button[type="submit"]').forEach(function(b) { b.disabled = true; });
    }, 0);
    setTimeout(function() { submitting = false; }, 5000);
});
```

### 5.3 二次确认弹窗

敏感操作使用 Bootstrap Modal 而非浏览器 `confirm()`：

```html
<button type="button" onclick="confirmAction()">危险操作</button>
<div class="modal fade" id="confirmModal">
    <!-- 确认弹窗 -->
</div>
```

### 5.4 认证检查

```csharp
private void CheckAuth()
{
    var loggedIn = HttpContext.Session.GetString("AdminLoggedIn");
    if (loggedIn == "true")
    {
        IsAuthenticated = true;
        CurrentEmployeeId = HttpContext.Session.GetString("AdminEmployeeId");
    }
}
```

未登录自动跳转登录页，登录后回到原页面（`returnUrl` 参数）。

---

## 6. 日志与审计

### 6.1 审计日志

所有写操作必须记录审计日志：

```csharp
private static void WriteAuditLog(string entry)
{
    var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {entry}{Environment.NewLine}";
    FileProtection.AppendAllText(AuditLogFile, line);
}

// 使用示例
WriteAuditLog($"密码重置 | 操作人: {operatorId} | 账号: {targetId}");
```

**审计日志文件不应在前端 UI 中提供清空按钮，仅通过 `audit.dat` 持久化保存。**

### 6.2 错误日志

使用 ASP.NET Core 内置 `ILogger`：

```csharp
try { ... }
catch (Exception ex) { _logger.LogError(ex, "操作描述失败"); }
```

---

## 7. 技术栈与依赖

| 组件 | 用途 |
|------|------|
| .NET 8 | 运行时 |
| ASP.NET Core Razor Pages | Web 框架 |
| Bootstrap 3 | UI 框架（通过 CDN 引入，无 npm） |
| System.DirectoryServices.AccountManagement | AD 操作 |
| System.Text.Json | JSON 序列化 |
| System.Net.Mail | 邮件发送 |
| System.Security.Cryptography | AES 加密 |

**原则：最小依赖。不使用 Entity Framework、不使用 npm、不引入第三方 NuGet 包（除非有明确需求）。**

---

## 8. 部署规范

### IIS 配置

1. **创建独立应用程序池**，避免与其他站点冲突
2. .NET CLR 版本：**"无托管代码"**
3. 应用池标识：**NetworkService**（以计算机账号访问 AD，无需维护密码）
4. 替换 DLL 后务必 **`iisreset`** 或回收应用池
5. 部署时**保留 `App_Data/` 文件夹**

### 文件清单（部署只需这些）

```
AccountManagement.dll          # 主程序（页面已编译在内）
AccountManagement.exe          # 本地运行入口
AccountManagement.deps.json    # 依赖清单
AccountManagement.runtimeconfig.json
web.config                     # IIS 配置
appsettings.json               # 配置文件（部署后修改一次）
wwwroot/                       # 静态资源
runtimes/                      # .NET 运行时文件
System.*.dll                   # 系统依赖
```

---

## 9. 命名规范

| 类型 | 规范 | 示例 |
|------|------|------|
| 页面文件名 | 功能描述 + `.cshtml` | `BatchUser.cshtml` |
| 类名 | `{PageName}Model` | `BatchUserModel` |
| 工具类 | `{Function}Helper` 或 `{Function}Protection` | `FileProtection` |
| 数据模型 | `{Entity}Request` 或 `{Entity}Record` | `OnboardRequest` |
| 方法 | 动词开头 PascalCase | `LoadAdminList()`, `WriteAuditLog()` |
| 静态字段 | `s_` 前缀 | `s_lock`, `s_cache` |
| 配置键 | PascalCase | `EmailSettings:SmtpServer` |

---

## 10. 常见问题排查

| 问题 | 原因 | 解决 |
|------|------|------|
| 无法写入文件 | 应用池身份无权限 | 给 `App_Data/` 文件夹添加 `NetworkService` 写入权限 |
| AD 操作报权限错误 | 应用池身份无 AD 权限 | 改用有 AD 权限的域账号、或给计算机账号授权 |
| 跨域查询超时/失败 | 防火墙或 DNS 问题 | 确认服务器能 ping 通目标域控 |
| 部署后页面没变化 | IIS 缓存旧 DLL | 执行 `iisreset` |
| TempData 不生效 | Cookie 超 4KB | 大数据改用静态缓存 |
| 密码修改后其他系统不更新 | 没用 NetUserSetInfo | 使用 `PasswordHelper.SetPasswordWithNotification()` |
