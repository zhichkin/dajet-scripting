using DaJet.Data.PostgreSql;
using Npgsql;
using System.Data;

namespace DaJet.Data
{
    public sealed class PgDataSourceScope : DataSourceScope<NpgsqlCommand>
    {
        private bool _disposed;
        private NpgsqlConnection _connection;
        private NpgsqlTransaction _transaction;
        public PgDataSourceScope(string connectionString, bool transactional = false)
        {
            _connection = PgDataSourceFactory.CreateConnection(in connectionString);

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
        public override DataSourceType Type { get { return DataSourceType.PostgreSql; } }
        public override NpgsqlConnection Connection { get { return _connection; } }
        public override NpgsqlTransaction Transaction { get { return _transaction; } }
        public override NpgsqlCommand CreateCommand()
        {
            ObjectDisposedException.ThrowIf(_disposed, typeof(PgDataSourceScope));

            NpgsqlCommand command = _connection.CreateCommand();

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