using System.Collections.Frozen;

namespace DaJet.Scripting
{
    internal static class PgSqlFunctions
    {
        private static readonly PG_STRING STRING_FUNCTION = new();
        private static readonly PG_DATETIME DATETIME_FUNCTION = new();

        private static readonly FrozenDictionary<Token, Function> _functions = CreateFunctionLookup();
        private static FrozenDictionary<Token, Function> CreateFunctionLookup()
        {
            List<KeyValuePair<Token, Function>> list =
            [
                new KeyValuePair<Token, Function>(Token.UPPER, STRING_FUNCTION),
                new KeyValuePair<Token, Function>(Token.LOWER, STRING_FUNCTION),
                new KeyValuePair<Token, Function>(Token.LTRIM, STRING_FUNCTION),
                new KeyValuePair<Token, Function>(Token.RTRIM, STRING_FUNCTION),
                new KeyValuePair<Token, Function>(Token.CONCAT, STRING_FUNCTION),
                new KeyValuePair<Token, Function>(Token.CONCAT_WS, STRING_FUNCTION),
                new KeyValuePair<Token, Function>(Token.REPLACE, STRING_FUNCTION),
                new KeyValuePair<Token, Function>(Token.STRING_AGG, STRING_FUNCTION),
                new KeyValuePair<Token, Function>(Token.NOW, DATETIME_FUNCTION),
                new KeyValuePair<Token, Function>(Token.UTC, DATETIME_FUNCTION),
                new KeyValuePair<Token, Function>(Token.DATESTART, DATETIME_FUNCTION),
                new KeyValuePair<Token, Function>(Token.DATEEND, DATETIME_FUNCTION),
                new KeyValuePair<Token, Function>(Token.DATEADD, DATETIME_FUNCTION),
                new KeyValuePair<Token, Function>(Token.DATEDIFF, new PG_DATEDIFF()),
                new KeyValuePair<Token, Function>(Token.SUBSTRING, new PG_SUBSTRING()),
                new KeyValuePair<Token, Function>(Token.ISNULL, new PG_ISNULL()),
                new KeyValuePair<Token, Function>(Token.VECTOR, new PG_VECTOR()),
                new KeyValuePair<Token, Function>(Token.NEWUUID, new PG_NEWUUID()),
                new KeyValuePair<Token, Function>(Token.DATALENGTH, new PG_DATALENGTH()),
                new KeyValuePair<Token, Function>(Token.CHARLENGTH, new PG_CHARLENGTH())
            ];
            return FrozenDictionary.ToFrozenDictionary(list);
        }
        internal static bool TryGet(Token token, out Function function)
        {
            return _functions.TryGetValue(token, out function);
        }
    }
}