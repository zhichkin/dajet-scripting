using DaJet.Metadata;
using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using System.Text;

namespace DaJet.Scripting
{
    public sealed class PgSequenceTranspiler : SqlTranspiler
    {
        private StringBuilder _script;
        private MetadataProvider _provider;
        public override bool TryTranspile(in SyntaxNode statement, in MetadataProvider provider, out string error)
        {
            ArgumentNullException.ThrowIfNull(provider, nameof(provider));
            ArgumentNullException.ThrowIfNull(statement, nameof(statement));

            error = null;
            _provider = provider;
            _script = new StringBuilder();
            
            try
            {
                Transpile(in statement);
            }
            catch (Exception exception)
            {
                error = ExceptionHelper.GetErrorMessage(exception);
            }

            _script = null;
            _provider = null;

            return error is null;
        }
        public override void Visit(in SyntaxNode statement, in StringBuilder script)
        {
            throw new NotImplementedException();
        }
        private void Transpile(in SyntaxNode statement)
        {
            if (statement is CreateSequenceStatement create)
            {
                Transpile(in create);
            }
            else if (statement is ApplySequenceStatement apply)
            {
                Transpile(in apply);
            }
            else if (statement is RevokeSequenceStatement revoke)
            {
                Transpile(in revoke);
            }
            else if (statement is DropSequenceStatement drop)
            {
                Transpile(in drop);
            }
            else
            {
                throw new InvalidOperationException($"Invalid sequence statement: {statement.GetType()}");
            }
        }
        private void Transpile(in CreateSequenceStatement statement)
        {
            // CREATE SEQUENCE IF NOT EXISTS {SEQUENCE_NAME} AS bigint INCREMENT BY 1 START WITH 1 CACHE 1;

            _script.Append("CREATE SEQUENCE IF NOT EXISTS ").Append(statement.Identifier)
                .Append(" AS bigint")
                .Append(" INCREMENT BY ").Append(statement.Increment)
                .Append(" START WITH ").Append(statement.StartWith);

            if (statement.CacheSize > 0)
            {
                _script.Append(" CACHE ").Append(statement.CacheSize);
            }
            else
            {
                _script.Append(" CACHE 1");
            }

            _script.AppendLine(";");

            statement.Sql = _script.ToString();
        }
        private static string CreateSequenceTriggerName(in string tableName)
        {
            if (tableName.StartsWith('_'))
            {
                return $"tr{tableName}_before_insert";
            }
            else
            {
                return $"tr_{tableName}_before_insert";
            }
        }
        private static string CreateSequenceFunctionName(in string tableName)
        {
            if (tableName.StartsWith('_'))
            {
                return $"fn{tableName}_before_insert";
            }
            else
            {
                return $"fn_{tableName}_before_insert";
            }
        }
        private void Transpile(in ApplySequenceStatement statement)
        {
            if (string.IsNullOrWhiteSpace(statement.Identifier))
            {
                throw new InvalidOperationException("[APPLY SEQUENCE] Sequence identifier missing");
            }

            if (statement.Table.Binding is not EntityDefinition table)
            {
                throw new InvalidOperationException("[APPLY SEQUENCE] Unsupported table binding");
            }

            if (statement.Column.Binding is not PropertyDefinition sequence)
            {
                throw new InvalidOperationException("[APPLY SEQUENCE] Unsupported column binding");
            }

            string tableName = table.DbName.ToLowerInvariant();
            string columnName = sequence.Columns[0].Name.ToLowerInvariant();
            string triggerName = CreateSequenceTriggerName(in tableName);
            string functionName = CreateSequenceFunctionName(in tableName);

            _script.Append("CREATE FUNCTION ").Append(functionName).AppendLine("()");
            _script.AppendLine("RETURNS trigger AS $BODY$");
            _script.AppendLine("BEGIN");
            _script.Append("NEW.").Append(columnName).Append(" := nextval('").Append(statement.Identifier).AppendLine("');"); //.AppendLine(" := CAST(nextval('so_outbox_queue') AS numeric(19, 0));");
            _script.AppendLine("RETURN NEW;");
            _script.AppendLine("END $BODY$ LANGUAGE 'plpgsql';");

            _script.AppendLine("GO"); //FIXME: commands splitter
            _script.Append("CREATE TRIGGER ").AppendLine(triggerName);
            _script.Append("BEFORE INSERT ON ").Append(tableName).AppendLine(" FOR EACH ROW");
            _script.Append("EXECUTE PROCEDURE ").Append(functionName).AppendLine("();");

            if (statement.ReCalculate)
            {
                _script.AppendLine("GO"); //FIXME: commands splitter
                _script.Append(CreateReCalculateSequenceColumnScript(in tableName, in columnName, statement.Identifier));
            }

            statement.Sql = _script.ToString();
        }
        private void Transpile(in RevokeSequenceStatement statement)
        {
            if (string.IsNullOrWhiteSpace(statement.Identifier))
            {
                throw new InvalidOperationException("[REVOKE SEQUENCE] Sequence identifier missing");
            }

            if (statement.Table.Binding is not EntityDefinition table)
            {
                throw new InvalidOperationException("[REVOKE SEQUENCE] Unsupported table binding");
            }

            string tableName = table.DbName.ToLowerInvariant();
            string triggerName = CreateSequenceTriggerName(in tableName);
            string functionName = CreateSequenceFunctionName(in tableName);

            _script.Append("DROP FUNCTION IF EXISTS ").Append(functionName).AppendLine(" CASCADE;");
            _script.AppendLine("GO"); //FIXME: commands splitter
            _script.Append("DROP TRIGGER IF EXISTS ").Append(triggerName).Append(" ON ").Append(tableName).Append(';').AppendLine();

            statement.Sql = _script.ToString();
        }
        private string CreateReCalculateSequenceColumnScript(in string tableName, in string columnName, in string sequenceName)
        {
            StringBuilder script = new();

            IndexInfo index = GetPrimaryOrUniqueIndex(in tableName)
                ?? throw new InvalidOperationException($"[APPLY SEQUENCE RECALCULATE]: Primary or unique index missing for table [{tableName}]");

            StringBuilder columns = new();
            StringBuilder orderby = new();
            StringBuilder compare = new();

            IndexColumnInfo column;

            for (int i = 0; i < index.Columns.Count; i++)
            {
                column = index.Columns[i];

                if (i > 0)
                {
                    columns.Append(',').Append(' ');
                    orderby.Append(',').Append(' ');
                    compare.Append(" AND ");
                }

                columns.Append(column.Name);
                orderby.Append(column.Name).Append(' ').Append(column.IsDescending ? "DESC" : "ASC");
                compare.Append(tableName).Append('.').Append(column.Name)
                    .Append(" = ")
                    .Append("cte").Append('.').Append(column.Name);
            }

            script.AppendLine("BEGIN TRANSACTION;").AppendLine();
            script.AppendLine("GO"); //FIXME: commands splitter
            script.AppendLine($"LOCK TABLE {tableName} IN ACCESS EXCLUSIVE MODE;");

            script.AppendLine("GO"); //FIXME: commands splitter
            script.AppendLine($"WITH cte AS (SELECT {columns}, nextval('{sequenceName}') AS sequence_value");
            script.AppendLine($"FROM {tableName} ORDER BY {orderby})");
            script.AppendLine($"UPDATE {tableName} SET {columnName} = cte.sequence_value FROM cte");
            script.AppendLine($"WHERE {compare};");

            script.AppendLine("GO"); //FIXME: commands splitter
            script.AppendLine("COMMIT TRANSACTION;");

            return script.ToString();
        }
        private IndexInfo GetPrimaryOrUniqueIndex(in string tableName)
        {
            List<IndexInfo> indexes = new PgSqlHelper(_provider.ConnectionString).GetIndexes(in tableName);

            foreach (IndexInfo index in indexes)
            {
                if (index.IsPrimary) { return index; }
            }

            foreach (IndexInfo index in indexes)
            {
                if (index.IsUnique && index.IsClustered) { return index; }
            }

            foreach (IndexInfo index in indexes)
            {
                if (index.IsUnique) { return index; }
            }

            return null;
        }
        private void Transpile(in DropSequenceStatement statement)
        {
            _script.Append("DROP SEQUENCE ").Append(statement.Identifier).AppendLine(";");

            statement.Sql = _script.ToString();
        }
    }
}