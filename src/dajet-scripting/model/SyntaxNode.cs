using DaJet.TypeSystem;

namespace DaJet.Scripting.Model
{
    public abstract class SyntaxNode
    {
        public Token Token { get; set; } = Token.Ignore;
        public override string ToString()
        {
            return $"{Token}: {GetType()}";
        }

        ///<summary>Выводит тип данных выражения, представленного данным узлом синтаксического дерева.</summary>
        public DataType InferType()
        {
            PropertyDefinition property = Infer(this);

            if (property is null)
            {
                throw new InvalidCastException($"Failed to infer data type from {GetType()} expression.");
            }

            return property.Type;
        }

        ///<summary>
        ///Используется для выведения источника данных ColumnExpression или ColumnReference.<br/>
        ///Источником данных в таком случае могут быть колонки таблицы базы данных или выражения, например функции.<br/>
        ///Если источником данных является выражение, а не колонка таблицы, то выводится только тип его значения,<br/>
        ///а имя возвращаемого PropertyDefinition при этом остаётся пустым (имя возвращается как оно задано в метаданных).<br/>
        ///<b>Пример использования:</b> определение схемы данных предложения SELECT или OUTPUT соответствующей SQL-команды.
        ///</summary>
        public PropertyDefinition InferSource() { return Infer(this); }
        private static PropertyDefinition Infer(in SyntaxNode node)
        {
            if (node is ColumnExpression column) { return Infer(in column); }
            else if (node is ColumnReference identifier) { return Infer(in identifier); }
            else if (node is ScalarExpression scalar) { return Infer(in scalar); }
            else if (node is VariableReference variable) { return Infer(in variable); }
            else if (node is MemberAccessExpression member) { return Infer(in member); }
            else if (node is FunctionExpression function) { return Infer(in function); }
            else if (node is GroupOperator group) { return Infer(in group); }
            else if (node is UnaryOperator unary) { return Infer(in unary); }
            else if (node is AdditionOperator addition) { return Infer(in addition); }
            else if (node is MultiplyOperator multiply) { return Infer(in multiply); }
            else if (node is CaseExpression _case) { return Infer(in _case); }
            else if (node is TableExpression table) { return Infer(in table); }
            else if (node is TypeReference type) { return Infer(in type); }

            throw new FormatException($"Unknown expression type: {node.GetType()}");
        }
        private static PropertyDefinition Infer(in ColumnExpression node)
        {
            return Infer(node.Expression);
        }
        private static PropertyDefinition Infer(in ColumnReference node)
        {
            if (node.Binding is PropertyDefinition property)
            {
                return Infer(in property);
            }
            else if (node.Binding is ColumnExpression column)
            {
                return Infer(in column);
            }

            //else if (column.Binding is EnumValue)
            //{
            //    union.IsUuid = true;
            //}

            throw new InvalidCastException($"Invalid binding of ColumnReference [{node.Identifier}]");
        }

        private static PropertyDefinition Infer(in TypeReference node)
        {
            PropertyDefinition property = new();

            if (node.Binding is MetadataEntry entity)
            {
                property.Type = DataType.Entity(entity.Code);
            }
            else
            {
                property.Type = node.Type;
            }

            return property;
        }
        private static PropertyDefinition Infer(in ScalarExpression node)
        {
            PropertyDefinition property = new();

            if (node.Token == Token.Boolean)
            {
                property.Type = DataType.Boolean;
            }
            else if (node.Token == Token.Integer)
            {
                property.Type = DataType.Integer();
            }
            else if (node.Token == Token.Decimal)
            {
                property.Type = DataType.Decimal();
            }
            else if (node.Token == Token.DateTime)
            {
                property.Type = DataType.DateTime;
            }
            else if (node.Token == Token.String)
            {
                property.Type = DataType.String();
            }
            else if (node.Token == Token.Binary)
            {
                property.Type = DataType.Binary();
            }
            else if (node.Token == Token.Uuid)
            {
                property.Type = DataType.Uuid();
            }
            else if (node.Token == Token.Entity)
            {
                if (Entity.TryParse(node.Literal, out Entity entity))
                {
                    property.Type = DataType.Entity(entity.TypeCode);
                }
            }
            else if (node.Token == Token.NULL)
            {
                property.Type = DataType.Undefined;
            }

            return property;
        }
        private static PropertyDefinition Infer(in VariableReference node)
        {
            if (node.Binding is not DeclareStatement variable)
            {
                throw new InvalidCastException($"Invalid binding of VariableReference [{node.Identifier}]");
            }

            return new PropertyDefinition()
            {
                Type = variable.Type
            };
        }
        private static PropertyDefinition Infer(in MemberAccessExpression node)
        {
            if (node.Binding is not DeclareStatement declare)
            {
                throw new InvalidCastException($"Invalid binding of MemberAccessExpression [{node.Identifier}]");
            }

            if (declare.Binding is not DefineStatement schema)
            {
                throw new InvalidCastException($"Failed to get data type schema of MemberAccessExpression [{node.Identifier}]");
            }

            DefineProperty property = new();

            List<string> members = node.GetAccessMembers();

            string member;

            for (int i = 1; i < members.Count; i++)
            {
                member = members[i];

                property = schema.GetPropertyByName(in member);

                if (property is not null)
                {
                    return new PropertyDefinition()
                    {
                        Type = property.Type
                    };
                }

                //TODO: get schema by name : schema = property.Schema
                //entity = entity.Entities.Where(e => e.Name == member).FirstOrDefault();
            }

            return new PropertyDefinition();
        }
        private static PropertyDefinition Infer(in FunctionExpression node)
        {
            if (node.Token == Token.UDF)
            {
                throw new InvalidCastException($"DaJet functions are not implemented for SELECT clause");
            }

            if (!SqlFunctions.TryGet(node.Token, out Function function))
            {
                throw new InvalidCastException($"Unknown SQL function name: {node.Name}");
            }

            return new PropertyDefinition()
            {
                Type = function.GetReturnType(in node)
            };
        }
        private static PropertyDefinition Infer(in PropertyDefinition property)
        {
            return property;
        }

        private static PropertyDefinition Infer(in GroupOperator node)
        {
            return Infer(node.Expression);
        }
        private static PropertyDefinition Infer(in UnaryOperator node)
        {
            PropertyDefinition property = Infer(node.Expression);

            if (node.Token == Token.Minus)
            {
                if (!(property.Type.IsInteger || property.Type.IsDecimal))
                {
                    throw new InvalidCastException("Failed to infer data type from UnaryOperator");
                }
            }
            else if (node.Token == Token.NOT)
            {
                if (!property.Type.IsBoolean)
                {
                    throw new InvalidCastException("Failed to infer data type from UnaryOperator");
                }
            }

            return property;
        }
        private static PropertyDefinition Infer(in AdditionOperator node)
        {
            PropertyDefinition property1 = Infer(node.Expression1);
            PropertyDefinition property2 = Infer(node.Expression2);

            //TODO: check if data types are convertible and use one, which has highest precedence

            return property1;
        }
        private static PropertyDefinition Infer(in MultiplyOperator node)
        {
            PropertyDefinition property1 = Infer(node.Expression1);
            PropertyDefinition property2 = Infer(node.Expression2);

            //TODO: check if data types are convertible and use one, which has highest precedence

            return property1;
        }
        private static PropertyDefinition Infer(in CaseExpression node)
        {
            if (node.CASE is null || node.CASE.Count == 0)
            {
                throw new InvalidCastException("Failed to infer CASE expression return type");
            }

            WhenClause when = node.CASE[0];

            return Infer(when.THEN);

            //TODO: 1C uses data type extension to union and transforms CASE expression to multiple columns

            //foreach (WhenClause when in node.CASE)
            //{
            //    Visit(when.THEN, in union, ref propertyName);
            //}
            //if (node.ELSE is not null)
            //{
            //    Visit(node.ELSE, in union, ref propertyName);
            //}
        }

        private static PropertyDefinition Infer(in TableExpression table)
        {
            return Infer(GetFirstColumnExpression(in table));
        }
        private static ColumnExpression GetFirstColumnExpression(in SyntaxNode node)
        {
            if (node is TableExpression table)
            {
                return GetFirstColumnExpression(in table);
            }
            else if (node is SelectExpression select)
            {
                return GetFirstColumnExpression(in select);
            }
            else if (node is TableUnionOperator union)
            {
                return GetFirstColumnExpression(in union);
            }

            return null;
        }
        private static ColumnExpression GetFirstColumnExpression(in TableExpression table)
        {
            return GetFirstColumnExpression(table.Expression);
        }
        private static ColumnExpression GetFirstColumnExpression(in SelectExpression select)
        {
            return select.Columns.FirstOrDefault();
        }
        private static ColumnExpression GetFirstColumnExpression(in TableUnionOperator union)
        {
            return GetFirstColumnExpression(union.Expression1);
        }
    }
}