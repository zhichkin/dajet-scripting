using DaJet.Data;
using DaJet.Scripting.Model;

namespace DaJet.Scripting
{
    public abstract class ScriptContext
    {
        public CancellationToken Cancellation { get; set; }
        public abstract DataSourceScope GetDataSource();
        public abstract object GetValue(in string name);
        public abstract void SetValue(in string name, in object value);
        public abstract object Evaluate(in SyntaxNode expression);
    }
}