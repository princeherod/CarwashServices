using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

using CarwashServices.Auth;
using CarwashServices.Dtos;
using CarwashServices.Shell;

namespace CarwashServices.Roles.SuperAdmin
{
    /// <summary>
    /// Super Admin Module: Backup & Restore Data.
    /// Left card: "Create Manual Backup" with INSERT SQL statement code preview and POST /api/backups trigger.
    /// Right card: "Restore from Backup" listing successful backups with Auto/Manual pills, warning banner,
    /// and "Restore Selected Backup" button that POSTs to /api/backups/{id}/restore.
    /// </summary>
    public class BackupRestoreView : UserControl
    {
        // ================================================================
        //  Palette
        // ================================================================
        private static readonly Color Navy = Color.FromArgb(0x0A, 0x16, 0x33);
        private static readonly Color NavyHover = Color.FromArgb(0x16, 0x2A, 0x5C);
        private static readonly Color Muted = Color.FromArgb(0x6B, 0x7A, 0x9A);
        private static readonly Color PageBg = Color.FromArgb(0xF0, 0xF4, 0xFA);
        private static readonly Color CardBorder = Color.FromArgb(0xE1, 0xE7, 0xF0);
        private static readonly Color Blue = Color.FromArgb(0x02, 0x84, 0xC7);
        private static readonly Color BlueSoft = Color.FromArgb(0xE0, 0xF2, 0xFE);
        private static readonly Color Purple = Color.FromArgb(0x7C, 0x3A, 0xED);
        private static readonly Color PurpleSoft = Color.FromArgb(0xF3, 0xE8, 0xFF);
        private static readonly Color Green = Color.FromArgb(0x15, 0x80, 0x3D);
        private static readonly Color GreenSoft = Color.FromArgb(0xDC, 0xFC, 0xE7);
        private static readonly Color Amber = Color.FromArgb(0xB4, 0x53, 0x09);
        private static readonly Color AmberBg = Color.FromArgb(0xFF, 0xFB, 0xEB);
        private static readonly Color AmberBorder = Color.FromArgb(0xFD, 0xE6, 0x8A);
        private static readonly Color Danger = Color.FromArgb(0xDC, 0x26, 0x26);
        private static readonly Color DangerHover = Color.FromArgb(0xB9, 0x1C, 0x1C);
        private static readonly Color CodeBg = Color.FromArgb(0x0F, 0x17, 0x2A);

        // ================================================================
        //  HTTP
        // ================================================================
        private readonly HttpClient _http = new()
        {
            BaseAddress = new Uri("http://localhost:5180/"),
            Timeout = TimeSpan.FromSeconds(60)
        };

        // ================================================================
        //  Controls & State
        // ================================================================
        private Panel _scrollContainer = null!;
        private Panel _leftCard = null!;
        private Panel _rightCard = null!;

        // Left Card controls
        private Label _leftDescLbl = null!;
        private Panel _codeHeader = null!;
        private TextBox _sqlText = null!;
        private Label _targetInfo = null!;
        private Label _backupStatusLbl = null!;
        private Button _createBackupBtn = null!;

        // Right Card controls
        private Label _rightDescLbl = null!;
        private Panel _warnBanner = null!;
        private Panel _backupsListPanel = null!;
        private Label _restoreStatusLbl = null!;
        private Button _restoreBtn = null!;

        private BackupItemDto? _selectedBackup;
        private List<BackupItemDto> _successfulBackups = new();

        public BackupRestoreView()
        {
            Dock = DockStyle.Fill;
            BackColor = PageBg;
            Font = new Font("Segoe UI", 9.5f);
            DoubleBuffered = true;

            if (SessionUser.RoleId != 4)
            {
                Controls.Add(new AccessDeniedView("Backup & Restore Data", "Super Admin (Role 4)"));
                return;
            }

            InitializeComponent();
            Sidebar.EnableDoubleBuffering(this);

            Load += async (s, e) => await LoadBackupsAsync();
        }

        private void InitializeComponent()
        {
            SuspendLayout();

            _scrollContainer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = PageBg,
                AutoScroll = true,
                Padding = new Padding(36, 16, 36, 24)
            };
            Controls.Add(_scrollContainer);

            // ---- Breadcrumb ----
            _scrollContainer.Controls.Add(new Label
            {
                Text = "Super Admin Modules  ›  Backup & Restore Data",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9f),
                Location = new Point(36, 12),
                AutoSize = true,
                UseMnemonic = false
            });

            // ---- Title ----
            _scrollContainer.Controls.Add(new Label
            {
                Text = "Backup & Restore Data",
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 22f),
                Location = new Point(36, 34),
                AutoSize = true,
                UseMnemonic = false
            });

            // ---- Subtitle ----
            _scrollContainer.Controls.Add(new Label
            {
                Text = SuperAdminLabels.BackupRestoreDataSubtitle,
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9f),
                Location = new Point(36, 80),
                AutoSize = true,
                UseMnemonic = false
            });

            // ---- Cards ----
            _leftCard = CreateCardPanel();
            _rightCard = CreateCardPanel();

            _scrollContainer.Controls.Add(_leftCard);
            _scrollContainer.Controls.Add(_rightCard);

            RelayoutCards();

            BuildLeftCard();
            BuildRightCard();

            _leftCard.Resize += (s, e) => RelayoutLeftCard();
            _rightCard.Resize += (s, e) => RelayoutRightCard();

            RelayoutLeftCard();
            RelayoutRightCard();

            _scrollContainer.Resize += (s, e) => RelayoutCards();

            ResumeLayout(true);
        }

        private static Panel CreateCardPanel()
        {
            var p = new Panel
            {
                BackColor = Color.White
            };
            p.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = RoundedRect(new Rectangle(0, 0, p.Width - 1, p.Height - 1), 12);
                using var borderPen = new Pen(CardBorder, 1.2f);
                e.Graphics.DrawPath(borderPen, path);
            };
            return p;
        }

        private void RelayoutCards()
        {
            int top = 120;
            int padX = 36;
            int gap = 24;
            int availableWidth = Math.Max(700, _scrollContainer.ClientSize.Width - (padX * 2));
            int availableHeight = Math.Max(580, _scrollContainer.ClientSize.Height - top - 24);

            int cardWidth = (availableWidth - gap) / 2;

            _leftCard.SetBounds(padX, top, cardWidth, availableHeight);
            _rightCard.SetBounds(padX + cardWidth + gap, top, cardWidth, availableHeight);
        }

        // ================================================================
        //  LEFT CARD: Create Manual Backup
        // ================================================================
        private void BuildLeftCard()
        {
            _leftCard.SuspendLayout();

            // Card Title
            var titleLbl = new Label
            {
                Text = SuperAdminLabels.CardCreateBackupTitle,
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 14f),
                Location = new Point(24, 20),
                AutoSize = true,
                UseMnemonic = false
            };
            _leftCard.Controls.Add(titleLbl);

            _leftDescLbl = new Label
            {
                Text = SuperAdminLabels.CardCreateBackupDesc,
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9f),
                Location = new Point(24, 50),
                Size = new Size(Math.Max(100, _leftCard.Width - 48), 36),
                UseMnemonic = false
            };
            _leftCard.Controls.Add(_leftDescLbl);

            // Details Header
            _codeHeader = new Panel
            {
                Location = new Point(24, 92),
                Size = new Size(Math.Max(100, _leftCard.Width - 48), 32),
                BackColor = Color.FromArgb(0x1E, 0x29, 0x3B)
            };
            _codeHeader.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = RoundedRectTop(new Rectangle(0, 0, _codeHeader.Width - 1, _codeHeader.Height), 8);
                using var brush = new SolidBrush(Color.FromArgb(0x1E, 0x29, 0x3B));
                e.Graphics.FillPath(brush, path);
            };
            _codeHeader.Controls.Add(new Label
            {
                Text = SuperAdminLabels.BackupDetailsHeader,
                Font = new Font("Segoe UI Semibold", 8.5f),
                ForeColor = Color.FromArgb(0x94, 0xA3, 0xB8),
                Location = new Point(14, 8),
                AutoSize = true,
                UseMnemonic = false
            });
            _leftCard.Controls.Add(_codeHeader);

            // Details Overview Box
            _sqlText = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = CodeBg,
                ForeColor = Color.FromArgb(0x38, 0xBD, 0xF8),
                Font = new Font("Segoe UI", 9.2f),
                BorderStyle = BorderStyle.None,
                Location = new Point(24, 124),
                Size = new Size(Math.Max(100, _leftCard.Width - 48), 240),
                Text = string.Join("\r\n", new[]
                {
                    "BACKUP CONFIGURATION & SPECIFICATION",
                    "",
                    "Database Target:      MSME_MasterERP",
                    "Backup Mechanism:     On-demand snapshot to local storage",
                    "Archive File Type:    SQL Server Database Backup (.bak)",
                    "Compression:          Full verification enabled",
                    "",
                    "DESTINATION DIRECTORY",
                    "%USERPROFILE%\\CarwashBackups",
                    "",
                    "EXECUTION PROCEDURE",
                    "1. Initialize manual snapshot request",
                    "2. Write complete database state to timestamped archive",
                    "3. Verify archive integrity and record in Backup History"
                })
            };
            _leftCard.Controls.Add(_sqlText);

            // Storage Target Note
            _targetInfo = new Label
            {
                Text = SuperAdminLabels.DestinationDirectoryLabel,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Muted,
                Location = new Point(24, _leftCard.Height - 118),
                Size = new Size(Math.Max(100, _leftCard.Width - 48), 20),
                UseMnemonic = false
            };
            _leftCard.Controls.Add(_targetInfo);

            // Status label
            _backupStatusLbl = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Green,
                Location = new Point(24, _leftCard.Height - 94),
                Size = new Size(Math.Max(100, _leftCard.Width - 48), 24),
                UseMnemonic = false
            };
            _leftCard.Controls.Add(_backupStatusLbl);

            // "Create Manual Backup" Button
            _createBackupBtn = new Button
            {
                Text = SuperAdminLabels.ButtonCreateManualBackup,
                Font = new Font("Segoe UI Semibold", 10f),
                Size = new Size(Math.Max(100, _leftCard.Width - 48), 44),
                Location = new Point(24, _leftCard.Height - 68),
                BackColor = Navy,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            _createBackupBtn.FlatAppearance.BorderSize = 0;
            _createBackupBtn.FlatAppearance.MouseOverBackColor = NavyHover;
            _createBackupBtn.Click += async (s, e) => await CreateManualBackupAsync();
            _leftCard.Controls.Add(_createBackupBtn);

            _leftCard.ResumeLayout(true);
        }

        private void RelayoutLeftCard()
        {
            if (_leftCard == null || _sqlText == null) return;
            int w = Math.Max(100, _leftCard.ClientSize.Width - 48);
            int h = _leftCard.ClientSize.Height;

            _leftDescLbl.Width = w;
            _codeHeader.Width = w;
            _sqlText.Width = w;
            _sqlText.Height = Math.Max(140, h - 124 - 132);

            _targetInfo.SetBounds(24, h - 122, w, 20);
            _backupStatusLbl.SetBounds(24, h - 98, w, 24);
            _createBackupBtn.SetBounds(24, h - 68, w, 44);
        }

        // ================================================================
        //  RIGHT CARD: Restore from Backup
        // ================================================================
        private void BuildRightCard()
        {
            _rightCard.SuspendLayout();

            // Card Title
            var titleLbl = new Label
            {
                Text = SuperAdminLabels.CardRestoreTitle,
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 14f),
                Location = new Point(24, 20),
                AutoSize = true,
                UseMnemonic = false
            };
            _rightCard.Controls.Add(titleLbl);

            _rightDescLbl = new Label
            {
                Text = SuperAdminLabels.CardRestoreDesc,
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9f),
                Location = new Point(24, 50),
                Size = new Size(Math.Max(100, _rightCard.Width - 48), 36),
                UseMnemonic = false
            };
            _rightCard.Controls.Add(_rightDescLbl);

            // Warning Banner
            _warnBanner = new Panel
            {
                Location = new Point(24, 92),
                Size = new Size(Math.Max(100, _rightCard.Width - 48), 64),
                BackColor = AmberBg,
                Padding = new Padding(12)
            };
            _warnBanner.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = RoundedRect(new Rectangle(0, 0, _warnBanner.Width - 1, _warnBanner.Height - 1), 8);
                using var fill = new SolidBrush(AmberBg);
                e.Graphics.FillPath(fill, path);
                using var pen = new Pen(AmberBorder, 1.2f);
                e.Graphics.DrawPath(pen, path);

                // Warning Icon
                using var iconPen = new Pen(Amber, 1.8f);
                e.Graphics.DrawPolygon(iconPen, new PointF[]
                {
                    new(22, 16),
                    new(32, 34),
                    new(12, 34)
                });
                using var b = new SolidBrush(Amber);
                e.Graphics.FillRectangle(b, 21, 23, 2, 5);
                e.Graphics.FillEllipse(b, 21, 30, 2, 2);
            };

            var warnText = new Label
            {
                Text = SuperAdminLabels.RestoreWarningCaution,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Amber,
                Location = new Point(40, 10),
                Size = new Size(_warnBanner.Width - 52, 44),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                UseMnemonic = false
            };
            _warnBanner.Controls.Add(warnText);
            _rightCard.Controls.Add(_warnBanner);

            // Backups List Panel (Radio options)
            _backupsListPanel = new Panel
            {
                Location = new Point(24, 164),
                Size = new Size(Math.Max(100, _rightCard.Width - 48), Math.Max(120, _rightCard.Height - 164 - 100)),
                BackColor = Color.FromArgb(0xFA, 0xFB, 0xFD),
                AutoScroll = true
            };
            _backupsListPanel.Paint += (s, e) =>
            {
                using var pen = new Pen(CardBorder);
                e.Graphics.DrawRectangle(pen, 0, 0, _backupsListPanel.Width - 1, _backupsListPanel.Height - 1);
            };
            _rightCard.Controls.Add(_backupsListPanel);

            // Restore Status Label
            _restoreStatusLbl = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Green,
                Location = new Point(24, _rightCard.Height - 94),
                Size = new Size(Math.Max(100, _rightCard.Width - 48), 24),
                UseMnemonic = false
            };
            _rightCard.Controls.Add(_restoreStatusLbl);

            // "Restore Selected Backup" Button
            _restoreBtn = new Button
            {
                Text = SuperAdminLabels.ButtonRestoreSelectedBackup,
                Font = new Font("Segoe UI Semibold", 10f),
                Size = new Size(Math.Max(100, _rightCard.Width - 48), 44),
                Location = new Point(24, _rightCard.Height - 68),
                BackColor = Danger,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Enabled = false
            };
            _restoreBtn.FlatAppearance.BorderSize = 0;
            _restoreBtn.FlatAppearance.MouseOverBackColor = DangerHover;
            _restoreBtn.Click += async (s, e) => await RestoreSelectedBackupAsync();
            _rightCard.Controls.Add(_restoreBtn);

            _rightCard.ResumeLayout(true);
        }

        private void RelayoutRightCard()
        {
            if (_rightCard == null || _backupsListPanel == null) return;
            int w = Math.Max(100, _rightCard.ClientSize.Width - 48);
            int h = _rightCard.ClientSize.Height;

            _rightDescLbl.Width = w;
            _warnBanner.Width = w;
            _backupsListPanel.SetBounds(24, 164, w, Math.Max(120, h - 164 - 106));

            _restoreStatusLbl.SetBounds(24, h - 98, w, 24);
            _restoreBtn.SetBounds(24, h - 68, w, 44);

            PopulateBackupsList();
        }

        // ================================================================
        //  API CALLS & LOGIC
        // ================================================================
        public async Task LoadBackupsAsync()
        {
            try
            {
                Cursor = Cursors.WaitCursor;
                _restoreStatusLbl.Text = SuperAdminLabels.StatusLoadingBackups;
                _restoreStatusLbl.ForeColor = Muted;

                var backups = await _http.GetFromJsonAsync<List<BackupItemDto>>("api/backups?status=Success")
                              ?? new List<BackupItemDto>();

                _successfulBackups = backups;
                _restoreStatusLbl.Text = "";
                PopulateBackupsList();
            }
            catch (Exception ex)
            {
                _restoreStatusLbl.ForeColor = Danger;
                _restoreStatusLbl.Text = $"Failed to load backups: {ex.Message}";
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void PopulateBackupsList()
        {
            if (_backupsListPanel == null) return;
            _backupsListPanel.SuspendLayout();
            _backupsListPanel.Controls.Clear();
            _selectedBackup = null;
            _restoreBtn.Enabled = false;

            if (_successfulBackups.Count == 0)
            {
                var emptyLbl = new Label
                {
                    Text = SuperAdminLabels.StatusNoBackupsFound,
                    ForeColor = Muted,
                    Font = new Font("Segoe UI", 9.5f),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Dock = DockStyle.Fill,
                    UseMnemonic = false
                };
                _backupsListPanel.Controls.Add(emptyLbl);
                _backupsListPanel.ResumeLayout(true);
                return;
            }

            int y = 8;
            int itemW = Math.Max(200, _backupsListPanel.ClientSize.Width - 16);

            foreach (var b in _successfulBackups)
            {
                var rowPanel = CreateBackupRow(b, itemW, y);
                _backupsListPanel.Controls.Add(rowPanel);
                y += rowPanel.Height + 8;
            }

            _backupsListPanel.ResumeLayout(true);
        }

        private Panel CreateBackupRow(BackupItemDto item, int width, int y)
        {
            var row = new Panel
            {
                Location = new Point(8, y),
                Size = new Size(width, 72),
                BackColor = Color.White,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            bool isSelected = _selectedBackup?.BackupId == item.BackupId;

            row.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = RoundedRect(new Rectangle(0, 0, row.Width - 1, row.Height - 1), 8);
                using var bgBrush = new SolidBrush(isSelected ? Color.FromArgb(0xEE, 0xF2, 0xFF) : Color.White);
                e.Graphics.FillPath(bgBrush, path);
                using var pen = new Pen(isSelected ? Purple : CardBorder, isSelected ? 1.5f : 1f);
                e.Graphics.DrawPath(pen, path);
            };

            // Radio Button
            var rb = new RadioButton
            {
                Location = new Point(14, 24),
                Size = new Size(20, 24),
                Checked = isSelected,
                Cursor = Cursors.Hand
            };

            // Pill: Auto / Manual
            bool isAuto = string.Equals(item.Type, "Auto", StringComparison.OrdinalIgnoreCase);
            var pillText = isAuto ? SuperAdminLabels.BackupTypeAuto : SuperAdminLabels.BackupTypeManual;
            var pillBg = isAuto ? BlueSoft : PurpleSoft;
            var pillFg = isAuto ? Blue : Purple;

            var pill = new Label
            {
                Text = pillText,
                Font = new Font("Segoe UI Semibold", 8.5f),
                ForeColor = pillFg,
                BackColor = pillBg,
                Location = new Point(42, 14),
                Padding = new Padding(8, 2, 8, 2),
                AutoSize = true,
                UseMnemonic = false
            };
            void RoundPill()
            {
                using var p = RoundedRect(new Rectangle(0, 0, pill.Width, pill.Height), pill.Height / 2);
                pill.Region = new Region(p);
            }
            pill.SizeChanged += (s, e) => RoundPill();
            RoundPill();

            // File Name
            var nameLbl = new Label
            {
                Text = item.FileName,
                Font = new Font("Segoe UI Semibold", 9.5f),
                ForeColor = Navy,
                Location = new Point(pill.Right + 10, 14),
                Size = new Size(Math.Max(50, row.Width - pill.Right - 20), 20),
                AutoEllipsis = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                UseMnemonic = false
            };

            // Subtitle: Date & Size & User
            var infoLbl = new Label
            {
                Text = $"Date Created: {item.BackupDate:yyyy-MM-dd HH:mm:ss} UTC  ·  {item.FileSizeFormatted}  ·  Performed By: {item.PerformedByName}",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Muted,
                Location = new Point(42, 40),
                Size = new Size(Math.Max(50, row.Width - 54), 18),
                AutoEllipsis = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                UseMnemonic = false
            };

            void SelectRow()
            {
                _selectedBackup = item;
                _restoreBtn.Enabled = true;
                _restoreStatusLbl.Text = "";

                // Uncheck other rows
                foreach (Control c in _backupsListPanel.Controls)
                {
                    if (c is Panel p)
                    {
                        foreach (Control sc in p.Controls)
                        {
                            if (sc is RadioButton otherRb) otherRb.Checked = (c == row);
                        }
                        p.Invalidate();
                    }
                }
            }

            row.Click += (s, e) => SelectRow();
            rb.Click += (s, e) => SelectRow();
            pill.Click += (s, e) => SelectRow();
            nameLbl.Click += (s, e) => SelectRow();
            infoLbl.Click += (s, e) => SelectRow();

            row.Controls.Add(rb);
            row.Controls.Add(pill);
            row.Controls.Add(nameLbl);
            row.Controls.Add(infoLbl);

            return row;
        }

        private async Task CreateManualBackupAsync()
        {
            _backupStatusLbl.Text = "Executing backup and inserting into BACKUP_LOGS...";
            _backupStatusLbl.ForeColor = Muted;
            _createBackupBtn.Enabled = false;
            Cursor = Cursors.WaitCursor;

            try
            {
                var payload = new CreateBackupRequest
                {
                    PerformedBy = SessionUser.UserId > 0 ? SessionUser.UserId : 39,
                    Type = "Manual"
                };

                using var resp = await _http.PostAsJsonAsync("api/backups", payload);
                if (!resp.IsSuccessStatusCode)
                {
                    var err = await resp.Content.ReadAsStringAsync();
                    _backupStatusLbl.ForeColor = Danger;
                    _backupStatusLbl.Text = $"Backup failed: {err}";
                    return;
                }

                var result = await resp.Content.ReadFromJsonAsync<CreateBackupResponse>();
                _backupStatusLbl.ForeColor = Green;
                _backupStatusLbl.Text = $"✓ Backup created: {result?.FileName} (Status: Success)";

                // Reload restore list to show newly created backup
                await LoadBackupsAsync();
            }
            catch (Exception ex)
            {
                _backupStatusLbl.ForeColor = Danger;
                _backupStatusLbl.Text = $"Error: {ex.Message}";
            }
            finally
            {
                _createBackupBtn.Enabled = true;
                Cursor = Cursors.Default;
            }
        }

        private async Task RestoreSelectedBackupAsync()
        {
            if (_selectedBackup == null) return;

            var confirm = MessageBox.Show(
                $"Are you sure you want to restore the database from backup:\n\n" +
                $"File: {_selectedBackup.FileName}\n" +
                $"Date: {_selectedBackup.BackupDate:yyyy-MM-dd HH:mm:ss} UTC\n\n" +
                "WARNING: This will overwrite all existing data in MSME_MasterERP!",
                "Confirm Database Restore",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (confirm != DialogResult.Yes) return;

            _restoreStatusLbl.Text = "Restoring database snapshot... Please wait.";
            _restoreStatusLbl.ForeColor = Amber;
            _restoreBtn.Enabled = false;
            Cursor = Cursors.WaitCursor;

            try
            {
                using var resp = await _http.PostAsync($"api/backups/{_selectedBackup.BackupId}/restore", null);
                if (!resp.IsSuccessStatusCode)
                {
                    var err = await resp.Content.ReadAsStringAsync();
                    _restoreStatusLbl.ForeColor = Danger;
                    _restoreStatusLbl.Text = $"Restore failed: {err}";
                    return;
                }

                _restoreStatusLbl.ForeColor = Green;
                _restoreStatusLbl.Text = $"✓ Database successfully restored from {_selectedBackup.FileName}!";

                MessageBox.Show(
                    $"Database 'MSME_MasterERP' has been successfully restored to snapshot:\n\n{_selectedBackup.FileName}",
                    "Restore Completed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                await LoadBackupsAsync();
            }
            catch (Exception ex)
            {
                _restoreStatusLbl.ForeColor = Danger;
                _restoreStatusLbl.Text = $"Restore error: {ex.Message}";
            }
            finally
            {
                _restoreBtn.Enabled = _selectedBackup != null;
                Cursor = Cursors.Default;
            }
        }

        private static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            int d = radius * 2;
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        private static GraphicsPath RoundedRectTop(Rectangle r, int radius)
        {
            int d = radius * 2;
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddLine(r.Right, r.Bottom, r.X, r.Bottom);
            p.CloseFigure();
            return p;
        }
    }
}
