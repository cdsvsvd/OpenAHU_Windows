using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using OpenAHU.Application.Errors;
using OpenAHU.Infrastructure.Persistence;

namespace OpenAHU.Infrastructure.Repositories;

internal static class StoreAccess
{
    public static async Task<T> RunAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return await operation();
        }
        catch (Exception exception) when (exception is DbException or DbUpdateException or IOException or UnauthorizedAccessException)
        {
            // The inner exception is diagnostic-only. Never include SQL, paths or connection strings in Message.
            throw new CampusStoreUnavailableException("本地数据库不可用（读取或保存失败），请稍后重试。", exception);
        }
    }

    public static Task RunAsync(Func<Task> operation, CancellationToken cancellationToken) =>
        RunAsync(async () => { await operation(); return true; }, cancellationToken);

    public static void RequireTransaction(CampusDbContext db)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("仓储写入必须在 IUnitOfWork.ExecuteAsync 事务中执行。");
    }
}
