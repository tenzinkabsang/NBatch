namespace NBatch.Main.Readers.FileReader
{
    public interface IFieldSetMapper<out T>
    {
        T MapFieldSet(FieldSet fieldSet);
    }
}