using NBatch.Core.Interfaces;

namespace NBatch.ConsoleApp;

/// <summary>
/// A sample processor that simply returns a new product with lowercase properties.
/// </summary>
public sealed class ProductLowercaseProcessor : IProcessor<Product, ProductLowercase>
{
    public Task<ProductLowercase> ProcessAsync(Product input, CancellationToken cancellationToken = default)
    {
        ProductLowercase product = new()
        {
            Sku = input.Sku.ToLower(),
            Name = input.Name.ToLower(),
            Description = input.Description.ToLower(),
            Price = input.Price
        };
        
        return Task.FromResult(product);
    }
}
