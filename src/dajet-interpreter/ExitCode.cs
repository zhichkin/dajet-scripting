namespace DaJet.Scripting
{
    public enum ExitCode : byte
    {
        None,
        Success,
        Faulted,
        Return,
        Break,
        Continue,
        Cancel
    }
}