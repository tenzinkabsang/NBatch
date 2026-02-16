---
layout: default
title: Readers & Writers
nav_order: 3
---

# Readers & Writers

NBatch ships with built-in readers and writers for common data sources. You can also implement your own by using the `IReader<T>` and `IWriter<T>` interfaces.

---

## Built-in Components

| Component | Direction | Description |
|-----------|-----------|-------------|
| `CsvReader<T>` | Read | Delimited text files (CSV, TSV, pipe) |
| `DbReader<T>` | Read | Any EF Core `DbContext` with pagination |
| `DbWriter<T>` | Write | Any EF Core `DbContext` |
| `FlatFileItemWriter<T>` | Write | Serializes objects to delimited text |

---

## `CsvReader<T>`

Reads items from a delimited text file. Automatically parses headers from the first row.

```csharp
var reader = new CsvReader<Product>("products.csv", row => new Product
{
    Name  = row.GetString("Name"),
    Price = row.GetDecimal("Price")
});
```

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

---

## `DbReader<T>`

Reads entities from any EF Core `DbContext` in paginated chunks. Provider-agnostic -- works with SQL Server, PostgreSQL, SQLite, etc.

```csharp
var reader = new DbReader<Product>(dbContext, q => q.OrderBy(p => p.Id));
```

The `queryBuilder` parameter applies ordering (and optional filtering) to the queryable. **An `OrderBy` clause is required** for deterministic pagination.

```csharp
// With filtering
var reader = new DbReader<Order>(dbContext, q => q
    .Where(o => o.Status == "pending")
    .OrderBy(o => o.CreatedAt));
```

---

## `DbWriter<T>`

Writes entities to any EF Core `DbContext`. Calls `AddRange` followed by `SaveChangesAsync`.

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
    .WithToken('\t');
```

Default token: `,` (comma)

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
