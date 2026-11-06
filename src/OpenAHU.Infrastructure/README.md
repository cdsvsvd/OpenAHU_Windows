# 成员05：SQLite数据库与仓储读写

已实现建库、迁移、演示数据、就绪检查，以及4号定义的8个仓储接口和IUnitOfWork。成员4交接包已原样导入 `src/OpenAHU.Application/` 作为依赖，未修改其源码。当前完成 Application → Infrastructure → SQLite 的读写验证；API端点和WPF页面由3号及前端成员接入。所有示例数据均为虚构资料。

## 已实现

- `Entities/`：students、terms、courses、course_weeks、enrollments、grades、exams、campus_links、app_settings 九张表。
- `Persistence/CampusDbContext.cs`：关系、复合主键、索引、校验约束和类型映射。
- `Migrations/`：正式首次迁移和模型快照；运行库不使用参考 SQL 或复制样例数据库建库。
- `Persistence/DemoData.cs`：仅对业务数据完全为空的库插入固定演示数据；记录 seed 版本，重复启动不重置修改或补回已删除的课程。
- `Persistence/CampusDatabase.cs`：受限目录创建、迁移前版本检查、初始化和只读健康探针。
- `DependencyInjection.cs`：`AddInfrastructure(IConfiguration)` 或 `AddInfrastructure(CampusDatabaseOptions)`。
- `Repositories/`：资料、学期、课表/周次、选课、成绩、考试和链接的真实SQLite读写；返回4号领域模型。
- `Persistence/EfUnitOfWork.cs`：与8个仓储共享Scoped DbContext；失败或取消时回滚并清理跟踪状态。
- `Verification/`：真实文件 SQLite 测试，使用独立临时目录。为遵守目录归属先放在5号目录，后续由6号决定是否迁入团队 tests。
- `Tools/`：独立数据库核验工具；必须显式指定绝对路径，可经4号真实业务服务执行课程CRUD。

## 最小验证

需要 .NET 10 SDK。以下命令从仓库根目录执行：

```powershell
dotnet build src/OpenAHU.Infrastructure/OpenAHU.Infrastructure.csproj -c Release
dotnet test src/OpenAHU.Infrastructure/Verification/OpenAHU.Infrastructure.Verification.csproj -c Release
$taskDatabase = Join-Path ([IO.Path]::GetTempPath()) 'OpenAHU-member05-demo/campus.db'
dotnet run --project src/OpenAHU.Infrastructure/Tools/OpenAHU.DatabaseTool.csproj -c Release -- --database-path $taskDatabase
dotnet run --project src/OpenAHU.Infrastructure/Tools/OpenAHU.DatabaseTool.csproj -c Release -- --database-path $taskDatabase --check-only
```

全新库应显示 `IsReady=true`，学生2、学期2、课程4、周次56、选课6、成绩4、考试3、链接2、设置4。设置表比任务包多一条 `demo_seed_version=v1`，用于防止重置数据。

运行库默认路径是 `%LocalAppData%/OpenAHU/campus.db`；配置键为 `OpenAHU:DatabasePath`。本次验证全部使用临时库或仓库外的工作目录，没有写入用户正式运行库。

完整交付和后续待办见 `docs/database/`。

## 当前验证状态

57项真实文件SQLite测试通过：基础27项＋仓储及业务集成30项。另在6个独立进程中完成新增→读取→修改→读取→删除→确认不存在；每次调用使用实际Application服务和EF仓储。

最新状态、3号接入方式和仍需协调的问题，以 `docs/database/05_读写接口交接与验证.md` 为准。先前阶段文档保留历史记录。
