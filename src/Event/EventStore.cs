using System.Collections;
using System.Data;
using Dapper;
using eventstore_net.Projection;
using eventstore_net.tests;
using eventstore_net.Util;
using Newtonsoft.Json;
using Npgsql;

namespace eventstore_net.Event;

public class EventStore : IDisposable
{
    
    private const string Apply = "Apply";
    private const string Version = "Version";
    
    private readonly NpgsqlConnection _dbConnection;
    private readonly Dictionary<Type, List<IProjection>> projections = new();

    public EventStore(NpgsqlConnection dbConnection)
    {
        this._dbConnection = dbConnection;
    }

    /// <summary>
    /// Sets up the necessary tables for event sourcing, i.e. the streams and events tables.
    /// </summary>
    public void Init()
    {
        CreateStreamsTable();
        CreateEventsTable();
        CreateAppendEventFunction();
        InitProjections();
    }

    public void RegisterProjection(IProjection projection)
    {
        foreach (var eventType in projection.Handles)
        {
            if (!projections.ContainsKey(eventType))
            {
                projections[eventType] = new List<IProjection>();
                
                projections[eventType].Add(projection);
            }
        }
    }

    private async Task ApplyProjections(object @event, CancellationToken ct)
    {
        if (!projections.ContainsKey(@event.GetType()))
        {
            return;
        }
        
        foreach(var projection in projections[@event.GetType()])
        {
            await projection.Handle(@event, ct);
        }
    }

    private void InitProjections()
    {
        foreach (var projection in projections.Values.SelectMany(p => p))
        {
            projection.Init();
        }
    }

    public async Task<IReadOnlyList<object>> GetEventsAsync(
        Guid streamId,
        long? atStreamVersion = null,
        DateTime? atTimestamp = null,
        CancellationToken ct = default)
    {
        var atStreamCondition = atStreamVersion != null ? "AND version <= @atStreamVersion" : string.Empty;
        var atTimestampCondition = atTimestamp != null ? "AND created <= @atTimestamp" : string.Empty;
        
        var getStreamSql =
            $@"$SELECT id, data, stream_id, type, version, created
                  FROM events
                  WHERE stream_id = @streamId
                  {atStreamCondition}
                  {atTimestampCondition}
                  ORDER BY version";

        var events = await _dbConnection
            .QueryAsync<dynamic>(getStreamSql, new { streamId });
        
        return events.Select(@event =>
                JsonConvert.DeserializeObject(
                    @event.data,
                    Type.GetType(@event.type, true)!
                ))
            .ToList();
    }

    public bool AppendEvent<TStream>(Guid streamId, object @event, long? expectedVersion = null) =>
        _dbConnection.QuerySingle<bool>(
            "SELECT append_event(@Id, @Data::jsonb, @Type, @StreamId, @StreamType, @ExpectedVersion)",
            new
            {
                Id = Guid.NewGuid(),
                Data = JsonConvert.SerializeObject(@event),
                Type = @event.GetType().AssemblyQualifiedName,
                StreamId = streamId,
                StreamType = typeof(TStream).AssemblyQualifiedName,
                ExpectedVersion = expectedVersion
            },
            commandType: CommandType.Text
        );
    
    public Task AppendEventsAsync<TStream>(
        Guid streamId,
        IEnumerable<object> events,
        long? expectedVersion = null,
        CancellationToken ct = default
    ) where TStream : notnull =>
        _dbConnection.InTransaction(async () =>
            {
                foreach (var @event in events)
                {
                    var succeeded = await _dbConnection.QuerySingleAsync<bool>(
                        "SELECT append_event(@Id, @Data::jsonb, @Type, @StreamId, @StreamType, @ExpectedVersion)",
                        new
                        {
                            Id = Guid.NewGuid(),
                            Data = JsonConvert.SerializeObject(@event),
                            Type = @event.GetType().AssemblyQualifiedName,
                            StreamId = streamId,
                            StreamType = typeof(TStream).AssemblyQualifiedName,
                            ExpectedVersion = expectedVersion++
                        },
                        commandType: CommandType.Text
                    );

                    if (!succeeded)
                        throw new InvalidOperationException("Expected version did not match the stream version!");

                    await ApplyProjections(@event, ct);
                }
            },
            ct
        );
    
    
    private void CreateAppendEventFunction()
    {
        const string appendEventFunctionSql =
            @"CREATE OR REPLACE FUNCTION append_event(
            id uuid,
            data jsonb,
            type text,
            stream_id uuid,
            stream_type text,
            expected_stream_version bigint default null
        ) RETURNS boolean
        LANGUAGE plpgsql
        AS $$
        DECLARE
            stream_version int;
        BEGIN
            --get stream version
            SELECT version INTO stream_version FROM streams as s WHERE s.id = stream_id FOR UPDATE;

            -- if stream doesnt exist - create new one with version 0
            IF stream_version IS NULL THEN
                stream_version := 0;

                INSERT INTO streams (id, type, version) VALUES (stream_id, stream_type, stream_version);
            END IF;

            -- check optimistic concurrency
            IF expected_stream_version IS NOT NULL AND stream_version != expected_stream_version THEN
                RETURN FALSE;
            END IF;

            -- increment stream version
            stream_version := stream_version + 1;

            -- append event
            INSERT INTO events
            (id, data, type, stream_id, version)
            VALUES
            (id, data::jsonb, stream_id, type, stream_version)

            -- update stream version
            UPDATE streams as s
                SET version = stream_version
            WHERE s.id = stream_id;
            RETURN TRUE;


        END;
        $$";

        _dbConnection.Execute(appendEventFunctionSql);
    }
    
    public StreamState GetStreamState(Guid streamId) =>
        throw new NotImplementedException("Return here stream state, so: id, type and version.");

    public IEnumerable GetEvents(Guid streamId) =>
        throw new NotImplementedException("Return here stream events stored in database.");
    
    // 1. Create instance
    // 2. Get Stream Events
    // 3. For each event call apply method on aggregate and increment aggregate version
    // 4. Return Aggregate
    public T AggregateStream<T>(Guid streamId, long? atStreamVersion = null, DateTime? atTimestamp = null) where T: notnull
    {
        var aggregate = (T)Activator.CreateInstance(typeof(T), true)!;

        var events = GetEvents(streamId);
        var version = 0;

        foreach (var @event in events)
        {
            aggregate.InvokeIfExists(Apply, @event);
            aggregate.SetIfExists(Version, ++version);
        }

        return aggregate;
    }



    private void CreateEventsTable()
    {
        const string createEventsTableSql =

            @"CREATE TABLE IF NOT EXISTS events (
            id UUID NOT NULL PRIMARY KEY,
            data JSONB NOT NULL,
            stream_id UUID NOT NULL,
            type VARCHAR(50) NOT NULL,
            version BIGINT NOT NULL,
            created timestamp with time zone NOT NULL default (now()),
            FOREIGN KEY (stream_id) REFERENCES streams(id)
            CONSTRAINT events_stream_and_version UNIQUE (stream_id, version)
        )";

        _dbConnection.Execute(createEventsTableSql);

    }

    private void CreateStreamsTable()
    {
        const string createStreamsTableSql =
            @"CREATE TABLE IF NOT EXISTS streams (
            id UUID NOT NULL PRIMARY KEY,
            type TEXT NOT NULL,
            version BIGINT NOT NULL,
        )";
        _dbConnection.Execute(createStreamsTableSql);
    }
    
    public void Dispose() =>
        _dbConnection.Dispose();
}