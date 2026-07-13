namespace DaJet.Host
{
    public readonly struct RunningTaskStatus
    {
        public static readonly RunningTaskStatus Default;
        public RunningTaskStatus(int taskId, string taskStatus, string scriptPath, string singletonKey)
        {
            Id = taskId;
            Status = taskStatus;
            ScriptPath = scriptPath ?? string.Empty;
            SingletonKey = singletonKey ?? string.Empty;
        }
        public int Id { get; }
        public string Status { get; } = string.Empty;
        public string ScriptPath { get; } = string.Empty;
        public string SingletonKey { get; } = string.Empty;
        public override string ToString()
        {
            if (string.IsNullOrEmpty(Status))
            {
                return string.Format("[{0}]", Id);
            }
            else if (string.IsNullOrEmpty(ScriptPath))
            {
                return string.Format("[{0}] {{{1}}}", Id, Status);
            }
            else if (string.IsNullOrEmpty(SingletonKey))
            {
                return string.Format("[{0}] {{{1}}} {2}", Id, Status, ScriptPath);
            }
            else
            {
                return string.Format("[{0}] {{{1}}} {2} \"{3}\"", Id, Status, ScriptPath, SingletonKey);
            }
        }
    }
}