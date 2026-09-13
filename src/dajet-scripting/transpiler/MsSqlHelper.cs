using DaJet.TypeSystem;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Text;
using System.Xml.Linq;

namespace DaJet.Scripting
{
    public sealed class MsSqlHelper
    {
        private readonly string _connectionString;
        public MsSqlHelper(in string connectionString)
        {
            _connectionString = connectionString;
        }
        private static string GetSelectIndexesScript()
        {
            StringBuilder script = new();

            script.AppendLine(@"SELECT");
            script.AppendLine(@"i.index_id AS index_id,");
            script.AppendLine(@"i.name AS index_name,");
            script.AppendLine(@"ic.key_ordinal AS column_ordinal,");
            script.AppendLine(@"c.name AS column_name,");
            script.AppendLine(@"[column_type] = CASE");
            script.AppendLine(@"WHEN dt.[name] IN ('varchar', 'char', 'binary', 'varbinary') THEN dt.[name] + '(' + IIF(c.max_length = -1, 'max', CAST(c.max_length AS VARCHAR(25))) + ')'");
            script.AppendLine(@"WHEN dt.[name] IN ('nvarchar', 'nchar') THEN dt.[name] + '(' + IIF(c.max_length = -1, 'max', CAST(c.max_length / 2 AS VARCHAR(25))) + ')'");
            script.AppendLine(@"WHEN dt.[name] IN ('decimal', 'numeric') THEN dt.[name] + '(' + CAST(c.[precision] AS VARCHAR(25)) + ',' + CAST(c.[scale] AS VARCHAR(25)) + ')'");
            script.AppendLine(@"ELSE dt.[name] END,");
            script.AppendLine(@"c.is_nullable AS is_nullable,");
            script.AppendLine(@"ic.is_descending_key AS is_descending,");
            script.AppendLine(@"i.is_unique AS is_unique,");
            script.AppendLine(@"i.is_primary_key AS is_primary,");
            script.AppendLine(@"CASE WHEN i.type = 1 THEN CAST(0x01 AS bit) ELSE CAST(0x00 AS bit) END AS is_clustered");
            script.AppendLine(@"FROM sys.indexes AS i");
            script.AppendLine(@"INNER JOIN sys.tables AS t ON t.object_id = i.object_id");
            script.AppendLine(@"INNER JOIN sys.index_columns AS ic ON ic.object_id = t.object_id AND ic.index_id = i.index_id");
            script.AppendLine(@"INNER JOIN sys.columns AS c ON c.object_id = t.object_id AND c.column_id = ic.column_id");
            script.AppendLine(@"INNER JOIN sys.types dt ON dt.system_type_id = dt.user_type_id AND c.user_type_id = dt.user_type_id");
            script.AppendLine(@"WHERE t.object_id = OBJECT_ID(@table_name) AND i.type = 1"); // CLUSTERED
            script.AppendLine(@"ORDER BY i.index_id ASC, ic.key_ordinal ASC;");

            return script.ToString();
        }
        public List<IndexInfo> GetIndexes(in string tableName)
        {
            List<IndexInfo> list = new();

            using (SqlConnection connection = new(_connectionString))
            {
                connection.Open();

                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandType = CommandType.Text;
                    command.CommandText = GetSelectIndexesScript();
                    command.Parameters.AddWithValue("table_name", tableName);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        int current_id = 0;
                        IndexInfo index = null;
                        IndexColumnInfo column = null;

                        while (reader.Read())
                        {
                            int index_id = (int)reader.GetValue("index_id");

                            if (current_id != index_id)
                            {
                                index = new IndexInfo(
                                    reader.GetString("index_name"),
                                    reader.GetBoolean("is_unique"),
                                    reader.GetBoolean("is_primary"),
                                    reader.GetBoolean("is_clustered"));

                                list.Add(index);

                                current_id = index_id;
                            }

                            column = new IndexColumnInfo(
                                reader.GetString("column_name"),
                                reader.GetString("column_type"),
                                reader.GetByte("column_ordinal"),
                                false,
                                reader.GetBoolean("is_nullable"),
                                reader.GetBoolean("is_descending"));

                            index.Columns.Add(column);
                        }
                        reader.Close();
                    }
                }
            }

            return list;
        }

        public static string ToSqlDataType(DataType type)
        {
            if (type.IsBoolean) { return "binary(1)"; }
            else if (type.IsDecimal) { return string.Format("numeric({0},{1})", type.Precision, type.Scale); }
            else if (type.IsDateTime) { return "datetime2"; }
            else if (type.IsString) { return (type.Size == 0) ? "nvarchar(max)" : string.Format("{0}({1})", (type.IsFixed) ? "nchar" : "nvarchar", type.Size); }
            else if (type.IsBinary) { return (type.Size == 0) ? "varbinary(max)" : string.Format("binary({0})", type.Size); }
            else if (type.IsUuid) { return "binary(16)"; }
            else if (type.IsEntity) { return "binary(16)"; }
            else if (type.IsInteger) { return (type.Size == 4) ? "int" : "bigint"; }

            throw new InvalidOperationException("Failed to map DaJet data type to SQL data type.");
        }

        public static string CreateTable(in EntityDefinition metadata, in string name)
        {
            StringBuilder sql = new();

            sql.Append("CREATE TABLE ").Append(name).Append(' ').Append('(');

            int count;
            bool first = true;
            ColumnDefinition column;
            List<ColumnDefinition> columns;

            foreach (PropertyDefinition property in metadata.Properties)
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

                    if (!first) { sql.Append(',').Append(' '); }
                    
                    sql.Append(column.Name).Append(' ').Append(ToSqlDataType(column.Type));

                    if (column.IsPrimaryKey)
                    {
                        sql.Append(' ').Append("PRIMARY KEY");
                    }

                    first = false;
                }
            }

            sql.Append(')').Append(';');

            return sql.ToString();
        }
        public static string CreateType(in EntityDefinition metadata, in string name)
        {
            StringBuilder sql = new();

            sql.Append("CREATE TYPE").Append(' ').Append(name).Append(' ').Append("AS TABLE").Append(' ').Append('(');

            int count;
            bool first = true;
            ColumnDefinition column;
            List<ColumnDefinition> columns;

            foreach (PropertyDefinition property in metadata.Properties)
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

                    if (!first) { sql.Append(',').Append(' '); }

                    sql.Append(column.Name).Append(' ').Append(ToSqlDataType(column.Type));

                    if (column.IsPrimaryKey)
                    {
                        sql.Append(' ').Append("PRIMARY KEY");
                    }

                    first = false;
                }
            }

            sql.Append(')').Append(';');

            return sql.ToString();
        }
        private static void DeclareTableColumn(in PropertyDefinition property, in StringBuilder sql)
        {
            ColumnDefinition column;

            for (int i = 0; i < property.Columns.Count; i++)
            {
                column = property.Columns[i];

                if (i > 0) { sql.Append(", "); }

                string alias = property.Name;

                if (property.Columns.Count == 1) // single column
                {
                    sql.Append(alias);
                }
                else // multiple columns
                {
                    sql.Append(alias).Append('_').Append(column.Purpose.GetSuffix());
                }

                sql.Append(' ').Append(ToSqlDataType(column.Type));
            }
        }
        public static string SelectColumns(in EntityDefinition table)
        {
            StringBuilder sql = new();

            int count;
            bool first = true;
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

                    if (!first) { sql.Append(',').Append(' '); }

                    sql.Append(column.Name);

                    first = false;
                }
            }

            return sql.ToString();
        }
    }

    public sealed class IndexInfo
    {
        public IndexInfo(string name, bool unique, bool primary, bool clustered)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            IsUnique = unique;
            IsPrimary = primary;
            IsClustered = clustered; //  1 - CLUSTERED, 2 - NONCLUSTERED
        }
        public string Name { get; private set; }
        public bool IsUnique { get; private set; }
        public bool IsPrimary { get; private set; }
        public bool IsClustered { get; private set; }
        public List<IndexColumnInfo> Columns { get; } = new List<IndexColumnInfo>();
        public List<IndexColumnInfo> Includes { get; } = new List<IndexColumnInfo>();
        public override string ToString() { return Name; }
    }
    public sealed class IndexColumnInfo
    {
        public IndexColumnInfo(string name, string type, byte ordinal, bool included, bool nullable, bool descending)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            TypeName = type;
            KeyOrdinal = ordinal;
            IsIncluded = included;  //  1 - CLUSTERED, 2 - NONCLUSTERED
            IsNullable = nullable;
            IsDescending = descending;
        }
        public string Name { get; private set; }
        public string TypeName { get; private set; }
        public byte KeyOrdinal { get; private set; }
        public bool IsIncluded { get; private set; }
        public bool IsNullable { get; private set; }
        public bool IsDescending { get; private set; } // 0 - ASC, 1 - DESC
        public override string ToString() { return Name; }
    }
}