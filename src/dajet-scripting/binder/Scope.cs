using DaJet.Scripting.Model;

namespace DaJet.Scripting
{
    public sealed class Scope
    {
        ///<summary>Иерархия области видимости (физическая)</summary>
        private readonly Scope _ancestor; //TODO: encapsulate logic in OpenScope method or class
        public Scope() { }
        public Scope(SyntaxNode owner, Scope parent)
        {
            Owner = owner;
            Parent = parent; //NOTE: can be overriden in OpenScope method !!!
            _ancestor = parent; //NOTE: is used by CloseScope method
        }
        public SyntaxNode Owner { get; set; }
        ///<summary>Иерархия области видимости (логическа)</summary>
        public Scope Parent { get; set; }
        ///<summary>Дочерние области видимости (логические)</summary>
        public List<Scope> Children { get; } = new();

        /// <summary>
        /// 1. CTE (common table expression) or temporary tables <br/>
        /// 2. Table expression (subquery) or database schema tables
        /// </summary>
        public Dictionary<string, object> Tables { get; } = new();
        /// <summary>
        /// SELECT scoped column references (SelectExpression)
        /// </summary>
        public Dictionary<string, object> Columns { get; } = new();
        /// <summary>
        /// DECLARE statement
        /// </summary>
        public Dictionary<string, DeclareStatement> Variables { get; } = new();
        public override string ToString() { return $"Owner: {Owner}"; }

        public Scope GetRoot()
        {
            Scope root = this;

            while (root.Parent is not null)
            {
                root = root.Parent;
            }

            return root;
        }
        public Scope Ancestor<TOwner>() where TOwner : SyntaxNode
        {
            Type type = typeof(TOwner);

            Scope scope = this;
            SyntaxNode owner = Owner;

            while (scope is not null)
            {
                if (owner is not null && owner.GetType() == type)
                {
                    return scope;
                }

                scope = scope.Parent;
                owner = scope?.Owner;
            }

            return null;
        }
        public Scope OpenScope(in SyntaxNode owner)
        {
            Scope scope = new(owner, this);

            if (owner is not SelectExpression select || select.IsCorrelated)
            {
                Children.Add(scope); return scope;
            }

            Scope parent = this;

            while (parent is not null)
            {
                select = parent.Owner as SelectExpression;

                if (select is null)
                {
                    scope.Parent = parent;
                    parent.Children.Add(scope);
                    return scope;
                }
                else if (select.IsCorrelated)
                {
                    scope.Parent = parent.Parent;
                    parent.Parent.Children.Add(scope);
                    return scope;
                }

                parent = parent.Parent;
            }

            throw new InvalidOperationException($"Failed to open scope [{owner}]");
        }
        public Scope CloseScope() { return _ancestor; }

        public DeclareStatement GetVariable(in string identifier)
        {
            Scope scope = this;

            while (scope is not null)
            {
                if (scope.Variables.TryGetValue(identifier, out DeclareStatement variable))
                {
                    return variable;
                }

                scope = scope.Parent;
            }

            return null;
        }

        public List<object> GetScopedTables()
        {
            // Ищем все, доступные для поиска колонки, таблицы.
            // Общие табличные выражения верхнего уровня не учитываем,
            // так как они уже были привязаны ранее по областям видимости

            List<object> tables = new();

            Scope scope = this; // lookup current and upper scopes

            while (scope is not null && scope.Owner is not SelectStatement)
            {
                if (scope.Tables.Count > 0)
                {
                    tables.AddRange(scope.Tables.Values);
                }

                scope = scope.Parent;
            }

            return tables;
        }
        public object GetTableBinding(in string name)
        {
            Scope scope = this;

            while (scope is not null)
            {
                if (scope.Tables.TryGetValue(name, out object binding))
                {
                    return binding;
                }

                scope = scope.Parent;
            }

            return null;
        }
        public bool TryGetTableByAlias(in string alias, out object table)
        {
            // TODO: find all candidate tables and warn ambiguous names

            if (string.IsNullOrEmpty(alias)
                || alias.Equals("deleted", StringComparison.OrdinalIgnoreCase)
                || alias.Equals("inserted", StringComparison.OrdinalIgnoreCase))
            {
                // take first available table
                table = Tables.Values.FirstOrDefault();
                
                return (table is not null);
            }

            // lookup current and upper scopes

            Scope scope = this;

            while (scope is not null)
            {
                if (scope.Tables.TryGetValue(alias, out table))
                {
                    return true;
                }

                scope = scope.Parent;
            }

            // failed to bind table by alias

            table = null;
            return false;
        }
    }
}