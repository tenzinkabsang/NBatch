using NBatch.Readers.FileReader;

namespace NBatch.ConsoleApp;

public sealed class ProductMapper : IFieldSetMapper<Product>
{
    public Product MapFieldSet(FieldSet fieldSet) => 
        new Product(
                Sku: fieldSet.GetString("Sku"),
                Name: fieldSet.GetString("Name"),
                Description: fieldSet.GetString("Description"),
                Price: fieldSet.GetDecimal("Price")
            );
}
