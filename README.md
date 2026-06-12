# GARCHINA 账户管理系统

企业级 Active Directory 账户管理平台，支持用户自助改密、管理员批量操作、入职流程、可视化仪表盘。

## 技术栈

- **.NET 8** + **ASP.NET Core Razor Pages** + **Bootstrap 3**
- **System.DirectoryServices.AccountManagement**（AD 操作）
- **Windows Server + IIS** 部署，**Kestrel** 开发调试
- **NetUserSetInfo API** 触发 PCNSSVC 多域密码同步
- **AES-256** 加密保护敏感日志文件

### 部署位置说明

**不需要部署在 AD 域控服务器上**，只要部署在**已加入域**的任意 Windows Server 或 Windows 电脑即可。

域控发现机制：代码中使用 `new PrincipalContext(ContextType.Domain)` 不加任何 IP 地址，系统通过 DNS SRV 记录自动发现域控：

1. 查询 `_ldap._tcp.garchina.com` 获取域内所有 DC 列表
2. 优先选择同一 Active Directory 站点（子网）的 DC
3. 自动故障转移：当前 DC 不可用时切换下一个

所有操作走 Windows 集成身份验证（应用池标识），域控升级或 IP 变更无需改代码。

## 功能模块

### 用户自助
- 域密码修改（当前密码验证 → 强度校验 9位+大小写+数字+符号+无连续 → 邮件通知用户）
- 入职申请提交（多字段表单 + 自动邮件通知管理员）

### 账号查询管理
- **模糊搜索**：按员工号/姓名/UPN 关键字搜索，支持数字和字母混合查询
- 用户详情：显示名、员工号、UPN、邮箱、电话、启用/锁定状态、密码到期时间、所属组
- 密码重置（随机生成 12 位 + PCNS 同步 + 邮件通知）
- 一键离职（禁用账号 + 修改 UPN/邮箱 + 自动附加日期描述）
- 批量解锁所有锁定账号
- 加用户组（弹窗输入组名，自动添加）
- 管理员授权管理（超级管理员保护，前端不显示删除按钮）

### 批量用户管理
- 多行输入（支持换行/逗号/空格分隔）
- 四个操作按钮：查询 / 启用 / 禁用 / 批量重置密码
- 批量重置密码：生成随机密码 + PCNS 同步 + 整合为一封邮件通知
- 二次确认弹窗防误操作

### 手动创建用户
- 填写表单 → 写入 AD → 异步发送邮件
- 自动生成 12 位密码（排除易混淆字符 1/l/L/o/0/O）
- 创建记录和邮件状态持久化

### 待创建用户（入职流程）
- 员工提交入职申请 → 管理员审批
- 审批通过自动创建 AD 账号 + 发送通知
- 支持编辑申请信息后再创建
- 回传邮箱通知用户账号已创建
- 申请编号格式 YYYYMMDD-员工号

### 可视化系统仪表盘
- 用户总数/启用/禁用/锁定/待处理 实时卡片（点击下钻跳转）
- OU 用户分布进度条
- 7 日密码更新趋势
- 最近 30 日入职统计
- 今日操作 + 密码到期预警（点击直接查询）
- 运营数据（30 天密码重置/自助修改次数）

### 用户统计报表
- 按 OU 查询用户列表（动态列宽对齐）
- 密码到期查询（7/30/60 天）
- 最近 7 天更新密码用户
- CSV 导出
- 统计图表（条形图 + 占比百分比）
- 12 小时服务器缓存，极速查询

### 高级日志查询
- 统一日志面板，200 条/页分页
- 合并显示所有操作（登录、密码操作、创建用户、离职、解锁）+ 邮件发送状态
- 页面底部显示日志文件路径

### 安全
- Session 认证 + 管理员白名单（JSON 文件管理）
- 未登录自动跳转登录页，登录后返回原页面
- 所有 App_Data 文件 AES-256 加密存储（.dat 扩展名）
- 审计日志（密码变更、离职、解锁、登录等全记录，永不清除）
- 敏感操作二次确认（Bootstrap 模态框）

## 本地运行

```powershell
cd D:\VScode\iis改密码
dotnet run
```
双击 `AccountManagement.exe` 自动打开浏览器访问 `http://localhost:5000`。

## 部署到 Windows Server + IIS

1. 服务器安装 .NET 8 Hosting Bundle 和 IIS
2. 将 `bin/Publish/` 目录复制到服务器（如 `D:\WebApps\AccountManagement\`）
3. IIS 中**创建独立的应用程序池**（名称如 `AccountManagement`），避免与其他站点冲突
4. 应用程序池设置：
   - .NET CLR 版本：**"无托管代码"**
   - 标识：设为有 AD 权限的域账号，推荐使用 **NetworkService**（以计算机账号 `DOMAIN\SERVERNAME$` 访问 AD）
5. IIS 创建站点/应用程序，指向部署目录，绑定到新建的应用池
6. 部署时**保留 `App_Data/` 文件夹**不动，避免日志丢失
7. 替换 DLL 后务必执行 `iisreset` 或回收应用池

## 项目结构

```
├── Helpers/
│   ├── PasswordHelper.cs      # NetUserSetInfo P/Invoke 触发 PCNS
│   └── FileProtection.cs      # AES 加密文件读写
├── Models/
│   └── OnboardRequest.cs      # 入职申请数据模型
├── Pages/
│   ├── Index.cshtml           # 用户自助改密主页
│   ├── Onboard.cshtml         # 入职申请页面
│   └── Admin/
│       ├── Login.cshtml       # 管理员登录
│       ├── UserAdmin.cshtml   # 账号查询管理（模糊搜索 + 控制面板）
│       ├── Dashboard.cshtml   # 系统仪表盘（可视化 + 下钻）
│       ├── Report.cshtml      # 用户统计报表（缓存 + CSV 导出）
│       ├── Request.cshtml     # 待创建用户（入职流程）
│       ├── NewUser.cshtml     # 手动创建用户
│       ├── BatchUser.cshtml   # 批量用户管理
│       ├── AdminLog.cshtml    # 高级日志查询（加密存储 + 分页）
│       └── SinarmasUser.cshtml # 跨域用户查询
├── Pages/Shared/
│   ├── _Layout.cshtml         # 公共布局（页脚）
│   └── _AdminLayout.cshtml    # 管理后台布局（侧边栏 + 导航）
├── wwwroot/                   # 静态资源 (css/js/lib/logo)
├── appsettings.json           # 配置文件 (SMTP/OU/PathBase)
└── Program.cs                 # 应用入口 (自动打开浏览器)
```
