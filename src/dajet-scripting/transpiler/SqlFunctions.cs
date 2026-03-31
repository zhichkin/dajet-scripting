using System.Collections.Frozen;

namespace DaJet.Scripting
{
    internal static class SqlFunctions
    {
        private static readonly FrozenDictionary<Token, SqlFunction> _functions = CreateFunctionLookup();
        private static FrozenDictionary<Token, SqlFunction> CreateFunctionLookup()
        {
            List<KeyValuePair<Token, SqlFunction>> list =
            [
                new KeyValuePair<Token, SqlFunction>(Token.SUM, new SUM())
            ];
            return FrozenDictionary.ToFrozenDictionary(list);
        }
        internal static bool TryGet(Token token, out SqlFunction function)
        {
            return _functions.TryGetValue(token, out function);
        }
    }
}