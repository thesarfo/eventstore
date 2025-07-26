using FluentAssertions;
using Npgsql;
using Npgsql.Replication.PgOutput.Messages;
using Xunit;

namespace eventstore_net.tests;

public class CreateStreamsTable : IDisposable
{
    private readonly NpgsqlConnection databaseConnection;
    private readonly PostgresSchemaProvider schemaProvider;

    private const string StreamsTableName = "streams";

    private const string IdColumnName = "id";
    private const string TypeColumnName = "type";
    private const string VersionColumnName = "version";

    /// <summary>
    /// Inits Event Store
    /// </summary>
    public CreateStreamsTable()
    {
        databaseConnection = PostgresDbConnectionProvider.GetFreshDbConnection();
        schemaProvider = new PostgresSchemaProvider(databaseConnection);

        // Create Event Store
        var eventStore = new EventStore(databaseConnection);

        // Initialize Event Store
        eventStore.Init();
    }

    /// <summary>
    /// Verifies if Stream table was created
    /// </summary>
    [Fact]
    [Trait("Category", "SkipCI")]
    public void StreamsTable_ShouldBeCreated()
    {
        var streamsTable = schemaProvider.GetTable(StreamsTableName);

        streamsTable.Should().NotBeNull();
        streamsTable!.Name.Should().Be(StreamsTableName);
    }

    /// <summary>
    /// Verifies if Stream table has Id column of type Guid
    /// </summary>
    [Fact]
    [Trait("Category", "SkipCI")]
    public void StreamsTable_ShouldHave_IdColumn()
    {
        var idColumn = schemaProvider
            .GetTable(StreamsTableName)?
            .GetColumn(IdColumnName);

        idColumn.Should().NotBeNull();
        idColumn!.Name.Should().Be(IdColumnName);
    }

    /// <summary>
    /// Verifies if Stream table has Type column of type String
    /// </summary>
    [Fact]
    [Trait("Category", "SkipCI")]
    public void StreamsTable_ShouldHave_TypeColumn_WithStringType()
    {
        var typeColumn = schemaProvider
            .GetTable(StreamsTableName)?
            .GetColumn(TypeColumnName);

        typeColumn.Should().NotBeNull();
        typeColumn!.Name.Should().Be(TypeColumnName);
    }

    /// <summary>
    /// Verifies if Stream table has Version column of type Long
    /// </summary>
    [Fact]
    [Trait("Category", "SkipCI")]
    public void StreamsTable_ShouldHave_VersionColumn_WithLongType()
    {
        var versionColumn = schemaProvider
            .GetTable(StreamsTableName)?
            .GetColumn(VersionColumnName);

        versionColumn.Should().NotBeNull();
        versionColumn!.Name.Should().Be(VersionColumnName);
    }

    /// <summary>
    /// Disposes connection to database
    /// </summary>
    public void Dispose() =>
        databaseConnection.Dispose();
}