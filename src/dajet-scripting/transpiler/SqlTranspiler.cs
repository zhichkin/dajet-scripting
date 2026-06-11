using DaJet.Metadata;
using DaJet.Scripting.Model;
using System.Text;

namespace DaJet.Scripting
{
    public abstract class SqlTranspiler
    {
        public virtual int YearOffset { get; protected set; }
        public abstract void Visit(in SyntaxNode expression, in StringBuilder script);
        public abstract bool TryTranspile(in SyntaxNode node, in MetadataProvider provider, out string error);
    }
}