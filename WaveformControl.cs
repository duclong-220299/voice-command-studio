using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace VoiceCommandApp
{
    public class WaveformControl : Control
    {
        private Queue<float> samples = new Queue<float>();
        private const int MaxSamples = 400;
        private bool isActive = false;
        private Color waveColor = Color.FromArgb(30, 144, 255);
        private Color bgColor = Color.FromArgb(12, 12, 20);
        private float[] displaySamples = new float[0];
        private float peakLevel = 0;
        private System.Windows.Forms.Timer decayTimer;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color WaveColor
        {
            get => waveColor;
            set { waveColor = value; Invalidate(); }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IsActive
        {
            get => isActive;
            set { isActive = value; Invalidate(); }
        }

        public WaveformControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.DoubleBuffer, true);
            BackColor = bgColor;

            decayTimer = new System.Windows.Forms.Timer { Interval = 50 };
            decayTimer.Tick += (s, e) =>
            {
                peakLevel *= 0.9f;
                Invalidate();
            };
            decayTimer.Start();
        }

        public void AddSamples(float[] newSamples)
        {
            if (newSamples == null) return;

            // Downsample for display
            int step = Math.Max(1, newSamples.Length / 40);
            for (int i = 0; i < newSamples.Length; i += step)
            {
                samples.Enqueue(newSamples[i]);
                if (samples.Count > MaxSamples)
                    samples.Dequeue();

                float abs = Math.Abs(newSamples[i]);
                if (abs > peakLevel) peakLevel = abs;
            }

            displaySamples = samples.ToArray();
            Invalidate();
        }

        public void Clear()
        {
            samples.Clear();
            displaySamples = new float[0];
            peakLevel = 0;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Background
            g.Clear(bgColor);

            // Grid lines
            using (var gridPen = new Pen(Color.FromArgb(25, 255, 255, 255), 1))
            {
                for (int i = 1; i < 4; i++)
                {
                    int y = Height * i / 4;
                    g.DrawLine(gridPen, 0, y, Width, y);
                }
                for (int i = 1; i < 8; i++)
                {
                    int x = Width * i / 8;
                    g.DrawLine(gridPen, x, 0, x, Height);
                }
            }

            // Center line
            using (var centerPen = new Pen(Color.FromArgb(40, 255, 255, 255), 1))
                g.DrawLine(centerPen, 0, Height / 2, Width, Height / 2);

            int centerY = Height / 2;

            if (displaySamples.Length > 1)
            {
                // Filled waveform
                var topPoints = new List<PointF>();
                var botPoints = new List<PointF>();

                topPoints.Add(new PointF(0, centerY));
                botPoints.Add(new PointF(0, centerY));

                for (int i = 0; i < displaySamples.Length; i++)
                {
                    float x = (float)i / displaySamples.Length * Width;
                    float amp = displaySamples[i];
                    float y = centerY - amp * (centerY - 4);

                    topPoints.Add(new PointF(x, y));
                    botPoints.Add(new PointF(x, centerY + (amp * (centerY - 4))));
                }

                topPoints.Add(new PointF(Width, centerY));
                botPoints.Add(new PointF(Width, centerY));

                // Draw filled area
                if (isActive)
                {
                    var fillPoints = new List<PointF>(topPoints);
                    var reversed = new List<PointF>(botPoints);
                    reversed.Reverse();
                    fillPoints.AddRange(reversed);

                    if (fillPoints.Count > 2)
                    {
                        using (var fillBrush = new LinearGradientBrush(
                            new Point(0, 0), new Point(0, Height),
                            Color.FromArgb(60, waveColor), Color.FromArgb(20, waveColor)))
                        {
                            g.FillPolygon(fillBrush, fillPoints.ToArray());
                        }
                    }

                    // Draw wave line
                    if (topPoints.Count > 1)
                    {
                        using (var wavePen = new Pen(waveColor, 1.5f))
                        {
                            wavePen.LineJoin = LineJoin.Round;
                            g.DrawLines(wavePen, topPoints.ToArray());
                        }
                    }
                }
                else
                {
                    // Inactive - grey flat
                    using (var flatPen = new Pen(Color.FromArgb(50, 100, 100, 120), 1.5f))
                        g.DrawLine(flatPen, 0, centerY, Width, centerY);
                }
            }
            else
            {
                // No data - flat line
                Color lineColor = isActive
                    ? Color.FromArgb(80, waveColor)
                    : Color.FromArgb(50, 100, 100, 120);

                using (var linePen = new Pen(lineColor, 1.5f))
                    g.DrawLine(linePen, 0, centerY, Width, centerY);
            }

            // Peak level indicator
            if (isActive && peakLevel > 0.01f)
            {
                int barHeight = (int)(peakLevel * (Height - 4));
                int barY = Height / 2 - barHeight / 2;
                using (var peakBrush = new SolidBrush(Color.FromArgb(80, waveColor)))
                    g.FillRectangle(peakBrush, Width - 8, barY, 4, barHeight);
            }

            // Border
            using (var borderPen = new Pen(isActive
                ? Color.FromArgb(80, waveColor)
                : Color.FromArgb(40, 60, 60, 80), 1))
                g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);

            // Status text
            if (!isActive && displaySamples.Length == 0)
            {
                using (var font = new Font("Segoe UI", 8))
                using (var brush = new SolidBrush(Color.FromArgb(60, 180, 180, 200)))
                {
                    var text = "Chưa có tín hiệu";
                    var size = g.MeasureString(text, font);
                    g.DrawString(text, font, brush,
                        (Width - size.Width) / 2, (Height - size.Height) / 2);
                }
            }
        }
    }
}