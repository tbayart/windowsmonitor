using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WindowsMonitorLib.Counters
{
    public class CounterFactory
    {
        /// <summary>
        /// Creates counters for each physical disk connected
        /// </summary>
        /// <returns>enumeration of counters</returns>
        public static IEnumerable<SimpleCounter> CreatePhysicalDiskCounters()
        {
            List<SimpleCounter> list = new List<SimpleCounter>();

            System.Diagnostics.PerformanceCounterCategory category = new System.Diagnostics.PerformanceCounterCategory("PhysicalDisk");
            foreach (string instance in category.GetInstanceNames().OrderBy(s => s))
            {
                if (instance == "_Total") { continue; }

                System.Diagnostics.PerformanceCounter pc = new System.Diagnostics.PerformanceCounter("PhysicalDisk", "% Idle Time", instance, true);
                list.Add(new ReversePerformanceCounter(new PerformanceCounter(pc), new StaticPerformanceCounter(100)));
            }

            return list;
        }

        /// <summary>
        /// Creates physical memory counter
        /// </summary>
        public static SimpleCounter CreatePhysicalMemoryCounter()
        {
            System.Diagnostics.PerformanceCounter pc = new System.Diagnostics.PerformanceCounter("Memory", "Available Bytes", null, true);
            return new ReversePerformanceCounter(new PerformanceCounter(pc), new MaxMemoryPerformanceCounter());
        }

        /// <summary>
        /// Creates virtual memory counter
        /// </summary>
        public static SimpleCounter CreateVirtualMemoryCounter()
        {
            System.Diagnostics.PerformanceCounter pc = new System.Diagnostics.PerformanceCounter("Memory", "Committed Bytes", null, true);
            System.Diagnostics.PerformanceCounter pcMax = new System.Diagnostics.PerformanceCounter("Memory", "Commit Limit", null, true);

            return new KnownMaxPerformanceCounter(new PerformanceCounter(pc), new PerformanceCounter(pcMax));
        }

        /// <summary>
        /// Creates CPU counter
        /// </summary>
        public static SimpleCounter CreateCPUCounter()
        {
            // Créer un compteur par CPU
            System.Diagnostics.PerformanceCounterCategory category = new System.Diagnostics.PerformanceCounterCategory("Processor");
            SimpleCounter mainCounter = null;
            List<SimpleCounter> counters = new List<SimpleCounter>();
            foreach (string instance in category.GetInstanceNames().OrderBy(s => s))
            {
                System.Diagnostics.PerformanceCounter pc = new System.Diagnostics.PerformanceCounter("Processor", "% Processor Time", instance, true);

                SimpleCounter counter = new KnownMaxPerformanceCounter(new PerformanceCounter(pc), new StaticPerformanceCounter(100));

                if (instance == "_Total")
                {
                    mainCounter = counter;
                }
                else
                {
                    counters.Add(counter);
                }
            }

            return new SubPerformanceCounter(mainCounter, counters);
        }

        /// <summary>
        /// Creates network counter
        /// </summary>
        public static SimpleCounter CreateNetworkCounter()
        {
            // Créer les contrôles pour chaque interface réseau
            System.Diagnostics.PerformanceCounterCategory category = new System.Diagnostics.PerformanceCounterCategory("Network Interface");
            List<SimpleCounter> counters = new List<SimpleCounter>();
            foreach (string instance in category.GetInstanceNames().OrderBy(s => s))
            {
                System.Diagnostics.PerformanceCounter pc = new System.Diagnostics.PerformanceCounter("Network Interface", "Bytes Total/sec", instance, true);
                PerformanceCounter counter = new PerformanceCounter(pc);
                counters.Add(new PerformanceCounter(pc));
            }

            return new SumPerformanceCounter(counters);
        }

        /// <summary>
        /// Create counter list from string
        /// </summary>
        /// <param name="counterString">string which represents one or more counters: "\\<machineName>\<categoryName>(<instanceName>)\<counterName>"
        /// counterName and/or instanceName can have the special value "#ALL#", meaning we want to get all of them</param>
        /// <returns>list of counters</returns>
        public static IEnumerable<System.Diagnostics.PerformanceCounter> CreateCountersFromString(string counterString)
        {
            string machineName, categoryName, instanceName, counterName;
            
            ParseCounterString(counterString, out machineName, out categoryName, out instanceName, out counterName);

            System.Diagnostics.PerformanceCounterCategory category = new System.Diagnostics.PerformanceCounterCategory(categoryName, machineName);

            IEnumerable<System.Diagnostics.PerformanceCounter> counters = new System.Diagnostics.PerformanceCounter[] { };

            if (counterName == "#ALL#" && instanceName == "#ALL#")
            {
                foreach (string instance in category.GetInstanceNames().OrderBy(s => s))
                {
                    counters = counters.Concat(category.GetCounters(instance));
                }
            }
            else if (counterName == "#ALL#")
            {
                if (string.IsNullOrEmpty(instanceName))
                {
                    counters = category.GetCounters();
                }
                else
                {
                    counters = category.GetCounters(instanceName);
                }
            }
            else if (instanceName == "#ALL#")
            {
                foreach (string instance in category.GetInstanceNames().OrderBy(s => s))
                {
                    counters = counters.Concat(new System.Diagnostics.PerformanceCounter[] { new System.Diagnostics.PerformanceCounter(categoryName, counterName, instance, machineName) });
                }
            }
            else
            {
                counters = new System.Diagnostics.PerformanceCounter[] { new System.Diagnostics.PerformanceCounter(categoryName, counterName, instanceName, machineName) };
            }

            // Création des contrôles
            return counters;
        }

        /// <summary>
        /// Parsing de chaîne de compteur "\\<machineName>\<categoryName>(<instanceName>)\<counterName>"
        /// </summary>
        /// <param name="counterString"></param>
        /// <param name="machineName">can be "."</param>
        /// <param name="categoryName">can be empty if there is only one instance</param>
        /// <param name="instanceName"></param>
        /// <param name="counterName"></param>
        private static void ParseCounterString(string counterString, out string machineName, out string categoryName, out string instanceName, out string counterName)
        {
            string[] detail = counterString.Substring(2).Split('\\');
            char[] separator = { '(', ')' };
            string[] detail2 = detail[1].Split(separator);

            machineName = detail[0];
            categoryName = detail2[0];
            counterName = detail[2];
            instanceName = detail2.Length >= 2 ? detail2[1] : null;
        }
    }
}
