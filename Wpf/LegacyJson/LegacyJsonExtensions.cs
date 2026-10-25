using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MeloongCore.Extensions;

public static class LegacyJsonExtensions {
    private static readonly JsonSerializerSettings Settings = new() {
        DateParseHandling = DateParseHandling.None
    };

    public static JToken? DeserializeJson(this string? data) =>
        string.IsNullOrWhiteSpace(data) ? null : JsonConvert.DeserializeObject<JToken>(data!, Settings);

    public static T? DeserializeJson<T>(this string? data) where T : JToken =>
        (T?)DeserializeJson(data);
}
