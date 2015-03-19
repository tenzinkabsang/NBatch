## NBatch
	

Batch Processing Framework For .NET

_Batch processing_ is one of the most common things that every application needs, either handling transactions from a Queue or running as a nightly scheduled task, processing _mission critical_ things.  However, there are no framework support for batch processing, it is left to each individual to come up with their own solution and essentially reinvent the wheel every single time. This results in a lot of wasted time dealing with the plumbing of batch processing (exception cases, handle restart scenarios, etc.,) instead of focusing on the business needs.  
NBatch is inspired by Spring Batch and carries over some of the high level concepts, but the implemention is unique to `.NET` and not a port of spring batch.

## Documentation

For a Getting started guide, API docs, etc. see the [documentation page](/docs/README.md)!

## Sample
Read items from a file, and print it to the console.

```C#
static void Main(string[] args)
        {
            string sourceUrl = @"c:\sample.txt";
            
            IStep processFileStep = new Step<Order, Order>("processFileStep")
						                .UseFlatFileItemReader(
						                    resourceUrl: sourceUrl,
						                    fieldMapper: new ProductMapper(),
						                    linesToSkip: 1,
						                    headers: new[] { "ProductId", "Name", "Description", "Price" })
						                .SetWriter(new ConsoleWriter<Product>());

            new Job()
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

