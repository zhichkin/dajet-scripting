using System.Data.Common;

namespace DaJet.Data
{
    public abstract class DataSourceScope : IDisposable
    {
        public abstract DataSourceType Type { get; }
        public abstract DbConnection Connection { get; }
        public abstract DbTransaction Transaction { get; }
        public abstract void Commit();
        public abstract void Cancel();
        public abstract void Dispose();
        public event EventHandler OnCommit;
        public event EventHandler OnCancel;
        public event EventHandler OnDispose;
        protected virtual void Disposed()
        {
            OnDispose?.Invoke(this, EventArgs.Empty);
        }
        protected virtual void Synchronize(bool success)
        {
            if (success)
            {
                OnCommit?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                OnCancel?.Invoke(this, EventArgs.Empty);
            }
        }
    }
    public abstract class DataSourceScope<T> : DataSourceScope where T : DbCommand
    {
        public abstract T CreateCommand();
    }
}