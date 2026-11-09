using OpenAHU.Contracts.Dtos;
using OpenAHU.Contracts.Local;

namespace OpenAHU.Desktop.Features.Services;

// Feature boundary only; DTOs and write payloads remain owned by Contracts.
public interface IFeatureApi
{
    Task<StudentProfileDto> GetProfileAsync(CancellationToken ct);
    Task<DataStatusDto> GetDataStatusAsync(CancellationToken ct);
    Task<HealthDto> GetHealthAsync(CancellationToken ct);
    Task<TermDto[]> GetTermsAsync(CancellationToken ct);
    Task<CourseDto[]> GetCoursesAsync(string termId, CancellationToken ct);
    Task<GradeDto[]> GetGradesAsync(string termId, CancellationToken ct);
    Task<ExamDto[]> GetExamsAsync(string termId, CancellationToken ct);
    Task<LinkDto[]> GetLinksAsync(CancellationToken ct);
    Task<CourseDto> CreateCourseAsync(CourseUpsertRequest request, CancellationToken ct);
    Task<CourseDto> UpdateCourseAsync(string id, CourseUpsertRequest request, CancellationToken ct);
}
