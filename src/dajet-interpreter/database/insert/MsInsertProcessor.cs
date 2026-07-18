using DaJet.Data;
using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using Microsoft.Data.SqlClient;
using System.Buffers.Binary;
using System.Data;
using System.Text;

namespace DaJet.Scripting
{
    public sealed class MsInsertProcessor : ProcessorBase
    {
        private readonly static byte[] TRUE = [0x01];
        private readonly static byte[] FALSE = [0x00];
        private readonly static byte[] EMPTY_TYPE_CODE = [0x00000000];
        private readonly static byte[] EMPTY_UUID = [0x00000000000000000000000000000000];
        private readonly static byte[] VALUE_STORAGE = [0x01, 0x01, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xEF, 0xBB, 0xBF, 0x7B, 0x22, 0x55, 0x22, 0x7D];

        // 0x01010800000000000000EFBBBF7B2255227D {"U"} Значение по умолчанию для ХранилищеЗначения

        private readonly static byte[] TAG_UNDEFINED = [0x01];
        private readonly static byte[] TAG_BOOLEAN = [0x02];
        private readonly static byte[] TAG_DECIMAL = [0x03];
        private readonly static byte[] TAG_DATETIME = [0x04];
        private readonly static byte[] TAG_STRING = [0x05];
        private readonly static byte[] TAG_ENTITY = [0x08];

        private readonly ScriptContext _context;
        private readonly MsDataSourceScope _dataSource;
        private readonly InsertStatement _statement;
        private readonly EntityDefinition _target;
        private readonly SelectExpression _source;
        private readonly int _yearOffset;
        private readonly byte[] _buffer = new byte[16];
        private readonly Dictionary<ColumnDefinition, SqlParameter> _parameters = new();
        private readonly string _sql = string.Empty;
        public MsInsertProcessor(in ScriptContext context, in InsertStatement statement)
        {
            if (context.GetDataSource() is not MsDataSourceScope use)
            {
                throw new InvalidOperationException();
            }

            _dataSource = use;
            _context = context;
            _statement = statement;
            _yearOffset = statement.YearOffset;

            if (_statement.Target is not TableReference table || table.Binding is not EntityDefinition target)
            {
                throw new InvalidOperationException();
            }

            if (_statement.Source is not SelectExpression source)
            {
                throw new InvalidOperationException();
            }

            _target = target;
            _source = source;

            PrepareCommand();
            
            _sql = TranspileToSql();
        }
        private void PrepareCommand()
        {
            int ordinal = 0;
            string parameterName;
            SqlParameter parameter;
            ColumnDefinition column;
            List<ColumnDefinition> columns;
            PropertyDefinition property;
            List<PropertyDefinition> properties = _target.Properties;

            for (int p = 0; p < properties.Count; p++)
            {
                property = properties[p];

                columns = property.Columns;

                for (int c = 0; c < columns.Count; c++)
                {
                    column = columns[c];

                    if (column.IsGenerated)
                    {
                        continue; // timestamp column _Version
                    }

                    parameterName = string.Format("p{0}", ordinal++);

                    parameter = new SqlParameter()
                    {
                        ParameterName = parameterName,
                        Direction = ParameterDirection.Input
                    };

                    DataType type = column.Type;

                    if (column.Purpose == ColumnPurpose.Value)
                    {
                        if (type.IsBoolean)
                        {
                            parameter.Size = 1;
                            parameter.SqlDbType = SqlDbType.Binary;
                        }
                        else if (type.IsDecimal)
                        {
                            parameter.SqlDbType = SqlDbType.Decimal;
                            parameter.Scale = type.Scale;
                            parameter.Precision = type.Precision;
                        }
                        else if (type.IsDateTime)
                        {
                            parameter.SqlDbType = SqlDbType.DateTime2;
                        }
                        else if (type.IsString)
                        {
                            if (column.Type.Size > 0)
                            {
                                parameter.Size = column.Type.Size * 2;
                            }
                            else
                            {
                                parameter.Size = 0; // nvarchar(max)
                            }

                            if (column.Type.IsFixed)
                            {
                                parameter.SqlDbType = SqlDbType.NChar;
                            }
                            else
                            {
                                parameter.SqlDbType = SqlDbType.NVarChar;
                            }
                        }
                        else if (type.IsBinary)
                        {
                            parameter.Size = 0; // varbinary(max)
                            parameter.SqlDbType = SqlDbType.Binary;
                        }
                        else if (type.IsUuid)
                        {
                            parameter.Size = 16;
                            parameter.SqlDbType = SqlDbType.Binary;
                        }
                        else if (type.IsEntity)
                        {
                            parameter.Size = 16;
                            parameter.SqlDbType = SqlDbType.Binary;
                        }
                    }
                    else if (column.Purpose == ColumnPurpose.Tag)
                    {
                        parameter.Size = 1;
                        parameter.SqlDbType = SqlDbType.Binary;
                    }
                    else if (column.Purpose == ColumnPurpose.TypeCode)
                    {
                        parameter.Size = 4;
                        parameter.SqlDbType = SqlDbType.Binary;
                    }
                    else if (column.Purpose == ColumnPurpose.Identity)
                    {
                        parameter.Size = 16;
                        parameter.SqlDbType = SqlDbType.Binary;
                    }
                    else if (column.Purpose == ColumnPurpose.Boolean)
                    {
                        parameter.Size = 1;
                        parameter.SqlDbType = SqlDbType.Binary;
                    }
                    else if (column.Purpose == ColumnPurpose.Numeric)
                    {
                        parameter.SqlDbType = SqlDbType.Decimal;
                        parameter.Scale = type.Scale;
                        parameter.Precision = type.Precision;
                    }
                    else if (column.Purpose == ColumnPurpose.DateTime)
                    {
                        parameter.SqlDbType = SqlDbType.DateTime2;
                    }
                    else if (column.Purpose == ColumnPurpose.String)
                    {
                        if (column.Type.Size > 0)
                        {
                            parameter.Size = type.Size * 2;
                        }
                        else
                        {
                            parameter.Size = 0; // nvarchar(max)
                        }

                        if (column.Type.IsFixed)
                        {
                            parameter.SqlDbType = SqlDbType.NChar;
                        }
                        else
                        {
                            parameter.SqlDbType = SqlDbType.NVarChar;
                        }
                    }

                    _parameters.Add(column, parameter);
                }
            }
        }
        private string TranspileToSql()
        {
            StringBuilder sql = new();

            sql.Append("INSERT ").Append(_target.DbName).Append('(');

            StringBuilder columnNames = new();
            StringBuilder parameters = new();

            int ordinal = 0;
            string parameterName;
            ColumnDefinition column;
            List<ColumnDefinition> columns;
            PropertyDefinition property;
            List<PropertyDefinition> properties = _target.Properties;

            for (int p = 0; p < properties.Count; p++)
            {
                property = properties[p];

                columns = property.Columns;

                for (int c = 0; c < columns.Count; c++)
                {
                    column = columns[c];

                    if (column.IsGenerated)
                    {
                        continue; // timestamp column _Version
                    }

                    parameterName = string.Format("@p{0}", ordinal++);

                    if (p > 0)
                    {
                        parameters.Append(',').Append(' ');
                        columnNames.Append(',').Append(' ');
                    }

                    parameters.Append(parameterName);
                    columnNames.Append(column.Name);
                }
            }

            sql.Append(columnNames).Append(')').Append('\n').Append("VALUES (").Append(parameters).Append(')').Append(';');
            
            return sql.ToString();
        }
        private void SetParameters(in SqlCommand command)
        {
            command.Parameters.Clear();

            foreach (PropertyDefinition property in _target.Properties)
            {
                if (_source.TryGetColumn(property.Name, out ColumnExpression map))
                {
                    object value = _context.Evaluate(map.Expression);

                    DataType type = value is null ? DataType.Undefined : DataType.FromType(value.GetType());

                    foreach (ColumnDefinition column in property.Columns)
                    {
                        if (_parameters.TryGetValue(column, out SqlParameter parameter))
                        {
                            if (value is null)
                            {
                                parameter.Value = DBNull.Value;
                            }
                            else if (column.Purpose == ColumnPurpose.Value)
                            {
                                if (value is bool boolean)
                                {
                                    parameter.Value = boolean ? TRUE : FALSE;
                                }
                                else if (value is decimal numeric)
                                {
                                    parameter.Value = numeric;
                                }
                                else if (value is DateTime datetime)
                                {
                                    parameter.Value = datetime.AddYears(_yearOffset);
                                }
                                else if (value is string text)
                                {
                                    parameter.Value = text;
                                }
                                else if (value is byte[] binary)
                                {
                                    parameter.Value = binary;
                                }
                                else if (value is Guid uuid)
                                {
                                    parameter.Value = uuid.ToByteArray();
                                }
                                else if (value is Entity entity)
                                {
                                    parameter.Value = entity.Identity.ToByteArray();
                                }
                                else
                                {
                                    parameter.Value = value; // this might be error
                                }
                            }
                            else if (column.Purpose == ColumnPurpose.Tag)
                            {
                                if (value is null) { parameter.Value = TAG_UNDEFINED; }
                                else if (value is bool) { parameter.Value = TAG_BOOLEAN; }
                                else if (value is decimal) { parameter.Value = TAG_DECIMAL; }
                                else if (value is DateTime) { parameter.Value = TAG_DATETIME; }
                                else if (value is string) { parameter.Value = TAG_STRING; }
                                else
                                {
                                    parameter.Value = TAG_ENTITY;
                                }
                            }
                            else if (column.Purpose == ColumnPurpose.TypeCode)
                            {
                                if (value is Entity entity)
                                {
                                    Span<byte> buffer = _buffer.AsSpan(0, 4);
                                    BinaryPrimitives.WriteInt32BigEndian(buffer, entity.TypeCode);
                                    parameter.Value = buffer.ToArray();
                                }
                                else
                                {
                                    parameter.Value = EMPTY_TYPE_CODE;
                                }
                            }
                            else if (column.Purpose == ColumnPurpose.Identity)
                            {
                                if (value is Entity entity)
                                {
                                    parameter.Value = entity.Identity.ToByteArray();
                                }
                                else
                                {
                                    parameter.Value = EMPTY_UUID;
                                }
                            }
                            else if (column.Purpose == ColumnPurpose.Boolean)
                            {
                                if (value is bool boolean)
                                {
                                    parameter.Value = boolean ? TRUE : FALSE;
                                }
                                else
                                {
                                    parameter.Value = FALSE;
                                }
                            }
                            else if (column.Purpose == ColumnPurpose.Numeric)
                            {
                                if (value is decimal numeric)
                                {
                                    parameter.Value = numeric;
                                }
                                else
                                {
                                    parameter.Value = 0m;
                                }
                            }
                            else if (column.Purpose == ColumnPurpose.DateTime)
                            {
                                if (value is DateTime datetime)
                                {
                                    parameter.Value = datetime.AddYears(_yearOffset);
                                }
                                else
                                {
                                    parameter.Value = DateTime.MinValue.AddYears(_yearOffset);
                                }
                            }
                            else if (column.Purpose == ColumnPurpose.String)
                            {
                                if (value is string text)
                                {
                                    parameter.Value = text;
                                }
                                else
                                {
                                    parameter.Value = string.Empty;
                                }
                            }
                        }

                        command.Parameters.Add(parameter);
                    }
                }
                else
                {
                    SetDefaultValues(in property, in command);
                }
            }
        }
        private void SetDefaultValues(in PropertyDefinition property, in SqlCommand command)
        {
            foreach (ColumnDefinition column in property.Columns)
            {
                if (column.IsGenerated)
                {
                    continue;
                }

                DataType type = column.Type;

                if (_parameters.TryGetValue(column, out SqlParameter parameter))
                {
                    if (column.Purpose == ColumnPurpose.Value)
                    {
                        if (type.IsBoolean) { parameter.Value = FALSE; }
                        else if (type.IsDecimal) { parameter.Value = 0m; }
                        else if (type.IsDateTime) { parameter.Value = DateTime.MinValue.AddYears(_yearOffset); }
                        else if (type.IsString) { parameter.Value = string.Empty; }
                        else if (type.IsBinary) { parameter.Value = VALUE_STORAGE; }
                        else if (type.IsUuid) { parameter.Value = EMPTY_UUID; }
                        else if (type.IsEntity)
                        {
                            parameter.Value = EMPTY_UUID;
                        }
                    }
                    else if (column.Purpose == ColumnPurpose.Tag)
                    {
                        parameter.Value = TAG_UNDEFINED;
                    }
                    else if (column.Purpose == ColumnPurpose.TypeCode)
                    {
                        parameter.Value = EMPTY_TYPE_CODE;
                    }
                    else if (column.Purpose == ColumnPurpose.Identity)
                    {
                        parameter.Value = EMPTY_UUID;
                    }
                    else if (column.Purpose == ColumnPurpose.Boolean)
                    {
                        parameter.Value = FALSE;
                    }
                    else if (column.Purpose == ColumnPurpose.Numeric)
                    {
                        parameter.Value = 0m;
                    }
                    else if (column.Purpose == ColumnPurpose.DateTime)
                    {
                        parameter.Value = DateTime.MinValue.AddYears(_yearOffset);
                    }
                    else if (column.Purpose == ColumnPurpose.String)
                    {
                        parameter.Value = string.Empty;
                    }

                    command.Parameters.Add(parameter);
                }
            }
        }
        public override void Process()
        {
            using (SqlCommand command = _dataSource.CreateCommand())
            {
                command.CommandText = _sql;

                SetParameters(in command);

                int recordsAffected = command.ExecuteNonQuery();
            }
        }
        public override void Dispose()
        {
            // do nothing
        }
    }
}