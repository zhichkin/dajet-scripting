namespace DaJet.Compiler
{
    public abstract class ScriptProcessor
    {
        private readonly List<ProcessorBase> __Processors = new();
        private readonly Stack<MsDataSource> __DataSources = new();
        protected ScriptProcessor()
        {
            // reserved for the future
        }
        public void Execute()
        {
            try
            {
                Process();
            }
            catch // ?
            {
                throw; //TODO: CANCEL script command
            }
            finally
            {
                Cancel();
            }
        }
        protected abstract void Process();
        public void Cancel()
        {
            foreach (ProcessorBase processor in __Processors)
            {
                processor.Cancel();
            }
        }
        protected void UseDataSource(string connectionString)
        {
            // Read Committed
            // Repeatable Read
            // Serializable

            MsDataSource source = new(connectionString, "READCOMMITTED");

            __DataSources.Push(source);
        }
        protected void DisposeDataSource()
        {
            if (__DataSources.TryPop(out MsDataSource source))
            {
                source.Dispose();
            }
        }
        public MsDataSource GetMsDataSource() // make internal ?
        {
            return __DataSources.Peek() as MsDataSource;
        }
        protected void Synchronize()
        {
            // reserved for the future
        }
    }
}