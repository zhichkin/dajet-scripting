using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using System.Data;
using System.Text;

namespace DaJet.Scripting
{
    public abstract class SqlTranspiler
    {
        public SqlTranspiler(ISchemaProvider schema)
        {
            Schema = schema;
        }
        protected ISchemaProvider Schema { get; private set; }
        protected int YearOffset { get; set; }
        protected void Visit(in SyntaxNode expression, in StringBuilder script)
        {
            if (expression is GroupOperator group) { Visit(in group, in script); }
            else if (expression is UnaryOperator unary) { Visit(in unary, in script); }
            else if (expression is BinaryOperator binary) { Visit(in binary, in script); }
            else if (expression is AdditionOperator addition) { Visit(in addition, in script); }
            else if (expression is MultiplyOperator multiply) { Visit(in multiply, in script); }
            else if (expression is ComparisonOperator comparison) { Visit(in comparison, in script); }
            else if (expression is CaseExpression case_when) { Visit(in case_when, in script); }
            else if (expression is ScalarExpression scalar) { Visit(in scalar, in script); }
            else if (expression is VariableReference variable) { Visit(in variable, in script); }
            else if (expression is MemberAccessExpression member) { Visit(in member, in script); }
            else if (expression is SelectExpression select) { Visit(in select, in script); }
            else if (expression is TableJoinOperator join) { Visit(in join, in script); }
            else if (expression is TableUnionOperator union) { Visit(in union, in script); }
            else if (expression is TableExpression derived) { Visit(in derived, in script); }
            else if (expression is TableReference table) { Visit(in table, in script); }
            else if (expression is ColumnReference column) { Visit(in column, in script); }
            else if (expression is FunctionExpression function) { Visit(in function, in script); }
            else if (expression is TableVariableExpression table_variable) { Visit(in table_variable, in script); }
            else if (expression is TemporaryTableExpression temporary_table) { Visit(in temporary_table, in script); }
            else if (expression is StarExpression star) { Visit(in star, in script); }
            
            //else if (expression is InsertStatement insert) { Visit(in insert, in script); }
            //else if (expression is UpdateStatement update) { Visit(in update, in script); }
            //else if (expression is DeleteStatement delete) { Visit(in delete, in script); }
            //else if (expression is SetExpression set) { Visit(in set, in script); }

            else if (expression is CreateSequenceStatement sequence) { Visit(in sequence, in script); }
            else if (expression is DropSequenceStatement drop_sequence) { Visit(in drop_sequence, in script); }
            else if (expression is ApplySequenceStatement apply_sequence) { Visit(in apply_sequence, in script); }
            else if (expression is RevokeSequenceStatement revoke_sequence) { Visit(in revoke_sequence, in script); }
        }
        public bool TryTranspile(in Script script, out List<string> errors)
        {
            errors = new List<string>();

            try
            {
                foreach (SyntaxNode node in script.Statements)
                {
                    if (node is UseStatement use)
                    {
                        StatementBlock block = use.Statements;

                        foreach (SyntaxNode statement in block.Statements)
                        {
                            if (statement is SelectStatement select)
                            {
                                Visit(in select);
                            }
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                errors.Add(ExceptionHelper.GetErrorMessage(exception));
            }

            return (errors.Count == 0);
        }

        #region "SELECT STATEMENT"
        protected virtual void Visit(in SelectStatement node)
        {
            StringBuilder script = new();

            if (node.CommonTables is not null)
            {
                script.Append("WITH ");

                Visit(node.CommonTables, in script);

                script.AppendLine();
            }

            Visit(node.Expression, in script);

            node.Sql = script.ToString();
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
                Visit(in column, in script); // terminates tree traversing at column reference

                if (column.Token == Token.Enumeration)
                {
                    if (!string.IsNullOrEmpty(node.Alias))
                    {
                        script.Append(" AS ").Append(node.Alias);
                    }
                }
            }
            else
            {
                Visit(node.Expression, in script);

                if (!string.IsNullOrEmpty(node.Alias))
                {
                    script.Append(" AS ").Append(node.Alias);
                }
            }
        }
        protected virtual void Visit(in StarExpression node, in StringBuilder script)
        {
            script.Append('*');
        }
        protected virtual void Visit(in ColumnReference node, in StringBuilder script)
        {
            if (node.Binding is PropertyDefinition property)
            {
                Visit(property.Columns, in script);
            }

            //if (node.Mapping is not null) // we are here from anywhere, but not ColumnExpression itself
            //{
            //    Visit(node.Mapping, in script); // terminates tree traversing at column reference
            //}
            //else if (node.Binding is EnumValue value)
            //{
            //    Visit(in value, in script);
            //}
        }
        protected virtual void Visit(in List<ColumnDefinition> columns, in StringBuilder script)
        {
            ColumnDefinition column;

            for (int i = 0; i < columns.Count; i++)
            {
                column = columns[i];

                if (i > 0) { script.Append(", "); }

                script.Append(column.Name);

                //if (!string.IsNullOrEmpty(column.Alias))
                //{
                //    script.Append(" AS ").Append(column.Alias);
                //}
            }
        }
        protected virtual void Visit(in ColumnDefinition column, in StringBuilder script, in string tableAlias)
        {
            if (!string.IsNullOrEmpty(tableAlias))
            {
                script.Append(tableAlias).Append('.');
            }

            script.Append(column.Name);
        }
        protected virtual void Visit(in PropertyDefinition property, in StringBuilder script, in string tableAlias)
        {
            List<ColumnDefinition> columns = property.Columns
                .OrderBy((column) => { return column.Purpose; })
                .ToList();

            ColumnDefinition column;

            for (int i = 0; i < columns.Count; i++)
            {
                column = columns[i];

                if (i > 0)
                {
                    script.Append(", ");
                }

                Visit(in column, in script, in tableAlias);
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
            script.Append(node.Identifier);
        }
        protected virtual void Visit(in MemberAccessExpression node, in StringBuilder script)
        {
            script.Append(node.GetDbParameterName());
        }
        
        //protected virtual void Visit(in EnumValue node, in StringBuilder script)
        //{
        //    script.Append($"0x{ParserHelper.GetUuidHexLiteral(node.Uuid)}");
        //}

        protected virtual void Visit(in FunctionExpression node, in StringBuilder script)
        {
            if (node.Token == Token.UDF)
            {
                throw new InvalidOperationException($"Invalid function name: {node.Name}");
            }

            string name = node.Name.ToUpperInvariant();

            script.Append(node.Name);

            if (node.Token != Token.EXISTS)
            {
                script.Append('('); //NOTE: EXISTS function has one parameter - TableExpression
            }

            if (node.Token == Token.COUNT && node.Modifier == Token.DISTINCT)
            {
                script.Append("DISTINCT ");
            }

            SyntaxNode expression;

            for (int i = 0; i < node.Parameters.Count; i++)
            {
                expression = node.Parameters[i];

                if (i > 0) { script.Append(", "); }
                
                Visit(in expression, in script);
            }

            if (node.Token != Token.EXISTS)
            {
                script.Append(')'); //NOTE: EXISTS function has one parameter - TableExpression
            }

            if (node.Over is not null)
            {
                script.Append(' ');

                Visit(node.Over, in script);
            }
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
        #endregion

        #region "CREATE DROP APPLY REVOKE SEQUENCE"
        public abstract void Visit(in CreateSequenceStatement node, in StringBuilder script);
        protected virtual void Visit(in DropSequenceStatement node, in StringBuilder script)
        {
            script.Append("DROP SEQUENCE ").Append(node.Identifier).AppendLine(";");
        }
        public abstract void Visit(in ApplySequenceStatement node, in StringBuilder script);
        public abstract void Visit(in RevokeSequenceStatement node, in StringBuilder script);
        #endregion
    }
}