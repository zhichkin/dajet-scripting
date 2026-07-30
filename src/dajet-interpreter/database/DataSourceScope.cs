using System.Data.Common;

namespace DaJet.Data
{
    public abstract class DataSourceScope : IDisposable
    {
        public abstract DataSourceType Type { get; }
        public abstract void Commit();
        public abstract void Rollback();
        public abstract void Dispose();
    }
    public abstract class DataSourceScope<T> : DataSourceScope where T : DbCommand
    {
        public abstract T CreateCommand();
    }
}