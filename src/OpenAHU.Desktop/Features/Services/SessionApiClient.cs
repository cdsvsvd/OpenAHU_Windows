using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using OpenAHU.Contracts.Dtos;
using OpenAHU.Contracts.Errors;
using OpenAHU.Contracts.Json;
using OpenAHU.Contracts.Local;

namespace OpenAHU.Desktop.Features.Services;

public sealed class FeatureApiException(HttpStatusCode status, ApiError? error) : Exception(error?.Message)
{
    public HttpStatusCode Status { get; } = status;
    public ApiError? Error { get; } = error;
}

/// <summary>Shell supplies a validated handshake. No database or handshake-file access here.</summary>
public sealed class SessionApiClient : IFeatureApi, IDisposable
{
    private readonly HttpClient _http;
    private readonly string _token;
    private readonly JsonSerializerOptions _json = ContractJson.CreateOptions();

    public SessionApiClient(Uri baseUri, string token)
        : this(baseUri, token, new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false, UseCookies = false }) { }

    internal SessionApiClient(Uri baseUri, string token, HttpMessageHandler handler)
    {
        if (!baseUri.IsAbsoluteUri || baseUri.Scheme != "http" || baseUri.Host != "127.0.0.1" ||
            baseUri.Port is < 1 or > 65535 || baseUri.AbsolutePath != "/" ||
            baseUri.Query.Length != 0 || baseUri.Fragment.Length != 0 || baseUri.UserInfo.Length != 0)
            throw new ArgumentException("仅允许握手提供的 http://127.0.0.1:端口 地址。", nameof(baseUri));
        if (string.IsNullOrWhiteSpace(token) || token.Any(char.IsControl))
            throw new ArgumentException("本地会话令牌无效。", nameof(token));
        _token = token;
        _http = new HttpClient(handler) { BaseAddress = baseUri, Timeout = TimeSpan.FromSeconds(15) };
    }

    public Task<StudentProfileDto> GetProfileAsync(CancellationToken ct) => Get<StudentProfileDto>("profile", ct);
    public Task<DataStatusDto> GetDataStatusAsync(CancellationToken ct) => Get<DataStatusDto>("data-status", ct);
    public Task<HealthDto> GetHealthAsync(CancellationToken ct) => Get<HealthDto>("health", ct);
    public Task<TermDto[]> GetTermsAsync(CancellationToken ct) => Get<TermDto[]>("terms", ct);
    public Task<CourseDto[]> GetCoursesAsync(string termId, CancellationToken ct) => Get<CourseDto[]>(Query("courses", termId), ct);
    public Task<GradeDto[]> GetGradesAsync(string termId, CancellationToken ct) => Get<GradeDto[]>(Query("grades", termId), ct);
    public Task<ExamDto[]> GetExamsAsync(string termId, CancellationToken ct) => Get<ExamDto[]>(Query("exams", termId), ct);
    public Task<LinkDto[]> GetLinksAsync(CancellationToken ct) => Get<LinkDto[]>("links", ct);
    public Task<CourseDto> CreateCourseAsync(CourseUpsertRequest request, CancellationToken ct) =>
        Send<CourseDto>(HttpMethod.Post, "local/courses", request, ct);
    public Task<CourseDto> UpdateCourseAsync(string id, CourseUpsertRequest request, CancellationToken ct) =>
        Send<CourseDto>(HttpMethod.Put, "local/courses/" + Uri.EscapeDataString(id), request, ct);

    private static string Query(string resource, string termId) => resource + "?termId=" + Uri.EscapeDataString(termId);
    private Task<T> Get<T>(string path, CancellationToken ct) => Send<T>(HttpMethod.Get, path, null, ct);

    private async Task<T> Send<T>(HttpMethod method, string path, CourseUpsertRequest? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, "api/v1/" + path);
        request.Headers.Add("X-OpenAHU-Session", _token);
        if (body is not null) request.Content = JsonContent.Create(body, options: _json);
        using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            ApiError? error = null;
            try { error = await response.Content.ReadFromJsonAsync<ApiError>(_json, ct).ConfigureAwait(false); }
            catch (JsonException) { /* HTML/empty errors still map to a safe local-service message. */ }
            throw new FeatureApiException(response.StatusCode, error);
        }
        return await response.Content.ReadFromJsonAsync<T>(_json, ct).ConfigureAwait(false)
            ?? throw new JsonException("本地服务返回空响应。");
    }

    public void Dispose() => _http.Dispose();
}
