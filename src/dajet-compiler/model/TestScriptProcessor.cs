namespace DaJet.Compiler
{
    public class Data { public string Value { get; set; } }
    public sealed class TestScriptProcessor : ScriptProcessor
    {
        private readonly Data _data;
        public TestScriptProcessor()
        {
            _data = new Data();

            _data.Value = "test";
        }
        protected override void Process()
        {
            throw new NotImplementedException();
        }
    }
}