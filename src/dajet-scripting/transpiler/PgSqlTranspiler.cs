using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using System.Text;

namespace DaJet.Scripting
{
    //public sealed class PgSqlTranspiler : SqlTranspiler
    //{
    //    public PgSqlTranspiler(ISchemaProvider schema) : base(schema) { }

    //    private bool IsRecursive(in CommonTableExpression cte)
    //    {
    //        if (IsRecursive(in cte, cte.Expression))
    //        {
    //            return true;
    //        }

    //        if (cte.Next is null) { return false; }

    //        return IsRecursive(cte.Next);
    //    }
    //    private bool IsRecursive(in CommonTableExpression cte, in SyntaxNode node)
    //    {
    //        if (node is SelectExpression select)
    //        {
    //            return IsRecursive(in cte, in select);
    //        }
    //        else if (node is TableJoinOperator join)
    //        {
    //            return IsRecursive(in cte, in join);
    //        }
    //        else if (node is TableUnionOperator union)
    //        {
    //            return IsRecursive(in cte, in union);
    //        }
    //        else if (node is TableReference table)
    //        {
    //            return IsRecursive(in cte, in table);
    //        }

    //        return false;
    //    }
    //    private bool IsRecursive(in CommonTableExpression cte, in SelectExpression select)
    //    {
    //        if (select.From is null) { return false; }

    //        return IsRecursive(in cte, select.From.Expression);
    //    }
    //    private bool IsRecursive(in CommonTableExpression cte, in TableJoinOperator node)
    //    {
    //        if (IsRecursive(in cte, node.Expression1))
    //        {
    //            return true;
    //        }

    //        return IsRecursive(in cte, node.Expression2);
    //    }
    //    private bool IsRecursive(in CommonTableExpression cte, in TableUnionOperator node)
    //    {
    //        if (node.Expression1 is SelectExpression select1)
    //        {
    //            if (IsRecursive(in cte, in select1)) { return true; }
    //        }
    //        else if (node.Expression1 is TableUnionOperator union1)
    //        {
    //            if (IsRecursive(in cte, in union1)) { return true; }
    //        }

    //        if (node.Expression2 is SelectExpression select2)
    //        {
    //            return IsRecursive(in cte, in select2);
    //        }
    //        else if (node.Expression2 is TableUnionOperator union2)
    //        {
    //            return IsRecursive(in cte, in union2);
    //        }

    //        return false;
    //    }
    //    private bool IsRecursive(in CommonTableExpression cte, in TableReference table)
    //    {
    //        return (table.Binding == cte);
    //    }

    //    protected override void Visit(in SelectStatement node, out SqlStatement statement)
    //    {
    //        StringBuilder script = new();

    //        script.AppendLine();

    //        if (node.CommonTables is not null)
    //        {
    //            script.Append("WITH ");

    //            if (IsRecursive(node.CommonTables))
    //            {
    //                script.Append("RECURSIVE ");
    //            }

    //            Visit(node.CommonTables, in script);
    //        }

    //        Visit(node.Expression, in script);

    //        script.Append(';');

    //        statement = new SqlStatement()
    //        {
    //            Node = node,
    //            Sql = script.ToString()
    //        };
    //    }
    //    protected override void Visit(in SelectExpression node, in StringBuilder script)
    //    {
    //        script.Append("SELECT");

    //        if (node.Distinct)
    //        {
    //            script.Append(" DISTINCT");
    //        }

    //        script.AppendLine();

    //        for (int i = 0; i < node.Columns.Count; i++)
    //        {
    //            if (i > 0) { script.AppendLine(","); }

    //            Visit(node.Columns[i], in script);
    //        }

    //        if (node.Into is not null) { Visit(node.Into, in script); }
    //        if (node.From is not null) { Visit(node.From, in script); }
    //        if (node.Where is not null) { Visit(node.Where, in script); }
    //        if (node.Group is not null) { Visit(node.Group, in script); }
    //        if (node.Having is not null) { Visit(node.Having, in script); }
    //        if (node.Order is not null) { Visit(node.Order, in script); }
    //        if (node.Top is not null) { Visit(node.Top, in script); }

    //        //if (!string.IsNullOrEmpty(node.Hints))
    //        //{
    //        //    script.AppendLine().Append(node.Hints);
    //        //}
    //    }
    //    protected override void Visit(in TopClause node, in StringBuilder script)
    //    {
    //        script.AppendLine().Append("LIMIT ");

    //        Visit(node.Expression, in script);
    //    }

    //    //protected override void Visit(in IntoClause node, in StringBuilder script)
    //    //{
    //    //    if (node.Table is not null)
    //    //    {
    //    //        script.AppendLine().Append("INTO TEMPORARY TABLE ");
    //    //        Visit(node.Table, in script);
    //    //    }
    //    //}
    //    //protected override void Visit(in TableReference node, in StringBuilder script)
    //    //{
    //    //    if (node.Binding is UserDefinedType table)
    //    //    {
    //    //        script.Append("UNNEST(").Append(table.TableName).Append(')');

    //    //        if (!string.IsNullOrEmpty(node.Alias))
    //    //        {
    //    //            script.Append(" AS ").Append(node.Alias);
    //    //        }
    //    //    }
    //    //    else
    //    //    {
    //    //        base.Visit(in node, in script);
    //    //    }
    //    //}
    //    //protected override void Visit(in List<ColumnMapper> mapping, in StringBuilder script)
    //    //{
    //    //    ColumnMapper column;

    //    //    for (int i = 0; i < mapping.Count; i++)
    //    //    {
    //    //        column = mapping[i];

    //    //        if (i > 0) { script.Append(", "); }

    //    //        if (column.TypeName.StartsWith("char") || column.TypeName.StartsWith("text"))
    //    //        {
    //    //            script.Append("CAST(").Append(column.Name).Append(" AS mvarchar)"); // table variable column trick
    //    //        }
    //    //        else
    //    //        {
    //    //            script.Append(column.Name);
    //    //        }

    //    //        if (!string.IsNullOrEmpty(column.Alias))
    //    //        {
    //    //            script.Append(" AS ").Append(column.Alias);
    //    //        }
    //    //    }
    //    //}

    //    protected override void Visit(in TableJoinOperator node, in StringBuilder script)
    //    {
    //        Visit(node.Expression1, in script); // left operand

    //        //if (node.Token == Token.APPEND)
    //        //{
    //        //    //NOTE: do not generate SQL database code
    //        //    //for the right TableExpression operand
    //        //    //leave it for the script processor
                
    //        //    return;
    //        //}

    //        if (node.Token == Token.CROSS_APPLY)
    //        {
    //            script.AppendLine().Append("INNER JOIN LATERAL ");
    //        }
    //        else if (node.Token == Token.OUTER_APPLY)
    //        {
    //            script.AppendLine().Append("LEFT JOIN LATERAL ");
    //        }
    //        else
    //        {
    //            script.AppendLine().Append(node.Token.ToString()).Append(" JOIN ");
    //        }

    //        Visit(node.Expression2, in script); // right operand

    //        if (node.Token == Token.CROSS_APPLY || node.Token == Token.OUTER_APPLY)
    //        {
    //            script.Append(" ON TRUE");
    //        }
    //        else if (node.On is not null) //NOTE: null if CROSS JOIN
    //        {
    //            Visit(node.On, in script);
    //        }
    //    }

    //    //protected override void Visit(in EnumValue node, in StringBuilder script)
    //    //{
    //    //    script.Append($"CAST(E'\\\\x{ParserHelper.GetUuidHexLiteral(node.Uuid)}' AS bytea)");
    //    //}

    //    protected override void Visit(in ScalarExpression node, in StringBuilder script)
    //    {
    //        if (node.Token == Token.Boolean)
    //        {
    //            script.Append(node.Literal);
    //        }
    //        else if (node.Token == Token.DateTime)
    //        {
    //            if (DateTime.TryParse(node.Literal, out DateTime datetime))
    //            {
    //                script.Append($"\'{datetime.AddYears(YearOffset):yyyy-MM-ddTHH:mm:ss}\'::timestamp");
    //            }
    //            else
    //            {
    //                script.Append(node.Literal);
    //            }
    //        }
    //        else if (node.Token == Token.String)
    //        {
    //            script.Append($"CAST(\'{node.Literal}\' AS mvarchar)");
    //        }
    //        else if (node.Token == Token.Uuid)
    //        {
    //            script.Append($"CAST(E'\\\\x{LexerHelper.GetUuidHexLiteral(new Guid(node.Literal))}' AS bytea)");
    //        }
    //        else if (node.Token == Token.Binary || node.Literal.StartsWith("0x"))
    //        {
    //            if (node.Literal == "0x00") // TODO: подумать как убрать этот костыль
    //            {
    //                script.Append("FALSE");
    //            }
    //            else if (node.Literal == "0x01") // TODO: может прилетать как значение по умолчанию для INSERT
    //            {
    //                script.Append("TRUE");
    //            }
    //            else
    //            {
    //                script.Append($"CAST(E'\\\\{node.Literal.TrimStart('0')}' AS bytea)");
    //            }
    //        }
    //        else if (node.Token == Token.Entity) // implicit cast to uuid
    //        {
    //            script.Append($"CAST(E'\\\\x{LexerHelper.GetUuidHexLiteral(Entity.Parse(node.Literal).Identity)}' AS bytea)");
    //        }
    //        else // Number
    //        {
    //            script.Append(node.Literal);
    //        }
    //    }
    //    protected override void Visit(in VariableReference node, in StringBuilder script)
    //    {
    //        if (node.Binding is Type type && type == typeof(string))
    //        {
    //            script.Append($"CAST({node.Identifier} AS mvarchar)");
    //        }
    //        else
    //        {
    //            script.Append(node.Identifier);
    //        }
    //    }
    //    protected override void Visit(in MemberAccessExpression node, in StringBuilder script)
    //    {
    //        string identifier = node.GetDbParameterName();

    //        if (node.Binding is Type type && type == typeof(string))
    //        {
    //            script.Append($"CAST({identifier} AS mvarchar)");
    //        }
    //        else
    //        {
    //            script.Append(identifier);
    //        }
    //    }
        
        //protected override void Visit(in FunctionExpression node, in StringBuilder script)
        //{
        //    if (node.Token == TokenType.UDF)
        //    {
        //        if (UDF.TryGet(node.Name, out IUserDefinedFunction transpiler))
        //        {
        //            FunctionDescriptor function = transpiler.Transpile(this, in node, in script);

        //            if (function is not null)
        //            {
        //                Functions.Add(function);
        //            }
        //        }
        //        else
        //        {
        //            throw new InvalidOperationException($"Invalid function name: {node.Name}");
        //        }

        //        return;
        //    }

        //    string name = node.Name.ToUpperInvariant();

        //    string pg_name = null;

        //    if (name == "NEWUUID")
        //    {
        //        script.Append($"CAST(E'\\\\x{ParserHelper.GetUuidHexLiteral(Guid.NewGuid())}' AS bytea)"); return;
        //    }
        //    else if (name == "ISNULL")
        //    {
        //        node.Name = "COALESCE";
        //    }
        //    else if (name == "DATALENGTH")
        //    {
        //        node.Name = "OCTET_LENGTH";
        //        script.Append(node.Name).Append('(');
        //        script.Append("CAST(");
        //        Visit(node.Parameters[0], in script);
        //        script.Append(" AS text)");
        //        script.Append(')');
        //        return; //TODO: OCTET_LENGTH - what if data type of column is bytea ?
        //    }
        //    else if (name == "NOW") // timestamp without time zone
        //    {
        //        script.Append("NOW()::timestamp"); return;
        //    }
        //    else if (name == "UTC") // timestamp without time zone
        //    {
        //        script.Append("NOW() AT TIME ZONE 'UTC'"); return;
        //    }
        //    else if (name == "VECTOR")
        //    {
        //        if (node.Parameters is not null && node.Parameters.Count > 0 && node.Parameters[0] is ScalarExpression scalar)
        //        {
        //            //script.Append($"CAST(nextval('{scalar.Literal.ToLower()}') AS numeric(19, 0))");
        //            script.Append($"nextval('{scalar.Literal.ToLower()}')");
        //        }
        //        return;
        //    }
        //    else if (name == "CHARLENGTH") { pg_name = "LENGTH"; }

        //    if (pg_name is not null)
        //    {
        //        script.Append(pg_name);
        //    }
        //    else
        //    {
        //        script.Append(node.Name);
        //    }

        //    if (node.Token != TokenType.EXISTS) 
        //    {
        //        script.Append('('); //NOTE: EXISTS function has one parameter - TableExpression
        //    }

        //    if (node.Token == TokenType.COUNT &&
        //        node.Modifier == TokenType.DISTINCT)
        //    {
        //        script.Append("DISTINCT ");
        //    }

        //    SyntaxNode expression;

        //    for (int i = 0; i < node.Parameters.Count; i++)
        //    {
        //        expression = node.Parameters[i];
        //        if (i > 0) { script.Append(", "); }

        //        if (name == "SUBSTRING" && i == 0)
        //        {
        //            script.Append("CAST(");
        //            Visit(in expression, in script);
        //            script.Append(" AS varchar)");
        //        }
        //        else if (name == "STRING_AGG" || name == "REPLACE"
        //            || name == "CONCAT" || name == "CONCAT_WS"
        //            || name == "LOWER" || name == "UPPER"
        //            || name == "LTRIM" || name == "RTRIM")
        //        {
        //            script.Append("CAST(");
        //            Visit(in expression, in script);
        //            script.Append(" AS text)");
        //        }
        //        else
        //        {
        //            Visit(in expression, in script);
        //        }
        //    }

        //    if (node.Token != TokenType.EXISTS)
        //    {
        //        script.Append(')'); //NOTE: EXISTS function has one parameter - TableExpression
        //    }

        //    if (node.Over is not null)
        //    {
        //        script.Append(' ');
        //        Visit(node.Over, in script);
        //    }
        //}

        //protected override void Visit(in TableVariableExpression node, in StringBuilder script)
        //{
        //    script.Append($"CREATE TEMPORARY TABLE {node.Name} AS").AppendLine();

        //    base.Visit(in node, in script);

        //    script.Append(';').AppendLine();
        //}
        //protected override void Visit(in TemporaryTableExpression node, in StringBuilder script)
        //{
        //    script.Append($"CREATE TEMPORARY TABLE {node.Name} AS").AppendLine();

        //    base.Visit(in node, in script);

        //    script.Append(';').AppendLine();
        //}

        //public override void Visit(in CreateSequenceStatement node, in StringBuilder script)
        //{
        //    // CREATE SEQUENCE IF NOT EXISTS {SEQUENCE_NAME} AS bigint INCREMENT BY 1 START WITH 1 CACHE 1;

        //    script.Append("CREATE SEQUENCE IF NOT EXISTS ").Append(node.Identifier)
        //        .Append(" AS bigint")
        //        .Append(" INCREMENT BY ").Append(node.Increment)
        //        .Append(" START WITH ").Append(node.StartWith);

        //    if (node.CacheSize > 0)
        //    {
        //        script.Append(" CACHE ").Append(node.CacheSize);
        //    }
        //    else
        //    {
        //        script.Append(" CACHE 1");
        //    }

        //    script.AppendLine(";");
        //}
        //private static string CreateSequenceTriggerName(in string tableName)
        //{
        //    if (tableName.StartsWith('_'))
        //    {
        //        return $"tr{tableName}_before_insert";
        //    }
        //    else
        //    {
        //        return $"tr_{tableName}_before_insert";
        //    }
        //}
        //private static string CreateSequenceFunctionName(in string tableName)
        //{
        //    if (tableName.StartsWith('_'))
        //    {
        //        return $"fn{tableName}_before_insert";
        //    }
        //    else
        //    {
        //        return $"fn_{tableName}_before_insert";
        //    }
        //}
        //public override void Visit(in ApplySequenceStatement node, in StringBuilder script)
        //{
        //    if (string.IsNullOrWhiteSpace(node.Identifier))
        //    {
        //        throw new InvalidOperationException("[APPLY SEQUENCE] Sequence identifier missing");
        //    }

        //    if (node.Table.Binding is not EntityDefinition table)
        //    {
        //        throw new InvalidOperationException("[APPLY SEQUENCE] Unsupported table binding");
        //    }

        //    if (node.Column.Binding is not PropertyDefinition sequence)
        //    {
        //        throw new InvalidOperationException("[APPLY SEQUENCE] Unsupported column binding");
        //    }

        //    string tableName = table.DbName.ToLowerInvariant();
        //    string columnName = sequence.Columns[0].Name.ToLowerInvariant();
        //    string triggerName = CreateSequenceTriggerName(in tableName);
        //    string functionName = CreateSequenceFunctionName(in tableName);

        //    script.Append("CREATE FUNCTION ").Append(functionName).AppendLine("()");
        //    script.AppendLine("RETURNS trigger AS $BODY$");
        //    script.AppendLine("BEGIN");
        //    script.Append("NEW.").Append(columnName).Append(" := nextval('").Append(node.Identifier).AppendLine("');");//.AppendLine(" := CAST(nextval('so_outbox_queue') AS numeric(19, 0));");
        //    script.AppendLine("RETURN NEW;");
        //    script.AppendLine("END $BODY$ LANGUAGE 'plpgsql';");

        //    script.AppendLine();
        //    script.Append("CREATE TRIGGER ").AppendLine(triggerName);
        //    script.Append("BEFORE INSERT ON ").Append(tableName).AppendLine(" FOR EACH ROW");
        //    script.Append("EXECUTE PROCEDURE ").Append(functionName).AppendLine("();");

        //    //if (node.ReCalculate)
        //    //{
        //    //    script.AppendLine();
        //    //    script.Append(CreateReCalculateSequenceColumnScript(in tableName, in columnName, node.Identifier));
        //    //}
        //}
        //public override void Visit(in RevokeSequenceStatement node, in StringBuilder script)
        //{
        //    if (string.IsNullOrWhiteSpace(node.Identifier))
        //    {
        //        throw new InvalidOperationException("[REVOKE SEQUENCE] Sequence identifier missing");
        //    }

        //    if (node.Table.Binding is not EntityDefinition table)
        //    {
        //        throw new InvalidOperationException("[REVOKE SEQUENCE] Unsupported table binding");
        //    }

        //    string tableName = table.DbName.ToLowerInvariant();
        //    string triggerName = CreateSequenceTriggerName(in tableName);
        //    string functionName = CreateSequenceFunctionName(in tableName);

        //    script.Append("DROP FUNCTION IF EXISTS ").Append(functionName).AppendLine(" CASCADE;");
        //    script.Append("DROP TRIGGER IF EXISTS ").Append(triggerName).Append(" ON ").Append(tableName).Append(';').AppendLine();
        //}
        
        //private string CreateReCalculateSequenceColumnScript(in string tableName, in string columnName, in string sequenceName)
        //{
        //    StringBuilder script = new();

        //    IndexInfo index = GetPrimaryOrUniqueIndex(in tableName)
        //        ?? throw new InvalidOperationException($"[APPLY SEQUENCE RECALCULATE]: Primary or unique index missing for table [{tableName}]");

        //    StringBuilder columns = new();
        //    StringBuilder orderby = new();
        //    StringBuilder compare = new();

        //    IndexColumnInfo column;

        //    for (int i = 0; i < index.Columns.Count; i++)
        //    {
        //        column = index.Columns[i];

        //        if (i > 0)
        //        {
        //            columns.Append(',').Append(' ');
        //            orderby.Append(',').Append(' ');
        //            compare.Append(" AND ");
        //        }

        //        columns.Append(column.Name);
        //        orderby.Append(column.Name).Append(' ').Append(column.IsDescending ? "DESC" : "ASC");
        //        compare.Append(tableName).Append('.').Append(column.Name)
        //            .Append(" = ")
        //            .Append("cte").Append('.').Append(column.Name);
        //    }

        //    script.AppendLine("BEGIN TRANSACTION;").AppendLine();
        //    script.AppendLine($"LOCK TABLE {tableName} IN ACCESS EXCLUSIVE MODE;");
            
        //    script.AppendLine();
        //    script.AppendLine($"WITH cte AS (SELECT {columns}, nextval('{sequenceName}') AS sequence_value");
        //    script.AppendLine($"FROM {tableName} ORDER BY {orderby})");
        //    script.AppendLine($"UPDATE {tableName} SET {columnName} = cte.sequence_value FROM cte");
        //    script.AppendLine($"WHERE {compare};");

        //    script.AppendLine();
        //    script.AppendLine("COMMIT TRANSACTION;");

        //    return script.ToString();
        //}
//    }

}

//**********************************************************************
//* Шаблон запроса на деструктивное чтение с обогащением данных (JOIN) *
//**********************************************************************
//
//WITH changes AS
//(
//  SELECT
//    Изменения._nodetref AS _nodetref,
//    Изменения._noderref AS _noderref,
//    Изменения._idrref AS _idrref,
//    ПланОбмена._Code AS Получатель,
//    Данные._IDRRef AS Ссылка,
//    Данные._Code AS Код,
//    Данные._Description AS Наименование
//        FROM _ReferenceChngR363 AS Изменения
//  INNER JOIN _Node362 AS ПланОбмена  ON (Изменения._NodeTRef = CAST(E'\\x0000016A' AS bytea) AND Изменения._NodeRRef = ПланОбмена._IDRRef)
//  LEFT  JOIN _Reference287 AS Данные ON Изменения._IDRRef = Данные._IDRRef
//  WHERE ПланОбмена._Code = CAST('DaJet' AS mvarchar)
//  ORDER BY Данные._Code ASC
//  LIMIT 10
//  FOR UPDATE OF Изменения SKIP LOCKED),
//source AS
//(
//    DELETE FROM _ReferenceChngR363 AS target USING changes
//    WHERE target._nodetref = changes._nodetref
//      AND target._noderref = changes._noderref
//      AND target._idrref   = changes._idrref
//    RETURNING changes.Получатель, changes.Ссылка, changes.Код, changes.Наименование
//)
//SELECT * FROM source ORDER BY source.Код ASC;
//
//**********************************************************************

// Шаблон запроса на деструктивное чтение для PostgreSQL
//WITH filter AS
//(SELECT
//  МоментВремени,
//  Идентификатор
//FROM
//  {TABLE_NAME}
//ORDER BY
//  МоментВремени ASC,
//  Идентификатор ASC
//LIMIT
//  @MessageCount
//FOR UPDATE SKIP LOCKED
//),

//queue AS(
//DELETE FROM {TABLE_NAME} t USING filter
//WHERE t.МоментВремени = filter.МоментВремени
//  AND t.Идентификатор = filter.Идентификатор
//RETURNING
//  t.МоментВремени, t.Идентификатор, t.ДатаВремя,
//  t.Отправитель, t.Получатели, t.Заголовки,
//  t.ТипОперации, t.ТипСообщения, t.ТелоСообщения
//)

//SELECT
//  queue.МоментВремени, queue.Идентификатор, queue.ДатаВремя,
//  CAST(queue.Заголовки     AS text)    AS "Заголовки",
//  CAST(queue.Отправитель   AS varchar) AS "Отправитель",
//  CAST(queue.Получатели    AS text)    AS "Получатели",
//  CAST(queue.ТипОперации   AS varchar) AS "ТипОперации",
//  CAST(queue.ТипСообщения  AS varchar) AS "ТипСообщения",
//  CAST(queue.ТелоСообщения AS text)    AS "ТелоСообщения"
//FROM
//  queue
//ORDER BY
//  queue.МоментВремени ASC,
//  queue.Идентификатор ASC
//;