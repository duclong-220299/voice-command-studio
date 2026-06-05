using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace VoiceCommandApp
{
    public class ResultPopupForm : Form
    {
        private string commandLabel;
        private string emoji;
        private Color accentColor;
        private System.Windows.Forms.Timer closeTimer;
        private int countdown = 3;
        private Label countdownLabel;
        private float animAlpha = 0f;
        private System.Windows.Forms.Timer animTimer;

        public ResultPopupForm(string commandLabel, string emoji, Color accentColor)
        {
            this.commandLabel = commandLabel;
            this.emoji = emoji;
            this.accentColor = accentColor;
            InitializeComponent();
            StartAnimation();
        }

        private void InitializeComponent()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Size = new Size(380, 220);
            BackColor = Color.FromArgb(18, 18, 24);
            TopMost = true;
            Opacity = 0;
            ShowInTaskbar = false;

            // Position at top-right of screen
            var screen = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(screen.Right - Width - 20, screen.Top + 20);

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.DoubleBuffer, true);

            // Countdown label
            countdownLabel = new Label
            {
                Text = "3",
                Font = new Font("Segoe UI", 9, FontStyle.Regular),
                ForeColor = Color.FromArgb(120, 120, 140),
                AutoSize = true
            };
            Controls.Add(countdownLabel);

            // Close timer
            closeTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            closeTimer.Tick += (s, e) =>
            {
                countdown--;
                countdownLabel.Text = countdown.ToString();
                if (countdown <= 0)
                {
                    closeTimer.Stop();
                    FadeOut();
                }
            };
        }

        private void StartAnimation()
        {
            animTimer = new System.Windows.Forms.Timer { Interval = 16 };
            animTimer.Tick += (s, e) =>
            {
                animAlpha += 0.08f;
                if (animAlpha >= 1f)
                {
                    animAlpha = 1f;
                    animTimer.Stop();
                    closeTimer.Start();
                }
                Opacity = animAlpha;
                Invalidate();
            };
            animTimer.Start();
        }

        private void FadeOut()
        {
            var fadeTimer = new System.Windows.Forms.Timer { Interval = 16 };
            fadeTimer.Tick += (s, e) =>
            {
                Opacity -= 0.06;
                if (Opacity <= 0)
                {
                    fadeTimer.Stop();
                    Close();
                }
            };
            fadeTimer.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Background with rounded corners
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = RoundedRect(rect, 16))
            {
                using (var bg = new SolidBrush(Color.FromArgb(22, 22, 32)))
                    g.FillPath(bg, path);

                // Accent border
                using (var border = new Pen(Color.FromArgb(60, accentColor), 1.5f))
                    g.DrawPath(border, path);
            }

            // Glow effect at top
            using (var glowBrush = new LinearGradientBrush(
                new Point(0, 0), new Point(0, 60),
                Color.FromArgb(40, accentColor), Color.Transparent))
            {
                g.FillRectangle(glowBrush, 0, 0, Width, 60);
            }

            // "Khẩu lệnh nhận diện" header
            using (var headerFont = new Font("Segoe UI", 9, FontStyle.Regular))
            using (var headerBrush = new SolidBrush(Color.FromArgb(140, 140, 160)))
                g.DrawString("🎙 Nhận diện khẩu lệnh", headerFont, headerBrush, new PointF(20, 18));

            // Emoji
            using (var emojiFont = new Font("Segoe UI Emoji", 32))
            using (var emojiBrush = new SolidBrush(Color.White))
                g.DrawString(emoji, emojiFont, emojiBrush, new PointF(20, 50));

            // Command text
            using (var cmdFont = new Font("Segoe UI", 28, FontStyle.Bold))
            using (var cmdBrush = new SolidBrush(accentColor))
                g.DrawString(commandLabel, cmdFont, cmdBrush, new PointF(85, 50));

            // Divider
            using (var divPen = new Pen(Color.FromArgb(40, 40, 55), 1))
                g.DrawLine(divPen, 20, 140, Width - 20, 140);

            // Instruction
            using (var infoFont = new Font("Segoe UI", 9))
            using (var infoBrush = new SolidBrush(Color.FromArgb(100, 100, 120)))
                g.DrawString("Tự động đóng sau vài giây...", infoFont, infoBrush, new PointF(20, 153));

            // Countdown position
            if (countdownLabel != null)
                countdownLabel.Location = new Point(Width - 35, 158);

            // Pulse indicator
            float pulse = (float)(0.5 + 0.5 * Math.Sin(DateTime.Now.Ticks / 2000000.0));
            int indicatorSize = 10;
            using (var indicBrush = new SolidBrush(Color.FromArgb((int)(255 * pulse), accentColor)))
                g.FillEllipse(indicBrush, Width - 28, 14, indicatorSize, indicatorSize);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            closeTimer.Stop();
            FadeOut();
        }

        private GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, radius * 2, radius * 2, 180, 90);
            path.AddArc(bounds.Right - radius * 2, bounds.Y, radius * 2, radius * 2, 270, 90);
            path.AddArc(bounds.Right - radius * 2, bounds.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
            path.CloseFigure();
            return path;
        }

        // Repaint for animation
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            var pulseTimer = new System.Windows.Forms.Timer { Interval = 50 };
            pulseTimer.Tick += (s, ev) => Invalidate();
            pulseTimer.Start();
            FormClosed += (s, ev) => pulseTimer.Stop();
        }
    }
}