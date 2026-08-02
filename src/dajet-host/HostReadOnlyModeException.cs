namespace DaJet.Host
{
    public sealed class HostReadOnlyModeException : Exception
    {
        public HostReadOnlyModeException() { }
        public HostReadOnlyModeException(string message) : base(message) { }
        public HostReadOnlyModeException(string message, Exception inner) : base(message, inner) { }
    }
}