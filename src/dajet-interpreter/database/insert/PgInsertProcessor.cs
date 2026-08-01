using DaJet.Data;
using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using Npgsql;
using NpgsqlTypes;
using System.Buffers.Binary;
using System.Data;

namespace DaJet.Scripting
{
    public sealed class PgInsertProcessor : ProcessorBase
    {
        private readonly static byte[] EMPTY_TYPE_CODE = [0x00000000];
        private readonly static byte[] EMPTY_UUID = [0x00000000000000000000000000000000];

        private readonly static byte[] TAG_UNDEFINED = [0x01];
        private readonly static byte[] TAG_BOOLEAN = [0x02];
        private readonly static byte[] TAG_DECIMAL = [0x03];
        private readonly static byte[] TAG_DATETIME = [0x04];
        private readonly static byte[] TAG_STRING = [0x05];
        private readonly static byte[] TAG_ENTITY = [0x08];

        private readonly ScriptContext _context;
        private readonly PgDataSourceScope _dataSource;
        private readonly InsertStatement _statement;
        private readonly EntityDefinition _target;
        private readonly int _yearOffset;
        private readonly byte[] _buffer = new byte[16];
        private readonly Dictionary<ColumnDefinition, NpgsqlParameter> _parameters = new();
        public PgInsertProcessor(in ScriptContext context, in InsertStatement statement)
        {
            if (context.GetDataSource() is not PgDataSourceScope use)
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

            PrepareCommand();
        }
        private void PrepareCommand()
        {
            int ordinal = 0;
            string parameterName;
            NpgsqlParameter parameter;
            ColumnDefinition column;
            List<ColumnDefinition> columns;
            PropertyDefinition property;

            foreach (SyntaxNode node in _statement.Input)
            {
                if (node is not ColumnExpression map)
                {
                    continue;
                }

                property = _target.GetPropertyByName(map.Alias);

                columns = property.Columns;

                for (int c = 0; c < columns.Count; c++)
                {
                    column = columns[c];

                    if (column.IsGenerated)
                    {
                        continue; // timestamp column _Version
                    }

                    parameterName = string.Format("${0}", ++ordinal);

                    parameter = new NpgsqlParameter()
                    {
                        Direction = ParameterDirection.Input
                    };

                    DataType type = column.Type;

                    if (column.Purpose == ColumnPurpose.Value)
                    {
                        if (type.IsBoolean)
                        {
                            parameter.NpgsqlDbType = NpgsqlDbType.Boolean;
                        }
                        else if (type.IsDecimal)
                        {
                            parameter.NpgsqlDbType = NpgsqlDbType.Numeric;
                            parameter.Scale = type.Scale;
                            parameter.Precision = type.Precision;
                        }
                        else if (type.IsDateTime)
                        {
                            parameter.NpgsqlDbType = NpgsqlDbType.Timestamp;
                        }
                        else if (type.IsString)
                        {
                            if (column.Type.Size > 0)
                            {
                                parameter.Size = type.Size;
                            }

                            if (column.Type.IsFixed)
                            {
                                parameter.NpgsqlDbType = NpgsqlDbType.Char;
                            }
                            else
                            {
                                parameter.NpgsqlDbType = NpgsqlDbType.Text;
                            }
                        }
                        else if (type.IsBinary)
                        {
                            parameter.NpgsqlDbType = NpgsqlDbType.Bytea;
                        }
                        else if (type.IsUuid)
                        {
                            parameter.Size = 16;
                            parameter.NpgsqlDbType = NpgsqlDbType.Bytea;
                        }
                        else if (type.IsEntity)
                        {
                            parameter.Size = 16;
                            parameter.NpgsqlDbType = NpgsqlDbType.Bytea;
                        }
                    }
                    else if (column.Purpose == ColumnPurpose.Tag)
                    {
                        parameter.Size = 1;
                        parameter.NpgsqlDbType = NpgsqlDbType.Bytea;
                    }
                    else if (column.Purpose == ColumnPurpose.TypeCode)
                    {
                        parameter.Size = 4;
                        parameter.NpgsqlDbType = NpgsqlDbType.Bytea;
                    }
                    else if (column.Purpose == ColumnPurpose.Identity)
                    {
                        parameter.Size = 16;
                        parameter.NpgsqlDbType = NpgsqlDbType.Bytea;
                    }
                    else if (column.Purpose == ColumnPurpose.Boolean)
                    {
                        parameter.NpgsqlDbType = NpgsqlDbType.Boolean;
                    }
                    else if (column.Purpose == ColumnPurpose.Numeric)
                    {
                        parameter.NpgsqlDbType = NpgsqlDbType.Numeric;
                        parameter.Scale = type.Scale;
                        parameter.Precision = type.Precision;
                    }
                    else if (column.Purpose == ColumnPurpose.DateTime)
                    {
                        parameter.NpgsqlDbType = NpgsqlDbType.Timestamp;
                    }
                    else if (column.Purpose == ColumnPurpose.String)
                    {
                        if (column.Type.Size > 0)
                        {
                            parameter.Size = type.Size;
                        }

                        if (column.Type.IsFixed)
                        {
                            parameter.NpgsqlDbType = NpgsqlDbType.Char;
                        }
                        else
                        {
                            parameter.NpgsqlDbType = NpgsqlDbType.Text;
                        }
                    }

                    _parameters.Add(column, parameter);
                }
            }
        }
        private void SetParameters(in NpgsqlCommand command)
        {
            command.Parameters.Clear();

            foreach (SyntaxNode node in _statement.Input)
            {
                if (node is not ColumnExpression map)
                {
                    continue;
                }

                PropertyDefinition property = _target.GetPropertyByName(map.Alias);

                object value = _context.Evaluate(map.Expression);

                foreach (ColumnDefinition column in property.Columns)
                {
                    if (_parameters.TryGetValue(column, out NpgsqlParameter parameter))
                    {
                        if (value is null)
                        {
                            parameter.Value = DBNull.Value;
                        }
                        else if (column.Purpose == ColumnPurpose.Value)
                        {
                            if (value is bool boolean)
                            {
                                parameter.Value = boolean;
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
                                parameter.Value = boolean;
                            }
                            else
                            {
                                parameter.Value = false;
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
        }
        public override void Process()
        {
            using (NpgsqlCommand command = _dataSource.CreateCommand())
            {
                command.CommandText = _statement.Sql;

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