namespace DaJet.Scripting
{
    public abstract class ProcessorBase
    {
        protected virtual void Initialize() { /* Called from constructor */ }
        public abstract void Execute();
        public abstract void Dispose();
    }
}