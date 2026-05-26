using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using System.Text;

namespace DaJet.Scripting
{
    internal sealed class DATETIME : Function
    {
        public override DataType GetReturnType(in FunctionExpression node)
        {
            return DataType.DateTime;
        }
        public override void Transpile(in SqlTranspiler statement, in FunctionExpression node, in StringBuilder script)
        {
            int offset = statement.YearOffset;

            if (node.Token == Token.NOW)
            {
                if (offset == 0)
                {
                    script.Append("GETDATE()");
                }
                else
                {
                    script.Append(string.Format("DATEADD(year, {0}, GETDATE())", offset));
                }
            }
            else if (node.Token == Token.UTC)
            {
                if (offset == 0)
                {
                    script.Append("GETUTCDATE()");
                }
                else
                {
                    script.Append(string.Format("DATEADD(year, {0}, GETUTCDATE())", offset));
                }
            }
        }
    }
}