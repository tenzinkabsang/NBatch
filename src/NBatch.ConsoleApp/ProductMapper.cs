using NBatch.Readers.FileReader;

namespace NBatch.ConsoleApp;

public sealed class ProductMapper : IFieldSetMapper<Product>
{
    public Product MapFieldSet(FieldSet fieldSet) => 
        new Product(
                Id: fieldSet.GetInt("Id"),
                Name: fieldSet.GetString("Name"),
                Description: fieldSet.GetString("Description"),
                Price: fieldSet.GetDecimal("Price")
            );
}
