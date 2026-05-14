using System.Runtime.InteropServices;
using System.Text;

namespace WinTiles.Core.Services;

public sealed class StartMenuShortcutService
{
    private static readonly PROPERTYKEY AppUserModelIdKey = new(
        new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"),
        5);

    private static readonly PROPERTYKEY RelaunchCommandKey = new(
        new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"),
        2);

    private static readonly PROPERTYKEY RelaunchDisplayNameKey = new(
        new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"),
        4);

    private static readonly PROPERTYKEY RelaunchIconResourceKey = new(
        new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"),
        3);

    private static readonly PROPERTYKEY VisualElementsManifestHintPathKey = new(
        new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"),
        31);

    public string CreateShortcut(
        string shortcutPath,
        string targetPath,
        string arguments,
        string workingDirectory,
        string description,
        string appUserModelId,
        string? iconPath = null,
        string? visualElementsManifestHintPath = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(shortcutPath)!);

        var shellLink = (IShellLinkW)(object)new ShellLink();
        try
        {
            var effectiveIconPath = string.IsNullOrWhiteSpace(iconPath) ? targetPath : iconPath;
            shellLink.SetPath(targetPath);
            shellLink.SetArguments(arguments);
            shellLink.SetWorkingDirectory(workingDirectory);
            shellLink.SetDescription(description);
            shellLink.SetIconLocation(effectiveIconPath, 0);

            var propertyStore = (IPropertyStore)shellLink;
            SetStringProperty(propertyStore, AppUserModelIdKey, appUserModelId);
            SetStringProperty(propertyStore, RelaunchCommandKey, $"\"{targetPath}\" {arguments}");
            SetStringProperty(propertyStore, RelaunchDisplayNameKey, description);
            SetStringProperty(propertyStore, RelaunchIconResourceKey, $"{effectiveIconPath},0");

            // 显式提示壳层去哪里找 Win32 的 VisualElementsManifest，
            // 这样开始菜单更容易按磁贴资源渲染，而不是退回普通快捷方式图标。
            if (!string.IsNullOrWhiteSpace(visualElementsManifestHintPath))
            {
                SetStringProperty(propertyStore, VisualElementsManifestHintPathKey, visualElementsManifestHintPath);
            }

            propertyStore.Commit();

            var persistFile = (IPersistFile)shellLink;
            persistFile.Save(shortcutPath, true);
            return shortcutPath;
        }
        finally
        {
            Marshal.FinalReleaseComObject(shellLink);
        }
    }

    public string? TryReadAppUserModelId(string shortcutPath)
    {
        if (!File.Exists(shortcutPath))
        {
            return null;
        }

        var shellLink = (IShellLinkW)(object)new ShellLink();
        try
        {
            ((IPersistFile)shellLink).Load(shortcutPath, 0);
            var propertyStore = (IPropertyStore)shellLink;
            propertyStore.GetValue(in AppUserModelIdKey, out var value);
            try
            {
                return value.GetString();
            }
            finally
            {
                value.Dispose();
            }
        }
        finally
        {
            Marshal.FinalReleaseComObject(shellLink);
        }
    }

    private static void SetStringProperty(IPropertyStore propertyStore, PROPERTYKEY key, string value)
    {
        using var propVariant = PROPVARIANT.FromString(value);
        propertyStore.SetValue(in key, in propVariant);
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct PROPERTYKEY
    {
        public PROPERTYKEY(Guid formatId, uint propertyId)
        {
            fmtid = formatId;
            pid = propertyId;
        }

        public Guid fmtid;

        public uint pid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WIN32_FIND_DATAW
    {
        public uint dwFileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
        public uint nFileSizeHigh;
        public uint nFileSizeLow;
        public uint dwReserved0;
        public uint dwReserved1;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string cFileName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
        public string cAlternateFileName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROPVARIANT : IDisposable
    {
        private const ushort VT_LPWSTR = 31;

        public ushort vt;
        public ushort wReserved1;
        public ushort wReserved2;
        public ushort wReserved3;
        public nint pwszVal;
        public int cVal1;
        public int cVal2;

        public static PROPVARIANT FromString(string value)
        {
            return new PROPVARIANT
            {
                vt = VT_LPWSTR,
                pwszVal = Marshal.StringToCoTaskMemUni(value)
            };
        }

        public string? GetString() => vt == VT_LPWSTR && pwszVal != nint.Zero
            ? Marshal.PtrToStringUni(pwszVal)
            : null;

        public void Dispose()
        {
            PropVariantClear(ref this);
        }
    }

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PROPVARIANT pvar);

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private sealed class ShellLink
    {
    }

    [ComImport]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkW
    {
        void GetPath([Out] StringBuilder pszFile, int cchMaxPath, out WIN32_FIND_DATAW pfd, uint fFlags);
        void GetIDList(out nint ppidl);
        void SetIDList(nint pidl);
        void GetDescription([Out] StringBuilder pszName, int cchMaxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out] StringBuilder pszDir, int cchMaxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out] StringBuilder pszArgs, int cchMaxPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
        void Resolve(nint hwnd, uint fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport]
    [Guid("0000010b-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPersistFile
    {
        void GetClassID(out Guid pClassID);
        void IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, bool fRemember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
        void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
    }

    [ComImport]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        void GetCount(out uint cProps);
        void GetAt(uint iProp, out PROPERTYKEY pkey);
        void GetValue(in PROPERTYKEY key, out PROPVARIANT pv);
        void SetValue(in PROPERTYKEY key, in PROPVARIANT propvar);
        void Commit();
    }
}
