using MeloongCore.Extensions;
using Newtonsoft.Json.Linq;
using System.Text;

namespace MeloongCore;

public static class LegacyFileUtils {
    public static JToken? ReadAsJson(string filePath, Encoding? encoding = null, Type? type = null) =>
        FileUtils.ReadAsString(filePath, encoding, type).DeserializeJson();

    public static T? ReadAsJson<T>(string filePath, Encoding? encoding = null, Type? type = null) where T : JToken =>
        (T?)ReadAsJson(filePath, encoding, type);
}
