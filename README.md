## NBatch [![NuGet Version](http://img.shields.io/nuget/v/NBatch.svg?style=flat)](https://www.nuget.org/packages/NBatch/) [![NuGet Downloads](http://img.shields.io/nuget/dt/NBatch.svg?style=flat)](https://www.nuget.org/packages/NBatch/)

	

Batch Processing Framework For .NET

__NBatch__ simplifies batch processing by providing a framework that supports common features needed for all mission critical tasks.  

Should your application stop if it encounters a badly formatted line, or should it skip that line and continue on? What about when you restart a failed batch job? NBatch handles
all of these low-level infrastructural plumbing code and exposes configurable components that the user can set-up to cater for there particular app.
NBatch internally tracks everything that happens within the framework and provides features to handle errors, self-recover, able to restart where it left off, etc,.

Developers can implement only the business logic and simply plug them into the framework, which handles the batch processing machinery, thus giving developers more time to focus on what's really important - their business logic.


## Documentation

For a Getting started guide, API docs, etc. see the [documentation page](/doc/gettingStarted/readme.md)!

## Sample
Parse items from a CSV file, uppercase all values and save to a database.

sample.csv
```
ProductId,Name,Description,Price
1111,C# For Dummies,The book you should avoid,800.00
2222,Design Patterns,The worlds authority on software designs,299.99
3333,Java 8 In Depth,Finally Lambdas,399.99
```

```C#
public static async Task Main(string[] args)
{
    // Use .UseJobStore() to enable SQL-based job tracking with restart support.
    // Omit it to use lightweight in-memory tracking (great for one-off ETL scripts).
    var job = Job.CreateBuilder(jobName: "JOB-1")
        .UseJobStore(jobDbConnString)
        .AddStep("Import from file and save to database", step => step
            .ReadFrom(new CsvReader<Product>(filePath, row => new Product
            {
                Name = row.GetString("Name"),
                Description = row.GetString("Description"),
                Price = row.GetDecimal("Price")
            }))
            .ProcessWith(p => new Product
            {
                Name = p.Name.ToUpper(),
                Description = p.Description.ToUpper(),
                Price = p.Price
            })
            .WriteTo(new DbWriter<Product>(destinationDb))
            .WithSkipPolicy(SkipPolicy.For<FlatFileParseException>(maxSkips: 3))
            .WithChunkSize(10))
        .Build();

    var result = await job.RunAsync();

    // result.Success — overall job success
    // result.Steps   — per-step details (ItemsRead, ItemsProcessed, ErrorsSkipped)
}
```

## Want to contribute?

1. Fork it!
2. Create your feature branch: `git checkout -b my-new-feature`
3. Commit your changes: `git commit -am 'Add some feature'`
4. Push to the branch: `git push origin my-new-feature`
5. Submit a pull request :D
