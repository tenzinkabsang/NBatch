using NBatch.Main.Core;

namespace NBatch.ConsoleDemo
{
    public class ProductUppercaseProcessor : IProcessor<Product, Product>
    {
        public Product Process(Product input)
        {
            return new Product
            {
                ProductId = input.ProductId,
                Name = input.Name.ToUpper(),
                Description = input.Description.ToUpper(),
                Price = input.Price
            };
        }
    }
}