using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using PTBox.Launcher.Models;

namespace PTBox.Launcher.Services;

public static class ShortcutService
{
    public static LauncherItem Resolve(string shortcut)
    {
        // Read stored metadata only: Resolve() would search disks/network or show Shell UI.
        var initialized = CoInitializeEx(IntPtr.Zero, 0);
        if (initialized < 0 && initialized != unchecked((int)0x80010106)) Marshal.ThrowExceptionForHR(initialized);
        object? instance = null;
        try
        {
            instance = new ShellLink();
            ((IPersistFile)instance).Load(shortcut, 0);
            var link = (IShellLinkW)instance;
            var target = new StringBuilder(32768); var arguments = new StringBuilder(32768); var working = new StringBuilder(32768);
            link.GetPath(target, target.Capacity, IntPtr.Zero, 4); // SLGP_RAWPATH
            link.GetArguments(arguments, arguments.Capacity);
            link.GetWorkingDirectory(working, working.Capacity);
            var path = Environment.ExpandEnvironmentVariables(target.ToString().Trim().Trim('"'));
            if (!Path.IsPathFullyQualified(path) || !Path.GetExtension(path).Equals(".exe", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("此快捷方式没有可直接启动的 EXE 目标，请选择程序本身，或使用网页 / 游戏的 URL 快捷方式。");
            if (!File.Exists(path)) throw new FileNotFoundException("快捷方式指向的程序不存在，请重新选择已安装的程序。", path);
            ((IShellLinkDataList)instance).GetFlags(out var flags);
            var directory = working.ToString();
            if (!string.IsNullOrWhiteSpace(directory))
                directory = Path.GetFullPath(Environment.ExpandEnvironmentVariables(directory.Trim().Trim('"')), Path.GetDirectoryName(path)!);
            return new LauncherItem
            {
                Name = Path.GetFileNameWithoutExtension(shortcut), Category = "apps", Path = path,
                Arguments = arguments.ToString(), WorkingDirectory = directory,
                RunAsAdministrator = (flags & 0x2000) != 0 // SLDF_RUNAS_USER
            };
        }
        catch (COMException ex) { throw new InvalidDataException("无法读取此快捷方式，请重新选择程序的 EXE 文件。", ex); }
        finally
        {
            if (instance != null) Marshal.FinalReleaseComObject(instance);
            if (initialized >= 0) CoUninitialize();
        }
    }

    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLink { }

    // Methods must stay in native vtable order, including unused slots.
    [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int capacity, IntPtr findData, uint flags);
        void GetIDList(out IntPtr list);
        void SetIDList(IntPtr list);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder text, int capacity);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string text);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int capacity);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string path);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder arguments, int capacity);
    }
    [ComImport, Guid("45E2B4AE-B1C3-11D0-B92F-00A0C90312E1"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkDataList
    {
        void AddDataBlock(IntPtr block);
        void CopyDataBlock(uint signature, out IntPtr block);
        void RemoveDataBlock(uint signature);
        void GetFlags(out uint flags);
    }
    [DllImport("ole32.dll")] private static extern int CoInitializeEx(IntPtr reserved, uint flags);
    [DllImport("ole32.dll")] private static extern void CoUninitialize();
}
