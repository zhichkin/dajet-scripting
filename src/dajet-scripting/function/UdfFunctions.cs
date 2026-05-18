using System.Collections.Frozen;

namespace DaJet.Scripting
{
    internal static class UdfFunctions
    {
        private static readonly FrozenDictionary<string, UdfFunction> _functions = CreateFunctionLookup();
        private static FrozenDictionary<string, UdfFunction> CreateFunctionLookup()
        {
            List<KeyValuePair<string, UdfFunction>> list =
            [
                new KeyValuePair<string, UdfFunction>(nameof(JSON), new JSON()),
                new KeyValuePair<string, UdfFunction>(nameof(TYPEOF), new TYPEOF()),
                new KeyValuePair<string, UdfFunction>(nameof(UUIDOF), new UUIDOF())
            ];
            return FrozenDictionary.ToFrozenDictionary(list);
        }
        internal static bool TryGet(string name, out UdfFunction function)
        {
            return _functions.TryGetValue(name, out function);
        }
    }
}