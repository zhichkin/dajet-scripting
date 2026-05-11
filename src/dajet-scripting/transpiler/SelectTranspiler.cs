using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using System.Text;

namespace DaJet.Scripting
{
    public class SelectTranspiler : IStatementTranspiler
    {
        private SqlStatement _statement;
        public int YearOffset { get; set; }
        
        void IStatementTranspiler.Visit(in SyntaxNode expression, in StringBuilder script)
        {
            Visit(in expression, in script);
        }
        bool IStatementTranspiler.TryTranspile(in SyntaxNode node, out SqlStatement statement, out string error)
        {
            if (node is not SelectStatement select)
            {
                throw new ArgumentOutOfRangeException(nameof(node));
            }

            return TryTranspile(in select, out statement, out error);
        }
        
        public bool TryTranspile(in SelectStatement node, out SqlStatement statement, out string error)
        {
            error = null;

            StringBuilder script = new();

            _statement = new SqlStatement(node);

            try
            {
                Visit(in node, in script);

                _statement.Sql = script.ToString();
                
                if (node.GetIntoClause() is IntoClause into)
                {
                    if (into.Value is VariableReference variable)
                    {
                        _statement.Output = variable;
                    }
                    else if (into.Table is not null)
                    {
                        _statement.Output = into.Table;
                    }
                }
            }
            catch (Exception exception)
            {
                error = ExceptionHelper.GetErrorMessage(exception);
            }

            statement = _statement;
            
            _statement = null;

            return string.IsNullOrEmpty(error);
        }
        private void Visit(in SyntaxNode node, in StringBuilder script)
        {
            if (node is GroupOperator group) { Visit(in group, in script); }
            else if (node is UnaryOperator unary) { Visit(in unary, in script); }
            else if (node is BinaryOperator binary) { Visit(in binary, in script); }
            else if (node is AdditionOperator addition) { Visit(in addition, in script); }
            else if (node is MultiplyOperator multiply) { Visit(in multiply, in script); }
            else if (node is ComparisonOperator comparison) { Visit(in comparison, in script); }
            else if (node is CaseExpression case_when) { Visit(in case_when, in script); }
            else if (node is ScalarExpression scalar) { Visit(in scalar, in script); }
            else if (node is VariableReference variable) { Visit(in variable, in script); }
            else if (node is MemberAccessExpression member) { Visit(in member, in script); }
            else if (node is SelectExpression select) { Visit(in select, in script); }
            else if (node is TableJoinOperator join) { Visit(in join, in script); }
            else if (node is TableUnionOperator union) { Visit(in union, in script); }
            else if (node is TableExpression derived) { Visit(in derived, in script); }
            else if (node is TableReference table) { Visit(in table, in script); }
            else if (node is ColumnReference column) { Visit(in column, in script); }
            else if (node is FunctionExpression function) { Visit(in function, in script); }
            else if (node is OverClause over) { Visit(in over, in script); }
            else if (node is TemporaryTableExpression temporary_table) { Visit(in temporary_table, in script); }
        }

        protected virtual void Visit(in SelectStatement node, in StringBuilder script)
        {
            if (node.CommonTables is not null)
            {
                script.Append("WITH ");

                Visit(node.CommonTables, in script);

                script.AppendLine();
            }

            Visit(node.Expression, in script);
        }
        protected virtual void Visit(in SelectExpression node, in StringBuilder script)
        {
            script.Append("SELECT");

            if (node.Distinct)
            {
                script.Append(" DISTINCT");
            }

            if (node.Top is not null)
            {
                Visit(node.Top, in script);
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
        }
        protected virtual void Visit(in TableReference node, in StringBuilder script)
        {
            if (node.Binding is EntityDefinition entity)
            {
                script.Append(entity.DbName);
            }
            else if (node.Binding is TableExpression
                || node.Binding is CommonTableExpression
                || node.Binding is TableVariableExpression
                || node.Binding is TemporaryTableExpression)
            {
                script.Append(node.Identifier);
            }

            if (!string.IsNullOrEmpty(node.Alias))
            {
                script.Append(" AS ").Append(node.Alias);
            }
        }
        
        protected virtual void Visit(in ColumnExpression node, in StringBuilder script)
        {
            if (node.Expression is ColumnReference column)
            {
                Visit(in column, in script); // ColumnReference - своя логика обработки Alias

                //if (column.Token == Token.Enumeration)
                //{
                //    if (!string.IsNullOrEmpty(node.Alias))
                //    {
                //        script.Append(" AS ").Append(node.Alias);
                //    }
                //}
            }
            else
            {
                Visit(node.Expression, in script);

                if (!string.IsNullOrEmpty(node.Alias)) // Стандартная логика обработки Alias
                {
                    script.Append(" AS ").Append(node.Alias);
                }
            }
        }
        protected virtual void Visit(in ColumnReference node, in StringBuilder script)
        {
            if (node.Binding is PropertyDefinition property) // Прямой источник данных
            {
                int dot = node.Identifier.IndexOf('.');
                string tableAlias = dot > 0 ? node.Identifier[..dot] : string.Empty;

                ColumnDefinition column;

                for (int i = 0; i < property.Columns.Count; i++)
                {
                    column = property.Columns[i];

                    if (i > 0) { script.Append(", "); }

                    if (!string.IsNullOrEmpty(tableAlias))
                    {
                        script.Append(tableAlias).Append('.');
                    }

                    script.Append(column.Name);

                    if (node.Parent is ColumnExpression parent) // SELECT clause column
                    {
                        string alias = string.IsNullOrEmpty(parent.Alias) ? property.Name : parent.Alias;

                        if (property.Columns.Count == 1) // single column
                        {
                            script.Append(" AS ").Append(alias);
                        }
                        else // multiple columns
                        {
                            string suffix = GetColumnPurposeSuffix(column.Purpose);
                            script.Append(" AS ").Append(alias).Append('_').Append(suffix);
                        }
                    }
                }
            }
            else if (node.Binding is ColumnExpression derived) // Наследуемый источник данных
            {
                if (derived.Source is null) // Константа, функция, параметр или выражение
                {
                    if (derived.Expression is ScalarExpression)
                    {
                        script.Append(node.Identifier); return;
                    }
                    
                    throw new InvalidOperationException($"Ошибка привязки данных: {node.Identifier}");
                }
                else
                {
                    property = derived.Source;
                }

                ColumnDefinition column;

                for (int i = 0; i < property.Columns.Count; i++)
                {
                    column = property.Columns[i];

                    if (i > 0) { script.Append(", "); }

                    string alias = null;

                    if (node.Parent is ColumnExpression parent) // SELECT clause column
                    {
                        alias = parent.Alias;
                    }
                    
                    if (property.Columns.Count == 1) // single column
                    {
                        script.Append(node.Identifier);

                        if (!string.IsNullOrEmpty(alias))
                        {
                            script.Append(" AS ").Append(alias);
                        }
                    }
                    else // multiple columns
                    {
                        string suffix = GetColumnPurposeSuffix(column.Purpose);
                        script.Append(node.Identifier).Append('_').Append(suffix);
                        
                        if (!string.IsNullOrEmpty(alias))
                        {
                            script.Append(" AS ").Append(alias).Append('_').Append(suffix);
                        }
                    }
                }
            }

            //else if (node.Binding is EnumValue value)
            //{
            //    Visit(in value, in script);
            //}
        }
        private static string GetColumnPurposeSuffix(ColumnPurpose purpose)
        {
            if (purpose == ColumnPurpose.Tag) { return "TYPE"; }
            else if (purpose == ColumnPurpose.Boolean) { return "L"; }
            else if (purpose == ColumnPurpose.Numeric) { return "N"; }
            else if (purpose == ColumnPurpose.DateTime) { return "T"; }
            else if (purpose == ColumnPurpose.String) { return "S"; }
            else if (purpose == ColumnPurpose.TypeCode) { return "TRef"; }
            else if (purpose == ColumnPurpose.Identity) { return "RRef"; }
            else
            {
                return string.Empty;
            }
        }

        protected virtual void Visit(in TableExpression node, in StringBuilder script)
        {
            script.Append('(');

            Visit(node.Expression, in script);

            script.Append(')');

            if (!string.IsNullOrEmpty(node.Alias))
            {
                script.Append(" AS " + node.Alias);
            }
        }
        protected virtual void Visit(in TableJoinOperator node, in StringBuilder script)
        {
            Visit(node.Expression1, in script); // left operand

            //if (node.Token == Token.APPEND)
            //{
            //    //NOTE: do not generate SQL database code
            //    //for the right TableExpression operand
            //    //leave it for the script processor

            //    return;
            //}

            if (node.Token == Token.CROSS_APPLY)
            {
                script.AppendLine().Append("CROSS APPLY ");
            }
            else if (node.Token == Token.OUTER_APPLY)
            {
                script.AppendLine().Append("OUTER APPLY ");
            }
            else
            {
                script.AppendLine().Append(node.Token.ToString()).Append(" JOIN ");
            }

            Visit(node.Expression2, in script); // right operand

            if (node.On is not null) { Visit(node.On, in script); } //NOTE: null if CROSS JOIN
        }
        protected virtual void Visit(in TableUnionOperator node, in StringBuilder script)
        {
            if (node.Expression1 is SelectExpression select1)
            {
                Visit(in select1, in script);
            }
            else if (node.Expression1 is TableUnionOperator union1)
            {
                Visit(in union1, in script);
            }

            if (node.Token == Token.UNION)
            {
                script.AppendLine().AppendLine("UNION");
            }
            else
            {
                script.AppendLine().AppendLine("UNION ALL");
            }

            if (node.Expression2 is SelectExpression select2)
            {
                Visit(in select2, in script);
            }
            else if (node.Expression2 is TableUnionOperator union2)
            {
                Visit(in union2, in script);
            }

            if (node.Order is OrderClause order)
            {
                Visit(in order, in script);
            }
        }
        protected virtual void Visit(in CommonTableExpression node, in StringBuilder script)
        {
            if (node.Next is not null)
            {
                Visit(node.Next, in script);
            }

            if (node.Next is not null)
            {
                script.Append(", ");
            }

            script.AppendLine($"{node.Name} AS ").Append('(');

            Visit(node.Expression, in script);

            script.AppendLine(")");
        }
        protected virtual void Visit(in TopClause node, in StringBuilder script)
        {
            script.Append(" TOP ").Append('(');

            Visit(node.Expression, in script);

            script.Append(')');
        }
        protected virtual void Visit(in IntoClause node, in StringBuilder script)
        {
            if (node.Table is not null)
            {
                script.AppendLine().Append("INTO ");

                Visit(node.Table, in script);
            }
        }
        protected virtual void Visit(in FromClause node, in StringBuilder script)
        {
            script.AppendLine().Append("FROM ");

            Visit(node.Expression, in script);
        }
        protected virtual void Visit(in WhereClause node, in StringBuilder script)
        {
            script.AppendLine().Append("WHERE ");

            Visit(node.Expression, in script);
        }
        protected virtual void Visit(in GroupClause node, in StringBuilder script)
        {
            if (node is null || node.Expressions is null || node.Expressions.Count == 0)
            {
                return;
            }

            script.AppendLine().AppendLine("GROUP BY");

            string separator = "," + Environment.NewLine;

            for (int i = 0; i < node.Expressions.Count; i++)
            {
                if (i > 0) { script.Append(separator); }

                Visit(node.Expressions[i], in script);
            }

            script.AppendLine();
        }
        protected virtual void Visit(in HavingClause node, in StringBuilder script)
        {
            script.Append("HAVING ");

            Visit(node.Expression, in script);
        }
        protected virtual void Visit(in OnClause node, in StringBuilder script)
        {
            script.AppendLine().Append("ON ");

            Visit(node.Expression, in script);
        }
        protected virtual void Visit(in OrderClause node, in StringBuilder script)
        {
            if (node is null || node.Expressions is null || node.Expressions.Count == 0)
            {
                return;
            }

            script.AppendLine().AppendLine("ORDER BY");

            OrderExpression order;

            string separator = ", ";

            for (int i = 0; i < node.Expressions.Count; i++)
            {
                order = node.Expressions[i];

                if (i > 0) { script.Append(separator); }

                if (order.Expression is ColumnReference column
                    && column.Binding is PropertyDefinition property
                    && property.Columns.Count > 1)
                {
                    ColumnDefinition field;

                    for (int f = 0; f < property.Columns.Count; f++)
                    {
                        field = property.Columns[f];

                        if (f > 0) { script.Append(", "); }

                        script.Append(field.Name);

                        if (order.Token == Token.DESC)
                        {
                            script.Append(" DESC");
                        }
                        else
                        {
                            script.Append(" ASC"); // default
                        }
                    }
                }
                else
                {
                    Visit(order.Expression, in script);

                    if (order.Token == Token.DESC)
                    {
                        script.Append(" DESC");
                    }
                    else
                    {
                        script.Append(" ASC"); // default
                    }
                }
            }

            if (node.Offset is not null)
            {
                script.AppendLine();

                script.Append("OFFSET ");

                Visit(node.Offset, in script);

                script.AppendLine(" ROWS");

                if (node.Fetch is not null)
                {
                    script.Append("FETCH NEXT ");

                    Visit(node.Fetch, in script);

                    script.AppendLine(" ROWS ONLY");
                }
            }
        }
        protected virtual void Visit(in GroupOperator node, in StringBuilder script)
        {
            script.Append('(');

            Visit(node.Expression, in script);

            script.Append(')');
        }
        protected virtual void Visit(in UnaryOperator node, in StringBuilder script)
        {
            script.Append(node.Token == Token.Minus ? "-" : "NOT ");

            Visit(node.Expression, in script);
        }
        protected virtual void Visit(in BinaryOperator node, in StringBuilder script)
        {
            Visit(node.Expression1, in script);

            script.AppendLine().Append(node.Token.ToString()).Append(' ');

            Visit(node.Expression2, in script);
        }
        protected virtual void Visit(in AdditionOperator node, in StringBuilder script)
        {
            Visit(node.Expression1, in script);

            if (node.Token == Token.Plus)
            {
                script.Append(" + ");
            }
            else if (node.Token == Token.Minus)
            {
                script.Append(" - ");
            }

            Visit(node.Expression2, in script);
        }
        protected virtual void Visit(in MultiplyOperator node, in StringBuilder script)
        {
            Visit(node.Expression1, in script);

            if (node.Token == Token.Star)
            {
                script.Append(" * ");
            }
            else if (node.Token == Token.Divide)
            {
                script.Append(" / ");
            }
            else if (node.Token == Token.Modulo)
            {
                script.Append(" % ");
            }

            Visit(node.Expression2, in script);
        }
        protected virtual void Visit(in ComparisonOperator node, in StringBuilder script)
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

                SyntaxNode value;

                for (int i = 0; i < values.Values.Count; i++)
                {
                    value = values.Values[i];

                    if (i > 0) { script.Append(", "); }

                    Visit(in value, in script);
                }

                script.Append(')');
            }
            else
            {
                Visit(node.Expression2, in script);
            }
        }
        protected virtual void Visit(in CaseExpression node, in StringBuilder script)
        {
            script.Append("CASE");

            foreach (WhenClause when in node.CASE)
            {
                script.Append(" WHEN ");

                Visit(when.WHEN, in script);

                script.Append(" THEN ");

                Visit(when.THEN, in script);
            }
            if (node.ELSE is not null)
            {
                script.Append(" ELSE ");

                Visit(node.ELSE, in script);
            }
            script.Append(" END");
        }
        protected virtual void Visit(in ScalarExpression node, in StringBuilder script)
        {
            if (node.Token == Token.Boolean)
            {
                if (LexerHelper.IsTrueLiteral(node.Literal))
                {
                    script.Append("0x01");
                }
                else
                {
                    script.Append("0x00");
                }
            }
            else if (node.Token == Token.DateTime)
            {
                if (DateTime.TryParse(node.Literal, out DateTime datetime))
                {
                    script.Append($"CAST(\'{datetime.AddYears(YearOffset):yyyy-MM-ddTHH:mm:ss}\' AS datetime2)");
                }
                else
                {
                    script.Append(node.Literal);
                }
            }
            else if (node.Token == Token.String)
            {
                script.Append($"\'{node.Literal}\'");
            }
            else if (node.Token == Token.Uuid)
            {
                script.Append($"0x{LexerHelper.GetUuidHexLiteral(new Guid(node.Literal))}");
            }
            else if (node.Token == Token.Entity) // implicit cast to uuid
            {
                script.Append($"0x{LexerHelper.GetUuidHexLiteral(Entity.Parse(node.Literal).Identity)}");
            }
            else // Number | Binary
            {
                script.Append(node.Literal);
            }
        }
        protected virtual void Visit(in VariableReference node, in StringBuilder script)
        {
            int count = _statement.Input.Count;

            string parameter = string.Format("@p{0}", count);

            script.Append(parameter); // node.Identifier

            _statement.Input.Add(node);
        }
        protected virtual void Visit(in MemberAccessExpression node, in StringBuilder script)
        {
            int count = _statement.Input.Count;

            string parameter = string.Format("@p{0}", count);

            script.Append(parameter);

            _statement.Input.Add(node);

            //script.Append(node.GetDbParameterName());
        }

        //protected virtual void Visit(in EnumValue node, in StringBuilder script)
        //{
        //    script.Append($"0x{ParserHelper.GetUuidHexLiteral(node.Uuid)}");
        //}

        protected virtual void Visit(in FunctionExpression node, in StringBuilder script)
        {
            if (SqlFunctions.TryGet(node.Token, out SqlFunction function))
            {
                function.Visit(in node, in script, this);
            }
            else
            {
                throw new InvalidOperationException($"Unknown function name: {node.Name}");
            }

            //else if (name == "NOW")
            //{
            //    if (YearOffset == 0)
            //    {
            //        script.Append("GETDATE()");
            //    }
            //    else
            //    {
            //        script.Append("DATEADD(year, " + YearOffset.ToString() + ", GETDATE())");
            //    }
            //}
            //else if (name == "UTC")
            //{
            //    if (YearOffset == 0)
            //    {
            //        script.Append("GETUTCDATE()");
            //    }
            //    else
            //    {
            //        script.Append("DATEADD(year, " + YearOffset.ToString() + ", GETUTCDATE())");
            //    }
            //}
            //else if (name == "VECTOR")
            //{
            //    if (node.Parameters is not null && node.Parameters.Count > 0 && node.Parameters[0] is ScalarExpression scalar)
            //    {
            //        script.Append("NEXT VALUE FOR ").Append(scalar.Literal);
            //    }
            //}
            //else if (name == "CHARLENGTH")
            //{
            //    script.Append("LEN").Append('(');
            //    Visit(node.Parameters[0], in script);
            //    script.Append(')');
            //}
            //else if (name == "NEWUUID")
            //{
            //    script.Append("NEWID()");
            //}
            //else if (node.Token != TokenType.UDF)
            //{
            //    base.Visit(in node, in script);
            //}
            //else
            //{
            //    throw new InvalidOperationException($"Invalid function name: {node.Name}");
            //}
        }
        protected virtual void Visit(in OverClause node, in StringBuilder script)
        {
            script.Append("OVER").Append('(');

            if (node.Partition is not null &&
                node.Partition.Columns is not null &&
                node.Partition.Columns.Count > 0)
            {
                Visit(node.Partition, in script);
            }
            if (node.Order is not null)
            {
                Visit(node.Order, in script);
            }
            if (node.Preceding is not null || node.Following is not null)
            {
                script.Append(' ').Append(node.FrameType.ToString()).Append(' ');

                if (node.Preceding is not null && node.Following is not null)
                {
                    script.Append("BETWEEN").Append(' ');

                    Visit(node.Preceding, in script);

                    script.Append(" AND ");

                    Visit(node.Following, in script);
                }
                else if (node.Preceding is not null)
                {
                    Visit(node.Preceding, in script);
                }
            }
            script.Append(')');
        }
        protected virtual void Visit(in WindowFrame node, in StringBuilder script)
        {
            if (node.Extent == -1)
            {
                script.Append("UNBOUNDED ").Append(node.Token.ToString());
            }
            else if (node.Extent == 0)
            {
                script.Append("CURRENT ROW");
            }
            else if (node.Extent > 0)
            {
                script
                    .Append(node.Extent)
                    .Append(' ')
                    .Append(node.Token.ToString());
            }
        }
        protected virtual void Visit(in PartitionClause node, in StringBuilder script)
        {
            script.AppendLine().AppendLine("PARTITION BY");

            SyntaxNode expression;

            for (int i = 0; i < node.Columns.Count; i++)
            {
                expression = node.Columns[i];

                if (i > 0) { script.Append(", "); }

                Visit(in expression, in script);
            }
        }

        protected virtual void Visit(in TableVariableExpression node, in StringBuilder script)
        {
            Visit(node.Expression, in script);
        }
        protected virtual void Visit(in TemporaryTableExpression node, in StringBuilder script)
        {
            Visit(node.Expression, in script);
        }
    }
}