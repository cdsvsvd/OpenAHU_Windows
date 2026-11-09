using OpenAHU.Contracts.Dtos;
using OpenAHU.Contracts.Local;

namespace OpenAHU.Application;

/// <summary>学期存在性、课程字段、上课周次与业务边界校验。</summary>
public sealed class CampusService : ICampusService
{
    private readonly ICampusRepository _repository;
    public CampusService(ICampusRepository repository) => _repository = repository;

    public async Task<StudentProfileDto> GetProfileAsync(CancellationToken ct) =>
        await _repository.GetProfileAsync(ct) ??
        throw new CampusException(CampusErrorKind.DbUnavailable, "当前演示档案不可用，请检查本地数据库。");

    public Task<IReadOnlyList<TermDto>> GetTermsAsync(CancellationToken ct) => _repository.GetTermsAsync(ct);
    public Task<IReadOnlyList<LinkDto>> GetLinksAsync(CancellationToken ct) => _repository.GetLinksAsync(ct);

    private async Task<TermDto> RequireTermAsync(string? termId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(termId) || termId.Length > 100)
            throw new CampusException(CampusErrorKind.InvalidRequest, "请提供有效的学期编号termId。");
        return await _repository.GetTermAsync(termId, ct)
            ?? throw new CampusException(CampusErrorKind.NotFound, "所选学期不存在。");
    }

    public async Task<IReadOnlyList<CourseDto>> GetCoursesAsync(string termId, CancellationToken ct)
    {
        await RequireTermAsync(termId, ct);
        return await _repository.GetCoursesAsync(termId, ct);
    }
    public async Task<IReadOnlyList<GradeDto>> GetGradesAsync(string termId, CancellationToken ct)
    {
        await RequireTermAsync(termId, ct);
        return await _repository.GetGradesAsync(termId, ct);
    }
    public async Task<IReadOnlyList<ExamDto>> GetExamsAsync(string termId, CancellationToken ct)
    {
        await RequireTermAsync(termId, ct);
        return await _repository.GetExamsAsync(termId, ct);
    }

    private async Task ValidateCourseAsync(CourseUpsertRequest? r, CancellationToken ct)
    {
        if (r is null)
            throw new CampusException(CampusErrorKind.InvalidRequest, "请求体不能为空。");

        // This pure validator preserves the frozen DTO and API error shape, but provides
        // actionable field names so the Windows form can highlight invalid entries.
        CourseWriteValidator.ValidateFields(r);
        var term = await RequireTermAsync(r.TermId, ct);
        CourseWriteValidator.ValidateWeeks(r, term.WeekCount);
    }

    public async Task<CourseDto> CreateCourseAsync(CourseUpsertRequest request, CancellationToken ct)
    {
        await ValidateCourseAsync(request, ct);
        return await _repository.CreateCourseAsync(request, ct);
    }
    public async Task<CourseDto> UpdateCourseAsync(string id, CourseUpsertRequest request, CancellationToken ct)
    {
        RequireId(id);
        await ValidateCourseAsync(request, ct);
        return await _repository.UpdateCourseAsync(id, request, ct);
    }
    public Task DeleteCourseAsync(string id, CancellationToken ct)
    {
        RequireId(id);
        return _repository.DeleteCourseAsync(id, ct);
    }
    private static void RequireId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 100)
            throw new CampusException(CampusErrorKind.InvalidRequest, "课程编号无效。");
    }
}
