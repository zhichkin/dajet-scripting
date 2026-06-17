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
            ArgumentNullException.ThrowIfNull(schema, nameof(schema));

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

        #region "Binding and syntax errors"
        private void RegisterBindingError(Token token, string identifier)
        {
            _errors.Add($"Failed to bind [{token}: {identifier}]");
        }
        private void DuplicateTableIdentifierError(Type type, string identifier)
        {
            _errors.Add($"Duplicate table name or alias [{type.Name}: {identifier}]");
        }
        private void DuplicateColumnAliasError(Type table, string alias)
        {
            _errors.Add($"Duplicate SELECT column alias [{table.Name}: {alias}]");
        }
        private void ColumnAliasIsNotDefinedError()
        {
            _errors.Add("[SELECT] expression column alias must be defined");
        }
        private void AmbiguousColumnNameError(string name)
        {
            _errors.Add($"Ambiguous сolumn name [{name}]");
        }
        private void TableAliasIsNotFound(string alias, string column)
        {
            _errors.Add($"Table alias [{alias}] is not found for column identifier [{column}]");
        }
        #endregion

        private void Bind(in SyntaxNode node)
        {
            if (node is null) { return; }

            if (node is Script script) { Bind(in script); } // ? EXECUTE вложенный скрипт

            else if (node is TypeReference type) { Bind(in type); }
            else if (node is DeclareStatement declare) { Bind(in declare); }
            else if (node is VariableReference variable) { Bind(in variable); }
            else if (node is MemberAccessExpression member) { Bind(in member); }
            else if (node is AssignmentOperator set) { Bind(in set); }

            else if (node is UseStatement use) { Bind(in use); }
            
            else if (node is SelectStatement select_statement) { Bind(in select_statement); }
            else if (node is CommonTableExpression cte) { Bind(in cte); }
            else if (node is SelectExpression select) { Bind(in select); }
            else if (node is TableExpression derived) { Bind(in derived); }
            else if (node is TableJoinOperator join) { Bind(in join); }
            else if (node is TableUnionOperator union) { Bind(in union); }
            else if (node is TableReference table) { Bind(in table); }
            
            //else if (node is TemporaryTableExpression temporary_table) { Bind(in temporary_table); }

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

            else if (node is StarExpression star) { Bind(in star); }
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
            //else if (node is AssignmentOperator assignment) { Bind(in assign); }
            //else if (node is CaseStatement case_statement) { Bind(in case_statement); }
            //else if (node is IfStatement if_statement) { Bind(in if_statement); }
            //else if (node is TryStatement try_statement) { Bind(in try_statement); }
            //else if (node is WhileStatement while_statement) { Bind(in while_statement); }
            //else if (node is SleepStatement sleep_statement) { Bind(in sleep_statement); }
            //else if (node is ThrowStatement throw_statement) { Bind(in throw_statement); }
            //else if (node is ProcessStatement process) { Bind(in process); }
            //else if (node is ExecuteStatement execute) { Bind(in execute); } // nothing to bind
            //else if (node is WaitStatement wait) { Bind(in wait); }
            //else if (node is ModifyStatement modify) { Bind(in modify); } // nothing to bind
            else if (node is PrintStatement print) { Bind(in print); }
            else if (node is ReturnStatement return_statement) { Bind(in return_statement); }
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
        private void Bind(in TypeReference node)
        {
            if (!node.Type.IsUndefined)
            {
                return; // Простой тип данных
            }

            // Объект метаданных (базы данных)

            Scope scope = _scope.Ancestor<UseStatement>();

            if (scope is not null)
            {
                if (scope.Owner is UseStatement use)
                {
                    MetadataEntry entity = _schema.GetEntry(use.Source, node.Schema);

                    if (entity is not null)
                    {
                        node.Binding = entity;
                    }
                }
            }

            if (node.Binding is null)
            {
                RegisterBindingError(node.Token, node.Schema);
            }
        }
        private void Bind(in DeclareStatement node)
        {
            _scope.Variables.Add(node.Identifier, node);

            if (!string.IsNullOrEmpty(node.Schema))
            {
                if (!SchemaRegistry.TryGet(node.Schema, out _))
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
            List<string> members = node.GetAccessMembers();

            string target = members[0];

            node.Binding = _scope.GetVariable(target);

            if (node.Binding is null)
            {
                RegisterBindingError(node.Token, node.Identifier);
            }
        }
        private void Bind(in AssignmentOperator node)
        {
            if (node.Target is not null)
            {
                Bind(node.Target);
            }

            if (node.Initializer is not null)
            {
                Bind(node.Initializer);
            }

            if (node.Target is MemberAccessExpression member &&
                member.Binding is DeclareStatement declare)
            {
                if (declare.Binding is DefineStatement binding)
                {
                    List<string> members = member.GetAccessMembers();

                    string memberName = members[1];

                    DefineProperty property = binding.GetPropertyByName(memberName);

                    if (property is null && node.Initializer is not null) // Добавляем свойство в анонимную схему, если оно отсутствует
                    {
                        DataType type = node.Initializer.InferType(); // Выводим тип свойства

                        if (type.IsObject || type.IsArray)
                        {
                            if (node.Initializer is VariableReference variable)
                            {
                                if (variable.Binding is DeclareStatement source)
                                {
                                    if (string.IsNullOrEmpty(source.Schema))
                                    {
                                        binding.Properties.Add(new DefineProperty()
                                        {
                                            Name = memberName,
                                            Schema = $"AnonymousDataSchema.{source.Identifier.TrimStart('@')}",
                                            Type = source.Type
                                        });
                                    }
                                    else
                                    {
                                        binding.Properties.Add(new DefineProperty()
                                        {
                                            Name = memberName,
                                            Schema = source.Schema,
                                            Type = source.Type
                                        });
                                    }
                                }
                            }
                            else if (node.Initializer is MemberAccessExpression memberAccess)
                            {
                                //TODO:
                            }
                        }
                    }
                }
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

            foreach (SyntaxNode statement in node.Statements)
            {
                Bind(in statement);
            }

            _scope = _scope.CloseScope();
        }
        private void Bind(in SelectStatement node)
        {
            _scope = _scope.OpenScope(node);

            if (node.CommonTables is not null)
            {
                Bind(node.CommonTables);
            }

            Bind(node.Expression); //NOTE: SelectExpression | TableUnionOperator

            BindIntoClause(in node);

            _scope = _scope.CloseScope();
        }
        private void BindIntoClause(in SelectStatement select)
        {
            //NOTE: INTO columns are derived from the host SELECT expression
            //NOTE: SELECT columns are bound already

            IntoClause into = select.GetIntoClause();

            if (into is null) { return; }

            if (into.Table is not null) 
            {
                //TODO: temporary table
            }
            else
            {
                Bind(into.Value); // script variable
            }

            // Define and apply schema to object or array variable

            if (into.Value is VariableReference variable &&
                variable.Binding is DeclareStatement declare)
            {
                DefineStatement schema = select.InferSchema();

                if (declare.Binding is DefineStatement binding)
                {
                    foreach (DefineProperty property in schema.Properties)
                    {
                        if (binding.GetPropertyByName(property.Name) is null)
                        {
                            binding.Properties.Add(property); // extend anonymous schema
                        }
                    }
                }
                else // this is the first time of binding variable to SELECT INTO output
                {
                    // Script processor property name - see compiler
                    // Variable name is used in case schema is not defined
                    // New type is compiled and added to AnonymousDataSchema
                    
                    schema.Identifier = declare.Identifier;

                    declare.Binding = schema;
                }
            }
        }
        private void BindOrderByClause(in SelectExpression select, in Dictionary<string, ColumnExpression> aliases)
        {
            if (select.Order is not OrderClause order)
            {
                return;
            }

            OrderExpression expression;

            List<OrderExpression> expressions = order.Expressions;

            if (expressions is null)
            {
                return;
            }

            int count = expressions.Count;

            for (int i = 0; i < count; i++)
            {
                expression = expressions[i];

                if (expression.Expression is ColumnReference column)
                {
                    column.GetColumnIdentifiers(out string tableAlias, out string columnName);

                    if (string.IsNullOrEmpty(tableAlias))
                    {
                        // Check for special SELECT ... ORDER BY <alias> case
                        if (aliases.TryGetValue(columnName, out ColumnExpression property))
                        {
                            column.Binding = property; // successful binding
                        }
                        else
                        {
                            Bind(in column);
                        }
                    }
                    else
                    {
                        Bind(in column);
                    }
                }
                else
                {
                    Bind(in expression);
                }
            }
        }

        #region "TABLE BINDING"
        private void Bind(in CommonTableExpression node)
        {
            //NOTE: common table expression can be recursive and reference itself

            if (node.Next is not null)
            {
                Bind(node.Next);
            }

            // Join current statement scope

            if (!_scope.Tables.TryAdd(node.Name, node))
            {
                DuplicateTableIdentifierError(node.GetType(), node.Name);
            }

            Bind(node.Expression); //NOTE: { SelectExpression | TableUnionOperator | INSERT | UPDATE | DELETE }
        }
        private void Bind(in FromClause node) { Bind(node.Expression); }
        private void Bind(in SelectExpression node)
        {
            _scope = _scope.OpenScope(node);
            
            if (node.Top is not null) { Bind(node.Top); }
            if (node.From is not null) { Bind(node.From); }
            if (node.Where is not null) { Bind(node.Where); }
            if (node.Group is not null) { Bind(node.Group); }
            if (node.Having is not null) { Bind(node.Having); }

            ColumnExpression column;
            List<ColumnExpression> columns = node.Columns;
            int count = columns.Count;
            Dictionary<string, ColumnExpression> aliases = new(count);

            for (int i = 0; i < count; i++)
            {
                column = columns[i];

                Bind(in column);

                if (!node.IsUnionSubordinate)
                {
                    if (string.IsNullOrEmpty(column.Alias))
                    {
                        if (column.Expression is not ColumnReference reference)
                        {
                            ColumnAliasIsNotDefinedError(); // Выражения должны иметь синоним (имя свойства)
                        }
                        else
                        {
                            if (reference.Binding is PropertyDefinition) // Это колонка таблицы базы данных
                            {
                                reference.GetColumnIdentifiers(out _, out string columnName);

                                column.Alias = columnName; // Неявная нормализация имён свойств схемы данных
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(column.Alias))
                    {
                        // Проверка на дублирование имён свойств и подготовка синонимов для ORDER BY

                        if (!aliases.TryAdd(column.Alias, column))
                        {
                            Type type = _scope.Parent?.Owner?.GetType();

                            DuplicateColumnAliasError(type is null ? node.GetType() : type, column.Alias);
                        }
                    }
                }
            }

            if (node.Order is not null)
            {
                BindOrderByClause(in node, in aliases);
            }

            _scope = _scope.CloseScope();
        }
        private void Bind(in TableExpression node) //NOTE: this is subquery
        {
            Bind(node.Expression); //NOTE: SelectExpression - opens new scope inside current

            if (!string.IsNullOrWhiteSpace(node.Alias))
            {
                if (!_scope.Tables.TryAdd(node.Alias, node)) // join current SelectExpression scope
                {
                    DuplicateTableIdentifierError(node.GetType(), node.Alias);
                }
            }
        }
        private void Bind(in TableJoinOperator node)
        {
            Bind(node.Expression1); //NOTE: { TableReference | TableExpression | TableJoinOperator }

            Bind(node.Expression2);

            if (node.On is not null) { Bind(node.On); }
        }
        private void Bind(in TableUnionOperator node)
        {
            if (node.Expression1 is SelectExpression select1)
            {
                Bind(in select1);
            }

            if (node.Expression2 is SelectExpression select2)
            {
                Bind(in select2);
            }
            else if (node.Expression2 is TableUnionOperator union)
            {
                Bind(in union);
            }

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
            node.Binding = _scope.GetTableBinding(node.Identifier);
            
            if (node.Binding is null) // Try bind database schema table
            {
                Scope scope = _scope.Ancestor<UseStatement>();

                if (scope is not null)
                {
                    if (scope.Owner is UseStatement use)
                    {
                        EntityDefinition entity = _schema.GetSchema(use.Source, node.Identifier);

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
            else // successful binding - join current SelectExpression scope
            {
                if (!string.IsNullOrWhiteSpace(node.Alias))
                {
                    if (!_scope.Tables.TryAdd(node.Alias, node.Binding))
                    {
                        DuplicateTableIdentifierError(node.GetType(), node.Alias);
                    }
                }
                else
                {
                    if (!_scope.Tables.TryAdd(node.Identifier, node.Binding))
                    {
                        DuplicateTableIdentifierError(node.GetType(), node.Identifier);
                    }
                }
            }
        }

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
        private void Bind(in ValuesExpression node)
        {
            foreach (SyntaxNode value in node.Values)
            {
                Bind(in value);
            }
        }
        #endregion

        #region "COLUMN BINDING"
        private void Bind(in StarExpression node)
        {
            /* TODO: implement transformer into column expressions */
        }
        private void Bind(in ColumnExpression node)
        {
            Bind(node.Expression);

            if (node.Expression is ColumnReference column)
            {
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
            //    BindColumn(in node);
            //}

            //NOTE: ColumnReference can be bound to either PropertyDefinition (direct) or ColumnExpression (derived)

            if (_scope.Columns.TryGetValue(node.Identifier, out object binding))
            {
                node.Binding = binding; // Сolumn identifier is already bound in the current scope
            }
            else
            {
                BindColumn(in node);

                if (node.Binding is not null) // successful binding
                {
                    _ = _scope.Columns.TryAdd(node.Identifier, node.Binding);
                }
            }
        }
        private void BindColumn(in ColumnReference column)
        {
            column.GetColumnIdentifiers(out string tableAlias, out string columnName);

            if (string.IsNullOrEmpty(tableAlias)) // Если синоним таблицы не указан
            {
                BindColumnToMultipleTables(in column);
            }
            else
            {
                if (_scope.TryGetTableByAlias(in tableAlias, out object table))
                {
                    BindColumn(in table, in columnName, in column);
                }
                else
                {
                    TableAliasIsNotFound(tableAlias, column.Identifier);
                }
            }

            if (column.Binding is null)
            {
                RegisterBindingError(column.Token, column.Identifier);
            }
        }
        private void BindColumnToMultipleTables(in ColumnReference column)
        {
            string columnName = column.Identifier;

            List<object> bound = new();
            List<object> tables = _scope.GetScopedTables();

            foreach (object item in tables)
            {
                column.Binding = null;

                BindColumn(in item, in columnName, in column);

                if (column.Binding is not null)
                {
                    bound.Add(column.Binding);
                }
            }

            if (bound.Count == 0)
            {
                column.Binding = null; // Failed to bind
            }
            else if (bound.Count > 1)
            {
                column.Binding = null;

                AmbiguousColumnNameError(columnName);
            }
            else // successful binding
            {
                column.Binding = bound[0];
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
            else if (source is TableUnionOperator union)
            {
                BindColumn(in union, in identifier, in column); // ORDER BY clause columns of the UNION operator
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
            // Used to bind ORDER BY clause columns of the UNION operator

            if (union.Expression1 is SelectExpression select)
            {
                BindColumn(in select, in identifier, in column);
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

        private void Bind(in PrintStatement node)
        {
            Bind(node.Expression);
        }
        private void Bind(in ReturnStatement node)
        {
            Bind(node.Expression);
        }
    }
}