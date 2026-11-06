namespace OpenAHU.Application.Models;

/// <summary>
/// 本地常用入口（对应 SQLite <c>campus_links</c> 表）。
/// 首版只含项目开发相关链接，不指向任何学校业务站点。
/// </summary>
/// <param name="Id">链接主键。</param>
/// <param name="Title">展示标题。</param>
/// <param name="Url">绝对 URL（http/https）。</param>
/// <param name="Category">分组。</param>
public sealed record CampusLink(
    string Id,
    string Title,
    string Url,
    string Category);
