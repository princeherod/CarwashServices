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
using CarwashServices.Dialogs;
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
        private static readonly Color DeepNavy = Color.FromArgb(0x0C, 0x4A, 0x6E);
        private static readonly Color DeepNavySoft = Color.FromArgb(0xEA, 0xF2, 0xFD);
        private static readonly Color SlateBlue = Color.FromArgb(0x47, 0x55, 0x69);
        private static readonly Color SlateBlueSoft = Color.FromArgb(0xF1, 0xF5, 0xF9);
        private static readonly Color RoyalBlue = Color.FromArgb(0x1D, 0x4E, 0xD8);
        private static readonly Color RoyalBlueHover = Color.FromArgb(0x1E, 0x40, 0xAF);
        private static readonly Color CodeBg = Color.FromArgb(0x0F, 0x17, 0x2A);

        // ================================================================
        //  HTTP
        // ================================================================
        private readonly HttpClient _http = new()
        {
            BaseAddress = new Uri("http://localhost:5180/"),
            Timeout = TimeSpan.FromSeconds(180)
        };

        // ================================================================
        //  Controls & State
        // ================================================================
        private Panel _scrollContainer = null!;
        private Panel _leftCard = null!;
        private Panel _rightCard = null!;
        private Panel _syncCard = null!;

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

        // Cloud Sync Card controls
        private Label _syncDescLbl = null!;
        private Label _syncStatusLbl = null!;
        private Button _syncAquaBtn = null!;
        private Button _syncSparkleBtn = null!;
        private Button _syncCleanBtn = null!;
        private Button _syncMasterBtn = null!;
        private Button _syncAllBtn = null!;
        private Button _refreshStatusBtn = null!;
        private Label _statusAquaPill = null!;
        private Label _statusSparklePill = null!;
        private Label _statusCleanPill = null!;
        private Label _statusMasterPill = null!;
        private Panel _tileAqua = null!;
        private Panel _tileSparkle = null!;
        private Panel _tileClean = null!;
        private Panel _tileMaster = null!;
        private Panel? _bottomSpacer;

        private BackupItemDto? _selectedBackup;
        private List<BackupItemDto> _successfulBackups = new();

        public BackupRestoreView()
        {
            Dock = DockStyle.Fill;
            BackColor = PageBg;
            Font = new Font("Segoe UI", 9.5f);
            DoubleBuffered = true;

            if (SessionUser.RoleId != 1 && SessionUser.Role != UserRole.SuperAdmin)
            {
                Controls.Add(new AccessDeniedView("Backup & Restore Data", "Super Admin (Role 1)"));
                return;
            }

            InitializeComponent();
            Sidebar.EnableDoubleBuffering(this);

            Load += async (s, e) =>
            {
                await LoadBackupsAsync();
                await LoadCloudStatusAsync();
            };
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

            // ---- Title ----
            _scrollContainer.Controls.Add(new Label
            {
                Text = "Backup, Restore & Cloud Sync",
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 22f),
                Location = new Point(36, 20),
                AutoSize = true,
                UseMnemonic = false
            });

            // ---- Subtitle ----
            _scrollContainer.Controls.Add(new Label
            {
                Text = "Manage local database backups, restore points, and safe local-to-MonsterASP cloud synchronization.",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9.5f),
                Location = new Point(36, 64),
                AutoSize = true,
                UseMnemonic = false
            });

            // ---- Cards ----
            _leftCard = CreateCardPanel();
            _rightCard = CreateCardPanel();
            _syncCard = CreateCardPanel();
            _bottomSpacer = new Panel { BackColor = Color.Transparent };

            _scrollContainer.Controls.Add(_leftCard);
            _scrollContainer.Controls.Add(_rightCard);
            _scrollContainer.Controls.Add(_syncCard);
            _scrollContainer.Controls.Add(_bottomSpacer);

            BuildLeftCard();
            BuildRightCard();
            BuildSyncCard();

            _leftCard.Resize += (s, e) => RelayoutLeftCard();
            _rightCard.Resize += (s, e) => RelayoutRightCard();
            _syncCard.Resize += (s, e) => RelayoutSyncCard();

            RelayoutCards();
            RelayoutLeftCard();
            RelayoutRightCard();
            RelayoutSyncCard();

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
            int top = 106;
            int padX = 36;
            int gap = 24;
            int availableWidth = Math.Max(700, _scrollContainer.ClientSize.Width - (padX * 2));
            int cardHeight = 490;

            int cardWidth = (availableWidth - gap) / 2;

            _leftCard.SetBounds(padX, top, cardWidth, cardHeight);
            _rightCard.SetBounds(padX + cardWidth + gap, top, cardWidth, cardHeight);

            int syncTop = top + cardHeight + 24;
            int syncHeight = availableWidth >= 850 ? 285 : 395;
            _syncCard.SetBounds(padX, syncTop, availableWidth, syncHeight);

            if (_bottomSpacer != null)
            {
                _bottomSpacer.SetBounds(padX, syncTop + syncHeight, availableWidth, 32);
            }
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
                    "Database Target:      MSME_MasterCrm",
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
                ForeColor = RoyalBlue,
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
                BackColor = DeepNavySoft,
                Padding = new Padding(12)
            };
            _warnBanner.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = RoundedRect(new Rectangle(0, 0, _warnBanner.Width - 1, _warnBanner.Height - 1), 8);
                using var fill = new SolidBrush(DeepNavySoft);
                e.Graphics.FillPath(fill, path);
                using var pen = new Pen(Blue, 1.2f);
                e.Graphics.DrawPath(pen, path);

                // Warning Icon
                using var iconPen = new Pen(DeepNavy, 1.8f);
                e.Graphics.DrawPolygon(iconPen, new PointF[]
                {
                    new(22, 16),
                    new(32, 34),
                    new(12, 34)
                });
                using var b = new SolidBrush(DeepNavy);
                e.Graphics.FillRectangle(b, 21, 23, 2, 5);
                e.Graphics.FillEllipse(b, 21, 30, 2, 2);
            };

            var warnText = new Label
            {
                Text = SuperAdminLabels.RestoreWarningCaution,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = DeepNavy,
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
                ForeColor = RoyalBlue,
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
                BackColor = RoyalBlue,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Enabled = false
            };
            _restoreBtn.FlatAppearance.BorderSize = 0;
            _restoreBtn.FlatAppearance.MouseOverBackColor = RoyalBlueHover;
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
                _restoreStatusLbl.ForeColor = SlateBlue;
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
                using var bgBrush = new SolidBrush(isSelected ? DeepNavySoft : Color.White);
                e.Graphics.FillPath(bgBrush, path);
                using var pen = new Pen(isSelected ? RoyalBlue : CardBorder, isSelected ? 1.5f : 1f);
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
            var pillBg = isAuto ? BlueSoft : SlateBlueSoft;
            var pillFg = isAuto ? Blue : SlateBlue;

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
                    _backupStatusLbl.ForeColor = SlateBlue;
                    _backupStatusLbl.Text = $"Backup failed: {err}";
                    return;
                }

                var result = await resp.Content.ReadFromJsonAsync<CreateBackupResponse>();
                _backupStatusLbl.ForeColor = RoyalBlue;
                _backupStatusLbl.Text = $"✓ Backup created: {result?.FileName} (Status: Success)";

                // Reload restore list to show newly created backup
                await LoadBackupsAsync();
            }
            catch (Exception ex)
            {
                _backupStatusLbl.ForeColor = SlateBlue;
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
                "WARNING: This will overwrite all existing data in MSME_MasterCrm!",
                "Confirm Database Restore",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (confirm != DialogResult.Yes) return;

            _restoreStatusLbl.Text = "Restoring database snapshot... Please wait.";
            _restoreStatusLbl.ForeColor = Blue;
            _restoreBtn.Enabled = false;
            Cursor = Cursors.WaitCursor;

            try
            {
                using var resp = await _http.PostAsync($"api/backups/{_selectedBackup.BackupId}/restore", null);
                if (!resp.IsSuccessStatusCode)
                {
                    var err = await resp.Content.ReadAsStringAsync();
                    _restoreStatusLbl.ForeColor = SlateBlue;
                    _restoreStatusLbl.Text = $"Restore failed: {err}";
                    return;
                }

                _restoreStatusLbl.ForeColor = RoyalBlue;
                _restoreStatusLbl.Text = $"✓ Database successfully restored from {_selectedBackup.FileName}!";

                MessageBox.Show(
                    $"Database 'MSME_MasterCrm' has been successfully restored to snapshot:\n\n{_selectedBackup.FileName}",
                    "Restore Completed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                await LoadBackupsAsync();
            }
            catch (Exception ex)
            {
                _restoreStatusLbl.ForeColor = SlateBlue;
                _restoreStatusLbl.Text = $"Restore error: {ex.Message}";
            }
            finally
            {
                _restoreBtn.Enabled = _selectedBackup != null;
                Cursor = Cursors.Default;
            }
        }

        // ================================================================
        //  BOTTOM CARD: Safe Cloud Synchronization
        // ================================================================
        private void BuildSyncCard()
        {
            _syncCard.SuspendLayout();

            var titleLbl = new Label
            {
                Text = "SAFE LOCAL ➔ MONSTERASP CLOUD SYNCHRONIZATION",
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 13f),
                Location = new Point(24, 18),
                AutoSize = true,
                UseMnemonic = false
            };
            _syncCard.Controls.Add(titleLbl);

            _syncDescLbl = new Label
            {
                Text = "Safely synchronize records from local SQL Server databases to MonsterASP cloud databases (Port 1433). " +
                       "Record-level sync performs INSERT for new records and UPDATE for modified records. Existing cloud records are never deleted.",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9f),
                Location = new Point(24, 46),
                Size = new Size(Math.Max(100, _syncCard.Width - 48), 28),
                UseMnemonic = false
            };
            _syncCard.Controls.Add(_syncDescLbl);

            // 4 Database Tiles
            _tileAqua = CreateDatabaseTile("AquaShine", "aquaShine_db ➔ db70860", out _statusAquaPill, out _syncAquaBtn, "Sync AquaShine", () => RunSyncFlowAsync("AquaShine"));
            _tileSparkle = CreateDatabaseTile("SparkleRide", "sparkleRide_db ➔ db70861", out _statusSparklePill, out _syncSparkleBtn, "Sync SparkleRide", () => RunSyncFlowAsync("SparkleRide"));
            _tileClean = CreateDatabaseTile("CleanRide", "cleanRide_db ➔ db70862", out _statusCleanPill, out _syncCleanBtn, "Sync CleanRide", () => RunSyncFlowAsync("CleanRide"));
            _tileMaster = CreateDatabaseTile("Master ERP", "MSME_MasterCrm ➔ db67193", out _statusMasterPill, out _syncMasterBtn, "Sync Master ERP", () => RunSyncFlowAsync("MasterERP"));

            _syncCard.Controls.Add(_tileAqua);
            _syncCard.Controls.Add(_tileSparkle);
            _syncCard.Controls.Add(_tileClean);
            _syncCard.Controls.Add(_tileMaster);

            // Status label
            _syncStatusLbl = new Label
            {
                Text = "Initializing cloud connectivity...",
                Font = new Font("Segoe UI", 8.8f),
                ForeColor = Muted,
                Location = new Point(24, 235),
                Size = new Size(400, 24),
                AutoEllipsis = true,
                UseMnemonic = false
            };
            _syncCard.Controls.Add(_syncStatusLbl);

            // Refresh Status button
            _refreshStatusBtn = new Button
            {
                Text = "↻ Refresh Status",
                Font = new Font("Segoe UI", 8.8f),
                ForeColor = SlateBlue,
                BackColor = SlateBlueSoft,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Size = new Size(130, 36)
            };
            _refreshStatusBtn.FlatAppearance.BorderColor = Color.FromArgb(0xCB, 0xD5, 0xE1);
            _refreshStatusBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(0xE2, 0xE8, 0xF0);
            _refreshStatusBtn.Click += async (s, e) => await LoadCloudStatusAsync();
            _syncCard.Controls.Add(_refreshStatusBtn);

            // "Sync All Databases" Button
            _syncAllBtn = new Button
            {
                Text = "⚡ Sync All Databases",
                Font = new Font("Segoe UI Semibold", 9.5f),
                ForeColor = Color.White,
                BackColor = RoyalBlue,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Size = new Size(185, 36)
            };
            _syncAllBtn.FlatAppearance.BorderSize = 0;
            _syncAllBtn.FlatAppearance.MouseOverBackColor = RoyalBlueHover;
            _syncAllBtn.Click += async (s, e) => await RunSyncFlowAsync("All");
            _syncCard.Controls.Add(_syncAllBtn);

            _syncCard.ResumeLayout(true);
        }

        private Panel CreateDatabaseTile(
            string title,
            string subtitle,
            out Label pill,
            out Button syncBtn,
            string buttonText,
            Func<Task> onSync)
        {
            var p = new Panel
            {
                BackColor = Color.FromArgb(0xF8, 0xFA, 0xFC),
                Padding = new Padding(12)
            };
            p.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = RoundedRect(new Rectangle(0, 0, p.Width - 1, p.Height - 1), 8);
                using var pen = new Pen(Color.FromArgb(0xE2, 0xE8, 0xF0), 1.2f);
                e.Graphics.DrawPath(pen, path);
            };

            var titleLbl = new Label
            {
                Text = title,
                Font = new Font("Segoe UI Semibold", 10.2f),
                ForeColor = Navy,
                Location = new Point(12, 10),
                AutoSize = true,
                UseMnemonic = false
            };
            p.Controls.Add(titleLbl);

            var subLbl = new Label
            {
                Text = subtitle,
                Font = new Font("Segoe UI", 7.8f),
                ForeColor = Muted,
                Location = new Point(12, 33),
                AutoSize = true,
                UseMnemonic = false
            };
            p.Controls.Add(subLbl);

            pill = new Label
            {
                Text = "Checking...",
                Font = new Font("Segoe UI", 7.6f, FontStyle.Bold),
                ForeColor = SlateBlue,
                BackColor = SlateBlueSoft,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(96, 20),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                UseMnemonic = false
            };
            pill.Location = new Point(Math.Max(120, p.Width - 12 - pill.Width), 10);
            p.Controls.Add(pill);

            syncBtn = new Button
            {
                Text = buttonText,
                Font = new Font("Segoe UI Semibold", 9f),
                ForeColor = Color.White,
                BackColor = Navy,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Height = 32,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            syncBtn.FlatAppearance.BorderSize = 0;
            syncBtn.FlatAppearance.MouseOverBackColor = NavyHover;
            syncBtn.Location = new Point(12, Math.Max(50, p.Height - 12 - syncBtn.Height));
            syncBtn.Width = Math.Max(80, p.Width - 24);
            syncBtn.Click += async (s, e) => await onSync();
            p.Controls.Add(syncBtn);

            var localPill = pill;
            var localBtn = syncBtn;
            p.Resize += (s, e) =>
            {
                localPill.Left = Math.Max(10, p.Width - 12 - localPill.Width);
                localBtn.Width = Math.Max(80, p.Width - 24);
                localBtn.Top = Math.Max(50, p.Height - 12 - localBtn.Height);
            };

            return p;
        }

        private void RelayoutSyncCard()
        {
            if (_syncCard == null || _syncDescLbl == null) return;
            int w = Math.Max(100, _syncCard.ClientSize.Width - 48);

            _syncDescLbl.Width = w;

            int tilesTop = 80;
            int gap = 12;

            if (w >= 850)
            {
                int tileWidth = (w - (3 * gap)) / 4;
                int tileHeight = 125;

                _tileAqua.SetBounds(24, tilesTop, tileWidth, tileHeight);
                _tileSparkle.SetBounds(24 + (tileWidth + gap), tilesTop, tileWidth, tileHeight);
                _tileClean.SetBounds(24 + (tileWidth + gap) * 2, tilesTop, tileWidth, tileHeight);
                _tileMaster.SetBounds(24 + (tileWidth + gap) * 3, tilesTop, tileWidth, tileHeight);

                int bottomTop = tilesTop + tileHeight + 16;
                _syncStatusLbl.SetBounds(24, bottomTop + 6, Math.Max(200, w - 340), 24);
                _refreshStatusBtn.SetBounds(24 + w - 330, bottomTop, 130, 36);
                _syncAllBtn.SetBounds(24 + w - 190, bottomTop, 190, 36);
            }
            else
            {
                int tileWidth = (w - gap) / 2;
                int tileHeight = 115;

                _tileAqua.SetBounds(24, tilesTop, tileWidth, tileHeight);
                _tileSparkle.SetBounds(24 + tileWidth + gap, tilesTop, tileWidth, tileHeight);
                _tileClean.SetBounds(24, tilesTop + tileHeight + gap, tileWidth, tileHeight);
                _tileMaster.SetBounds(24 + tileWidth + gap, tilesTop + tileHeight + gap, tileWidth, tileHeight);

                int bottomTop = tilesTop + (tileHeight * 2) + gap + 16;
                _syncStatusLbl.SetBounds(24, bottomTop + 6, Math.Max(200, w - 340), 24);
                _refreshStatusBtn.SetBounds(24 + w - 330, bottomTop, 130, 36);
                _syncAllBtn.SetBounds(24 + w - 190, bottomTop, 190, 36);
            }
        }

        private async Task LoadCloudStatusAsync()
        {
            try
            {
                _syncStatusLbl.Text = "Checking MonsterASP cloud connectivity...";
                _syncStatusLbl.ForeColor = Muted;
                _statusAquaPill.Text = "Checking...";
                _statusSparklePill.Text = "Checking...";
                _statusCleanPill.Text = "Checking...";
                _statusMasterPill.Text = "Checking...";

                using var req = CreateAuthenticatedRequest(HttpMethod.Get, "api/cloud-sync/connectivity");
                using var resp = await _http.SendAsync(req);
                if (resp.IsSuccessStatusCode)
                {
                    var connectivity = await resp.Content.ReadFromJsonAsync<Dictionary<string, bool>>();
                    if (connectivity != null)
                    {
                        connectivity.TryGetValue("AquaShine", out var a);
                        connectivity.TryGetValue("SparkleRide", out var s);
                        connectivity.TryGetValue("CleanRide", out var c);
                        connectivity.TryGetValue("MasterERP", out var m);

                        UpdatePill(_statusAquaPill, a);
                        UpdatePill(_statusSparklePill, s);
                        UpdatePill(_statusCleanPill, c);
                        UpdatePill(_statusMasterPill, m);

                        bool allOnline = (a && s && c && m);
                        _syncStatusLbl.Text = allOnline
                            ? "✓ All 4 MonsterASP cloud databases are online and ready for synchronization."
                            : "⚠ Some cloud databases are offline or unreachable. Check credentials and firewall.";
                        _syncStatusLbl.ForeColor = allOnline ? Color.FromArgb(0x16, 0x65, 0x34) : Color.FromArgb(0x99, 0x1B, 0x1B);
                        return;
                    }
                }

                _syncStatusLbl.Text = "Could not check cloud connectivity (API responded with error).";
                _syncStatusLbl.ForeColor = SlateBlue;
            }
            catch (Exception ex)
            {
                _syncStatusLbl.Text = $"Connectivity check failed: {ex.Message}";
                _syncStatusLbl.ForeColor = SlateBlue;
            }
        }

        private static void UpdatePill(Label pill, bool isOnline)
        {
            pill.Text = isOnline ? "● Cloud Online" : "● Offline";
            pill.ForeColor = isOnline ? Color.FromArgb(0x16, 0x65, 0x34) : Color.FromArgb(0x99, 0x1B, 0x1B);
            pill.BackColor = isOnline ? Color.FromArgb(0xDC, 0xFC, 0xE7) : Color.FromArgb(0xFE, 0xE2, 0xE2);
        }

        private void SetSyncButtonsEnabled(bool enabled)
        {
            _syncAquaBtn.Enabled = enabled;
            _syncSparkleBtn.Enabled = enabled;
            _syncCleanBtn.Enabled = enabled;
            _syncMasterBtn.Enabled = enabled;
            _syncAllBtn.Enabled = enabled;
            _refreshStatusBtn.Enabled = enabled;
        }

        private static HttpRequestMessage CreateAuthenticatedRequest(HttpMethod method, string url)
        {
            var req = new HttpRequestMessage(method, url);
            int userId = SessionUser.UserId > 0 ? SessionUser.UserId : 11;
            req.Headers.Add("X-Current-User-Id", userId.ToString());
            req.Headers.Add("X-User-Id", userId.ToString());
            return req;
        }

        private async Task RunSyncFlowAsync(string target)
        {
            // 1. Confirm Dialog
            using (var confirmDlg = new SyncConfirmDialog(target))
            {
                if (confirmDlg.ShowDialog(this) != DialogResult.OK)
                    return;
            }

            SetSyncButtonsEnabled(false);
            _syncStatusLbl.Text = $"[1/3] Generating preview for {target}... Inspecting schemas and comparing records.";
            _syncStatusLbl.ForeColor = Blue;
            Cursor = Cursors.WaitCursor;

            try
            {
                // 2. Fetch Preview
                using var previewReq = CreateAuthenticatedRequest(HttpMethod.Get, $"api/cloud-sync/preview?target={Uri.EscapeDataString(target)}");
                using var previewResp = await _http.SendAsync(previewReq);

                if (!previewResp.IsSuccessStatusCode)
                {
                    var err = await previewResp.Content.ReadAsStringAsync();
                    MessageBox.Show(
                        $"Failed to generate sync preview:\n\n{err}",
                        "Sync Preview Failed",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    _syncStatusLbl.Text = "Preview generation failed.";
                    _syncStatusLbl.ForeColor = Color.FromArgb(0x99, 0x1B, 0x1B);
                    return;
                }

                var previews = await previewResp.Content.ReadFromJsonAsync<List<SyncDatabasePreviewDto>>();
                if (previews == null || previews.Count == 0)
                {
                    MessageBox.Show(
                        "No database preview data returned from server.",
                        "Preview Empty",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                // Show Preview Dialog
                using (var previewDlg = new SyncPreviewDialog(target, previews))
                {
                    if (previewDlg.ShowDialog(this) != DialogResult.OK)
                    {
                        _syncStatusLbl.Text = "Synchronization cancelled by user after preview.";
                        _syncStatusLbl.ForeColor = Muted;
                        return;
                    }
                }

                // 3. Execute Sync
                _syncStatusLbl.Text = $"[2/3] Synchronizing records for {target} to MonsterASP cloud... Please wait.";
                _syncStatusLbl.ForeColor = RoyalBlue;

                using var syncReq = CreateAuthenticatedRequest(HttpMethod.Post, $"api/cloud-sync/sync?target={Uri.EscapeDataString(target)}");
                using var syncResp = await _http.SendAsync(syncReq);

                if (!syncResp.IsSuccessStatusCode)
                {
                    var err = await syncResp.Content.ReadAsStringAsync();
                    MessageBox.Show(
                        $"Sync execution failed:\n\n{err}",
                        "Synchronization Failed",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    _syncStatusLbl.Text = $"Sync failed: {err}";
                    _syncStatusLbl.ForeColor = Color.FromArgb(0x99, 0x1B, 0x1B);
                    return;
                }

                var result = await syncResp.Content.ReadFromJsonAsync<MultiSyncResultDto>();
                if (result == null)
                {
                    MessageBox.Show("Sync completed but no result details were received.", "Sync Finished", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // 4. Show Result Dialog
                _syncStatusLbl.Text = $"[3/3] Sync finished in {result.TotalDuration}. Showing results.";
                using (var resultDlg = new SyncResultDialog(target, result))
                {
                    resultDlg.ShowDialog(this);
                }

                int totalIns = result.Databases.Sum(d => d.TotalInserted);
                int totalUpd = result.Databases.Sum(d => d.TotalUpdated);
                int totalSkip = result.Databases.Sum(d => d.TotalSkipped);
                int totalFail = result.Databases.Sum(d => d.TotalFailed);

                _syncStatusLbl.Text = result.Success
                    ? $"✓ Sync completed successfully: {totalIns} inserted, {totalUpd} updated, {totalSkip} unchanged, {totalFail} failed (Duration: {result.TotalDuration})."
                    : $"⚠ Sync completed with errors: {totalIns} inserted, {totalUpd} updated, {totalFail} failed.";
                _syncStatusLbl.ForeColor = result.Success ? Color.FromArgb(0x16, 0x65, 0x34) : Color.FromArgb(0x99, 0x1B, 0x1B);

                await LoadCloudStatusAsync();
            }
            catch (Exception ex)
            {
                _syncStatusLbl.Text = $"Synchronization error: {ex.Message}";
                _syncStatusLbl.ForeColor = Color.FromArgb(0x99, 0x1B, 0x1B);
                MessageBox.Show(
                    $"An unexpected error occurred during synchronization:\n\n{ex.Message}",
                    "Sync Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                SetSyncButtonsEnabled(true);
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
