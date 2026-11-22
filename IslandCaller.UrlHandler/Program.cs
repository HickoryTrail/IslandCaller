using System.Globalization;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

const string pipeName = "IslandCaller.Lite";
const string clientName = "IslandCaller.UrlHandler";

#if DEBUG
NativeConsole.AttachConsole(NativeConsole.AttachParentProcess);
#endif

Console.WriteLine($"[{DateTimeOffset.Now:O}] IslandCaller URL Handler 启动，收到 {args.Length} 个参数。");

if (!TryParseArguments(args, out var count, out var gender, out var error))
{
    Console.WriteLine($"[{DateTimeOffset.Now:O}] 参数解析失败：{error}");
    Console.Error.WriteLine(error);
    return 1;
}

Console.WriteLine($"[{DateTimeOffset.Now:O}] 参数解析成功：count={count}, gender={gender}。");

var request = new RequestEnvelope
{
    JsonRpc = "2.0",
    Version = "1.0",
    Method = "getRandomStudent",
    Parameters = new RequestParameters
    {
        ClientName = clientName,
        Count = count,
        Gender = gender,
        Notify = true
    },
    Id = 1
};

try
{
    Console.WriteLine($"[{DateTimeOffset.Now:O}] 正在连接命名管道 {pipeName}。");
    await using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
    await pipe.ConnectAsync(5000);
    Console.WriteLine($"[{DateTimeOffset.Now:O}] IPC 连接已建立，正在发送请求。");
    var requestJson = JsonSerializer.Serialize(request, UrlHandlerJsonContext.Default.RequestEnvelope);
    var requestBytes = Encoding.UTF8.GetBytes(requestJson);
    Console.WriteLine($"[{DateTimeOffset.Now:O}] 请求 JSON：{requestJson}");
    await pipe.WriteAsync(requestBytes);
    await pipe.FlushAsync();
    Console.WriteLine($"[{DateTimeOffset.Now:O}] 请求已发送，等待主程序响应。");

    using var reader = new StreamReader(pipe, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
    var response = await reader.ReadToEndAsync();
    Console.WriteLine($"[{DateTimeOffset.Now:O}] 收到主程序响应：{response}");
    Console.WriteLine(response);
    return 0;
}
catch (TimeoutException)
{
    Console.WriteLine($"[{DateTimeOffset.Now:O}] IPC 连接超时。");
    Console.Error.WriteLine("连接 IslandCaller 主程序超时，请确认主程序已启动。");
    return 2;
}
catch (Exception ex)
{
    Console.WriteLine($"[{DateTimeOffset.Now:O}] 请求发生异常：{ex}");
    Console.Error.WriteLine($"请求 IslandCaller 失败：{ex.Message}");
    return 3;
}

static bool TryParseArguments(string[] arguments, out int count, out string gender, out string error)
{
    count = 0;
    gender = string.Empty;
    error = string.Empty;

    if (arguments.Length != 1 || !Uri.TryCreate(arguments[0], UriKind.Absolute, out var uri))
    {
        error = "用法：IslandCaller.UrlHandler <URI>（查询参数需包含 count 和 gender）";
        return false;
    }

    var query = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    foreach (var part in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
    {
        var parts = part.Split('=', 2);
        if (parts.Length == 2)
        {
            query.TryAdd(Uri.UnescapeDataString(parts[0]), Uri.UnescapeDataString(parts[1]));
        }
    }

    if (!query.TryGetValue("count", out var countValue) ||
        !int.TryParse(countValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out count) || count <= 0)
    {
        error = "count 必须为正整数。";
        return false;
    }

    if (!query.TryGetValue("gender", out gender!) || gender is not ("all" or "male" or "female"))
    {
        error = "gender 必须为 all、male 或 female。";
        return false;
    }

    return true;
}

internal sealed record RequestEnvelope
{
    [JsonPropertyName("jsonrpc")]
    public required string JsonRpc { get; init; }

    [JsonPropertyName("version")]
    public required string Version { get; init; }

    [JsonPropertyName("method")]
    public required string Method { get; init; }

    [JsonPropertyName("params")]
    public required RequestParameters Parameters { get; init; }

    [JsonPropertyName("id")]
    public required int Id { get; init; }
}

internal sealed record RequestParameters
{
    [JsonPropertyName("clientName")]
    public required string ClientName { get; init; }

    [JsonPropertyName("count")]
    public required int Count { get; init; }

    [JsonPropertyName("gender")]
    public required string Gender { get; init; }

    [JsonPropertyName("notify")]
    public required bool Notify { get; init; }
}

[JsonSerializable(typeof(RequestEnvelope))]
internal partial class UrlHandlerJsonContext : JsonSerializerContext
{
}

#if DEBUG
internal static class NativeConsole
{
    internal const int AttachParentProcess = -1;

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    internal static extern bool AttachConsole(int processId);
}
#endif
