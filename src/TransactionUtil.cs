using System.Data;
using Npgsql;

namespace eventstore_net;


public static class TransactionUtil
{
    public static async Task InTransaction(
        this NpgsqlConnection dbConnection,
        Func<Task> callback,
        CancellationToken ct = default
    )
    {
        if (dbConnection.State == ConnectionState.Closed)
            await dbConnection.OpenAsync(ct);

        await using var transaction = await dbConnection.BeginTransactionAsync(ct);

        try
        {
            await callback();

            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }
}