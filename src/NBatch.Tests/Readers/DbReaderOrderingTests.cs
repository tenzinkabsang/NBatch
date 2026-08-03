using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NBatch.Readers.DbReader;
using NUnit.Framework;

namespace NBatch.Tests.Readers;

/// <summary>
/// DbReader must refuse unordered queries: Skip/Take over an unordered SQL query
/// returns rows in arbitrary order, which silently corrupts both normal chunking
/// and restart-from-failure positions.
/// </summary>
[TestFixture]
internal sealed class DbReaderOrderingTests
{
    private sealed class Item
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private sealed class ItemContext(DbContextOptions<ItemContext> options) : DbContext(options)
    {
        public DbSet<Item> Items => Set<Item>();
    }

    private SqliteConnection _connection = null!;
    private ItemContext _ctx = null!;

    [SetUp]
    public async Task BeforeEach()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ItemContext>().UseSqlite(_connection).Options;
        _ctx = new ItemContext(options);
        await _ctx.Database.EnsureCreatedAsync();

        _ctx.Items.AddRange(Enumerable.Range(1, 5).Select(i => new Item { Id = i, Name = $"item{i}" }));
        await _ctx.SaveChangesAsync();
        _ctx.ChangeTracker.Clear();
    }

    [TearDown]
    public async Task AfterEach()
    {
        await _ctx.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Test]
    public void Unordered_query_throws_on_first_read()
    {
        var reader = new DbReader<Item>(_ctx, q => q);

        var ex = Assert.ThrowsAsync<InvalidOperationException>(() => reader.ReadAsync(0, 2));

        Assert.That(ex!.Message, Does.Contain("OrderBy"));
    }

    [Test]
    public void Unordered_filtered_query_throws_on_first_read()
    {
        var reader = new DbReader<Item>(_ctx, q => q.Where(i => i.Id > 1));

        Assert.ThrowsAsync<InvalidOperationException>(() => reader.ReadAsync(0, 2));
    }

    [Test]
    public async Task Ordered_query_pages_deterministically()
    {
        var reader = new DbReader<Item>(_ctx, q => q.OrderBy(i => i.Id));

        var chunk0 = (await reader.ReadAsync(0, 2)).Select(i => i.Id).ToList();
        var chunk1 = (await reader.ReadAsync(2, 2)).Select(i => i.Id).ToList();
        var chunk2 = (await reader.ReadAsync(4, 2)).Select(i => i.Id).ToList();
        var chunk3 = (await reader.ReadAsync(6, 2)).Select(i => i.Id).ToList();

        Assert.That(chunk0, Is.EqualTo(new[] { 1, 2 }));
        Assert.That(chunk1, Is.EqualTo(new[] { 3, 4 }));
        Assert.That(chunk2, Is.EqualTo(new[] { 5 }));
        Assert.That(chunk3, Is.Empty);
    }

    [Test]
    public async Task Where_with_OrderByDescending_is_accepted()
    {
        var reader = new DbReader<Item>(_ctx, q => q.Where(i => i.Id > 1).OrderByDescending(i => i.Id));

        var chunk = (await reader.ReadAsync(0, 10)).Select(i => i.Id).ToList();

        Assert.That(chunk, Is.EqualTo(new[] { 5, 4, 3, 2 }));
    }
}
