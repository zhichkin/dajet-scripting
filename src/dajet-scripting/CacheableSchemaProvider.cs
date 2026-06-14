using DaJet.Metadata;
using DaJet.TypeSystem;

namespace DaJet.Scripting
{
    public sealed class CacheableSchemaProvider : ISchemaProvider
    {
        private static readonly Dictionary<string, EntityDefinition> _cache = new();
        public MetadataEntry GetEntry(in string domain, int typeCode)
        {
            MetadataProvider provider = MetadataProvider.Get(in domain);

            return provider.GetMetadataEntry(typeCode);
        }
        public MetadataEntry GetEntry(in string domain, Guid typeUuid)
        {
            MetadataProvider provider = MetadataProvider.Get(in domain);

            return provider.GetMetadataEntry(typeUuid);
        }
        public MetadataEntry GetEntry(in string domain, in string identifier)
        {
            MetadataProvider provider = MetadataProvider.Get(in domain);

            return provider.GetMetadataEntry(in identifier);
        }
        public Entity GetEnumerationEntity(in string domain, in string identifier)
        {
            MetadataProvider provider = MetadataProvider.Get(in domain);

            return provider.GetEnumerationEntity(in identifier);
        }
        public EntityDefinition GetSchema(in string domain, in string identifier)
        {
            string key = string.Format("{0}.{1}", domain, identifier);

            if (_cache.TryGetValue(key, out EntityDefinition schema))
            {
                return schema;
            }

            MetadataProvider provider = MetadataProvider.Get(in domain);

            schema = provider.GetMetadataObject(in identifier);

            _ = _cache.TryAdd(key, schema);

            return schema;
        }
    }
}