using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Nodes;
namespace MeloongCore;

#region ConfigProvider | 配置提供器

public interface IConfigProvider
{
    void Set<T>(string key, T? value, bool encrypted);
    void Remove(string key);
    T? Read<T>(string key, T? defaultValue, bool encrypted);
    bool HasValue(string key);
    void DiscardCache();
}

public class JsonConfigProvider : IConfigProvider
{
    public static readonly ConcurrentBag<WeakReference<JsonConfigProvider>> allProviders = [];
    private readonly string filePath;

#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 'required' 修饰符或声明为可以为 null。
    public JsonConfigProvider(string filePath)
    {
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 'required' 修饰符或声明为可以为 null。
        this.filePath = filePath;
        allProviders.Add(new(this));
        saveWorker = new(Save, 500, RateLimitMode.WaitUnillNoNewInvoke);
        InitJson();
    }

    // 原始文件缓存
    private Lazy<JsonObject> json;
    // 反序列化后的结果缓存
    private readonly ConcurrentDictionary<string, object?> cache = new();
    //? Newtonsoft 的 ToObject/反序列化默认大小写不敏感，STJ 需显式开启以保持 Config<T> 读取语义不变。
    //? IncludeFields：STJ 默认只序列化属性，公开字段型配置类（Folder/Version 等公开字段成员）会被落盘为 {}，
    //? 读取时经构造器参数绑定反序列化即失败。
    //? Version 转换器：System.Version 的公共属性全部只读，STJ 无法反序列化，需以字符串形态往返，
    //? 否则 Java 列表里的版本号会静默清零。
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        IncludeFields = true,
        Converters = { new VersionToStringConverter() },
    };
    //? UnsafeRelaxedJsonEscaping 让中文等非 ASCII 字符原样写入，与旧 Newtonsoft 的文件布局保持一致。
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
    private void InitJson()
    {
        json = new(() =>
        {
            try
            {
                return FileUtils.Exists(filePath) ? FileUtils.ReadAsJson(filePath) as JsonObject ?? new JsonObject() : new JsonObject();
            } catch (Exception ex)
            {
                Logger.Error(ex, $"读取配置文件失败（{filePath}）", LogBehavior.Alert);
                return new JsonObject();
            }
        });
    }

    // ================================= 设置、读取、缓存 =================================

    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "Config<T> 不在 AOT 路径上，泛型反射已在 TECHDEBT 第 8 节记录")]
    public void Set<T>(string key, T? value, bool encrypted)
    {
        cache[key] = value;
        lock (this)
        {
            if (value is null)
            {
                // 直接在 JSON 中表示为 null
                json.Value[key] = null;
                Logger.Trace($"配置已修改：{key} = null（{filePath}）");
            } else if (encrypted)
            {
                // 需要加密，在 JSON 中保存密文字符串
                throw new NotImplementedException("加密需要改为使用识别码，现在尚未实现。");
                // Logger.Trace($"配置已修改：{key} = 已加密（{filePath}）");
                // json.Value[key] = CryptographyUtils.AesEncrypt(value is string str ? str : JsonSerializer.Serialize(value));
            } else
            {
                // 用 JsonNode 保留原始结构
                JsonNode node = JsonSerializer.SerializeToNode(value, SerializerOptions)!;
                json.Value[key] = node;
                Logger.Trace($"配置已修改：{key} = {node.ToJsonString()}（{filePath}）");
            }
        }
        MakeDirty();
    }
    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "Config<T> 不在 AOT 路径上，泛型反射已在 TECHDEBT 第 8 节记录")]
    public T? Read<T>(string key, T? defaultValue, bool encrypted)
    {
        if (cache.TryGetValue(key, out var cachedValue)) return (T?) cachedValue; // 读取缓存
        JsonNode? entry;
        lock (this)
        {
            if (!json.Value.TryGetPropertyValue(key, out entry)) return defaultValue; // 未找到该键
            entry = entry?.DeepClone();
        }
        try
        {
            T? result;
            if (entry is null)
            {
                result = default;
            } else if (encrypted)
            {
                string plainText = CryptographyUtils.AesDecrypt(entry.GetValue<string>())!;
                result = typeof(T) == typeof(string) ? (T) (object) plainText : JsonSerializer.Deserialize<T>(plainText, SerializerOptions);
            } else
            {
                result = entry.Deserialize<T>(SerializerOptions);
            }
            cache[key] = result;
            return result;
        } catch (CryptographicException ex)
        {
            Logger.Error(ex, $"解密配置失败，该配置将被重置（{key}）", LogBehavior.Alert);
            Remove(key);
            return defaultValue;
        }
    }

    /// <summary>
    /// System.Version 以其 ToString() 字符串形态往返（如 "16.0.1"）。
    /// STJ 无法反序列化 Version（公共属性全部只读，且存在多个公共构造器），原样往返会把版本数据清零。
    /// </summary>
    private sealed class VersionToStringConverter : JsonConverter<Version>
    {
        public override Version? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => reader.GetString() is { } text ? new Version(text) : null;
        public override void Write(Utf8JsonWriter writer, Version value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.ToString());
    }

    public bool HasValue(string key)
    {
        lock (this) return json.Value.ContainsKey(key);
    }
    public void Remove(string key)
    {
        lock (this) json.Value.Remove(key);
        cache.TryRemove(key, out _);
        MakeDirty();
    }
    public void DiscardCache()
    {
        lock (this) InitJson();
        cache.Clear();
    }

    // ===================================== 保存 =====================================

    private readonly RateLimitedWorker saveWorker;
    private bool isDirty = false;
    public void MakeDirty()
    {
        lock (this) isDirty = true;
        saveWorker.Start();
    }
    /// <summary>同步保存配置，并等待同一实例正在进行的保存完成；保存失败时保留待保存状态。</summary>
    public void Save()
    {
        lock (this)
        {
            if (!isDirty) return;
            FileUtils.WriteAtomic(filePath, json.Value.ToJsonString(WriteOptions));
            isDirty = false;
        }
    }
}

public static class ConfigProviders
{
    public static JsonConfigProvider Default = new(Path.Combine(Paths.AppDataThenName, "config.json"));
}

#endregion

#region ConfigEntry | 单个配置项

/// <summary>
/// 当前获取到的配置值。
/// </summary>
public class PreviewGetEventArgs<T>(T? value) : EventArgs
{
    public T? Value { get; set; } = value;
}

/// <summary>
/// 使用固定的 <see cref="IConfigProvider"/> 的配置项。
/// <para/>若不指定 <paramref name="provider"/>，则使用 <see cref="ConfigProviders.Default"/>。
/// </summary>
public class ConfigEntry<T>(string key, T? defaultValue, bool encrypted = false, IConfigProvider? provider = null)
{
    public IConfigProvider Provider { get; } = provider ?? ConfigProviders.Default;

    /// <summary>
    /// 当调整配置项的值时触发，参数为新值。<para/>新值可能和旧值相同。
    /// </summary>
    public event Action<T?>? Changed;
    /// <summary>
    /// 当获取配置项的值时触发。
    /// </summary>
    public event Action<PreviewGetEventArgs<T>, IConfigProvider>? PreviewGet;

    public void Edit(Func<T?, T?> editFunc) => Set(editFunc(Get()));
    public void Edit(RefAction editAction)
    {
        var current = Get();
        editAction(ref current);
        Set(current);
    }
    public delegate void RefAction(ref T? value);

    public void Set(T? value)
    {
        Provider.Set(key, value, encrypted);
        Changed?.Invoke(value);
    }
    public void Reset()
    {
        if (!Provider.HasValue(key)) return; // 值未设置，无需重置
        Provider.Remove(key);
        Changed?.Invoke(defaultValue);
    }
    public T? Get()
    {
        PreviewGetEventArgs<T> args = new(Provider.Read(key, defaultValue, encrypted));
        PreviewGet?.Invoke(args, Provider);
        return args.Value;
    }
    public bool HasValue() => Provider.HasValue(key);
}

/// <summary>
/// 在每次操作时动态指定 <see cref="IConfigProvider"/> 的配置项。
/// </summary>
public class DynamicConfigEntry<T>(string key, T? defaultValue, bool encrypted = false)
{
    /// <summary>
    /// 当调整配置项的值时触发，参数为新值。<para/>新值可能和旧值相同。
    /// </summary>
    public event Action<T?>? Changed;
    /// <summary>
    /// 当获取配置项的值时触发。
    /// </summary>
    public event Action<PreviewGetEventArgs<T>, IConfigProvider>? PreviewGet;


    public void Edit(Func<T?, T?> editFunc, IConfigProvider provider) => Set(editFunc(Get(provider)), provider);
    public void Edit(RefAction editAction, IConfigProvider provider)
    {
        var current = Get(provider);
        editAction(ref current);
        Set(current, provider);
    }
    public delegate void RefAction(ref T? value);

    public void Set(T? value, IConfigProvider provider)
    {
        provider.Set(key, value, encrypted);
        Changed?.Invoke(value);
    }
    public void Reset(IConfigProvider provider)
    {
        if (!provider.HasValue(key)) return; // 值未设置，无需重置
        provider.Remove(key);
        Changed?.Invoke(defaultValue);
    }
    public T? Get(IConfigProvider provider)
    {
        PreviewGetEventArgs<T> args = new(provider.Read(key, defaultValue, encrypted));
        PreviewGet?.Invoke(args, provider);
        return args.Value;
    }

    public bool HasValue(IConfigProvider provider) => provider.HasValue(key);
}

#endregion

public static class ConfigUtils
{

    /// <summary>立即保存所有配置。</summary>
    public static void SaveAll()
    {
        foreach (var weakRef in JsonConfigProvider.allProviders)
        {
            if (weakRef.TryGetTarget(out var provider)) provider.Save();
        }
    }

    private static readonly Lazy<JsonObject> secret = new(() =>
    {
        string filePath = Path.Combine(Paths.AppData, "Meloong", "Dev", "secret.json");
        try
        {
            return FileUtils.Exists(filePath) ? JsonNode.Parse(FileUtils.ReadAsString(filePath)) as JsonObject ?? throw new JsonException("secret.json 不是 JSON 对象") : new JsonObject();
        } catch
        {
            throw new InvalidDataException($"读取密钥文件失败（{filePath}），请检查文件内容与加密格式。");
        }
    });

    /// <summary>从 secret.json 中读取加密的密钥；如果未找到对应密钥则抛出异常。</summary>
    public static string GetSecret(string key)
    {
        string value = secret.Value[key]?.GetValue<string>() ?? throw new KeyNotFoundException($"未找到密钥：{key}");
        try
        {
            return CryptographyUtils.AesDecrypt(value)!;
        } catch
        {
            throw new InvalidDataException($"读取密钥失败（{key}），请检查文件内容与加密格式。");
        }
    }

}