using System;
using System.ComponentModel;
using System.ServiceProcess;

namespace WinServMgr
{
    public sealed class ServiceEntry
    {
        [DisplayName("服务名称")]
        public string ServiceName { get; set; }

        [DisplayName("显示名称")]
        public string DisplayName { get; set; }

        [DisplayName("状态")]
        public ServiceControllerStatus ServiceState { get; set; }

        // Not a bound column; used to avoid needless grid refreshes.
        public bool StateChanged;

        public ServiceEntry(string serviceName, string displayName, ServiceControllerStatus state)
        {
            ServiceName = serviceName;
            DisplayName = displayName;
            ServiceState = state;
            StateChanged = true;
        }

        public override bool Equals(object obj)
        {
            var other = obj as ServiceEntry;
            return other != null &&
                string.Equals(ServiceName, other.ServiceName, StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode()
        {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(ServiceName ?? "");
        }
    }
}
