using Microsoft.Data.SqlClient;

namespace DaJet.Compiler
{
    public sealed class SelectIntoArrayProcessor : SelectProcessor
    {
        private ScriptProcessor _context;
        public SelectIntoArrayProcessor(ScriptProcessor context)
        {
            _context = context;
        }
        protected override void Configure(SqlCommand command) // input
        {
            command.Parameters.Clear();

            command.Parameters.AddWithValue("p0", DBNull.Value);
            command.Parameters.AddWithValue("p1", true);
            command.Parameters.AddWithValue("p2", 12.34M);
            command.Parameters.AddWithValue("p3", DateTime.Now);
            command.Parameters.AddWithValue("p4", "string");
            command.Parameters.AddWithValue("p5", Guid.Empty);
            //_ = command.Parameters.AddWithValue("p6", _context.Variable);
        }
    }
}