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

            _transaction?.Commit();
        }
        public override void Rollback()
        {
            ObjectDisposedException.ThrowIf(_disposed, typeof(PgDataSourceScope));

            try
            {
                _transaction?.Rollback();
            }
            finally
            {
                _transaction = null;
            }
        }
        public override void Dispose()
        {
            if (_disposed) { return; }

            _transaction?.Dispose(); //NOTE: rolls back uncommitted transaction
            _transaction = null;

            _connection?.Dispose();
            _connection = null;

            _disposed = true;
        }
    }
}