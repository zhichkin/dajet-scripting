using System.Collections.Frozen;

namespace DaJet.Scripting
{
    public static class DaJetFunctions
    {
        private static readonly FrozenDictionary<string, Function> _functions = CreateFunctionLookup();
        private static FrozenDictionary<string, Function> CreateFunctionLookup()
        {
            List<KeyValuePair<string, Function>> list =
            [
                new KeyValuePair<string, Function>(nameof(JSON), new JSON()),
                new KeyValuePair<string, Function>(nameof(TYPEOF), new TYPEOF()),
                new KeyValuePair<string, Function>(nameof(UUIDOF), new UUIDOF()),
                new KeyValuePair<string, Function>(nameof(NOW), new NOW()),
                new KeyValuePair<string, Function>(nameof(UTC), new UTC())
            ];
            return FrozenDictionary.ToFrozenDictionary(list);
        }
        public static bool Contains(string name)
        {
            return _functions.ContainsKey(name);
        }
        public static bool TryGet(string name, out Function function)
        {
            return _functions.TryGetValue(name, out function);
        }
    }
}