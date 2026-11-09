# 成员02实际验收记录

日期：2026-11-08（Asia/Shanghai）。

## 仓库和环境

- 远程：`https://github.com/cdsvsvd/-Windows-.git`。实际 `git ls-remote --symref ... HEAD` 返回 `refs/heads/main` 和下述 commit，未收到重定向提示。
- 基线 HEAD：`e4ec21e00145d26c8f0920a2d09d7528a3417939`，`Integrate member05 SQLite safeguards and updated dependencies`。
- 工作分支：`feat/ui-pages`。修改保留在工作区，未创建新 commit、未推送、未创建或合并 PR。
- OS：`Microsoft Windows 10.0.26200`，x64。
- 初始环境只有 .NET 运行时；已从微软官方安装脚本下载 SDK 至仓库忽略目录 `.dsh-tmp/dotnet/`。实际 `dotnet --version` 输出 `10.0.403`。
- 原始仓库无 Desktop 项目，因此 Features 为可独立编译的 WPF 类库，Verification 为独立验收宿主，不冒充已集成的正式 Shell。

## 最终实际命令

在仓库根目录执行：

```powershell
$env:DOTNET_CLI_HOME = "$PWD/.dsh-tmp/cli"
$env:NUGET_PACKAGES = "$PWD/.dsh-tmp/packages"
./src/OpenAHU.Desktop/Features/Verification/verify.ps1 `
  -Dotnet "$PWD/.dsh-tmp/dotnet/dotnet.exe" `
  -NuGetConfig "$PWD/.dsh-tmp/NuGet.Config" -Integration
```

退出码 `0`。脚本实际运行 restore、Release build、可执行行为断言、WPF 渲染和真实后端联调。关键输出原文：

```text
已成功生成。
    0 个警告
    0 个错误
PASS: 30 behavior assertions
PASS: six WPF views + course editor rendered; zero binding warnings/errors
PASS: real API reports SQLite ready
PASS: real SQLite terms load in ViewModel
PASS: UI ViewModel POST to real SQLite and refresh
PASS: UI ViewModel PUT updates course weeks transactionally;
PASS: restart reloads persisted edited course through new session
PASS: real nullable grade survives HTTP deserialization
PASS: six WPF views + course editor rendered; zero binding warnings/errors
PASS: API -> Application -> SQLite -> HTTP -> WPF ViewModel create/edit/restart/render
```

完整本机日志：`.dsh-tmp/verification/verification.log`。

后端第一次全量编译有 **10 条既有 CS1591（缺 XML 注释）警告、0 错误**；最终增量编译 0 警告、0 错误不代表已修复这些原有警告。Features 和验收项目编译均为 0 警告、0 错误。

另实际执行：

```powershell
./.dsh-tmp/dotnet/dotnet.exe test `
  src/OpenAHU.Desktop/Features/Verification/OpenAHU.Features.Verification.csproj `
  -c Release --no-build --no-restore
```

该命令退出 `0`、无测试发现输出。此项目不是 VSTest 项目，**不能将此结果当作测试已执行**；30 项断言和真实联调由上方 `verify.ps1` 调用的可执行宿主完成。

## 检查覆盖与图像

- 周次范围/中文逗号/去重/非法输入；必填与可选字段；非数字节次；结束节次小于开始节次。
- 本周过滤、周日排序、空周状态、全学期维护入口。
- POST/PUT 分流与保存后重新读取；409 保留表单和追踪号；已保存但刷新失败；不确定写入防止重复提交。
- 空分数和绩点保持 null；空成绩；未知考试排末尾；首页不伪造更新时间；health 降级。
- 较旧响应不能覆盖较新响应；空学期清除旧数据；下拉框实际绑定后刷新。
- 拒绝非 127.0.0.1 / 非 HTTP / 非根路径基地址；请求携带会话头；转义 termId；401 与无效 JSON 的 503；拒绝重定向响应。
- 真实 API 创建课程、更新名称与周次、退出并重启 API、更换会话后由全新 ViewModel 读取已持久化结果。
- 六个页面和课程编辑区从真实 SQLite/API 数据渲染；WPF 绑定警告/错误为零。

已查看 `.dsh-tmp/verification/real-api-pages/` 中的 Home、Timetable、Grades、Exams、Links、Settings 和课程编辑区图像；仅含虚构演示数据。列表长文本使用省略号及完整内容提示，编辑区保存/取消固定在底部。

## 过程中的限制及最终状态

- 沙盒内第一次 NuGet 还原因用户配置目录访问限制失败；正常权限还原成功。
- 沙盒内 PUT 返回 `HttpIOException: The response ended prematurely`；同一程序集在正常本机回环环境完成 PUT 和重启持久化验收，未修改后端绕过问题。最终验收脚本在该正常环境执行通过。
- 不触碰用户原有数据库；仅 API 进程访问独立测试数据库。客户端不引用 EF/SQLite，也不执行 SQL。
- 未进行人工点击、屏幕阅读器、多 DPI、正式 Shell 导航与发行包测试；具体步骤和成员依赖见上级 README。
- 未访问学校站点，无校园账号或真实个人信息。无数据库/迁移/公共 DTO 改动。

文件清单见同目录 `FILES.md`；PR 拟标题、摘要、回滚和跨组待办见上级 `README.md`。
