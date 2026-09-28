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
            ObjectDisposedException.ThrowIf(_disposed, typeof(PgDataSourceScope));

            base.Synchronize(true);

            _transaction?.Commit();
        }
        public override void Cancel()
        {
            ObjectDisposedException.ThrowIf(_disposed, typeof(PgDataSourceScope));

            try
            {
                _transaction?.Rollback();
            }
            catch
            {
                // do nothing
            }
            finally
            {
                _transaction = null;
            }

            base.Synchronize(false);
        }
        public override void Dispose()
        {
            if (_disposed) { return; }

            try
            {
                _transaction?.Dispose(); //NOTE: rolls back uncommitted transaction
                
                _connection?.Dispose();
                
                base.Disposed();
            }
            catch
            {
                // do nothing
            }
            finally
            {
                _connection = null;
                _transaction = null;
            }

            _disposed = true;
        }
    }
}