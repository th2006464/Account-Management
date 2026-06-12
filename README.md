# GARCHINA 账户管理系统

企业级 Active Directory 账户管理平台，支持用户自助改密、管理员批量操作、入职审批流程、可视化仪表盘。

## 技术栈

- **.NET 8** + **ASP.NET Core Razor Pages** + **Bootstrap 3**
- **System.DirectoryServices.AccountManagement**（AD 操作）
- **Windows Server + IIS** 部署，**Kestrel** 开发调试
- **NetUserSetInfo API** 触发 PCNSSVC 多域密码同步
- **AES-256** 加密保护敏感日志文件

## 功能模块

### 用户自助
- 域密码修改（当前密码验证 → 强度校验 → PCNS 同步通知）
- 入职申请提交（多字段表单 + 邮件通知管理员）

### 管理员控制台
- 用户检索、密码重置、一键离职、批量解锁、加用户组
- 创建 AD 用户（自动生成密码、发送邮件）
- 入职审批流程（提交 → 审批通过自动创建账号 → 邮件通知用户）
- 管理员授权管理（超级管理员保护）

### 可视化仪表盘
- 用户总数/启用/禁用/锁定/待审批 实时卡片
- OU 用户分布进度条
- 7 日密码更新趋势
- 最近 30 日入职统计
- 今日操作 + 密码到期预警

### 用户报表
- 按 OU 查询用户列表（动态列宽对齐）
- 密码到期查询（7/30/60 天）
- CSV 导出
- 统计图表

### 安全
- Session 认证 + 管理员白名单
- 所有 App_Data 文件 AES-256 加密存储
- 审计日志（密码重置、离职、解锁、登录等全记录）
- 未登录自动跳转登录页，登录后返回原页面

## 本地运行

```powershell
cd D:\VScode\iis改密码
dotnet run
```
双击 `AccountManagement.exe` 自动打开浏览器访问 `http://localhost:5000`。

## 部署到 Windows Server + IIS

1. 服务器安装 .NET 8 Hosting Bundle 和 IIS
2. 将 `bin/Publish/` 目录复制到服务器
3. IIS 创建站点指向该目录，应用池选择"无托管代码"
4. 应用池标识设为有 AD 权限的域账号（推荐 NetworkService）
5. 部署时**保留 `App_Data/` 文件夹**不动，避免日志丢失

## 项目结构

```
├── Helpers/
│   ├── PasswordHelper.cs      # NetUserSetInfo P/Invoke
│   └── FileProtection.cs      # AES 加密文件读写
├── Models/
│   └── OnboardRequest.cs      # 入职申请数据模型
├── Pages/
│   ├── Index.cshtml           # 用户自助改密主页
│   ├── Onboard.cshtml         # 入职申请页面
│   └── Admin/
│       ├── Login.cshtml       # 管理员登录
│       ├── UserAdmin.cshtml   # 管理员控制台
│       ├── Dashboard.cshtml   # 可视化仪表盘
│       ├── Report.cshtml      # 用户报表
│       ├── Request.cshtml     # 入职审批
│       ├── NewUser.cshtml     # 创建用户
│       ├── AdminLog.cshtml    # 操作日志（加密存储）
├── wwwroot/                   # 静态资源
├── appsettings.json           # 配置文件
└── Program.cs                 # 应用入口
```
