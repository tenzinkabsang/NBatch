using NBatch.Core.Interfaces;

namespace NBatch.ConsoleApp;

/// <summary>
/// A sample processor that simply returns a new product with lowercase properties.
/// </summary>
public sealed class ProductLowercaseProcessor : IProcessor<Product, Product>
{
    public Task<Product> ProcessAsync(Product input)
    {
        return Task.FromResult(new Product(
                Id: input.Id,
                Name: input.Name.ToLower(),
                Description: input.Description.ToLower(),
                Price: input.Price)
            );
    }
}
