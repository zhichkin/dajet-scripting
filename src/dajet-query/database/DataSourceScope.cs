namespace DaJet.Data
{
    public abstract class DataSourceScope : IDisposable
    {
        public abstract DataSourceType Type { get; }
        public abstract void Dispose();
    }
}