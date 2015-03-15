namespace NBatch.Core.Reader.FileReader
{
    public interface IFieldSetMapper<out T>
    {
        T MapFieldSet(FieldSet fieldSet);
    }
}