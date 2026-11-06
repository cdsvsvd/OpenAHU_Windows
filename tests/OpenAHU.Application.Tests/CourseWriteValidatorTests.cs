using OpenAHU.Application.Models;
using OpenAHU.Application.Services;
using OpenAHU.Application.Tests.Fakes;
using Xunit;

namespace OpenAHU.Application.Tests;

/// <summary>
/// 字段校验是纯函数，直接测；不需要仓储、不需要异步。
/// 这一组用例的价值在于：所有字段名的拼写必须与 <c>CourseUpsertRequest</c> 的 JSON 字段一致，
/// 否则 2 号拿到的 <c>field</c> 无法定位到输入框。
/// </summary>
public sealed class CourseWriteValidatorTests
{
    private static readonly Term Term = new("term-2026-fall", "2026年秋季学期（演示）", new DateOnly(2026, 9, 7), 18, true);

    [Fact]
    public void 空课程名被拒绝()
    {
        var result = CourseWriteValidator.Validate(ApplicationHarness.Draft(name: "   "), Term);

        Assert.NotNull(result);
        Assert.Equal(CourseWriteStatus.ValidationFailed, result!.Status);
        Assert.Equal("name", result.Field);
    }

    [Fact]
    public void 课程名超过六十个字符被拒绝()
    {
        var result = CourseWriteValidator.Validate(
            ApplicationHarness.Draft(name: new string('课', 61)), Term);

        Assert.NotNull(result);
        Assert.Equal("name", result!.Field);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    public void 起始节次越界被拒绝(int startSection)
    {
        var result = CourseWriteValidator.Validate(
            ApplicationHarness.Draft(startSection: startSection, endSection: 21), Term);

        Assert.NotNull(result);
        Assert.Equal("startSection", result!.Field);
    }

    [Fact]
    public void 结束节次小于起始节次被拒绝()
    {
        var result = CourseWriteValidator.Validate(
            ApplicationHarness.Draft(startSection: 5, endSection: 4), Term);

        Assert.NotNull(result);
        Assert.Equal("endSection", result!.Field);
    }

    [Fact]
    public void 周次为空被拒绝()
    {
        var result = CourseWriteValidator.Validate(
            ApplicationHarness.Draft(weeks: Array.Empty<int>()), Term);

        Assert.NotNull(result);
        Assert.Equal("weekNumbers", result!.Field);
    }

    [Fact]
    public void 周次超出学期总周数被拒绝()
    {
        // 学期共 18 周，第 19 周不合法。
        var result = CourseWriteValidator.Validate(
            ApplicationHarness.Draft(weeks: new[] { 1, 19 }), Term);

        Assert.NotNull(result);
        Assert.Equal("weekNumbers", result!.Field);
        Assert.Contains("18", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void 非法星期值被拒绝()
    {
        var result = CourseWriteValidator.Validate(
            ApplicationHarness.Draft() with { DayOfWeek = (DayOfWeek)99 }, Term);

        Assert.NotNull(result);
        Assert.Equal("dayOfWeek", result!.Field);
    }

    [Fact]
    public void 合法草稿通过校验()
    {
        var result = CourseWriteValidator.Validate(ApplicationHarness.Draft(), Term);

        Assert.Null(result);
    }
}
