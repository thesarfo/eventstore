namespace eventstore_net;

public static class EventStoreExtensions
{
    public static async Task<T> AggregateStreamsAsync<T>(
        this EventStore eventStore,
        Func<T> getDefault,
        Func<T, object, T> evolve,
        Guid streamId,
        long? atStreamVersion = null,
        DateTime? atTimestamp = null,
        CancellationToken ct = default) where T : notnull
    {
        var events = await eventStore.GetEventsAsync(streamId, atStreamVersion, atTimestamp, ct);

        var aggregate = getDefault();

        return events.Aggregate(aggregate, (current, @event) => evolve(current, @event));
    }

    public static async Task Handle<TEntity>(
        this EventStore eventStore,
        Func<TEntity> getDefault,
        Func<TEntity, object, TEntity> evolve,
        Func<object, TEntity, object[]> decide,
        Guid streamId,
        object command,
        long? expectedVersion = null,
        CancellationToken ct = default
    ) where TEntity : notnull
    {
        var entity = await eventStore.AggregateStreamsAsync(
            getDefault,
            evolve,
            streamId,
            ct: ct);

        var newEvents = decide(command, entity);

        eventStore.AppendEvent<TEntity>(streamId, newEvents, expectedVersion);
    }
}