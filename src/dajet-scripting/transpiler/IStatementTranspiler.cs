using DaJet.Scripting.Model;
using System.Text;

namespace DaJet.Scripting
{
    public interface IStatementTranspiler
    {
        void Visit(in SyntaxNode expression, in StringBuilder script);
        bool TryTranspile(in SyntaxNode node, out SqlStatement statement, out string error);
    }
}