namespace DaJet.Scripting
{
    public readonly struct ScriptStatus
    {
        public static readonly ScriptStatus Default;
        public ScriptStatus(ExitCode code, DateTime start, DateTime finish, long elapsed)
        {
            Code = code;
            Start = start;
            Finish = finish;
            Duration = elapsed;
        }
        public ExitCode Code { get; }
        public DateTime Start { get; }
        public DateTime Finish { get; }
        public long Duration { get; }
        public override string ToString()
        {
            return $"[{Code}] {Start:yyyy-MM-dd HH:mm:ss:fff} - {Finish:yyyy-MM-dd HH:mm:ss:fff} {{{Duration}}}";
        }
    }
}