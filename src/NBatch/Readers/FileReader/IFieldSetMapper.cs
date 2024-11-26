namespace NBatch.Readers.FileReader;

internal interface IFieldSetMapper<T>
{
    T MapFieldSet(FieldSet fieldSet);
}
