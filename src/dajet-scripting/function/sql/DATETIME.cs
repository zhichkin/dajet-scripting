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
            else if (node.Token == Token.DATESTART)
            {
                DATESTART(in statement, in node, in script);
            }
            else if (node.Token == Token.DATEEND)
            {
                DATEEND(in statement, in node, in script);
            }
            else if (node.Token == Token.DATEADD)
            {
                DATEADD(in statement, in node, in script);
            }
        }
        private static void DATESTART(in SqlTranspiler statement, in FunctionExpression node, in StringBuilder script)
        {
            SyntaxNode expression = node.Parameters[0];

            if (expression is not ScalarExpression scalar)
            {
                throw new InvalidOperationException($"[DATESTART] the first parameter must be string");
            }

            string datepart = scalar.Literal;

            expression = node.Parameters[1];

            if (datepart == "YEAR")
            {
                script.Append("DATETIME2FROMPARTS(YEAR(");
                statement.Visit(in expression, in script);
                script.Append("), 1, 1, 0, 0, 0, 0, 0)");
            }
            else if (datepart == "QUARTER")
            {
                script.Append("DATETIME2FROMPARTS(YEAR(");
                statement.Visit(in expression, in script);
                script.Append("), CASE WHEN MONTH(");
                statement.Visit(in expression, in script);
                script.Append(") <= 3 THEN 1 WHEN MONTH(");
                statement.Visit(in expression, in script);
                script.Append(") <= 6 THEN 4 WHEN MONTH(");
                statement.Visit(in expression, in script);
                script.Append(") <= 9 THEN 7 ELSE 10 END, 1, 0, 0, 0, 0, 0)");
            }
            else if (datepart == "MONTH")
            {
                script.Append("DATETIME2FROMPARTS(YEAR(");
                statement.Visit(in expression, in script);
                script.Append("), MONTH(");
                statement.Visit(in expression, in script);
                script.Append("), 1, 0, 0, 0, 0, 0)");
            }
            else if (datepart == "DAY")
            {
                script.Append("DATETIME2FROMPARTS(YEAR(");
                statement.Visit(in expression, in script);
                script.Append("), MONTH(");
                statement.Visit(in expression, in script);
                script.Append("), DAY(");
                statement.Visit(in expression, in script);
                script.Append("), 0, 0, 0, 0, 0)");
            }
            else if (datepart == "HOUR")
            {
                script.Append("DATETIME2FROMPARTS(YEAR(");
                statement.Visit(in expression, in script);
                script.Append("), MONTH(");
                statement.Visit(in expression, in script);
                script.Append("), DAY(");
                statement.Visit(in expression, in script);
                script.Append("), DATEPART(HOUR, ");
                statement.Visit(in expression, in script);
                script.Append("), 0, 0, 0, 0)");
            }
            else if (datepart == "MINUTE")
            {
                script.Append("DATETIME2FROMPARTS(YEAR(");
                statement.Visit(in expression, in script);
                script.Append("), MONTH(");
                statement.Visit(in expression, in script);
                script.Append("), DAY(");
                statement.Visit(in expression, in script);
                script.Append("), DATEPART(HOUR, ");
                statement.Visit(in expression, in script);
                script.Append("), DATEPART(MINUTE, ");
                statement.Visit(in expression, in script);
                script.Append("), 0, 0, 0)");
            }
            else if (datepart == "SECOND")
            {
                script.Append("DATETIME2FROMPARTS(YEAR(");
                statement.Visit(in expression, in script);
                script.Append("), MONTH(");
                statement.Visit(in expression, in script);
                script.Append("), DAY(");
                statement.Visit(in expression, in script);
                script.Append("), DATEPART(HOUR, ");
                statement.Visit(in expression, in script);
                script.Append("), DATEPART(MINUTE, ");
                statement.Visit(in expression, in script);
                script.Append("), DATEPART(SECOND, ");
                statement.Visit(in expression, in script);
                script.Append("), 0, 0)");
            }
            else
            {
                throw new InvalidOperationException($"[DATESTART] the first parameter is invalid value");
            }
        }
        private static void DATEEND(in SqlTranspiler statement, in FunctionExpression node, in StringBuilder script)
        {
            SyntaxNode expression = node.Parameters[0];

            if (expression is not ScalarExpression scalar)
            {
                throw new InvalidOperationException($"[DATEEND] the first parameter must be string");
            }

            string datepart = scalar.Literal;

            expression = node.Parameters[1];

            if (datepart == "YEAR")
            {
                script.Append("DATETIME2FROMPARTS(YEAR(");
                statement.Visit(in expression, in script);
                script.Append("), 12, 31, 23, 59, 59, 0, 0)");
            }
            else if (datepart == "QUARTER")
            {
                script.Append("DATETIME2FROMPARTS(YEAR(");
                statement.Visit(in expression, in script);
                script.Append("), CASE WHEN MONTH(");
                statement.Visit(in expression, in script);
                script.Append(") <= 3 THEN 3 WHEN MONTH(");
                statement.Visit(in expression, in script);
                script.Append(") <= 6 THEN 6 WHEN MONTH(");
                statement.Visit(in expression, in script);
                script.Append(") <= 9 THEN 9 ELSE 12 END, CASE WHEN MONTH(");
                statement.Visit(in expression, in script);
                script.Append(") <= 3 THEN 31 WHEN MONTH(");
                statement.Visit(in expression, in script);
                script.Append(") <= 6 THEN 30 WHEN MONTH(");
                statement.Visit(in expression, in script);
                script.Append(") <= 9 THEN 30 ELSE 31 END, 23, 59, 59, 0, 0)");
            }
            else if (datepart == "MONTH")
            {
                script.Append("DATETIME2FROMPARTS(YEAR(");
                statement.Visit(in expression, in script);
                script.Append("), MONTH(");
                statement.Visit(in expression, in script);
                script.Append("), DAY(EOMONTH(");
                statement.Visit(in expression, in script);
                script.Append(")), 23, 59, 59, 0, 0)");
            }
            else if (datepart == "DAY")
            {
                script.Append("DATETIME2FROMPARTS(YEAR(");
                statement.Visit(in expression, in script);
                script.Append("), MONTH(");
                statement.Visit(in expression, in script);
                script.Append("), DAY(");
                statement.Visit(in expression, in script);
                script.Append("), 23, 59, 59, 0, 0)");
            }
            else if (datepart == "HOUR")
            {
                script.Append("DATETIME2FROMPARTS(YEAR(");
                statement.Visit(in expression, in script);
                script.Append("), MONTH(");
                statement.Visit(in expression, in script);
                script.Append("), DAY(");
                statement.Visit(in expression, in script);
                script.Append("), DATEPART(HOUR, ");
                statement.Visit(in expression, in script);
                script.Append("), 59, 59, 0, 0)");
            }
            else if (datepart == "MINUTE")
            {
                script.Append("DATETIME2FROMPARTS(YEAR(");
                statement.Visit(in expression, in script);
                script.Append("), MONTH(");
                statement.Visit(in expression, in script);
                script.Append("), DAY(");
                statement.Visit(in expression, in script);
                script.Append("), DATEPART(HOUR, ");
                statement.Visit(in expression, in script);
                script.Append("), DATEPART(MINUTE, ");
                statement.Visit(in expression, in script);
                script.Append("), 59, 0, 0)");
            }
            else if (datepart == "SECOND")
            {
                script.Append("DATETIME2FROMPARTS(YEAR(");
                statement.Visit(in expression, in script);
                script.Append("), MONTH(");
                statement.Visit(in expression, in script);
                script.Append("), DAY(");
                statement.Visit(in expression, in script);
                script.Append("), DATEPART(HOUR, ");
                statement.Visit(in expression, in script);
                script.Append("), DATEPART(MINUTE, ");
                statement.Visit(in expression, in script);
                script.Append("), DATEPART(SECOND, ");
                statement.Visit(in expression, in script);
                script.Append("), 0, 0)");
            }
            else
            {
                throw new InvalidOperationException($"[DATEEND] the first parameter is invalid value");
            }
        }
        private static void DATEADD(in SqlTranspiler statement, in FunctionExpression node, in StringBuilder script)
        {
            SyntaxNode expression = node.Parameters[0];

            if (expression is not ScalarExpression scalar)
            {
                throw new InvalidOperationException($"[DATEADD] the first parameter must be string");
            }

            string datepart = scalar.Literal.ToUpper();

            script.Append("DATEADD(").Append(datepart).Append(',').Append(' ');

            expression = node.Parameters[1];
            statement.Visit(in expression, in script);
            script.Append(',').Append(' ');

            expression = node.Parameters[2];
            statement.Visit(in expression, in script);
            script.Append(')');
        }
    }
}