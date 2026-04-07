namespace DaJet.Compiler
{
    public class ScriptProcessor
    {
        public string Variable { get; set; }
        public void Execute()
        {
            Console.WriteLine($"{typeof(ScriptProcessor)} method {nameof(Execute)} is invoked.");
        }
    }
}