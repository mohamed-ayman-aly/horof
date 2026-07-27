using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace horof.Services.Network;

/// <summary>
/// Cross-platform LAN client: TcpClient + NDJSON request/response with push notifications.
/// </summary>
public sealed class LanGameClient : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<LanResponse>> _pending = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private TcpClient? _tcp;
    private StreamWriter? _writer;
    private CancellationTokenSource? _cts;
    private Task? _readTask;
    private bool _disposed;

    public bool IsConnected => _tcp?.Connected == true;

    public event Action<SessionSnapshot>? SessionUpdated;
    public event Action<string>? Disconnected;

    public async Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        await DisconnectAsync();

        var tcp = new TcpClient();
        await tcp.ConnectAsync(host, port, cancellationToken);

        var stream = tcp.GetStream();
        var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
        {
            AutoFlush = true,
            NewLine = "\n"
        };
        var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var cts = new CancellationTokenSource();

        _tcp = tcp;
        _writer = writer;
        _cts = cts;
        _readTask = ReadLoopAsync(reader, cts.Token);
    }

    public async Task<T> InvokeAsync<T>(string method, params object?[] args)
    {
        if (_writer is null || !IsConnected)
            throw new InvalidOperationException("Not connected to host.");

        var id = Guid.NewGuid().ToString("N");
        var tcs = new TaskCompletionSource<LanResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = tcs;

        try
        {
            var line = LanJson.SerializeRequest(id, method, args);
            await _writeLock.WaitAsync();
            try
            {
                await _writer.WriteLineAsync(line);
            }
            finally
            {
                _writeLock.Release();
            }

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await using var reg = timeout.Token.Register(() =>
                tcs.TrySetException(new TimeoutException($"Timed out calling {method}.")));

            var response = await tcs.Task;
            if (!response.Ok)
                throw new InvalidOperationException(response.Error ?? "Request failed.");

            if (typeof(T) == typeof(object) || typeof(T) == typeof(JsonElement))
                return (T)(object)(response.Result ?? default(JsonElement));

            var value = LanJson.DeserializeResult<T>(response.Result);
            return value is null && !typeof(T).IsValueType
                ? throw new InvalidOperationException($"Empty result for {method}.")
                : value!;
        }
        finally
        {
            _pending.TryRemove(id, out _);
        }
    }

    public async Task DisconnectAsync()
    {
        foreach (var pending in _pending.Values)
            pending.TrySetCanceled();

        _pending.Clear();

        if (_cts is not null)
        {
            await _cts.CancelAsync();
            _cts.Dispose();
            _cts = null;
        }

        try
        {
            _tcp?.Close();
        }
        catch
        {
            // ignore
        }

        _tcp?.Dispose();
        _tcp = null;
        _writer = null;

        if (_readTask is not null)
        {
            try
            {
                await _readTask;
            }
            catch
            {
                // ignore
            }

            _readTask = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        await DisconnectAsync();
        _writeLock.Dispose();
    }

    private async Task ReadLoopAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (line is null)
                    break;

                if (!LanJson.TryParseLine(line, out _, out var response, out var push))
                    continue;

                if (response is not null)
                {
                    if (_pending.TryGetValue(response.Id, out var tcs))
                        tcs.TrySetResult(response);
                    continue;
                }

                if (push is not null &&
                    push.Method == LanMethods.SessionUpdated &&
                    push.Args.Length > 0)
                {
                    var snapshot = push.Args[0].Deserialize<SessionSnapshot>(LanJson.Options);
                    if (snapshot is not null)
                        SessionUpdated?.Invoke(snapshot);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
        catch
        {
            // connection dropped
        }
        finally
        {
            Disconnected?.Invoke("انقطع الاتصال بالمضيف");
        }
    }
}
