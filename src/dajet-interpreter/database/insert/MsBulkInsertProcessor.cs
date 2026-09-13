using DaJet.Data;
using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Runtime.Intrinsics.X86;
using System.Text;
using static Npgsql.Replication.PgOutput.Messages.RelationMessage;

namespace DaJet.Scripting
{
    public sealed class MsBulkInsertProcessor : ProcessorBase
    {
        private readonly ScriptContext _context;
        private readonly InsertStatement _statement;
        private readonly EntityDefinition _target;
        private readonly int _yearOffset;
        private readonly Func<object, object> _convertDateTime;
        private readonly string _bufferName;
        private readonly string _bufferItem;
        private string _createType;
        private string _createTable;
        private string _insertTable;
        private string _dropTable;
        private readonly DataTable _table = new();
        private readonly Dictionary<ColumnDefinition, DataColumn> _map = new();
        private readonly Dictionary<ColumnDefinition, Func<object, object>> _converters = new();
        public MsBulkInsertProcessor(in ScriptContext context, in InsertStatement statement)
        {
            if (context.GetDataSource() is not MsDataSourceScope)
            {
                throw new InvalidOperationException();
            }

            _context = context;
            _statement = statement;
            _yearOffset = statement.YearOffset;

            if (_statement.Target is not TableReference table || table.Binding is not EntityDefinition target)
            {
                throw new InvalidOperationException();
            }

            if (_statement.Source is not VariableReference variable
                || variable.Binding is not DeclareStatement declare
                || !(declare.Type.IsArray && declare.Type.IsObject))
            {
                throw new InvalidOperationException();
            }

            _target = target;

            _bufferName = variable.Identifier;
            _bufferItem = string.Format("{0}{1}", _bufferName, "_Item");

            _context.CreateVariable(in _bufferItem);
            
            _convertDateTime = ConvertDateTimeWithOffset;

            TranspileTvp();
            //TranspileBcp();

            PrepareDataTable();
        }

        private void TranspileTvp()
        {
            string columns = TranspileColumns();

            StringBuilder sql = new();

            sql.Append("INSERT INTO ");
            sql.Append(_target.DbName).Append(' ').Append('(');
            sql.Append(columns);
            sql.Append(')').Append(' ').Append("SELECT").Append(' ');
            sql.Append(columns);
            sql.Append(' ').Append("FROM @stage_table ORDER BY order_column ASC;");

            _insertTable = sql.ToString();
        }
        private string TranspileColumns()
        {
            StringBuilder sql = new();

            int count;
            bool first = true;
            ColumnDefinition column;
            List<ColumnDefinition> columns;

            foreach (PropertyDefinition property in _target.Properties)
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

        private int _rowNumber = 0;
        private object GetRowNumber(object value)
        {
            return _rowNumber;
        }
        private object ConvertDateTimeWithOffset(object value)
        {
            return MsDataMapper.ConvertDateTime(value, _yearOffset);
        }
        private void PrepareDataTable()
        {
            DataColumn primaryKey = new()
            {
                DataType = typeof(int),
                ColumnName = "order_column"
            };

            _table.Columns.Add(primaryKey);

            PropertyDefinition property;
            List<PropertyDefinition> properties = _target.Properties;

            for (int p = 0; p < properties.Count; p++)
            {
                property = properties[p];

                PrepareColumns(in property);
            }
        }
        private void PrepareColumns(in PropertyDefinition property)
        {
            if (_statement.TryGetMapping(property.Name, out ColumnExpression map))
            {
                if (map.Expression is FunctionExpression function && function.Token == Token.VECTOR)
                {
                    ColumnDefinition column = property.Columns[0];

                    DataColumn target = new()
                    {
                        DataType = typeof(int),
                        ColumnName = column.Name
                    };

                    _map.Add(column, target);

                    _table.Columns.Add(target);

                    _converters.Add(column, GetRowNumber);

                    return; // generated by SEQUENCE
                }
                else if (map.Expression is MemberAccessExpression member && member.GetVariableName() == _bufferName)
                {
                    member.Identifier = member.Identifier.Replace(_bufferName, _bufferItem);
                }
            }

            DataType input = property.Type;

            foreach (ColumnDefinition column in property.Columns)
            {
                if (column.IsGenerated)
                {
                    continue; // database auto-generated column
                }
                
                Type type = typeof(byte[]);
                Func<object, object> converter = null;

                DataColumn target = new()
                {
                    ColumnName = column.Name
                };

                if (column.Purpose == ColumnPurpose.Value)
                {
                    if (input.IsBoolean) { converter = MsDataMapper.ConvertBoolean; }
                    else if (input.IsDecimal) { converter = MsDataMapper.ConvertNumeric; type = typeof(decimal); }
                    else if (input.IsInteger) { converter = MsDataMapper.ConvertNumeric; type = typeof(decimal); }
                    else if (input.IsDateTime) { converter = _convertDateTime; type = typeof(DateTime); }
                    else if (input.IsString) { converter = MsDataMapper.ConvertString; type = typeof(string); }
                    else if (input.IsBinary) { converter = MsDataMapper.ConvertBinary; }
                    else if (input.IsUuid) { converter = MsDataMapper.ConvertUuid; }
                    else if (input.IsEntity) { converter = MsDataMapper.ConvertIdentity; }
                }
                else if (column.Purpose == ColumnPurpose.Tag) { converter = MsDataMapper.ConvertTag; }
                else if (column.Purpose == ColumnPurpose.Boolean) { converter = MsDataMapper.ConvertBoolean; }
                else if (column.Purpose == ColumnPurpose.Numeric) { converter = MsDataMapper.ConvertNumeric; type = typeof(decimal); }
                else if (column.Purpose == ColumnPurpose.DateTime) { converter = _convertDateTime; type = typeof(DateTime); }
                else if (column.Purpose == ColumnPurpose.String) { converter = MsDataMapper.ConvertString; type = typeof(string); }
                else if (column.Purpose == ColumnPurpose.TypeCode) { converter = MsDataMapper.ConvertTypeCode; }
                else if (column.Purpose == ColumnPurpose.Identity) { converter = MsDataMapper.ConvertIdentity; }

                target.DataType = type;

                _map.Add(column, target);

                _table.Columns.Add(target);

                _converters.Add(column, converter);
            }
        }
        private void SetDataTableRowValues(in DataRow row)
        {
            row["order_column"] = GetRowNumber(null);

            foreach (PropertyDefinition property in _target.Properties)
            {
                object value = null; // default value

                if (_statement.TryGetMapping(property.Name, out ColumnExpression map))
                {
                    if (map.Expression is FunctionExpression function && function.Token == Token.VECTOR)
                    {
                        value = 0; //TODO: row sequence number
                    }
                    else
                    {
                        value = _context.Evaluate(map.Expression);
                    }
                }

                foreach (ColumnDefinition column in property.Columns)
                {
                    if (!_map.TryGetValue(column, out DataColumn target))
                    {
                        continue; // database auto-generated column
                    }

                    row[target] = _converters[column](value);
                }
            }
        }

        public override ExitCode Process()
        {
            if (_context.GetDataSource() is not MsDataSourceScope use)
            {
                throw new InvalidOperationException();
            }

            if (_context.GetValue(in _bufferName) is not List<DataObject> buffer)
            {
                throw new InvalidOperationException();
            }

            if (buffer.Count == 0)
            {
                return ExitCode.Success;
            }

            return ProcessTvp(in use, in buffer);
            //return ProcessBcp(in use, in buffer);
            
            //return ExitCode.Success;
        }
        private ExitCode ProcessTvp(in MsDataSourceScope use, in List<DataObject> buffer)
        {
            _table.Rows.Clear();

            using (SqlCommand command = use.CreateCommand())
            {
                //command.CommandText = _createType;
                //command.CommandType = CommandType.Text;
                //command.CommandTimeout = 10; // seconds
                //command.ExecuteNonQuery();

                command.CommandText = _insertTable;
                command.CommandType = CommandType.Text;
                command.CommandTimeout = 600; // seconds
                SqlParameter tvp = command.Parameters.Add("@stage_table", SqlDbType.Structured);
                //tvp.SqlDbType = SqlDbType.Structured;
                tvp.TypeName = "dbo.udt" + _target.DbName;

                _rowNumber = 0;

                foreach (DataObject record in buffer)
                {
                    _context.SetValue(in _bufferItem, record);

                    DataRow row = _table.NewRow();

                    _table.Rows.Add(row);

                    _rowNumber++;

                    SetDataTableRowValues(in row);

                    if (_table.Rows.Count == 100)
                    {
                        command.Parameters["@stage_table"].Value = _table;
                        
                        command.ExecuteNonQuery();
                        
                        _table.Rows.Clear();
                    }
                }
            }

            _context.SetValue(in _bufferItem, null);

            return ExitCode.Success;
        }

        private void TranspileBcp()
        {
            _createTable = CreateTable();

            string columns = TranspileColumns();

            StringBuilder sql = new();
            sql.Append("INSERT INTO ");
            sql.Append(_target.DbName).Append(' ').Append('(');
            sql.Append(columns);
            sql.Append(')').Append(' ').Append("SELECT").Append(' ');
            sql.Append(columns);
            sql.Append(' ').Append("FROM #stage_table ORDER BY order_column ASC;");
            _insertTable = sql.ToString();

            _dropTable = "DROP TABLE #stage_table;";
        }
        private string CreateTable()
        {
            StringBuilder sql = new();

            sql.Append("CREATE TABLE ").Append("#stage_table").Append(' ').Append('(');
            sql.Append("order_column int PRIMARY KEY");

            int count;
            ColumnDefinition column;
            List<ColumnDefinition> columns;

            foreach (PropertyDefinition property in _target.Properties)
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

                    sql.Append(column.Name).Append(' ').Append(MsSqlHelper.ToSqlDataType(column.Type));
                }
            }

            sql.Append(')').Append(';');

            return sql.ToString();
        }
        private ExitCode ProcessBcp(in MsDataSourceScope use, in List<DataObject> buffer)
        {
            _table.Rows.Clear();

            _rowNumber = 0;

            foreach (DataObject record in buffer)
            {
                _context.SetValue(in _bufferItem, record);

                DataRow row = _table.NewRow();

                _table.Rows.Add(row);

                _rowNumber++;

                SetDataTableRowValues(in row);
            }

            _context.SetValue(in _bufferItem, null);

            using (SqlCommand command = use.CreateCommand())
            {
                command.CommandText = _createTable;
                command.CommandType = CommandType.Text;
                command.CommandTimeout = 10; // seconds
                command.ExecuteNonQuery();

                try
                {
                    SqlBulkCopyOptions options = SqlBulkCopyOptions.TableLock;

                    using (SqlBulkCopy insert = new(use.Connection, options, use.Transaction))
                    {
                        insert.BatchSize = 100; //TODO: statement option
                        insert.BulkCopyTimeout = 600; //TODO: statement option
                        insert.DestinationTableName = "#stage_table"; // _target.DbName;

                        insert.ColumnMappings.Add("order_column", "order_column");

                        foreach (var mapping in _map)
                        {
                            insert.ColumnMappings.Add(mapping.Key.Name, mapping.Value.ColumnName);
                        }

                        insert.ColumnOrderHints.Add("order_column", SortOrder.Ascending);

                        insert.WriteToServer(_table);

                        // int count = insert.RowsCopied;
                    }
                }
                catch
                {
                    throw;
                }
                finally
                {
                    _table.Rows.Clear();
                }

                command.CommandText = _insertTable;
                command.CommandType = CommandType.Text;
                command.CommandTimeout = 600; // seconds
                command.ExecuteNonQuery();

                command.CommandText = _dropTable;
                command.CommandType = CommandType.Text;
                command.CommandTimeout = 600; // seconds
                command.ExecuteNonQuery();
            }

            return ExitCode.Success;
        }
        public override void Dispose()
        {
            // do nothing
        }

        
        //internal override IEnumerable<ConfigFileBuffer> Stream(string tableName, string[] fileNames)
        //{
        //    using (SqlConnection connection = new(_connectionString))
        //    {
        //        connection.Open();

        //        using (SqlCommand command = connection.CreateCommand())
        //        {
        //            command.CommandText = "CREATE TABLE #ConfigFileNames (FileName nvarchar(128) NOT NULL);";
        //            command.CommandType = CommandType.Text;
        //            command.CommandTimeout = 10; // seconds
        //            command.ExecuteNonQuery();

        //            using (SqlBulkCopy insert = new(connection))
        //            {
        //                insert.DestinationTableName = "#ConfigFileNames";
        //                DataTable table = CreateFileNamesTable(in fileNames);
        //                insert.WriteToServer(table);
        //            }

        //            command.CommandText = tableName == ConfigTables.Config
        //                ? MS_CONFIG_STREAM_SCRIPT
        //                : MS_CONFIG_CAS_STREAM_SCRIPT;

        //            command.CommandType = CommandType.Text;
        //            command.CommandTimeout = 60; // seconds

        //            using (SqlDataReader reader = command.ExecuteReader())
        //            {
        //                while (reader.Read())
        //                {
        //                    using (ConfigFileBuffer buffer = new(reader))
        //                    {
        //                        yield return buffer;
        //                    }
        //                }
        //                reader.Close();
        //            }
        //        }
        //    }
        //}
    }
}

//CREATE TABLE #test
//(
//   НомерСообщения int PRIMARY KEY,
//   ТелоСообщения nvarchar(10)
//);

//INSERT #test WITH (TABLOCK) VALUES (1, N'test 1'), (2, N'test 2'), (3, N'test 3');

//SELECT NEXT VALUE FOR so_import OVER (ORDER BY НомерСообщения), ТелоСообщения FROM #test;

//DROP TABLE #test;