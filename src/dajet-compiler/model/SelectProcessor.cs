using DaJet.TypeSystem;
using Microsoft.Data.SqlClient;
using System.Data;

namespace DaJet.Compiler
{
    public abstract class SelectProcessor : ProcessorBase
    {
        protected readonly static byte[] TRUE = [0x01];
        protected readonly static byte[] FALSE = [0x00];
        
        protected readonly ScriptProcessor _script;
        protected SelectProcessor(ScriptProcessor script)
        {
            _script = script;
        }
        public int YearOffset { get; set; }
        public string SqlCommand { get; set; }
        public override void Execute()
        {
            Setup();

            int processed = 0; //TODO: @@ROWCOUNT

            MsDataSource source = _script.GetMsDataSource();

            using (SqlCommand command = source.CreateCommand())
            {
                command.CommandText = SqlCommand;

                Configure(command);

                try
                {
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            Process(reader); processed++;
                        }

                        reader.Close();
                    }

                    if (processed > 0)
                    {
                        Synchronize();
                    }
                }
                //catch (Exception error)
                //{
                //    try
                //    {
                //        source.TxRollback(); throw;
                //    }
                //    catch
                //    {
                //        throw error;
                //    }
                //}
                finally
                {
                    //Cancel(); ?
                }
            }
        }
        protected virtual void Setup()
        {
            // prepare output buffer
        }
        protected virtual void Configure(SqlCommand command) // input
        {
            bool value = true;
            command.Parameters.Clear();
            if (value)
            {
                command.Parameters.AddWithValue("boolean", new byte[1] { 0x01 });
            }
            else
            {
                command.Parameters.AddWithValue("boolean", new byte[1] { 0x00 });
            }
            //command.Parameters.AddWithValue("boolean", true);
            command.Parameters.AddWithValue("integer", 1234);
            command.Parameters.AddWithValue("decimal", 5_000_000_000_000.2M);
            
            SqlParameter parameter = command.Parameters.AddWithValue("datetime", DateTime.Now);
            parameter.SqlDbType = SqlDbType.DateTime2;

            command.Parameters.AddWithValue("string", "test");
            command.Parameters.AddWithValue("binary", new byte[16]);
            command.Parameters.AddWithValue("uuid", Guid.NewGuid());
            command.Parameters.AddWithValue("entity", Entity.Undefined.Identity.ToByteArray());
        }
        protected virtual void Process(SqlDataReader reader) // output
        {
            byte[] buffer = new byte[16];

            Union value;

            if (reader.IsDBNull(0))
            {
                value = Union.Undefined;
            }
            else
            {
                byte tag = ((byte[])reader.GetValue(0))[0];

                switch (tag)
                {
                    case 1:
                        value = Union.Undefined;
                        break;
                    case 2:
                        value = reader.IsDBNull(1) ? false : reader.GetBoolean(1);
                        break;
                    case 3:
                        value = reader.IsDBNull(2) ? 0M : reader.GetDecimal(2);
                        break;
                    case 4:
                        value = reader.IsDBNull(3) ? DateTime.MinValue : reader.GetDateTime(3);
                        break;
                    case 5:
                        value = reader.IsDBNull(4) ? string.Empty : reader.GetString(4);
                        break;
                    default:
                        reader.GetBytes(5, 0L, buffer, 0, 16);
                        value = new Entity(123, new Guid(buffer));
                        break;
                }
            }
        }
        protected virtual void Synchronize()
        {
            // submit batch transaction or throw

            // ISynchronizable.Synchronize();
        }

        public override void Cancel()
        {
            Console.WriteLine($"SelectProcessor.Cancel() invoked");

            //_context.Variable = null; // clear output buffer
        }
    }
}