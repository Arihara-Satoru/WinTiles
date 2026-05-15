using System.Collections;
using System.Runtime.InteropServices;

namespace WinTiles.Core.Services;

public sealed class StartMenuPinVerbInvoker
{
    private static readonly string[] PinVerbCandidates = ["startpin", "pintostartscreen"];
    private const int AppsFolderLookupRetryCount = 30;
    private const int RetryDelayMilliseconds = 100;

    public async Task<StartMenuPinVerbResult> TryPinAsync(
        string appUserModelId,
        string shortcutPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appUserModelId);
        ArgumentException.ThrowIfNullOrWhiteSpace(shortcutPath);

        return await RunOnStaThreadAsync(
            () => TryPinCore(appUserModelId, shortcutPath, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    private static StartMenuPinVerbResult TryPinCore(
        string appUserModelId,
        string shortcutPath,
        CancellationToken cancellationToken)
    {
        object? shellApplication = null;
        object? appsFolder = null;
        object? appsFolderItem = null;
        object? shortcutFolder = null;
        object? shortcutItem = null;

        try
        {
            var shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType is null)
            {
                return StartMenuPinVerbResult.CreateFailure("系统未提供 Shell.Application，无法自动触发固定命令。");
            }

            shellApplication = Activator.CreateInstance(shellType);
            if (shellApplication is null)
            {
                return StartMenuPinVerbResult.CreateFailure("创建 Shell.Application 失败，无法自动触发固定命令。");
            }

            dynamic shell = shellApplication;
            appsFolder = shell.Namespace("shell:AppsFolder");
            if (appsFolder is not null)
            {
                appsFolderItem = WaitForAppsFolderItem(appsFolder, appUserModelId, cancellationToken);
                if (TryInvokePinVerb(appsFolderItem, "AppsFolder", out var appsFolderResult))
                {
                    return appsFolderResult;
                }
            }

            string shortcutDirectory = Path.GetDirectoryName(shortcutPath) ?? string.Empty;
            string shortcutFileName = Path.GetFileName(shortcutPath);
            shortcutFolder = shell.Namespace(shortcutDirectory);
            if (shortcutFolder is not null)
            {
                dynamic folder = shortcutFolder;
                shortcutItem = folder.ParseName(shortcutFileName);
                if (TryInvokePinVerb(shortcutItem, "StartMenuShortcut", out var shortcutResult))
                {
                    return shortcutResult;
                }
            }

            return StartMenuPinVerbResult.CreateFailure("未能找到可自动触发“固定到开始屏幕”的系统入口。");
        }
        catch (Exception exception)
        {
            return StartMenuPinVerbResult.CreateFailure($"自动触发系统固定命令失败：{exception.Message}");
        }
        finally
        {
            ReleaseComObject(shortcutItem);
            ReleaseComObject(shortcutFolder);
            ReleaseComObject(appsFolderItem);
            ReleaseComObject(appsFolder);
            ReleaseComObject(shellApplication);
        }
    }

    private static object? WaitForAppsFolderItem(object appsFolder, string appUserModelId, CancellationToken cancellationToken)
    {
        dynamic folder = appsFolder;
        for (int attempt = 0; attempt < AppsFolderLookupRetryCount; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            object? parsedItem = folder.ParseName(appUserModelId);
            if (parsedItem is not null)
            {
                return parsedItem;
            }

            if (folder.Items() is IEnumerable items)
            {
                foreach (object item in items)
                {
                    try
                    {
                        dynamic currentItem = item;
                        string? itemPath = Convert.ToString(currentItem.Path);
                        if (string.Equals(itemPath, appUserModelId, StringComparison.OrdinalIgnoreCase))
                        {
                            return item;
                        }
                    }
                    catch (COMException)
                    {
                        // AppsFolder 里有些虚拟项读取 Path 会抛 COM 异常，这里直接跳过即可。
                    }
                }
            }

            Thread.Sleep(RetryDelayMilliseconds);
        }

        return null;
    }

    private static bool TryInvokePinVerb(object? shellItem, string targetName, out StartMenuPinVerbResult result)
    {
        if (shellItem is null)
        {
            result = StartMenuPinVerbResult.CreateFailure($"{targetName} 项不存在。");
            return false;
        }

        foreach (string verb in PinVerbCandidates)
        {
            try
            {
                dynamic item = shellItem;
                item.InvokeVerb(verb);

                // verb 调用是异步交给 Explorer 处理的，这里稍等一下，避免后续立刻读状态时还没落地。
                Thread.Sleep(250);

                result = StartMenuPinVerbResult.CreateSuccess(targetName, verb);
                return true;
            }
            catch (COMException)
            {
                // 某些环境下 verb 不可用时会直接抛 COM 异常，继续尝试下一个候选 verb。
            }
            catch (Exception exception)
            {
                result = StartMenuPinVerbResult.CreateFailure(
                    $"{targetName} 调用固定命令失败：{exception.Message}");
                return false;
            }
        }

        result = StartMenuPinVerbResult.CreateFailure($"{targetName} 没有可用的自动固定命令。");
        return false;
    }

    private static Task<StartMenuPinVerbResult> RunOnStaThreadAsync(
        Func<StartMenuPinVerbResult> action,
        CancellationToken cancellationToken)
    {
        var taskCompletionSource = new TaskCompletionSource<StartMenuPinVerbResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var thread = new Thread(() =>
        {
            try
            {
                taskCompletionSource.TrySetResult(action());
            }
            catch (OperationCanceledException)
            {
                taskCompletionSource.TrySetCanceled(cancellationToken);
            }
            catch (Exception exception)
            {
                taskCompletionSource.TrySetResult(
                    StartMenuPinVerbResult.CreateFailure($"自动触发系统固定命令失败：{exception.Message}"));
            }
        });

        thread.IsBackground = true;
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        if (!cancellationToken.CanBeCanceled)
        {
            return taskCompletionSource.Task;
        }

        cancellationToken.Register(() => taskCompletionSource.TrySetCanceled(cancellationToken));
        return taskCompletionSource.Task;
    }

    private static void ReleaseComObject(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            Marshal.FinalReleaseComObject(value);
        }
    }
}

public sealed class StartMenuPinVerbResult
{
    public required bool Invoked { get; init; }

    public string? TargetName { get; init; }

    public string? VerbName { get; init; }

    public string? ErrorMessage { get; init; }

    public static StartMenuPinVerbResult CreateSuccess(string targetName, string verbName) => new()
    {
        Invoked = true,
        TargetName = targetName,
        VerbName = verbName
    };

    public static StartMenuPinVerbResult CreateFailure(string errorMessage) => new()
    {
        Invoked = false,
        ErrorMessage = errorMessage
    };
}
