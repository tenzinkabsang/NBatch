using NBatch.Main.Readers.FileReader;

namespace NBatch.ConsoleDemo
{
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