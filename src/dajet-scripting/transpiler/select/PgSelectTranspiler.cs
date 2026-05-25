using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using System.Reflection;
using System.Text;

namespace DaJet.Scripting
{
    public sealed class PgSelectTranspiler : SelectTranspiler
    {
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

            script.Append(';');
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

            if (!string.IsNullOrEmpty(node.Hints))
            {
                script.AppendLine().Append(node.Hints);
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
            int count = _statement.Input.Count + 1; //FIXME: create RegisterInputParameter !?

            string parameter;

            if (node.Binding is DeclareStatement declare && declare.Type.IsString)
            {
                parameter = string.Format("${0}::mvarchar", count);
            }
            else
            {
                parameter = string.Format("${0}", count);
            }

            script.Append(parameter);

            _statement.Input.Add(node);
        }
        protected override void Visit(in MemberAccessExpression node, in StringBuilder script)
        {
            List<string> members = node.GetAccessMembers(node.Identifier);

            string memberName = members[1];

            int count = _statement.Input.Count + 1; //FIXME: create RegisterInputParameter !?

            string parameter = string.Format("${0}", count);

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
            if (SqlFunctions.TryGet(node.Token, out SqlFunction function))
            {
                base.Visit(in node, in script);

                //function.Visit(in node, in script, this);
            }
            else if (DaJetFunctions.Contains(node.Name))
            {
                int count = _statement.Input.Count + 1; //FIXME: create RegisterInputParameter !?

                string parameter = string.Format("${0}", count);

                script.Append(parameter);

                _statement.Input.Add(node);
            }
            else
            {
                throw new InvalidOperationException($"Unknown function name: {node.Name}");
            }
        }

        //protected override void Visit(in FunctionExpression node, in StringBuilder script)
        //{
        //    if (node.Token == TokenType.UDF)
        //    {
        //        if (UDF.TryGet(node.Name, out IUserDefinedFunction transpiler))
        //        {
        //            FunctionDescriptor function = transpiler.Transpile(this, in node, in script);

            //            if (function is not null)
            //            {
            //                Functions.Add(function);
            //            }
            //        }
            //        else
            //        {
            //            throw new InvalidOperationException($"Invalid function name: {node.Name}");
            //        }

            //        return;
            //    }

            //    string name = node.Name.ToUpperInvariant();

            //    string pg_name = null;

            //    if (name == "NEWUUID")
            //    {
            //        script.Append($"CAST(E'\\\\x{ParserHelper.GetUuidHexLiteral(Guid.NewGuid())}' AS bytea)"); return;
            //    }
            //    else if (name == "ISNULL")
            //    {
            //        node.Name = "COALESCE";
            //    }
            //    else if (name == "DATALENGTH")
            //    {
            //        node.Name = "OCTET_LENGTH";
            //        script.Append(node.Name).Append('(');
            //        script.Append("CAST(");
            //        Visit(node.Parameters[0], in script);
            //        script.Append(" AS text)");
            //        script.Append(')');
            //        return; //TODO: OCTET_LENGTH - what if data type of column is bytea ?
            //    }
            //    else if (name == "NOW") // timestamp without time zone
            //    {
            //        script.Append("NOW()::timestamp"); return;
            //    }
            //    else if (name == "UTC") // timestamp without time zone
            //    {
            //        script.Append("NOW() AT TIME ZONE 'UTC'"); return;
            //    }
            //    else if (name == "VECTOR")
            //    {
            //        if (node.Parameters is not null && node.Parameters.Count > 0 && node.Parameters[0] is ScalarExpression scalar)
            //        {
            //            //script.Append($"CAST(nextval('{scalar.Literal.ToLower()}') AS numeric(19, 0))");
            //            script.Append($"nextval('{scalar.Literal.ToLower()}')");
            //        }
            //        return;
            //    }
            //    else if (name == "CHARLENGTH") { pg_name = "LENGTH"; }

            //    if (pg_name is not null)
            //    {
            //        script.Append(pg_name);
            //    }
            //    else
            //    {
            //        script.Append(node.Name);
            //    }

            //    if (node.Token != TokenType.EXISTS)
            //    {
            //        script.Append('('); //NOTE: EXISTS function has one parameter - TableExpression
            //    }

            //    if (node.Token == TokenType.COUNT &&
            //        node.Modifier == TokenType.DISTINCT)
            //    {
            //        script.Append("DISTINCT ");
            //    }

            //    SyntaxNode expression;

            //    for (int i = 0; i < node.Parameters.Count; i++)
            //    {
            //        expression = node.Parameters[i];
            //        if (i > 0) { script.Append(", "); }

            //        if (name == "SUBSTRING" && i == 0)
            //        {
            //            script.Append("CAST(");
            //            Visit(in expression, in script);
            //            script.Append(" AS varchar)");
            //        }
            //        else if (name == "STRING_AGG" || name == "REPLACE"
            //            || name == "CONCAT" || name == "CONCAT_WS"
            //            || name == "LOWER" || name == "UPPER"
            //            || name == "LTRIM" || name == "RTRIM")
            //        {
            //            script.Append("CAST(");
            //            Visit(in expression, in script);
            //            script.Append(" AS text)");
            //        }
            //        else
            //        {
            //            Visit(in expression, in script);
            //        }
            //    }

            //    if (node.Token != TokenType.EXISTS)
            //    {
            //        script.Append(')'); //NOTE: EXISTS function has one parameter - TableExpression
            //    }

            //    if (node.Over is not null)
            //    {
            //        script.Append(' ');
            //        Visit(node.Over, in script);
            //    }
            //}

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
    }
}