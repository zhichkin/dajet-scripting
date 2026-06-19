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
    }
}