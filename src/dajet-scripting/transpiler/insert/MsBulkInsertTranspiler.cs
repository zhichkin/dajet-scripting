using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using System.Text;

namespace DaJet.Scripting
{
    public static class MsBulkInsertTranspiler
    {
        public readonly static string TempTableName = "#tmp";
        public readonly static string TableVariableName = "@tvp";
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

            string typeName = GetTableTypeName(in table);

            string typeExists = SelectTypeStatement(in table);

            sql.Append("CREATE TABLE").Append(' ').Append(TempTableName).Append(' ').Append('(');
            sql.Append(OrderColumnName).Append(" int PRIMARY KEY");

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
                        continue;
                    }

                    sql.Append(',').Append(' ');

                    type = MsSqlHelper.ToSqlDataType(column.Type);

                    sql.Append(column.Name).Append(' ').Append(type);
                }
            }

            sql.Append(')').Append(';');

            return sql.ToString();
        }
        public static string InsertFromTmpStatement(in InsertStatement statement)
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
                        select.Append("NEXT VALUE FOR").Append(' ').Append(sequenceName).Append(' ');
                        select.Append("OVER (ORDER BY").Append(' ').Append(OrderColumnName).Append(' ');
                        select.Append("ASC)");
                    }
                    else
                    {
                        select.Append(column.Name);
                    }

                    first = false;
                }
            }

            StringBuilder sql = new();

            sql.Append("INSERT INTO ");
            sql.Append(table.DbName).Append(' ').Append('(');
            sql.Append(insert).Append(')').Append(' ');
            sql.Append("SELECT").Append(' ');
            sql.Append(select).Append(' ');

            if (string.IsNullOrEmpty(vectorName))
            {
                sql.Append("FROM").Append(' ').Append(TempTableName).Append(' ');
                sql.Append("ORDER BY").Append(' ').Append(OrderColumnName).Append(' ').Append("ASC").Append(';');
            }
            else
            {
                sql.Append("FROM").Append(' ').Append(TempTableName).Append(';');
            }

            return sql.ToString();
        }

        public static string GetTableTypeName(in EntityDefinition table)
        {
            return string.Format("udt{0}_bulk_insert", table.DbName);
        }
        public static string SelectTypeStatement(in EntityDefinition table)
        {
            StringBuilder sql = new();

            string typeName = GetTableTypeName(in table);

            sql.Append("SELECT 1 FROM sys.types WHERE is_user_defined = 1 AND is_table_type = 1 AND name = ");
            sql.Append('\'').Append(typeName).Append('\'');

            return sql.ToString();
        }
        public static string CreateTypeStatement(in EntityDefinition table)
        {
            StringBuilder sql = new();

            string typeName = GetTableTypeName(in table);

            string typeExists = SelectTypeStatement(in table);

            sql.Append("IF NOT EXISTS(").Append(typeExists).Append(')').Append(' ').Append("BEGIN").AppendLine();

            sql.Append("CREATE TYPE").Append(' ').Append(typeName).Append(' ');
            sql.Append("AS TABLE").Append(' ').Append('(');
            sql.Append(OrderColumnName).Append(" int PRIMARY KEY");

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
                        continue;
                    }

                    sql.Append(',').Append(' ');

                    type = MsSqlHelper.ToSqlDataType(column.Type);

                    sql.Append(column.Name).Append(' ').Append(type);
                }
            }

            sql.Append(')').Append(';');

            sql.AppendLine().Append("END"); // IF NOT EXISTS

            return sql.ToString();
        }
        public static string DropTypeStatement(in EntityDefinition table)
        {
            StringBuilder sql = new();

            string typeName = GetTableTypeName(in table);

            sql.Append("DROP TYPE ").Append(typeName).Append(';');

            return sql.ToString();
        }
        public static string InsertFromTvpStatement(in InsertStatement statement)
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
                        select.Append("NEXT VALUE FOR").Append(' ').Append(sequenceName).Append(' ');
                        select.Append("OVER (ORDER BY").Append(' ').Append(OrderColumnName).Append(' ');
                        select.Append("ASC)");
                    }
                    else
                    {
                        select.Append(column.Name);
                    }

                    first = false;
                }
            }

            StringBuilder sql = new();

            sql.Append("INSERT INTO ");
            sql.Append(table.DbName).Append(' ').Append('(');
            sql.Append(insert).Append(')').Append(' ');
            sql.Append("SELECT").Append(' ');
            sql.Append(select).Append(' ');

            if (string.IsNullOrEmpty(vectorName))
            {
                sql.Append("FROM").Append(' ').Append(TableVariableName).Append(' ');
                sql.Append("ORDER BY").Append(' ').Append(OrderColumnName).Append(' ').Append("ASC").Append(';');
            }
            else
            {
                sql.Append("FROM").Append(' ').Append(TableVariableName).Append(';');
            }

            return sql.ToString();
        }
    }
}