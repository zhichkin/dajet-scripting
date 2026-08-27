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
                if (IsUnionNode(comparison.Expression1))
                {
                    return TransformColumnIsType(in comparison);
                }
                else if (comparison.Expression1 is ColumnReference column && comparison.Expression2 is TypeReference type && type.Type.IsObject) 
                {
                    // Колонка не составного типа данных, не являющаяся выражением
                    // Например: WHERE Номенклатура IS Справочник.Номенклатура

                    PropertyDefinition source = column.InferSource();

                    if (string.IsNullOrEmpty(source.Name))
                    {
                        return null; // Константа, функция, параметр или выражение
                    }

                    if (source.Type.IsUnion || !source.Type.IsEntity)
                    {
                        return null; // Финальная проверка на всякий случай
                    }

                    // Это порождает такой код, как (0x00000035 = 0x00000035), что не является ошибкой

                    comparison.Token = Token.Equals;

                    return TransformUnionComparison(in comparison); //FIXME: consider more appropriate method names
                }
            }
            else if (comparison.Token == Token.Equals)
            {
                if (IsUnionNode(comparison.Expression1) || IsUnionNode(comparison.Expression2))
                {
                    return TransformUnionComparison(in comparison);
                }
            }
            else if (comparison.Token == Token.IN)
            {
                if (IsUnionNode(comparison.Expression1))
                {
                    // Преобразование оператора IN для составных типов данных не реализовано:
                    // без трансформации колонка транспилируется в несколько колонок СУБД,
                    // что порождает некорректный SQL. Сообщаем об этом явно.

                    throw new NotSupportedException($"[ComparisonOperatorTransformer][{comparison.Expression1}] IN operator is not supported for union type columns. " +
                        "Use a chain of equality comparisons with bound parameters instead: <column> = @p0 OR <column> = @p1 ...");
                }
            }

            return null; // no transformation is needed
        }
        private static bool IsUnionNode(in SyntaxNode node)
        {
            //NOTE: на всякий случчай. Вдруг кто-то решит написать как-то вот так: WHERE (<column>) = @value
            SyntaxNode target = UnwrapGroupOperator(in node); 

            if (target is not ColumnReference)
            {
                return false; // Константа, функция, параметр или выражение
            }
            
            PropertyDefinition source = target.InferSource();
            
            if (string.IsNullOrEmpty(source.Name))
            {
                return false; // Константа, функция, параметр или выражение
            }

            return source.Type.IsUnion; // Источник данных - колонка таблицы СУБД
        }
        private static SyntaxNode UnwrapGroupOperator(in SyntaxNode node)
        {
            SyntaxNode target = node;

            while (target is GroupOperator group)
            {
                target = group.Expression;
            }

            return target;
        }
        private static void ThrowUnableToCompareException(SyntaxNode node1, SyntaxNode node2)
        {
            throw new InvalidCastException($"[ComparisonOperatorTransformer] Unable to compare {node1} and {node2}");
        }

        private SyntaxNode TransformColumnIsType(in ComparisonOperator comparison)
        {
            //NOTE: на всякий случчай. Вдруг кто-то решит написать как-то вот так: WHERE (<column>) IS <type>
            SyntaxNode unwrapped = UnwrapGroupOperator(comparison.Expression1);

            if (unwrapped is not ColumnReference left)
            {
                return null; // no transformation is needed
            }

            SyntaxNode right = comparison.Expression2;

            UnaryOperator unary = right as UnaryOperator; // NOT

            if (unary is not null)
            {
                // Выражение вида Регистратор IS NOT Документ.Перемещение
                // трансформируем в NOT Регистратор IS Документ.Перемещение

                right = unary.Expression;
            }

            if (right is ScalarExpression scalar && scalar.Token == Token.NULL) // _Fld_TYPE IS [NOT] NULL
            {
                //NOTE: Для сравнения на NULL берём первую колонку свойства составного типа данных
                //TODO: _Fld_TYPE = 0x01 Неопределено

                if (left.Binding is PropertyDefinition property)
                {
                    left.Binding = new PropertyDefinition()
                    {
                        Columns = [property.Columns[0]]
                    };
                }
                else if (left.Binding is ColumnExpression derived)
                {
                    if (derived.Source is not null) // Источник данных - колонка таблицы СУБД
                    {
                        ColumnDefinition source = derived.Source.Columns[0];

                        comparison.Expression1 = CreateSingleColumnReference(in left, in source);
                    }
                }

                return null; // no more transformation is needed
            }

            if (right is not TypeReference)
            {
                throw new FormatException($"IS operator: right operand must be a valid data type identifier.");
            }

            comparison.Token = Token.Equals; // Преобразуем оператор IS в = (равно)

            if (unary is not null)
            {
                comparison.Expression2 = right;
            }

            SyntaxNode node = TransformUnionComparison(in comparison); //NOTE: node is GroupOperator

            if (unary is not null)
            {
                node = new UnaryOperator()
                {
                    Token = Token.NOT,
                    Expression = node
                };
            }

            return node;
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
                { ColumnPurpose.TypeCode, new ComparisonOperator() { Token = Token.Equals } },
                { ColumnPurpose.Identity, new ComparisonOperator() { Token = Token.Equals } }
            };

            Transform(comparison.Expression1, in map, SetExpression1);
            Transform(comparison.Expression2, in map, SetExpression2);

            ValidateUnionComparisonOrThrow(in comparison, in map);

            GroupOperator group = new();

            foreach (var item in map)
            {
                if (group.Expression is null)
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

            if (group.Expression is null) //NOTE: проверка на всякий случай
            {
                ThrowUnableToCompareException(comparison.Expression1, comparison.Expression2);
            }

            return group;
        }
        private static void ValidateUnionComparisonOrThrow(in ComparisonOperator comparison, in Dictionary<ColumnPurpose, ComparisonOperator> map)
        {
            List<ColumnPurpose> toRemove = new();

            foreach (var item in map)
            {
                ComparisonOperator slot = item.Value;

                if (slot.Expression1 is null || slot.Expression2 is null)
                {
                    toRemove.Add(item.Key);
                }
            }

            foreach (ColumnPurpose purpose in toRemove)
            {
                _ = map.Remove(purpose);
            }

            if (map.Count == 0)
            {
                ThrowUnableToCompareException(comparison.Expression1, comparison.Expression2);
            }

            if (map.Count == 1)
            {
                //NOTE: Special case: <union> = TYPEOF(NULL) - проверка на _TYPE = 0x01 (Неопределено)
                if (comparison.Expression2 is FunctionExpression function && function.Name == nameof(TYPEOF))
                {
                    if (function.Parameters is not null && function.Parameters.Count == 1)
                    {
                        SyntaxNode parameter = function.Parameters[0];

                        if (parameter is ScalarExpression scalar && scalar.Token == Token.NULL)
                        {
                            if (map.TryGetValue(ColumnPurpose.Tag, out _))
                            {
                                return; // valid comparison
                            }

                            ThrowUnableToCompareException(comparison.Expression1, comparison.Expression2);
                        }
                    }
                }

                if (comparison.Expression2 is not TypeReference) //NOTE: Special case: <union> IS <type>
                {
                    ThrowUnableToCompareException(comparison.Expression1, comparison.Expression2);
                }

                if (map.TryGetValue(ColumnPurpose.Tag, out _)) { return; } // СоставнойТип IS boolean (_TYPE = 0x02)

                if (map.TryGetValue(ColumnPurpose.TypeCode, out _)) { return; } // Регистратор IS Документ.Приход (_RTRef = 0x0000003C)

                ThrowUnableToCompareException(comparison.Expression1, comparison.Expression2);
            }
        }
        private void SetExpression1(ComparisonOperator comparison, SyntaxNode value)
        {
            comparison.Expression1 = value;
        }
        private void SetExpression2(ComparisonOperator comparison, SyntaxNode value)
        {
            comparison.Expression2 = value;
        }
        private static ColumnReference CreateSingleColumnReference(in ColumnReference column, in ColumnDefinition source)
        {
            string columnName = string.Format("{0}_{1}", column.ColumnName, source.Purpose.GetSuffix());

            string identifier = string.Format("{0}_{1}", column.Identifier, source.Purpose.GetSuffix());

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
        private void Transform(in SyntaxNode node, in Dictionary<ColumnPurpose, ComparisonOperator> map, Action<ComparisonOperator, SyntaxNode> setter)
        {
            if (node is TypeReference type)
            {
                Transform(in type, map, setter);
            }
            else if (node is ColumnReference column)
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
            else if (node is GroupOperator group) // (<expression>) - разворачиваем скобки
            {
                Transform(group.Expression, in map, setter);
            }
        }
        private void Transform(in TypeReference node, in Dictionary<ColumnPurpose, ComparisonOperator> map, Action<ComparisonOperator, SyntaxNode> setter)
        {
            if (!map.TryGetValue(ColumnPurpose.Tag, out ComparisonOperator tag))
            {
                throw new InvalidOperationException("[ComparisonOperatorTransformer] tag slot is missing");
            }

            string tagValue = "0x01";
            DataType type = node.Type;

            if (type.IsBoolean) { tagValue = "0x02"; }
            else if (type.IsDecimal) { tagValue = "0x03"; }
            else if (type.IsDateTime) { tagValue = "0x04"; }
            else if (type.IsString) { tagValue = "0x05"; }
            else if (type.IsEntity) { tagValue = "0x08"; }
            else if (type.IsObject) { tagValue = "0x08"; }
            else
            {
                throw new InvalidOperationException($"[ComparisonOperatorTransformer][{node}] unsupported data type to compare");
            }

            setter(tag, new ScalarExpression() { Token = Token.Binary, Literal = tagValue });

            if (!type.IsObject) { return; } // this is not metadata object

            if (node.Binding is not MetadataEntry entity)
            {
                return; // binding error
            }

            if (map.TryGetValue(ColumnPurpose.TypeCode, out ComparisonOperator comparison))
            {
                setter(comparison, new ScalarExpression()
                {
                    Token = Token.Binary,
                    Literal = $"0x{Convert.ToHexString(DbUtilities.GetByteArray(entity.Code))}"
                });
            }
        }
        private void Transform(in ColumnReference node, in Dictionary<ColumnPurpose, ComparisonOperator> map, Action<ComparisonOperator, SyntaxNode> setter)
        {
            PropertyDefinition binding;

            if (node.Binding is PropertyDefinition property) // Прямой источник данных
            {
                ColumnDefinition column = property.GetColumnByPurpose(ColumnPurpose.Value);

                if (column is not null)
                {
                    string tagValue = "0x01";
                    DataType type = property.Type;
                    ColumnPurpose purpose = ColumnPurpose.Value;

                    if (type.IsBoolean) { purpose = ColumnPurpose.Boolean; tagValue = "0x02"; }
                    else if (type.IsDecimal) { purpose = ColumnPurpose.Numeric; tagValue = "0x03"; }
                    else if (type.IsDateTime) { purpose = ColumnPurpose.DateTime; tagValue = "0x04"; }
                    else if (type.IsString) { purpose = ColumnPurpose.String; tagValue = "0x05"; }
                    else if (type.IsEntity) { purpose = ColumnPurpose.Identity; tagValue = "0x08"; }
                    else
                    {
                        throw new InvalidOperationException($"[ComparisonOperatorTransformer][{node}] unsupported data type to compare");
                    }

                    if (map.TryGetValue(ColumnPurpose.Tag, out ComparisonOperator tag))
                    {
                        setter(tag, new ScalarExpression() { Token = Token.Binary, Literal = tagValue });
                    }

                    if (purpose == ColumnPurpose.Identity)
                    {
                        if (map.TryGetValue(ColumnPurpose.TypeCode, out ComparisonOperator typecode))
                        {
                            setter(typecode, new ScalarExpression()
                            {
                                Token = Token.Binary,
                                Literal = $"0x{Convert.ToHexString(DbUtilities.GetByteArray(type.TypeCode))}"
                            });
                        }
                    }

                    if (map.TryGetValue(purpose, out ComparisonOperator value))
                    {
                        setter(value, node);
                    }
                    
                    return;
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

                column = property.GetColumnByPurpose(ColumnPurpose.Boolean);

                if (column is not null)
                {
                    if (map.TryGetValue(ColumnPurpose.Boolean, out ComparisonOperator boolean))
                    {
                        binding = new PropertyDefinition() { Columns = [column] };

                        ColumnReference expression = new()
                        {
                            Binding = binding,
                            Identifier = node.Identifier
                        };

                        setter(boolean, expression);
                    }
                }

                column = property.GetColumnByPurpose(ColumnPurpose.Numeric);

                if (column is not null)
                {
                    if (map.TryGetValue(ColumnPurpose.Numeric, out ComparisonOperator numeric))
                    {
                        binding = new PropertyDefinition() { Columns = [column] };

                        ColumnReference expression = new()
                        {
                            Binding = binding,
                            Identifier = node.Identifier
                        };

                        setter(numeric, expression);
                    }
                }

                column = property.GetColumnByPurpose(ColumnPurpose.DateTime);

                if (column is not null)
                {
                    if (map.TryGetValue(ColumnPurpose.DateTime, out ComparisonOperator datetime))
                    {
                        binding = new PropertyDefinition() { Columns = [column] };

                        ColumnReference expression = new()
                        {
                            Binding = binding,
                            Identifier = node.Identifier
                        };

                        setter(datetime, expression);
                    }
                }

                column = property.GetColumnByPurpose(ColumnPurpose.String);

                if (column is not null)
                {
                    if (map.TryGetValue(ColumnPurpose.String, out ComparisonOperator _string))
                    {
                        binding = new PropertyDefinition() { Columns = [column] };

                        ColumnReference expression = new()
                        {
                            Binding = binding,
                            Identifier = node.Identifier
                        };

                        setter(_string, expression);
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

                column = property.GetColumnByPurpose(ColumnPurpose.Identity);

                if (column is not null)
                {
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
                }
            }
            else if (node.Binding is ColumnExpression derived) // Наследуемый источник данных
            {
                if (derived.Source is not null)
                {
                    PropertyDefinition source = derived.Source;

                    ColumnDefinition column = source.GetColumnByPurpose(ColumnPurpose.Value);

                    if (column is not null)
                    {
                        string tagValue = "0x01";
                        DataType type = source.Type;
                        ColumnPurpose purpose = ColumnPurpose.Value;

                        if (type.IsBoolean) { purpose = ColumnPurpose.Boolean; tagValue = "0x02"; }
                        else if (type.IsDecimal) { purpose = ColumnPurpose.Numeric; tagValue = "0x03"; }
                        else if (type.IsDateTime) { purpose = ColumnPurpose.DateTime; tagValue = "0x04"; }
                        else if (type.IsString) { purpose = ColumnPurpose.String; tagValue = "0x05"; }
                        else if (type.IsEntity) { purpose = ColumnPurpose.Identity; tagValue = "0x08"; }
                        else
                        {
                            throw new InvalidOperationException($"[ComparisonOperatorTransformer][{node}] unsupported data type to compare");
                        }

                        if (map.TryGetValue(ColumnPurpose.Tag, out ComparisonOperator tag))
                        {
                            setter(tag, new ScalarExpression() { Token = Token.Binary, Literal = tagValue });
                        }

                        if (purpose == ColumnPurpose.Identity)
                        {
                            if (map.TryGetValue(ColumnPurpose.TypeCode, out ComparisonOperator typecode))
                            {
                                setter(typecode, new ScalarExpression()
                                {
                                    Token = Token.Binary,
                                    Literal = $"0x{Convert.ToHexString(DbUtilities.GetByteArray(type.TypeCode))}"
                                });
                            }
                        }

                        if (map.TryGetValue(purpose, out ComparisonOperator value))
                        {
                            setter(value, node);
                        }

                        return;
                    }

                    column = source.GetColumnByPurpose(ColumnPurpose.Tag);

                    if (column is not null)
                    {
                        if (map.TryGetValue(ColumnPurpose.Tag, out ComparisonOperator tag))
                        {
                            setter(tag, CreateSingleColumnReference(in node, in column));
                        }
                    }

                    column = source.GetColumnByPurpose(ColumnPurpose.Boolean);

                    if (column is not null)
                    {
                        if (map.TryGetValue(ColumnPurpose.Boolean, out ComparisonOperator boolean))
                        {
                            setter(boolean, CreateSingleColumnReference(in node, in column));
                        }
                    }

                    column = source.GetColumnByPurpose(ColumnPurpose.Numeric);

                    if (column is not null)
                    {
                        if (map.TryGetValue(ColumnPurpose.Numeric, out ComparisonOperator numeric))
                        {
                            setter(numeric, CreateSingleColumnReference(in node, in column));
                        }
                    }

                    column = source.GetColumnByPurpose(ColumnPurpose.DateTime);

                    if (column is not null)
                    {
                        if (map.TryGetValue(ColumnPurpose.DateTime, out ComparisonOperator datetime))
                        {
                            setter(datetime, CreateSingleColumnReference(in node, in column));
                        }
                    }

                    column = source.GetColumnByPurpose(ColumnPurpose.String);

                    if (column is not null)
                    {
                        if (map.TryGetValue(ColumnPurpose.String, out ComparisonOperator _string))
                        {
                            setter(_string, CreateSingleColumnReference(in node, in column));
                        }
                    }

                    column = source.GetColumnByPurpose(ColumnPurpose.TypeCode);

                    if (column is not null)
                    {
                        if (map.TryGetValue(ColumnPurpose.TypeCode, out ComparisonOperator typecode))
                        {
                            setter(typecode, CreateSingleColumnReference(in node, in column));
                        }
                    }

                    column = source.GetColumnByPurpose(ColumnPurpose.Identity);

                    if (column is not null)
                    {
                        if (map.TryGetValue(ColumnPurpose.Identity, out ComparisonOperator identity))
                        {
                            setter(identity, CreateSingleColumnReference(in node, in column));
                        }
                    }
                }
                else
                {
                    //NOTE: не реализовано: функция или выражение, например, CASE
                }
            }
            else if (node.Binding is Entity entity) // enumeration value
            {
                //NOTE: значение перечисления, например: СоставнойТип = Перечисление.СтавкиНДС.БезНДС

                if (map.TryGetValue(ColumnPurpose.Tag, out ComparisonOperator tag))
                {
                    setter(tag, new ScalarExpression() { Token = Token.Binary, Literal = "0x08" });
                }
                
                if (map.TryGetValue(ColumnPurpose.TypeCode, out ComparisonOperator typecode))
                {
                    setter(typecode, new ScalarExpression()
                    {
                        Token = Token.Binary,
                        Literal = $"0x{Convert.ToHexString(DbUtilities.GetByteArray(entity.TypeCode))}"
                    });
                }

                if (map.TryGetValue(ColumnPurpose.Identity, out ComparisonOperator identity))
                {
                    Guid uuid = UuidConverter.CONVERT_UUID_1C_TO_DB(entity.Identity);

                    setter(identity, new ScalarExpression()
                    {
                        Token = Token.Binary,
                        Literal = $"0x{Convert.ToHexString(uuid.ToByteArray())}"
                    });
                }
            }
        }
        private void Transform(in ScalarExpression node, in Dictionary<ColumnPurpose, ComparisonOperator> map, Action<ComparisonOperator, SyntaxNode> setter)
        {
            if (!map.TryGetValue(ColumnPurpose.Tag, out ComparisonOperator tag))
            {
                throw new InvalidOperationException("[ComparisonOperatorTransformer] tag slot is missing");
            }

            if (node.Token == Token.Boolean)
            {
                setter(tag, new ScalarExpression() { Token = Token.Binary, Literal = "0x02" });

                if (map.TryGetValue(ColumnPurpose.Boolean, out ComparisonOperator boolean))
                {
                    setter(boolean, node);
                }
            }
            else if (node.Token == Token.Decimal || node.Token == Token.Integer)
            {
                setter(tag, new ScalarExpression() { Token = Token.Binary, Literal = "0x03" });

                if (map.TryGetValue(ColumnPurpose.Numeric, out ComparisonOperator number))
                {
                    setter(number, node);
                }
            }
            else if (node.Token == Token.DateTime)
            {
                setter(tag, new ScalarExpression() { Token = Token.Binary, Literal = "0x04" });

                if (map.TryGetValue(ColumnPurpose.DateTime, out ComparisonOperator datetime))
                {
                    setter(datetime, node);
                }
            }
            else if (node.Token == Token.String)
            {
                setter(tag, new ScalarExpression() { Token = Token.Binary, Literal = "0x05" });

                if (map.TryGetValue(ColumnPurpose.String, out ComparisonOperator _string))
                {
                    setter(_string, node);
                }
            }
            else if (node.Token == Token.Entity)
            {
                Entity entity = Entity.Parse(node.Literal);

                setter(tag, new ScalarExpression() { Token = Token.Binary, Literal = "0x08" });

                if (map.TryGetValue(ColumnPurpose.TypeCode, out ComparisonOperator typecode))
                {
                    setter(typecode, new ScalarExpression()
                    {
                        Token = Token.Binary,
                        Literal = $"0x{Convert.ToHexString(DbUtilities.GetByteArray(entity.TypeCode))}"
                    });
                }

                if (map.TryGetValue(ColumnPurpose.Identity, out ComparisonOperator identity))
                {
                    setter(identity, new ScalarExpression()
                    {
                        Token = Token.Binary,
                        Literal = $"0x{Convert.ToHexString(entity.Identity.ToByteArray())}"
                    });
                }
            }
            else
            {
                throw new InvalidCastException($"[ComparisonOperatorTransformer] unsupported literal '{node.Literal}'");
            }
        }
        private void Transform(in VariableReference node, in Dictionary<ColumnPurpose, ComparisonOperator> map, Action<ComparisonOperator, SyntaxNode> setter)
        {
            if (!map.TryGetValue(ColumnPurpose.Tag, out ComparisonOperator tag))
            {
                throw new InvalidOperationException("[ComparisonOperatorTransformer] tag slot is missing");
            }

            DataType type = node.InferType();

            if (type.IsEntity)
            {
                setter(tag, new ScalarExpression() { Token = Token.Binary, Literal = "0x08" });

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
            else if (type.IsBoolean)
            {
                setter(tag, new ScalarExpression() { Token = Token.Binary, Literal = "0x02" });

                if (map.TryGetValue(ColumnPurpose.Boolean, out ComparisonOperator boolean))
                {
                    setter(boolean, node);
                }
            }
            else if (type.IsDecimal)
            {
                setter(tag, new ScalarExpression() { Token = Token.Binary, Literal = "0x03" });

                if (map.TryGetValue(ColumnPurpose.Numeric, out ComparisonOperator number))
                {
                    setter(number, node);
                }
            }
            else if (type.IsDateTime)
            {
                setter(tag, new ScalarExpression() { Token = Token.Binary, Literal = "0x04" });

                if (map.TryGetValue(ColumnPurpose.DateTime, out ComparisonOperator datetime))
                {
                    setter(datetime, node);
                }
            }
            else if (type.IsString)
            {
                setter(tag, new ScalarExpression() { Token = Token.Binary, Literal = "0x05" });

                if (map.TryGetValue(ColumnPurpose.String, out ComparisonOperator _string))
                {
                    setter(_string, node);
                }
            }
            
            //else if (type.IsUuid)
            //{
            //    setter(tag, new ScalarExpression() { Token = Token.Binary, Literal = "0x08" });

            //    if (map.TryGetValue(ColumnPurpose.Identity, out ComparisonOperator uuid))
            //    {
            //        setter(uuid, new FunctionExpression()
            //        {
            //            Token = Token.UDF,
            //            Name = nameof(UUIDOF),
            //            Parameters = { node }
            //        });
            //    }
            //}
        }
        private void Transform(in MemberAccessExpression node, in Dictionary<ColumnPurpose, ComparisonOperator> map, Action<ComparisonOperator, SyntaxNode> setter)
        {
            if (!map.TryGetValue(ColumnPurpose.Tag, out ComparisonOperator tag))
            {
                throw new InvalidOperationException("[ComparisonOperatorTransformer] tag slot is missing");
            }

            DataType type = node.InferType();

            if (type.IsEntity)
            {
                setter(tag, new ScalarExpression() { Token = Token.Binary, Literal = "0x08" });

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
            else if (type.IsBoolean)
            {
                setter(tag, new ScalarExpression() { Token = Token.Binary, Literal = "0x02" });

                if (map.TryGetValue(ColumnPurpose.Boolean, out ComparisonOperator boolean))
                {
                    setter(boolean, node);
                }
            }
            else if (type.IsDecimal)
            {
                setter(tag, new ScalarExpression() { Token = Token.Binary, Literal = "0x03" });

                if (map.TryGetValue(ColumnPurpose.Numeric, out ComparisonOperator number))
                {
                    setter(number, node);
                }
            }
            else if (type.IsDateTime)
            {
                setter(tag, new ScalarExpression() { Token = Token.Binary, Literal = "0x04" });

                if (map.TryGetValue(ColumnPurpose.DateTime, out ComparisonOperator datetime))
                {
                    setter(datetime, node);
                }
            }
            else if (type.IsString)
            {
                setter(tag, new ScalarExpression() { Token = Token.Binary, Literal = "0x05" });

                if (map.TryGetValue(ColumnPurpose.String, out ComparisonOperator _string))
                {
                    setter(_string, node);
                }
            }

            //else if (type.IsUuid)
            //{
            //    setter(tag, new ScalarExpression() { Token = Token.Binary, Literal = "0x08" });

            //    if (map.TryGetValue(ColumnPurpose.Identity, out ComparisonOperator uuid))
            //    {
            //        setter(uuid, new FunctionExpression()
            //        {
            //            Token = Token.UDF,
            //            Name = nameof(UUIDOF),
            //            Parameters = { node }
            //        });
            //    }
            //}
        }
        private void Transform(in FunctionExpression node, in Dictionary<ColumnPurpose, ComparisonOperator> map, Action<ComparisonOperator, SyntaxNode> setter)
        {
            //NOTE: обработка исключительных недокументированных случаев:
            // 1. СоставнойТип = TYPEOF(NULL)   - проверка _TYPE = 0x01
            // 2. СоставнойТип = TYPEOF(entity) - извлечение кода типа ссылки
            // 3. СоставнойТип = UUIDOF(entity) - извлечение идентификатора ссылки

            if (!map.TryGetValue(ColumnPurpose.Tag, out ComparisonOperator tag))
            {
                throw new InvalidOperationException("[ComparisonOperatorTransformer] tag slot is missing");
            }

            SyntaxNode parameter = node.Parameters[0];

            DataType type = parameter.InferType();

            if (node.Name == nameof(TYPEOF))
            {
                if (parameter is ScalarExpression scalar && scalar.Token == Token.NULL)
                {
                    setter(tag, new ScalarExpression() { Token = Token.Binary, Literal = "0x01" });
                }
                else if (type.IsEntity)
                {
                    setter(tag, new ScalarExpression() { Token = Token.Binary, Literal = "0x08" });

                    if (map.TryGetValue(ColumnPurpose.TypeCode, out ComparisonOperator typecode))
                    {
                        setter(typecode, node);
                    }
                }
            }
            else if (node.Name == nameof(UUIDOF))
            {
                setter(tag, new ScalarExpression() { Token = Token.Binary, Literal = "0x08" });

                if (type.IsEntity)
                {
                    if (map.TryGetValue(ColumnPurpose.Identity, out ComparisonOperator comparison))
                    {
                        setter(comparison, node);
                    }
                }
            }
        }
    }
}