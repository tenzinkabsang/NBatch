## NBatch
	

Batch Processing Framework For .NET

_Batch processing_ is one of the most common things that every company needs, either handling transactions from a Queue or running as a nightly scheduled task. In any case, these batch jobs are handling mission critical tasks and thus has to be robust, self-recoverable and handle errors gracefully without shutting down the entire system.  For an application to be able to handle all of that, takes a lot of low-level plumbing code, all of which are infrastructural level concerns that should be abstracted away from developers.

NBatch is built to allow developers to focus on what's really important by implementing only the business logic and simply plugging them into the framework, which handles the batch processing machinery, automatically giving them features to handle errors, self-recover, able to restart where it left off, etc,.

NBatch is inspired by Spring Batch and carries over some of the high level concepts, but the implemention is unique to `.NET` and not a port of spring batch.

## Documentation

For a Getting started guide, API docs, etc. see the [documentation page](/docs/gettingStarted/readme.md)!

## Sample
Read items from a file, and print it to the console.

```C#
static void Main(string[] args)
{
    string sourceUrl = @"c:\sample.txt";
    
    IStep processFileStep = new Step<Product, Product>("processFileStep")
        .UseFlatFileItemReader(url: sourceUrl, fieldMapper: new ProductMapper())
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

