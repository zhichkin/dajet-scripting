using Microsoft.Data.SqlClient;
using System.Data;

namespace DaJet.Compiler
{
    public abstract class DbDataSource : IDisposable
    {
        public void Dispose()
        {
            
        }
    }
    public sealed class MsDataSource: IDisposable
    {
        private bool _disposed;
        private SqlConnection _connection;
        private SqlTransaction _transaction;
        public MsDataSource(string connectionString, string isolationLevel)
        {
            _connection = new SqlConnection(connectionString);

            try
            {
                _connection.Open();

                if (!string.IsNullOrEmpty(isolationLevel))
                {
                    _transaction = _connection.BeginTransaction();
                }
            }
            catch
            {
                Dispose(); throw;
            }
        }
        public SqlCommand CreateCommand()
        {
            SqlCommand command = _connection.CreateCommand();

            command.Connection = _connection;
            command.Transaction = _transaction;
            command.CommandType = CommandType.Text;

            return command;
        }
        public void TxBegin()
        {
            _transaction = _connection.BeginTransaction();
        }
        public void TxCommit()
        {
            _transaction?.Commit();
        }
        public void TxRollback()
        {
            _transaction?.Rollback();
        }
        public void Dispose()
        {
            if (_disposed) { return; }

            _transaction?.Dispose();
            _transaction = null;

            _connection?.Dispose();
            _connection = null;

            _disposed = true;

            // ObjectDisposedException.ThrowIf(_disposed, typeof(MsDataContext));
        }
    }
}