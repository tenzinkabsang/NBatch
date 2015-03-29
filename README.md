## NBatch
	

Batch Processing Framework For .NET

_Batch processing_ is one of the most common things that every company needs, either handling transactions from a Queue or running as a nightly scheduled task. In any case, these batch jobs are handling mission critical tasks and thus has to be robust, self-recoverable and handle errors gracefully without shutting down the entire system.  For an application to be able to handle all of that, takes a lot of low-level plumbing code, all of which are infrastructural level concerns that should be abstracted away from developers.

NBatch is built to allow developers to focus on what's really important by implementing only the business logic and simply plugging them into the framework, which handles the batch processing machinery, automatically giving them features to handle errors, self-recover, able to restart where it left off, etc,.

NBatch is inspired by Spring Batch and carries over some of the high level concepts, but the implemention is unique to `.NET` and not a port of spring batch.

## Documentation

For a Getting started guide, API docs, etc. see the [documentation page](/doc/gettingStarted/readme.md)!

## Sample
Parse items from a file, uppercase all values and save it to a database.

sample.csv
```
ProductId,	Name,				Description
1111,		C# For Dummies,		The book you should avoid
2222,		Design Patterns,	Just a template
3333,		Java 8 In Depth,	Finally Lambdas
```

```C#
// Define a reader
public static IReader<Product> FlatFileReader() 
{
   string sourceUrl = @"c:\sample.csv";
   return new FlatFileItemBuilder<Product>(sourceUrl, new ProductMapper())
	      .WithHeaders(new[] {"Name", "Description" })
	      .LinesToSkip(1)
	      .Build();
}
```
```C#
// Define a writer
public static IWriter<Product> SqlWriter()
{
    return new SqlDbItemWriter<Product>("myDB")
              .SetSql("INSERT INTO Product (Name, Description) VALUES (@Name, @Description)");
}
```

```C#
// Define an optional processor if you need to do any transformation
// before sending it to the writer.
public class ProductUppercaseProcessor : IProcessor<Product, Product>
{
    public Product Process(Product input)
    {
        return new Product
			        {
			            Name = input.Name.ToUpper(),
			            Description = input.Description.ToUpper()
			        };
    }
}
```

```C#
public static void Main(string[] args)
{
	// Create a Step containing the reader, processor and writer.
	IStep processFileStep = new Step<Product, Product>("step1")
	        .SetReader(FlatFileReader())
	        .SetProcessor(new ProductUppercaseProcessor())
	        .SetWriter(SqlWriter());
        
    // Create a Job with the step and run.
    new Job("myJob", "myDB")
        .AddStep(processFileStep)
        .Start();
}
```

## Want to contribute?


1. Fork it!
2. Create your feature branch: `git checkout -b my-new-feature`
3. Commit your changes: `git commit -am 'Add some feature'`
4. Push to the branch: `git push origin my-new-feature`
5. Submit a pull request :D
