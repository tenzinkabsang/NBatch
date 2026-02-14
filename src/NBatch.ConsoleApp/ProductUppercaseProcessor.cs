using NBatch.Core.Interfaces;

namespace NBatch.ConsoleApp;

/// <summary>
/// A sample processor that simply returns a new product with uppercase properties.
/// </summary>
public sealed class ProductUppercaseProcessor : IProcessor<Product, Product>
{
    public Task<Product> ProcessAsync(Product input, CancellationToken cancellationToken = default)
    {
        Product product = new(
                        Sku: input.Sku.ToUpper(),
                        Name: input.Name.ToUpper(),
                        Description: input.Description.ToUpper(),
                        Price: input.Price);

        return Task.FromResult(product);
    }
}
