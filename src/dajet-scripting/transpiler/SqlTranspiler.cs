using DaJet.Metadata;
using DaJet.Scripting.Model;
using System.Text;

namespace DaJet.Scripting
{
    public abstract class SqlTranspiler
    {
        public abstract void Visit(in SyntaxNode expression, in StringBuilder script);
        public abstract bool TryTranspile(in MetadataProvider provider, in SyntaxNode node, out SqlStatement statement, out string error);
    }
}