# Windows 发布包与 SQL Server 连接

本发布包是一个真正的Windows x64桌面程序，包含两个执行文件：

- `OpenAHU.Desktop.exe`：双击启动的WPF应用。
- `OpenAHU.Api.exe`：随桌面端自动启动的本机后端；不要单独发送或删除。

两者需要放在**同一个文件夹**。不需要另外安装.NET运行时（已使用自包含发布）；无需访问智慧安大官网。

## 默认SQLite模式（立即演示）

直接双击 `OpenAHU.Desktop.exe`，首次运行自动在
`%LOCALAPPDATA%\OpenAHU\campus.db` 初始化9张表、两名虚构学生、学期、课程、成绩、考试与链接。

## 切换到你本机的SQL Server 2025 / SSMS 22

1. 在SSMS中连接本机数据库引擎实例。SSMS是管理界面，不等于SQL Server数据库服务。
2. 执行随包附带的 `01_Create_CampusDemo.sql`，创建 `OpenAHU_CampusDemo` 数据库及9张表。
3. 把 `appsettings.sqlserver.example.json` 复制成 `appsettings.json`（替换原文件）。
4. 修改 `Server=localhost` 为你在SSMS连接时输入的准确服务器名称，如 `.\SQLEXPRESS` 或 `(localdb)\MSSQLLocalDB`。
5. 使用Windows集成身份认证时保持 `Integrated Security=True`，不需要写入学生账号密码。
6. 双击 `OpenAHU.Desktop.exe`，首次启动将向空表插入虚构演示数据。
7. 在SSMS执行 `02_Check_CampusDemo.sql` 核验结果。

**开发机本地证书例外：** 模板中的 `TrustServerCertificate=True` 只用于你控制的本地演示SQL Server。连接正式服务器时必须采用有效证书并关闭该选项。

**注意**：本模式不会自动建SQL Server数据库、也不会对已有SQL Server表运行SQLite迁移。使用独立演示库，不要填写学校或真实业务数据库连接字符串。连接字符串若使用SQL登录，请通过个人环境变量等安全方式配置，不要提交账号密码到GitHub。

连接不上时，先确认SQL Server服务正在运行、实例名正确、Windows身份对目标库有权限；查看 `/health` 的 `databaseReady` 状态并检查API控制台日志。当前不支持多人通过远程SQL Server同时登录并分别查看私有数据；本阶段以一名演示学生的本机应用为目标。

## 开发者重新构建

在仓库根目录执行：

```powershell
pwsh -File ./tools/publish-windows.ps1
```

产生 `artifacts/OpenAHU-win-x64.zip`。不要把数据库文件或密码写进发布包。
