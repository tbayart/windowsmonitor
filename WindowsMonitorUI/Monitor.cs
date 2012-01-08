using System;
using System.Collections.Generic;
using System.Configuration;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
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
			InitializeCountersFromSettings();

			// Automate refresh
			timer1.Interval = RefreshMilliSecondsInterval;
			timer1.Start();
			timer1_Tick(null, null);

			// Activate physical disk change detection
			driveDetector = new Dolinay.DriveDetector();
			driveDetector.DeviceArrived += new Dolinay.DriveDetectorEventHandler(driveDetector_DeviceArrived);
			driveDetector.DeviceRemoved += new Dolinay.DriveDetectorEventHandler(driveDetector_DeviceRemoved);
		}

		/// <summary>
		/// Build counters by reading settings file
		/// </summary>
		private void InitializeCountersFromSettings()
		{
			System.Xml.XmlDocument doc = new System.Xml.XmlDocument();
			doc.Load(ConfigurationManager.AppSettings["settingsFile"]);

			// Création des compteurs
			foreach (System.Xml.XmlNode monitor in doc.SelectNodes("/Monitor/*"))
			{
				switch (monitor.Attributes["type"].Value)
				{
					case "predefined":
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
								
							case "CPUFrequency":
								CreateCPUFrequency();
								break;
								
							default:
								throw new Exception(String.Format("value {0} unknown", monitor.Attributes["value"].Value));
						}
						break;
					case "perfmon":
						CreateSpecific(monitor.Attributes["value"].Value);
						break;

					default:
						throw new Exception(String.Format("type {0} unknown", monitor.Attributes["type"].Value));
				}
			}
		}

		/// <summary>
		/// Création des compteurs disque
		/// </summary>
		private void CreateDisk()
		{
			foreach (SimpleCounter counter in WindowsMonitorLib.Counters.CounterFactory.CreatePhysicalDiskCounters())
			{
				AddCounterControl(counter,
				                  Color.MediumOrchid,
				                  "userControlCounterDisk",
				                  string.Format("Disk activity {0}", counter.Name));
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
			UserControlCounter userControlCounter = AddCounterControl(CounterFactory.CreatePhysicalMemoryCounter(),
			                                                          Color.RoyalBlue,
			                                                          "userControlCounterPhysicalMemory",
			                                                          "Physical Memory");
			userControlCounter.CounterHistory.Counter.DisplayCoef = 1F / 1024F / 1024F / 1024F;
			userControlCounter.CounterHistory.Counter.Unit = "Go";
		}

		/// <summary>
		/// Création des compteurs mémoire virtuelle
		/// </summary>
		private void CreateVirtualMemory()
		{
			UserControlCounter userControlCounter = AddCounterControl(
				CounterFactory.CreateVirtualMemoryCounter(),
				Color.LimeGreen,
				"userControlCounterVirtualMemory",
				"Virtual Memory");
			userControlCounter.CounterHistory.Counter.DisplayCoef = 1F / 1024F / 1024F / 1024F;
			userControlCounter.CounterHistory.Counter.Unit = "Go";
		}

		/// <summary>
		/// Création des compteurs CPU
		/// </summary>
		private void CreateCPU()
		{
			AddCounterControl(CounterFactory.CreateCPUCounter(),
			                  Color.Goldenrod,
			                  "userControlCounterCPU",
			                  "CPU");
		}

		/// <summary>
		/// Création des compteurs CPU
		/// </summary>
		private void CreateCPUFrequency()
		{
			AddCounterControl(CounterFactory.CreateCPUFrequencyCounter(),
			                  Color.Goldenrod,
			                  "userControlCounterCPUFrequency",
			                  "CPUFrequency");
		}

		/// <summary>
		/// Création des compteurs réseau
		/// </summary>
		private void CreateNetwork()
		{
			UserControlCounter userControlCounter = AddCounterControl(CounterFactory.CreateNetworkCounter(),
			                                                          Color.Gold,
			                                                          "userControlCounterNetwork",
			                                                          "Network");
			userControlCounter.CounterHistory.Counter.DisplayCoef = 1F / 1024F;
			userControlCounter.CounterHistory.Counter.Unit = "Ko";
		}

		/// <summary>
		/// Création d'un compteur spécifique
		/// </summary>
		private void CreateSpecific(string counterString)
		{
			// Création des contrôles
			foreach (System.Diagnostics.PerformanceCounter counter in CounterFactory.CreateCountersFromString(counterString))
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

		private void RefreshDisk()
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
					// TODO
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
				RefreshDisk();
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
			RefreshDisk();
		}

		void driveDetector_DeviceRemoved(object sender, Dolinay.DriveDetectorEventArgs e)
		{
			RefreshDisk();
		}
	}
}
