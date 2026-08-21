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
                new KeyValuePair<string, Function>(nameof(UTC), new UTC()),
                new KeyValuePair<string, Function>(nameof(ERROR_MESSAGE), new ERROR_MESSAGE()),
                new KeyValuePair<string, Function>(nameof(DATESTART), new DATESTART()),
                new KeyValuePair<string, Function>(nameof(DATEEND), new DATEEND()),
                new KeyValuePair<string, Function>(nameof(DATEADD), new DATEADD()),
                new KeyValuePair<string, Function>(nameof(DATEDIFF), new DATEDIFF())
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