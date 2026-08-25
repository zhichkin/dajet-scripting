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
                else if (comparison.Expression1 is ColumnReference column && comparison.Expression2 is TypeReference type) 
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

                    throw new NotSupportedException(
                        "IN operator is not supported for multi-type (union) columns. " +
                        "Use a chain of equality comparisons with bound parameters instead: <column> = @p0 OR <column> = @p1 ...");
                }
            }

            return null; // no transformation is needed
        }
        private bool IsUnionNode(in SyntaxNode node)
        {
            SyntaxNode target = node;

            while (target is GroupOperator group) // (Колонка) - выражение в скобках
            {
                target = group.Expression;
            }

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
        private void ThrowUnableToCompareException(SyntaxNode node1, SyntaxNode node2)
        {
            throw new InvalidCastException($"Unable to compare {node1} and {node2}");
        }

        private SyntaxNode TransformColumnIsType(in ComparisonOperator comparison)
        {
            SyntaxNode unwrapped = comparison.Expression1;

            while (unwrapped is GroupOperator group) // (Колонка) - выражение в скобках
            {
                unwrapped = group.Expression;
            }

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
                { ColumnPurpose.Binary, new ComparisonOperator() { Token = Token.Equals } },
                { ColumnPurpose.TypeCode, new ComparisonOperator() { Token = Token.Equals } },
                { ColumnPurpose.Identity, new ComparisonOperator() { Token = Token.Equals } }
            };

            Transform(comparison.Expression1, in map, SetExpression1);
            Transform(comparison.Expression2, in map, SetExpression2);

            ConfigureTag(in map, in comparison); // _TYPE column
            ConfigureTypeCode(in map, in comparison); // _TRef column

            // Слот сравнения, заполненный только с одной стороны (литерал ссылки {код:guid},
            // строковый или числовой операнд, ISNULL, CASE и т.п.), при сборке результата
            // молча выбрасывается: значение операнда игнорируется, а запрос возвращает
            // правдоподобный неверный результат. Сообщаем об этом явно.

            ThrowIfComparisonPartIsDropped(in map, in comparison);
            ThrowIfFixedEntityTypesDiffer(in map, in comparison);

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
        private static void ThrowIfComparisonPartIsDropped(in Dictionary<ColumnPurpose, ComparisonOperator> map, in ComparisonOperator comparison)
        {
            if (IsTypeReference(comparison.Expression1) || IsTypeReference(comparison.Expression2))
            {
                return; // Оператор IS: сравнение только по коду типа - контракт оператора
            }

            foreach (var item in map)
            {
                ComparisonOperator slot = item.Value;

                if (slot.Expression1 is null == slot.Expression2 is null)
                {
                    continue; // Слот либо не задействован, либо сопоставлен полностью
                }

                throw new NotSupportedException(
                    $"Comparison of {Unwrap(comparison.Expression1)} and {Unwrap(comparison.Expression2)} is not supported: " +
                    $"the {item.Key} part of the comparison has no counterpart and would be silently dropped. " +
                    "Use a bound parameter (@parameter) of a reference type instead; to test the type only, use the IS <type> operator.");
            }
        }
        private static void ThrowIfFixedEntityTypesDiffer(in Dictionary<ColumnPurpose, ComparisonOperator> map, in ComparisonOperator comparison)
        {
            if (map.TryGetValue(ColumnPurpose.Tag, out ComparisonOperator tag)
                && (tag.Expression1 is not null || tag.Expression2 is not null))
            {
                return; // Тип сравнивается по колонке _TYPE
            }

            if (map.TryGetValue(ColumnPurpose.TypeCode, out ComparisonOperator code)
                && (code.Expression1 is not null || code.Expression2 is not null))
            {
                return; // Тип сравнивается по колонке _TRRef
            }

            // Сравнение свелось к равенству только по GUID (Identity). Статический тип
            // надёжно известен только у физических колонок: для UDF (UUIDOF) InferType
            // выбрасывает исключение, а для производных выражений (CASE и т.п.) вывод
            // типа неполон. Прочие формы оставляем как есть - сравнение только по GUID.

            if (Unwrap(comparison.Expression1) is not ColumnReference column1 || column1.Binding is not PropertyDefinition)
            {
                return;
            }

            if (Unwrap(comparison.Expression2) is not ColumnReference column2 || column2.Binding is not PropertyDefinition)
            {
                return;
            }

            // Обе стороны - физические колонки с фиксированным типом ссылки. Если типы
            // различны, равенство ссылок невозможно, а совпадение GUID разных типов
            // дало бы ложное срабатывание.

            DataType type1 = column1.InferType();
            DataType type2 = column2.InferType();

            if (type1.IsEntity && type2.IsEntity
                && type1.TypeCode > 0 && type2.TypeCode > 0
                && type1.TypeCode != type2.TypeCode)
            {
                throw new NotSupportedException(
                    $"Comparison of {Unwrap(comparison.Expression1)} and {Unwrap(comparison.Expression2)} is not supported: " +
                    $"the operands have different fixed entity types ({type1.TypeCode} and {type2.TypeCode}) " +
                    "and no type columns to compare - equality by identity (GUID) alone would be incorrect.");
            }
        }
        private static bool IsTypeReference(in SyntaxNode node)
        {
            return Unwrap(node) is TypeReference;
        }
        private static SyntaxNode Unwrap(in SyntaxNode node)
        {
            SyntaxNode target = node;

            while (target is GroupOperator group) { target = group.Expression; }

            return target;
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
        private void ConfigureTag(in Dictionary<ColumnPurpose, ComparisonOperator> map, in ComparisonOperator comparison)
        {
            if (!map.TryGetValue(ColumnPurpose.Tag, out ComparisonOperator item)) { return; }
            
            if (item.Expression1 is null && item.Expression2 is null) { return; } // Tag column is not used
            
            if (item.Expression1 is not null && item.Expression2 is not null) { return; } // Tag column is mapped already

            DataType target;
            DataType source;

            if (item.Expression1 is null)
            {
                target = comparison.Expression1.InferType();
                source = comparison.Expression2.InferType();
            }
            else
            {
                target = comparison.Expression2.InferType();
                source = comparison.Expression1.InferType();
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
                target = comparison.Expression1.InferType();
            }
            else
            {
                target = comparison.Expression2.InferType();
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
            if (node is GroupOperator group) // (выражение) - разворачиваем скобки
            {
                Transform(group.Expression, in map, setter);
            }
            else if (node is TypeReference type)
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
        }
        private void Transform(in TypeReference node, in Dictionary<ColumnPurpose, ComparisonOperator> map, Action<ComparisonOperator, SyntaxNode> setter)
        {
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
            else if (node.Binding is ColumnExpression derived) // Наследуемый источник данных
            {
                if (derived.Source is not null)
                {
                    PropertyDefinition source = derived.Source;

                    ColumnDefinition column = source.GetColumnByPurpose(ColumnPurpose.Value);

                    column ??= source.GetColumnByPurpose(ColumnPurpose.Identity);

                    if (map.TryGetValue(ColumnPurpose.Identity, out ComparisonOperator identity))
                    {
                        setter(identity, CreateSingleColumnReference(in node, in column));
                    }

                    column = source.GetColumnByPurpose(ColumnPurpose.Tag);

                    if (column is not null)
                    {
                        if (map.TryGetValue(ColumnPurpose.Tag, out ComparisonOperator tag))
                        {
                            setter(tag, CreateSingleColumnReference(in node, in column));
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
                }
                else // expression
                {
                    if (map.TryGetValue(ColumnPurpose.Identity, out ComparisonOperator identity))
                    {
                        setter(identity, node);
                    }
                }
            }
            else if (node.Binding is Entity entity) // enumeration value
            {
                if (map.TryGetValue(ColumnPurpose.Identity, out ComparisonOperator identity))
                {
                    setter(identity, node);

                    //setter(identity, new ScalarExpression()
                    //{
                    //    Token = Token.Uuid,
                    //    Literal = entity.Identity.ToString()
                    //});
                }
            }
        }
        private void Transform(in ScalarExpression node, in Dictionary<ColumnPurpose, ComparisonOperator> map, Action<ComparisonOperator, SyntaxNode> setter)
        {
            DataType type = node.InferType();

            //NOTE: Литерал ссылки {код:guid} не даёт сравнения по Identity - потеря GUID
            //      обнаруживается общей проверкой ThrowIfComparisonPartIsDropped выше по стеку.

            //UnionTag tag = type.IsUuid ? UnionTag.Entity : type.GetSingleTagOrUndefined();

            //if (map.TryGetValue(tag, out ComparisonOperator comparison))
            //{
            //    setter(comparison, node);
            //}
        }
        private void Transform(in VariableReference node, in Dictionary<ColumnPurpose, ComparisonOperator> map, Action<ComparisonOperator, SyntaxNode> setter)
        {
            DataType type = node.InferType();

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
            DataType type = node.InferType();

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
    }
}