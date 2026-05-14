#include <windows.h>
#include <shellapi.h>
#include <string>

static std::wstring ReadConfigValue(const std::wstring& section, const std::wstring& key, const std::wstring& configPath)
{
    wchar_t buffer[2048] = {};
    GetPrivateProfileStringW(section.c_str(), key.c_str(), L"", buffer, ARRAYSIZE(buffer), configPath.c_str());
    return buffer;
}

static std::wstring Quote(const std::wstring& value)
{
    return L"\"" + value + L"\"";
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

    // TileHost only forwards activation back to the main WinTiles process.
    std::wstring parameters = std::wstring(L"--tile-id ") + Quote(tileId);
    auto mainExecutableDirectory = mainExecutable.substr(0, mainExecutable.find_last_of(L"\\/"));

    SHELLEXECUTEINFOW executeInfo = {};
    executeInfo.cbSize = sizeof(executeInfo);
    executeInfo.fMask = SEE_MASK_NOCLOSEPROCESS;
    executeInfo.lpFile = mainExecutable.c_str();
    executeInfo.lpParameters = parameters.c_str();
    executeInfo.lpDirectory = mainExecutableDirectory.c_str();
    executeInfo.nShow = SW_SHOWNORMAL;

    if (!ShellExecuteExW(&executeInfo))
    {
        return 3;
    }

    if (executeInfo.hProcess)
    {
        CloseHandle(executeInfo.hProcess);
    }

    return 0;
}
