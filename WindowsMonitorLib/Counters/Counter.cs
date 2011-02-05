using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WindowsMonitorLib.Counters
{
    /// <summary>
    /// Compteur qui fourni une valeur et un maximum lorsqu'on lui demande
    /// </summary>
    public abstract class Counter
    {
        /// <summary>
        /// Fourni ou défini le nom "fonctionnel" du compteur
        /// </summary>
        public abstract string Name
        {
            get;
            set;
        }

        /// <summary>
        /// Fourni une aide "technique" sur le compteur
        /// </summary>
        public abstract string Help
        {
            get;
        }

        /// <summary>
        /// Fourni ou défini l'unité
        /// </summary>
        public abstract string Unit
        {
            get;
            set;
        }

        /// <summary>
        /// Fourni ou défini le coefficient multiplicateur à l'affichage
        /// </summary>
        public abstract float DisplayCoef
        {
            get;
            set;
        }

        /// <summary>
        /// Retourne la valeur courante du compteur
        /// </summary>
        public abstract float Value
        {
            get;
        }

        /// <summary>
        /// Retourne la valeur maximum du compteur
        /// </summary>
        public abstract float Max
        {
            get;
        }
    }

    /// <summary>
    /// Compteur qui implémente le stockage de la propriété Name
    /// </summary>
    public class SimpleCounter : Counter
    {
        private string name; // Nom du compteur
        private string unit = "%"; // Unité
        private float displayCoef = 1F; // Coefficient multiplicateur

        public override string Name
        {
            get { return name; }
            set { name = value; }
        }

        public override string Unit
        {
            get { return unit; }
            set { unit = value; }
        }

        public override float DisplayCoef
        {
            get { return displayCoef; }
            set { displayCoef = value; }
        }

        public override string Help
        {
            get { throw new NotImplementedException(); }
        }

        public override float Value
        {
            get { throw new NotImplementedException(); }
        }

        public override float Max
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>
        /// Fourni la représentation sous forme de chaine du nom d'un compteur de performance
        /// </summary>
        /// <param name="pc">Compteur de performance</param>
        /// <returns>nom du compteur de performance</returns>
        protected static string CounterName(System.Diagnostics.PerformanceCounter pc)
        {
            StringBuilder name = new StringBuilder();

            name.AppendFormat("\\\\{0}\\{1}", pc.MachineName, pc.CategoryName);
            if (pc.InstanceName != null)
            {
                name.AppendFormat("({0})", pc.InstanceName);
            }
            name.AppendFormat("\\{0}", pc.CounterName);

            return name.ToString();
        }
    }

    /// <summary>
    /// Compteur de performance
    /// </summary>
    public class PerformanceCounter : SimpleCounter
    {
        private System.Diagnostics.PerformanceCounter pc; // Compteur de performances système
        private float max; // valeur max

        public PerformanceCounter(System.Diagnostics.PerformanceCounter pc)
        {
            this.pc = pc;
        }

        public override string Help
        {
            get { return CounterName(pc); }
        }

        public override float Value
        {
            get {
                float value = pc.NextValue();
                max = Math.Max(max, value);
                return value;
            }
        }

        public override float Max
        {
            get { return max; }
        }
    }

    /// <summary>
    /// Compteur de performance dont le maximum est défini par un compteur
    /// </summary>
    public class KnownMaxPerformanceCounter : SimpleCounter
    {
        private Counter pc;
        private Counter pcMax;

        public KnownMaxPerformanceCounter(Counter pc, Counter pcMax)
        {
            this.pc = pc;
            this.pcMax = pcMax;
        }

        public override string Help
        {
            get { return pc.Help + " / " + pcMax.Help; }
        }

        public override float Value
        {
            get { return pc.Value; }
        }

        public override float Max
        {
            get { return pcMax.Value; }
        }
    }

    /// <summary>
    /// Compteur de performance qui donne la quantité de mémoire physique
    /// </summary>
    public class MaxMemoryPerformanceCounter : SimpleCounter
    {
        float memory;

        public MaxMemoryPerformanceCounter()
        {
            Microsoft.VisualBasic.Devices.ComputerInfo ci = new Microsoft.VisualBasic.Devices.ComputerInfo();
            memory = ci.TotalPhysicalMemory;
        }

        public override string Help
        {
            get { return "Physical memory"; }
        }

        public override float Value
        {
            get { return memory; }
        }
    }

    /// <summary>
    /// Compteur de performance ayant une valeur fixe
    /// </summary>
    public class StaticPerformanceCounter : SimpleCounter
    {
        float value;

        public StaticPerformanceCounter(float value)
        {
            this.value = value;
        }

        public override string Help
        {
            get { return "Static value"; }
        }

        public override float Value
        {
            get { return value; }
        }
    }

    /// <summary>
    /// Compteur de performance inversé: la valeur retournée est la différence en le maximum et la valeur brute du compteur de performance
    /// </summary>
    public class ReversePerformanceCounter : SimpleCounter
    {
        private Counter pc;
        private Counter pcMax;

        public ReversePerformanceCounter(Counter pc, Counter pcMax)
        {
            this.pc = pc;
            this.pcMax = pcMax;
        }

        public override string Help
        {
            get { return pc.Help + " / " + pcMax.Help; }
        }

        public override float Value
        {
            get { return pcMax.Value - pc.Value; }
        }

        public override float Max
        {
            get { return pcMax.Value; }
        }
    }

    /// <summary>
    /// Compteur de performance qui fait la somme d'autres compteurs
    /// </summary>
    public class SumPerformanceCounter : SimpleCounter
    {
        private List<SimpleCounter> counters;

        public SumPerformanceCounter(List<SimpleCounter> counters)
        {
            this.counters = counters;
        }

        public override string Help
        {
            get {
                StringBuilder sb = new StringBuilder();
                foreach (SimpleCounter counter in counters)
                {
                    if (sb.Length > 0) { sb.AppendLine(); }
                    sb.Append(counter.Help);
                }

                return sb.ToString();
            }
        }

        public override float Value
        {
            get { return counters.Sum(s => s.Value); }
        }

        public override float Max
        {
            get { return counters.Sum(s => s.Max); }
        }
    }

    /// <summary>
    /// Compteur de performance qui inclus des sous compteurs
    /// </summary>
    public class SubPerformanceCounter : SimpleCounter
    {
        private SimpleCounter counter;
        private List<SimpleCounter> subCounters;

        public SubPerformanceCounter(SimpleCounter counter, List<SimpleCounter> subCounters)
        {
            this.counter = counter;
            this.subCounters = subCounters;
        }

        public override string Help
        {
            get
            {
                StringBuilder sb = new StringBuilder();
                foreach (SimpleCounter counter in subCounters)
                {
                    if (sb.Length > 0) { sb.AppendLine(); }
                    sb.AppendFormat("{0}: {1}{2}", counter.Help, Math.Round(counter.Value * counter.DisplayCoef, 1), counter.Unit);
                }

                return sb.ToString();
            }
        }

        public override float Value
        {
            get { return counter.Value ; }
        }

        public override float Max
        {
            get { return counter.Max; }
        }
    }

    /// <summary>
    /// Compteur de performance qui mesure le processus le plus consommateur
    /// /// </summary>
    public class MostConsumingProcessPerformanceCounter : SimpleCounter
    {
        public MostConsumingProcessPerformanceCounter()
        {
        }

        public override string Help
        {
            get {
                return "";
            }
        }

        public override float Value
        {
            get { return 0; }
        }

        public override float Max
        {
            get { return 0; }
        }
    }

    /// <summary>
    /// Stocke l'historique des valeurs d'un compteur
    /// </summary>
    public class CounterHistory
    {
        private List<float> values;
        private float realMax;
        private Counter counter;

        private const int HistorySize = 100;

        public CounterHistory(Counter counter)
        {
            this.counter = counter;
            values = new List<float>(HistorySize);
        }

        public Counter Counter
        {
            get { return counter; }
            set { counter = value; }
        }

        public void Save()
        {
            values.Insert(0, counter.Value);
            realMax = Math.Max(realMax, values.Max());

            if (values.Count > HistorySize)
            {
                values.RemoveAt(values.Count - 1);
            }
        }

        public int Count
        {
            get { return values.Count; }
        }

		public float this[int index]
        {
	        get { return values[index]; }
        }

        public float Max
        {
            get { return Math.Max(counter.Max, realMax); }
        }

        public float RealMax
        {
            get { return realMax; }
        }
    }
}
