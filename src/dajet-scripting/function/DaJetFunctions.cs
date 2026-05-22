using System.Collections.Frozen;

namespace DaJet.Scripting
{
    public static class DaJetFunctions
    {
        private static readonly FrozenDictionary<string, DaJetFunction> _functions = CreateFunctionLookup();
        private static FrozenDictionary<string, DaJetFunction> CreateFunctionLookup()
        {
            List<KeyValuePair<string, DaJetFunction>> list =
            [
                new KeyValuePair<string, DaJetFunction>(nameof(JSON), new JSON()),
                new KeyValuePair<string, DaJetFunction>(nameof(TYPEOF), new TYPEOF()),
                new KeyValuePair<string, DaJetFunction>(nameof(UUIDOF), new UUIDOF())
            ];
            return FrozenDictionary.ToFrozenDictionary(list);
        }
        public static bool Contains(string name)
        {
            return _functions.ContainsKey(name);
        }
        public static bool TryGet(string name, out DaJetFunction function)
        {
            return _functions.TryGetValue(name, out function);
        }
    }
}