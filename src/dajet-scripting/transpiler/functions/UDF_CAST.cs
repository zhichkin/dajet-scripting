using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using System.Text;

namespace DaJet.Scripting
{
    public sealed class UDF_CAST
    {
        public const string Name = "CAST";
        public Type GetReturnType(in FunctionExpression node)
        {
            if (node.Name != UDF_CAST.Name)
            {
                throw new FormatException($"[CAST] invalid mapping {node.Name}");
            }

            if (node.Parameters.Count != 2)
            {
                throw new FormatException("[CAST] invalid number of parameters");
            }

            SyntaxNode target_type = node.Parameters[1];

            if (target_type is not TypeIdentifier type)
            {
                throw new FormatException("[CAST] invalid target type");
            }

            if (type.Identifier == "boolean")
            {
                return typeof(bool);
            }
            else if (type.Identifier == "integer")
            {
                if (type.Qualifier1 == 8)
                {
                    return typeof(long);
                }
                else
                {
                    return typeof(int);
                }
            }
            else if (type.Identifier == "number" || type.Identifier == "decimal")
            {
                return typeof(decimal);
            }
            else if (type.Identifier == "datetime") { return typeof(DateTime); }
            else if (type.Identifier == "string") { return typeof(string); }
            else if (type.Identifier == "binary") { return typeof(byte[]); }
            else if (type.Identifier == "uuid") { return typeof(Guid); }
            else if (type.Identifier == "entity") { return typeof(Entity); }

            return null; // error
        }
        public FunctionDescriptor Transpile(in SqlTranspiler transpiler, in FunctionExpression node, in StringBuilder script)
        {
            if (node.Name != UDF_CAST.Name)
            {
                throw new FormatException($"[CAST] invalid mapping {node.Name}");
            }

            if (node.Parameters.Count != 2)
            {
                throw new FormatException("[CAST] invalid number of parameters");
            }

            FunctionDescriptor descriptor = null;

            SyntaxNode expression = node.Parameters[0];
            SyntaxNode target_type = node.Parameters[1];

            if (target_type is not TypeIdentifier type)
            {
                throw new FormatException("[CAST] invalid target type");
            }

            if (expression is ColumnReference column)
            {
                descriptor = Transpile(in column, in type, in script);
            }
            else
            {
                throw new FormatException("[CAST] unsupported expression type");
            }

            return descriptor;
        }
        private FunctionDescriptor Transpile(in ColumnReference column, in TypeIdentifier type, in StringBuilder script)
        {
            if (column.Binding is not PropertyDefinition property)
            {
                throw new FormatException("[CAST] invalid database column type");
            }

            if (property.Columns.Count == 1)
            {
                return TranspileSimpleProperty(in column, in type, in script);
            }
            
            return TranspileUnionProperty(in column, in type, in script);
        }
        private FunctionDescriptor TranspileSimpleProperty(in ColumnReference column, in TypeIdentifier type, in StringBuilder script)
        {
            //NOTE: Функция реализована для примера преобразования поля _KeyField binary(4) в тип integer.

            if (column.Binding is not PropertyDefinition property)
            {
                throw new FormatException("[CAST] invalid database column type");
            }

            ColumnDefinition map = property.Columns[0];

            if (!map.Type.IsBinary)
            {
                throw new FormatException("[CAST] invalid database column type");
            }

            //if (_target == DatabaseProvider.SqlServer)
            //{
            //    script.Append($"CAST({map.Name} AS int)");
            //}
            //else if (_target == DatabaseProvider.PostgreSql)
            //{
            //    script.Append($"(get_byte({map.Name}, 0) << 24) | (get_byte({map.Name}, 1) << 16) | (get_byte({map.Name}, 2) << 8) | get_byte({map.Name}, 3)");
            //}
            //else
            //{
            //    throw new FormatException($"[CAST] unsupported database {_target}");
            //}

            return null;
        }
        private FunctionDescriptor TranspileUnionProperty(in ColumnReference column, in TypeIdentifier type, in StringBuilder script)
        {
            if (column.Binding is not PropertyDefinition property)
            {
                throw new FormatException("[CAST] invalid database column type");
            }

            UnionTag tag;

            if (type.Identifier == "boolean")
            {
                tag = UnionTag.Boolean; // _L
            }
            else if (type.Identifier == "number")
            {
                tag = UnionTag.Decimal; // _N
            }
            else if (type.Identifier == "datetime")
            {
                tag = UnionTag.DateTime; // _T
            }
            else if (type.Identifier == "string")
            {
                tag = UnionTag.String; // _S
            }
            else if (type.Identifier == "entity")
            {
                tag = UnionTag.Entity; // _RRef
            }
            else
            {
                throw new FormatException($"[CAST] unsupported cast to [{type.Identifier}]");
            }

            ColumnDefinition map = null;

            for (int i = 0; i < property.Columns.Count; i++)
            {
                map = property.Columns[i];

                //if (map.Type == tag)
                //{
                //    map = column.Mapping[i]; break;
                //}
            }

            if (map is not null)
            {
                script.Append(map.Name);
            }
            else
            {
                throw new FormatException($"[CAST] type column [{type.Identifier}] is not found");
            }
            
            return null;
        }
    }
}