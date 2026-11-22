using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace WinServMgr
{
    /// <summary>
    /// Only change fields explicitly edited by the user. Other SCM configuration
    /// fields remain SERVICE_NO_CHANGE / NULL. No raw registry writes.
    /// </summary>
    internal static class ServiceConfiguration
    {
        private const uint ScManagerConnect = 0x0001;
        private const uint ServiceChangeConfig = 0x0002;
        private const uint NoChange = 0xffffffff;
        private const uint AutoStart = 2;
        private const uint DemandStart = 3;
        private const uint Disabled = 4;
        private const uint DelayedAutoStartInfo = 3;

        [StructLayout(LayoutKind.Sequential)]
        private struct ServiceDelayedAutoStartInfo
        {
            [MarshalAs(UnmanagedType.Bool)]
            public bool IsDelayed;
        }

        [DllImport("advapi32.dll", EntryPoint = "OpenSCManagerW",
            CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr OpenSCManager(
            string computerName, string databaseName, uint desiredAccess);

        [DllImport("advapi32.dll", EntryPoint = "OpenServiceW",
            CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr OpenService(
            IntPtr manager, string serviceName, uint desiredAccess);

        [DllImport("advapi32.dll", EntryPoint = "ChangeServiceConfigW",
            CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ChangeServiceConfig(
            IntPtr service, uint serviceType, uint startType, uint errorControl,
            string binaryPathName, string loadOrderGroup, IntPtr tagId,
            string dependencies, string serviceStartName, string password, string displayName);

        [DllImport("advapi32.dll", EntryPoint = "ChangeServiceConfig2W",
            CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ChangeServiceConfig2(
            IntPtr service, uint informationLevel,
            ref ServiceDelayedAutoStartInfo info);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseServiceHandle(IntPtr handle);

        internal static void Save(string serviceName, string newDisplayName,
            string newStartup)
        {
            if (newDisplayName == null && newStartup == null) return;

            uint start = NoChange;
            bool delayed = false;
            if (newStartup != null)
            {
                switch (newStartup)
                {
                    case "自动":
                        start = AutoStart;
                        break;
                    case "自动（延迟启动）":
                        start = AutoStart;
                        delayed = true;
                        break;
                    case "手动":
                        start = DemandStart;
                        break;
                    case "禁用":
                        start = Disabled;
                        break;
                    default:
                        throw new ArgumentException("不支持的启动类型：" + newStartup);
                }
            }

            IntPtr manager = OpenSCManager(null, null, ScManagerConnect);
            if (manager == IntPtr.Zero)
                throw LastError("无法打开服务控制管理器");
            try
            {
                IntPtr service = OpenService(manager, serviceName, ServiceChangeConfig);
                if (service == IntPtr.Zero)
                    throw LastError("没有修改服务 [" + serviceName + "] 的权限");
                try
                {
                    // lpDisplayName=null means preserve the existing name;
                    // dwStartType=SERVICE_NO_CHANGE preserves the existing start mode.
                    if (!ChangeServiceConfig(service, NoChange, start, NoChange,
                        null, null, IntPtr.Zero, null, null, null, newDisplayName))
                        throw LastError("修改服务配置失败");

                    // Only automatic services can use delayed auto-start.
                    // Explicitly clear delayed mode when switching to ordinary auto.
                    if (start == AutoStart)
                    {
                        var info = new ServiceDelayedAutoStartInfo { IsDelayed = delayed };
                        if (!ChangeServiceConfig2(service, DelayedAutoStartInfo, ref info))
                            throw LastError("启动类型已设为自动，但设置延迟启动失败；请刷新核对实际状态");
                    }
                }
                finally
                {
                    CloseServiceHandle(service);
                }
            }
            finally
            {
                CloseServiceHandle(manager);
            }
        }

        private static Win32Exception LastError(string action)
        {
            int error = Marshal.GetLastWin32Error();
            return new Win32Exception(error,
                action + "（Windows 错误 " + error + "：" +
                new Win32Exception(error).Message + "）");
        }
    }
}
