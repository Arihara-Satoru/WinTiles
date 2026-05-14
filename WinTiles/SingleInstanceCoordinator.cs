using System.IO;
using System.IO.Pipes;
using System.Text;

namespace WinTiles;

public sealed class SingleInstanceCoordinator : IDisposable
{
    private readonly string _pipeName;
    private readonly Func<string?, Task> _activationHandler;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private Task? _listeningTask;

    public SingleInstanceCoordinator(string pipeName, Func<string?, Task> activationHandler)
    {
        _pipeName = pipeName;
        _activationHandler = activationHandler;
    }

    public void Start()
    {
        _listeningTask = Task.Run(ListenAsync);
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        try
        {
            _listeningTask?.Wait(TimeSpan.FromSeconds(1));
        }
        catch
        {
            // 应用退出时不再继续把后台监听异常抛到前台。
        }
        _cancellationTokenSource.Dispose();
    }

    public static async Task SendActivationAsync(string? tileId)
    {
        await using var client = new NamedPipeClientStream(
            ".",
            "WinTiles.ActivationPipe",
            PipeDirection.Out,
            PipeOptions.Asynchronous);

        try
        {
            await client.ConnectAsync(1000).ConfigureAwait(false);
            await using var writer = new StreamWriter(client, Encoding.UTF8, 1024, leaveOpen: true);
            await writer.WriteLineAsync(tileId ?? string.Empty).ConfigureAwait(false);
            await writer.FlushAsync().ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // 主实例如果正好退出，这里静默结束即可。
        }
    }

    private async Task ListenAsync()
    {
        while (!_cancellationTokenSource.IsCancellationRequested)
        {
            await using var server = new NamedPipeServerStream(
                _pipeName,
                PipeDirection.In,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

            try
            {
                await server.WaitForConnectionAsync(_cancellationTokenSource.Token).ConfigureAwait(false);
                using var reader = new StreamReader(server, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
                var payload = await reader.ReadLineAsync(_cancellationTokenSource.Token).ConfigureAwait(false);
                await _activationHandler(string.IsNullOrWhiteSpace(payload) ? null : payload).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
