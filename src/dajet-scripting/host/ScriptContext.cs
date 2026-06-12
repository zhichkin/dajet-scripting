using DaJet.Data;
using DaJet.Scripting.Model;

namespace DaJet.Scripting.Host
{
    public abstract class ScriptContext
    {
        public abstract DataSourceScope GetDataSource();
        public abstract object GetValue(in string name);
        public abstract void SetValue(in string name, in object value);
        public abstract object Evaluate(in SyntaxNode expression);
    }
}