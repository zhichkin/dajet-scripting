using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using System.Text;

namespace DaJet.Scripting
{
    public sealed class MsSequenceTranspiler : SequenceTranspiler
    {
        public MsSequenceTranspiler(ISchemaProvider schema) : base(schema) { }

        //private string GetCreateTableColumnList(in SelectExpression select)
        //{
        //    StringBuilder columns = new();

        //    ColumnMapper column;
        //    PropertyMapper property;
        //    EntityMapper map = DataMapper.CreateEntityMap(in select);

        //    for (int i = 0; i < map.Properties.Count; i++)
        //    {
        //        property = map.Properties[i];

        //        for (int ii = 0; ii < property.ColumnSequence.Count; ii++)
        //        {
        //            column = property.ColumnSequence[ii];

        //            if (column.Ordinal > 0) { columns.Append(", "); }

        //            columns.Append(column.Alias).Append(' ').Append(column.TypeName);
        //        }
        //    }

        //    return columns.ToString();
        //}

        //protected override void Visit(in TableReference node, in StringBuilder script)
        //{
        //    if (node.Binding is EntityDefinition entity)
        //    {
        //        script.Append(entity.DbName);
        //    }
        //    else if (node.Binding is TableExpression || node.Binding is CommonTableExpression)
        //    {
        //        script.Append(node.Identifier);
        //    }
        //    else if (node.Binding is TableVariableExpression)
        //    {
        //        script.Append($"@{node.Identifier}");
        //    }
        //    else if (node.Binding is TemporaryTableExpression)
        //    {
        //        script.Append($"#{node.Identifier}");
        //    }

        //    if (!string.IsNullOrEmpty(node.Alias))
        //    {
        //        script.Append(" AS ").Append(node.Alias);
        //    }

        //    if (!string.IsNullOrEmpty(node.Hints))
        //    {
        //        script.Append(' ').Append(node.Hints); // CONSUME statement support only
        //    }
        //}

        //protected override void Visit(in FunctionExpression node, in StringBuilder script)
        //{
        //    string name = node.Name.ToUpperInvariant();

        //    if (UDF.TryGet(node.Name, out IUserDefinedFunction transpiler))
        //    {
        //        FunctionDescriptor function = transpiler.Transpile(this, in node, in script);

        //        if (function is not null)
        //        {
        //            Functions.Add(function);
        //        }
        //    }
        //    else if (name == "NOW")
        //    {
        //        if (YearOffset == 0)
        //        {
        //            script.Append("GETDATE()");
        //        }
        //        else
        //        {
        //            script.Append("DATEADD(year, " + YearOffset.ToString() + ", GETDATE())");
        //        }
        //    }
        //    else if (name == "UTC")
        //    {
        //        if (YearOffset == 0)
        //        {
        //            script.Append("GETUTCDATE()");
        //        }
        //        else
        //        {
        //            script.Append("DATEADD(year, " + YearOffset.ToString() + ", GETUTCDATE())");
        //        }
        //    }
        //    else if (name == "VECTOR")
        //    {
        //        if (node.Parameters is not null && node.Parameters.Count > 0 && node.Parameters[0] is ScalarExpression scalar)
        //        {
        //            script.Append("NEXT VALUE FOR ").Append(scalar.Literal);
        //        }
        //    }
        //    else if (name == "CHARLENGTH")
        //    {
        //        script.Append("LEN").Append('(');
        //        Visit(node.Parameters[0], in script);
        //        script.Append(')');
        //    }
        //    else if (name == "NEWUUID")
        //    {
        //        script.Append("NEWID()");
        //    }
        //    else if (node.Token != TokenType.UDF)
        //    {
        //        base.Visit(in node, in script);
        //    }
        //    else
        //    {
        //        throw new InvalidOperationException($"Invalid function name: {node.Name}");
        //    }
        //}
        
        public override void Visit(in CreateSequenceStatement node, in StringBuilder script)
        {
            // IF NOT EXISTS(SELECT 1 FROM sys.sequences WHERE name = '{SEQUENCE_NAME}')
            // BEGIN
            // CREATE SEQUENCE {SEQUENCE_NAME} AS numeric(19,0) START WITH 1 INCREMENT BY 1 CACHE 1;
            // END;

            script
                .Append("IF NOT EXISTS(SELECT 1 FROM sys.sequences WHERE name = '")
                .Append(node.Identifier).AppendLine("')")
                .AppendLine("BEGIN");

            script.Append("CREATE SEQUENCE ").Append(node.Identifier).Append(" AS ");

            if (node.DataType is TypeReference info)
            {
                if (!info.Type.IsUndefined)
                {
                    if (info.Type.IsDecimal) // number(p,s))
                    {
                        if (info.Type.Precision > 0)
                        {
                            script.Append("numeric(").Append(info.Type.Precision).Append(',').Append(info.Type.Scale).Append(')');
                        }
                        else
                        {
                            script.Append("bigint");
                        }
                    }
                    else if (info.Type.IsInteger)
                    {
                        if (info.Type.Size == 8)
                        {
                            script.Append("bigint");
                        }
                        else
                        {
                            script.Append("int");
                        }
                    }   
                }
                else
                {
                    script.Append("bigint");
                }
            }
            else
            {
                script.Append("bigint");
            }

            script
                .Append(" START WITH ").Append(node.StartWith)
                .Append(" INCREMENT BY ").Append(node.Increment);

            if (node.CacheSize > 0)
            {
                script.Append(" CACHE ").Append(node.CacheSize);
            }

            script.AppendLine(";").AppendLine("END;");
        }
        private static string CreateSequenceTriggerName(string tableName)
        {
            return $"{tableName.ToLowerInvariant()}_instead_of_insert";
        }
        public override void Visit(in ApplySequenceStatement node, in StringBuilder script)
        {
            if (string.IsNullOrWhiteSpace(node.Identifier))
            {
                throw new InvalidOperationException("[APPLY SEQUENCE] Sequence identifier missing");
            }

            if (node.Table.Binding is not EntityDefinition table)
            {
                throw new InvalidOperationException("[APPLY SEQUENCE] Unsupported table binding");
            }

            if (node.Column.Binding is not PropertyDefinition sequence)
            {
                throw new InvalidOperationException("[APPLY SEQUENCE] Unsupported column binding");
            }

            string triggerName = CreateSequenceTriggerName(table.DbName);

            script.Append("IF OBJECT_ID('").Append(triggerName).AppendLine("', 'TR') IS NULL");

            script
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

                        values.Append("NEXT VALUE FOR ").Append(node.Identifier);
                    }
                    else
                    {
                        values.Append('i').Append('.').Append(column.Name);
                    }
                }
            }

            script.Append("INSERT ").Append(table.DbName).Append('(').Append(columns).Append(')').AppendLine();
            script.Append("SELECT ").Append(values).AppendLine();
            script.AppendLine("FROM INSERTED AS i;');"); // close EXECUTE statement

            //if (node.ReCalculate)
            //{
            //    script.AppendLine();
            //    script.Append(CreateReCalculateSequenceColumnScript(table.DbName, in sequenceColumn, node.Identifier));
            //}
        }
        public override void Visit(in RevokeSequenceStatement node, in StringBuilder script)
        {
            if (string.IsNullOrWhiteSpace(node.Identifier))
            {
                throw new InvalidOperationException("[REVOKE SEQUENCE] Sequence identifier missing");
            }

            if (node.Table.Binding is not EntityDefinition table)
            {
                throw new InvalidOperationException("[REVOKE SEQUENCE] Unsupported table binding");
            }

            string triggerName = CreateSequenceTriggerName(table.DbName);

            script
                .Append("IF OBJECT_ID('").Append(triggerName).Append("', 'TR') IS NOT NULL ")
                .AppendLine("DROP TRIGGER ").Append(triggerName).Append(';').AppendLine();
        }

        //private string CreateReCalculateSequenceColumnScript(in string tableName, in string columnName, in string sequenceName)
        //{
        //    StringBuilder script = new();

        //    IndexInfo index = GetPrimaryOrUniqueIndex(in tableName)
        //        ?? throw new InvalidOperationException($"[APPLY SEQUENCE RECALCULATE]: Primary or unique index missing for table [{tableName}]");

        //    string temporaryTable = $"#COPY{tableName}";

        //    StringBuilder columns = new();
        //    StringBuilder orderby = new();
        //    StringBuilder joinon = new();

        //    IndexColumnInfo column;

        //    for (int i = 0; i < index.Columns.Count; i++)
        //    {
        //        column = index.Columns[i];

        //        if (i > 0)
        //        {
        //            columns.Append(',').Append(' ');
        //            orderby.Append(',').Append(' ');
        //            joinon.Append(" AND ");
        //        }

        //        columns.Append(column.Name);
        //        orderby.Append(column.Name).Append(' ').Append(column.IsDescending ? "DESC" : "ASC");
        //        joinon.Append('T').Append('.').Append(column.Name)
        //            .Append(" = ");
        //        joinon.Append('S').Append('.').Append(column.Name);
        //    }

        //    script.AppendLine("BEGIN TRANSACTION;");

        //    script.Append($"SELECT {columns}");
        //    script.AppendLine($", NEXT VALUE FOR {sequenceName} OVER (ORDER BY {orderby}) AS sequence_value");
        //    script.AppendLine($"INTO {temporaryTable} FROM {tableName} WITH (TABLOCKX, HOLDLOCK);");

        //    script.AppendLine($"UPDATE T SET T.{columnName} = S.sequence_value FROM {tableName} AS T");
        //    script.AppendLine($"INNER JOIN {temporaryTable} AS S ON {joinon};");

        //    script.AppendLine($"DROP TABLE {temporaryTable};");

        //    script.AppendLine("COMMIT TRANSACTION;");

        //    return script.ToString();
        //}
        //private IndexInfo GetPrimaryOrUniqueIndex(in string tableName)
        //{
        //    List<IndexInfo> indexes = new MsSqlHelper().GetIndexes(Metadata.ConnectionString, tableName);

        //    foreach (IndexInfo index in indexes)
        //    {
        //        if (index.IsPrimary) { return index; }
        //    }

        //    foreach (IndexInfo index in indexes)
        //    {
        //        if (index.IsUnique && index.IsClustered) { return index; }
        //    }

        //    foreach (IndexInfo index in indexes)
        //    {
        //        if (index.IsUnique) { return index; }
        //    }

        //    return null;
        //}
        //private IndexInfo GetPrimaryOrUniqueIndex(in TableReference table)
        //{
        //    if (table.Binding is not ApplicationObject entity)
        //    {
        //        throw new InvalidOperationException("CONSUME: target table has no entity binding.");
        //    }

        //    string target = entity.TableName.ToLowerInvariant();

        //    List<IndexInfo> indexes = new MsSqlHelper().GetIndexes(Metadata.ConnectionString, target);

        //    foreach (IndexInfo index in indexes)
        //    {
        //        if (index.IsPrimary) { return index; }
        //    }

        //    foreach (IndexInfo index in indexes)
        //    {
        //        if (index.IsUnique && index.IsClustered) { return index; }
        //    }

        //    foreach (IndexInfo index in indexes)
        //    {
        //        if (index.IsUnique) { return index; }
        //    }

        //    return null;
        //}
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