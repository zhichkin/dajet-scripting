using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using System.Reflection;
using System.Text;

namespace DaJet.Scripting
{
    public sealed class PgSelectTranspiler : SelectTranspiler
    {
        protected override string GetNextParameterName()
        {
            return string.Format("${0}", GetParametersCount() + 1);
        }

        protected override void Visit(in SelectStatement node, in StringBuilder script)
        {
            script.AppendLine();

            if (node.CommonTables is not null)
            {
                script.Append("WITH ");

                if (IsRecursive(node.CommonTables))
                {
                    script.Append("RECURSIVE ");
                }

                Visit(node.CommonTables, in script);
            }

            Visit(node.Expression, in script);
        }
        protected override void Visit(in SelectExpression node, in StringBuilder script)
        {
            script.Append("SELECT");

            if (node.Distinct)
            {
                script.Append(" DISTINCT");
            }

            script.AppendLine();

            for (int i = 0; i < node.Columns.Count; i++)
            {
                if (i > 0) { script.AppendLine(","); }

                Visit(node.Columns[i], in script);
            }

            if (node.Into is not null) { Visit(node.Into, in script); }
            if (node.From is not null) { Visit(node.From, in script); }
            if (node.Where is not null) { Visit(node.Where, in script); }
            if (node.Group is not null) { Visit(node.Group, in script); }
            if (node.Having is not null) { Visit(node.Having, in script); }
            if (node.Order is not null) { Visit(node.Order, in script); }
            if (node.Top is not null) { Visit(node.Top, in script); }

            if (!string.IsNullOrEmpty(node.Options))
            {
                script.AppendLine().Append(node.Options).AppendLine();
            }
        }
        protected override void Visit(in TopClause node, in StringBuilder script)
        {
            script.AppendLine().Append("LIMIT ");

            Visit(node.Expression, in script);
        }
        protected override void Visit(in IntoClause node, in StringBuilder script)
        {
            if (node.Table is not null)
            {
                script.AppendLine().Append("INTO TEMPORARY TABLE ");

                Visit(node.Table, in script);
            }
        }
        protected override void Visit(in TableJoinOperator node, in StringBuilder script)
        {
            Visit(node.Expression1, in script); // left operand

            if (node.Token == Token.CROSS_APPLY)
            {
                script.AppendLine().Append("INNER JOIN LATERAL ");
            }
            else if (node.Token == Token.OUTER_APPLY)
            {
                script.AppendLine().Append("LEFT JOIN LATERAL ");
            }
            else
            {
                script.AppendLine().Append(node.Token.ToString()).Append(" JOIN ");
            }

            Visit(node.Expression2, in script); // right operand

            if (node.Token == Token.CROSS_APPLY || node.Token == Token.OUTER_APPLY)
            {
                script.Append(" ON TRUE");
            }
            else if (node.On is not null) //NOTE: null if CROSS JOIN
            {
                Visit(node.On, in script);
            }
        }
        private bool IsRecursive(in CommonTableExpression cte)
        {
            if (IsRecursive(in cte, cte.Expression))
            {
                return true;
            }

            if (cte.Next is null) { return false; }

            return IsRecursive(cte.Next);
        }
        private bool IsRecursive(in CommonTableExpression cte, in SyntaxNode node)
        {
            if (node is SelectExpression select)
            {
                return IsRecursive(in cte, in select);
            }
            else if (node is TableJoinOperator join)
            {
                return IsRecursive(in cte, in join);
            }
            else if (node is TableUnionOperator union)
            {
                return IsRecursive(in cte, in union);
            }
            else if (node is TableReference table)
            {
                return IsRecursive(in cte, in table);
            }

            return false;
        }
        private bool IsRecursive(in CommonTableExpression cte, in SelectExpression select)
        {
            if (select.From is null) { return false; }

            return IsRecursive(in cte, select.From.Expression);
        }
        private bool IsRecursive(in CommonTableExpression cte, in TableJoinOperator node)
        {
            if (IsRecursive(in cte, node.Expression1))
            {
                return true;
            }

            return IsRecursive(in cte, node.Expression2);
        }
        private bool IsRecursive(in CommonTableExpression cte, in TableUnionOperator node)
        {
            if (node.Expression1 is SelectExpression select1)
            {
                if (IsRecursive(in cte, in select1)) { return true; }
            }
            else if (node.Expression1 is TableUnionOperator union1)
            {
                if (IsRecursive(in cte, in union1)) { return true; }
            }

            if (node.Expression2 is SelectExpression select2)
            {
                return IsRecursive(in cte, in select2);
            }
            else if (node.Expression2 is TableUnionOperator union2)
            {
                return IsRecursive(in cte, in union2);
            }

            return false;
        }
        private bool IsRecursive(in CommonTableExpression cte, in TableReference table)
        {
            return (table.Binding == cte);
        }
        
        protected override void Visit(in ScalarExpression node, in StringBuilder script)
        {
            if (node.Token == Token.Boolean)
            {
                script.Append(node.Literal);
            }
            else if (node.Token == Token.DateTime)
            {
                if (DateTime.TryParse(node.Literal, out DateTime datetime))
                {
                    script.Append($"\'{datetime.AddYears(YearOffset):yyyy-MM-ddTHH:mm:ss}\'::timestamp");
                }
                else
                {
                    script.Append(node.Literal);
                }
            }
            else if (node.Token == Token.String)
            {
                script.Append($"CAST(\'{node.Literal}\' AS mvarchar)");
            }
            else if (node.Token == Token.Uuid)
            {
                script.Append($"CAST(E'\\\\x{LexerHelper.GetUuidHexLiteral(new Guid(node.Literal))}' AS bytea)");
            }
            else if (node.Token == Token.Binary || node.Literal.StartsWith("0x"))
            {
                if (node.Literal == "0x00") // TODO: подумать как убрать этот костыль
                {
                    script.Append("FALSE");
                }
                else if (node.Literal == "0x01") // TODO: может прилетать как значение по умолчанию для INSERT
                {
                    script.Append("TRUE");
                }
                else
                {
                    script.Append($"CAST(E'\\\\{node.Literal.TrimStart('0')}' AS bytea)");
                }
            }
            else if (node.Token == Token.Entity) // implicit cast to uuid
            {
                script.Append($"CAST(E'\\\\x{LexerHelper.GetUuidHexLiteral(Entity.Parse(node.Literal).Identity)}' AS bytea)");
            }
            else // Number
            {
                script.Append(node.Literal);
            }
        }
        protected override void Visit(in VariableReference node, in StringBuilder script)
        {
            string parameter = GetNextParameterName();

            if (node.Binding is DeclareStatement declare && declare.Type.IsString)
            {
                if (declare.Type.IsArray)
                {
                    parameter += "::mvarchar[]";
                }
                else
                {
                    parameter += "::mvarchar";
                }
            }

            script.Append(parameter);

            _statement.Input.Add(node);
        }
        protected override void Visit(in MemberAccessExpression node, in StringBuilder script)
        {
            List<string> members = node.GetAccessMembers();

            string memberName = members[1];

            string parameter = GetNextParameterName();

            if (node.Binding is DeclareStatement declare && declare.Type.IsObject)
            {
                if (!string.IsNullOrEmpty(declare.Schema))
                {
                    if (!SchemaRegistry.TryGet(declare.Schema, out Type type))
                    {
                        PropertyInfo property = type.GetProperty(memberName,
                            BindingFlags.Instance | BindingFlags.Public);

                        if (property is not null && property.PropertyType == typeof(string))
                        {
                            parameter += "::mvarchar";
                        }
                    }
                }
                else if (declare.Binding is DefineStatement binding) // Anonymous data schema
                {
                    DefineProperty definition = binding.GetPropertyByName(memberName);

                    if (definition is not null && definition.Type.IsString)
                    {
                        parameter += "::mvarchar";
                    }
                }
            }

            script.Append(parameter);

            _statement.Input.Add(node);
        }
        protected override void Visit(in FunctionExpression node, in StringBuilder script)
        {
            if (PgSqlFunctions.TryGet(node.Token, out Function function))
            {
                function.Transpile(this, in node, in script);
            }
            else if (SqlFunctions.TryGet(node.Token, out _))
            {
                base.Visit(in node, in script);
            }
            else if (DaJetFunctions.Contains(node.Name))
            {
                script.Append(GetNextParameterName());

                _statement.Input.Add(node);
            }
            else
            {
                throw new InvalidOperationException($"Unknown function name: {node.Name}");
            }
        }
        protected override void VisitEnumValue(in ColumnReference node, in StringBuilder script)
        {
            if (node.Binding is not Entity value)
            {
                return;
            }

            script.Append($"E'\\\\x{LexerHelper.GetUuidHexLiteral(value.Identity)}'::bytea");
            
            if (node.Parent is ColumnExpression parent) // SELECT clause column
            {
                if (!string.IsNullOrEmpty(parent.Alias))
                {
                    script.Append(" AS ").Append(parent.Alias);
                }
            }
        }

        protected override void Visit(in TableVariableExpression node, in StringBuilder script)
        {
            script.Append($"CREATE TEMPORARY TABLE {node.Name} AS").AppendLine();

            base.Visit(in node, in script);

            script.Append(';').AppendLine();
        }
        protected override void Visit(in TemporaryTableExpression node, in StringBuilder script)
        {
            script.Append($"CREATE TEMPORARY TABLE {node.Name} AS").AppendLine();

            base.Visit(in node, in script);

            script.Append(';').AppendLine();
        }

        protected override void Visit(in ComparisonOperator node, in StringBuilder script)
        {
            Visit(node.Expression1, in script);

            if (node.Modifier == Token.NOT)
            {
                script.Append(" NOT ");
            }
            else
            {
                script.Append(' ');
            }

            script.Append(LexerHelper.GetComparisonLiteral(node.Token));

            script.Append(' ');

            if (node.Modifier == Token.ALL)
            {
                script.Append("ALL ");
            }
            else if (node.Modifier == Token.ANY)
            {
                script.Append("ANY ");
            }

            if (node.Token == Token.IN && node.Expression2 is ValuesExpression values)
            {
                script.Append('(');

                SyntaxNode value = values.Values[0];

                if (value is VariableReference variable &&
                    variable.Binding is DeclareStatement declare &&
                    declare.Type.IsArray)
                {
                    //NOTE: alternative : <expression> = ANY ($array::mvarchar[])

                    script.Append("SELECT unnest(");

                    Visit(in value, in script);

                    script.Append(')');
                }
                else
                {
                    for (int i = 0; i < values.Values.Count; i++)
                    {
                        value = values.Values[i];

                        if (i > 0) { script.Append(", "); }

                        Visit(in value, in script);
                    }
                }

                script.Append(')');
            }
            else
            {
                Visit(node.Expression2, in script);
            }
        }
    }
}