namespace DaJet.Compiler
{
    public abstract class ScriptProcessor
    {
        public virtual void Execute()
        {
            SelectIntoArrayProcessor select = new(this);
        }
    }
}