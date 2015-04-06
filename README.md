## NBatch [![NuGet Version](http://img.shields.io/nuget/v/NBatch.svg?style=flat)](https://www.nuget.org/packages/NBatch/) [![NuGet Downloads](http://img.shields.io/nuget/dt/NBatch.svg?style=flat)](https://www.nuget.org/packages/NBatch/)

	

Batch Processing Framework For .NET

__NBatch__ simplifies your batch processing needs by providing a platform that supports common features needed for all mission critical tasks.  

Should your application stop if it encounters a badly formatted line, or should it skip that line and continue on? What about when you restart a failed batch job? NBatch handles
all of these low-level infrastructural plumbing code and exposes configurable components that the user can set-up to cater for there particular app.
NBatch internally tracks everything that happens within the framework and provides features to handle errors, self-recover, able to restart where it left off, etc,.

NBatch is built to allow developers to focus on what's really important by implementing only the business logic and simply plugging them into the framework, which handles the batch processing machinery.


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
// FieldSet contains the headers you define below in FlatFileReader.
public class ProductMapper : IFieldSetMapper<Product>
{
    public Product MapFieldSet(FieldSet fieldSet)
    {
        return new Product
        {
            Name = fieldSet.GetString("Name"),
            Description = fieldSet.GetString("Description")
        };
    }
}
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

## Donate
- Just your Luv! :smile: :v:

