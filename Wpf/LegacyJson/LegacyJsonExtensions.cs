using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text.Json.Nodes;

namespace MeloongCore.Extensions;

public static class LegacyJsonExtensions
{
    private static readonly JsonSerializerSettings Settings = new()
    {
        DateParseHandling = DateParseHandling.None
    };

    public static JToken? DeserializeJson(this string? data) =>
        string.IsNullOrWhiteSpace(data) ? null : JsonConvert.DeserializeObject<JToken>(data!, Settings);

    public static T? DeserializeJson<T>(this string? data) where T : JToken =>
        (T?) DeserializeJson(data);

    public static JsonNode? ToJsonNode(this JToken? token) =>
        token is null ? null : JsonNode.Parse(token.ToString(Formatting.None));

    public static JToken? ToJToken(this JsonNode? node) =>
        node is null ? null : JToken.Parse(node.ToJsonString());
}