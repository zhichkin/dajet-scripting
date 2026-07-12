namespace DaJet.Host
{
    public readonly struct RunningTaskInfo
    {
        public RunningTaskInfo(int taskId, string scriptPath, string singletonKey)
        {
            TaskId = taskId;
            ScriptPath = scriptPath ?? string.Empty;
            SingletonKey = singletonKey ?? string.Empty;
        }
        public int TaskId { get; }
        public string ScriptPath { get; }
        public string SingletonKey { get; }
        public override string ToString()
        {
            return string.Format("[{0}] {1} {{{2}}}", TaskId, ScriptPath, SingletonKey);
        }
    }
}