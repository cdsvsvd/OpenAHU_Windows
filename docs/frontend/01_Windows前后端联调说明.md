# OpenAHU Windows 前端与后端联调说明

## 集成基线
- 前端来源：用户上传的`1.zip`，内含1号WPF Shell与2号业务页面。压缩包内的旧Backend和`bin/obj`缓存被排除，不影响此前3/4/5号已经经过Windows CI验证的后端。
- 单一数据流：`WPF View + ViewModel -> SessionApiClient -> 127.0.0.1本机API -> Application -> Infrastructure -> SQLite`。
- 协议使用原有11条API及公开DTO，不复制其他成员的源码版本。

## 本次完善
1. Shell切换页面时触发该页`ActivateAsync()`并执行真实API刷新；主内容区域`HasPage`状态在首次加载和断线时通知WPF绑定，避免错误遮罩覆盖页面。
2. 启动器等待新进程握手时必须验证`ProcessId`一致，避免误读崩溃残留的旧session.json；自定义握手路径会传给伴随API；连接失败时回收自建子进程，允许重试。
3. 课表种子课程和共享课程不开放编辑，客户端只允许编辑`local-*`，与后端权限边界一致。新增本地课程仍保持原有CRUD请求。
4. 同步建立`OpenAHU.slnx`包含后端、前端、Features及Verification；Windows CI增加WPF编译和行为断言。
5. 不上传压缩包里的编译缓存、虚构演示数据库或会话令牌。

## Windows本地运行
```powershell
dotnet restore OpenAHU.slnx
dotnet build OpenAHU.slnx -c Release
dotnet run --project src/OpenAHU.Desktop/OpenAHU.Desktop.csproj -c Release
```
若以独立方式调试桌面程序，可以通过环境变量`OPENAHU_API_PATH`指定后端的exe/dll/csproj路径。后端按配置生成本机随机端口及私有会话令牌，不监听公网端口。

## 仍需手工验收
- 六个页面在首次进入时确实刷新；导航切换、断网、数据库损坏、重新连接后的状态是否符合提示。
- 100%、125%、150%屏幕缩放，键盘导航，窗口最小尺寸及视觉可读性。
- 打包时含WPF应用和本地API伴随进程，独立用户机器可启动；不得打包`session.json`或已写入个人数据的SQLite。
- 项目当前是面向学习的原型，不宣称校园官方功能或真实数据接入。
