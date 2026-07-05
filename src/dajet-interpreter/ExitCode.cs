namespace DaJet.Scripting
{
    public enum ExitCode : byte
    {
        None,
        Running,
        Success,
        Faulted,
        Return,
        Continue,
        Break,
        Cancel
    }
}