using Newtonsoft.Json.Serialization;
using Newtonsoft.Json;

namespace JCTG
{
    public class IgnoreJsonPropertyContractResolver : DefaultContractResolver
    {
        protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
        {
            var properties = base.CreateProperties(type, memberSerialization);
            foreach (var p in properties) { p.PropertyName = p.UnderlyingName; }
            return properties;
        }
    }
}
