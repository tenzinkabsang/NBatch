---
layout: default
title: Readers & Writers
nav_order: 3
---

# Readers & Writers

NBatch ships with built-in readers and writers for common data sources. You can also implement your own by using the `IReader<T>` and `IWriter<T>` interfaces.

---

## Built-in Components

| Component | Package | Direction | Description |
|-----------|---------|-----------|-------------|
| `CsvReader<T>` | `NBatch` | Read | Delimited text files (CSV, TSV, pipe) |
| `DbReader<T>` | `NBatch.EntityFrameworkCore` | Read | Any EF Core `DbContext` with pagination |
| `DbWriter<T>` | `NBatch.EntityFrameworkCore` | Write | Any EF Core `DbContext` |
| `FlatFileItemWriter<T>` | `NBatch` | Write | Serializes objects to delimited text |

---

## `CsvReader<T>`

Reads items from a delimited text file. Automatically parses headers from the first row.

Fields may be quoted (RFC 4180): a quoted field can contain the delimiter, and `""`
inside a quoted field is an escaped literal quote. Embedded newlines inside quoted
fields are **not** supported — the reader is line-based. Values are trimmed, and all
parsing uses the invariant culture.

```csharp
var reader = new CsvReader<Product>("products.csv", row => new Product
{
    Name  = row.GetString("Name"),
    Price = row.GetDecimal("Price")
});
```

### Automatic mapping

Skip the mapping function entirely and let headers bind to properties:

```csharp
var reader = new CsvReader<Product>("products.csv");
```

- Header names match **public settable properties** case-insensitively.
- Supported types: `string`, `int`, `long`, `decimal`, `double`, `float`, `bool`,
  `DateTime`, `DateTimeOffset`, `Guid`, enums (case-insensitive), and `Nullable<>`
  of each — an empty value becomes `null` for nullable properties.
- Unmatched headers are ignored; unmatched (or unsupported-type) properties keep
  their defaults.
- `T` needs a **public parameterless constructor** — positional records don't have
  one, so use the map-function overload for those (a clear error at construction
  points this out).
- Auto-mapping is reflection-based, so it is not trimming/AOT-safe.

### Options

**Custom delimiter** -- override the default comma:

```csharp
var reader = new CsvReader<Product>("data.tsv", mapFn)
    .WithDelimiter('\t');
```

**Explicit headers** -- provide column names instead of reading from the first row:

```csharp
var reader = new CsvReader<Product>("data.csv", mapFn)
    .WithHeaders("Name", "Description", "Price");
```

### `CsvRow` API

The mapping function receives a `CsvRow` with typed accessor methods. Each method is available with both **name** and **index** overloads:

| Method | Return Type | Example |
|--------|-------------|------|
| `GetString("column")` | `string` | `row.GetString("Name")` or `row.GetString(0)` |
| `GetInt("column")` | `int` | `row.GetInt("Quantity")` or `row.GetInt(1)` |
| `GetLong("column")` | `long` | `row.GetLong("Id")` or `row.GetLong(0)` |
| `GetDecimal("column")` | `decimal` | `row.GetDecimal("Price")` or `row.GetDecimal(2)` |
| `GetDouble("column")` | `double` | `row.GetDouble("Weight")` or `row.GetDouble(3)` |
| `GetBool("column")` | `bool` | `row.GetBool("Active")` or `row.GetBool(4)` |
| `GetDateTime("column")` | `DateTime` | `row.GetDateTime("CreatedOn")` or `row.GetDateTime(5)` |
| `GetGuid("column")` | `Guid` | `row.GetGuid("Id")` or `row.GetGuid(6)` |

Every accessor also has a nullable **`*OrNull`** variant (`GetIntOrNull`,
`GetStringOrNull`, `GetDateTimeOrNull`, …) that returns `null` when the column is
missing or the value is empty — a non-empty unparseable value still throws.

### Error behavior

- A line that fails to parse or map throws a `FlatFileParseException` whose **`LineNumber`**
  is the 1-based physical line in the file, with the original error as `InnerException`. Skip
  policies for either the wrapper or the inner type (e.g. `FormatException`) match it.
- **Blank (whitespace-only) lines are ignored** wherever they appear — including runs of
  blank lines longer than the chunk size — and never count toward chunk positions.
- Duplicate header names are rejected with a clear error.
- A **missing file** throws `FileNotFoundException` — file I/O errors are never wrapped
  as parse errors, so they are not accidentally skippable.

---

## `DbReader<T>`

Reads entities from any EF Core `DbContext` in paginated chunks. Provider-agnostic -- works with SQL Server, PostgreSQL, SQLite, etc.

> Lives in the **`NBatch.EntityFrameworkCore`** package (namespace `NBatch.Readers.DbReader`).

```csharp
var reader = new DbReader<Product>(dbContext, q => q.OrderBy(p => p.Id));
```

The `queryBuilder` parameter applies ordering (and optional filtering) to the queryable. **A deterministic `OrderBy` clause is required and enforced**: pagination — and the item-level re-reads performed when a chunk fails under a [skip policy](skip-policies) — rely on stable row positions. An unordered SQL query may return rows in any order, which would silently corrupt chunking and restarts, so the first read throws `InvalidOperationException` if no ordering was applied.

```csharp
// With filtering
var reader = new DbReader<Order>(dbContext, q => q
    .Where(o => o.Status == "pending")
    .OrderBy(o => o.CreatedAt));
```

---

## `DbWriter<T>`

Writes entities to any EF Core `DbContext`. Calls `AddRange` followed by `SaveChangesAsync`, then **detaches** the written entities so the change tracker stays flat on long-running jobs (entities you track separately on a shared context are untouched).

> Lives in the **`NBatch.EntityFrameworkCore`** package (namespace `NBatch.Writers.DbWriter`).

```csharp
var writer = new DbWriter<Product>(dbContext);
```

---

## `FlatFileItemWriter<T>`

Serializes objects to a delimited text file using reflection-based property serialization.

```csharp
var writer = new FlatFileItemWriter<Product>("output.csv");
```

### Custom separator

```csharp
var writer = new FlatFileItemWriter<Product>("output.tsv")
    .WithDelimiter('\t');
```

Default token: `,` (comma)

### Output format

- Values are formatted with the **invariant culture**, matching `CsvReader`'s parsing — the same file round-trips on any machine locale.
- Fields containing the delimiter, a quote, or a line break are **quoted RFC 4180-style** (embedded quotes doubled), so the output stays parseable.
- The writer **appends** to the destination file. That keeps restart-from-failure safe (previously written output survives a resume), but it also means re-running a completed job appends a second copy — use a per-run file name (e.g. timestamped) for jobs that run repeatedly.

---

## Custom Readers & Writers

### `IReader<T>`

```csharp
public interface IReader<TItem>
{
    Task<IEnumerable<TItem>> ReadAsync(
        long startIndex,
        int chunkSize,
        CancellationToken cancellationToken = default);
}
```

Implement this to read from any source -- REST APIs, message queues, cloud storage, etc.

> **Contract:** `ReadAsync` must honor `startIndex` (random access). When a chunk fails
> under a skip policy, NBatch re-reads the chunk range one item at a time
> (`ReadAsync(index, 1)`) to isolate the failing positions — a forward-only reader that
> ignores `startIndex` will misbehave there and on restarts.
>
> Two more rules follow from position-based tracking:
>
> - **Stable positions** — the same `startIndex` must map to the same item on every
>   call, including across process restarts. For databases this means a deterministic
>   `ORDER BY`; for growing sources it means restarts assume the already-processed
>   prefix hasn't shifted.
> - **Full chunks** — return exactly `chunkSize` items for every range before the end
>   of the data; a shorter result is only valid for the final chunk, and an empty
>   result means end of data. The engine advances by `chunkSize` positions per chunk
>   and fails the step if a reader produces more items after a partial chunk.

```csharp
public class ApiReader<T> : IReader<T>
{
    public async Task<IEnumerable<T>> ReadAsync(
        long startIndex, int chunkSize, CancellationToken ct)
    {
        // Fetch a page of items from your API
        return await httpClient.GetFromJsonAsync<List<T>>(
            $"/api/items?skip={startIndex}&take={chunkSize}", ct);
    }
}
```

### `IWriter<T>`

```csharp
public interface IWriter<TItem>
{
    Task WriteAsync(
        IEnumerable<TItem> items,
        CancellationToken cancellationToken = default);
}
```

### Lambda Writers

You can skip implementing `IWriter<T>` and use a lambda directly:

```csharp
// Simple async lambda
.WriteTo(async items =>
{
    foreach (var item in items)
        Console.WriteLine(item);
})

// With CancellationToken
.WriteTo(async (items, ct) =>
{
    await httpClient.PostAsJsonAsync("/api/products", items, ct);
})
```

---

## Custom Processors

### `IProcessor<TInput, TOutput>`

```csharp
public interface IProcessor<TInput, TOutput>
{
    Task<TOutput> ProcessAsync(
        TInput input,
        CancellationToken cancellationToken = default);
}
```

Or use a lambda &mdash; synchronous or async:

```csharp
// Synchronous lambda
.ProcessWith(p => new ProductDto { Name = p.Name.ToUpper(), Price = p.Price })

// Async lambda with CancellationToken
.ProcessWith(async (p, ct) =>
{
    var rate = await exchangeService.GetRateAsync(ct);
    return new ProductDto { Name = p.Name, Price = p.Price * rate };
})
```

---

**Next:** [Skip Policies &rarr;](skip-policies)
