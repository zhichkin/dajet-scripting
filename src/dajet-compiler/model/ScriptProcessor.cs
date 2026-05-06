namespace DaJet.Compiler
{
    public abstract class ScriptProcessor
    {
        protected readonly Stack<MsDataSource> _sources = new();
        protected readonly List<ProcessorBase> _processors = new();
        protected ScriptProcessor()
        {
            // reserved for the future
        }
        protected abstract void Process();
        public void Execute()
        {
            try
            {
                Process();
            }
            catch (OperationCanceledException)
            {
                throw; //TODO: CANCEL script command - canceled by code
            }
            catch
            {
                throw; // unexpected exception
            }
            finally
            {
                Cancel(); // completed successfully
            }
        }
        public void Cancel() // canceled by host
        {
            foreach (ProcessorBase processor in _processors)
            {
                processor.Dispose();
            }
        }
        protected void UseDataSource(string connectionString)
        {
            // Read Committed
            // Repeatable Read
            // Serializable

            // The constructor must guarantee that it will return an object or throw an exception.
            MsDataSource source = new(connectionString, "READCOMMITTED");

            _sources.Push(source);
        }
        protected void DisposeDataSource()
        {
            if (_sources.TryPop(out MsDataSource source))
            {
                source.Dispose();
            }
        }
        internal MsDataSource GetMsDataSource()
        {
            return _sources.Peek() as MsDataSource;
        }
        protected void Synchronize()
        {
            // reserved for the future
        }
    }
}