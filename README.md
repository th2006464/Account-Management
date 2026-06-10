# AD 用户密码管理页面

这是一个针对 Windows Server 2022 + IIS 的简单 ASP.NET Core Razor Pages 应用，用于：

- 查询域用户账号状态
- 重置用户密码

## 运行方式

1. 在服务器上打开 PowerShell，导航到项目目录：
   ```powershell
   cd d:\VScode\iis改密码
   dotnet run --urls http://localhost:5000
   ```
2. 在浏览器中访问 `http://localhost:5000`
3. 也可以使用 IIS 部署该应用，注意：
   - 应用池身份需要为域用户或有足够权限的服务账号
   - 该账号必须有访问 Active Directory 的权限

## 部署到 Windows Server + IIS

1. 在开发机或服务器上执行发布：
   ```powershell
   cd d:\VScode\iis改密码
   dotnet publish -c Release -o .\publish
   ```
2. 在 Windows Server 上安装 IIS，并确保已启用“ASP.NET Core 模块”。
3. 在 IIS 管理器中创建新的站点：
   - 物理路径指向 `d:\VScode\iis改密码\publish`
   - 绑定到所需的主机名和端口
4. 将应用池身份设置为一个有权限访问 Active Directory 的域账号，或者使用具有 AD 权限的服务账号。
5. 打开浏览器访问该站点地址，例如 `http://your-server-name/`。

## 主要文件

- `Pages/Index.cshtml`：页面 UI
- `Pages/Index.cshtml.cs`：查询账号状态与密码重置逻辑

## 注意事项

- 用户名请使用 `sAMAccountName` 格式
- 密码重置功能需要域管理员权限或具备密码重置权限的账号
- 如果出现权限错误，请检查 IIS 应用池身份是否为域账号，并确保该账户有 AD 访问权限
