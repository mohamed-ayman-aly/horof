using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace horof.Services.Network;

/// <summary>
/// Cross-platform LAN host: TcpListener + NDJSON lines routed to <see cref="RoomSessionServer"/>.
/// </summary>
public sealed class LanGameHost : IAsyncDisposable
{
    private readonly IQuestionBank _questionBank;
    private readonly ConcurrentDictionary<string, ClientSession> _clients = new();
    private readonly SemaphoreSlim _startLock = new(1, 1);
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _acceptTask;

    public LanGameHost(IQuestionBank questionBank)
    {
        _questionBank = questionBank;
    }

    public RoomSessionServer? Server { get; private set; }

    public async Task<RoomSessionServer> EnsureStartedAsync(CancellationToken cancellationToken = default)
    {
        await _startLock.WaitAsync(cancellationToken);
        try
        {
            if (Server is not null)
                return Server;

            var server = new RoomSessionServer(_questionBank);
            server.SetSessionPusher(PushSessionAsync);

            var listener = new TcpListener(IPAddress.Any, NetworkHelper.DefaultPort);
            listener.Start();

            var cts = new CancellationTokenSource();
            _listener = listener;
            _cts = cts;
            Server = server;

            var ip = NetworkHelper.GetLocalIPv4() ?? "127.0.0.1";
            server.SetHostAddress($"{ip}:{NetworkHelper.DefaultPort}");

            _acceptTask = AcceptLoopAsync(cts.Token);
            return server;
        }
        finally
        {
            _startLock.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _startLock.WaitAsync(cancellationToken);
        try
        {
            if (_cts is not null)
            {
                await _cts.CancelAsync();
                _cts.Dispose();
                _cts = null;
            }

            try
            {
                _listener?.Stop();
            }
            catch
            {
                // ignore
            }

            _listener = null;

            var clients = _clients.Values.ToArray();
            _clients.Clear();
            foreach (var client in clients)
                client.Close();

            if (_acceptTask is not null)
            {
                try
                {
                    await _acceptTask;
                }
                catch
                {
                    // ignore accept-loop cancellation
                }

                _acceptTask = null;
            }

            Server = null;
        }
        finally
        {
            _startLock.Release();
        }
    }

    public async ValueTask DisposeAsync() => await StopAsync();

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient? tcp = null;
            try
            {
                tcp = await _listener!.AcceptTcpClientAsync(cancellationToken);
                var connectionId = Guid.NewGuid().ToString("N");
                var session = new ClientSession(connectionId, tcp, HandleRequest, OnClientClosed);
                if (!_clients.TryAdd(connectionId, session))
                {
                    session.Close();
                    continue;
                }

                session.Start();
            }
            catch (OperationCanceledException)
            {
                tcp?.Dispose();
                break;
            }
            catch
            {
                tcp?.Dispose();
                if (cancellationToken.IsCancellationRequested)
                    break;
            }
        }
    }

    private void OnClientClosed(string connectionId)
    {
        _clients.TryRemove(connectionId, out _);
        Server?.RemoveConnection(connectionId);
    }

    private async Task PushSessionAsync(string connectionId, SessionSnapshot snapshot)
    {
        if (!_clients.TryGetValue(connectionId, out var client))
            return;

        await client.SendLineAsync(LanJson.SerializePush(LanMethods.SessionUpdated, snapshot));
    }

    private object? HandleRequest(string connectionId, LanRequest request)
    {
        var server = Server ?? throw new InvalidOperationException("Host is not running.");
        var args = request.Args ?? [];

        return request.Method switch
        {
            LanMethods.JoinRoom => server.TryJoin(
                connectionId,
                GetStringArg(args, 0) ?? "",
                GetStringArg(args, 1) ?? ""),
            LanMethods.SetReady => server.SetReady(
                GetStringArg(args, 0) ?? "",
                GetBoolArg(args, 1)),
            LanMethods.SelectHex => server.SelectHex(
                GetStringArg(args, 0) ?? "",
                GetIntArg(args, 1)),
            LanMethods.Buzz => server.Buzz(GetStringArg(args, 0) ?? ""),
            LanMethods.HostJudge => server.HostJudge(
                GetStringArg(args, 0) ?? "",
                GetBoolArg(args, 1)),
            LanMethods.GetSnapshot => server.GetSnapshotForConnection(connectionId),
            _ => throw new InvalidOperationException($"Unknown method: {request.Method}")
        };
    }

    private static string? GetStringArg(JsonElement[] args, int index)
    {
        if (index >= args.Length)
            return null;

        var el = args[index];
        return el.ValueKind == JsonValueKind.String ? el.GetString() : el.ToString();
    }

    private static bool GetBoolArg(JsonElement[] args, int index)
    {
        if (index >= args.Length)
            return false;

        var el = args[index];
        return el.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => bool.TryParse(el.GetString(), out var b) && b,
            _ => false
        };
    }

    private static int GetIntArg(JsonElement[] args, int index)
    {
        if (index >= args.Length)
            return 0;

        var el = args[index];
        return el.ValueKind switch
        {
            JsonValueKind.Number => el.GetInt32(),
            JsonValueKind.String => int.TryParse(el.GetString(), out var n) ? n : 0,
            _ => 0
        };
    }

    private sealed class ClientSession
    {
        private readonly string _connectionId;
        private readonly TcpClient _tcp;
        private readonly Func<string, LanRequest, object?> _handler;
        private readonly Action<string> _onClosed;
        private readonly SemaphoreSlim _writeLock = new(1, 1);
        private readonly CancellationTokenSource _cts = new();
        private StreamWriter? _writer;
        private int _closed;

        public ClientSession(
            string connectionId,
            TcpClient tcp,
            Func<string, LanRequest, object?> handler,
            Action<string> onClosed)
        {
            _connectionId = connectionId;
            _tcp = tcp;
            _handler = handler;
            _onClosed = onClosed;
        }

        public void Start()
        {
            var stream = _tcp.GetStream();
            _writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
            {
                AutoFlush = true,
                NewLine = "\n"
            };
            var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
            _ = ReadLoopAsync(reader, _cts.Token);
        }

        public async Task SendLineAsync(string line)
        {
            if (_writer is null || Volatile.Read(ref _closed) != 0)
                return;

            await _writeLock.WaitAsync();
            try
            {
                if (Volatile.Read(ref _closed) != 0 || _writer is null)
                    return;

                await _writer.WriteLineAsync(line);
            }
            catch
            {
                // connection may already be closing
            }
            finally
            {
                _writeLock.Release();
            }
        }

        public void Close()
        {
            if (Interlocked.Exchange(ref _closed, 1) != 0)
                return;

            try
            {
                _cts.Cancel();
            }
            catch
            {
                // ignore
            }

            try
            {
                _tcp.Close();
            }
            catch
            {
                // ignore
            }

            _tcp.Dispose();
            _cts.Dispose();
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

                    if (!LanJson.TryParseLine(line, out var request, out _, out _) || request is null)
                        continue;

                    try
                    {
                        var result = _handler(_connectionId, request);
                        await SendLineAsync(LanJson.SerializeResponse(request.Id, ok: true, result: result));
                    }
                    catch (Exception ex)
                    {
                        await SendLineAsync(LanJson.SerializeResponse(request.Id, ok: false, error: ex.Message));
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
                _onClosed(_connectionId);
                Close();
            }
        }
    }
}
