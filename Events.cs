public record BankAccountOpened(
    Guid BankAccountId,
    string AccountNumber,
    Guid ClientId,
    string CurrentyISOCode,
    DateTime CreatedAt,
    long Version
);

public record DepositRecoreded(
    Guid BankAccountId,
    decimal Amount,
    Guid CashierId,
    DateTime RecordedAt,
    long Version
);

public record CashWithdrawnFromATM(
    Guid BankAccountId,
    decimal Amount,
    Guid ATMId,
    DateTime RecordedAt,
    long Version
);

public record BankAccountClosed(
    Guid BankAccountId,
    string Reason,
    DateTime ClosedAt,
    long Version
);

public enum BankAccountStatus
{
    Opened,
    Closed
};

public record BankAccount(
    Guid Id,
    BankAccountStatus Status,
    decimal Balance,
    long Version = 0
)
{
    private static BankAccount Create(BankAccountOpened @event) => new BankAccount(
        @event.BankAccountId,
        BankAccountStatus.Opened,
        0,
        @event.Version
    );

    private BankAccount Apply(DepositRecoreded @event) => this with
    {
        Balance = Balance + @event.Amount,
        Version = @event.Version
    };

    private BankAccount Apply(CashWithdrawnFromATM @event) => this with
    {
        Balance = Balance - @event.Amount,
        Version = @event.Version
    };

    private BankAccount Apply(BankAccountClosed @event) => this with
    {
        Status = BankAccountStatus.Closed,
        Version = @event.Version
    };

    public static BankAccount Evolve(BankAccount bankAccount, object @event)
    {
        return @event switch
        {
            BankAccountOpened bankAccountCreated => Create(bankAccountCreated),
            DepositRecoreded depositRecoreded => bankAccount.Apply(depositRecoreded),
            CashWithdrawnFromATM cashWithdrawnFromATM => bankAccount.Apply(cashWithdrawnFromATM),
            BankAccountClosed bankAccountClosed => bankAccount.Apply(bankAccountClosed),
            _ => bankAccount
        };
    }
}