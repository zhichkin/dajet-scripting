namespace DaJet.Compiler
{
    public sealed class TestProcessor : SelectProcessor
    {
        private readonly ScriptProcessor _script; // context ???
        public TestProcessor(ScriptProcessor script) : base(script)
        {
            _script = script; //TODO: ???
        }
    }
}