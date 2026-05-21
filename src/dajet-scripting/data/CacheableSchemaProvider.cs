using DaJet.Metadata;
using DaJet.TypeSystem;

namespace DaJet.Scripting
{
    internal sealed class CacheableSchemaProvider : ISchemaProvider
    {
        private static readonly Dictionary<string, EntityDefinition> _cache = new();
        public EntityDefinition GetSchema(in string domain, in string identifier)
        {
            string key = string.Format("{0}.{1}", domain, identifier);

            if (_cache.TryGetValue(key, out EntityDefinition schema))
            {
                return schema;
            }

            schema = MetadataProvider.Get(in domain).GetMetadataObject(in identifier);

            _ = _cache.TryAdd(key, schema);

            return schema;
        }
    }
}