using Moq;
using NBatch.Main.Core;

namespace NBatch.Main.UnitTests.Core
{
    public class FakeStep<T, U> : Step<T, U>
    {
        public Mock<IReader<T>> MockReader;
        public Mock<IProcessor<T, U>> MockProcessor;
        public Mock<IWriter<U>> MockWriter;

        FakeStep(string name, Mock<IReader<T>> reader, Mock<IProcessor<T, U>> processor, Mock<IWriter<U>> writer)
            : base(name)
        {
            MockReader = reader;
            MockProcessor = processor;
            MockWriter = writer;
        }

        public static FakeStep<T, U> Create(string name)
        {
            var reader = new Mock<IReader<T>>();
            var writer = new Mock<IWriter<U>>();
            var processor = new Mock<IProcessor<T, U>>();

            var fakeStep = new FakeStep<T, U>(name, reader, processor, writer);
            fakeStep.SetReader(reader.Object);
            fakeStep.SetProcessor(processor.Object);
            fakeStep.SetWriter(writer.Object);

            // Set chunk size to one for tests
            fakeStep.WithChunkSize(1);

            return fakeStep;
        }
    }
}
