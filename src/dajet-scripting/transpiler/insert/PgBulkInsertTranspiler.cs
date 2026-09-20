using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using System.Text;

namespace DaJet.Scripting
{
    public static class PgBulkInsertTranspiler
    {
        public readonly static string TempTableName = "import_table";
        public readonly static string OrderColumnName = "order_column";
        private static bool TryGetSequenceNames(in InsertStatement statement, out string propertyName, out string sequenceName)
        {
            propertyName = null;
            sequenceName = null;

            foreach (ColumnExpression map in statement.Values)
            {
                if (map.Expression is FunctionExpression function && function.Token == Token.VECTOR)
                {
                    propertyName = map.Alias;

                    if (function.Parameters is List<SyntaxNode> parameters
                        && parameters.Count > 0
                        && parameters[0] is ScalarExpression scalar)
                    {
                        sequenceName = scalar.Literal; return true;
                    }
                }
            }

            return false;
        }
        public static string CreateTempTableStatement(in EntityDefinition table)
        {
            StringBuilder sql = new();

            sql.Append("CREATE TEMPORARY TABLE").Append(' ').Append(TempTableName).Append(' ').Append('(');
            sql.Append(OrderColumnName).Append(" integer PRIMARY KEY");

            int count;
            string type;
            ColumnDefinition column;
            List<ColumnDefinition> columns;

            foreach (PropertyDefinition property in table.Properties)
            {
                columns = property.Columns;

                if (columns is null || columns.Count == 0)
                {
                    continue;
                }

                count = property.Columns.Count;

                for (int i = 0; i < count; i++)
                {
                    column = columns[i];

                    if (column.IsGenerated)
                    {
                        continue; //FIXME: _version column in PostgreSQL has integer data type and DEFAULT 0 constraint
                    }

                    sql.Append(',').Append(' ');

                    //FIXME: column metadata is provided by DaJet as SQL Server data types
                    bool boolean = (column.Purpose == ColumnPurpose.Boolean) ||
                        (column.Purpose == ColumnPurpose.Value && property.Type.IsBoolean);
                    
                    type = PgSqlHelper.ToSqlDataType(boolean ? DataType.Boolean : column.Type);

                    if (column.Type.IsString)
                    {
                        type = (column.Type.Size == 0) ? "varchar" : string.Format("{0}({1})", (column.Type.IsFixed) ? "char" : "varchar", column.Type.Size);
                    }

                    sql.Append(column.Name.ToLowerInvariant()).Append(' ').Append(type);
                }
            }

            sql.Append(')').Append(' ').Append("ON COMMIT DROP").Append(';');

            return sql.ToString();
        }
        public static string InsertFromTempTableStatement(in InsertStatement statement)
        {
            if (statement.Target is not TableReference target || target.Binding is not EntityDefinition table)
            {
                throw new InvalidOperationException();
            }

            _ = TryGetSequenceNames(in statement, out string vectorName, out string sequenceName);

            bool first = true;
            StringBuilder insert = new();
            StringBuilder select = new();

            foreach (PropertyDefinition property in table.Properties)
            {
                foreach (ColumnDefinition column in property.Columns)
                {
                    if (column.IsGenerated) { continue; }

                    if (!first)
                    {
                        insert.Append(',').Append(' ');
                        select.Append(',').Append(' ');
                    }

                    insert.Append(column.Name);

                    if (property.Name == vectorName)
                    {
                        select.Append("nextval").Append('(').Append('\'').Append(sequenceName).Append('\'').Append(')');
                    }
                    else
                    {
                        select.Append(column.Name.ToLowerInvariant());

                        if (column.Type.IsString)
                        {
                            select.Append("::").Append(PgSqlHelper.ToSqlDataType(column.Type));
                        }
                    }

                    first = false;
                }
            }

            StringBuilder sql = new();

            sql.Append("INSERT INTO ").Append(table.DbName.ToLowerInvariant()).Append(' ').Append('(');
            sql.Append(insert).Append(')').Append(' ');
            sql.Append("SELECT").Append(' ');
            sql.Append(select).Append(' ').Append("FROM").Append(' ').Append(TempTableName).Append(' ');
            sql.Append("ORDER BY").Append(' ').Append(OrderColumnName).Append(' ').Append("ASC").Append(';');

            return sql.ToString();
        }
    }
}