using Microsoft.Data.SqlClient;
using System.Data;

namespace DaJet.Data
{
    public sealed class MsDataSourceScope : DataSourceScope<SqlCommand>
    {
        private bool _disposed;
        private bool _committed;
        private SqlConnection _connection;
        private SqlTransaction _transaction;
        public MsDataSourceScope(string connectionString, bool transactional = false)
        {
            _connection = new SqlConnection(connectionString);

            try
            {
                _connection.Open();

                if (transactional)
                {
                    _transaction = _connection.BeginTransaction();
                }
            }
            catch
            {
                Dispose(); throw;
            }
        }
        public override DataSourceType Type { get { return DataSourceType.SqlServer; } }
        public override SqlConnection Connection { get { return _connection; } }
        public override SqlTransaction Transaction { get { return _transaction; } }
        public override SqlCommand CreateCommand()
        {
            ObjectDisposedException.ThrowIf(_disposed, typeof(MsDataSourceScope));

            SqlCommand command = _connection.CreateCommand();

            command.Connection = _connection;
            command.Transaction = _transaction;
            command.CommandType = CommandType.Text;

            return command;
        }
        public override void Commit()
        {
            ObjectDisposedException.ThrowIf(_disposed, typeof(MsDataSourceScope));

            base.Synchronize(); // Invokes OnCommit event

            //NOTE: Here, we expect the event subscriber to either complete successfully or throw an exception!

            _transaction?.Commit();

            _committed = true;
        }
        public override void Dispose()
        {
            if (_disposed) { return; }

            // Uncommitted (pending) transactions must be rolled back.

            if (!_committed)
            {
                try { _transaction?.Rollback(); }
                catch { /* unrecoverable error */ }
            }

            try { _transaction?.Dispose(); } 
            catch { /* unrecoverable error */ }
            finally { _transaction = null; }

            try { _connection?.Dispose(); }
            catch { /* unrecoverable error */ }
            finally { _connection = null; }

            _disposed = true;

            try
            {
                base.Disposed(); // Invokes OnDispose event
            }
            catch
            {
                //NOTE: Exceptions must be handled by the event subscriber!
            }

            _committed = false;
        }
    }
}