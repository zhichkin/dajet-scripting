using DaJet.Data;
using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using Npgsql;
using System.Data;

namespace DaJet.Scripting
{
    public sealed class PgBulkInsertProcessor : ProcessorBase
    {
        private readonly ScriptContext _context;
        private readonly InsertStatement _statement;
        private readonly EntityDefinition _target;
        private readonly string _bufferName;
        private readonly string _bufferItem;
        private readonly string _createTempTable;
        private readonly string _insertTempTable;
        private readonly PgBulkInsertMapper _mapper;
        public PgBulkInsertProcessor(in ScriptContext context, in InsertStatement statement)
        {
            if (context.GetDataSource() is not PgDataSourceScope)
            {
                throw new InvalidOperationException();
            }

            _context = context;
            _statement = statement;

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
            _bufferItem = string.Format("{0}{1}", _bufferName, "__item__");

            _context.CreateVariable(in _bufferItem);

            foreach (ColumnExpression map in _statement.Values)
            {
                List<MemberAccessExpression> members = Visitor.Extract<MemberAccessExpression>(map);

                foreach (MemberAccessExpression member in members)
                {
                    if (member.GetVariableName() == _bufferName)
                    {
                        member.Identifier = member.Identifier.Replace(_bufferName, _bufferItem);
                    }
                }
            }
            
            _createTempTable = PgBulkInsertTranspiler.CreateTempTableStatement(in _target);
            _insertTempTable = PgBulkInsertTranspiler.InsertFromTempTableStatement(in _statement);

            _mapper = new PgBulkInsertMapper(in _context, in _statement, in _bufferItem);
        }
        public override void Dispose()
        {
            _mapper.Dispose();
        }
        public override ExitCode Process()
        {
            if (_context.GetDataSource() is not PgDataSourceScope use)
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

            return ProcessBulkInsert(in use, in buffer);
        }
        private ExitCode ProcessBulkInsert(in PgDataSourceScope use, in List<DataObject> buffer)
        {
            int timeout = _statement.Timeout;

            string tempTable = PgBulkInsertTranspiler.TempTableName;
            string copyCommand = $"COPY {tempTable} FROM STDIN (FORMAT BINARY)";

            using (NpgsqlCommand command = use.CreateCommand())
            {
                command.CommandText = _createTempTable;
                command.CommandType = CommandType.Text;
                command.CommandTimeout = 10; // seconds
                command.ExecuteNonQuery();

                NpgsqlBatch insert = new(use.Connection, use.Transaction)
                {
                    Timeout = timeout,
                    BatchCommands =
                    {
                        new NpgsqlBatchCommand(_insertTempTable),
                        new NpgsqlBatchCommand($"TRUNCATE TABLE {tempTable};")
                    }
                };

                using (_mapper)
                {
                    _ = _mapper.Enumerate(in buffer);

                    while (_mapper.CanRead()) // Next batch to write
                    {
                        using (NpgsqlBinaryImporter importer = use.Connection.BeginBinaryImport(copyCommand))
                        {
                            importer.Timeout = TimeSpan.FromSeconds(timeout);

                            while (_mapper.MoveNext()) // Write batch to temp table
                            {
                                _mapper.WriteRowValues(in importer);
                            }

                            importer.Complete(); // Commit batch write
                        }

                        insert.ExecuteNonQuery();
                    }
                }

                command.CommandText = $"DROP TABLE {tempTable};";
                command.CommandType = CommandType.Text;
                command.CommandTimeout = 10; // seconds
                command.ExecuteNonQuery();
            }

            _context.SetValue(in _bufferItem, null);

            return ExitCode.Success;
        }
    }
}