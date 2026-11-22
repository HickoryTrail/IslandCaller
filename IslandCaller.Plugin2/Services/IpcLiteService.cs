using ClassIsland.Shared;
using IslandCaller.Plugin2.Helpers;
using IslandCaller.Services.IslandCallerService;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System.IO.Pipes;
using System.Text;

namespace IslandCaller.Plugin2.Services
{
    internal class IpcLiteService
    {
        private const string PipeName = "IslandCaller.Lite";
        private const int RequestTimeout = 200;
        private const int MaxMessageSize = 1024 * 1024; // 1MB
        private CancellationTokenSource Cts = new();
        private ILogger<IpcLiteService> Logger { get; }
        private IslandCallerService IslandCallerService { get; }
        public IpcLiteService()
        {
            Logger = IAppHost.GetService<ILogger<IpcLiteService>>();
            IslandCallerService = IAppHost.GetService<IslandCallerService>();
        }

        internal async Task RunAsync()
        {
            EnsureUrlProtocolRegistered();
            Logger.LogInformation("IPC Lite service started.");
            while (!Cts.Token.IsCancellationRequested)
            {
                using var server = new NamedPipeServerStream(PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Message, PipeOptions.Asynchronous);
                try
                {
                    {
                        // 连接客户端
                        using var message = new MemoryStream();
                        byte[] buffer = new byte[MaxMessageSize];
                        await server.WaitForConnectionAsync(Cts.Token);
                        Logger.LogDebug("IPC connection established.");
                        using var connectTokenSource = new CancellationTokenSource(RequestTimeout);
                        using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(Cts.Token, connectTokenSource.Token);
                        // 读取客户端请求
                        do
                        {
                            int count = await server.ReadAsync(buffer, 0, buffer.Length, linkedTokenSource.Token);
                            if (count == 0) return;
                            await message.WriteAsync(buffer, 0, count, linkedTokenSource.Token);
                        } while (!server.IsMessageComplete);
                        string request = Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length);
                        Logger.LogDebug("Received IPC message: {Message}", request);
                        object messageObj;

                        // 解析 JSON 请求
                        try
                        {
                            messageObj = JsonHelper.Parse(request) ?? throw new Exception("Parsed JSON is null.");
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError("Failed to parse IPC message: {Message}", request);
                            var errorResponse = new
                            {
                                jsonrpc = "2.0",
                                version = "1.0",
                                id = 2,
                                error = new
                                {
                                    code = -32700,
                                    message = "Invalid JSON",
                                    data = ex.Message
                                }
                            };
                            string errorResponseJson = JsonHelper.Encode(errorResponse, false);
                            byte[] errorResponseBytes = Encoding.UTF8.GetBytes(errorResponseJson);
                            await server.WriteAsync(errorResponseBytes, 0, errorResponseBytes.Length);
                            await server.FlushAsync();
                            continue;
                        }
                        Logger.LogDebug("Received IPC message: {Message}", messageObj);

                        // 验证请求体
                        if (!TryGetRandomStudentRequest(messageObj, out var parsedRequest, out var validationError))
                        {
                            Logger.LogWarning("Rejected IPC Lite request: {ValidationError}", validationError);
                            var errorResponse = new
                            {
                                jsonrpc = "2.0",
                                version = "1.0",
                                id = 2,
                                error = new
                                {
                                    code = -32600,
                                    message = validationError
                                }
                            };
                            string errorResponseJson = JsonHelper.Encode(errorResponse, false);
                            byte[] errorResponseBytes = Encoding.UTF8.GetBytes(errorResponseJson);
                            await server.WriteAsync(errorResponseBytes, 0, errorResponseBytes.Length);
                            await server.FlushAsync();
                            continue;
                        }

                        string clientName = parsedRequest.ClientName;
                        int requestedCount = parsedRequest.Count;
                        string gender = parsedRequest.Gender;
                        int id = parsedRequest.Id;
                        bool notify = parsedRequest.Notify;
                        Logger.LogDebug(
                            "Validated IPC Lite request {Id} from {ClientName}: getRandomStudent, count {Count}, gender {Gender}, notify {Notify}",
                            id,
                            clientName,
                            requestedCount,
                            gender,
                            notify);

                        // 获取点名结果
                        if (notify == true)
                        {
                            IslandCallerService.ShowRandomStudent(requestedCount);
                            var response = new
                            {
                                jsonrpc = "2.0",
                                version = "1.0",
                                id = 2,
                                result = "点名成功。"
                            };
                            string responseJson = JsonHelper.Encode(response, false);
                            byte[] responseBytes = Encoding.UTF8.GetBytes(responseJson);
                            await server.WriteAsync(responseBytes, 0, responseBytes.Length);
                            await server.FlushAsync();
                            Logger.LogInformation("IPC Lite request from {ClientName}: getRandomStudent (no notify)", clientName);
                        }
                        else
                        {
                            List<string> students = IslandCallerService.GetRandomStudents(requestedCount);
                            var response = new
                            {
                                jsonrpc = "2.0",
                                version = "1.0",
                                id = 2,
                                result = students.ToArray()
                            };
                            string responseJson = JsonHelper.Encode(response, false);
                            Logger.LogInformation("IPC Lite request from {ClientName}: getRandomStudent, result: {Result}", clientName, students);
                            byte[] responseBytes = Encoding.UTF8.GetBytes(responseJson);
                            await server.WriteAsync(responseBytes, 0, responseBytes.Length);
                            await server.FlushAsync();
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    continue;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error in IpcLiteService");
                }
                finally
                {
                    server.Disconnect();
                    Logger.LogDebug("IPC connection closed.");
                }
            }
        }

        private void EnsureUrlProtocolRegistered()
        {
            const string protocolName = "islandcaller";
            const string protocolRegistryPath = @"Software\Classes\islandcaller";

            try
            {
                string? assemblyDirectory = Path.GetDirectoryName(typeof(IpcLiteService).Assembly.Location);
                if (string.IsNullOrWhiteSpace(assemblyDirectory))
                {
                    Logger.LogWarning("Cannot register {Protocol} URI: plugin assembly directory is unavailable.", protocolName);
                    return;
                }

                string handlerPath = Path.GetFullPath(Path.Combine(assemblyDirectory, "IslandCaller.UrlHandler.exe"));
                if (!File.Exists(handlerPath))
                {
                    Logger.LogWarning("Cannot register {Protocol} URI: handler executable not found at {HandlerPath}.", protocolName, handlerPath);
                    return;
                }

                string command = $"\"{handlerPath}\" \"%1\"";
                using (var existingProtocol = Registry.CurrentUser.OpenSubKey(protocolRegistryPath))
                using (var existingCommand = existingProtocol?.OpenSubKey(@"shell\open\command"))
                {
                    if (existingProtocol?.GetValue("URL Protocol") is string &&
                        string.Equals(existingCommand?.GetValue(null) as string, command, StringComparison.OrdinalIgnoreCase))
                    {
                        Logger.LogDebug("{Protocol} URI is already registered to {HandlerPath}.", protocolName, handlerPath);
                        return;
                    }
                }

                using var protocolKey = Registry.CurrentUser.CreateSubKey(protocolRegistryPath, writable: true);
                if (protocolKey is null)
                {
                    Logger.LogWarning("Cannot create registry key for {Protocol} URI.", protocolName);
                    return;
                }

                protocolKey.SetValue(null, $"URL:{protocolName} Protocol", RegistryValueKind.String);
                protocolKey.SetValue("URL Protocol", string.Empty, RegistryValueKind.String);
                using var commandKey = protocolKey.CreateSubKey(@"shell\open\command", writable: true);
                commandKey?.SetValue(null, command, RegistryValueKind.String);
                Logger.LogInformation("Registered {Protocol} URI to {HandlerPath}.", protocolName, handlerPath);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to register {Protocol} URI.", protocolName);
            }
        }

        internal void Stop()
        {
            Cts.Cancel();
        }

        private static bool TryGetRandomStudentRequest(
            object? messageObj,
            out RandomStudentRequest parsedRequest,
            out string validationError)
        {
            parsedRequest = default!;
            validationError = string.Empty;

            if (messageObj is not Dictionary<string, object?> message)
            {
                validationError = "顶层 JSON 必须是对象。";
                return false;
            }

            if (!HasStringValue(message, "jsonrpc", "2.0"))
            {
                validationError = "jsonrpc 必须为字符串 \"2.0\"。";
                return false;
            }

            if (!HasStringValue(message, "version", "1.0"))
            {
                validationError = "version 必须为字符串 \"1.0\"。";
                return false;
            }

            if (!HasStringValue(message, "method", "getRandomStudent"))
            {
                validationError = "method 必须为 \"getRandomStudent\"。";
                return false;
            }

            if (!message.TryGetValue("id", out var idValue) || idValue is not int id || id != 1)
            {
                validationError = "id 必须为 1。";
                return false;
            }

            if (!message.TryGetValue("params", out var paramsValue) ||
                paramsValue is not Dictionary<string, object?> parameters)
            {
                validationError = "params 必须为对象。";
                return false;
            }

            if (!TryGetString(parameters, "clientName", out var clientName) || string.IsNullOrEmpty(clientName))
            {
                validationError = "params.clientName 必须为非空字符串。";
                return false;
            }

            if (!parameters.TryGetValue("count", out var countValue) || countValue is not int count || count <= 0)
            {
                validationError = "params.count 必须为正整数。";
                return false;
            }

            if (!TryGetString(parameters, "gender", out var gender) || (gender != "all" && gender != "male" && gender != "female"))
            {
                validationError = "params.gender 必须为字符串 \"all\"、\"male\" 或 \"female\"。";
                return false;
            }

            if (!parameters.TryGetValue("notify", out var notifyValue) || notifyValue is not bool notify)
            {
                validationError = "params.notify 必须为布尔值。";
                return false;
            }

            parsedRequest = new RandomStudentRequest(clientName, count, gender, id, notify);
            return true;
        }

        private static bool HasStringValue(Dictionary<string, object?> values, string key, string expected) =>
            values.TryGetValue(key, out var value) && value is string actual && actual == expected;

        private static bool TryGetString(Dictionary<string, object?> values, string key, out string value)
        {
            if (values.TryGetValue(key, out var rawValue) && rawValue is string stringValue)
            {
                value = stringValue;
                return true;
            }

            value = string.Empty;
            return false;
        }

        private sealed record RandomStudentRequest(string ClientName, int Count, string Gender, int Id, bool Notify);
    }
}
