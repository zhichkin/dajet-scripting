namespace DaJet.Host
{
    public readonly struct RunningTaskInfo
    {
        public RunningTaskInfo(int taskId, string name, string singletonKey)
        {
            TaskId = taskId;
            Name = name ?? string.Empty;
            SingletonKey = singletonKey ?? string.Empty;
        }
        public int TaskId { get; }
        public string Name { get; }
        public string SingletonKey { get; }
        public override string ToString()
        {
            return string.Format("[{0}] {1} {{{2}}}", TaskId, Name, SingletonKey);
        }
    }
}