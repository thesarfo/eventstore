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