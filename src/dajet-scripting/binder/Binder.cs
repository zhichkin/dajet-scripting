using DaJet.Scripting.Model;
using DaJet.TypeSystem;

namespace DaJet.Scripting
{
    public sealed class Binder
    {
        private Scope _scope;
        private List<string> _errors;
        private ISchemaProvider _schema;
        public bool TryBind(in Script script, in ISchemaProvider schema, out List<string> errors)
        {
            ArgumentNullException.ThrowIfNull(script, nameof(script));

            _schema = schema;

            _errors = new List<string>();

            try
            {
                Bind(in script);
            }
            catch (Exception exception)
            {
                _errors.Add(ExceptionHelper.GetErrorMessage(exception));
            }

            errors = _errors;
            _errors = null;

            return (errors.Count == 0);
        }
        private void RegisterBindingError(Token token, string identifier)
        {
            _errors.Add($"Failed to bind [{token}: {identifier}]");
        }
        private void Bind(in SyntaxNode node)
        {
            if (node is null) { return; }

            if (node is Script script) { Bind(in script); } // ? EXECUTE вложенный скрипт

            else if (node is StatementBlock statement_block) { Bind(in statement_block); } // ?

            else if (node is DefineStatement define) { Bind(in define); }
            else if (node is DeclareStatement declare) { Bind(in declare); }
            else if (node is VariableReference variable) { Bind(in variable); }
            else if (node is MemberAccessExpression member) { Bind(in member); }

            else if (node is UseStatement use) { Bind(in use); }

            else if (node is SelectStatement select_statement) { Bind(in select_statement); }
            else if (node is CommonTableExpression cte) { Bind(in cte); }
            else if (node is SelectExpression select) { Bind(in select); }
            else if (node is TableExpression derived) { Bind(in derived); }
            else if (node is TableJoinOperator join) { Bind(in join); }
            else if (node is TableUnionOperator union) { Bind(in union); }
            else if (node is TableReference table) { Bind(in table); }
            
            //else if (node is TemporaryTableExpression temporary_table) { Bind(in temporary_table); }

            else if (node is FromClause from) { Bind(in from); }
            else if (node is IntoClause into) { Bind(in into); }

            else if (node is TopClause top) { Bind(in top); }

            else if (node is CaseExpression case_when_then_else) { Bind(in case_when_then_else); }
            else if (node is WhenClause when) { Bind(in when); }
            else if (node is FunctionExpression function) { Bind(in function); }
            else if (node is OverClause over) { Bind(in over); }
            else if (node is PartitionClause partition) { Bind(in partition); }
            else if (node is WindowFrame frame) { Bind(in frame); }

            else if (node is OnClause join_on) { Bind(in join_on); }
            else if (node is WhereClause where) { Bind(in where); }
            else if (node is ValuesExpression values) { Bind(in values); }
            else if (node is GroupClause group_by) { Bind(in group_by); }
            else if (node is HavingClause having) { Bind(in having); }
            else if (node is OrderClause order_by) { Bind(in order_by); }
            else if (node is OrderExpression order_expression) { Bind(in order_expression); }

            //else if (node is StarExpression star) { Bind(in star); }
            else if (node is ColumnExpression column) { Bind(in column); }
            else if (node is ColumnReference reference) { Bind(in reference); }

            else if (node is GroupOperator group) { Bind(in group); }
            else if (node is UnaryOperator unary) { Bind(in unary); } // NOT | Minus
            else if (node is BinaryOperator binary) { Bind(in binary); } // AND | OR
            else if (node is MultiplyOperator multiply) { Bind(in multiply); }
            else if (node is AdditionOperator addition) { Bind(in addition); }
            else if (node is ComparisonOperator comparison) { Bind(in comparison); }

            //else if (node is InsertStatement insert) { Bind(in insert); }
            //else if (node is UpdateStatement update) { Bind(in update); }
            //else if (node is UpsertStatement upsert) { Bind(in upsert); }
            //else if (node is DeleteStatement delete) { Bind(in delete); }
            //else if (node is SetClause set_clause) { Bind(in set_clause); }
            //else if (node is SetExpression set_expression) { Bind(in set_expression); }
            //else if (node is OutputClause output) { Bind(in output); }

            //else if (node is ConsumeStatement consume_statement) { Bind(in consume_statement); }
            //else if (node is ProduceStatement produce_statement) { Bind(in produce_statement); }
            //else if (node is RequestStatement request_statement) { Bind(in request_statement); }
            //else if (node is ImportStatement import_statement) { Bind(in import_statement); }
            //else if (node is ForStatement for_each) { Bind(in for_each); }
            //else if (node is CreateTypeStatement udt) { Bind(in udt); }
            //else if (node is ApplySequenceStatement apply_sequence) { Bind(in apply_sequence); }
            //else if (node is RevokeSequenceStatement revoke_sequence) { Bind(in revoke_sequence); }
            //else if (node is AssignmentStatement assignment) { Bind(in assignment); }
            //else if (node is CaseStatement case_statement) { Bind(in case_statement); }
            //else if (node is IfStatement if_statement) { Bind(in if_statement); }
            //else if (node is TryStatement try_statement) { Bind(in try_statement); }
            //else if (node is WhileStatement while_statement) { Bind(in while_statement); }
            //else if (node is SleepStatement sleep_statement) { Bind(in sleep_statement); }
            //else if (node is ReturnStatement return_statement) { Bind(in return_statement); }
            //else if (node is ThrowStatement throw_statement) { Bind(in throw_statement); }
            //else if (node is PrintStatement print) { Bind(in print); }
            //else if (node is ProcessStatement process) { Bind(in process); }
            //else if (node is ExecuteStatement execute) { Bind(in execute); } // nothing to bind
            //else if (node is WaitStatement wait) { Bind(in wait); }
            //else if (node is ModifyStatement modify) { Bind(in modify); } // nothing to bind
        }

        private void Bind(in Script node)
        {
            if (_scope is null) // root Script
            {
                _scope = new Scope() { Owner = node };
            }
            else // nested Script - EXECUTE statement
            {
                _scope = _scope.OpenScope(node); 
            }

            //TODO: process IMPORT statements

            foreach (SyntaxNode statement in node.Statements)
            {
                Bind(in statement);
            }

            _scope = _scope.CloseScope();
        }
        private void Bind(in StatementBlock node)
        {
            if (node is null) { return; }

            foreach (SyntaxNode statement in node.Statements)
            {
                Bind(in statement);
            }
        }
        
        ///<summary>DEFINE statement</summary>
        private void Bind(in DefineStatement node)
        {
            EntityDefinition schema = new()
            {
                Name = node.Identifier
            };

            DefineProperty property;
            EntityDefinition entity;

            for (int i = 0; i < node.Properties.Count; i++)
            {
                property = node.Properties[i];

                if (property.Type.IsArray)
                {
                    entity = _scope.GetSchema(property.Schema);

                    if (entity is null)
                    {
                        RegisterBindingError(node.Token, node.Identifier);
                    }

                    schema.Entities.Add(entity);
                }
                else
                {
                    schema.Properties.Add(new PropertyDefinition()
                    {
                        Name = property.Name,
                        Type = property.Type,
                        Purpose = PropertyPurpose.Property
                    });
                }
            }

            _scope.Types.Add(schema.Name, schema);
        }
        private void Bind(in DeclareStatement node)
        {
            _scope.Variables.Add(node.Identifier, node);

            if (!string.IsNullOrEmpty(node.Schema))
            {
                node.Binding = _scope.GetSchema(node.Schema);

                if (node.Binding is null)
                {
                    RegisterBindingError(node.Token, node.Schema);
                }
            }
        }
        private void Bind(in VariableReference node)
        {
            node.Binding = _scope.GetVariable(node.Identifier);

            if (node.Binding is null)
            {
                RegisterBindingError(node.Token, node.Identifier);
            }
        }
        private void Bind(in MemberAccessExpression node)
        {
            List<string> members = node.GetAccessMembers(node.Identifier);

            string target = members[0];

            node.Binding = _scope.GetVariable(target);

            if (node.Binding is null)
            {
                RegisterBindingError(node.Token, node.Identifier);
            }
        }

        #region "ARITHMETIC AND LOGICAL OPERATORS"
        private void Bind(in GroupOperator node)
        {
            Bind(node.Expression);
        }
        private void Bind(in UnaryOperator node)
        {
            Bind(node.Expression);
        }
        private void Bind(in BinaryOperator node)
        {
            Bind(node.Expression1);
            Bind(node.Expression2);
        }
        private void Bind(in AdditionOperator node)
        {
            Bind(node.Expression1);
            Bind(node.Expression2);
        }
        private void Bind(in MultiplyOperator node)
        {
            Bind(node.Expression1);
            Bind(node.Expression2);
        }
        private void Bind(in ComparisonOperator node)
        {
            Bind(node.Expression1);
            Bind(node.Expression2);
        }
        #endregion

        private void Bind(in UseStatement node)
        {
            _scope = _scope.OpenScope(node);

            foreach (SyntaxNode statement in node.Statements.Statements)
            {
                Bind(in statement);
            }

            _scope = _scope.CloseScope();
        }
        private void Bind(in SelectStatement node)
        {
            //ValidateStatement(in node); APPEND operator

            _scope = _scope.OpenScope(node);

            if (node.CommonTables is not null)
            {
                Bind(node.CommonTables);
            }

            Bind(node.Expression); //NOTE: SelectExpression | TableUnionOperator

            // Define and apply schema to object or array variable

            IntoClause into = node.GetIntoClause();

            if (into.Value is VariableReference variable &&
                variable.Binding is DeclareStatement declare)
            {
                if (declare.Binding is null)
                {
                    EntityDefinition schema = DataMapper.InferSchema(in node);
                    schema.Name = declare.Identifier;
                    declare.Binding = schema;
                    declare.Schema = schema.Name;
                }
            }

            _scope = _scope.CloseScope();
        }

        #region "TABLE BINDING"
        private void Bind(in CommonTableExpression node)
        {
            //NOTE: common table expression can be recursive and reference itself

            if (node.Next is not null)
            {
                Bind(node.Next);
            }

            _scope.Tables.Add(node.Name, node); // join current statement scope

            Bind(node.Expression); //NOTE: { SelectExpression | TableUnionOperator | INSERT | UPDATE | DELETE }
        }
        private void Bind(in FromClause node) { Bind(node.Expression); }
        private void Bind(in SelectExpression node)
        {
            _scope = _scope.OpenScope(node);

            if (node.From is not null) { Bind(node.From); }

            for (int i = 0; i < node.Columns.Count; i++)
            {
                Bind(node.Columns[i]);
            }

            if (node.Top is not null) { Bind(node.Top); }
            if (node.Into is not null) { Bind(node.Into); }
            if (node.Where is not null) { Bind(node.Where); }
            if (node.Order is not null) { Bind(node.Order); }
            if (node.Group is not null) { Bind(node.Group); }
            if (node.Having is not null) { Bind(node.Having); }

            //if (node.From is not null)
            //{
            //    var appends = new AppendOperatorExtractor().Extract(node.From);

            //    foreach (var append in appends)
            //    {
            //        BindAppend(append); //FIXME: recursive multiple times binding when nested APPEND
            //    }
            //}

            _scope = _scope.CloseScope();
        }
        private void Bind(in TableExpression node) //NOTE: this is subquery
        {
            Bind(node.Expression); //NOTE: SelectExpression - opens new scope inside current

            if (!string.IsNullOrWhiteSpace(node.Alias))
            {
                _scope.Aliases.Add(node.Alias, node); // join current SelectExpression scope
            }
        }
        private void Bind(in TableJoinOperator node)
        {
            Bind(node.Expression1); //NOTE: { TableReference | TableExpression | TableJoinOperator }

            ////NOTE: delay binding till INTO clause has been binded
            //if (node.Token == Token.APPEND) { return; }

            Bind(node.Expression2);

            if (node.On is not null) { Bind(node.On); }
        }
        private void Bind(in TableUnionOperator node)
        {
            if (node.Expression1 is SelectExpression select1)
            {
                Bind(in select1);
            }
            else if (node.Expression1 is TableUnionOperator union1)
            {
                Bind(in union1);
            }

            if (node.Expression2 is SelectExpression select2)
            {
                Bind(in select2);
            }
            else if (node.Expression2 is TableUnionOperator union2)
            {
                Bind(in union2);
            }

            //NOTE: UNION root order clause
            if (node.Order is OrderClause order)
            {
                for (int i = 0; i < order.Expressions.Count; i++)
                {
                    if (order.Expressions[i].Expression is ColumnReference column)
                    {
                        column.GetColumnIdentifiers(out _, out string columnName);

                        BindColumn(in node, in columnName, in column);

                        if (column.Binding is null)
                        {
                            RegisterBindingError(column.Token, column.Identifier);
                        }
                    }
                }

                if (order.Offset is not null)
                {
                    Bind(order.Offset);

                    if (order.Fetch is not null)
                    {
                        Bind(order.Fetch);
                    }
                }
            }
        }
        private void Bind(in TableReference node)
        {
            // 1. try bind common table expression or temporary table
            node.Binding = _scope.GetTableBinding(node.Identifier);

            //// 2. try bind user-defined type (table-valued parameter)
            //// see DeclareStatement binding to UserDefinedType
            //if (node.Binding is null)
            //{
            //    node.Binding = _scope.GetVariableBinding(node.Identifier);
            //}

            // 3. try bind database schema table
            if (node.Binding is null)
            {
                Scope scope = _scope.Ancestor<UseStatement>();

                if (scope is not null)
                {
                    if (scope.Owner is UseStatement use)
                    {
                        EntityDefinition entity = _schema.GetSchema(use.Uri, node.Identifier);

                        if (entity is not null)
                        {
                            node.Binding = entity; // successful binding
                        }
                    }
                }
            }

            if (node.Binding is null)
            {
                RegisterBindingError(node.Token, node.Identifier);
            }
            else // successful binding
            {
                if (!string.IsNullOrWhiteSpace(node.Alias))
                {
                    _scope.Aliases.Add(node.Alias, node.Binding); // join current SelectExpression scope
                }
                else
                {
                    _scope.Aliases.Add(node.Identifier, node.Binding);
                }
            }
        }

        //private void BindAppend(in TableJoinOperator node)
        //{
        //    if (node.Token == Token.APPEND)
        //    {
        //        Bind(node.Expression2);
        //    }
        //}

        #endregion

        #region "SELECT CLAUSES"
        private void Bind(in TopClause node)
        {
            Bind(node.Expression);
        }
        private void Bind(in WhereClause node)
        {
            Bind(node.Expression);
        }
        private void Bind(in GroupClause node)
        {
            for (int i = 0; i < node.Expressions.Count; i++)
            {
                Bind(node.Expressions[i]);
            }
        }
        private void Bind(in HavingClause node)
        {
            Bind(node.Expression);
        }
        private void Bind(in OnClause node)
        {
            Bind(node.Expression);
        }
        private void Bind(in OrderClause node)
        {
            for (int i = 0; i < node.Expressions.Count; i++)
            {
                Bind(node.Expressions[i]);
            }

            if (node.Offset is not null)
            {
                Bind(node.Offset);

                if (node.Fetch is not null)
                {
                    Bind(node.Fetch);
                }
            }
        }
        private void Bind(in OrderExpression node)
        {
            Bind(node.Expression);
        }
        private void Bind(in CaseExpression node)
        {
            foreach (WhenClause when in node.CASE)
            {
                Bind(in when);
            }

            if (node.ELSE is not null)
            {
                Bind(node.ELSE);
            }
        }
        private void Bind(in WhenClause node)
        {
            Bind(node.WHEN);
            Bind(node.THEN);
        }
        private void Bind(in FunctionExpression node)
        {
            for (int i = 0; i < node.Parameters.Count; i++)
            {
                Bind(node.Parameters[i]);
            }

            if (node.Over is not null)
            {
                Bind(node.Over);
            }
        }
        private void Bind(in OverClause node)
        {
            if (node.Partition is not null)
            {
                Bind(node.Partition);
            }
            if (node.Order is not null)
            {
                Bind(node.Order);
            }
            if (node.Preceding is not null || node.Following is not null)
            {
                if (node.Preceding is not null && node.Following is not null)
                {
                    Bind(node.Preceding);
                    Bind(node.Following);
                }
                else if (node.Preceding is not null)
                {
                    Bind(node.Preceding);
                }
            }
        }
        private void Bind(in WindowFrame node) { }
        private void Bind(in PartitionClause node)
        {
            for (int i = 0; i < node.Columns.Count; i++)
            {
                Bind(node.Columns[i]);
            }
        }
        private void Bind(in IntoClause node)
        {
            //NOTE: INTO columns are derived from the host SELECT expression
            //NOTE: INTO columns are bound already !!!

            if (node.Table is not null)
            {
                //CreateTableVariable(in node); ?
            }
            else
            {
                Bind(node.Value);
            }
        }
        private void Bind(in ValuesExpression node)
        {
            foreach (SyntaxNode value in node.Values)
            {
                Bind(in value);
            }
        }
        #endregion

        #region "COLUMN BINDING"
        private void Bind(in StarExpression node) { /* TODO: implement transformer into column expressions */ }
        private void Bind(in ColumnExpression node)
        {
            Bind(node.Expression);

            if (node.Expression is ColumnReference column)
            {
                // При генерации SQL ColumnReference должен использовать Alias
                column.Alias = node.Alias;

                // Поднимаем наверх источник данных для ColumnExpression
                if (column.Binding is PropertyDefinition property)
                {
                    node.Source = property; // Прямой источник данных
                }
                else if (column.Binding is ColumnExpression derived)
                {
                    node.Source = derived.Source; // Наследуемый по ссылочной иерархии источник данных
                }
            }
        }
        private void Bind(in ColumnReference node)
        {
            //if (!TryBindEnumValue(in node))
            //{
            BindColumn(in node);
            //}

            if (node.Binding is null)
            {
                RegisterBindingError(node.Token, node.Identifier);
            }
            else // successful binding
            {
                //TODO: find all ambiguous names and report error
                _ = _scope.Columns.TryAdd(node.Identifier, node.Binding);
            }
        }
        
        //private bool TryBindEnumValue(in ColumnReference column)
        //{
        //    string[] identifiers = column.Identifier.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        //    if (identifiers is null || identifiers.Length != 3) { return false; }

        //    if (_schema is not null && _schema.TryGetEnumValue(column.Identifier, out EnumValue value) && value is not null)
        //    {
        //        column.Binding = value;
        //        column.Token = Token.Enumeration;

        //        return true;
        //    }

        //    return false;
        //}

        private void BindColumn(in ColumnReference column)
        {
            column.GetColumnIdentifiers(out string tableAlias, out string columnName);

            if (_scope.TryGetTableByAlias(in tableAlias, out object table))
            {
                BindColumn(in table, in columnName, in column);
            }
        }
        private void BindColumn(in object source, in string identifier, in ColumnReference column)
        {
            if (source is CommonTableExpression common)
            {
                BindColumn(in common, in identifier, in column);
            }
            else if (source is EntityDefinition entity)
            {
                BindColumn(in entity, in identifier, in column);
            }
            else if (source is TableExpression derived)
            {
                BindColumn(in derived, in identifier, in column);
            }
            else if (source is TableVariableExpression variable)
            {
                BindColumn(in variable, in identifier, in column);
            }
            else if (source is TemporaryTableExpression temporary)
            {
                BindColumn(in temporary, in identifier, in column);
            }
            else if (source is TableUnionOperator union) // ORDER clause column of the UNION operator 
            {
                BindColumn(in union, in identifier, in column);
            }
        }
        private void BindColumn(in TableExpression table, in string identifier, in ColumnReference column)
        {
            if (table.Expression is SelectExpression select)
            {
                BindColumn(in select, in identifier, in column);
            }
            else if (table.Expression is TableUnionOperator union)
            {
                BindColumn(in union, in identifier, in column);
            }
        }
        private void BindColumn(in TableUnionOperator union, in string identifier, in ColumnReference column)
        {
            if (union.Expression1 is SelectExpression select1)
            {
                BindColumn(in select1, in identifier, in column);
            }
            else if (union.Expression2 is SelectExpression select2)
            {
                BindColumn(in select2, in identifier, in column); // ?
            }
        }
        private void BindColumn(in CommonTableExpression table, in string identifier, in ColumnReference column)
        {
            if (table.Expression is SelectExpression select)
            {
                BindColumn(in select, in identifier, in column);
            }
            else if (table.Expression is TableUnionOperator union)
            {
                BindColumn(in union, in identifier, in column);
            }
            //else if (table.Expression is InsertStatement insert)
            //{
            //    BindColumn(in insert, in identifier, in column);
            //}
            //else if (table.Expression is UpdateStatement update)
            //{
            //    BindColumn(in update, in identifier, in column);
            //}
            //else if (table.Expression is DeleteStatement delete)
            //{
            //    BindColumn(in delete, in identifier, in column);
            //}
        }
        private void BindColumn(in TableVariableExpression table, in string identifier, in ColumnReference column)
        {
            if (table.Expression is SelectExpression select)
            {
                BindColumn(in select, in identifier, in column);
            }
            else if (table.Expression is TableUnionOperator union)
            {
                BindColumn(in union, in identifier, in column);
            }
        }
        private void BindColumn(in TemporaryTableExpression table, in string identifier, in ColumnReference column)
        {
            if (table.Expression is SelectExpression select)
            {
                BindColumn(in select, in identifier, in column);
            }
            else if (table.Expression is TableUnionOperator union)
            {
                BindColumn(in union, in identifier, in column);
            }
        }
        private void BindColumn(in SelectExpression table, in string identifier, in ColumnReference column)
        {
            string columnName = string.Empty;

            foreach (ColumnExpression expression in table.Columns)
            {
                if (!string.IsNullOrEmpty(expression.Alias))
                {
                    columnName = expression.Alias;
                }
                else if (expression.Expression is ColumnReference reference)
                {
                    reference.GetColumnIdentifiers(out string _, out columnName);
                }

                if (columnName == identifier)
                {
                    column.Binding = expression; return;
                }
            }
        }
        private void BindColumn(in EntityDefinition entity, in string identifier, in ColumnReference column)
        {
            foreach (PropertyDefinition property in entity.Properties)
            {
                if (property.Name == identifier)
                {
                    column.Binding = property; return;
                }
            }
        }
        
        //private void BindColumn(in OutputClause output, in string identifier, in ColumnReference column)
        //{
        //    if (output is null) { return; }

        //    string columnName = string.Empty;

        //    foreach (ColumnExpression expression in output.Columns)
        //    {
        //        if (!string.IsNullOrEmpty(expression.Alias))
        //        {
        //            columnName = expression.Alias;
        //        }
        //        else if (expression.Expression is ColumnReference reference)
        //        {
        //            reference.GetColumnIdentifiers(out string _, out columnName);
        //        }

        //        if (columnName == identifier)
        //        {
        //            column.Binding = expression; return; // success
        //        }
        //    }
        //}
        //private void BindColumn(in InsertStatement table, in string identifier, in ColumnReference column)
        //{
        //    if (table.Output is not null) { BindColumn(table.Output, in identifier, in column); }
        //}
        //private void BindColumn(in UpdateStatement table, in string identifier, in ColumnReference column)
        //{
        //    if (table.Output is not null) { BindColumn(table.Output, in identifier, in column); }
        //}
        //private void BindColumn(in DeleteStatement table, in string identifier, in ColumnReference column)
        //{
        //    if (table.Output is not null) { BindColumn(table.Output, in identifier, in column); }
        //}

        #endregion
    }
}