using Microsoft.Data.SqlClient;
using System.Data;

namespace DaJet.Compiler
{
    public class SelectProcessor
    {
        private readonly ScriptProcessor _context;
        public SelectProcessor(in ScriptProcessor context)
        {
            _context = context;
        }
        public string SqlCommand { get; set; }
        public string ConnectionString { get; set; }
        public void Execute()
        {
            int processed = 0;

            using (SqlConnection connection = new(ConnectionString))
            {
                connection.Open();

                SqlTransaction transaction = connection.BeginTransaction();

                using (SqlCommand command = connection.CreateCommand())
                {
                    command.Connection = connection;
                    command.Transaction = transaction;
                    command.CommandType = CommandType.Text;
                    command.CommandText = SqlCommand;

                    Configure(in command);

                    try
                    {
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                Process(in reader); processed++;
                            }

                            reader.Close();
                        }

                        if (processed > 0)
                        {
                            Synchronize();
                        }

                        transaction.Commit();
                    }
                    catch (Exception error)
                    {
                        try
                        {
                            transaction.Rollback(); throw;
                        }
                        catch
                        {
                            throw error;
                        }
                    }
                    finally
                    {
                        Cleanup();
                    }
                }
            }
        }
        public virtual void Configure(in SqlCommand command) // input
        {
            command.Parameters.Clear();

            command.Parameters.AddWithValue("p0", DBNull.Value);
        }
        public virtual void Process(in SqlDataReader reader) // output
        {
            int ordinal = 0;

            if (reader.IsDBNull(ordinal))
            {
                _context.Variable = string.Empty;
            }

            //bool value;

            //if (reader.GetFieldType(ordinal) == typeof(bool))
            //{
            //    value = reader.GetBoolean(ordinal); // PostgreSql
            //}
            //else
            //{
            //    value = (((byte[])reader.GetValue(ordinal))[0] == 1); // SqlServer
            //}

            // call next IProcessor.Process(); or _context.ProcessNext();
        }
        public virtual void Synchronize()
        {
            // submit batch transaction or throw

            // ISynchronizable.Synchronize();
        }
        public virtual void Cleanup()
        {
            _context.Variable = null; // clear streaming buffer
        }
    }
}