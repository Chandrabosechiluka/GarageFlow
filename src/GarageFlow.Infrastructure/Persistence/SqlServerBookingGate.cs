using System.Data;
using GarageFlow.Application.Abstractions;
using GarageFlow.Application.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GarageFlow.Infrastructure.Persistence;

/// <summary>
/// Uses a SQL Server "application lock" (sp_getapplock) inside a transaction.
/// The lock is just a named token held by the transaction: whoever holds "booking:{shopId}" runs, everyone else
/// waits (up to 10 seconds) until the holder commits or rolls back, which releases the lock automatically.
/// It works across any number of API instances because the lock lives in the database, not in this process.
/// </summary>
public class SqlServerBookingGate(GarageFlowDbContext db) : IBookingGate
{
    private const int LockTimeoutMilliseconds = 10_000;

    public async Task<T> RunExclusiveAsync<T>(string key, Func<CancellationToken, Task<T>> action, CancellationToken ct = default)
    {
        // Disposing the transaction without Commit rolls everything back, including when the action throws.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        await AcquireLockAsync(key, transaction, ct);
        var result = await action(ct);

        await transaction.CommitAsync(ct); // commit BEFORE the lock is released, so the next caller sees our booking
        return result;
    }

    private async Task AcquireLockAsync(string key, IDbContextTransaction transaction, CancellationToken ct)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "sp_getapplock";

        AddParameter(command, "@Resource", DbType.String, $"GarageFlow:{key}");
        AddParameter(command, "@LockMode", DbType.String, "Exclusive");
        AddParameter(command, "@LockOwner", DbType.String, "Transaction"); // released automatically when the transaction ends
        AddParameter(command, "@LockTimeout", DbType.Int32, LockTimeoutMilliseconds);

        var returnValue = command.CreateParameter();
        returnValue.ParameterName = "@ReturnValue";
        returnValue.DbType = DbType.Int32;
        returnValue.Direction = ParameterDirection.ReturnValue;
        command.Parameters.Add(returnValue);

        await command.ExecuteNonQueryAsync(ct);

        // 0 or 1 = lock granted. Negative = timed out, deadlock victim, or error.
        if ((int)returnValue.Value! < 0)
            throw new ConflictException("Another booking is being processed right now. Please try again in a moment.");
    }

    private static void AddParameter(System.Data.Common.DbCommand command, string name, DbType type, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
