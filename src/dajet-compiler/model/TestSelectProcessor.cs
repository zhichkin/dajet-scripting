using Microsoft.Data.SqlClient;

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
        protected override void Initialize()
        {
            throw new NotImplementedException();
        }
        protected override void Configure(SqlCommand command)
        {
            TestScriptProcessor script1 = _script as TestScriptProcessor;

            TestScriptProcessor script2 = (TestScriptProcessor)_script;

            if (_script is not TestScriptProcessor script3)
            {
                return;
            }

            int i = 0;
        }
        protected override void Process(SqlDataReader reader)
        {
            throw new NotImplementedException();
        }
    }
}