#include <windows.h>
#include <roapi.h>
#include <wrl/client.h>
#include <wrl/wrappers/corewrappers.h>
#include <inspectable.h>
#include <windows.foundation.h>
#include <string>
#include <algorithm>
#include <iostream>

#pragma comment(lib, "runtimeobject.lib")
#pragma comment(lib, "ole32.lib")

using namespace Microsoft::WRL;
using namespace Microsoft::WRL::Wrappers;

#define RETURN_IF_FAILED_LOCAL(expression)            \
    do                                               \
    {                                                \
        const HRESULT localHr = (expression);        \
        if (FAILED(localHr))                         \
        {                                            \
            return localHr;                          \
        }                                            \
    } while (false)

namespace ABI::WindowsInternal::Shell::UnifiedTile
{
    enum UnifiedTileIdentifierKind
    {
        UnifiedTileIdentifierKind_Unknown = 0x0,
        UnifiedTileIdentifierKind_Packaged = 0x1,
        UnifiedTileIdentifierKind_Win32 = 0x2,
        UnifiedTileIdentifierKind_TargetedContent = 0x3,
    };

    MIDL_INTERFACE("d3653510-4fff-4bfa-905b-ea038b142fa5")
    IUnifiedTileIdentifier : public IInspectable
    {
        virtual HRESULT STDMETHODCALLTYPE get_Kind(UnifiedTileIdentifierKind*) = 0;
        virtual HRESULT STDMETHODCALLTYPE get_SerializedIdentifier(HSTRING*) = 0;
        virtual HRESULT STDMETHODCALLTYPE get_NotificationId(HSTRING*) = 0;
        virtual HRESULT STDMETHODCALLTYPE get_TelemetryId(HSTRING*) = 0;
        virtual HRESULT STDMETHODCALLTYPE IsEqual(IUnifiedTileIdentifier*, BOOLEAN*) = 0;
    };

    MIDL_INTERFACE("87a52467-266a-4b20-a2c8-e316bfbaf64a")
    IUnifiedTileIdentifierStatics : public IInspectable
    {
        virtual HRESULT STDMETHODCALLTYPE DeserializeIdentifier(HSTRING, IUnifiedTileIdentifier**) = 0;
    };

    MIDL_INTERFACE("0e7735be-a965-44a6-a75f-54b8bcd67bec")
    IWin32UnifiedTileIdentifierFactory : public IInspectable
    {
        virtual HRESULT STDMETHODCALLTYPE Create(HSTRING, IUnifiedTileIdentifier**) = 0;
    };
}

namespace ABI::WindowsInternal::Shell::UnifiedTile::Private
{
    MIDL_INTERFACE("0083831c-82d6-4e8f-bcc2-a8ac2691be49")
    IUnifiedTileUserPinHelperStatics : public IInspectable
    {
        virtual HRESULT STDMETHODCALLTYPE CreateUserPinnedShortcutTile(ABI::WindowsInternal::Shell::UnifiedTile::IUnifiedTileIdentifier*) = 0;
    };
}

namespace ABI::WindowsInternal::Shell::UnifiedTile::CuratedTileCollections
{
    enum TilePinSize
    {
        TilePinSize_Tile2x2 = 0,
        TilePinSize_Tile4x2 = 1,
    };

    enum PackageStatusChangeType
    {
    };

    MIDL_INTERFACE("354cba6d-19ab-490c-97b6-8d4d9862e052")
    ICuratedTileGroup : public IInspectable
    {
    };

    MIDL_INTERFACE("a680369c-0862-41a0-b7cd-bb35e3c497eb")
    ICuratedTileCollectionOptions : public IInspectable
    {
    };

    MIDL_INTERFACE("51a07090-3a1f-49ef-9932-a971b8154790")
    ICuratedTileCollection : public IInspectable
    {
        virtual HRESULT STDMETHODCALLTYPE get_CollectionName(HSTRING*) = 0;
        virtual HRESULT STDMETHODCALLTYPE get_Attributes(int*) = 0;
        virtual HRESULT STDMETHODCALLTYPE put_Attributes(int) = 0;
        virtual HRESULT STDMETHODCALLTYPE get_Version(UINT*) = 0;
        virtual HRESULT STDMETHODCALLTYPE put_Version(UINT) = 0;
        virtual HRESULT STDMETHODCALLTYPE GetGroups(IInspectable**) = 0;
        virtual HRESULT STDMETHODCALLTYPE GetTiles(IInspectable**) = 0;
        virtual HRESULT STDMETHODCALLTYPE GetAllTilesInCollection(IInspectable**) = 0;
        virtual HRESULT STDMETHODCALLTYPE DoesCollectionContainTile(ABI::WindowsInternal::Shell::UnifiedTile::IUnifiedTileIdentifier*, IInspectable**, BOOLEAN*) = 0;
        virtual HRESULT STDMETHODCALLTYPE FindTileAndParentGroup(ABI::WindowsInternal::Shell::UnifiedTile::IUnifiedTileIdentifier*, IInspectable**, IInspectable**, BOOLEAN*) = 0;
        virtual HRESULT STDMETHODCALLTYPE MoveExistingGroupToNewParent(ICuratedTileGroup*, ICuratedTileGroup*) = 0;
        virtual HRESULT STDMETHODCALLTYPE CreateNewGroup(ICuratedTileGroup**) = 0;
        virtual HRESULT STDMETHODCALLTYPE GetGroup(GUID, ICuratedTileGroup**) = 0;
        virtual HRESULT STDMETHODCALLTYPE DeleteGroup(GUID) = 0;
        virtual HRESULT STDMETHODCALLTYPE RemoveGroup(GUID) = 0;
        virtual HRESULT STDMETHODCALLTYPE MoveExistingTileToNewParent(IInspectable*, ICuratedTileGroup*) = 0;
        virtual HRESULT STDMETHODCALLTYPE AddTile(ABI::WindowsInternal::Shell::UnifiedTile::IUnifiedTileIdentifier*, IInspectable**) = 0;
        virtual HRESULT STDMETHODCALLTYPE AddTileWithId(ABI::WindowsInternal::Shell::UnifiedTile::IUnifiedTileIdentifier*, GUID, IInspectable**) = 0;
        virtual HRESULT STDMETHODCALLTYPE GetTile(GUID, IInspectable**) = 0;
        virtual HRESULT STDMETHODCALLTYPE DeleteTile(GUID) = 0;
        virtual HRESULT STDMETHODCALLTYPE RemoveTile(GUID) = 0;
        virtual HRESULT STDMETHODCALLTYPE Commit() = 0;
        virtual HRESULT STDMETHODCALLTYPE CommitAsync(ABI::Windows::Foundation::IAsyncAction**) = 0;
        virtual HRESULT STDMETHODCALLTYPE CommitAsyncWithTimerBypass(ABI::Windows::Foundation::IAsyncAction**) = 0;
        virtual HRESULT STDMETHODCALLTYPE ResetToDefault() = 0;
        virtual HRESULT STDMETHODCALLTYPE ResetToDefaultAsync(ABI::Windows::Foundation::IAsyncAction**) = 0;
        virtual HRESULT STDMETHODCALLTYPE CheckForUpdate() = 0;
        virtual HRESULT STDMETHODCALLTYPE GetCustomProperty(HSTRING, HSTRING*) = 0;
        virtual HRESULT STDMETHODCALLTYPE HasCustomProperty(HSTRING, BOOLEAN*) = 0;
        virtual HRESULT STDMETHODCALLTYPE RemoveCustomProperty(HSTRING) = 0;
        virtual HRESULT STDMETHODCALLTYPE SetCustomProperty(HSTRING, HSTRING) = 0;
    };

    MIDL_INTERFACE("adbf8965-6056-4126-ab26-6660af4661ce")
    IStartTileCollection : public IInspectable
    {
        virtual HRESULT STDMETHODCALLTYPE PinToStart(ABI::WindowsInternal::Shell::UnifiedTile::IUnifiedTileIdentifier*, TilePinSize) = 0;
        virtual HRESULT STDMETHODCALLTYPE PinToStartAtLocation(
            ABI::WindowsInternal::Shell::UnifiedTile::IUnifiedTileIdentifier*,
            ICuratedTileGroup*,
            ABI::Windows::Foundation::Point,
            ABI::Windows::Foundation::Size) = 0;
        virtual HRESULT STDMETHODCALLTYPE UnpinFromStart(ABI::WindowsInternal::Shell::UnifiedTile::IUnifiedTileIdentifier*) = 0;
    };

    MIDL_INTERFACE("899ee71b-5c01-438f-b12e-61d49f6b4083")
    ICuratedTileCollectionManager : public IInspectable
    {
        // 这里必须和 ExplorerPatcher 里对应的内部接口顺序完全一致，
        // 否则 WRL 会把 GetCollection 调到错误的 vtable 槽位上，导致“返回成功但对象为空”。
        virtual HRESULT STDMETHODCALLTYPE NotifyPackageStatusChanged(HSTRING, PackageStatusChangeType) = 0;
        virtual HRESULT STDMETHODCALLTYPE GetCollection(HSTRING, ICuratedTileCollection**) = 0;
        virtual HRESULT STDMETHODCALLTYPE GetCollectionWithOptions(HSTRING, ICuratedTileCollectionOptions*, ICuratedTileCollection**) = 0;
        virtual HRESULT STDMETHODCALLTYPE DeleteCollection(HSTRING) = 0;
        virtual HRESULT STDMETHODCALLTYPE CollectionExists(HSTRING, BOOLEAN*) = 0;
        virtual HRESULT STDMETHODCALLTYPE InitializeCollection(HSTRING) = 0;
    };
}

enum class RequestedSize
{
    Medium2x2,
    Wide4x2
};

struct PinArguments
{
    std::wstring tileId;
    RequestedSize size = RequestedSize::Medium2x2;
    std::wstring hostExePath;
};

struct PinResult
{
    int exitCode = 0;
    std::wstring status = L"success";
    std::wstring message = L"";
    std::wstring pinMethod = L"Unknown";
    std::wstring warning = L"";
    std::wstring identityKind = L"";
    std::wstring identityValue = L"";
    int containsBefore = -1;
    int containsAfterCommit = -1;
    int containsAfterReopen = -1;
};

static std::wstring EscapeJson(const std::wstring& value)
{
    std::wstring escaped;
    escaped.reserve(value.size() + 8);
    for (wchar_t character : value)
    {
        switch (character)
        {
        case L'\\': escaped += L"\\\\"; break;
        case L'"': escaped += L"\\\""; break;
        case L'\r': escaped += L"\\r"; break;
        case L'\n': escaped += L"\\n"; break;
        case L'\t': escaped += L"\\t"; break;
        default:
            // 这里主动把非 ASCII 字符转成 \uXXXX，避免 helper 在重定向 stdout 时
            // 因控制台编码不一致把中文 JSON 截断，导致主程序只能读到半截结果。
            if (character < 0x20 || character > 0x7E)
            {
                wchar_t buffer[8] = {};
                swprintf_s(buffer, L"\\u%04X", static_cast<unsigned int>(character));
                escaped += buffer;
            }
            else
            {
                escaped += character;
            }
            break;
        }
    }
    return escaped;
}

static std::string ToAsciiString(const std::wstring& value)
{
    std::string converted;
    converted.reserve(value.size());
    for (wchar_t character : value)
    {
        converted.push_back(static_cast<char>(character));
    }

    return converted;
}

static std::wstring BuildAppUserModelId(const std::wstring& tileId)
{
    return L"WinTiles.Image." + tileId;
}

static bool IsConfirmedInCollection(const PinResult& result)
{
    return result.containsAfterReopen == 1 || result.containsAfterCommit == 1;
}

static void SetContainsState(int& target, bool value)
{
    target = value ? 1 : 0;
}

static std::string BoolStateToJson(int value)
{
    return value < 0 ? "null" : (value == 0 ? "false" : "true");
}

static std::wstring ToLowerCopy(const std::wstring& value)
{
    auto lowered = value;
    std::transform(lowered.begin(), lowered.end(), lowered.begin(), towlower);
    return lowered;
}

static bool IsClassicModeEnabled()
{
    DWORD value = 0;
    DWORD valueSize = sizeof(value);
    return RegGetValueW(
        HKEY_CURRENT_USER,
        L"Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced",
        L"Start_ShowClassicMode",
        RRF_RT_REG_DWORD,
        nullptr,
        &value,
        &valueSize) == ERROR_SUCCESS && value == 1;
}

static RequestedSize ParseRequestedSize(const std::wstring& value)
{
    const auto lowered = ToLowerCopy(value);
    if (lowered == L"4x2")
    {
        return RequestedSize::Wide4x2;
    }

    return RequestedSize::Medium2x2;
}

static std::wstring FormatHResult(HRESULT hr)
{
    wchar_t buffer[32] = {};
    swprintf_s(buffer, L"0x%08X", static_cast<unsigned int>(hr));
    return buffer;
}

static HRESULT GetActivationFactoryByName(const wchar_t* className, REFIID iid, void** result)
{
    HString classId;
    RETURN_IF_FAILED_LOCAL(classId.Set(className));
    return RoGetActivationFactory(classId.Get(), iid, result);
}

static PinResult CreateFailureResult(const std::wstring& message, const std::wstring& pinMethod)
{
    PinResult result;
    result.exitCode = 1;
    result.status = L"failure";
    result.message = message;
    result.pinMethod = pinMethod;
    return result;
}

static HRESULT OpenStartTileCollection(
    ABI::WindowsInternal::Shell::UnifiedTile::CuratedTileCollections::ICuratedTileCollection** tileCollection,
    ABI::WindowsInternal::Shell::UnifiedTile::CuratedTileCollections::IStartTileCollection** startTileCollection)
{
    using namespace ABI::WindowsInternal::Shell::UnifiedTile::CuratedTileCollections;

    if (!tileCollection || !startTileCollection)
    {
        return E_POINTER;
    }

    *tileCollection = nullptr;
    *startTileCollection = nullptr;

    HString collectionManagerClass;
    RETURN_IF_FAILED_LOCAL(collectionManagerClass.Set(L"WindowsInternal.Shell.UnifiedTile.CuratedTileCollections.CuratedTileCollectionManager"));

    ComPtr<IInspectable> collectionManagerInspectable;
    RETURN_IF_FAILED_LOCAL(RoActivateInstance(collectionManagerClass.Get(), &collectionManagerInspectable));

    ComPtr<ICuratedTileCollectionManager> collectionManager;
    RETURN_IF_FAILED_LOCAL(collectionManagerInspectable.As(&collectionManager));

    HString tileGridName;
    RETURN_IF_FAILED_LOCAL(tileGridName.Set(L"Start.TileGrid"));

    ComPtr<ICuratedTileCollection> localTileCollection;
    RETURN_IF_FAILED_LOCAL(collectionManager->GetCollection(tileGridName.Get(), &localTileCollection));
    RETURN_IF_FAILED_LOCAL(localTileCollection.CopyTo(tileCollection));

    ComPtr<IStartTileCollection> localStartTileCollection;
    RETURN_IF_FAILED_LOCAL(localTileCollection.As(&localStartTileCollection));
    RETURN_IF_FAILED_LOCAL(localStartTileCollection.CopyTo(startTileCollection));

    return S_OK;
}

static HRESULT WaitForAsyncAction(ABI::Windows::Foundation::IAsyncAction* asyncAction, DWORD timeoutMilliseconds)
{
    using namespace ABI::Windows::Foundation;

    if (!asyncAction)
    {
        return E_POINTER;
    }

    ComPtr<IAsyncInfo> asyncInfo;
    RETURN_IF_FAILED_LOCAL(asyncAction->QueryInterface(IID_PPV_ARGS(&asyncInfo)));

    DWORD elapsedMilliseconds = 0;
    constexpr DWORD pollIntervalMilliseconds = 50;
    while (true)
    {
        AsyncStatus status = Started;
        RETURN_IF_FAILED_LOCAL(asyncInfo->get_Status(&status));

        if (status == Completed)
        {
            return asyncAction->GetResults();
        }

        if (status == Error)
        {
            HRESULT errorCode = S_OK;
            RETURN_IF_FAILED_LOCAL(asyncInfo->get_ErrorCode(&errorCode));
            return errorCode;
        }

        if (status == Canceled)
        {
            return HRESULT_FROM_WIN32(ERROR_CANCELLED);
        }

        if (elapsedMilliseconds >= timeoutMilliseconds)
        {
            return HRESULT_FROM_WIN32(WAIT_TIMEOUT);
        }

        Sleep(pollIntervalMilliseconds);
        elapsedMilliseconds += pollIntervalMilliseconds;
    }
}

static void AppendWarning(PinResult& result, const std::wstring& message)
{
    if (message.empty())
    {
        return;
    }

    result.status = L"warning";
    if (!result.warning.empty())
    {
        result.warning += L" ";
    }

    result.warning += message;
}

static HRESULT CheckContains(
    ABI::WindowsInternal::Shell::UnifiedTile::CuratedTileCollections::ICuratedTileCollection* tileCollection,
    ABI::WindowsInternal::Shell::UnifiedTile::IUnifiedTileIdentifier* unifiedTileIdentifier,
    bool* contains)
{
    using namespace ABI::WindowsInternal::Shell::UnifiedTile::CuratedTileCollections;

    if (!tileCollection || !unifiedTileIdentifier || !contains)
    {
        return E_POINTER;
    }

    BOOLEAN value = FALSE;
    RETURN_IF_FAILED_LOCAL(tileCollection->DoesCollectionContainTile(unifiedTileIdentifier, nullptr, &value));
    *contains = value != FALSE;
    return S_OK;
}

static HRESULT CreateUnifiedTileIdentifier(
    ABI::WindowsInternal::Shell::UnifiedTile::IWin32UnifiedTileIdentifierFactory* win32Factory,
    const std::wstring& identityValue,
    ABI::WindowsInternal::Shell::UnifiedTile::IUnifiedTileIdentifier** unifiedTileIdentifier)
{
    using namespace ABI::WindowsInternal::Shell::UnifiedTile;

    if (!win32Factory || !unifiedTileIdentifier)
    {
        return E_POINTER;
    }

    HString identityString;
    RETURN_IF_FAILED_LOCAL(identityString.Set(identityValue.c_str()));
    return win32Factory->Create(identityString.Get(), unifiedTileIdentifier);
}

static HRESULT CommitTileCollection(
    ABI::WindowsInternal::Shell::UnifiedTile::CuratedTileCollections::ICuratedTileCollection* tileCollection,
    PinResult& result)
{
    using namespace ABI::Windows::Foundation;
    using namespace ABI::WindowsInternal::Shell::UnifiedTile::CuratedTileCollections;

    if (!tileCollection)
    {
        return E_POINTER;
    }

    HRESULT hr = tileCollection->Commit();
    if (FAILED(hr))
    {
        return hr;
    }

    ComPtr<IAsyncAction> asyncAction;
    hr = tileCollection->CommitAsyncWithTimerBypass(&asyncAction);
    if (FAILED(hr))
    {
        hr = tileCollection->CommitAsync(&asyncAction);
    }

    if (FAILED(hr))
    {
        AppendWarning(result, L"未能触发额外的 Start.TileGrid 刷新。");
        return S_OK;
    }

    hr = WaitForAsyncAction(asyncAction.Get(), 5000);
    if (FAILED(hr))
    {
        AppendWarning(result, L"Start.TileGrid 异步刷新未确认完成：" + FormatHResult(hr));
    }

    return S_OK;
}

static PinResult ExecutePinForShortcutTile(
    const PinArguments& arguments,
    ABI::WindowsInternal::Shell::UnifiedTile::Private::IUnifiedTileUserPinHelperStatics* userPinHelper,
    ABI::WindowsInternal::Shell::UnifiedTile::IWin32UnifiedTileIdentifierFactory* win32Factory,
    const std::wstring& pinIdentityKind,
    const std::wstring& pinIdentityValue,
    const std::wstring& probeIdentityKind,
    const std::wstring& probeIdentityValue)
{
    using namespace ABI::WindowsInternal::Shell::UnifiedTile;
    using namespace ABI::WindowsInternal::Shell::UnifiedTile::CuratedTileCollections;

    PinResult result;
    result.identityKind = pinIdentityKind;
    result.identityValue = pinIdentityValue;

    ComPtr<IUnifiedTileIdentifier> pinUnifiedTileIdentifier;
    auto hr = CreateUnifiedTileIdentifier(win32Factory, pinIdentityValue, &pinUnifiedTileIdentifier);
    if (FAILED(hr))
    {
        auto failure = CreateFailureResult(L"创建 Win32 tile 标识失败：" + FormatHResult(hr), L"Win32UnifiedTileIdentifierFactory.Create");
        failure.identityKind = pinIdentityKind;
        failure.identityValue = pinIdentityValue;
        return failure;
    }

    ComPtr<IUnifiedTileIdentifier> probeUnifiedTileIdentifier;
    const auto& effectiveProbeIdentityKind = probeIdentityValue.empty() ? pinIdentityKind : probeIdentityKind;
    const auto& effectiveProbeIdentityValue = probeIdentityValue.empty() ? pinIdentityValue : probeIdentityValue;
    hr = CreateUnifiedTileIdentifier(win32Factory, effectiveProbeIdentityValue, &probeUnifiedTileIdentifier);
    if (FAILED(hr))
    {
        auto failure = CreateFailureResult(L"创建探测 tile 标识失败：" + FormatHResult(hr), L"ProbeUnifiedTileIdentifierFactory.Create");
        failure.identityKind = pinIdentityKind;
        failure.identityValue = pinIdentityValue;
        failure.warning = L"探测身份为 " + effectiveProbeIdentityKind + L"。";
        return failure;
    }

    ComPtr<ICuratedTileCollection> tileCollection;
    ComPtr<IStartTileCollection> startTileCollection;
    hr = OpenStartTileCollection(&tileCollection, &startTileCollection);
    if (FAILED(hr))
    {
        auto failure = CreateFailureResult(L"打开 Start.TileGrid 失败：" + FormatHResult(hr), L"OpenStartTileCollection");
        failure.identityKind = pinIdentityKind;
        failure.identityValue = pinIdentityValue;
        return failure;
    }

    bool contains = false;
    if (SUCCEEDED(CheckContains(tileCollection.Get(), probeUnifiedTileIdentifier.Get(), &contains)))
    {
        SetContainsState(result.containsBefore, contains);
    }

    hr = userPinHelper->CreateUserPinnedShortcutTile(pinUnifiedTileIdentifier.Get());
    if (FAILED(hr))
    {
        auto failure = CreateFailureResult(L"创建用户固定快捷方式磁贴失败：" + FormatHResult(hr), L"CreateUserPinnedShortcutTile");
        failure.identityKind = pinIdentityKind;
        failure.identityValue = pinIdentityValue;
        failure.containsBefore = result.containsBefore;
        return failure;
    }

    if (arguments.size == RequestedSize::Wide4x2)
    {
        hr = startTileCollection->PinToStart(pinUnifiedTileIdentifier.Get(), TilePinSize_Tile4x2);
        result.pinMethod = L"PinToStart.Tile4x2";
        result.message = L"已请求固定为 4x2";
    }
    else
    {
        hr = startTileCollection->PinToStart(pinUnifiedTileIdentifier.Get(), TilePinSize_Tile2x2);
        result.pinMethod = L"PinToStart.Tile2x2";
        result.message = L"已请求固定为 2x2";
    }

    if (FAILED(hr))
    {
        auto failure = CreateFailureResult(L"调用固定接口失败：" + FormatHResult(hr), result.pinMethod);
        failure.identityKind = pinIdentityKind;
        failure.identityValue = pinIdentityValue;
        failure.containsBefore = result.containsBefore;
        return failure;
    }

    hr = CommitTileCollection(tileCollection.Get(), result);
    if (FAILED(hr))
    {
        auto failure = CreateFailureResult(L"提交 Start.TileGrid 失败：" + FormatHResult(hr), result.pinMethod);
        failure.identityKind = pinIdentityKind;
        failure.identityValue = pinIdentityValue;
        failure.containsBefore = result.containsBefore;
        return failure;
    }

    if (SUCCEEDED(CheckContains(tileCollection.Get(), probeUnifiedTileIdentifier.Get(), &contains)))
    {
        SetContainsState(result.containsAfterCommit, contains);
    }

    ComPtr<ICuratedTileCollection> reopenedTileCollection;
    ComPtr<IStartTileCollection> reopenedStartTileCollection;
    hr = OpenStartTileCollection(&reopenedTileCollection, &reopenedStartTileCollection);
    if (SUCCEEDED(hr) && SUCCEEDED(CheckContains(reopenedTileCollection.Get(), probeUnifiedTileIdentifier.Get(), &contains)))
    {
        SetContainsState(result.containsAfterReopen, contains);
    }

    if (!IsConfirmedInCollection(result))
    {
        result.status = L"warning";
        result.warning += (result.warning.empty() ? L"" : L" ") + std::wstring(L"Start.TileGrid 重新读取后仍未确认包含该磁贴。探测身份为 ") + effectiveProbeIdentityKind + L"。";
    }

    return result;
}

static PinResult PinTile(const PinArguments& arguments)
{
    using namespace ABI::WindowsInternal::Shell::UnifiedTile;
    using namespace ABI::WindowsInternal::Shell::UnifiedTile::Private;

    if (!IsClassicModeEnabled())
    {
        return CreateFailureResult(L"未检测到已启用的 ExplorerPatcher 经典开始菜单。", L"ClassicStartCheck");
    }

    const auto roInitializeResult = RoInitialize(RO_INIT_MULTITHREADED);
    if (FAILED(roInitializeResult))
    {
        return CreateFailureResult(L"RoInitialize 失败：" + FormatHResult(roInitializeResult), L"RoInitialize");
    }

    const bool shouldUninitialize = SUCCEEDED(roInitializeResult) || roInitializeResult == S_FALSE;

    ComPtr<IUnifiedTileIdentifierStatics> unifiedTileStatics;
    HRESULT hr = GetActivationFactoryByName(
        L"WindowsInternal.Shell.UnifiedTile.UnifiedTileIdentifier",
        IID_PPV_ARGS(&unifiedTileStatics));
    if (FAILED(hr))
    {
        if (shouldUninitialize)
        {
            RoUninitialize();
        }

        return CreateFailureResult(L"获取 UnifiedTileIdentifier 失败：" + FormatHResult(hr), L"ActivationFactory.UnifiedTileIdentifier");
    }

    ComPtr<IWin32UnifiedTileIdentifierFactory> win32Factory;
    hr = unifiedTileStatics.As(&win32Factory);
    if (FAILED(hr))
    {
        if (shouldUninitialize)
        {
            RoUninitialize();
        }

        return CreateFailureResult(L"切换到 Win32 tile factory 失败：" + FormatHResult(hr), L"ActivationFactory.Win32Factory");
    }

    ComPtr<IUnifiedTileUserPinHelperStatics> userPinHelper;
    hr = GetActivationFactoryByName(
        L"WindowsInternal.Shell.UnifiedTile.Private.UnifiedTileUserPinHelper",
        IID_PPV_ARGS(&userPinHelper));
    if (FAILED(hr))
    {
        if (shouldUninitialize)
        {
            RoUninitialize();
        }

        return CreateFailureResult(L"获取 UnifiedTileUserPinHelper 失败：" + FormatHResult(hr), L"ActivationFactory.UserPinHelper");
    }

    const auto appUserModelId = BuildAppUserModelId(arguments.tileId);

    // 4x2 在经典开始菜单里更容易和 HostExe 对齐；2x2 则继续保留原来的 AppUserModelID 路径。
    const auto useHostExeIdentity = arguments.size == RequestedSize::Wide4x2;
    const std::wstring pinIdentityKind = useHostExeIdentity ? L"HostExe" : L"AppUserModelID";
    const std::wstring pinIdentityValue = useHostExeIdentity ? arguments.hostExePath : appUserModelId;
    auto pinResult = ExecutePinForShortcutTile(
        arguments,
        userPinHelper.Get(),
        win32Factory.Get(),
        pinIdentityKind,
        pinIdentityValue,
        L"",
        L"");

    if (shouldUninitialize)
    {
        RoUninitialize();
    }

    return pinResult;
}

static bool TryParseArguments(int argc, wchar_t** argv, PinArguments& arguments)
{
    if (argc < 2 || std::wstring(argv[1]) != L"pin-image")
    {
        return false;
    }

    for (int index = 2; index < argc - 1; index += 2)
    {
        const std::wstring key = argv[index];
        const std::wstring value = argv[index + 1];

        if (key == L"--tile-id")
        {
            arguments.tileId = value;
        }
        else if (key == L"--size")
        {
            arguments.size = ParseRequestedSize(value);
        }
        else if (key == L"--host-exe")
        {
            arguments.hostExePath = value;
        }
        else if (key == L"--image" || key == L"--launch-target")
        {
            // 兼容旧版前端仍然传这些参数，但 helper 本身已经不再依赖它们。
            continue;
        }
        else
        {
            return false;
        }
    }

    return !arguments.tileId.empty() &&
        !arguments.hostExePath.empty();
}

static void WriteJsonResult(const PinResult& result)
{
    const auto status = ToAsciiString(EscapeJson(result.status));
    const auto message = ToAsciiString(EscapeJson(result.message));
    const auto pinMethod = ToAsciiString(EscapeJson(result.pinMethod));
    const auto warning = ToAsciiString(EscapeJson(result.warning));
    const auto identityKind = ToAsciiString(EscapeJson(result.identityKind));
    const auto identityValue = ToAsciiString(EscapeJson(result.identityValue));

    std::cout
        << "{"
        << "\"status\":\"" << status << "\","
        << "\"message\":\"" << message << "\","
        << "\"pinMethod\":\"" << pinMethod << "\","
        << "\"warning\":\"" << warning << "\","
        << "\"identityKind\":\"" << identityKind << "\","
        << "\"identityValue\":\"" << identityValue << "\","
        << "\"containsBefore\":" << BoolStateToJson(result.containsBefore) << ","
        << "\"containsAfterCommit\":" << BoolStateToJson(result.containsAfterCommit) << ","
        << "\"containsAfterReopen\":" << BoolStateToJson(result.containsAfterReopen)
        << "}";
}

int wmain(int argc, wchar_t** argv)
{
    PinArguments arguments;
    if (!TryParseArguments(argc, argv, arguments))
    {
        PinResult result;
        result.exitCode = 1;
        result.status = L"failure";
        result.message = L"PinHelper 参数无效。";
        result.pinMethod = L"ArgumentParsing";
        WriteJsonResult(result);
        return result.exitCode;
    }

    const auto result = PinTile(arguments);
    WriteJsonResult(result);
    return result.exitCode;
}
