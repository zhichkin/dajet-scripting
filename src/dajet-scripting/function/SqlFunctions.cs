using System.Collections.Frozen;

namespace DaJet.Scripting
{
    internal static class SqlFunctions
    {
        private static readonly STRING STRING_FUNCTION = new();
        private static readonly DATETIME DATETIME_FUNCTION = new();
        private static readonly AGGREGATE AGGREGATE_FUNCTION = new();

        private static readonly FrozenDictionary<Token, Function> _functions = CreateFunctionLookup();
        private static FrozenDictionary<Token, Function> CreateFunctionLookup()
        {
            List<KeyValuePair<Token, Function>> list =
            [
                new KeyValuePair<Token, Function>(Token.SUM, AGGREGATE_FUNCTION),
                new KeyValuePair<Token, Function>(Token.MIN, AGGREGATE_FUNCTION),
                new KeyValuePair<Token, Function>(Token.MAX, AGGREGATE_FUNCTION),
                new KeyValuePair<Token, Function>(Token.AVG, AGGREGATE_FUNCTION),
                new KeyValuePair<Token, Function>(Token.COUNT, new COUNT()),

                new KeyValuePair<Token, Function>(Token.UPPER, STRING_FUNCTION),
                new KeyValuePair<Token, Function>(Token.LOWER, STRING_FUNCTION),
                new KeyValuePair<Token, Function>(Token.LTRIM, STRING_FUNCTION),
                new KeyValuePair<Token, Function>(Token.RTRIM, STRING_FUNCTION),
                new KeyValuePair<Token, Function>(Token.CONCAT, STRING_FUNCTION),
                new KeyValuePair<Token, Function>(Token.CONCAT_WS, STRING_FUNCTION),
                new KeyValuePair<Token, Function>(Token.REPLACE, STRING_FUNCTION),
                new KeyValuePair<Token, Function>(Token.SUBSTRING, STRING_FUNCTION),
                new KeyValuePair<Token, Function>(Token.STRING_AGG, STRING_FUNCTION),

                new KeyValuePair<Token, Function>(Token.ISNULL, new ISNULL()),
                new KeyValuePair<Token, Function>(Token.VECTOR, new VECTOR()),
                new KeyValuePair<Token, Function>(Token.EXISTS, new EXISTS()),
                new KeyValuePair<Token, Function>(Token.NEWUUID, new NEWUUID()),
                new KeyValuePair<Token, Function>(Token.NOW, DATETIME_FUNCTION),
                new KeyValuePair<Token, Function>(Token.UTC, DATETIME_FUNCTION),
                new KeyValuePair<Token, Function>(Token.DATESTART, DATETIME_FUNCTION),
                new KeyValuePair<Token, Function>(Token.DATEEND, DATETIME_FUNCTION),
                new KeyValuePair<Token, Function>(Token.DATEADD, DATETIME_FUNCTION),
                new KeyValuePair<Token, Function>(Token.DATEDIFF, new DATEDIFF()),
                new KeyValuePair<Token, Function>(Token.ROW_NUMBER, new ROW_NUMBER()),
                new KeyValuePair<Token, Function>(Token.DATALENGTH, new DATALENGTH()),
                new KeyValuePair<Token, Function>(Token.CHARLENGTH, new CHARLENGTH())
            ];
            return FrozenDictionary.ToFrozenDictionary(list);
        }
        internal static bool TryGet(Token token, out Function function)
        {
            return _functions.TryGetValue(token, out function);
        }
    }
}