using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Threading.Tasks;

namespace WinServMgr
{
    internal static class SrvController
    {
        // Run this on a worker thread, never inside a WinForms timer event.
        internal static List<ServiceEntry> Enumerate()
        {
            var result = new List<ServiceEntry>();
            foreach (var sc in ServiceController.GetServices())
            {
                using (sc)
                {
                    try
                    {
                        result.Add(new ServiceEntry(sc.ServiceName, sc.DisplayName, sc.Status));
                    }
                    catch (InvalidOperationException)
                    {
                        // Services may disappear during enumeration.
                    }
                }
            }
            return result;
        }

        internal static Task PerformAsync(IEnumerable<string> names, Action<ServiceController> action)
        {
            var targets = names.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            return Task.Run(() =>
            {
                var errors = new System.Collections.Concurrent.ConcurrentBag<string>();
                Parallel.ForEach(targets,
                    new ParallelOptions { MaxDegreeOfParallelism = 4 }, name =>
                {
                    try
                    {
                        using (var sc = new ServiceController(name)) action(sc);
                    }
                    catch (Exception ex)
                    {
                        errors.Add(name + ": " + ex.Message);
                    }
                });
                if (!errors.IsEmpty)
                    throw new AggregateException("以下服务操作失败：\r\n" + string.Join("\r\n", errors));
            });
        }
    }
}
