using DaJet.Metadata;
using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using System.Text;

namespace DaJet.Scripting
{
    public sealed class MsSequenceTranspiler : SqlTranspiler
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
            // IF NOT EXISTS(SELECT 1 FROM sys.sequences WHERE name = '{SEQUENCE_NAME}')
            // BEGIN
            // CREATE SEQUENCE {SEQUENCE_NAME} AS numeric(19,0) START WITH 1 INCREMENT BY 1 CACHE 1;
            // END;

            _script
                .Append("IF NOT EXISTS(SELECT 1 FROM sys.sequences WHERE name = '")
                .Append(statement.Identifier).AppendLine("')")
                .AppendLine("BEGIN");

            _script.Append("CREATE SEQUENCE ").Append(statement.Identifier).Append(" AS ");

            if (statement.DataType is TypeReference info)
            {
                if (!info.Type.IsUndefined)
                {
                    if (info.Type.IsDecimal) // number(p,s))
                    {
                        if (info.Type.Precision > 0)
                        {
                            _script.Append("numeric(").Append(info.Type.Precision).Append(',').Append(info.Type.Scale).Append(')');
                        }
                        else
                        {
                            _script.Append("bigint");
                        }
                    }
                    else if (info.Type.IsInteger)
                    {
                        if (info.Type.Size == 8)
                        {
                            _script.Append("bigint");
                        }
                        else
                        {
                            _script.Append("int");
                        }
                    }   
                }
                else
                {
                    _script.Append("numeric(15,0)"); // default value
                }
            }
            else
            {
                _script.Append("numeric(15,0)");  // default value
            }

            _script
                .Append(" START WITH ").Append(statement.StartWith)
                .Append(" INCREMENT BY ").Append(statement.Increment);

            if (statement.CacheSize > 0)
            {
                _script.Append(" CACHE ").Append(statement.CacheSize);
            }

            _script.AppendLine(";").AppendLine("END;");

            statement.Sql = _script.ToString();
        }
        private static string CreateSequenceTriggerName(string tableName)
        {
            return $"{tableName.ToLowerInvariant()}_instead_of_insert";
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

            string triggerName = CreateSequenceTriggerName(table.DbName);

            _script.Append("IF OBJECT_ID('").Append(triggerName).AppendLine("', 'TR') IS NULL");

            _script
                .Append("EXECUTE('CREATE TRIGGER ")
                .Append(triggerName).Append(" ON ").Append(table.DbName)
                .AppendLine(" INSTEAD OF INSERT NOT FOR REPLICATION AS");

            bool use_comma = false;
            StringBuilder values = new();
            StringBuilder columns = new();

            ColumnDefinition column;
            PropertyDefinition property;
            string sequenceColumn = string.Empty;

            for (int p = 0; p < table.Properties.Count; p++)
            {
                property = table.Properties[p];

                for (int c = 0; c < property.Columns.Count; c++)
                {
                    column = property.Columns[c];

                    if (use_comma)
                    {
                        values.Append(',').Append(' ');
                        columns.Append(',').Append(' ');
                    }
                    else { use_comma = true; }

                    columns.Append(column.Name);

                    if (property.Name == sequence.Name)
                    {
                        sequenceColumn = column.Name;

                        values.Append("NEXT VALUE FOR ").Append(statement.Identifier);
                    }
                    else
                    {
                        values.Append('i').Append('.').Append(column.Name);
                    }
                }
            }

            _script.Append("INSERT ").Append(table.DbName).Append('(').Append(columns).Append(')').AppendLine();
            _script.Append("SELECT ").Append(values).AppendLine();
            _script.AppendLine("FROM INSERTED AS i;');"); // close EXECUTE statement

            if (statement.ReCalculate)
            {
                _script.AppendLine();
                _script.Append(CreateReCalculateSequenceColumnScript(table.DbName, in sequenceColumn, statement.Identifier));
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

            string triggerName = CreateSequenceTriggerName(table.DbName);

            _script
                .Append("IF OBJECT_ID('").Append(triggerName).Append("', 'TR') IS NOT NULL ")
                .AppendLine("DROP TRIGGER ").Append(triggerName).Append(';').AppendLine();

            statement.Sql = _script.ToString();
        }
        private string CreateReCalculateSequenceColumnScript(in string tableName, in string columnName, in string sequenceName)
        {
            StringBuilder script = new();

            IndexInfo index = GetPrimaryOrUniqueIndex(in tableName)
                ?? throw new InvalidOperationException($"[APPLY SEQUENCE RECALCULATE]: Primary or unique index missing for table [{tableName}]");

            string temporaryTable = $"#COPY{tableName}";

            StringBuilder columns = new();
            StringBuilder orderby = new();
            StringBuilder joinon = new();

            IndexColumnInfo column;

            for (int i = 0; i < index.Columns.Count; i++)
            {
                column = index.Columns[i];

                if (i > 0)
                {
                    columns.Append(',').Append(' ');
                    orderby.Append(',').Append(' ');
                    joinon.Append(" AND ");
                }

                columns.Append(column.Name);
                orderby.Append(column.Name).Append(' ').Append(column.IsDescending ? "DESC" : "ASC");
                joinon.Append('T').Append('.').Append(column.Name)
                    .Append(" = ");
                joinon.Append('S').Append('.').Append(column.Name);
            }

            script.AppendLine("BEGIN TRANSACTION;");

            script.Append($"SELECT {columns}");
            script.AppendLine($", NEXT VALUE FOR {sequenceName} OVER (ORDER BY {orderby}) AS sequence_value");
            script.AppendLine($"INTO {temporaryTable} FROM {tableName} WITH (TABLOCKX, HOLDLOCK);");

            script.AppendLine($"UPDATE T SET T.{columnName} = S.sequence_value FROM {tableName} AS T");
            script.AppendLine($"INNER JOIN {temporaryTable} AS S ON {joinon};");

            script.AppendLine($"DROP TABLE {temporaryTable};");

            script.AppendLine("COMMIT TRANSACTION;");

            return script.ToString();
        }
        private IndexInfo GetPrimaryOrUniqueIndex(in string tableName)
        {
            List<IndexInfo> indexes = new MsSqlHelper(_provider.ConnectionString).GetIndexes(in tableName);

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

// Шаблон запроса на деструктивное чтение с обогащением данных (JOIN)
//DECLARE @result TABLE(id binary(16));
//WITH changes AS 
//(SELECT TOP (10)
//Изменения._NodeTRef AS УзелОбмена_TRef, Изменения._NodeRRef AS УзелОбмена_RRef,
//Изменения._IDRRef AS Ссылка
//FROM _ReferenceChngR1253 AS Изменения WITH (ROWLOCK, READPAST)
//ORDER BY _IDRRef DESC
//)
//DELETE target
//OUTPUT
//changes.Ссылка
//INTO @result
//FROM _ReferenceChngR1253 AS target INNER JOIN changes ON target._IDRRef = changes.Ссылка
//;
//SELECT * FROM @result ORDER BY id ASC;
//;

// Шаблон запроса на деструктивное чтение для Microsoft SQL Server
//WITH queue AS
//(SELECT TOP (@MessageCount)
//  МоментВремени, Идентификатор, ДатаВремя,
//  Отправитель, Получатели, Заголовки,
//  ТипОперации, ТипСообщения, ТелоСообщения
//FROM
//  {TABLE_NAME} WITH (ROWLOCK, READPAST)
//ORDER BY
//  МоментВремени ASC,
//  Идентификатор ASC
//)
//DELETE queue OUTPUT
//  deleted.МоментВремени, deleted.Идентификатор, deleted.ДатаВремя,
//  deleted.Отправитель, deleted.Получатели, deleted.Заголовки,
//  deleted.ТипОперации, deleted.ТипСообщения, deleted.ТелоСообщения
//;
// ??? OPTION (MAXDOP 1) ???