using NBatch.Main.Core;

namespace NBatch.ConsoleDemo
{
    public class ProductLowercaseProcessor : IProcessor<Product, Product>
    {
        public Product Process(Product input)
        {
            return new Product
                   {
                       ProductId = input.ProductId,
                       Name = input.Name.ToLower(),
                       Description = input.Description.ToLower(),
                       Price = input.Price
                   };
        }
    }
}