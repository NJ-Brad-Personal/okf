using System.Text.Json.Serialization;
using SharpYaml.Serialization;

namespace okf;

[YamlSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[YamlSerializable(typeof(ConceptDocumentYaml))]
[YamlSerializable(typeof(Dictionary<string, object?>))]
[YamlSerializable(typeof(List<string>))]
internal partial class OkfYamlContext : YamlSerializerContext;