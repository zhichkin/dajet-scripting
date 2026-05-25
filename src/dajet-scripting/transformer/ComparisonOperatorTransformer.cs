using DaJet.Data;
using DaJet.Scripting.Model;
using DaJet.TypeSystem;

namespace DaJet.Scripting
{
    public sealed class ComparisonOperatorTransformer
    {
        public SyntaxNode Transform(in ComparisonOperator comparison)
        {
            // DONE:
            // 1. Ссылка = @Ссылка
            // 2. Ссылка1 = Ссылка2 (составные типы)
            // 3. Ссылка ССЫЛКА Справочник.Номенклатура = <column> IS [NOT] <type>
            // 4. ТИПЗНАЧЕНИЯ(Ссылка) = ТИП(Справочник.Номенклатура) = <column> IS [NOT] <type>
            // 5. <column> IS [NOT] NULL

            if (comparison.Token == Token.IS)
            {
                return TransformColumnIsType(in comparison);
            }

            if (comparison.Token == Token.Equals && IsUnionComparison(comparison.Expression1, comparison.Expression2))
            {
                return TransformUnionComparison(in comparison);
            }

            return null; // no transformation is needed
        }
        private void ThrowUnableToCompareException(SyntaxNode node1, SyntaxNode node2)
        {
            throw new InvalidCastException($"Unable to compare {node1} and {node2}");
        }

        #region "<column> IS <type>"
        private bool IsSimpleIsNullOperator(SyntaxNode left, SyntaxNode right)
        {
            return IsScalarColumn(left) && IsNullScalar(right);
        }
        private bool IsNullScalar(SyntaxNode node)
        {
            return (node is ScalarExpression scalar && scalar.Token == Token.NULL);
        }
        private bool IsScalarColumn(SyntaxNode node)
        {
            if (node is ColumnReference column)
            {
                return IsScalarColumn(column.Binding);
            }

            return false;
        }
        private bool IsScalarColumn(in object binding)
        {
            if (binding is PropertyDefinition property)
            {
                return (property.Columns.Count == 1);
            }
            else if (binding is ColumnExpression expression)
            {
                return IsScalarColumn(in expression);
            }

            return false;
        }
        private bool IsScalarColumn(in ColumnExpression expression)
        {
            if (expression.Expression is ColumnReference column)
            {
                return IsScalarColumn(column.Binding);
            }

            return false;
        }
        private bool IsUnionColumn(SyntaxNode node, out ColumnReference column)
        {
            column = null;

            if (node is ColumnReference identifier &&
                identifier.Binding is PropertyDefinition property &&
                property.Columns.Count > 1)
            {
                column = identifier;
            }

            return (column != null);
        }
        private bool IsTypeIdentifier(SyntaxNode node, out TypeIdentifier type)
        {
            type = null;

            if (node is not TypeIdentifier identifier)
            {
                return false;
            }

            if (identifier.Binding is Type || identifier.Binding is Entity)
            {
                type = identifier;
            }

            return (type != null);
        }

        private GroupOperator Transform(ComparisonOperator comparison, SyntaxNode node1, SyntaxNode node2)
        {
            int tag = (int)ColumnPurpose.Tag;
            int tref = (int)ColumnPurpose.TypeCode;
            int rref = (int)ColumnPurpose.Identity;

            object[] union1 = CreateUnion(node1);
            object[] union2 = CreateUnion(node2);

            if (union1[tag] is int tag1 && union2[tag] is int tag2)
            {
                if (tag1 == tag2)
                {
                    union1[tag] = null!;
                    union2[tag] = null!;
                }
                else
                {
                    ThrowUnableToCompareException(node1, node2);
                }
            }

            if (union1[tref] is int tref1 && union2[tref] is int tref2)
            {
                if (tref1 == tref2)
                {
                    union1[tref] = null!;
                    union2[tref] = null!;
                }
                else
                {
                    ThrowUnableToCompareException(node1, node2);
                }
            }

            GroupOperator group = new();

            for (int type = tag; type <= rref; type++)
            {
                if (union1[type] != null && union2[type] != null)
                {
                    if (group.Expression == null)
                    {
                        group.Expression = CreateComparisonOperator(comparison.Token, node1, node2, type, union1, union2);
                    }
                    else
                    {
                        group.Expression = new BinaryOperator()
                        {
                            Token = Token.AND,
                            Expression1 = group.Expression,
                            Expression2 = CreateComparisonOperator(comparison.Token, node1, node2, type, union1, union2)
                        };
                    }
                }
            }

            if (group.Expression == null)
            {
                return null!; // no compatible types are found to compare
            }

            return group;
        }

        private object[] CreateUnion(SyntaxNode node)
        {
            if (node is VariableReference variable)
            {
                return ConvertVariableToUnion(variable);
            }

            if (node is ScalarExpression scalar)
            {
                return ConvertScalarToUnion(scalar);
            }
            
            if (node is TypeIdentifier type)
            {
                return ConvertTypeToUnion(type);
            }

            if (node is ColumnReference column &&
                column.Binding is PropertyDefinition property &&
                property.Columns.Count > 0)
            {
                if (property.Columns.Count == 1)
                {
                    return ConvertSingleToUnion(property);
                }
                else
                {
                    return CreateUnion(property);
                }
            }
            
            return null;
        }
        private object[] CreateUnion(PropertyDefinition property)
        {
            if (property.Columns.Count == 1)
            {
                return ConvertSingleToUnion(property);
            }

            object[] union = new object[((int)ColumnPurpose.Identity) + 1];

            ColumnDefinition field;

            for (int i = 0; i < property.Columns.Count; i++)
            {
                field = property.Columns[i];

                union[(int)field.Purpose] = field;
            }

            if (union[(int)ColumnPurpose.Identity] != null &&
                union[(int)ColumnPurpose.Tag] == null)
            {
                // only reference type - tag is constant
                union[(int)ColumnPurpose.Tag] = (int)ColumnPurpose.Identity; // 0x08
            }

            if (union[(int)ColumnPurpose.Identity] != null &&
                union[(int)ColumnPurpose.TypeCode] == null)
            {
                // single reference type - type code is constant
                union[(int)ColumnPurpose.TypeCode] = property.Type.TypeCode;
            }

            return union;
        }
        private object[] ConvertSingleToUnion(PropertyDefinition property)
        {
            object[] union = new object[((int)ColumnPurpose.Identity) + 1];
            
            int value = 0;
            int tag = (int)ColumnPurpose.Tag;
            DataType type = property.Type;
            ColumnDefinition field = property.Columns[0];

            if (type.IsUuid || type.IsBinary)
            {
                value = (int)ColumnPurpose.Binary;
            }
            else if (type.IsBoolean)
            {
                value = (int)ColumnPurpose.Boolean;
            }
            else if (type.IsDecimal)
            {
                value = (int)ColumnPurpose.Numeric;
            }
            else if (type.IsDateTime)
            {
                value = (int)ColumnPurpose.DateTime;
            }
            else if (type.IsString)
            {
                value = (int)ColumnPurpose.String;
            }
            else if (type.IsEntity)
            {
                value = (int)ColumnPurpose.Identity;
                union[(int)ColumnPurpose.TypeCode] = type.TypeCode;
            }

            union[tag] = value;
            union[value] = field;

            return union;
        }
        private object[] ConvertScalarToUnion(ScalarExpression scalar)
        {
            object[] union = new object[((int)ColumnPurpose.Identity) + 1];

            int tag = (int)ColumnPurpose.Tag;
            int value = tag; // Undefined

            if (scalar.Token == Token.Boolean)
            {
                value = (int)ColumnPurpose.Boolean;
            }
            else if (scalar.Token == Token.Number)
            {
                value = (int)ColumnPurpose.Numeric;
            }
            else if (scalar.Token == Token.DateTime)
            {
                value = (int)ColumnPurpose.DateTime;
            }
            else if (scalar.Token == Token.String)
            {
                value = (int)ColumnPurpose.String;
            }
            else if (scalar.Token == Token.Uuid || scalar.Token == Token.Binary)
            {
                value = (int)ColumnPurpose.Binary;
            }
            else if (scalar.Token == Token.Entity)
            {
                value = (int)ColumnPurpose.Identity;
                union[(int)ColumnPurpose.TypeCode] = Entity.Parse(scalar.Literal).TypeCode;
            }

            union[tag] = value;
            union[value] = scalar.Literal;

            return union;
        }
        private object[] ConvertVariableToUnion(VariableReference variable)
        {
            object[] union = new object[((int)ColumnPurpose.Identity) + 1];

            int tag = (int)ColumnPurpose.Tag;
            int value = tag;

            if (variable.Binding is DeclareStatement type)
            {
                if (type.Type.IsBoolean)
                {
                    value = (int)ColumnPurpose.Boolean;
                }
                else if (type.Type.IsDecimal)
                {
                    value = (int)ColumnPurpose.Numeric;
                }
                else if (type.Type.IsDateTime)
                {
                    value = (int)ColumnPurpose.DateTime;
                }
                else if (type.Type.IsString)
                {
                    value = (int)ColumnPurpose.String;
                }
                else if (type.Type.IsUuid || type.Type.IsBinary)
                {
                    value = (int)ColumnPurpose.Binary;
                }
            }
            else if (variable.Binding is DeclareStatement declare && declare.Type.IsEntity)
            {
                value = (int)ColumnPurpose.Identity;
                union[(int)ColumnPurpose.TypeCode] = declare.Type.TypeCode;
            }

            union[tag] = value;
            union[value] = variable.Binding;

            return union;
        }
        private object[] ConvertTypeToUnion(TypeIdentifier identifier)
        {
            object[] union = new object[((int)ColumnPurpose.Identity) + 1];

            int tag = (int)ColumnPurpose.Tag; // адрес значения поля _TYPE
            int code = (int)ColumnPurpose.TypeCode; // адрес значения поля _TRef

            if (identifier.Binding is Type type)
            {
                if (type == typeof(Union)) // undefined
                {
                    union[tag] = (int)ColumnPurpose.Tag;
                }
                else if (type == typeof(bool)) // boolean
                {
                    union[tag] = (int)ColumnPurpose.Boolean;
                }
                else if (type == typeof(decimal)) // number
                {
                    union[tag] = (int)ColumnPurpose.Numeric;
                }
                else if (type == typeof(DateTime)) // datetime
                {
                    union[tag] = (int)ColumnPurpose.DateTime;
                }
                else if (type == typeof(string)) // string
                {
                    union[tag] = (int)ColumnPurpose.String;
                }
                else
                {
                    throw new FormatException($"Unknown type identifier: {identifier.Identifier}");
                }
            }
            else if (identifier.Binding is Entity entity)
            {
                union[tag] = (int)ColumnPurpose.Identity; // 0x08 - значение поля _TYPE
                union[code] = entity.TypeCode; // integer - значение поля _TRef
            }
            else
            {
                throw new FormatException($"Unknown type identifier: {identifier.Identifier}");
            }
            
            return union;
        }

        private ComparisonOperator CreateComparisonOperator(Token type, SyntaxNode column1, SyntaxNode column2, int tag, object[] union1, object[] union2)
        {
            ComparisonOperator comparison = new()
            {
                Token = type
            };

            if (union1[tag] is int value1)
            {
                comparison.Expression1 = new ScalarExpression()
                {
                    Token = Token.Number,
                    Literal = $"0x{Convert.ToHexString([(byte)value1])}" //((ColumnPurpose)tag).GetBinaryLiteral(value1)
                };
            }
            else
            {
                comparison.Expression1 = CreateSyntaxNode(in column1, union1[tag]);
            }

            if (union2[tag] is int value2)
            {
                comparison.Expression2 = new ScalarExpression()
                {
                    Token = Token.Number,
                    Literal = $"0x{Convert.ToHexString([(byte)value2])}" //((ColumnPurpose)tag).GetBinaryLiteral(value2)
                };
            }
            else
            {
                comparison.Expression2 = CreateSyntaxNode(in column2, union2[tag]);
            }

            return comparison;
        }
        private SyntaxNode CreateSyntaxNode(in SyntaxNode node, object binding)
        {
            if (node is ColumnReference property)
            {
                ColumnReference column = new()
                {
                    Binding = binding, // database column
                    Identifier = property.Identifier
                };

                column.GetColumnIdentifiers(out string tableAlias, out string _);

                if (column.Binding is ColumnDefinition source)
                {
                    //ColumnMapper map = new()
                    //{
                    //    Type = source.Purpose.GetUnionTag(),
                    //    Name = string.IsNullOrEmpty(tableAlias) ? source.Name : $"{tableAlias}.{source.Name}"
                    //};
                    //column.Mapping = new List<ColumnMapper>() { map };
                }

                return column;
            }
            else if (node is VariableReference variable)
            {
                return variable;
            }
            else if (node is ScalarExpression scalar)
            {
                return scalar;
            }

            return null; // TODO: throw error - unable to compare types
        }

        private ColumnDefinition GetColumnToCompareToNull(PropertyDefinition property)
        {
            for (int i = 0; i < property.Columns.Count; i++)
            {
                if (property.Columns[i].Purpose == ColumnPurpose.Tag)
                {
                    return property.Columns[i];
                }
            }

            for (int i = 0; i < property.Columns.Count; i++)
            {
                if (property.Columns[i].Purpose == ColumnPurpose.TypeCode)
                {
                    return property.Columns[i];
                }
            }

            return property.Columns[0];
        }
        private SyntaxNode TransformColumnIsType(in ComparisonOperator comparison)
        {
            Token _operator;
            SyntaxNode leftOperand = comparison.Expression1;
            SyntaxNode rigthOperand = comparison.Expression2;

            if (rigthOperand is UnaryOperator unary)
            {
                _operator = Token.NotEquals;
                rigthOperand = unary.Expression;
            }
            else
            {
                _operator = Token.Equals;
            }

            if (IsSimpleIsNullOperator(leftOperand, rigthOperand))
            {
                return null; // no transformation is needed
            }

            if (!IsUnionColumn(leftOperand, out ColumnReference column))
            {
                throw new FormatException($"IS operator: left operand must be the union type column.");
            }

            if (IsNullScalar(rigthOperand)) // _Fld_TYPE IS [NOT] NULL
            {
                if (column.Binding is PropertyDefinition property)
                {
                    column.Binding = GetColumnToCompareToNull(property);
                }
                return null;
            }

            if (!IsTypeIdentifier(rigthOperand, out TypeIdentifier identifier))
            {
                throw new FormatException($"IS operator: right operand is not valid type identifier.");
            }
            
            comparison.Token = _operator;

            return Transform(comparison, column, identifier);
        }

        #endregion

        #region "<union type> == <union type>"
        private bool IsUnionNode(in SyntaxNode node)
        {
            if (!DataMapper.TryInfer(in node, out PropertyDefinition source, out string error))
            {
                throw new InvalidCastException($"Failed to infer data type from {node.GetType()} expression: {error}");
            }

            if (string.IsNullOrEmpty(source.Name))
            {
                return false; // Константа, функция, параметр или выражение
            }

            return source.Type.IsUnion; // Источник данных - колонка таблицы СУБД
        }
        private bool IsUnionComparison(in SyntaxNode left, in SyntaxNode right)
        {
            return IsUnionNode(in left) || IsUnionNode(in right);
        }
        private SyntaxNode TransformUnionComparison(in ComparisonOperator comparison)
        {
            Dictionary<ColumnPurpose, ComparisonOperator> map = new()
            {
                { ColumnPurpose.Tag, new ComparisonOperator() { Token = Token.Equals } },
                { ColumnPurpose.Boolean, new ComparisonOperator() { Token = Token.Equals } },
                { ColumnPurpose.Numeric, new ComparisonOperator() { Token = Token.Equals } },
                { ColumnPurpose.DateTime, new ComparisonOperator() { Token = Token.Equals } },
                { ColumnPurpose.String, new ComparisonOperator() { Token = Token.Equals } },
                { ColumnPurpose.Binary, new ComparisonOperator() { Token = Token.Equals } },
                { ColumnPurpose.TypeCode, new ComparisonOperator() { Token = Token.Equals } },
                { ColumnPurpose.Identity, new ComparisonOperator() { Token = Token.Equals } }
            };

            Transform(comparison.Expression1, in map, SetExpression1);
            Transform(comparison.Expression2, in map, SetExpression2);

            ConfigureTag(in map, in comparison); // _TYPE column
            ConfigureTypeCode(in map, in comparison); // _TRef column

            GroupOperator group = new();

            foreach (var item in map)
            {
                if (item.Value.Expression1 is not null && item.Value.Expression2 is not null)
                {
                    if (group.Expression == null)
                    {
                        group.Expression = item.Value;
                    }
                    else
                    {
                        group.Expression = new BinaryOperator()
                        {
                            Token = Token.AND,
                            Expression1 = group.Expression,
                            Expression2 = item.Value
                        };
                    }
                }
            }

            if (group.Expression == null) // no compatible types are found to compare
            {
                ThrowUnableToCompareException(comparison.Expression1, comparison.Expression2);
            }

            return group;
        }
        private void SetExpression1(ComparisonOperator comparison, SyntaxNode value)
        {
            comparison.Expression1 = value;
        }
        private void SetExpression2(ComparisonOperator comparison, SyntaxNode value)
        {
            comparison.Expression2 = value;
        }
        private void ConfigureTag(in Dictionary<ColumnPurpose, ComparisonOperator> map, in ComparisonOperator comparison)
        {
            if (!map.TryGetValue(ColumnPurpose.Tag, out ComparisonOperator item)) { return; }
            
            if (item.Expression1 is null && item.Expression2 is null) { return; } // Tag column is not used
            
            if (item.Expression1 is not null && item.Expression2 is not null) { return; } // Tag column is mapped already

            DataType target;
            DataType source;

            if (item.Expression1 is null)
            {
                target = DataMapper.InferType(comparison.Expression1);
                source = DataMapper.InferType(comparison.Expression2);
            }
            else
            {
                target = DataMapper.InferType(comparison.Expression2);
                source = DataMapper.InferType(comparison.Expression1);
            }

            //DataType type = target.Type;

            //if (!source.Is(type)) { return; } // incompatible data types

            // $"0x{Convert.ToHexString(new byte[] { (byte)tag })}";

            string literal;

            if (target.IsBoolean) { literal = "0x02"; }
            else if (target.IsDecimal) { literal = "0x03"; }
            else if (target.IsDateTime) { literal = "0x04"; }
            else if (target.IsString) { literal = "0x05"; }
            else if (target.IsEntity) { literal = "0x08"; }
            else
            {
                return; //FIXME: !?
            }

            ScalarExpression scalar = new()
            {
                Token = Token.Binary,
                Literal = literal //$"0x{Convert.ToHexString([tag])}"
            };

            if (item.Expression1 is null)
            {
                item.Expression1 = scalar;
            }
            else
            {
                item.Expression2 = scalar;
            }
        }
        private void ConfigureTypeCode(in Dictionary<ColumnPurpose, ComparisonOperator> map, in ComparisonOperator comparison)
        {
            if (!map.TryGetValue(ColumnPurpose.TypeCode, out ComparisonOperator item)) { return; }
            
            if (item.Expression1 is null && item.Expression2 is null) { return; } // TypeCode column is not used
            
            if (item.Expression1 is not null && item.Expression2 is not null) { return; } // TypeCode column is mapped already

            DataType target;

            if (item.Expression1 is null)
            {
                target = DataMapper.InferType(comparison.Expression1);
            }
            else
            {
                target = DataMapper.InferType(comparison.Expression2);
            }

            if (!target.IsEntity) { return; } // TypeCode can only be used in conjunction with Entity

            ScalarExpression scalar = new()
            {
                Token = Token.Binary,
                Literal = $"0x{Convert.ToHexString(DbUtilities.GetByteArray(target.TypeCode))}"
            };

            if (item.Expression1 is null)
            {
                item.Expression1 = scalar;
            }
            else
            {
                item.Expression2 = scalar;
            }
        }
        private void Transform(in SyntaxNode node, in Dictionary<ColumnPurpose, ComparisonOperator> map, Action<ComparisonOperator, SyntaxNode> setter)
        {
            if (node is ColumnReference column)
            {
                Transform(in column, map, setter);
            }
            else if (node is ScalarExpression scalar)
            {
                Transform(in scalar, map, setter);
            }
            else if (node is VariableReference variable)
            {
                Transform(in variable, map, setter);
            }
            else if (node is MemberAccessExpression member)
            {
                Transform(in member, map, setter);
            }
            else if (node is FunctionExpression function)
            {
                Transform(in function, map, setter);
            }
        }
        private void Transform(in ColumnReference node, in Dictionary<ColumnPurpose, ComparisonOperator> map, Action<ComparisonOperator, SyntaxNode> setter)
        {
            if (node.Binding is PropertyDefinition property) // Прямой источник данных
            {
                PropertyDefinition binding;

                ColumnDefinition column = property.GetColumnByPurpose(ColumnPurpose.Value);

                column ??= property.GetColumnByPurpose(ColumnPurpose.Identity);

                if (map.TryGetValue(ColumnPurpose.Identity, out ComparisonOperator identity))
                {
                    binding = new PropertyDefinition() { Columns = [column] };

                    ColumnReference expression = new()
                    {
                        Binding = binding,
                        Identifier = node.Identifier
                    };

                    setter(identity, expression);
                }

                column = property.GetColumnByPurpose(ColumnPurpose.Tag);

                if (column is not null)
                {
                    if (map.TryGetValue(ColumnPurpose.Tag, out ComparisonOperator tag))
                    {
                        binding = new PropertyDefinition() { Columns = [column] };

                        ColumnReference expression = new()
                        {
                            Binding = binding,
                            Identifier = node.Identifier
                        };

                        setter(tag, expression);
                    }
                }

                column = property.GetColumnByPurpose(ColumnPurpose.TypeCode);

                if (column is not null)
                {
                    if (map.TryGetValue(ColumnPurpose.TypeCode, out ComparisonOperator typecode))
                    {
                        binding = new PropertyDefinition() { Columns = [column] };

                        ColumnReference expression = new()
                        {
                            Binding = binding,
                            Identifier = node.Identifier
                        };

                        setter(typecode, expression);
                    }
                }
            }

            //TODO: else if (node.Binding is ColumnExpression derived) // Наследуемый источник данных
            //{
            //    if (derived.Source is null) // Константа, функция, параметр или выражение
            //    {
            //        //if (derived.Expression is ScalarExpression)
            //        //{
            //        //    script.Append(node.Identifier); return;
            //        //}

            //        throw new InvalidOperationException($"Ошибка привязки данных: {node.Identifier}");
            //    }
            //    else
            //    {
            //        property = derived.Source;
            //    }
            //}
        }
        private void Transform(in ScalarExpression node, in Dictionary<ColumnPurpose, ComparisonOperator> map, Action<ComparisonOperator, SyntaxNode> setter)
        {
            DataType type = DataMapper.InferType(node);

            //UnionTag tag = type.IsUuid ? UnionTag.Entity : type.GetSingleTagOrUndefined();

            //if (map.TryGetValue(tag, out ComparisonOperator comparison))
            //{
            //    setter(comparison, node);
            //}
        }
        private void Transform(in VariableReference node, in Dictionary<ColumnPurpose, ComparisonOperator> map, Action<ComparisonOperator, SyntaxNode> setter)
        {
            DataType type = DataMapper.InferType(node);

            if (type.IsEntity)
            {
                if (map.TryGetValue(ColumnPurpose.Tag, out ComparisonOperator tag))
                {
                    setter(tag, new ScalarExpression()
                    {
                        Token = Token.Binary,
                        Literal = "0x08" // reference type (ссылка)
                    });
                }

                if (map.TryGetValue(ColumnPurpose.TypeCode, out ComparisonOperator code))
                {
                    setter(code, new FunctionExpression()
                    {
                        Token = Token.UDF,
                        Name = nameof(TYPEOF),
                        Parameters = { node }
                    });
                }

                if (map.TryGetValue(ColumnPurpose.Identity, out ComparisonOperator uuid))
                {
                    setter(uuid, new FunctionExpression()
                    {
                        Token = Token.UDF,
                        Name = nameof(UUIDOF),
                        Parameters = { node }
                    });
                }
            }
            else if (type.IsUuid)
            {
                if (map.TryGetValue(ColumnPurpose.Identity, out ComparisonOperator uuid))
                {
                    setter(uuid, new FunctionExpression()
                    {
                        Token = Token.UDF,
                        Name = nameof(UUIDOF),
                        Parameters = { node }
                    });
                }
            }
            else if (type.IsBoolean)
            {
                if (map.TryGetValue(ColumnPurpose.Boolean, out ComparisonOperator boolean))
                {
                    setter(boolean, node);
                }
            }
            else if (type.IsDecimal)
            {
                if (map.TryGetValue(ColumnPurpose.Numeric, out ComparisonOperator number))
                {
                    setter(number, node);
                }
            }
            else if (type.IsDateTime)
            {
                if (map.TryGetValue(ColumnPurpose.DateTime, out ComparisonOperator datetime))
                {
                    setter(datetime, node);
                }
            }
            else if (type.IsString)
            {
                if (map.TryGetValue(ColumnPurpose.String, out ComparisonOperator _string))
                {
                    setter(_string, node);
                }
            }
        }
        private void Transform(in MemberAccessExpression node, in Dictionary<ColumnPurpose, ComparisonOperator> map, Action<ComparisonOperator, SyntaxNode> setter)
        {
            DataType type = DataMapper.InferType(node);

            if (type.IsEntity)
            {
                if (map.TryGetValue(ColumnPurpose.Tag, out ComparisonOperator tag))
                {
                    setter(tag, new ScalarExpression()
                    {
                        Token = Token.Binary,
                        Literal = "0x08" // reference type (ссылка)
                    });
                }

                if (map.TryGetValue(ColumnPurpose.TypeCode, out ComparisonOperator code))
                {
                    setter(code, new FunctionExpression()
                    {
                        Token = Token.UDF,
                        Name = nameof(TYPEOF),
                        Parameters = { node }
                    });
                }

                if (map.TryGetValue(ColumnPurpose.Identity, out ComparisonOperator uuid))
                {
                    setter(uuid, new FunctionExpression()
                    {
                        Token = Token.UDF,
                        Name = nameof(UUIDOF),
                        Parameters = { node }
                    });
                }
            }
            else if (type.IsUuid)
            {
                if (map.TryGetValue(ColumnPurpose.Identity, out ComparisonOperator uuid))
                {
                    setter(uuid, new FunctionExpression()
                    {
                        Token = Token.UDF,
                        Name = nameof(UUIDOF),
                        Parameters = { node }
                    });
                }
            }
            else if (type.IsBoolean)
            {
                if (map.TryGetValue(ColumnPurpose.Boolean, out ComparisonOperator boolean))
                {
                    setter(boolean, node);
                }
            }
            else if (type.IsDecimal)
            {
                if (map.TryGetValue(ColumnPurpose.Numeric, out ComparisonOperator number))
                {
                    setter(number, node);
                }
            }
            else if (type.IsDateTime)
            {
                if (map.TryGetValue(ColumnPurpose.DateTime, out ComparisonOperator datetime))
                {
                    setter(datetime, node);
                }
            }
            else if (type.IsString)
            {
                if (map.TryGetValue(ColumnPurpose.String, out ComparisonOperator _string))
                {
                    setter(_string, node);
                }
            }
        }
        private void Transform(in FunctionExpression node, in Dictionary<ColumnPurpose, ComparisonOperator> map, Action<ComparisonOperator, SyntaxNode> setter)
        {
            if (node.Name == nameof(TYPEOF))
            {
                if (map.TryGetValue(ColumnPurpose.TypeCode, out ComparisonOperator comparison))
                {
                    setter(comparison, node);
                }
            }
            else if (node.Name == nameof(UUIDOF))
            {
                if (map.TryGetValue(ColumnPurpose.Identity, out ComparisonOperator comparison))
                {
                    setter(comparison, node);
                }
            }
        }
        #endregion
    }
}