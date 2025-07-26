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
);