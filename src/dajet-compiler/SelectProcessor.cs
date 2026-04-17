using DaJet.TypeSystem;
using Microsoft.Data.SqlClient;
using System.Buffers.Binary;
using System.ComponentModel.Design;
using System.Data;

namespace DaJet.Compiler
{
    public abstract class SelectProcessor
    {
        public int YearOffset { get; set; }
        public string SqlCommand { get; set; }
        public string ConnectionString { get; set; }
        public void Execute()
        {
            Setup();

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
        protected virtual void Setup()
        {
            // prepare output buffer
        }
        protected virtual void Configure(SqlCommand command) // input
        {
            // set command parameters
        }
        protected virtual void Process(SqlDataReader reader) // output
        {
            // map data and set output buffer values

            int ordinal = 0;

            if (reader.IsDBNull(ordinal))
            {
                
            }
            else
            {
                byte[] buffer = new byte[16];
                _ = reader.GetBytes(0, 0L, buffer, 0, 1);
                byte tag = buffer[0];

                if (tag == 1) // Неопределено
                {


                }
                else if (tag == 2) // Булево
                {
                    bool result = true;
                }
                else if (tag == 3) // Булево
                {

                }
                else
                {

                }
            }
        }
        protected virtual void Synchronize()
        {
            // submit batch transaction or throw

            // ISynchronizable.Synchronize();
        }
        protected virtual void Cleanup()
        {
            //_context.Variable = null; // clear output buffer
        }
    }
}