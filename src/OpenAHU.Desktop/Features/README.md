# 成员02：WPF 业务页面交付

基于 main `e4ec21e00145d26c8f0920a2d09d7528a3417939`，个人分支 `feat/ui-pages`。
所有新增源文件、测试与说明均在 `src/OpenAHU.Desktop/Features/`；没有修改 DTO、API、仓储、迁移、主窗口或其他成员目录。

## 已实现

- Home：读取演示资料、数据来源和更新时间；`UpdatedAt=null` 显示“尚未提供数据更新时间”。
- Timetable：学期、周次、按周筛选、全学期列表；按周一到周日和节次排序；空周次课程可在全学期列表编辑。
- Grades：按学期展示，空分数/绩点显示“未公布”；不计算或宣称官方 GPA。
- Exams：按学期展示，未知时间排末尾，空时间/地点明确提示；时间使用本机时区并显示偏移。
- Links：从 API 读取并按分类/标题排列，可复制网址。v3 不自动打开、预取或访问外部网站。
- Settings：只读服务/数据库就绪状态、版本和数据来源，无学校登录或伪登录按钮。
- 本地课程新增/编辑：使用统一 `CourseUpsertRequest`，校验名称、长度、星期、节次、学期周范围；支持 `1-8,10,12` 和中文逗号并去重。表单锁定所属学期；跨学期移动不在本次界面范围。
- 所有页面始终显示“本地演示数据，非学校官方信息”。失败、空数据、加载状态、刷新与重试均有展示。
- POST/PUT 成功后从 API 重新查询，不把客户端构造的记录当作保存结果。写入确认成功而刷新失败时提示勿重复新增；超时/连接断开等结果不确定时关闭提交入口并提示先刷新核对。

## 给成员01的集成提案（未改动 Shell）

实际仓库没有 `OpenAHU.Desktop.csproj`、Shell、导航或现成 HTTP Client，所以提供独立 WPF 类库和统一功能目录注册入口。以下是供成员01审查的集成方案，不是已提交的跨组 PR。

1. 主窗口项目使用 `net10.0-windows` / `UseWPF=true`。
2. 如果主窗口项目位于 `src/OpenAHU.Desktop/`，必须排除嵌套模块源文件，避免重复编译及将验收宿主带入产品：

```xml
<ItemGroup>
  <Compile Remove="Features/**/*.cs" />
  <Page Remove="Features/**/*.xaml" />
  <ProjectReference Include="Features/OpenAHU.Desktop.Features.csproj" />
</ItemGroup>
```

3. Shell 启动/复用本地伴随进程，验证 `session.json` 的进程归属、端口与地址一致后注入会话。不要把令牌记录到日志或截图。

```csharp
// 在 WPF UI 线程创建；baseUrl/token 来自成员01校验后的本地握手。
var api = new SessionApiClient(new Uri(baseUrl), token);
var pages = FeatureCatalog.Create(api);
// 将 page.Key / Title 注册导航，将 page.View 放入 ContentControl。
await pages.Single(p => p.Key == "home").ActivateAsync();
// 页面首次进入或需要刷新时调用 ActivateAsync；保留编辑表单时不要重建 page。
// 关闭：先 Dispose 每个 page，再 Dispose api。
// 会话重连：等待在途保存完成，释放旧 pages/client，以新握手重新创建。
```

4. `SessionApiClient` 仅接受 `http://127.0.0.1:随机端口/`，禁用代理、重定向和 Cookie；每次请求携带 `X-OpenAHU-Session`，超时 15 秒。Features 不读取握手文件、数据库或任何校园账号。握手文件读取仅存在于验收宿主，正式产品交给 Shell。
5. 推荐内容区至少 960×650 DIP；列表有水平滚动，编辑区有独立滚动，保存/取消固定在底部。主窗口 DPI、键盘焦点、导航前未保存提醒由成员01/06在整机集成时验收。

## 契约与兼容性

直接引用 `OpenAHU.Contracts` 中的 `StudentProfileDto`、`TermDto`、`CourseDto`、`GradeDto`、`ExamDto`、`LinkDto`、`DataStatusDto`、`HealthDto`、`ApiError`、`CourseUpsertRequest` 及 `ContractJson.CreateOptions()`。不重复声明 DTO，也不依赖任何仓储、EF Core 或 SQLite 包。

调用 `GET /api/v1/{profile,data-status,health,terms,courses,grades,exams,links}`，课程/成绩/考试均携带转义后的 `termId`；写入调用 `POST /api/v1/local/courses` 和 `PUT /api/v1/local/courses/{id}`。未添加删除入口，不改变关联删除策略。

400/404/409 显示 API 安全文案及追踪号；401 提示重新连接；503、网络错误和无效 JSON 使用“本地服务暂不可用”。过期请求取消且不能覆盖较新的页面结果。服务返回 2xx 才进入成功流程。

现有后端始终返回 `DataStatusDto.UpdatedAt=null`；尚无乐观锁字段，多个会话编辑仍可能最后写入覆盖。`HealthDto` / `CourseUpsertRequest` 代码注释仍标为待评审，但已被当前实际端点使用，本实现以该 commit 为准，后续改动需同步复测。

不涉及数据库 schema、迁移、种子或导入变化；没有触碰用户 `%LocalAppData%/OpenAHU/campus.db`。回滚只需移除本 Features 模块与后续 Shell 的注册/项目引用，已有后端课程数据应保留。

## 构建与验收

需要 Windows、.NET 10 SDK。`Verification` 是零第三方测试框架的可执行断言/渲染宿主，**真实测试入口为脚本及 `dotnet run`，不是 VSTest 自动发现**。

```powershell
# 仓库根目录；正常安装 SDK 的机器
./src/OpenAHU.Desktop/Features/Verification/verify.ps1 -Integration

# 本次工作区：已下载官方 SDK 至 git 忽略的 .dsh-tmp/dotnet
$env:DOTNET_CLI_HOME = "$PWD/.dsh-tmp/cli"
$env:NUGET_PACKAGES = "$PWD/.dsh-tmp/packages"
./src/OpenAHU.Desktop/Features/Verification/verify.ps1 `
  -Dotnet "$PWD/.dsh-tmp/dotnet/dotnet.exe" `
  -NuGetConfig "$PWD/.dsh-tmp/NuGet.Config" -Integration
```

脚本执行 restore/build、30 项行为断言、6 页及编辑区 WPF 渲染、绑定错误检查；`-Integration` 再编译并启动后端，用随机命名的独立临时数据库与会话路径验证新增、编辑、重启读取，以及真实数据页面渲染。只终止自身启动的子进程。生成物位于 `.dsh-tmp/verification/`，不进入 Git；其中含临时演示数据库和会话文件，不应打包发布。

输出：`verification.log`、`screenshots/*.png`（虚构测试夹具）、`real-api-pages/*.png`（真实本地 API / SQLite 演示数据）。真实记录见 `Verification/RESULTS.md`。

## 给成员06的手动方案

由成员01启动服务并完成握手后，可运行以下独立验收宿主（仅预览，不管理正式服务生命周期）：

```powershell
dotnet run --project src/OpenAHU.Desktop/Features/Verification/OpenAHU.Features.Verification.csproj `
  -c Release --no-build -- --preview "$env:LOCALAPPDATA/OpenAHU/session.json"
```

1. 首页核对虚构资料、演示声明和未知更新时间；六个标签页均正常加载。
2. 课表切换学期/周次；周一到周日排序、单双周筛选符合 API；启用“全学期”可编辑不在当前周的课程。
3. 新增虚构课程，先输入空名称、非法节次、超范围周次；应不发请求并给出提示。有效保存后列表出现记录。
4. 编辑新增课程的名称、周次，保存后关闭并重启后端/宿主；记录与修改后的周次仍在。
5. 用重叠时段触发 409；显示安全错误及追踪号，保留表单以便修改。服务中断时提示本地服务不可用；刷新后不显示旧学期数据。
6. 成绩空分数显示“未公布”；考试时间/地点未公布时分别提示；Links 仅展示/复制，Settings 无账号输入。
7. 在实际主窗口检查 100%/125%/150% DPI、960×650 和常用窗口大小、键盘操作与可访问性。

本次已做程序化 WPF 渲染和图像检查，**尚未做人工点击主窗口/DPI验收**，因为主窗口尚未交付。

## PR 提案与跨组待办

拟标题：`feat(desktop): 实现本地演示业务页与课程新增编辑`

拟摘要：新增只依赖 Contracts 的 WPF Features 模块，六页通过受会话保护的本地 HTTP 客户端读取 SQLite 演示数据，支持学期/周次筛选、未公布状态和课程新增编辑；附功能注册提案、可执行行为检查及隔离数据库持久化验收。无公共契约或数据库迁移修改。尚未创建、推送或合并远程 PR。

- 成员01：评审以上项目引用/页面注册方案，集成伴随进程、握手校验、会话重连及主窗口导航。
- 成员03：确认 `HealthDto` / `CourseUpsertRequest` 的评审状态；与04/05提供真实数据更新时间后，首页即可自动显示。
- 成员04/05：确认现有写入、共享课程与冲突语义；若添加乐观锁，先协商公共契约。本次不跨层修改。
- 成员06：将当前职责目录内验收脚本纳入正式 `tests/` / CI（由其执行迁移），完成主窗口交互、多 DPI、发行包伴随进程验收。不得把演示运行库、会话令牌或 SDK 临时目录打包。
