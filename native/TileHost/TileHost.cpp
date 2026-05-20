#include <windows.h>
#include <shellapi.h>
#include <string>

/// <summary>
/// 从宿主目录下的 ini 文件读取指定键值。
/// </summary>
/// <param name="section">ini 节名。</param>
/// <param name="key">ini 键名。</param>
/// <param name="configPath">ini 完整路径。</param>
/// <returns>读取到的字符串；若不存在则返回空字符串。</returns>
static std::wstring ReadConfigValue(const std::wstring& section, const std::wstring& key, const std::wstring& configPath)
{
    wchar_t buffer[2048] = {};
    GetPrivateProfileStringW(section.c_str(), key.c_str(), L"", buffer, ARRAYSIZE(buffer), configPath.c_str());
    return buffer;
}

/// <summary>
/// 为命令行参数补上双引号，避免磁贴标识中包含空格时被拆断。
/// </summary>
/// <param name="value">原始参数值。</param>
/// <returns>加引号后的参数文本。</returns>
static std::wstring Quote(const std::wstring& value)
{
    return L"\"" + value + L"\"";
}

/// <summary>
/// 判断当前配置是否为支持的网页点击动作。
/// </summary>
/// <param name="actionType">配置里的动作类型编号。</param>
/// <param name="url">网页地址。</param>
/// <returns>若可以直接按网页动作执行则返回 true。</returns>
static bool IsOpenUrlAction(const std::wstring& actionType, const std::wstring& url)
{
    return actionType == L"1" && !url.empty();
}

/// <summary>
/// 判断当前配置是否为支持的应用点击动作。
/// </summary>
/// <param name="actionType">配置里的动作类型编号。</param>
/// <param name="applicationPath">应用路径。</param>
/// <returns>若可以直接按应用动作执行则返回 true。</returns>
static bool IsOpenApplicationAction(const std::wstring& actionType, const std::wstring& applicationPath)
{
    return actionType == L"2" && !applicationPath.empty();
}

/// <summary>
/// 使用 ShellExecuteExW 执行外部目标，供网页和应用动作复用。
/// </summary>
/// <param name="fileName">要启动的文件、快捷方式或 URL。</param>
/// <param name="parameters">可选启动参数。</param>
/// <param name="workingDirectory">可选工作目录。</param>
/// <returns>成功返回 true，失败返回 false。</returns>
static bool ExecuteShellTarget(
    const std::wstring& fileName,
    const std::wstring& parameters,
    const std::wstring& workingDirectory)
{
    SHELLEXECUTEINFOW executeInfo = {};
    executeInfo.cbSize = sizeof(executeInfo);
    executeInfo.fMask = SEE_MASK_NOCLOSEPROCESS;
    executeInfo.lpFile = fileName.c_str();
    executeInfo.lpParameters = parameters.empty() ? nullptr : parameters.c_str();
    executeInfo.lpDirectory = workingDirectory.empty() ? nullptr : workingDirectory.c_str();
    executeInfo.nShow = SW_SHOWNORMAL;

    if (!ShellExecuteExW(&executeInfo))
    {
        return false;
    }

    if (executeInfo.hProcess)
    {
        CloseHandle(executeInfo.hProcess);
    }

    return true;
}

/// <summary>
/// 按照 ini 中的点击动作直接执行，避免总是先拉起 WinTiles 主程序。
/// </summary>
/// <param name="configPath">当前磁贴宿主对应的 ini 路径。</param>
/// <returns>若已成功执行点击动作则返回 true，否则返回 false 以便回退到主程序。</returns>
static bool TryExecuteConfiguredClickAction(const std::wstring& configPath)
{
    auto actionType = ReadConfigValue(L"TileHost", L"ClickActionType", configPath);
    auto url = ReadConfigValue(L"TileHost", L"ClickActionUrl", configPath);
    if (IsOpenUrlAction(actionType, url))
    {
        return ExecuteShellTarget(url, L"", L"");
    }

    auto applicationPath = ReadConfigValue(L"TileHost", L"ClickActionApplicationPath", configPath);
    if (IsOpenApplicationAction(actionType, applicationPath))
    {
        auto arguments = ReadConfigValue(L"TileHost", L"ClickActionArguments", configPath);
        auto workingDirectory = ReadConfigValue(L"TileHost", L"ClickActionWorkingDirectory", configPath);
        return ExecuteShellTarget(applicationPath, arguments, workingDirectory);
    }

    return false;
}

/// <summary>
/// 当没有可直接执行的点击动作时，回退为激活 WinTiles 主程序并传递 tileId。
/// </summary>
/// <param name="mainExecutable">主程序可执行文件路径。</param>
/// <param name="tileId">当前磁贴唯一标识。</param>
/// <returns>成功返回 true，失败返回 false。</returns>
static bool LaunchMainApplication(const std::wstring& mainExecutable, const std::wstring& tileId)
{
    std::wstring parameters = std::wstring(L"--tile-id ") + Quote(tileId);
    auto separator = mainExecutable.find_last_of(L"\\/");
    auto mainExecutableDirectory = separator == std::wstring::npos
        ? std::wstring()
        : mainExecutable.substr(0, separator);

    return ExecuteShellTarget(mainExecutable, parameters, mainExecutableDirectory);
}

int WINAPI wWinMain(HINSTANCE, HINSTANCE, PWSTR, int)
{
    wchar_t modulePath[MAX_PATH] = {};
    if (!GetModuleFileNameW(nullptr, modulePath, ARRAYSIZE(modulePath)))
    {
        return 1;
    }

    std::wstring hostPath = modulePath;
    auto separator = hostPath.find_last_of(L"\\/");
    auto tileDirectory = separator == std::wstring::npos ? L"." : hostPath.substr(0, separator);
    auto configPath = tileDirectory + L"\\TileHost.ini";

    auto mainExecutable = ReadConfigValue(L"TileHost", L"MainExecutable", configPath);
    auto tileId = ReadConfigValue(L"TileHost", L"TileId", configPath);
    if (mainExecutable.empty() || tileId.empty())
    {
        return 2;
    }

    // 优先执行磁贴自身绑定的点击动作，仅在未配置或执行失败时才回退到 WinTiles。
    if (TryExecuteConfiguredClickAction(configPath))
    {
        return 0;
    }

    if (!LaunchMainApplication(mainExecutable, tileId))
    {
        return 3;
    }

    return 0;
}
