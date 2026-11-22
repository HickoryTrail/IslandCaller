# IslandCaller IpcLite 接入说明

IpcLite 为其他本机程序提供随机点名调用接口。它使用 Windows 命名管道传递 JSON，不需要 HTTP 服务或额外端口。

## URI 快速调用

插件启动 IPC 服务时，会在当前用户的注册表中注册 `islandcaller` URL 协议。注册命令指向与 `IslandCaller.Plugin2.dll` 同目录的 `IslandCaller.UrlHandler.exe`，并将完整 URI 作为命令行参数传给它。

```text
islandcaller://call?count=1&gender=all
islandcaller://call?count=3&gender=female
```

- `count`：正整数。
- `gender`：`all`、`male` 或 `female`。
- URL Handler 固定发送 `clientName = "IslandCaller.UrlHandler"`、`notify = true`。

如果处理程序 EXE 不在插件程序集目录中，插件会记录警告并跳过协议注册。发布或部署时需要将 AOT 发布生成的 EXE 放到该目录。

## 命名管道接口

| 项目 | 值 |
| --- | --- |
| 管道名 | `IslandCaller.Lite` |
| 方向 | 双向 |
| 请求编码 | UTF-8 JSON，单条管道消息 |
| 单次读取缓冲区 | 1 MiB |
| 并发连接 | 一次一个客户端 |

服务端监听 IPC 管道期间接受请求。客户端连接后应在同一连接中发送一条完整 JSON 请求，然后读取响应直到服务端关闭连接。每个请求完成后服务端都会断开当前管道连接；下一次调用需重新连接。服务端每次读取最多缓冲 1 MiB，但当前实现没有限制整个请求的累计大小；请求应保持较小。

## 请求

请求结构由 [RequestSchema.json](RequestSchema.json) 定义。完整示例：

```json
{
  "jsonrpc": "2.0",
  "version": "1.0",
  "method": "getRandomStudent",
  "params": {
    "clientName": "ExampleClient",
    "count": 2,
    "gender": "all",
    "notify": true
  },
  "id": 1
}
```

| 参数 | 类型 | 必填 | 作用 |
| --- | --- | --- | --- |
| `jsonrpc` | string | 是 | JSON-RPC 版本，固定为 `2.0`。 |
| `version` | string | 是 | IpcLite 接口版本，当前固定为 `1.0`。 |
| `method` | string | 是 | 调用方法，当前固定为 `getRandomStudent`。 |
| `params.clientName` | string | 是 | 调用方名称，用于识别请求来源，不能为空。 |
| `params.count` | integer | 是 | 本次点名人数，必须为正整数。 |
| `params.gender` | string | 是 | 性别参数，可选 `all`、`male` 或 `female`；当前服务端只校验该值，尚未按性别筛选。 |
| `params.notify` | boolean | 是 | 是否启用应用内通知。`true` 时在 IslandCaller 中展示点名结果；`false` 时不启用应用内通知，并通过响应 `result` 返回学生姓名数组。 |
| `id` | integer | 是 | 请求编号，当前服务端要求固定为 `1`。 |

目前服务端会校验以上字段及其取值。`gender` 当前只做校验和日志记录，不参与抽取筛选。

## 响应

响应结构见 [ResponseSchema.json](ResponseSchema.json)。成功响应示例：

```json
{
  "jsonrpc": "2.0",
  "version": "1.0",
  "id": 2,
  "result": "点名成功。"
}
```

错误响应使用 `error` 对象，例如：

```json
{
  "jsonrpc": "2.0",
  "version": "1.0",
  "id": 2,
  "error": {
    "code": -32600,
    "message": "params.gender 必须为字符串 \"all\"、\"male\" 或 \"female\"。"
  }
}
```

注意：当前服务端将响应 `id` 固定为 `2`，与请求 `id` 不同。`notify = true` 时 `result` 为点名已开始的确认信息；响应不是抽取名单。`notify = false` 时 `result` 为包含点名结果的学生姓名数组，例如 `"result": ["张三", "李四"]`。客户端应以管道 EOF 作为响应结束标记，并按 UTF-8 字节解码。

## .NET 客户端示例

```csharp
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

await using var pipe = new NamedPipeClientStream(
    ".", "IslandCaller.Lite", PipeDirection.InOut, PipeOptions.Asynchronous);
await pipe.ConnectAsync(5000);

var request = new
{
    jsonrpc = "2.0",
    version = "1.0",
    method = "getRandomStudent",
    @params = new
    {
        clientName = "ExampleClient",
        count = 2,
        gender = "all",
        notify = true
    },
    id = 1
};

byte[] requestBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request));
await pipe.WriteAsync(requestBytes);
await pipe.FlushAsync();

using var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);
string responseJson = await reader.ReadToEndAsync(); // 服务端断开连接时结束
Console.WriteLine(responseJson);
```

## 失败与重试

- 主程序未启动或 IPC 服务尚未就绪时，管道连接会超时或抛出 `IOException`；确认 ClassIsland 与 IslandCaller 已启动后再重试。
- JSON 无法解析时返回错误码 `-32700`；请求字段或取值不合法时返回 `-32600`。
- 服务端单实例串行处理连接。收到响应并观察到 EOF 后，客户端应为下一次请求新建管道连接。
