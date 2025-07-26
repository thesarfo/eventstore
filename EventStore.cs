using Dapper;
using Npgsql;

public class EventStore
{
    private readonly NpgsqlConnection dbConnection;

    public EventStore(NpgsqlConnection dbConnection)
    {
        this.dbConnection = dbConnection;
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

        dbConnection.Execute(appendEventFunctionSql);
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

        dbConnection.Execute(createEventsTableSql);

    }

    private void CreateStreamsTable()
    {
        const string createStreamsTableSql =
        @"CREATE TABLE IF NOT EXISTS streams (
            id UUID NOT NULL PRIMARY KEY,
            type TEXT NOT NULL,
            version BIGINT NOT NULL,
        )";
        dbConnection.Execute(createStreamsTableSql);
    }
}