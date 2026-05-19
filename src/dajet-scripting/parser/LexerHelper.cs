using DaJet.TypeSystem;

namespace DaJet.Scripting
{
    internal static class LexerHelper
    {
        private static Dictionary<string, Token> _keywords = new()
        {
            { "WITH", Token.WITH },
            { "SELECT", Token.SELECT },
            { "INTO", Token.INTO },
            { "DISTINCT", Token.DISTINCT },
            { "TOP", Token.TOP },
            { "FROM", Token.FROM },
            { "WHERE", Token.WHERE },
            { "ORDER", Token.ORDER },
            { "BY", Token.BY },
            { "ASC", Token.ASC },
            { "DESC", Token.DESC },
            { "AND", Token.AND },
            { "OR", Token.OR },
            { "AS", Token.AS },
            { "NOT", Token.NOT },
            { "JOIN", Token.JOIN },
            { "LEFT", Token.LEFT },
            { "RIGHT", Token.RIGHT },
            { "FULL", Token.FULL },
            { "INNER", Token.INNER },
            { "CROSS", Token.CROSS },
            { "OUTER", Token.OUTER },
            { "APPLY", Token.APPLY },
            { "ON", Token.ON },
            { "DECLARE", Token.DECLARE },
            { "NOLOCK", Token.NOLOCK },
            { "ROWLOCK", Token.ROWLOCK },
            { "READPAST", Token.READPAST },
            { "UPDLOCK", Token.UPDLOCK },
            { "SERIALIZABLE", Token.SERIALIZABLE },
            { "READCOMMITTEDLOCK", Token.READCOMMITTEDLOCK },
            { "INSERT", Token.INSERT },
            { "VALUES", Token.VALUES },
            { "UPDATE", Token.UPDATE },
            { "DELETE", Token.DELETE },
            { "OUTPUT", Token.OUTPUT },
            { "SET", Token.SET },
            { "ROW", Token.ROW },
            { "ROWS", Token.ROWS },
            { "ONLY", Token.ONLY },
            { "OFFSET", Token.OFFSET },
            { "FETCH", Token.FETCH },
            { "FIRST", Token.FIRST },
            { "NEXT", Token.NEXT },
            { "GROUP", Token.GROUP },
            { "HAVING", Token.HAVING },
            { "OVER", Token.OVER },
            { "PARTITION", Token.PARTITION },
            { "RANGE", Token.RANGE },
            { "BETWEEN", Token.BETWEEN },
            { "UNBOUNDED", Token.UNBOUNDED },
            { "PRECEDING", Token.PRECEDING },
            { "CURRENT", Token.CURRENT },
            { "FOLLOWING", Token.FOLLOWING },
            { "CASE", Token.CASE },
            { "WHEN", Token.WHEN },
            { "THEN", Token.THEN },
            { "ELSE", Token.ELSE },
            { "BEGIN", Token.BEGIN },
            { "END", Token.END },
            { "EXISTS", Token.EXISTS },
            { "UNION", Token.UNION },
            { "ALL", Token.ALL },
            { "ANY", Token.ANY },
            { "IN", Token.IN },
            { "LIKE", Token.LIKE },
            { "IS", Token.IS },
            { "NULL", Token.NULL },
            { "CREATE", Token.CREATE },
            { "TABLE", Token.TABLE },
            { "VARIABLE", Token.VARIABLE },
            { "TEMPORARY", Token.TEMPORARY },
            { "UPSERT", Token.UPSERT },
            { "IGNORE", Token.IGNORE },
            { "TYPE", Token.TYPE },
            { "COLUMN", Token.COLUMN },
            { "OF", Token.OF },
            { "DROP", Token.DROP },
            { "CONSUME", Token.CONSUME },
            { "STRICT", Token.STRICT },
            { "RANDOM", Token.RANDOM },
            { "IMPORT", Token.IMPORT },
            { "SEQUENCE", Token.SEQUENCE },
            { "START", Token.START },
            { "INCREMENT", Token.INCREMENT },
            { "CACHE", Token.CACHE },
            { "USE", Token.USE },
            { "APPEND", Token.APPEND },
            { "FOR", Token.FOR },
            { "EACH", Token.EACH },
            { "MAXDOP", Token.MAXDOP },
            { "PRODUCE", Token.PRODUCE },
            { "STREAM", Token.STREAM },
            { "REQUEST", Token.REQUEST },
            { "REVOKE", Token.REVOKE },
            { "RECALCULATE", Token.RECALCULATE },
            { "IF", Token.IF },
            { "WHILE", Token.WHILE },
            { "PRINT", Token.PRINT },
            { "RETURN", Token.RETURN },
            { "BREAK", Token.BREAK },
            { "CONTINUE", Token.CONTINUE },
            { "EXECUTE", Token.EXECUTE },
            { "PROCESS", Token.PROCESS },
            { "TRY", Token.TRY },
            { "CATCH", Token.CATCH },
            { "FINALLY", Token.FINALLY },
            { "THROW", Token.THROW },
            { "SLEEP", Token.SLEEP },
            { "DEFAULT", Token.DEFAULT },
            { "TASK", Token.TASK },
            { "WORK", Token.WORK },
            { "SYNC", Token.SYNC },
            { "WAIT", Token.WAIT },
            { "TIMEOUT", Token.TIMEOUT },
            { "MODIFY", Token.MODIFY },
            { "DEFINE", Token.DEFINE }
        };
        private static Dictionary<string, Token> _function = new()
        {
            { "SUM", Token.SUM },
            { "MAX", Token.MAX },
            { "MIN", Token.MIN },
            { "AVG", Token.AVG },
            { "COUNT", Token.COUNT },
            { "ISNULL", Token.ISNULL },
            { "ROW_NUMBER", Token.ROW_NUMBER },
            { "SUBSTRING", Token.SUBSTRING },
            { "DATALENGTH", Token.DATALENGTH },
            { "NOW", Token.NOW },
            { "UTC", Token.UTC },
            { "VECTOR", Token.VECTOR },
            { "STRING_AGG", Token.STRING_AGG },
            { "CHARLENGTH", Token.CHARLENGTH },
            { "CONCAT", Token.CONCAT },
            { "CONCAT_WS", Token.CONCAT_WS },
            { "REPLACE", Token.REPLACE },
            { "LOWER", Token.LOWER },
            { "UPPER", Token.UPPER },
            { "LTRIM", Token.LTRIM },
            { "RTRIM", Token.RTRIM },
            { "LAG", Token.LAG },
            { "LEAD", Token.LEAD },
            { "FIRST_VALUE", Token.FIRST_VALUE },
            { "LAST_VALUE", Token.LAST_VALUE },
            { "NEWUUID", Token.NEWUUID }
        };
        
        internal static bool IsKeyword(string identifier, out Token token)
        {
            return _keywords.TryGetValue(identifier, out token);
        }
        internal static bool IsNullLiteral(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                return false;
            }

            string test = identifier.ToLowerInvariant();

            return (test == "null");
        }
        internal static bool IsTrueLiteral(string literal)
        {
            if (string.IsNullOrWhiteSpace(literal))
            {
                return false;
            }
            return ("true".Equals(literal, StringComparison.InvariantCultureIgnoreCase));
        }
        internal static bool IsFalseLiteral(string literal)
        {
            if (string.IsNullOrWhiteSpace(literal))
            {
                return false;
            }
            return ("false".Equals(literal, StringComparison.InvariantCultureIgnoreCase));
        }
        internal static bool IsBooleanLiteral(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                return false;
            }

            string test = identifier.ToLowerInvariant();

            return (test == "true" || test == "false");
        }
        internal static bool IsAlpha(char character)
        {
            return character == '_' || character == '-'
                || character == '[' || character == ']'
                || character == '\'' || character == '\''
                || character == '=' || character == '@'
                || character == '.' // multipart identifier
                || (character >= 'A' && character <= 'Z')
                || (character >= 'a' && character <= 'z')
                || (character >= 'А' && character <= 'Я')
                || (character >= 'а' && character <= 'я')
                || (character == 'Ё' || character == 'ё');
        }
        internal static bool IsNumeric(char character)
        {
            return (character >= '0' && character <= '9');
        }
        internal static bool IsAlphaNumeric(char character)
        {
            return IsAlpha(character) || IsNumeric(character);
        }
        internal static bool IsHexAlpha(char character)
        {
            return (character >= 'A' && character <= 'F')
                || (character >= 'a' && character <= 'f');
        }
        internal static bool IsHexadecimal(char character)
        {
            return IsNumeric(character) || IsHexAlpha(character);
        }
        internal static bool IsFunction(string identifier, out Token token)
        {
            return _function.TryGetValue(identifier, out token);
        }

        internal static string GetComparisonLiteral(Token token)
        {
            if (token == Token.IS) { return "IS"; }
            else if (token == Token.IN) { return "IN"; }
            else if (token == Token.LIKE) { return "LIKE"; }
            else if (token == Token.BETWEEN) { return "BETWEEN"; }
            else if (token == Token.Equals) { return "="; }
            else if (token == Token.NotEquals) { return "<>"; }
            else if (token == Token.Less) { return "<"; }
            else if (token == Token.LessOrEquals) { return "<="; }
            else if (token == Token.Greater) { return ">"; }
            else if (token == Token.GreaterOrEquals) { return ">="; }

            return token.ToString();
        }

        internal static string GetUuidHexLiteral(Guid uuid)
        {
            string value = uuid.ToString("N");

            return string.Concat(
                value.AsSpan(16, 16),
                value.AsSpan(12, 4),
                value.AsSpan(8, 4),
                value.AsSpan(0, 8));

            // SqlServer return $"0x{value}";

            // PostgreSql return $"CAST(E'\\\\x{value}' AS bytea)";
        }

        //public static ScalarExpression CreateDefaultScalar(Type type)
        //{
        //    string literal = string.Empty;
        //    TokenType token = TokenType.String;

        //    if (type == typeof(bool))
        //    {
        //        literal = "false";
        //        token = TokenType.Boolean;
        //    }
        //    else if (type == typeof(int))
        //    {
        //        literal = "0";
        //        token = TokenType.Integer;
        //    }
        //    else if (type == typeof(decimal))
        //    {
        //        literal = "0.00";
        //        token = TokenType.Number;
        //    }
        //    else if (type == typeof(DateTime))
        //    {
        //        literal = "0001-01-01T00:00:00";
        //        token = TokenType.DateTime;
        //    }
        //    else if (type == typeof(string))
        //    {
        //        literal = string.Empty;
        //        token = TokenType.String;
        //    }
        //    else if (type == typeof(byte[]))
        //    {
        //        literal = "0x00";
        //        token = TokenType.Binary;
        //    }
        //    else if (type == typeof(Guid))
        //    {
        //        literal = "00000000-0000-0000-0000-000000000000";
        //        token = TokenType.Uuid;
        //    }
        //    else if (type == typeof(Entity))
        //    {
        //        literal = "{0:00000000-0000-0000-0000-000000000000}";
        //        token = TokenType.Entity;
        //    }
        //    else if (type == typeof(Union))
        //    {
        //        literal = "union";
        //        token = TokenType.Union;
        //    }
        //    else if (type == typeof(object))
        //    {
        //        literal = "NULL";
        //        token = TokenType.Object;
        //    }
        //    else if (type == typeof(Array))
        //    {
        //        literal = "NULL";
        //        token = TokenType.Array;
        //    }

        //    return new ScalarExpression() { Token = token, Literal = literal };
        //}
        //public static object GetScalarValue(in ScalarExpression scalar)
        //{
        //    object value = null;
        //    string literal = scalar.Literal;
        //    TokenType token = scalar.Token;

        //    if (token == TokenType.NULL)
        //    {
        //        return null;
        //    }
        //    else if (token == TokenType.Boolean)
        //    {
        //        if (literal.Equals("true", StringComparison.InvariantCultureIgnoreCase))
        //        {
        //            value = true;
        //        }
        //        else if (literal.Equals("false", StringComparison.InvariantCultureIgnoreCase))
        //        {
        //            value = false;
        //        }
        //    }
        //    else if (token == TokenType.Number || token == TokenType.Decimal)
        //    {
        //        value = decimal.Parse(literal, CultureInfo.InvariantCulture);
        //    }
        //    else if (token == TokenType.Integer)
        //    {
        //        value = int.Parse(literal, CultureInfo.InvariantCulture);
        //    }
        //    else if (token == TokenType.DateTime)
        //    {
        //        value = DateTime.Parse(literal);
        //    }
        //    else if (token == TokenType.String)
        //    {
        //        value = literal;
        //    }
        //    else if (token == TokenType.Uuid)
        //    {
        //        value = new Guid(literal);
        //    }
        //    else if (token == TokenType.Binary)
        //    {
        //        value = DbUtilities.StringToByteArray(literal[2..]); // remove leading 0x
        //    }
        //    else if (token == TokenType.Entity)
        //    {
        //        // Metadata object reference parameter:
        //        // DECLARE @product entity = {50:9a1984dc-3084-11ed-9cd7-408d5c93cc8e};
        //        value = Entity.Parse(scalar.Literal);
        //    }

        //    return value;
        //}
        //public static object GetDefaultValue(in TypeIdentifier identifier)
        //{
        //    if (IsDataType(identifier.Identifier, out Type type))
        //    {
        //        if (type == typeof(bool)) { return false; }
        //        else if (type == typeof(int)) { return 0; }
        //        else if (type == typeof(decimal)) { return 0.00M; }
        //        else if (type == typeof(DateTime)) { return DateTime.MinValue; }
        //        else if (type == typeof(string)) { return string.Empty; }
        //        else if (type == typeof(byte[])) { return Array.Empty<byte>(); }
        //        else if (type == typeof(Guid)) { return Guid.Empty; }
        //        else if (type == typeof(Entity)) { return Entity.Undefined; }
        //        else if (type == typeof(object)) { return null; }
        //        else if (type == typeof(Array)) { return null; }
        //    }
        //    return null;
        //}
    }
}