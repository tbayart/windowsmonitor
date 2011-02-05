using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using WindowsMonitorLib.Counters;

namespace WindowsMonitorUI
{
    public partial class UserControlCounter : UserControl
    {
        private CounterHistory counterHistory;
        private System.Drawing.Color baseColor;
        private System.Drawing.Pen penBorder;
        private System.Drawing.Brush brushBackground;
        private System.Drawing.Brush brushHisto;
        private System.Drawing.Pen penAxis;
        private bool autoHide = false;
        private const int CurrentWidth = 5;
        private int x;
        private const int AxisWidth = 10;
        private const int Decimals = 1;
        private const float WarningThreshold = 0.8F;
        private const float AlertThreshold = 0.9F;

        public CounterHistory CounterHistory
        {
            get { return counterHistory; }
            set { counterHistory = value; }
        }

        public System.Drawing.Color Color
        {
            get { return baseColor; }
            set
            {
                baseColor = value;
                penBorder = new Pen(ControlPaint.Light(baseColor, -0.4F));
                brushBackground = new SolidBrush(ControlPaint.Light(baseColor, -0.2F));
                brushHisto = new SolidBrush(ControlPaint.Light(baseColor, 1.1F));
                penAxis = new System.Drawing.Pen(ControlPaint.Light(baseColor, 0.1F));
            }
        }

        public override string Text
        {
            get {
                if (counterHistory.Counter.Unit == "%" && counterHistory.Counter.DisplayCoef == 1F && counterHistory.Counter.Max == 100F)
                {
                    return String.Format("{0}%", Math.Round(counterHistory[0], Decimals));
                }
                else
                {
                    return String.Format("{0}{2} / {1}{2}", Math.Round(counterHistory[0] * counterHistory.Counter.DisplayCoef, Decimals), Math.Round(counterHistory.Max * counterHistory.Counter.DisplayCoef, Decimals), counterHistory.Counter.Unit);
                }
            }
        }

        public bool AutoHide
        {
            get { return autoHide; }
            set { autoHide = value; }
        }

        public UserControlCounter()
        {
            InitializeComponent();

            Color = Color.Aqua;

            if (DesignMode)
            {
                counterHistory = new CounterHistory(new SimpleCounter());
            }

        }

        public void UpdateDisplay()
        {
            x--;

            try
            {
                toolTip.ToolTipTitle = counterHistory.Counter.Name;
                toolTip.SetToolTip(this, counterHistory.Counter.Help + "\n" + Text);
            }
            catch (Exception ex)
            {
                toolTip.ToolTipTitle = CounterHistory.Counter.Name;
                toolTip.SetToolTip(this, "Erreur:\n" + ex.Message);
            }

            // Cacher si max nul
            if (autoHide)
            {
                Visible = (counterHistory.RealMax != 0F);
            }

            Invalidate();
        }

        private void UserControlCounter_Paint(object sender, PaintEventArgs e)
        {
            float max;

            if (DesignMode)
            {
                max = 100;
            }
            else
            {
                max = CounterHistory.Max;
            }

            // Dessiner le fond
            e.Graphics.FillRectangle(brushBackground,
                0,
                0,
                Width - 1,
                Height - 1);

            // Dessiner l'axe des ordonnées
            for (int i = x % AxisWidth; i < Width; i += AxisWidth)
            {
                e.Graphics.DrawLine(penAxis, i, 0, i, Height);
            }

            // Dessiner l'axe des abscisses
            for (int i = Height - AxisWidth; i > 0; i -= AxisWidth)
            {
                e.Graphics.DrawLine(penAxis, 0, i, Width, i);
            }

            // Dessiner l'histogramme de l'historique
            for (int i = 0; i < (counterHistory != null ? counterHistory.Count : 0); i++)
            {
                float data;

                if (DesignMode)
                {
                    data = 0.5F;
                }
                else
                {
                    data = counterHistory[i] / max;
                }

                // Borner les valeurs si les compteurs déliraient un peu
                if (data < 0) { data = 0; }
                if (data > 1) { data = 1; }

                // Couleur du pinceau de l'histogramme en fonction du niveau
                Brush brush;
                if (i == 0)
                {
                    if (data >= AlertThreshold) { brush = Brushes.Red; }
                    else if (data >= WarningThreshold) { brush = Brushes.Orange; }
                    else { brush = brushHisto; }
                }
                else
                {
                    //if (data >= AlertThreshold) { brush = Brushes.Red; }
                    //else if (data >= WarningThreshold) { brush = Brushes.Orange; }
                    //else
                    { brush = brushHisto; }
                }

                // Dessiner l'histogramme de la valeur courante
                if (!System.Single.IsNaN(data))
                {
                    e.Graphics.FillRectangle(brush,
                        Width - 1 - CurrentWidth - i,
                        Height - (int)Math.Round((float)(Height - 1) * data),
                        i == 0 ? CurrentWidth : 1,
                        (int)Math.Round((float)(Height - 1) * data));
                }
            }

            // Dessiner la bordure
            e.Graphics.DrawRectangle(penBorder,
                0,
                0,
                Width - 1,
                Height - 1);
        }

        private void UserControlCounter_Load(object sender, EventArgs e)
        {
            this.BackColor = System.Drawing.Color.Transparent;
        }
    }
}
