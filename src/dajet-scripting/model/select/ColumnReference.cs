using DaJet.TypeSystem;

namespace DaJet.Scripting.Model
{
    public sealed class ColumnReference : SyntaxNode
    {
        private string _identifier = string.Empty;
        private string _table = string.Empty;
        private string _column = string.Empty;
        private string _value = string.Empty;
        public ColumnReference() { Token = Token.Column; }
        public ColumnExpression Parent { get; set; } // SELECT clause column expressions
        ///<summary>Полный идентификатор колонки, например:<br/>
        ///1. Колонка [column name]<br/>
        ///2. Таблица.Колонка [table alias].[column name]<br/>
        ///3. Перечисление.СтавкиНДС.БезНДС [enumeration value]</summary>
        public string Identifier
        {
            get { return _identifier; }
            set
            {
                _identifier = value ?? string.Empty;

                StringSplitOptions TrimAndRemoveEmpty = StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries;

                Span<Range> names = stackalloc Range[3];

                int count = _identifier.Split(names, '.', TrimAndRemoveEmpty);

                if (count == 1)
                {
                    _column = _identifier[names[0]];
                }
                else if (count > 1)
                {
                    _table = _identifier[names[0]];
                    _column = _identifier[names[1]];
                }
                
                _value = count > 2 ? _identifier[names[2]] : string.Empty;
            }
        }
        ///<summary>Синоним или имя таблицы, которой принадлежит колонка<br/>
        ///Может быть не указано (выводится из контекста)</summary>
        public string TableAlias { get { return _table; } }
        ///<summary>Собственное имя колонки или ссылка на колонку другой таблицы</summary>
        public string ColumnName { get { return _column; } }
        ///<summary>Имя значения перечисления</summary>
        public string ValueName { get { return _value; } }
        public object Binding { get; set; } // PropertyDefinition | ColumnExpression | Entity (enumeration value)
        public override string ToString()
        {
            return $"[{Token}: {Identifier}]";
        }

        /// <summary>
        /// Трансформирует колонку составного типа в колонку одного заданного типа.
        /// </summary>
        /// <param name="source">Метаданные конкретной колонки составного типа.</param>
        /// <returns></returns>
        public ColumnReference ToSingleColumnReference(in ColumnDefinition source)
        {
            string columnName = this.ColumnName;
            string identifier = this.Identifier;

            if (source.Purpose != ColumnPurpose.Value)
            {
                string suffix = source.Purpose.GetSuffix();

                columnName = string.Format("{0}_{1}", columnName, suffix);
                identifier = string.Format("{0}_{1}", identifier, suffix);
            }

            PropertyDefinition binding = new()
            {
                Columns =
                [
                    new ColumnDefinition()
                    {
                        Name = columnName,
                        Type = source.Type,
                        Purpose = source.Purpose
                    }
                ]
            };

            return new ColumnReference()
            {
                Binding = binding,
                Identifier = identifier
            };
        }

        private ColumnReference ToSingleColumnReference(ColumnPurpose purpose)
        {
            throw new NotImplementedException();

            //ColumnDefinition source;
            //ColumnReference identity = null;

            //if (column.Binding is PropertyDefinition property) // Прямой источник данных
            //{
            //    source = property.GetColumnByPurpose(ColumnPurpose.Value);

            //    source ??= property.GetColumnByPurpose(ColumnPurpose.Identity);

            //    if (source is not null)
            //    {
            //        PropertyDefinition binding = new() { Columns = [source] };

            //        identity = new ColumnReference()
            //        {
            //            Binding = binding,
            //            Identifier = column.Identifier
            //        };
            //    }
            //}
            //else if (column.Binding is ColumnExpression derived) // Наследуемый источник данных
            //{
            //    if (derived.Source is not null)
            //    {
            //        property = derived.Source;

            //        source = property.GetColumnByPurpose(ColumnPurpose.Value);

            //        source ??= property.GetColumnByPurpose(ColumnPurpose.Identity);

            //        if (source is not null)
            //        {
            //            identity = column.ToSingleColumnReference(in source);
            //        }
            //    }
            //    else // Константа, параметр, функция или выражение
            //    {
            //        throw new InvalidOperationException("[UUID1C] The function parameter must be a database column reference.");
            //    }
            //}
            //else if (column.Binding is Entity value) // enumeration value
            //{
            //    //TODO: create scalar value !?
            //}

            //if (identity is null)
            //{
            //    throw new InvalidOperationException($"Database column '{this.Identifier}' must be of either 'entity' or 'uuid' data type.");
            //}

            //return identity;
        }
    }
}