namespace DaJet.Scripting
{
    public abstract class ProcessorBase
    {
        public abstract ExitCode Process();
        public abstract void Dispose();
    }
}