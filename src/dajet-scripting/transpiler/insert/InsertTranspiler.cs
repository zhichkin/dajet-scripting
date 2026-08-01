using DaJet.Metadata;
using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using System.Text;

namespace DaJet.Scripting
{
    public sealed class InsertTranspiler : SqlTranspiler
    {
        private MetadataProvider _provider;
        private SelectExpression _source;
        private EntityDefinition _target;
        private InsertStatement _statement;
        private int _parameterOrdinal;
        public override bool TryTranspile(in SyntaxNode statement, in MetadataProvider provider, out string error)
        {
            error = null;

            ArgumentNullException.ThrowIfNull(provider, nameof(provider));
            
            if (statement is not InsertStatement insert)
            {
                throw new InvalidOperationException();
            }

            if (insert.Source is not SelectExpression source)
            {
                throw new InvalidOperationException();
            }

            if (insert.Target is not TableReference table || table.Binding is not EntityDefinition target)
            {
                throw new InvalidOperationException();
            }
            
            _source = source;
            _target = target;
            _provider = provider;
            _statement = insert;

            _statement.Dialect = _provider.DataSource;

            if (_statement.Dialect == Data.DataSourceType.PostgreSql)
            {
                _parameterOrdinal = 1;
            }
            
            try
            {
                Transpile(in insert);
            }
            catch (Exception exception)
            {
                error = ExceptionHelper.GetErrorMessage(exception);
            }

            _source = null;
            _target = null;
            _provider = null;
            _statement = null;
            _parameterOrdinal = 0;

            return error is null;
        }
        public override void Visit(in SyntaxNode statement, in StringBuilder script)
        {
            throw new NotImplementedException();
        }
        private void Transpile(in InsertStatement statement)
        {
            StringBuilder sql = new();

            sql.Append("INSERT INTO ");

            sql.Append(_target.DbName).Append('(');

            List<string> columnNames = new();
            List<string> listOfValues = new();

            PropertyDefinition property;
            List<PropertyDefinition> properties = _target.Properties;

            for (int p = 0; p < properties.Count; p++)
            {
                property = properties[p];

                if (_source.TryGetColumn(property.Name, out ColumnExpression map))
                {
                    if (_statement.Dialect == Data.DataSourceType.PostgreSql)
                    {
                        PgTranspileParameters(in property, in map, in columnNames, in listOfValues);
                    }
                    else
                    {
                        MsTranspileParameters(in property, in map, in columnNames, in listOfValues);
                    }
                }
                else
                {
                    if (_statement.Dialect == Data.DataSourceType.PostgreSql)
                    {
                        PgTranspileDefaults(in property, in columnNames, in listOfValues);
                    }
                    else
                    {
                        MsTranspileDefaults(in property, in columnNames, in listOfValues);
                    }
                }
            }

            sql
                .Append(string.Join(',', columnNames)).Append(')').Append('\n')
                .Append("VALUES (").Append(string.Join(',', listOfValues)).Append(')').Append(';');

            statement.Sql = sql.ToString();
        }
        private string GetOrdinalParameterName()
        {
            string parameterName;

            if (_statement.Dialect == Data.DataSourceType.PostgreSql)
            {
                parameterName = string.Format("${0}", _parameterOrdinal);
            }
            else
            {
                parameterName = string.Format("@p{0}", _parameterOrdinal);
            }

            _parameterOrdinal++;

            return parameterName;
        }
        private bool TryTranspileFunction(in PropertyDefinition property, in ColumnExpression map, in List<string> columnNames, in List<string> listOfValues)
        {
            if (map.Expression is not FunctionExpression node)
            {
                return false;
            }

            if (node.Token != Token.VECTOR)
            {
                return false;
            }

            Function function;
            StringBuilder sql = new();

            if (_statement.Dialect == Data.DataSourceType.SqlServer)
            {
                if (SqlFunctions.TryGet(node.Token, out function))
                {
                    function.Transpile(this, in node, in sql);
                }
            }
            else if (PgSqlFunctions.TryGet(node.Token, out function))
            {
                function.Transpile(this, in node, in sql);
            }

            columnNames.Add(property.Columns[0].Name);

            listOfValues.Add(sql.ToString());

            return true;
        }
        
        private void MsTranspileDefaults(in PropertyDefinition property, in List<string> columnNames, in List<string> listOfValues)
        {
            string dateTime = DateTime.MinValue.AddYears(_statement.YearOffset).ToString("yyyy-MM-ddTHH:mm:ss");

            dateTime = string.Format("'{0}'", dateTime);

            foreach (ColumnDefinition column in property.Columns)
            {
                if (column.IsGenerated)
                {
                    continue; // database auto-generated column
                }

                string value = string.Empty;
                DataType type = column.Type;

                if (column.Purpose == ColumnPurpose.Value)
                {
                    if (type.IsBoolean) { value = "0x00"; }
                    else if (type.IsDecimal) { value = "0"; }
                    else if (type.IsDateTime) { value = dateTime; }
                    else if (type.IsString) { value = "N''"; }
                    else if (type.IsBinary)
                    {
                        if (type.Size == 1) { value = "0x00"; }
                        else if (type.Size == 4) { value = "0x00000000"; }
                        else if (type.Size == 16) { value = "0x00000000000000000000000000000000"; }
                        else
                        {
                            value = "0x01010800000000000000EFBBBF7B2255227D"; // ХранилищеЗначения
                        }
                    }
                    else if (type.IsUuid) { value = "0x00000000000000000000000000000000"; }
                    else if (type.IsEntity) { value = "0x00000000000000000000000000000000"; }
                }
                else if (column.Purpose == ColumnPurpose.Tag) { value = "0x01"; }
                else if (column.Purpose == ColumnPurpose.TypeCode) { value = "0x00000000"; }
                else if (column.Purpose == ColumnPurpose.Identity) { value = "0x00000000000000000000000000000000"; }
                else if (column.Purpose == ColumnPurpose.Boolean) { value = "0x00"; }
                else if (column.Purpose == ColumnPurpose.Numeric) { value = "0"; }
                else if (column.Purpose == ColumnPurpose.DateTime) { value = dateTime; }
                else if (column.Purpose == ColumnPurpose.String) { value = "N''"; }

                columnNames.Add(column.Name);
                listOfValues.Add(value);
            }
        }
        private void MsTranspileParameters(in PropertyDefinition property, in ColumnExpression map, in List<string> columnNames, in List<string> listOfValues)
        {
            if (TryTranspileFunction(in property, in map, in columnNames, in listOfValues))
            {
                return;
            }

            int before = columnNames.Count;

            ColumnDefinition column;
            List<ColumnDefinition> columns = property.Columns;

            for (int c = 0; c < columns.Count; c++)
            {
                column = columns[c];

                if (column.IsGenerated)
                {
                    continue; // timestamp column _Version
                }

                columnNames.Add(column.Name);

                listOfValues.Add(GetOrdinalParameterName());
            }

            if (columnNames.Count > before)
            {
                _statement.Input.Add(map);
            }
        }

        private void PgTranspileDefaults(in PropertyDefinition property, in List<string> columnNames, in List<string> listOfValues)
        {
            string dateTime = DateTime.MinValue.AddYears(_statement.YearOffset).ToString("yyyy-MM-ddTHH:mm:ss");

            dateTime = string.Format("'{0}'::timestamp", dateTime);

            foreach (ColumnDefinition column in property.Columns)
            {
                if (column.IsGenerated)
                {
                    continue; // database auto-generated column
                }

                string value = string.Empty;
                DataType type = column.Type;

                if (column.Purpose == ColumnPurpose.Value)
                {
                    if (type.IsBoolean) { value = "FALSE"; }
                    else if (type.IsDecimal) { value = "0"; }
                    else if (type.IsDateTime) { value = dateTime; }
                    else if (type.IsString) { value = "''::mvarchar"; }
                    else if (type.IsBinary)
                    {
                        if (type.Size == 1) { value = "E'\\\\x00'::bytea"; }
                        else if (type.Size == 4) { value = "E'\\\\x00000000'::bytea"; }
                        else if (type.Size == 16) { value = "E'\\\\x00000000000000000000000000000000'::bytea"; }
                        else
                        {
                            value = "E'\\\\x01010800000000000000EFBBBF7B2255227D'::bytea"; // ХранилищеЗначения
                        }
                    }
                    else if (type.IsUuid) { value = "E'\\\\x00000000000000000000000000000000'::bytea"; }
                    else if (type.IsEntity) { value = "E'\\\\x00000000000000000000000000000000'::bytea"; }
                }
                else if (column.Purpose == ColumnPurpose.Tag) { value = "E'\\\\x01'::bytea"; }
                else if (column.Purpose == ColumnPurpose.TypeCode) { value = "E'\\\\x00000000'::bytea"; }
                else if (column.Purpose == ColumnPurpose.Identity) { value = "E'\\\\x00000000000000000000000000000000'::bytea"; }
                else if (column.Purpose == ColumnPurpose.Boolean) { value = "E'\\\\x00'::bytea"; }
                else if (column.Purpose == ColumnPurpose.Numeric) { value = "0"; }
                else if (column.Purpose == ColumnPurpose.DateTime) { value = dateTime; }
                else if (column.Purpose == ColumnPurpose.String) { value = "''::mvarchar"; }

                columnNames.Add(column.Name);
                listOfValues.Add(value);
            }
        }
        private void PgTranspileParameters(in PropertyDefinition property, in ColumnExpression map, in List<string> columnNames, in List<string> listOfValues)
        {
            if (TryTranspileFunction(in property, in map, in columnNames, in listOfValues))
            {
                return;
            }

            int before = columnNames.Count;

            ColumnDefinition column;
            List<ColumnDefinition> columns = property.Columns;

            for (int c = 0; c < columns.Count; c++)
            {
                column = columns[c];

                if (column.IsGenerated)
                {
                    continue; // timestamp column _Version
                }

                columnNames.Add(column.Name);

                string parameterName = GetOrdinalParameterName();

                if (column.Type.IsString)
                {
                    parameterName += "::mvarchar";
                }

                listOfValues.Add(parameterName);
            }

            if (columnNames.Count > before)
            {
                _statement.Input.Add(map);
            }
        }
    }
}