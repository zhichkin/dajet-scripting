using DaJet.Data;
using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using Microsoft.Data.SqlClient;
using System.Data;

namespace DaJet.Scripting
{
    public sealed class MsBulkInsertProcessor : ProcessorBase
    {
        private readonly ScriptContext _context;
        private readonly InsertStatement _statement;
        private readonly EntityDefinition _target;
        private readonly string _bufferName;
        private readonly string _bufferItem;
        private readonly string _insertTvpTable;
        private readonly string _createTempTable;
        private readonly string _insertTempTable;
        private readonly MsBulkInsertMapper _mapper;
        public MsBulkInsertProcessor(in ScriptContext context, in InsertStatement statement)
        {
            if (context.GetDataSource() is not MsDataSourceScope)
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
            
            //_insertTvpTable = MsBulkInsertTranspiler.InsertFromTvpStatement(in _statement);
            
            _createTempTable = MsBulkInsertTranspiler.CreateTempTableStatement(in _target);
            _insertTempTable = MsBulkInsertTranspiler.InsertFromTmpStatement(in _statement);

            _mapper = new MsBulkInsertMapper(in _context, in _statement, in _bufferItem);
        }
        public override void Dispose()
        {
            _mapper.Dispose();
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

            return ProcessTmpInsert(in use, in buffer);

            //return ProcessTvpInsert(in use, in buffer);
        }
        private ExitCode ProcessTmpInsert(in MsDataSourceScope use, in List<DataObject> buffer)
        {
            int timeout = _statement.Timeout;

            IDataReader reader = _mapper.GetDataReader(in buffer);
            
            using (SqlCommand command = use.CreateCommand())
            {
                command.CommandText = _createTempTable;
                command.CommandType = CommandType.Text;
                command.CommandTimeout = 10; // seconds
                command.ExecuteNonQuery();

                string tempTable = MsBulkInsertTranspiler.TempTableName;
                SqlBulkCopyOptions options = SqlBulkCopyOptions.TableLock | SqlBulkCopyOptions.CacheMetadata;

                using (SqlBulkCopy insert = new(use.Connection, options, use.Transaction))
                {
                    insert.BulkCopyTimeout = timeout;
                    insert.DestinationTableName = tempTable;
                    insert.ColumnOrderHints.Add("order_column", SortOrder.Ascending);

                    while (_mapper.CanRead())
                    {
                        insert.WriteToServer(reader);

                        command.CommandText = _insertTempTable;
                        command.CommandType = CommandType.Text;
                        command.CommandTimeout = timeout; // seconds
                        command.ExecuteNonQuery();

                        command.CommandText = $"TRUNCATE TABLE {tempTable};";
                        command.CommandType = CommandType.Text;
                        command.CommandTimeout = 10; // seconds
                        command.ExecuteNonQuery();
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
        private ExitCode ProcessTvpInsert(in MsDataSourceScope use, in List<DataObject> buffer)
        {
            //NOTE: Необходимо использовать команду: CREATE TYPE РегистрСведений.ВходящаяОчередь

            int timeout = _statement.Timeout;

            using (SqlCommand command = use.CreateCommand())
            {
                command.CommandText = _insertTvpTable;
                command.CommandType = CommandType.Text;
                command.CommandTimeout = timeout; // seconds
                SqlParameter tvp = new()
                {
                    SqlDbType = SqlDbType.Structured,
                    TypeName = MsBulkInsertTranspiler.GetTableTypeName(in _target),
                    ParameterName = MsBulkInsertTranspiler.TableVariableName,
                    Value = _mapper.Enumerate(in buffer)
                };
                command.Parameters.Add(tvp);

                while (_mapper.CanRead())
                {
                    command.ExecuteNonQuery();
                }
            }

            _context.SetValue(in _bufferItem, null);

            return ExitCode.Success;
        }
    }
}