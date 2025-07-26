using System.Data;
using System.Text.Json.Serialization;
using Dapper;
using Newtonsoft.Json;
using Npgsql;

public class EventStore
{
    private readonly NpgsqlConnection _dbConnection;

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
}