using Dapper;
using Npgsql;

namespace eventstore_net;


public class CashierDashboard
{
    public Guid Id { get; set; }
    public string CashierName { get; set; } = default!;
    public int RecordedDepositsCount { get; set; }
    public decimal TotalBalance { get; set; }
}

public class CashierDashboardProjection: Projection
{
    private readonly NpgsqlConnection _dbConnection;

    public override void Init()
    {
        const string sql =
            @"CREATE TABLE IF NOT EXISTS CashierDashboards (
                      Id                     UUID              NOT NULL    PRIMARY KEY,
                      CashierName            TEXT              NOT NULL,
                      RecordedDepositsCount  integer           NOT NULL,
                      TotalBalance           decimal           NOT NULL
                );";
        _dbConnection.Execute(sql);
    }

    public CashierDashboardProjection(NpgsqlConnection dbConnection)
    {
        this._dbConnection = dbConnection;

        Projects<CashierEmployed>(Apply);
        Projects<DepositRecorded>(Apply);
    }

    private Task Apply(CashierEmployed @event, CancellationToken ct) =>
        _dbConnection.ExecuteAsync(
            @"INSERT INTO CashierDashboards (Id, CashierName, RecordedDepositsCount, TotalBalance)
                    VALUES (@CashierId, @Name, 0, 0)",
            @event
        );

    private Task Apply(DepositRecorded @event, CancellationToken ct) =>
        _dbConnection.ExecuteAsync(
            @"UPDATE CashierDashboards
                    SET TotalBalance = TotalBalance + @Amount, RecordedDepositsCount = RecordedDepositsCount + 1
                    WHERE Id = @CashierId",
            @event
        );
}