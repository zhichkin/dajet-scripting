using Microsoft.Data.SqlClient;
using System.Data;

namespace DaJet.Data
{
    public sealed class MsDataSourceScope : DataSourceScope<SqlCommand>
    {
        private bool _disposed;
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

            base.Synchronize(true); // Invokes OnCommit event

            //NOTE: Here, we expect the event subscriber to either complete successfully or throw an exception!

            _transaction?.Commit();
        }
        public override void Cancel()
        {
            ObjectDisposedException.ThrowIf(_disposed, typeof(MsDataSourceScope));

            //THINK: Call Dispose method !?

            try { _transaction?.Rollback(); }
            catch { /* unrecoverable error */ }
            finally { _transaction = null; }
            
            try
            {
                base.Synchronize(false); // Invokes OnCancel event
            }
            catch
            {
                //NOTE: Exceptions must be handled by the event subscriber!
            }
        }
        public override void Dispose()
        {
            if (_disposed) { return; }

            // Uncommitted (pending) transaction will be rolled back.

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
        }
    }
}