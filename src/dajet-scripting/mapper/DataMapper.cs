using DaJet.Scripting.Model;
using DaJet.TypeSystem;

// Исключения из правил:
// - _KeyField (табличная часть) binary(4) -> int CanBeNumeric
// - _Folder (иерархические ссылочные типы) binary(1) -> bool инвертировать !!!
// - _Version (ссылочные типы) timestamp binary(8) -> IsBinary
// - _Type (тип значений характеристики) varbinary(max) -> IsBinary nullable
// - _RecordKind (вид движения накопления) numeric(1) CanBeNumeric Приход = 0, Расход = 1
// - _DimHash numeric(10) ?

// NOTE: SQL Server rowversion is unsigned big-endian value
// NOTE: 1C binary(4) is integer, unsigned big-endian value

namespace DaJet.Scripting
{
    public static class DataMapper
    {
        public static EntityDefinition InferSchema(in SelectStatement node)
        {
            EntityDefinition schema = new();

            List<ColumnExpression> columns = GetColumnExpressions(in node);

            ColumnExpression column;
            PropertyDefinition property;

            for (int i = 0; i < columns.Count; i++)
            {
                column = columns[i];

                property = Infer(in column);

                property.Purpose = PropertyPurpose.Property;

                if (!string.IsNullOrEmpty(column.Alias))
                {
                    property.Name = column.Alias;
                }
                else if (string.IsNullOrEmpty(property.Name))
                {
                    throw new InvalidCastException("Property name missing");
                }

                schema.Properties.Add(property);
            }

            return schema;
        }
        private static List<ColumnExpression> GetColumnExpressions(in SelectStatement node)
        {
            if (node.Expression is SelectExpression select)
            {
                return select.Columns;
            }
            else if (node.Expression is TableUnionOperator union)
            {
                return GetColumnExpressions(in union);
            }

            return null;
        }
        private static List<ColumnExpression> GetColumnExpressions(in TableUnionOperator node)
        {
            if (node.Expression1 is SelectExpression select)
            {
                return select.Columns;
            }

            return null;
        }

        public static bool TryInfer(in SyntaxNode node, out PropertyDefinition property, out string error)
        {
            error = null;
            property = null;

            try
            {
                property = Infer(in node);
            }
            catch (Exception exception)
            {
                error = ExceptionHelper.GetErrorMessage(exception);
            }

            if (property is null)
            {
                throw new InvalidCastException($"Failed to infer data type from {node.GetType()} expression.");
            }

            return string.IsNullOrEmpty(error);
        }
        
        private static PropertyDefinition Infer(in SyntaxNode node)
        {
            if (node is ColumnExpression column) { return Infer(in column); }
            else if (node is ColumnReference identifier) { return Infer(in identifier); }
            else if (node is ScalarExpression scalar) { return Infer(in scalar); }
            else if (node is VariableReference variable) { return Infer(in variable); }
            else if (node is MemberAccessExpression member) { Infer(in member); }
            else if (node is FunctionExpression function) { return Infer(in function); }
            else if (node is GroupOperator group) { return Infer(in group); }
            else if (node is UnaryOperator unary) { return Infer(in unary); }
            else if (node is AdditionOperator addition) { return Infer(in addition); }
            else if (node is MultiplyOperator multiply) { return Infer(in multiply); }
            else if (node is CaseExpression _case) { return Infer(in _case); }
            else if (node is TableExpression table) { return Infer(in table); }

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
            else if (node.Token == Token.Decimal || node.Token == Token.Number)
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

            if (!(declare.Binding is EntityDefinition entity))
            {
                throw new InvalidCastException($"Failed to get data type schema of MemberAccessExpression [{node.Identifier}]");
            }
            
            PropertyDefinition property = new();

            List<string> members = node.GetAccessMembers(node.Identifier);

            for (int i = 1; i < members.Count; i++)
            {
                string member = members[i];

                property = entity.Properties.Where(p => p.Name == member).FirstOrDefault();

                if (property is not null)
                {
                    return property;
                }

                entity = entity.Entities.Where(e => e.Name == member).FirstOrDefault();
            }

            return property;
        }
        private static PropertyDefinition Infer(in FunctionExpression node)
        {
            if (node.Token == Token.UDF)
            {
                //if (!UDF.TryGet(function.Name, out IUserDefinedFunction udf))
                //{
                //    throw new InvalidOperationException($"Invalid function name: {function.Name}");
                //}

                //Type returnType = udf.GetReturnType(in function);
                
                throw new InvalidCastException($"Failed to infer function return type {node.Name}");
            }

            if (!SqlFunctions.TryGet(node.Token, out SqlFunction function))
            {
                throw new InvalidCastException($"Unknown function name: {node.Name}");
            }

            return new PropertyDefinition()
            {
                Type = function.GetReturnType(in node)
            };
            
            //string name = function.Name.ToUpperInvariant();

            //if (name == "COUNT")
            //{
            //    union.IsInteger = true; return;
            //}
            //else if (name == "ROW_NUMBER")
            //{
            //    //TODO: IsVersion is int64 (bigint) hack
            //    //NOTE: the function does not have any parameters
            //    union.IsVersion = true; return;
            //}
            //else if (name == "DATALENGTH" || name == "OCTET_LENGTH" || name == "CHARLENGTH")
            //{
            //    //TODO: IsInteger is int32 (int) hack
            //    //NOTE: the function have one parameter, but we ignore it
            //    union.IsInteger = true; return;
            //}
            //else if (name == "SUBSTRING" || name == "STRING_AGG"
            //    || name == "CONCAT" || name == "CONCAT_WS" || name == "REPLACE"
            //    || name == "LOWER" || name == "UPPER" || name == "LTRIM" || name == "RTRIM")
            //{
            //    union.IsString = true; return;
            //}
            //else if (name == "NOW" || name == "UTC")
            //{
            //    union.IsDateTime = true; return;
            //}
            //else if (name == "VECTOR")
            //{
            //    //union.IsNumeric = true; return;
            //    //TODO: IsVersion is int64 (bigint) hack
            //    union.IsVersion = true; return;
            //}
            //else if (name == "NEWUUID")
            //{
            //    union.IsUuid = true; return;
            //}
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