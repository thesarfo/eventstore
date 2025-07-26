namespace eventstore_net;

public static class BankAccountExtensions
{
    public static Task<BankAccount> GetBankAccount(
        this EventStore eventStore,
        Guid streamId,
        long? atStreamVersion = null,
        DateTime? atTimestamp = null,
        CancellationToken ct = default
    ) =>
        eventStore.AggregateStreamsAsync(ObjectFactory<BankAccount>.GetEmpty,
            BankAccount.Evolve,
            streamId,
            atStreamVersion,
            atTimestamp,
            ct
        );


    public static Task Handle(
        this EventStore eventStore,
        Guid streamId,
        object command,
        long? expectedVersion = null,
        CancellationToken ct = default
    ) =>
        eventStore.Handle(
            ObjectFactory<BankAccount>.GetEmpty,
            BankAccount.Evolve,
            (_, account) => new[] { BankAccountService.Handle(command, account) },
            streamId,
            command,
            expectedVersion,
            ct
        );

}