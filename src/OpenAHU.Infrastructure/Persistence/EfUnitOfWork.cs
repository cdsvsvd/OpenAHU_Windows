using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenAHU.Application.Abstractions;
using OpenAHU.Infrastructure.Repositories;

namespace OpenAHU.Infrastructure.Persistence;

internal sealed class EfUnitOfWork(CampusDbContext db, ILogger<EfUnitOfWork> logger) : IUnitOfWork
{
    public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken) =>
        StoreAccess.RunAsync(async () =>
        {
            ArgumentNullException.ThrowIfNull(operation);
            if (db.Database.CurrentTransaction is not null)
                throw new InvalidOperationException("不支持嵌套 IUnitOfWork 事务。");

            var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            Exception? originalFailure = null;
            try
            {
                var result = await operation(cancellationToken);
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch (Exception exception)
            {
                originalFailure = exception;
                try { await transaction.RollbackAsync(CancellationToken.None); }
                catch (Exception rollbackFailure)
                {
                    logger.LogError(rollbackFailure, "数据库事务回滚失败，保留原始异常。");
                }
                // Rolled-back entities must not be silently saved by a later operation in this scope.
                db.ChangeTracker.Clear();
                throw;
            }
            finally
            {
                try { await transaction.DisposeAsync(); }
                catch (Exception disposeFailure) when (originalFailure is not null)
                {
                    logger.LogError(disposeFailure, "数据库事务清理失败，保留原始异常。");
                }
            }
        }, cancellationToken);
}
