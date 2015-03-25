using NBatch.Core;
using NBatch.Core.ItemProcessor;
using NBatch.Core.ItemReader;
using NBatch.Core.Reader.FileReader;

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

    public class ProductMapper : IFieldSetMapper<Product>
    {
        public Product MapFieldSet(FieldSet fieldSet)
        {
            return new Product
            {
                ProductId = fieldSet.GetInt("ProductId"),
                Name = fieldSet.GetString("Name"),
                Description = fieldSet.GetString("Description"),
                Price = fieldSet.GetDecimal("Price")
            };
        }
    }
}