namespace NBatch.Readers.FileReader;

public interface IFieldSetMapper<T>
{
    T MapFieldSet(FieldSet fieldSet);
}
