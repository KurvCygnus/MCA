using System.Globalization;
using Newtonsoft.Json.Linq;

namespace MeloongCore.Tests;

public class LegacyJsonExtensionsTest {

    [Test]
    public async Task DeserializeJson_日期保持字符串且可还原同一时刻() {
        var token = "{\"time\": \"2026-10-23T08:30:00Z\"}".DeserializeJson<JObject>();
        await Assert.That(token).IsNotNull();
        var value = token!.Value<string>("time");
        await Assert.That(value).IsEqualTo("2026-10-23T08:30:00Z");
        var instant = DateTime.Parse(value!, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal).ToUniversalTime();
        await Assert.That(instant.ToBinary()).IsEqualTo(new DateTime(2026, 10, 23, 8, 30, 0, DateTimeKind.Utc).ToBinary());
    }

    [Test]
    public async Task DeserializeJson_空白输入返回Null() {
        await Assert.That(((string?)null).DeserializeJson()).IsNull();
        await Assert.That("   ".DeserializeJson()).IsNull();
    }

}
