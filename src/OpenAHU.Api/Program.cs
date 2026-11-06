using OpenAHU.Api.Endpoints;
using OpenAHU.Api.Errors;
using OpenAHU.Api.Hosting;
using OpenAHU.Application;
using OpenAHU.Infrastructure;
using OpenAHU.Contracts.Errors;
using OpenAHU.Contracts.Json;

// 注意：这是受WPF控制的本机伴随进程，不对校园官网发送任何请求。
var builder=WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://127.0.0.1:0");
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize=32768);
var json=ContractJson.CreateOptions();
builder.Services.ConfigureHttpJsonOptions(o => {
    o.SerializerOptions.PropertyNamingPolicy=json.PropertyNamingPolicy;
    o.SerializerOptions.PropertyNameCaseInsensitive=json.PropertyNameCaseInsensitive;
    o.SerializerOptions.NumberHandling=json.NumberHandling;
});
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
var databaseStartupState = new DatabaseStartupState();
builder.Services.AddSingleton(databaseStartupState);
var sessionToken=SessionToken.Create();
builder.Services.AddSingleton(sessionToken);
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();

var handshakePath=builder.Configuration["OpenAHU:SessionPath"] ?? SessionHandshake.DefaultPath;
using var lease=SingleInstanceLease.Acquire(handshakePath);
var app=builder.Build();
app.UseExceptionHandler();
app.UseMiddleware<SessionTokenMiddleware>();
app.UseStatusCodePages(async ctx => {
    var h=ctx.HttpContext;
    if(h.Request.Path.StartsWithSegments("/api",StringComparison.OrdinalIgnoreCase))
    {
        var code=h.Response.StatusCode==405?ApiErrorCodes.InvalidRequest:ApiErrorCodes.NotFound;
        await ApiResults.WriteAsync(h,h.Response.StatusCode,code,
            h.Response.StatusCode==405?"不支持该HTTP方法。":"接口不存在。",h.RequestAborted);
    }
});
app.MapCampusEndpoints();
app.MapFallback((HttpContext ctx) => ApiResults.Error(404,ApiErrorCodes.NotFound,"接口不存在。",ctx));

// 初始化若失败也允许启动 /health 报 degraded，业务接口返回标准 503。
try
{
    using var scope=app.Services.CreateScope();
    var db=scope.ServiceProvider.GetRequiredService<CampusDbContext>();
    await DatabaseCompatibilityGuard.AssertCompatibleAsync(
        db, InfrastructureOptions.DatabasePath(app.Configuration), CancellationToken.None);
    await DemoDatabaseInitializer.InitializeAsync(db,CancellationToken.None);
    databaseStartupState.IsReady = true;
    app.Logger.LogInformation("本地SQLite已完成首次迁移与演示资料检查。");
}
catch(Exception e)
{
    app.Logger.LogError(e,"SQLite初始化失败：API仍可启动，但/health将报告降级。");
}
await app.StartAsync();
SessionHandshake.Write(handshakePath,SessionHandshake.Create(app.Urls,sessionToken.Value,Environment.ProcessId));
app.Logger.LogInformation("OpenAHU本地API已启动（127.0.0.1随机端口，令牌不记录在日志）。");
try { await app.WaitForShutdownAsync(); }
finally {
    SessionHandshake.Delete(handshakePath);
    await app.DisposeAsync();
}
