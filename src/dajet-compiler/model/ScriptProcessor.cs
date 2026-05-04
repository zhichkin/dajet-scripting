namespace DaJet.Compiler
{
    public abstract class ScriptProcessor : IDisposable
    {
        private readonly List<ProcessorBase> Processors = new();
        private readonly Stack<MsDataSource> DataSources = new();
        protected ScriptProcessor()
        {
            // reserved for the future
        }
        public virtual void Execute()
        {
            Processors.Add(new TestProcessor(this));
        }
        public void UseDataSource(string connectionString)
        {
            MsDataSource source = new(connectionString, "READCOMMITTED");

            DataSources.Push(source);
        }
        public MsDataSource GetDataSource()
        {
            return DataSources.Peek();
        }
        public void DisposeDataSource()
        {
            if (DataSources.TryPop(out MsDataSource source))
            {
                source.Dispose();
            }
        }
        public void Synchronize()
        {
            // reserved for the future
        }
        public void Cancel()
        {
            // reserved for the future
        }
        public void Dispose()
        {
            foreach (ProcessorBase processor in Processors)
            {
                processor.Dispose();
            }
        }
    }
}