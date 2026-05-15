using System.Diagnostics;
using System.Text.Json;
using WinTiles.Core.Models;

namespace WinTiles.Core.Services;

public sealed class PinHelperInvoker
{
    public async Task<PinHelperResult> PinImageAsync(
        string pinHelperPath,
        string tileId,
        TileRequestSize requestedSize,
        string hostExePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(pinHelperPath))
        {
            return new PinHelperResult
            {
                Status = PinHelperResultStatus.Failure,
                Message = $"未找到 PinHelper：{pinHelperPath}",
                PinMethod = "Unavailable"
            };
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = pinHelperPath,
            Arguments = PinHelperCommandLine.BuildPinArguments(tileId, requestedSize, hostExePath),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);
        return ParseResult(process.ExitCode, stdout, stderr, requestedSize, isClearOperation: false);
    }

    public async Task<PinHelperResult> UnpinImageAsync(
        string pinHelperPath,
        string tileId,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(pinHelperPath))
        {
            return new PinHelperResult
            {
                Status = PinHelperResultStatus.Failure,
                Message = $"未找到 PinHelper：{pinHelperPath}",
                PinMethod = "Unavailable"
            };
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = pinHelperPath,
            Arguments = PinHelperCommandLine.BuildUnpinArguments(tileId),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);
        return ParseResult(process.ExitCode, stdout, stderr, TileRequestSize.Medium2x2, isClearOperation: true);
    }

    private static PinHelperResult ParseResult(
        int exitCode,
        string stdout,
        string stderr,
        TileRequestSize requestedSize,
        bool isClearOperation)
    {
        if (!string.IsNullOrWhiteSpace(stdout))
        {
            try
            {
                using var document = JsonDocument.Parse(stdout);
                var root = document.RootElement;

                var statusText = root.TryGetProperty("status", out var statusElement)
                    ? statusElement.GetString()
                    : null;

                var status = statusText?.ToLowerInvariant() switch
                {
                    "success" => PinHelperResultStatus.Success,
                    "warning" => PinHelperResultStatus.Warning,
                    _ => exitCode == 0 ? PinHelperResultStatus.Success : PinHelperResultStatus.Failure
                };

                return new PinHelperResult
                {
                    Status = status,
                    Message = root.TryGetProperty("message", out var messageElement)
                        ? messageElement.GetString() ?? BuildFallbackMessage(status, requestedSize, isClearOperation)
                        : BuildFallbackMessage(status, requestedSize, isClearOperation),
                    PinMethod = root.TryGetProperty("pinMethod", out var pinMethodElement)
                        ? pinMethodElement.GetString() ?? "Unknown"
                        : "Unknown",
                    Warning = root.TryGetProperty("warning", out var warningElement)
                        ? warningElement.GetString()
                        : null,
                    IdentityKind = root.TryGetProperty("identityKind", out var identityKindElement)
                        ? identityKindElement.GetString()
                        : null,
                    IdentityValue = root.TryGetProperty("identityValue", out var identityValueElement)
                        ? identityValueElement.GetString()
                        : null,
                    ContainsBefore = root.TryGetProperty("containsBefore", out var containsBeforeElement) && containsBeforeElement.ValueKind is JsonValueKind.True or JsonValueKind.False
                        ? containsBeforeElement.GetBoolean()
                        : null,
                    ContainsAfterCommit = root.TryGetProperty("containsAfterCommit", out var containsAfterCommitElement) && containsAfterCommitElement.ValueKind is JsonValueKind.True or JsonValueKind.False
                        ? containsAfterCommitElement.GetBoolean()
                        : null,
                    ContainsAfterReopen = root.TryGetProperty("containsAfterReopen", out var containsAfterReopenElement) && containsAfterReopenElement.ValueKind is JsonValueKind.True or JsonValueKind.False
                        ? containsAfterReopenElement.GetBoolean()
                        : null,
                    RawOutput = stdout,
                    RawError = stderr
                };
            }
            catch (JsonException)
            {
                // PinHelper 输出的内容如果不是 JSON，就直接回退为文本错误。
            }
        }

        var fallbackStatus = exitCode == 0 ? PinHelperResultStatus.Success : PinHelperResultStatus.Failure;
        return new PinHelperResult
        {
            Status = fallbackStatus,
            Message = !string.IsNullOrWhiteSpace(stderr)
                ? stderr.Trim()
                : BuildFallbackMessage(fallbackStatus, requestedSize, isClearOperation),
            PinMethod = "Unknown",
            RawOutput = stdout,
            RawError = stderr
        };
    }

    private static string BuildFallbackMessage(
        PinHelperResultStatus status,
        TileRequestSize requestedSize,
        bool isClearOperation)
    {
        if (isClearOperation)
        {
            return status switch
            {
                PinHelperResultStatus.Success => "已清除固定",
                PinHelperResultStatus.Warning => "已尝试清除固定，但系统可能未完全刷新",
                _ => "清除固定失败，请查看详细提示。"
            };
        }

        return status switch
        {
            PinHelperResultStatus.Success => $"已请求固定为 {requestedSize.ToDisplayText()}",
            PinHelperResultStatus.Warning => "已固定，但系统可能未按请求尺寸显示",
            _ => "固定图片失败，请查看详细提示。"
        };
    }
}
