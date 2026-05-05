namespace DaJet.Compiler
{
    public sealed class TestSelectProcessor : SelectProcessor
    {
        public TestSelectProcessor(TestScriptProcessor script) : base(script)
        {
            //script.Synchronize();
        }

        public override void Cancel()
        {
            base.Cancel();

            Console.WriteLine($"TestSelectProcessor.Cancel() invoked");
        }
    }
}