using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WindowsMonitorLib.Counters
{
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
