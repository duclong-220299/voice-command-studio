using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace VoiceCommandApp
{
    public class MainForm : Form
    {
        // ── Win32 keyboard hook ──────────────────────────────────────────
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        private const int HOTKEY_PRESS = 1;
        private const int HOTKEY_RELEASE = 2;
        private const int WM_HOTKEY = 0x0312;

        // ── Data ─────────────────────────────────────────────────────────
        private VoiceCommandDatabase db;
        private AudioRecorder recorder;
        private bool isTestMode = false;
        private bool isRecordingCtrl = false;
        private float[] lastTestSamples;
        private string currentTrainingCommand = null;
        private int currentTrainingSlot = 0;

        // ── UI Controls ──────────────────────────────────────────────────
        private Panel headerPanel;
        private TabControl mainTab;
        private TabPage trainingTab, testTab;
        private Panel[] commandPanels = new Panel[3];
        private Label[] statusLabels = new Label[3];
        private Label[] countLabels = new Label[3];
        private ProgressBar[] progressBars = new ProgressBar[3];
        private WaveformControl trainingWaveform;
        private WaveformControl testWaveform;
        private Label testStatusLabel;
        private Panel resultPanel;
        private Label resultLabel;
        private Label resultEmoji;
        private Label ctrlHintLabel;
        private Panel bottomBar;
        private Label logLabel;
        private Button saveBtn;

        public MainForm()
        {
            InitializeComponent();
            InitDB();
            InitAudio();
            UpdateAllStatus();
        }

        private void InitDB()
        {
            db = new VoiceCommandDatabase();
            bool loaded = db.Load();
            if (loaded) Log("✅ Đã tải dữ liệu từ file mã hóa.");
            else Log("📂 Chưa có dữ liệu, bắt đầu thu âm mới.");
        }

        private void InitAudio()
        {
            try
            {
                recorder = new AudioRecorder();
                recorder.WaveformData += (s, samples) =>
                {
                    BeginInvoke(new Action(() =>
                    {
                        if (isRecordingCtrl && testWaveform != null)
                            testWaveform.AddSamples(samples);
                        else if (!isTestMode && trainingWaveform != null)
                            trainingWaveform.AddSamples(samples);
                    }));
                };

                recorder.RecordingComplete += (s, samples) =>
                {
                    BeginInvoke(new Action(() =>
                    {
                        if (isTestMode)
                            ProcessTestSamples(samples);
                        else if (currentTrainingCommand != null)
                            ProcessTrainingSamples(samples);
                    }));
                };

                Log($"🎙 Microphone: {AudioRecorder.GetDeviceName(0)}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi microphone:\n{ex.Message}", "Lỗi",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void InitializeComponent()
        {
            Text = "Voice Command Studio";
            Size = new Size(860, 680);
            MinimumSize = new Size(860, 680);
            BackColor = Color.FromArgb(14, 14, 22);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9);
            StartPosition = FormStartPosition.CenterScreen;
            Icon = SystemIcons.Application;

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.DoubleBuffer, true);

            BuildHeader();
            BuildTabs();
            BuildBottomBar();

            FormClosing += (s, e) =>
            {
                UnregisterHotKey(Handle, HOTKEY_PRESS);
                recorder?.Dispose();
            };
        }

        private void BuildHeader()
        {
            headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 64,
                BackColor = Color.FromArgb(18, 18, 30)
            };

            var titleLabel = new Label
            {
                Text = "🎙 Voice Command Studio",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(20, 14)
            };
            headerPanel.Controls.Add(titleLabel);

            var subLabel = new Label
            {
                Text = "Hệ thống thu âm & nhận dạng khẩu lệnh tiếng Việt",
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(120, 120, 150),
                AutoSize = true,
                Location = new Point(22, 40)
            };
            headerPanel.Controls.Add(subLabel);

            // Version badge
            var badge = new Label
            {
                Text = "DTW + MFCC",
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 144, 255),
                BackColor = Color.FromArgb(20, 30, 144, 255),
                AutoSize = true,
                Padding = new Padding(8, 4, 8, 4)
            };
            headerPanel.Controls.Add(badge);
            headerPanel.Paint += (s, e) =>
            {
                badge.Location = new Point(headerPanel.Width - badge.Width - 20, 22);
            };

            Controls.Add(headerPanel);
        }

        private void BuildTabs()
        {
            mainTab = new TabControl
            {
                Location = new Point(10, 74),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                DrawMode = TabDrawMode.OwnerDrawFixed,
                ItemSize = new Size(160, 36),
                SizeMode = TabSizeMode.Fixed,
            };
            mainTab.Size = new Size(Width - 20, Height - 160);
            mainTab.Resize += (s, e) => { };

            mainTab.DrawItem += OnDrawTabItem;

            trainingTab = new TabPage("📚  Thu Âm Huấn Luyện") { BackColor = Color.FromArgb(14, 14, 22) };
            testTab = new TabPage("🔊  Kiểm Tra Nhận Dạng") { BackColor = Color.FromArgb(14, 14, 22) };

            BuildTrainingTab();
            BuildTestTab();

            mainTab.TabPages.Add(trainingTab);
            mainTab.TabPages.Add(testTab);
            Controls.Add(mainTab);

            mainTab.Selected += (s, e) =>
            {
                isTestMode = mainTab.SelectedTab == testTab;
                if (isTestMode)
                {
                    RegisterHotKey(Handle, HOTKEY_PRESS, 0, (uint)Keys.ControlKey);
                    Log("🎯 Chế độ kiểm tra: Giữ Ctrl để nhận dạng giọng nói.");
                }
                else
                {
                    UnregisterHotKey(Handle, HOTKEY_PRESS);
                }
            };
        }

        private void OnDrawTabItem(object sender, DrawItemEventArgs e)
        {
            bool selected = e.Index == mainTab.SelectedIndex;
            var bg = selected ? Color.FromArgb(24, 24, 38) : Color.FromArgb(16, 16, 26);
            var fg = selected ? Color.White : Color.FromArgb(120, 120, 150);

            using (var brush = new SolidBrush(bg))
                e.Graphics.FillRectangle(brush, e.Bounds);

            if (selected)
            {
                using (var accentBrush = new SolidBrush(Color.FromArgb(30, 144, 255)))
                    e.Graphics.FillRectangle(accentBrush,
                        e.Bounds.X, e.Bounds.Bottom - 2, e.Bounds.Width, 2);
            }

            using (var textBrush = new SolidBrush(fg))
            {
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                e.Graphics.DrawString(mainTab.TabPages[e.Index].Text, new Font("Segoe UI", 9, FontStyle.Bold),
                    textBrush, e.Bounds, sf);
            }
        }

        private void BuildTrainingTab()
        {
            var scroll = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.FromArgb(14, 14, 22)
            };
            trainingTab.Controls.Add(scroll);

            var infoLabel = new Label
            {
                Text = "Thu âm mỗi khẩu lệnh 3 lần để tạo mẫu nhận dạng. Nhấn \"Bắt đầu\" rồi nói rõ ràng.",
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(140, 140, 160),
                Location = new Point(16, 12),
                AutoSize = true
            };
            scroll.Controls.Add(infoLabel);

            // Waveform
            trainingWaveform = new WaveformControl
            {
                Location = new Point(16, 38),
                Size = new Size(810, 70),
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
            };
            scroll.Controls.Add(trainingWaveform);

            // Command panels
            int yOff = 120;
            for (int i = 0; i < 3; i++)
            {
                var panel = BuildCommandPanel(i, new Point(16, yOff));
                scroll.Controls.Add(panel);
                commandPanels[i] = panel;
                yOff += 150;
            }

            // Save button
            saveBtn = new Button
            {
                Text = "💾  Lưu & Mã Hóa Dữ Liệu",
                Location = new Point(16, yOff + 10),
                Size = new Size(220, 40),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 120, 60),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            saveBtn.FlatAppearance.BorderSize = 0;
            saveBtn.Click += SaveData;
            scroll.Controls.Add(saveBtn);

            var clearBtn = new Button
            {
                Text = "🗑  Xóa Tất Cả",
                Location = new Point(250, yOff + 10),
                Size = new Size(140, 40),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(80, 20, 20),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10),
                Cursor = Cursors.Hand
            };
            clearBtn.FlatAppearance.BorderSize = 0;
            clearBtn.Click += ClearAllData;
            scroll.Controls.Add(clearBtn);
        }

        private Panel BuildCommandPanel(int idx, Point location)
        {
            var cmdName = VoiceCommandDatabase.CommandNames[idx];
            var cmdLabel = VoiceCommandDatabase.CommandLabels[idx];
            var emoji = VoiceCommandDatabase.CommandEmojis[idx];
            var colorStr = VoiceCommandDatabase.CommandColors[idx];
            var color = ColorTranslator.FromHtml(colorStr);

            var panel = new Panel
            {
                Location = location,
                Size = new Size(820, 135),
                BackColor = Color.FromArgb(20, 20, 32),
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
            };

            // Accent left bar
            var accent = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(4, 135),
                BackColor = color
            };
            panel.Controls.Add(accent);

            // Emoji label
            var emojiLabel = new Label
            {
                Text = emoji,
                Font = new Font("Segoe UI Emoji", 22),
                Location = new Point(14, 14),
                AutoSize = true,
                ForeColor = Color.White
            };
            panel.Controls.Add(emojiLabel);

            // Command name
            var nameLabel = new Label
            {
                Text = cmdLabel,
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                Location = new Point(65, 14),
                AutoSize = true,
                ForeColor = color
            };
            panel.Controls.Add(nameLabel);

            // Status label
            statusLabels[idx] = new Label
            {
                Text = "Chưa có mẫu",
                Font = new Font("Segoe UI", 9),
                Location = new Point(66, 44),
                AutoSize = true,
                ForeColor = Color.FromArgb(120, 120, 150)
            };
            panel.Controls.Add(statusLabels[idx]);

            // Count
            countLabels[idx] = new Label
            {
                Text = "0 / 3",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Location = new Point(66, 64),
                AutoSize = true,
                ForeColor = Color.White
            };
            panel.Controls.Add(countLabels[idx]);

            // Progress
            progressBars[idx] = new ProgressBar
            {
                Location = new Point(66, 88),
                Size = new Size(200, 8),
                Maximum = 3,
                Minimum = 0,
                Value = 0,
                Style = ProgressBarStyle.Continuous,
                ForeColor = color,
                BackColor = Color.FromArgb(30, 30, 45)
            };
            panel.Controls.Add(progressBars[idx]);

            // Record buttons
            int btnX = 300;
            for (int slot = 0; slot < 3; slot++)
            {
                int s = slot;
                var recBtn = new Button
                {
                    Text = $"⏺  Mẫu {slot + 1}",
                    Location = new Point(btnX + slot * 165, 50),
                    Size = new Size(150, 42),
                    Tag = $"{cmdName}|{slot}",
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(25, 25, 40),
                    ForeColor = Color.FromArgb(180, 180, 210),
                    Font = new Font("Segoe UI", 9, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                recBtn.FlatAppearance.BorderColor = Color.FromArgb(50, 50, 70);
                recBtn.FlatAppearance.BorderSize = 1;
                recBtn.MouseDown += RecordBtnMouseDown;
                recBtn.MouseUp += RecordBtnMouseUp;
                panel.Controls.Add(recBtn);

                // Slot label
                var slotLabel = new Label
                {
                    Text = "Giữ để ghi",
                    Font = new Font("Segoe UI", 7),
                    ForeColor = Color.FromArgb(80, 80, 100),
                    Location = new Point(btnX + slot * 165, 96),
                    AutoSize = true
                };
                panel.Controls.Add(slotLabel);
            }

            return panel;
        }

        private void RecordBtnMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            var btn = (Button)sender;
            var parts = btn.Tag.ToString().Split('|');
            currentTrainingCommand = parts[0];
            currentTrainingSlot = int.Parse(parts[1]);

            btn.BackColor = Color.FromArgb(0, 80, 160);
            btn.Text = "⏹  Đang ghi...";
            trainingWaveform.IsActive = true;
            trainingWaveform.Clear();
            recorder?.StartRecording();
            Log($"⏺  Đang thu âm: {GetLabel(currentTrainingCommand)}, mẫu {currentTrainingSlot + 1}...");
        }

        private void RecordBtnMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            var btn = (Button)sender;
            btn.BackColor = Color.FromArgb(0, 120, 50);
            btn.Text = "✅  Đã ghi";
            trainingWaveform.IsActive = false;
            recorder?.StopRecording();
        }

        private void ProcessTrainingSamples(float[] samples)
        {
            try
            {
                if (samples.Length < 800)
                {
                    Log("⚠️  Mẫu quá ngắn, vui lòng ghi lại.");
                    return;
                }

                float[] processed = MFCCExtractor.Normalize(samples);
                processed = MFCCExtractor.TrimSilence(processed);
                double[][] mfcc = MFCCExtractor.Extract(processed);

                var template = db.Templates[currentTrainingCommand];

                // Replace slot or add
                if (currentTrainingSlot < template.MFCCRecordings.Count)
                    template.MFCCRecordings[currentTrainingSlot] = mfcc;
                else
                    template.AddRecording(mfcc);

                UpdateAllStatus();
                Log($"✅  Đã lưu mẫu {currentTrainingSlot + 1} cho \"{GetLabel(currentTrainingCommand)}\". ({mfcc.Length} frames MFCC)");
            }
            catch (Exception ex)
            {
                Log($"❌  Lỗi xử lý: {ex.Message}");
            }
            finally
            {
                currentTrainingCommand = null;
            }
        }

        private void BuildTestTab()
        {
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(14, 14, 22) };
            testTab.Controls.Add(panel);

            // Instruction
            var instrPanel = new Panel
            {
                Location = new Point(16, 14),
                Size = new Size(810, 90),
                BackColor = Color.FromArgb(20, 30, 50)
            };
            panel.Controls.Add(instrPanel);

            ctrlHintLabel = new Label
            {
                Text = "Giữ Ctrl để nói",
                Font = new Font("Segoe UI", 22, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 144, 255),
                Location = new Point(20, 10),
                AutoSize = true
            };
            instrPanel.Controls.Add(ctrlHintLabel);

            var hintSub = new Label
            {
                Text = "Nhấn và giữ phím Ctrl bất kỳ lúc nào để bắt đầu nhận dạng giọng nói. Thả ra để xử lý.",
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(120, 140, 180),
                Location = new Point(22, 50),
                AutoSize = true
            };
            instrPanel.Controls.Add(hintSub);

            // Waveform
            var waveLabel = new Label
            {
                Text = "TÍN HIỆU ÂM THANH",
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                ForeColor = Color.FromArgb(80, 80, 100),
                Location = new Point(16, 116),
                AutoSize = true
            };
            panel.Controls.Add(waveLabel);

            testWaveform = new WaveformControl
            {
                Location = new Point(16, 136),
                Size = new Size(810, 90),
                WaveColor = Color.FromArgb(50, 220, 130)
            };
            panel.Controls.Add(testWaveform);

            // Status
            testStatusLabel = new Label
            {
                Text = "Sẵn sàng — Giữ Ctrl để nói",
                Font = new Font("Segoe UI", 12),
                ForeColor = Color.FromArgb(140, 140, 180),
                Location = new Point(16, 240),
                AutoSize = true
            };
            panel.Controls.Add(testStatusLabel);

            // Result panel
            resultPanel = new Panel
            {
                Location = new Point(16, 278),
                Size = new Size(810, 120),
                BackColor = Color.FromArgb(18, 22, 35),
                Visible = false
            };
            panel.Controls.Add(resultPanel);

            resultEmoji = new Label
            {
                Text = "",
                Font = new Font("Segoe UI Emoji", 36),
                Location = new Point(20, 16),
                AutoSize = true,
                ForeColor = Color.White
            };
            resultPanel.Controls.Add(resultEmoji);

            resultLabel = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 28, FontStyle.Bold),
                Location = new Point(90, 20),
                AutoSize = true,
                ForeColor = Color.White
            };
            resultPanel.Controls.Add(resultLabel);

            var resultSub = new Label
            {
                Text = "Khẩu lệnh nhận diện được",
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(100, 100, 130),
                Location = new Point(92, 72),
                AutoSize = true
            };
            resultPanel.Controls.Add(resultSub);

            // Score breakdown
            BuildScorePanel(panel);

            // Registered commands list
            BuildRegisteredList(panel);
        }

        private Panel scorePanel;
        private Label[] scoreLabels = new Label[3];

        private void BuildScorePanel(Panel parent)
        {
            var title = new Label
            {
                Text = "ĐỘ TƯƠNG ĐỒNG",
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                ForeColor = Color.FromArgb(80, 80, 100),
                Location = new Point(16, 415),
                AutoSize = true
            };
            parent.Controls.Add(title);

            scorePanel = new Panel
            {
                Location = new Point(16, 434),
                Size = new Size(810, 80),
                BackColor = Color.Transparent
            };
            parent.Controls.Add(scorePanel);

            for (int i = 0; i < 3; i++)
            {
                var color = ColorTranslator.FromHtml(VoiceCommandDatabase.CommandColors[i]);
                var lbl = VoiceCommandDatabase.CommandLabels[i];

                var card = new Panel
                {
                    Location = new Point(i * 270, 0),
                    Size = new Size(256, 75),
                    BackColor = Color.FromArgb(20, 20, 32)
                };

                var accent = new Panel
                {
                    Location = new Point(0, 0),
                    Size = new Size(3, 75),
                    BackColor = color
                };
                card.Controls.Add(accent);

                var cmdLbl = new Label
                {
                    Text = $"{VoiceCommandDatabase.CommandEmojis[i]}  {lbl}",
                    Font = new Font("Segoe UI", 9, FontStyle.Bold),
                    Location = new Point(12, 10),
                    AutoSize = true,
                    ForeColor = Color.FromArgb(200, 200, 220)
                };
                card.Controls.Add(cmdLbl);

                scoreLabels[i] = new Label
                {
                    Text = "—",
                    Font = new Font("Segoe UI", 14, FontStyle.Bold),
                    Location = new Point(12, 36),
                    AutoSize = true,
                    ForeColor = color
                };
                card.Controls.Add(scoreLabels[i]);

                scorePanel.Controls.Add(card);
            }
        }

        private void BuildRegisteredList(Panel parent)
        {
            var title = new Label
            {
                Text = "TRẠNG THÁI MẪU",
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                ForeColor = Color.FromArgb(80, 80, 100),
                Location = new Point(16, 530),
                AutoSize = true
            };
            parent.Controls.Add(title);

            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                var color = ColorTranslator.FromHtml(VoiceCommandDatabase.CommandColors[i]);

                var row = new Panel
                {
                    Location = new Point(16 + i * 270, 552),
                    Size = new Size(256, 40),
                    BackColor = Color.FromArgb(18, 18, 28)
                };

                var dot = new Panel
                {
                    Location = new Point(10, 14),
                    Size = new Size(12, 12),
                    BackColor = color,
                    Tag = $"dot_{i}"
                };
                MakeCircle(dot);
                row.Controls.Add(dot);

                var lbl = new Label
                {
                    Text = $"{VoiceCommandDatabase.CommandLabels[i]}",
                    Font = new Font("Segoe UI", 9, FontStyle.Bold),
                    Location = new Point(30, 10),
                    AutoSize = true,
                    ForeColor = Color.White,
                    Tag = $"name_{i}"
                };
                row.Controls.Add(lbl);

                parent.Controls.Add(row);
            }
        }

        private void MakeCircle(Panel p)
        {
            p.Region = new Region(new System.Drawing.Drawing2D.GraphicsPath());
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddEllipse(0, 0, p.Width, p.Height);
            p.Region = new Region(path);
        }

        private void BuildBottomBar()
        {
            bottomBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 36,
                BackColor = Color.FromArgb(10, 10, 18)
            };

            logLabel = new Label
            {
                Text = "Khởi động...",
                Font = new Font("Segoe UI", 8),
                ForeColor = Color.FromArgb(100, 100, 130),
                Location = new Point(12, 10),
                AutoSize = true
            };
            bottomBar.Controls.Add(logLabel);

            Controls.Add(bottomBar);
        }

        private void Log(string message)
        {
            if (InvokeRequired) { BeginInvoke(new Action(() => Log(message))); return; }
            logLabel.Text = $"[{DateTime.Now:HH:mm:ss}] {message}";
        }

        private void UpdateAllStatus()
        {
            for (int i = 0; i < 3; i++)
            {
                var cmd = VoiceCommandDatabase.CommandNames[i];
                var template = db.Templates[cmd];
                int count = template.RecordingCount;

                countLabels[i].Text = $"{count} / 3";
                progressBars[i].Value = Math.Min(count, 3);

                if (count == 0)
                    statusLabels[i].Text = "⚠️  Chưa có mẫu nào";
                else if (count < 3)
                    statusLabels[i].Text = $"⏳  Cần thêm {3 - count} mẫu nữa";
                else
                    statusLabels[i].Text = "✅  Đã đủ mẫu — sẵn sàng nhận dạng";

                var color = ColorTranslator.FromHtml(VoiceCommandDatabase.CommandColors[i]);
                statusLabels[i].ForeColor = count >= 3 ? Color.FromArgb(50, 200, 100) : Color.FromArgb(200, 140, 40);
            }
        }

        private void SaveData(object sender, EventArgs e)
        {
            try
            {
                db.Save();
                Log("💾  Dữ liệu đã được mã hóa AES-256 và lưu thành công.");
                MessageBox.Show("Dữ liệu đã được mã hóa AES-256 và lưu thành công!",
                    "Đã Lưu", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Log($"❌  Lỗi lưu: {ex.Message}");
                MessageBox.Show($"Lỗi khi lưu:\n{ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ClearAllData(object sender, EventArgs e)
        {
            if (MessageBox.Show("Xóa toàn bộ dữ liệu thu âm?", "Xác Nhận",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

            foreach (var t in db.Templates.Values)
                t.ClearRecordings();

            UpdateAllStatus();
            Log("🗑  Đã xóa toàn bộ dữ liệu mẫu.");
        }

        // ── Hotkey handling ──────────────────────────────────────────────
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_PRESS)
            {
                // Ctrl key down
                if (!isRecordingCtrl && isTestMode)
                {
                    isRecordingCtrl = true;
                    testWaveform.IsActive = true;
                    testWaveform.Clear();
                    testStatusLabel.Text = "🔴  Đang nghe... Thả Ctrl để nhận dạng";
                    testStatusLabel.ForeColor = Color.FromArgb(255, 80, 80);
                    ctrlHintLabel.Text = "🔴  Đang ghi âm...";
                    ctrlHintLabel.ForeColor = Color.FromArgb(255, 80, 80);
                    resultPanel.Visible = false;
                    recorder?.StartRecording();
                    Log("🎙  Đang nghe giọng nói...");
                }
            }
            base.WndProc(ref m);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // Detect Ctrl key release via KeyUp in form
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);
            if ((e.KeyCode == Keys.ControlKey || e.KeyCode == Keys.LControlKey || e.KeyCode == Keys.RControlKey)
                && isRecordingCtrl && isTestMode)
            {
                StopTestRecording();
            }
        }

        private void StopTestRecording()
        {
            isRecordingCtrl = false;
            testWaveform.IsActive = false;
            testStatusLabel.Text = "⏳  Đang phân tích...";
            testStatusLabel.ForeColor = Color.FromArgb(255, 180, 30);
            ctrlHintLabel.Text = "Giữ Ctrl để nói";
            ctrlHintLabel.ForeColor = Color.FromArgb(30, 144, 255);
            recorder?.StopRecording();
        }

        // Also catch Ctrl release via low-level approach using Application.Idle
        private bool ctrlWasDown = false;
        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            Application.Idle += CheckCtrlReleased;
        }
        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            Application.Idle -= CheckCtrlReleased;
        }
        private void CheckCtrlReleased(object sender, EventArgs e)
        {
            bool ctrlDown = (Control.ModifierKeys & Keys.Control) != 0;
            if (ctrlWasDown && !ctrlDown && isRecordingCtrl && isTestMode)
            {
                StopTestRecording();
            }
            ctrlWasDown = ctrlDown;
        }

        private void ProcessTestSamples(float[] samples)
        {
            try
            {
                if (samples == null || samples.Length < 800)
                {
                    testStatusLabel.Text = "⚠️  Không nghe thấy — thử lại!";
                    testStatusLabel.ForeColor = Color.FromArgb(255, 140, 30);
                    Log("⚠️  Mẫu quá ngắn hoặc không có âm thanh.");
                    return;
                }

                float[] processed = MFCCExtractor.Normalize(samples);
                processed = MFCCExtractor.TrimSilence(processed);
                double[][] queryMFCC = MFCCExtractor.Extract(processed);

                // Match against all templates
                string bestCmd = null;
                double bestScore = double.MaxValue;
                double[] scores = new double[3];

                for (int i = 0; i < VoiceCommandDatabase.CommandNames.Length; i++)
                {
                    var cmd = VoiceCommandDatabase.CommandNames[i];
                    var template = db.Templates[cmd];

                    if (template.RecordingCount == 0)
                    {
                        scores[i] = double.MaxValue;
                        continue;
                    }

                    double dist = template.MatchScore(queryMFCC);
                    scores[i] = dist;

                    if (dist < bestScore)
                    {
                        bestScore = dist;
                        bestCmd = cmd;
                    }
                }

                // Update score labels
                double minScore = double.MaxValue, maxScore = 0;
                for (int i = 0; i < 3; i++)
                    if (scores[i] != double.MaxValue)
                    {
                        if (scores[i] < minScore) minScore = scores[i];
                        if (scores[i] > maxScore) maxScore = scores[i];
                    }

                for (int i = 0; i < 3; i++)
                {
                    if (scores[i] == double.MaxValue)
                        scoreLabels[i].Text = "N/A";
                    else
                    {
                        // Convert to similarity %
                        double norm = maxScore > minScore
                            ? 100.0 - (scores[i] - minScore) / (maxScore - minScore) * 80
                            : 100.0;
                        scoreLabels[i].Text = $"{norm:F0}%";
                    }
                }

                // Threshold check
                const double threshold = 80.0;
                bool confident = bestScore < threshold;

                if (bestCmd != null && confident)
                {
                    ShowResult(bestCmd);
                }
                else if (bestCmd != null)
                {
                    testStatusLabel.Text = "❓  Không nhận ra — nói rõ hơn hoặc ghi thêm mẫu";
                    testStatusLabel.ForeColor = Color.FromArgb(200, 140, 40);
                    resultPanel.Visible = false;
                    Log($"❓  Điểm thấp nhất: {bestScore:F2} — không đủ tin cậy.");
                }
                else
                {
                    testStatusLabel.Text = "⚠️  Chưa có mẫu — hãy thu âm trước";
                    testStatusLabel.ForeColor = Color.FromArgb(255, 80, 80);
                    resultPanel.Visible = false;
                }
            }
            catch (Exception ex)
            {
                testStatusLabel.Text = "❌  Lỗi phân tích";
                testStatusLabel.ForeColor = Color.Red;
                Log($"❌  Lỗi nhận dạng: {ex.Message}");
            }
        }

        private void ShowResult(string cmdName)
        {
            int idx = Array.IndexOf(VoiceCommandDatabase.CommandNames, cmdName);
            var label = VoiceCommandDatabase.CommandLabels[idx];
            var emoji = VoiceCommandDatabase.CommandEmojis[idx];
            var color = ColorTranslator.FromHtml(VoiceCommandDatabase.CommandColors[idx]);

            resultLabel.Text = label;
            resultLabel.ForeColor = color;
            resultEmoji.Text = emoji;
            resultPanel.Visible = true;

            testStatusLabel.Text = $"✅  Đã nhận ra: {label}";
            testStatusLabel.ForeColor = color;

            Log($"🎯  Nhận dạng: \"{label}\"");

            // Show popup
            var popup = new ResultPopupForm(label, emoji, color);
            popup.Show(this);
        }

        private string GetLabel(string cmdName)
        {
            int idx = Array.IndexOf(VoiceCommandDatabase.CommandNames, cmdName);
            return idx >= 0 ? VoiceCommandDatabase.CommandLabels[idx] : cmdName;
        }
    }
}