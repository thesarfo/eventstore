
using eventstore_net.Event;
using eventstore_net.Util;

namespace eventstore_net.BankAccount;

public static class BankAccountExtensions
{
    public static Task<Event.BankAccount> GetBankAccount(
        this EventStore eventStore,
        Guid streamId,
        long? atStreamVersion = null,
        DateTime? atTimestamp = null,
        CancellationToken ct = default
    ) =>
        eventStore.AggregateStreamsAsync(ObjectFactory<Event.BankAccount>.GetEmpty,
            Event.BankAccount.Evolve,
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
            ObjectFactory<Event.BankAccount>.GetEmpty,
            Event.BankAccount.Evolve,
            (_, account) => new[] { BankAccountService.Handle(command, account) },
            streamId,
            command,
            expectedVersion,
            ct
        );

}