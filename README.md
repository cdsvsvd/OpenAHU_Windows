# OpenAHU Windows（3号API完善分支 / 本地SQLite演示版）

这是基于3号同学原始 `OpenAHU_Windows-main.zip` 完善的后端代码，保留 `OpenAHU.Contracts` 与 `OpenAHU.Api`，新增独立的 Application、Infrastructure 两层，优先跑通 v3 规定的本地数据读写闭环。**无智慧安大官网访问、无校方账号认证、无真实学生资料。**

## 快速启动（Windows 10/11，.NET 10 SDK）

```powershell
dotnet restore .\src\OpenAHU.Api\OpenAHU.Api.csproj
dotnet build .\src\OpenAHU.Api\OpenAHU.Api.csproj -c Release
dotnet run --project .\src\OpenAHU.Api\OpenAHU.Api.csproj
```

正常启动后，在 `%LOCALAPPDATA%\OpenAHU\session.json` 读取随机端口及会话令牌；每个请求头都要包含 `X-OpenAHU-Session`。首次迁移会生成 `%LOCALAPPDATA%\OpenAHU\campus.db` 并插入虚构演示资料。**不要使用项目中的 demo.sqlite3 覆盖个人正式数据库。**

安全与数据边界：后端仅监听本地127.0.0.1；会话令牌来自受当前用户目录保护的文件。此机制限制远程访问和未持令牌的请求，**不宣称防御已经以同一Windows账户运行且有文件读取权限的恶意软件**。课程数据由SQLite持久化，卸载时不得直接清理个人资料。

## 结构

- `src/OpenAHU.Contracts/`：3号原始冻结DTO与 JSON 约定；新增401错误码。
- `src/OpenAHU.Api/`：11条完整端点、统一错误、启动握手、单实例锁、回环令牌校验。
- `src/OpenAHU.Application/`：4号职责参考实现；`ICampusService`和`ICampusRepository`与业务校验，零 EF 依赖。
- `src/OpenAHU.Infrastructure/`：5号职责参考实现；SQLite EF Core 10、初始迁移、虚构Seed、查询和写入事务。
- `docs/`：冻结接口、集成说明、风险与测试记录。
- `tests/`：交互式Windows PowerShell烟测与Python离线数据库检查。

**依赖方向：** API→Application←Infrastructure，API在`Program.cs`只进行服务注册；WPF只走HTTP/JSON，不打开数据库。

## 验证

这个交付环境没有 .NET 10 SDK 且不能访问NuGet，因此无法实际完成C#编译和 HTTP 集成测试。已经进行源码结构、冻结DTO/路由静态检查与 SQLite DDL 测试；**请在Windows安装SDK后首先运行**：

```powershell
.\tests\smoke.ps1
```

烟测会使用独立临时数据库、不触碰已有个人数据；编译结果和测试结果必须以实际运行输出为准。任何编译错误优先修复后再合并 main。

## 团队衔接

本补丁为解决未完成接口而提供了4号和5号层的**参考实现**，不代表替代同学最终的PR。合并前由4、5号对模型、迁移和数据兼容性复核，避免同时合并不同版本的同名类型。具体见 `docs/团队对接与变更说明.md`。


## Windows客户端（成员1号+2号已接入）

- **主工程**：`src/OpenAHU.Desktop/OpenAHU.Desktop.csproj`，WPF/.NET 10，包含导航Shell和六个业务页面。
- **业务模块**：`src/OpenAHU.Desktop/Features/OpenAHU.Desktop.Features.csproj`，通过`OpenAHU.Contracts`和本机API访问课程、成绩、考试、校园链接、设置与首页。不会直接读取SQLite或登录智慧安大。
- **统一解决方案**：`OpenAHU.slnx`，Windows+Visual Studio 2022（支持.NET10）或安装.NET 10 SDK后使用`dotnet build OpenAHU.slnx -c Release`。
- **启动**：先执行`dotnet build src/OpenAHU.Api/OpenAHU.Api.csproj -c Release`，然后运行`dotnet run --project src/OpenAHU.Desktop/OpenAHU.Desktop.csproj -c Release`。桌面程序负责启动/复用本机API并读取私有握手文件。发布时需将API可执行文件一起部署。
- **验收**：Windows CI的`tests/smoke.ps1`在后端测试通过后还会编译WPF前端并运行页面行为断言；人工主窗口、DPI、打包发布仍需6号同学验收。
- **安全提醒**：目前全部业务数据为本地SQLite演示数据，不访问学校网站。仅新建的`local-*`课程可在客户端编辑，种子课程只读。
