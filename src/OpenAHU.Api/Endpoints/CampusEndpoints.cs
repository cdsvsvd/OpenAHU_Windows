using System.Text.Json;
using OpenAHU.Api.Errors;
using OpenAHU.Infrastructure;
using OpenAHU.Application;
using OpenAHU.Contracts.Dtos;
using OpenAHU.Contracts.Errors;
using OpenAHU.Contracts.Json;
using OpenAHU.Contracts.Local;

namespace OpenAHU.Api.Endpoints;

/// <summary>完整的 v3 HTTP 接口。所有业务操作经 Application 服务访问 SQLite。</summary>
public static class CampusEndpoints
{
    private static readonly string ApiVersion =
        typeof(CampusEndpoints).Assembly.GetName().Version?.ToString() ?? "0.0.0";

    public static IEndpointRouteBuilder MapCampusEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group=endpoints.MapGroup("/api/v1");
        group.MapGet("/health",async (ICampusRepository repo,DatabaseStartupState startup,HttpContext context) =>
        {
            var ready=startup.IsReady && await repo.IsReadyAsync(context.RequestAborted);
            return Results.Ok(new HealthDto(ready?"ok":"degraded",ready,ApiVersion));
        });
        group.MapGet("/data-status",()=>Results.Ok(new DataStatusDto(
            Provider:"LocalSQLite",IsDemo:true,UpdatedAt:null,Message:"本地演示数据，非学校官方信息")));

        group.MapGet("/profile",async (ICampusService service,HttpContext context) =>
            Results.Ok(await service.GetProfileAsync(context.RequestAborted)));
        group.MapGet("/terms",async (ICampusService service,HttpContext context) =>
            Results.Ok(await service.GetTermsAsync(context.RequestAborted)));
        group.MapGet("/courses",async (ICampusService service,HttpContext context) =>
            Results.Ok(await service.GetCoursesAsync(TermId(context),context.RequestAborted)));
        group.MapGet("/grades",async (ICampusService service,HttpContext context) =>
            Results.Ok(await service.GetGradesAsync(TermId(context),context.RequestAborted)));
        group.MapGet("/exams",async (ICampusService service,HttpContext context) =>
            Results.Ok(await service.GetExamsAsync(TermId(context),context.RequestAborted)));
        group.MapGet("/links",async (ICampusService service,HttpContext context) =>
            Results.Ok(await service.GetLinksAsync(context.RequestAborted)));

        group.MapPost("/local/courses",async (ICampusService service,HttpContext context) =>
        {
            var request=await ReadCourseAsync(context);
            var course=await service.CreateCourseAsync(request,context.RequestAborted);
            return Results.Json(course,statusCode:StatusCodes.Status201Created);
        });
        group.MapPut("/local/courses/{id}",async (string id,ICampusService service,HttpContext context) =>
        {
            var request=await ReadCourseAsync(context);
            var course=await service.UpdateCourseAsync(id,request,context.RequestAborted);
            return Results.Ok(course);
        });
        group.MapDelete("/local/courses/{id}",async (string id,ICampusService service,HttpContext context) =>
        {
            await service.DeleteCourseAsync(id,context.RequestAborted);
            return Results.NoContent();
        });
        return endpoints;
    }

    private static string TermId(HttpContext ctx)
    {
        var q=ctx.Request.Query["termId"];
        if (q.Count!=1 || string.IsNullOrWhiteSpace(q[0]) || q[0]!.Length>100)
            throw new CampusException(CampusErrorKind.InvalidRequest,"请提供且仅提供一个有效的termId。");
        return q[0]!;
    }

    private static async Task<CourseUpsertRequest> ReadCourseAsync(HttpContext context)
    {
        if (context.Request.ContentLength is > 32768 ||
            context.Request.ContentType is null ||
            !context.Request.ContentType.StartsWith("application/json",StringComparison.OrdinalIgnoreCase))
            throw new CampusException(CampusErrorKind.InvalidRequest,"请提供不超过32KB的JSON请求体。");
        try
        {
            return await context.Request.ReadFromJsonAsync<CourseUpsertRequest>(
                ContractJson.CreateOptions(),context.RequestAborted)
                ?? throw new CampusException(CampusErrorKind.InvalidRequest,"JSON请求体不能为空。");
        }
        catch (JsonException)
        {
            throw new CampusException(CampusErrorKind.InvalidRequest,"JSON字段类型或格式不正确。");
        }
        catch (BadHttpRequestException)
        {
            throw new CampusException(CampusErrorKind.InvalidRequest,"请求体无法读取或内容不完整。");
        }
        catch (NotSupportedException)
        {
            throw new CampusException(CampusErrorKind.InvalidRequest,"不支持的请求体格式。");
        }
    }
}
