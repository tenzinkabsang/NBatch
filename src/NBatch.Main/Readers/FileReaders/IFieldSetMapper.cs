namespace NBatch.Main.Readers.FileReaders
{
    public interface IFieldSetMapper<out T>
    {
        T MapFieldSet(FieldSet fieldSet);
    }
}