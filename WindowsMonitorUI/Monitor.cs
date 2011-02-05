using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Net.NetworkInformation;
using System.Management;
using System.Configuration;
using WindowsMonitorLib.Counters;

namespace WindowsMonitorUI
{
    public partial class Monitor : Form
    {
        private Point delta;
        private const int RefreshMilliSecondsInterval = 2000;
        private Dolinay.DriveDetector driveDetector;

        public Monitor()
        {
            InitializeComponent();
        }

        private void Monitor_Load(object sender, EventArgs e)
        {
            System.Xml.XmlDocument doc = new System.Xml.XmlDocument();
            doc.Load(ConfigurationManager.AppSettings["settingsFile"]);

            // Création des compteurs
            foreach (System.Xml.XmlNode monitor in doc.SelectNodes("/Monitor/*"))
            {
                if (monitor.Attributes["type"].Value == "predefined")
                {
                    switch (monitor.Attributes["value"].Value)
                    {
                        case "CPU":
                            CreateCPU();
                            break;

                        case "VirtualMemory":
                            CreateVirtualMemory();
                            break;

                        case "PhysicalMemory":
                            CreatePhysicalMemory();
                            break;

                        case "Network":
                            CreateNetwork();
                            break;

                        case "Disk":
                            CreateDisk();
                            break;
                    }
                }
                else
                {
                    string machineName, categoryName, instanceName, counterName;
                    ParseCounterString(monitor.Attributes["value"].Value, out machineName, out categoryName, out instanceName, out counterName);
                    CreateSpecific(machineName, categoryName, counterName, instanceName);
                }
            }

            // Planification du rafraîchissement
            timer1.Interval = RefreshMilliSecondsInterval;
            timer1.Start();
            timer1_Tick(null, null);

            // Detection des changements de disques
            driveDetector = new Dolinay.DriveDetector();
            driveDetector.DeviceArrived += new Dolinay.DriveDetectorEventHandler(driveDetector_DeviceArrived);
            driveDetector.DeviceRemoved += new Dolinay.DriveDetectorEventHandler(driveDetector_DeviceRemoved);
        }

        /// <summary>
        /// Parsing de chaîne de compteur "\\<machineName>\<categoryName>(<instanceName>)\<counterName>"
        /// </summary>
        /// <param name="counterString"></param>
        /// <param name="machineName">can be "."</param>
        /// <param name="categoryName">can be empty if there is only one instance</param>
        /// <param name="instanceName"></param>
        /// <param name="counterName"></param>
        private void ParseCounterString(string counterString, out string machineName, out string categoryName, out string instanceName, out string counterName)
        {
            string[] detail = counterString.Substring(2).Split('\\');
            char[] separator = { '(', ')' };
            string[] detail2 = detail[1].Split(separator);

            machineName = detail[0];
            categoryName = detail2[0];
            counterName = detail[2];
            instanceName = detail2.Length >= 2 ? detail2[1] : null;
        }

        /// <summary>
        /// Création des compteurs disque
        /// </summary>
        private void CreateDisk()
        {
            System.Diagnostics.PerformanceCounterCategory category = new System.Diagnostics.PerformanceCounterCategory("Disque physique");
            foreach (string instance in category.GetInstanceNames().OrderBy(s => s))
            {
                if (instance == "_Total") { continue; }

                System.Diagnostics.PerformanceCounter pc = new System.Diagnostics.PerformanceCounter("Disque physique", "% durée d'inactivité", instance, true);
                UserControlCounter userControlCounter = AddCounterControl(new ReversePerformanceCounter(new PerformanceCounter(pc), new StaticPerformanceCounter(100)),
                    Color.MediumOrchid,
                    "userControlCounterDisk",
                    string.Format("Activité disque {0}", instance));
            }

#if false
            // Afficher disques physiques et paritions logiques
            ManagementObjectCollection partitions = new ManagementObjectSearcher(
            @"Select * From Win32_DiskPartition " +
            "Where DeviceID = 'Disk #0, Partition #0'").Get();

            foreach (ManagementObject partition in partitions)
            {
                ManagementObjectCollection diskDrives =
                new ManagementObjectSearcher
                ("ASSOCIATORS OF {Win32_DiskPartition.DeviceID='" +
                partition["DeviceID"] + "'} " +
                "WHERE AssocClass = Win32_DiskDriveToDiskPartition").Get();

                foreach (ManagementObject diskDrive in diskDrives)
                {
                    foreach (PropertyData property in diskDrive.Properties)
                    {
                        if (!property.IsArray)
                            Console.WriteLine("Disque {0} : {1}",
                            property.Name,
                            property.Value);
                    }
                }

            }
#endif
        }

        /// <summary>
        /// Suppression des compteurs disque
        /// </summary>
        private void DeleteDisk()
        {
            List<UserControlCounter> list = new List<UserControlCounter>(flowLayoutPanel.Controls.OfType<UserControlCounter>());
            foreach (UserControlCounter userControl in list)
            {
                if (userControl.Name == "userControlCounterDisk")
                {
                    flowLayoutPanel.Controls.Remove(userControl);
                }
            }
        }

        /// <summary>
        /// Création des compteurs mémoire physique
        /// </summary>
        private void CreatePhysicalMemory()
        {
            System.Diagnostics.PerformanceCounter pc = new System.Diagnostics.PerformanceCounter("Mémoire", "Octets disponibles", null, true);
            UserControlCounter userControlCounter = AddCounterControl(new ReversePerformanceCounter(new PerformanceCounter(pc), new MaxMemoryPerformanceCounter()),
                Color.RoyalBlue,
                "userControlCounterPhysicalMemory",
                "Mémoire physique");
            userControlCounter.CounterHistory.Counter.DisplayCoef = 1F / 1024F / 1024F / 1024F;
            userControlCounter.CounterHistory.Counter.Unit = "Go";
        }

        /// <summary>
        /// Création des compteurs mémoire virtuelle
        /// </summary>
        private void CreateVirtualMemory()
        {
            System.Diagnostics.PerformanceCounter pc = new System.Diagnostics.PerformanceCounter("Mémoire", "octets dédiés", null, true);
            System.Diagnostics.PerformanceCounter pcMax = new System.Diagnostics.PerformanceCounter("Mémoire", "limite de mémoire dédiée", null, true);

            UserControlCounter userControlCounter = AddCounterControl(
                new KnownMaxPerformanceCounter(new PerformanceCounter(pc), new PerformanceCounter(pcMax)),
                Color.LimeGreen,
                "userControlCounterPhysicalMemory",
                "Mémoire virtuelle");
            userControlCounter.CounterHistory.Counter.DisplayCoef = 1F / 1024F / 1024F / 1024F;
            userControlCounter.CounterHistory.Counter.Unit = "Go";
        }

        /// <summary>
        /// Création des compteurs CPU
        /// </summary>
        private void CreateCPU()
        {
            // Créer un compteur par CPU
            System.Diagnostics.PerformanceCounterCategory category = new System.Diagnostics.PerformanceCounterCategory("Processeur");
            SimpleCounter mainCounter = null;
            List<SimpleCounter> counters = new List<SimpleCounter>();
            foreach (string instance in category.GetInstanceNames().OrderBy(s => s))
            {
                System.Diagnostics.PerformanceCounter pc = new System.Diagnostics.PerformanceCounter("Processeur", "% Temps Processeur", instance, true);

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
            //counters.Add(new MostConsumingProcessPerformanceCounter());

            UserControlCounter userControlCounter = AddCounterControl(new SubPerformanceCounter(mainCounter, counters),
                Color.Goldenrod,
                "userControlCounterCPU",
                "CPU");
        }

        /// <summary>
        /// Création des compteurs réseau
        /// </summary>
        private void CreateNetwork()
        {
            // Créer les contrôles pour chaque interface réseau
            System.Diagnostics.PerformanceCounterCategory category = new System.Diagnostics.PerformanceCounterCategory("Interface réseau");
            List<SimpleCounter> counters = new List<SimpleCounter>();
            foreach (string instance in category.GetInstanceNames().OrderBy(s => s))
            {
                System.Diagnostics.PerformanceCounter pc = new System.Diagnostics.PerformanceCounter("Interface réseau", "Total des octets/s", instance, true);
                PerformanceCounter counter = new PerformanceCounter(pc);
                counters.Add(new PerformanceCounter(pc));
            }

            UserControlCounter userControlCounter = AddCounterControl(new SumPerformanceCounter(counters),
                Color.Gold,
                "userControlCounterNetwork",
                string.Format("Réseau ({0} interfaces)", counters.Count));
            userControlCounter.CounterHistory.Counter.DisplayCoef = 1F / 1024F;
            userControlCounter.CounterHistory.Counter.Unit = "Ko";
        }

        /// <summary>
        /// Création d'un compteur spécifique
        /// </summary>
        private void CreateSpecific(string machineName, string categoryName, string counterName, string instanceName)
        {
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
            foreach (System.Diagnostics.PerformanceCounter counter in counters)
            {
                AddCounterControl(new PerformanceCounter(counter),
                    Color.LightSteelBlue,
                    "userControlCounterSpecific",
                    string.Format(counter.CounterName));
            }
        }

        private UserControlCounter AddCounterControl(SimpleCounter counter, Color color, string name, string internalName)
        {
            UserControlCounter userControlCounter = new UserControlCounter();
            userControlCounter.CounterHistory = new CounterHistory(counter);
            userControlCounter.CounterHistory.Counter.Name = internalName;
            userControlCounter.BackColor = System.Drawing.Color.Transparent;
            userControlCounter.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            userControlCounter.Color = System.Drawing.Color.FromArgb(0, color);
            userControlCounter.ImeMode = System.Windows.Forms.ImeMode.Disable;
            userControlCounter.Location = new System.Drawing.Point(0, 21);
            userControlCounter.Margin = new System.Windows.Forms.Padding(0, 0, 1, 1);
            userControlCounter.Name = name;
            userControlCounter.Size = new System.Drawing.Size(40, 20);
            flowLayoutPanel.Controls.Add(userControlCounter);

            // Remonter les actions souris des contrôles à nous-mêmes
            userControlCounter.MouseDown += new MouseEventHandler(Monitor_MouseDown);
            userControlCounter.MouseMove += new MouseEventHandler(Monitor_MouseMove);
            userControlCounter.MouseUp += new MouseEventHandler(Monitor_MouseUp);

            return userControlCounter;
        }

        private new void Refresh()
        {
            DeleteDisk();
            CreateDisk();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            foreach (UserControlCounter counter in this.flowLayoutPanel.Controls.OfType<UserControlCounter>())
            {
                try
                {
                    counter.CounterHistory.Save();
                }
                catch (Exception ex)
                {
                    // TODO: implémenter le traitement de cette exception
                }

                counter.UpdateDisplay();
            }
        }

        private void Monitor_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                Capture = true;
                delta = PointToClient(MousePosition);
                this.Location = new Point(MousePosition.X - delta.X, MousePosition.Y - delta.Y);
            }
        }

        private void Monitor_MouseMove(object sender, MouseEventArgs e)
        {
            if (Capture)
            {
                this.Location = new Point(MousePosition.X - delta.X, MousePosition.Y - delta.Y);
            }
        }

        private void Monitor_MouseUp(object sender, MouseEventArgs e)
        {
            if (Capture)
            {
                this.Location = new Point(MousePosition.X - delta.X, MousePosition.Y - delta.Y);
                Capture = false;
            }
        }

        private void contextMenuStrip_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            // Fermer le formulaire si clic sur Quitter
            if (e.ClickedItem == toolStripMenuItemExit)
            {
                Close();
            }
            else if (e.ClickedItem == refreshToolStripMenuItem)
            {
                Refresh();
            }
            else if (e.ClickedItem == horizontalToolStripMenuItem)
            {
                this.flowLayoutPanel.FlowDirection = FlowDirection.LeftToRight;
                horizontalToolStripMenuItem.CheckState = CheckState.Checked;
                verticalToolStripMenuItem.CheckState = CheckState.Unchecked;
            }
            else if (e.ClickedItem == verticalToolStripMenuItem)
            {
                this.flowLayoutPanel.FlowDirection = FlowDirection.TopDown;
                verticalToolStripMenuItem.CheckState = CheckState.Checked;
                horizontalToolStripMenuItem.CheckState = CheckState.Unchecked;
            }
        }

        void driveDetector_DeviceArrived(object sender, Dolinay.DriveDetectorEventArgs e)
        {
            Refresh();
        }

        void driveDetector_DeviceRemoved(object sender, Dolinay.DriveDetectorEventArgs e)
        {
            Refresh();
        }
    }
}
