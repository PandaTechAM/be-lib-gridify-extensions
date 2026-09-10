using GridifyExtensions.Extensions;
using GridifyExtensions.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GridifyExtensions.Tests;

public sealed class DistinctValuesSearchRankTests : IDisposable
{
    // Term "ara": exact, then prefix, then a word starting with it, then plain contains. Ids are deliberately
    // out of that order so the keyed path proves it orders by the key inside a rank, not by length.
    private static readonly Person[] People =
    [
        new() { Id = 1, Name = "Karapet Sargsyan" },
        new() { Id = 2, Name = "Ara Petrosyan" },
        new() { Id = 3, Name = "ara sargsyan" },
        new() { Id = 4, Name = "Sargis Karapetyan" },
        new() { Id = 5, Name = "Ara" },
        new() { Id = 6, Name = "Hovhannes Ara-Petrosyan" },
        new() { Id = 7, Name = "Petros Aramyan" },
        new() { Id = 8, Name = null }
    ];

    private static readonly string[] ExpectedByLength =
    [
        "Ara",
        "ara sargsyan",
        "Ara Petrosyan",
        "Petros Aramyan",
        "Hovhannes Ara-Petrosyan",
        "Karapet Sargsyan",
        "Sargis Karapetyan"
    ];

    private static readonly string[] ExpectedByKey =
    [
        "Ara",
        "Ara Petrosyan",
        "ara sargsyan",
        "Hovhannes Ara-Petrosyan",
        "Petros Aramyan",
        "Karapet Sargsyan",
        "Sargis Karapetyan"
    ];

    private readonly SqliteConnection _connection;
    private readonly PeopleContext _db;

    static DistinctValuesSearchRankTests()
    {
        WebApplication.CreateBuilder()
            .AddGridify(typeof(DistinctValuesSearchRankTests).Assembly);
    }

    public DistinctValuesSearchRankTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<PeopleContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new PeopleContext(options);
        _db.Database.EnsureCreated();
        _db.People.AddRange(People);
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Paged_string_column_ranks_exact_prefix_word_then_rest()
    {
        var result = await _db.People.ColumnDistinctValuesAsync(new ColumnDistinctValueQueryModel
        {
            PropertyName = "Name",
            Filter = "Name=*ara",
            Page = 1,
            PageSize = 50
        }, ct: TestContext.Current.CancellationToken);

        Assert.Equal(ExpectedByLength, result.Data.Cast<string>());
        Assert.Equal(ExpectedByLength.Length, result.TotalCount);
    }

    [Fact]
    public async Task Cursored_string_column_ranks_exact_prefix_word_then_rest()
    {
        var result = await _db.People.ColumnDistinctValuesAsync(new ColumnDistinctValueCursoredQueryModel
        {
            PropertyName = "Name",
            Filter = "Name=*ara",
            PageSize = 50
        }, ct: TestContext.Current.CancellationToken);

        Assert.Equal(ExpectedByLength, result.Data.Cast<string>());
    }

    [Fact]
    public async Task Keyed_column_ranks_the_same_then_orders_by_key()
    {
        var result = await _db.People.ColumnDistinctValuesAsync(new ColumnDistinctValueQueryModel
        {
            PropertyName = "Alias",
            Filter = "Alias=*ara",
            Page = 1,
            PageSize = 50
        }, ct: TestContext.Current.CancellationToken);

        Assert.Equal(ExpectedByKey, result.Data.Cast<string>());
        Assert.Equal(ExpectedByKey.Length, result.TotalCount);
    }
}

public sealed class Person
{
    public int Id { get; set; }
    public string? Name { get; set; }
}

public sealed class PeopleContext(DbContextOptions<PeopleContext> options) : DbContext(options)
{
    public DbSet<Person> People => Set<Person>();
}

public sealed class PersonMapper : FilterMapper<Person>
{
    public PersonMapper()
    {
        AddMap("Name", x => x.Name);
        AddMap("Alias", x => x.Name);
        AddMap("AliasSortKey", x => x.Id);
        AddMapForNaturalSortKey("Alias", "AliasSortKey");
    }
}
