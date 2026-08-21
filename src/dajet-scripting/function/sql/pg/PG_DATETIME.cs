using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using System.Text;

namespace DaJet.Scripting
{
    internal sealed class PG_DATETIME : Function
    {
        public override DataType GetReturnType(in FunctionExpression node)
        {
            return DataType.DateTime;
        }
        public override void Transpile(in SqlTranspiler statement, in FunctionExpression node, in StringBuilder script)
        {
            if (node.Token == Token.NOW)
            {
                script.Append("NOW()::timestamp");
            }
            else if (node.Token == Token.UTC)
            {
                script.Append("NOW() AT TIME ZONE 'UTC'");
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
                script.Append("make_timestamp(date_part('year', ");
                statement.Visit(in expression, in script);
                script.Append(")::integer, 1, 1, 0, 0, 0.0)");
            }
            else if (datepart == "QUARTER")
            {
                script.Append("make_timestamp(date_part('year', ");
                statement.Visit(in expression, in script);
                script.Append(")::integer, CASE WHEN date_part('month', ");
                statement.Visit(in expression, in script);
                script.Append(")::integer <= 3 THEN 1 WHEN date_part('month', ");
                statement.Visit(in expression, in script);
                script.Append(")::integer <= 6 THEN 4 WHEN date_part('month', ");
                statement.Visit(in expression, in script);
                script.Append(")::integer <= 9 THEN 7 ELSE 10 END, 1, 0, 0, 0.0)");
            }
            else if (datepart == "MONTH")
            {
                script.Append("make_timestamp(date_part('year', ");
                statement.Visit(in expression, in script);
                script.Append(")::integer, date_part('month', ");
                statement.Visit(in expression, in script);
                script.Append(")::integer, 1, 0, 0, 0.0)");
            }
            else if (datepart == "DAY")
            {
                script.Append("make_timestamp(date_part('year', ");
                statement.Visit(in expression, in script);
                script.Append(")::integer, date_part('month', ");
                statement.Visit(in expression, in script);
                script.Append(")::integer, date_part('day', ");
                statement.Visit(in expression, in script);
                script.Append(")::integer, 0, 0, 0.0)");
            }
            else if (datepart == "HOUR")
            {
                script.Append("make_timestamp(date_part('year', ");
                statement.Visit(in expression, in script);
                script.Append(")::integer, date_part('month', ");
                statement.Visit(in expression, in script);
                script.Append(")::integer, date_part('day', ");
                statement.Visit(in expression, in script);
                script.Append(")::integer, date_part('hour', ");
                statement.Visit(in expression, in script);
                script.Append(")::integer, 0, 0.0)");
            }
            else if (datepart == "MINUTE")
            {
                script.Append("make_timestamp(date_part('year', ");
                statement.Visit(in expression, in script);
                script.Append(")::integer, date_part('month', ");
                statement.Visit(in expression, in script);
                script.Append(")::integer, date_part('day', ");
                statement.Visit(in expression, in script);
                script.Append(")::integer, date_part('hour', ");
                statement.Visit(in expression, in script);
                script.Append(")::integer, date_part('minute', ");
                statement.Visit(in expression, in script);
                script.Append(")::integer, 0.0)");
            }
            else if (datepart == "SECOND")
            {
                script.Append("make_timestamp(date_part('year', ");
                statement.Visit(in expression, in script);
                script.Append(")::integer, date_part('month', ");
                statement.Visit(in expression, in script);
                script.Append(")::integer, date_part('day', ");
                statement.Visit(in expression, in script);
                script.Append(")::integer, date_part('hour', ");
                statement.Visit(in expression, in script);
                script.Append(")::integer, date_part('minute', ");
                statement.Visit(in expression, in script);
                script.Append(")::integer, date_part('second', ");
                statement.Visit(in expression, in script);
                script.Append(')').Append(')');
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
                script.Append("date_trunc('year', ");
                statement.Visit(in expression, in script);
                script.Append(") + '11 months 30 days 23 hours 59 minutes 59 seconds'");
            }
            else if (datepart == "QUARTER")
            {
                script.Append("make_timestamp(date_part('year', ");
                statement.Visit(in expression, in script);
                script.Append(")::integer, CASE WHEN date_part('month', ");
                statement.Visit(in expression, in script);
                script.Append(")::integer <= 3 THEN 1 WHEN date_part('month', ");
                statement.Visit(in expression, in script);
                script.Append(")::integer <= 6 THEN 4 WHEN date_part('month', ");
                statement.Visit(in expression, in script);
                script.Append(")::integer <= 9 THEN 7 ELSE 10 END, 1, 0, 0, 0.0)");
            }
            else if (datepart == "MONTH")
            {
                script.Append("date_trunc('month', ");
                statement.Visit(in expression, in script);
                script.Append(")");
            }
            else if (datepart == "DAY")
            {
                script.Append("date_trunc('day', ");
                statement.Visit(in expression, in script);
                script.Append(")");
            }
            else if (datepart == "HOUR")
            {
                script.Append("make_timestamp(date_part('year', ");
                statement.Visit(in expression, in script);
                script.Append(")::integer, date_part('month', ");
                statement.Visit(in expression, in script);
                script.Append(")::integer, date_part('day', ");
                statement.Visit(in expression, in script);
                script.Append(")::integer, date_part('hour', ");
                statement.Visit(in expression, in script);
                script.Append(")::integer, 0, 0.0)");
            }
            else if (datepart == "MINUTE")
            {
                script.Append("make_timestamp(date_part('year', ");
                statement.Visit(in expression, in script);
                script.Append(")::integer, date_part('month', ");
                statement.Visit(in expression, in script);
                script.Append(")::integer, date_part('day', ");
                statement.Visit(in expression, in script);
                script.Append(")::integer, date_part('hour', ");
                statement.Visit(in expression, in script);
                script.Append(")::integer, date_part('minute', ");
                statement.Visit(in expression, in script);
                script.Append(")::integer, 0.0)");
            }
            else if (datepart == "SECOND")
            {
                script.Append("make_timestamp(date_part('year', ");
                statement.Visit(in expression, in script);
                script.Append(")::integer, date_part('month', ");
                statement.Visit(in expression, in script);
                script.Append(")::integer, date_part('day', ");
                statement.Visit(in expression, in script);
                script.Append(")::integer, date_part('hour', ");
                statement.Visit(in expression, in script);
                script.Append(")::integer, date_part('minute', ");
                statement.Visit(in expression, in script);
                script.Append(")::integer, date_part('second', ");
                statement.Visit(in expression, in script);
                script.Append(')').Append(')');
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