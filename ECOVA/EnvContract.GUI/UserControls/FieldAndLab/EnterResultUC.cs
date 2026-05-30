using EnvContract.BLL.Interfaces;
using EnvContract.DTO.Entities;
using EnvContract.GUI.Forms.Main;
using EnvContract.GUI.Helpers;
using EnvContract.GUI.Services;
using Guna.UI2.WinForms;
using Serilog;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using EnvContract.Common;

namespace EnvContract.GUI.UserControls.FieldAndLab
{
    public class EnterResultUC : UserControl
    {
        private IPlanningService _planningService;
        private ITestingService _testingService;
        private VoiceSearchService _voiceService;
        private Action _langHandler;

        // Left panel
        private Guna2TextBox txtSearchContract;
        private FlowLayoutPanel flpContractList;
        private Panel _pnlListClip;

        // Dynamic Language Labels
        private Label _lblSampleTypeLabel;
        private Label _lblResultLabel;
        private Guna2ComboBox _cboResultFilter;

        // Right panel
        private Panel pnlRightContent;
        private Label lblContractId;
        private Label lblCustomerName;
        private Label lblContractBadge;
        private string _contractBadgeText = "";
        private Guna2ComboBox cboSampleType;
        private FlowLayoutPanel flpSamplingAreas;
        private Guna2Panel pnlFilterWrap;
        private Guna2Panel pnlFrame;

        // Data
        private List<ContractDisplayDTO> _allContracts = new List<ContractDisplayDTO>();
        private ContractDisplayDTO _selectedContract;
        private Panel _selectedContractPanel;

        // Read-only mode
        private bool _isReadOnly = false;
        private Label _lblTitle;

        // Color constants — copy từ Planning
        private static readonly Color DarkGreen1 = Color.FromArgb(19, 42, 19);
        private static readonly Color DarkGreen2 = Color.FromArgb(49, 87, 44);
        private static readonly Color MediumGreen = Color.FromArgb(79, 119, 45);
        private static readonly Color LightGreenBg = Color.FromArgb(220, 237, 200);
        private static readonly Color YellowGreen = Color.FromArgb(236, 243, 158);
        private static readonly Color PageBg = Color.FromArgb(240, 244, 235);
        private static readonly Color InputBg = Color.FromArgb(226, 231, 220);
        private static readonly Color TableHeaderBg = Color.FromArgb(174, 196, 128);
        private static readonly Color TableRowAlt = Color.FromArgb(245, 248, 241);

        public EnterResultUC()
        {
            _voiceService = Program.ServiceProvider?.GetService<VoiceSearchService>(); // must be before InitializeComponent
            InitializeComponent();
            this.DoubleBuffered = true;
            if (!this.DesignMode)
            {
                _planningService = Program.ServiceProvider.GetRequiredService<IPlanningService>();
                _testingService = Program.ServiceProvider.GetRequiredService<ITestingService>();
            }
        }

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            if (_planningService != null)
            {
                try
                {
                    _allContracts = await _planningService.GetContractListAsync();
                    PopulateContractList(_allContracts);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading contracts: {ex.Message}");
                    _allContracts = new List<ContractDisplayDTO>();
                    PopulateContractList(_allContracts);
                }
            }

            _isReadOnly = MainForm.IsReadOnlyForRole("R04");
            if (_isReadOnly) ApplyReadOnlyRecursive(this);
        }

        private void ApplyReadOnlyRecursive(Control ctrl)
        {
            if (ctrl is Guna2Button btn &&
                (btn.Text.Contains("Lưu")      || btn.Text.Contains("Thêm") ||
                 btn.Text.Contains("Xóa")      || btn.Text.Contains("Cập nhật")))
            {
                btn.Enabled = false;
                btn.FillColor = Color.FromArgb(180, 180, 180);
                btn.ForeColor = Color.FromArgb(100, 100, 100);
            }
            if (ctrl is DataGridView dgv)
            {
                dgv.ReadOnly = true;
            }
            foreach (Control child in ctrl.Controls)
                ApplyReadOnlyRecursive(child);
        }

        // =====================================================================
        // UI LAYOUT — copy 1:1 từ SampleConfigUC (Planning)
        // =====================================================================

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.Dock = DockStyle.Fill;
            this.BackColor = PageBg;
            this.Padding = new Padding(0);

            // === TITLE ===
            var pnlTitle = new Panel
            {
                Dock = DockStyle.Top,
                Height = 90,
                BackColor = PageBg
            };
            var LM = LanguageManager.Instance;
            _lblTitle = new Label
            {
                Text = LM.Get("field_title"),
                Font = new Font("Segoe UI", 26, FontStyle.Bold),
                ForeColor = Color.Black,
                AutoSize = true,
                BackColor = PageBg,
                Location = new Point(30, 30)
            };
            pnlTitle.Controls.Add(_lblTitle);

            // === LEFT SIDEBAR — copy từ Planning ===
            var pnlLeftWrapper = new Panel
            {
                Dock = DockStyle.Left,
                Width = 295,
                Padding = new Padding(30, 10, 5, 15),
                BackColor = PageBg
            };

            var pnlLeft = new Guna2Panel
            {
                Dock = DockStyle.Fill,
                FillColor = LightGreenBg,
                BackColor = PageBg,
                BorderRadius = 20,
                BorderThickness = 1,
                BorderColor = MediumGreen,
                Padding = new Padding(1, 0, 1, 10)
            };
            pnlLeft.ShadowDecoration.Enabled = true;
            pnlLeft.ShadowDecoration.Depth = 3;
            pnlLeft.ShadowDecoration.Color = Color.FromArgb(40, 0, 0, 0);

            var pnlSearch = new Panel
            {
                Dock = DockStyle.Top,
                Height = 55,
                Padding = new Padding(15, 10, 15, 5)
            };
            txtSearchContract = new Guna2TextBox
            {
                Dock = DockStyle.Fill,
                PlaceholderText = LM.Get("planning_search"),
                PlaceholderForeColor = Color.FromArgb(64, 64, 64),
                Font = new Font("Segoe UI", 12),
                BorderRadius = 15,
                FillColor = InputBg,
                BorderColor = Color.Black,
                BorderThickness = 1,
                ForeColor = Color.Black
            };
            txtSearchContract.TextChanged += TxtSearchContract_TextChanged;
            pnlSearch.Controls.Add(txtSearchContract);
            VoiceSearchHelper.AttachVoiceButtonInPanel(txtSearchContract, pnlSearch, _voiceService,
                () => VoiceSearchHelper.ExtractCardContext(flpContractList, "ContractId", "CustomerName"));
            pnlLeft.Controls.Add(pnlSearch);

            _pnlListClip = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0), BackColor = LightGreenBg };
            flpContractList = new Planning.DoubleBufferedFlowLayoutPanel
            {
                AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(5, 5, 5, 10), BackColor = LightGreenBg
            };

            _pnlListClip.Resize += (s, e) =>
            {
                int sbWidth = SystemInformation.VerticalScrollBarWidth + 2;
                flpContractList.SetBounds(0, 0, _pnlListClip.Width + sbWidth, _pnlListClip.Height);
                foreach (Control ctrl in flpContractList.Controls) ctrl.Width = Math.Max(_pnlListClip.Width - 12, 200);
            };

            _pnlListClip.Controls.Add(flpContractList);
            pnlLeft.Controls.Add(_pnlListClip);
            _pnlListClip.BringToFront();
            pnlLeftWrapper.Controls.Add(pnlLeft);

            // === RIGHT CONTENT — copy từ Planning ===
            var pnlRightWrapper = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(5, 10, 15, 15),
                BackColor = PageBg,
                Visible = false
            };

            pnlRightContent = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = PageBg };
            var pnlRightInner = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = PageBg };

            var pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 85,
                Padding = new Padding(5, 0, 5, 10),
                BackColor = PageBg
            };

            pnlFrame = new Guna2Panel
            {
                Dock = DockStyle.Fill,
                BorderRadius = 15,
                BorderThickness = 1,
                BorderColor = Color.FromArgb(170, 190, 150),
                FillColor = Color.White,
                BackColor = PageBg,
                Padding = new Padding(5, 10, 5, 5),
                Visible = false
            };

            BuildRightHeader(pnlHeader, pnlFrame);

            flpSamplingAreas = new Planning.DoubleBufferedFlowLayoutPanel
            {
                Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(5, 5, 5, 5)
            };
            // Tắt thanh cuộn ngang — tránh xuất hiện khi scrollbar dọc xuất hiện
            flpSamplingAreas.AutoScroll = false;
            flpSamplingAreas.HorizontalScroll.Enabled = false;
            flpSamplingAreas.HorizontalScroll.Visible = false;
            flpSamplingAreas.AutoScroll = true;
            flpSamplingAreas.Resize += (s, e) =>
            {
                int targetWidth = flpSamplingAreas.ClientSize.Width - 10;
                if (targetWidth < 200) targetWidth = 200;

                flpSamplingAreas.SuspendLayout();
                foreach (Control ctrl in flpSamplingAreas.Controls)
                {
                    if (ctrl is Panel p && p.Width != targetWidth)
                        p.Width = targetWidth;
                }
                flpSamplingAreas.ResumeLayout(false);
            };
            pnlFrame.Controls.Add(flpSamplingAreas);
            flpSamplingAreas.BringToFront();

            pnlRightInner.Controls.Add(pnlFrame);
            pnlRightInner.Controls.Add(pnlHeader);

            pnlRightContent.Controls.Add(pnlRightInner);
            pnlRightWrapper.Controls.Add(pnlRightContent);

            // Z-order: Fill → Left → Top
            this.Controls.Add(pnlRightWrapper);
            this.Controls.Add(pnlLeftWrapper);
            this.Controls.Add(pnlTitle);

            _langHandler = () =>
            {
                if (this.IsDisposed) return;
                var lm = LanguageManager.Instance;
                if (_lblTitle != null) _lblTitle.Text = lm.Get("field_title");
                if (txtSearchContract != null) txtSearchContract.PlaceholderText = lm.Get("planning_search");

                if (_lblSampleTypeLabel != null) _lblSampleTypeLabel.Text = lm.Get("planning_filter_sample");
                if (_lblResultLabel != null) _lblResultLabel.Text = lm.Get("field_filter_display");

                // Reposition filter controls dynamically based on label widths
                if (_lblSampleTypeLabel != null && cboSampleType != null &&
                    _lblResultLabel != null && _cboResultFilter != null)
                {
                    var fnt = _lblSampleTypeLabel.Font;
                    int lbl1W = TextRenderer.MeasureText(_lblSampleTypeLabel.Text, fnt).Width + 4;
                    int lbl2W = TextRenderer.MeasureText(_lblResultLabel.Text, fnt).Width + 4;
                    int iconEnd = 44;
                    int lbl1X = iconEnd;
                    int cbo1X = lbl1X + lbl1W + 4;
                    int lbl2X = cbo1X + cboSampleType.Width + 10;
                    int cbo2X = lbl2X + lbl2W + 4;
                    int wrapW = cbo2X + _cboResultFilter.Width + 10;
                    _lblSampleTypeLabel.Location = new Point(lbl1X, _lblSampleTypeLabel.Location.Y);
                    cboSampleType.Location       = new Point(cbo1X, cboSampleType.Location.Y);
                    _lblResultLabel.Location     = new Point(lbl2X, _lblResultLabel.Location.Y);
                    _cboResultFilter.Location    = new Point(cbo2X, _cboResultFilter.Location.Y);
                    if (pnlFilterWrap != null) pnlFilterWrap.Width = Math.Max(wrapW, 400);
                }

                if (cboSampleType != null && cboSampleType.Items.Count >= 4)
                {
                    int sel1 = cboSampleType.SelectedIndex;
                    cboSampleType.Items.Clear();
                    cboSampleType.Items.AddRange(new object[] { lm.Get("planning_filter_all"), lm.Get("planning_env_air"), lm.Get("planning_env_water"), lm.Get("planning_env_soil") });
                    if (sel1 >= 0 && sel1 < cboSampleType.Items.Count) cboSampleType.SelectedIndex = sel1;
                }

                if (_cboResultFilter != null && _cboResultFilter.Items.Count >= 3)
                {
                    int sel2 = _cboResultFilter.SelectedIndex;
                    _cboResultFilter.Items.Clear();
                    _cboResultFilter.Items.AddRange(new object[] { lm.Get("planning_filter_all"), lm.Get("field_filter_not_entered"), lm.Get("field_filter_entered") });
                    if (sel2 >= 0 && sel2 < _cboResultFilter.Items.Count) _cboResultFilter.SelectedIndex = sel2;
                }

                // Cập nhật column headers trong tất cả DataGridView đang hiển thị
                if (flpSamplingAreas != null)
                {
                    foreach (Control outerCtrl in flpSamplingAreas.Controls)
                    {
                        // Cấu trúc: flpSamplingAreas > Panel (Tag="ENV:...") > Panel (areaPanel) + Guna2Panel (header)
                        if (outerCtrl is Panel sectionPanel)
                        {
                            if (sectionPanel.Controls.Count > 0 && sectionPanel.Controls[0] is Guna.UI2.WinForms.Guna2Panel headerPnl)
                            {
                                var lblTitle = headerPnl.Controls.OfType<Label>().FirstOrDefault(c => c.Name == "lblEnvTitle");
                                if (lblTitle != null && lblTitle.Tag != null)
                                {
                                    string et = lblTitle.Tag.ToString();
                                    string icon = et switch { "Không khí" => "🌬", "Nước thải" => "💧", "Đất" => "🌱", _ => "📌" };
                                    string loc = et switch { "Không khí" => lm.Get("planning_env_air"), "Nước thải" => lm.Get("planning_env_water"), "Đất" => lm.Get("planning_env_soil"), _ => et };
                                    lblTitle.Text = $"{icon} {loc.ToUpper()}";
                                }
                                var lblCount = headerPnl.Controls.OfType<Label>().FirstOrDefault(c => c.Name == "lblEnvCount");
                                if (lblCount != null && lblCount.Tag != null)
                                {
                                    // Use plan_sp_area_count everywhere for consistency
                                    lblCount.Text = string.Format(lm.Get("plan_sp_area_count"), lblCount.Tag.ToString());
                                }
                            }

                            foreach (Control innerCtrl in sectionPanel.Controls)
                            {
                                if (innerCtrl is Panel areaPanel)
                                {
                                    var dgv = areaPanel.Controls.OfType<DataGridView>().FirstOrDefault();
                                    if (dgv != null)
                                    {
                                        if (dgv.Columns.Contains("ParamName")) dgv.Columns["ParamName"].HeaderText = lm.Get("plan_sp_col_name");
                                        if (dgv.Columns.Contains("Unit")) dgv.Columns["Unit"].HeaderText = lm.Get("plan_sp_col_unit");
                                        if (dgv.Columns.Contains("ResultValue")) dgv.Columns["ResultValue"].HeaderText = lm.Get("director_col_result");
                                        if (dgv.Columns.Contains("QcvnLimit")) dgv.Columns["QcvnLimit"].HeaderText = lm.Get("plan_sp_col_qcvn_short");
                                    }
                                }
                            }
                        }
                    }
                }
            };
            LanguageManager.Instance.LanguageChanged += _langHandler;
            _langHandler(); // Apply immediately

            pnlRightWrapper.Visible = true;
            this.ResumeLayout(false);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_langHandler != null) LanguageManager.Instance.LanguageChanged -= _langHandler;
            }
            base.Dispose(disposing);
        }

        // =====================================================================
        // RIGHT HEADER — copy từ Planning, filter khác (Hiển thị thay vì Phân công)
        // =====================================================================

        private void BuildRightHeader(Panel pnlHeader, Guna2Panel pnlFrame)
        {
            // Contract Info — copy từ Planning
            var pnlContractInfo = new Panel
            {
                Height = 85,
                Dock = DockStyle.Top,
                Padding = new Padding(5, 5, 0, 0)
            };

            lblContractBadge = new Label
            {
                Text = "",
                Size = new Size(48, 48),
                Location = new Point(5, 5),
                BackColor = Color.Transparent,
                Visible = false
            };
            lblContractBadge.Paint += (s, e) =>
            {
                var lbl = (Label)s;
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var brush = new SolidBrush(LightGreenBg))
                    e.Graphics.FillEllipse(brush, 0, 0, lbl.Width - 1, lbl.Height - 1);
                using (var pen = new Pen(DarkGreen2, 2f))
                    e.Graphics.DrawEllipse(pen, 1, 1, lbl.Width - 3, lbl.Height - 3);
                using (var font = new Font("Segoe UI", 12, FontStyle.Bold))
                    TextRenderer.DrawText(e.Graphics, _contractBadgeText, font,
                        new Rectangle(0, 0, lbl.Width, lbl.Height),
                        DarkGreen2,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            };
            pnlContractInfo.Controls.Add(lblContractBadge);

            lblContractId = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 18, FontStyle.Bold),
                ForeColor = DarkGreen2,
                AutoSize = true,
                Location = new Point(65, 5),
                BackColor = Color.Transparent,
                Visible = false
            };
            pnlContractInfo.Controls.Add(lblContractId);

            lblCustomerName = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 11),
                ForeColor = DarkGreen1,
                AutoSize = true,
                Location = new Point(67, 42),
                BackColor = Color.Transparent,
                Visible = false
            };
            pnlContractInfo.Controls.Add(lblCustomerName);

            pnlHeader.Controls.Add(pnlContractInfo);

            // Filter bar — nằm trong pnlFrame (copy từ Planning)
            var pnlFilter = new Panel
            {
                Height = 48,
                Dock = DockStyle.Top,
                BackColor = Color.Transparent
            };

            pnlFilterWrap = new Guna2Panel
            {
                FillColor = Color.FromArgb(245, 248, 241),
                BorderRadius = 15,
                BorderThickness = 1,
                BorderColor = Color.FromArgb(170, 190, 150),
                Size = new Size(600, 42),
                Location = new Point(5, 0),
                Visible = false
            };

            // Icon phễu lọc — copy từ Planning
            var lblFilterIcon = new Label
            {
                Text = "",
                Size = new Size(30, 30),
                Location = new Point(8, 6),
                BackColor = Color.Transparent
            };
            lblFilterIcon.Paint += (s, e) =>
            {
                var lbl = (Label)s;
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var brush = new SolidBrush(MediumGreen))
                    e.Graphics.FillEllipse(brush, 0, 0, lbl.Width - 1, lbl.Height - 1);
                using (var pen = new Pen(Color.White, 2f))
                {
                    var points = new PointF[]
                    {
                        new PointF(7, 8), new PointF(23, 8), new PointF(17, 16),
                        new PointF(17, 22), new PointF(13, 22), new PointF(13, 16),
                    };
                    e.Graphics.FillPolygon(new SolidBrush(Color.White), points);
                }
            };
            pnlFilterWrap.Controls.Add(lblFilterIcon);

            _lblSampleTypeLabel = new Label
            {
                Text = LanguageManager.Instance.Get("planning_filter_sample"),
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.FromArgb(80, 80, 80),
                AutoSize = true,
                Location = new Point(50, 12),
                BackColor = Color.Transparent
            };
            pnlFilterWrap.Controls.Add(_lblSampleTypeLabel);

            cboSampleType = new Guna2ComboBox
            {
                Location = new Point(138, 3),
                Size = new Size(155, 36),
                Font = new Font("Segoe UI", 10),
                BorderRadius = 15,
                FillColor = Color.White,
                BorderColor = Color.FromArgb(170, 190, 150),
                ForeColor = Color.Black
            };
            cboSampleType.Items.AddRange(new object[] {
                LanguageManager.Instance.Get("planning_filter_all"),
                LanguageManager.Instance.Get("planning_env_air"),
                LanguageManager.Instance.Get("planning_env_water"),
                LanguageManager.Instance.Get("planning_env_soil")
            });
            cboSampleType.SelectedIndex = 0;
            cboSampleType.SelectedIndexChanged += CboSampleType_SelectedIndexChanged;
            pnlFilterWrap.Controls.Add(cboSampleType);

            // Filter riêng của Field: "Hiển thị" (Tất cả / Chưa nhập / Đã nhập)
            _lblResultLabel = new Label
            {
                Text = LanguageManager.Instance.Get("field_filter_display"),
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.FromArgb(80, 80, 80),
                AutoSize = true,
                Location = new Point(320, 12),
                BackColor = Color.Transparent
            };
            pnlFilterWrap.Controls.Add(_lblResultLabel);

            _cboResultFilter = new Guna2ComboBox
            {
                Location = new Point(418, 3),
                Size = new Size(165, 36),
                Font = new Font("Segoe UI", 10),
                BorderRadius = 15,
                FillColor = Color.White,
                BorderColor = Color.FromArgb(170, 190, 150),
                ForeColor = Color.Black
            };
            _cboResultFilter.Items.AddRange(new object[] {
                LanguageManager.Instance.Get("planning_filter_all"),
                LanguageManager.Instance.Get("field_filter_not_entered"),
                LanguageManager.Instance.Get("field_filter_entered")
            });
            _cboResultFilter.SelectedIndex = 0;
            // Dùng SelectedIndex thay vì text — tránh lỗi khi ngôn ngữ thay đổi
            _cboResultFilter.SelectedIndexChanged += (s, e) => ApplyResultFilter(_cboResultFilter.SelectedIndex);
            pnlFilterWrap.Controls.Add(_cboResultFilter);

            pnlFilter.Controls.Add(pnlFilterWrap);
            pnlFrame.Controls.Add(pnlFilter);
        }

        // =====================================================================
        // CONTRACT LIST — copy từ Planning
        // =====================================================================

        private void PopulateContractList(List<ContractDisplayDTO> contracts)
        {
            flpContractList.SuspendLayout();
            flpContractList.Controls.Clear();

            foreach (var c in contracts)
            {
                var pnl = CreateContractItem(c);
                flpContractList.Controls.Add(pnl);
            }

            flpContractList.ResumeLayout(true);

            if (contracts.Count > 0 && flpContractList.Controls.Count > 0)
            {
                SelectContract(contracts[0], (Panel)flpContractList.Controls[0]);
            }
        }

        private Panel CreateContractItem(ContractDisplayDTO contract)
        {
            int visibleWidth = _pnlListClip != null ? _pnlListClip.Width : 260;
            int itemWidth = Math.Max(visibleWidth - 12, 200);
            var pnl = new Panel
            {
                Size = new Size(itemWidth, 55),
                MinimumSize = new Size(200, 55),
                BackColor = LightGreenBg,
                Margin = new Padding(0, 2, 0, 2),
                Cursor = Cursors.Hand,
                Tag = contract
            };

            // Copy từ Planning — AutoSize = true (Planning hoạt động OK, không overlap)
            var lblId = new Label
            {
                Text = contract.ContractId,
                Font = new Font("Segoe UI", 13, FontStyle.Bold),
                ForeColor = DarkGreen2,
                AutoSize = true,
                Location = new Point(10, 5),
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };

            var lblName = new Label
            {
                Text = contract.CustomerName,
                Font = new Font("Segoe UI", 10),
                ForeColor = DarkGreen1,
                AutoSize = true,
                Location = new Point(12, 30),
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };

            pnl.Controls.Add(lblId);
            pnl.Controls.Add(lblName);

            EventHandler onClick = (s, e) => SelectContract(contract, pnl);
            pnl.Click += onClick;
            lblId.Click += onClick;
            lblName.Click += onClick;

            EventHandler onEnter = (s, e) =>
            {
                if (_selectedContractPanel != pnl)
                    pnl.BackColor = Color.FromArgb(200, 225, 180);
            };
            EventHandler onLeave = (s, e) =>
            {
                if (_selectedContractPanel != pnl)
                    pnl.BackColor = LightGreenBg;
            };
            pnl.MouseEnter += onEnter;
            pnl.MouseLeave += onLeave;
            lblId.MouseEnter += onEnter;
            lblName.MouseEnter += onEnter;
            lblId.MouseLeave += onLeave;
            lblName.MouseLeave += onLeave;

            return pnl;
        }

        private async void SelectContract(ContractDisplayDTO contract, Panel panel)
        {
            if (_selectedContractPanel != null)
                _selectedContractPanel.BackColor = LightGreenBg;

            _selectedContract = contract;
            _selectedContractPanel = panel;
            panel.BackColor = Color.FromArgb(180, 210, 160);

            // Hiện các thành phần Header — copy từ Planning
            lblContractBadge.Visible = true;
            lblContractId.Visible = true;
            lblCustomerName.Visible = true;
            if (pnlFilterWrap != null) pnlFilterWrap.Visible = true;
            if (pnlFrame != null) pnlFrame.Visible = true;

            _contractBadgeText = contract.ContractId.Length > 2
                ? contract.ContractId.Substring(0, 2)
                : contract.ContractId;
            lblContractBadge.Invalidate();

            lblContractId.Text = contract.ContractId;
            lblCustomerName.Text = contract.CustomerName;

            await LoadSamplingAreas(contract.ContractId);
        }

        private void TxtSearchContract_TextChanged(object sender, EventArgs e)
        {
            var query = txtSearchContract.Text.Trim().ToLower();
            var filtered = _allContracts
                .Where(c => c.ContractId.ToLower().Contains(query) ||
                            c.CustomerName.ToLower().Contains(query))
                .ToList();
            PopulateContractList(filtered);
        }

        // =====================================================================
        // SAMPLING AREAS — layout copy từ Planning, logic riêng Field
        // =====================================================================

        private async System.Threading.Tasks.Task LoadSamplingAreas(string contractId)
        {
            flpSamplingAreas.SuspendLayout();
            flpSamplingAreas.Controls.Clear();

            try
            {
                var orders = await _planningService.GetOrdersByContractAsync(contractId);
                if (orders != null && orders.Count > 0)
                {
                    var grouped = orders
                        .GroupBy(o => string.IsNullOrEmpty(o.EnvironmentType) ? "Kh\u00f4ng kh\u00ed" : o.EnvironmentType)
                        .OrderBy(g => g.Key == "Kh\u00f4ng kh\u00ed" ? 0 : g.Key == "N\u01b0\u1edbc th\u1ea3i" ? 1 : 2);

                    foreach (var group in grouped)
                    {
                        var sectionPanel = await CreateSampleTypeSection(group.Key, group.ToList());
                        sectionPanel.Tag = "ENV:" + group.Key;
                        flpSamplingAreas.Controls.Add(sectionPanel);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading sampling areas: {ex.Message}");
            }

            flpSamplingAreas.ResumeLayout(true);
            ApplySampleTypeFilter();
        }

        private async System.Threading.Tasks.Task<Panel> CreateSampleTypeSection(
            string envType, System.Collections.Generic.List<EnvContract.DTO.Entities.OrderDTO> orders)
        {
            var sectionPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                Width = flpSamplingAreas.Width - 25,
                Margin = new Padding(0, 0, 0, 20)
            };

            string envIcon = envType switch
            {
                "Kh\u00f4ng kh\u00ed" => "🌬",
                "N\u01b0\u1edbc th\u1ea3i" => "💧",
                "\u0110\u1ea5t"       => "🌱",
                _           => "📌"
            };

            string envTypeLocalized = envType switch
            {
                "Kh\u00f4ng kh\u00ed" => LanguageManager.Instance.Get("planning_env_air"),
                "N\u01b0\u1edbc th\u1ea3i" => LanguageManager.Instance.Get("planning_env_water"),
                "\u0110\u1ea5t"       => LanguageManager.Instance.Get("planning_env_soil"),
                _           => envType
            };

            var pnlSectionHeader = new Guna.UI2.WinForms.Guna2Panel
            {
                Width = sectionPanel.Width,
                Height = 45,
                FillColor = Color.FromArgb(79, 119, 45),
                BorderRadius = 15,
                CustomizableEdges = new Guna.UI2.WinForms.Suite.CustomizableEdges
                {
                    BottomLeft = false, BottomRight = false,
                    TopLeft = true, TopRight = true
                },
                Margin = new Padding(0)
            };

            var lblSectionTitle = new Label
            {
                Name = "lblEnvTitle",
                Tag = envType,
                Text = $"{envIcon} {envTypeLocalized.ToUpper()}",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(20, 10)
            };
            pnlSectionHeader.Controls.Add(lblSectionTitle);

            var lblSectionCount = new Label
            {
                Name = "lblEnvCount",
                Tag = orders.Count.ToString(),
                Text = string.Format(LanguageManager.Instance.Get("plan_sp_area_count"), orders.Count),
                Font = new Font("Segoe UI", 10, FontStyle.Italic),
                ForeColor = Color.WhiteSmoke,
                BackColor = Color.Transparent,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleRight,
                Width = 150,
                Location = new Point(pnlSectionHeader.Width - 170, 10)
            };
            pnlSectionHeader.Controls.Add(lblSectionCount);
            sectionPanel.Controls.Add(pnlSectionHeader);

            int areaIndex = 1;
            foreach (var order in orders)
            {
                string locationName = !string.IsNullOrEmpty(order.OrderName)
                    ? order.OrderName : $"Khu v\u1ef1c {areaIndex}";
                var areaPanel = await CreateSamplingAreaPanel(areaIndex, locationName, order.OrderID);
                areaPanel.Margin = new Padding(0);
                sectionPanel.Controls.Add(areaPanel);
                areaIndex++;
            }

            return sectionPanel;
        }

        private void ApplySampleTypeFilter()
        {
            // Dùng SelectedIndex thay vì text — tránh lỗi khi ngôn ngữ thay đổi
            // 0 = Tất cả/All, 1 = Không khí/Air, 2 = Nước thải/Wastewater, 3 = Đất/Soil
            int selectedIdx = cboSampleType?.SelectedIndex ?? 0;
            string[] dbEnvTypes = { "", "Không khí", "Nước thải", "Đất" }; // DB values (bất biến)
            string dbFilter = selectedIdx > 0 && selectedIdx < dbEnvTypes.Length ? dbEnvTypes[selectedIdx] : "";

            foreach (Control ctrl in flpSamplingAreas.Controls)
            {
                if (ctrl is Panel p && p.Tag?.ToString()?.StartsWith("ENV:") == true)
                {
                    string envType = p.Tag.ToString().Substring(4);
                    // Tag chứa DB value ("Không khí", "Nước thải", "Đất")
                    p.Visible = selectedIdx == 0 || envType == dbFilter;
                }
            }
        }



        private async System.Threading.Tasks.Task<Panel> CreateSamplingAreaPanel(int index, string locationName, string orderId)
        {
            // Copy cấu trúc từ Planning.CreateSamplingAreaPanel
            var pnlArea = new Panel
            {
                AutoSize = false,
                Margin = new Padding(0, 5, 0, 15),
                Padding = new Padding(0),
                Tag = orderId
            };

            // ---- HEADER — copy từ Planning ----
            var pnlAreaHeader = new Guna2Panel
            {
                Dock = DockStyle.Top,
                Height = 55,
                FillColor = LightGreenBg,
                BorderRadius = 15,
                Margin = new Padding(0)
            };
            pnlAreaHeader.CustomizableEdges.BottomLeft = false;
            pnlAreaHeader.CustomizableEdges.BottomRight = false;

            var lblBadge = new Label
            {
                Text = "",
                Size = new Size(32, 32),
                Location = new Point(15, 13),
                BackColor = Color.Transparent
            };
            lblBadge.Paint += (s, e) =>
            {
                var lbl = (Label)s;
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                using (var brush = new SolidBrush(DarkGreen2))
                {
                    g.FillEllipse(brush, 0, 0, lbl.Width - 1, lbl.Height - 1);
                }

                string text = index.ToString();
                using (var font = new Font("Segoe UI", 11, FontStyle.Bold))
                using (var brush = new SolidBrush(YellowGreen))
                {
                    var size = g.MeasureString(text, font);
                    g.DrawString(text, font, brush, (lbl.Width - size.Width) / 2 + 0.5f, (lbl.Height - size.Height) / 2 + 0.5f);
                }
            };
            pnlAreaHeader.Controls.Add(lblBadge);

            var lblAreaName = new Label
            {
                Text = locationName,
                Font = new Font("Segoe UI", 15, FontStyle.Bold),
                ForeColor = DarkGreen1,
                AutoSize = true,
                Location = new Point(55, 13),
                BackColor = Color.Transparent
            };
            pnlAreaHeader.Controls.Add(lblAreaName);

            pnlArea.Controls.Add(pnlAreaHeader);

            // ---- DATA TABLE — copy pattern từ Planning ----
            var dgv = CreateFieldResultDataGridView();
            dgv.Tag = orderId;
            pnlArea.Controls.Add(dgv);
            dgv.BringToFront();

            // Load Field-specific data
            await LoadFieldParametersIntoGrid(dgv, orderId);

            RecalculateAreaPanelSize(pnlArea, dgv);

            pnlArea.Width = Math.Max(flpSamplingAreas.ClientSize.Width - 10, 200);
            dgv.Width = pnlArea.Width;
            pnlAreaHeader.Dock = DockStyle.Top;
            dgv.Dock = DockStyle.Top;

            return pnlArea;
        }

        private void RecalculateAreaPanelSize(Panel pnlArea, DataGridView dgv)
        {
            int rowCount = dgv.Rows.Count;
            int tableHeight = Math.Max(rowCount * 38 + 45, 120);
            dgv.Height = tableHeight;
            pnlArea.Height = 55 + tableHeight + 10;
        }

        // =====================================================================
        // DATA GRID VIEW — Field-specific (nhập kết quả đo)
        // Copy style từ Planning, cột khác
        // =====================================================================

        private DataGridView CreateFieldResultDataGridView()
        {
            var dgv = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                EditMode = DataGridViewEditMode.EditOnEnter,
                Font = new Font("Segoe UI", 11),
                GridColor = Color.White, // Ẩn grid mặc định — vẽ đường ngang bằng CellPainting
                CellBorderStyle = DataGridViewCellBorderStyle.None, // Bỏ hết đường kẻ mặc định
                RowTemplate = { Height = 38 },
                ScrollBars = ScrollBars.None,
                EnableHeadersVisualStyles = false,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None, // Bỏ border header
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.White,
                    ForeColor = Color.Black,
                    SelectionBackColor = Color.FromArgb(210, 233, 185),
                    SelectionForeColor = Color.Black
                },
                AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = LightGreenBg, // Xanh nhạt rõ ràng hơn (220,237,200)
                    ForeColor = Color.Black,
                    SelectionBackColor = Color.FromArgb(200, 225, 175),
                    SelectionForeColor = Color.Black
                },
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = TableHeaderBg,
                    ForeColor = DarkGreen1,
                    Font = new Font("Segoe UI", 11, FontStyle.Bold),
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    SelectionBackColor = TableHeaderBg,
                    SelectionForeColor = DarkGreen1,
                    Padding = new Padding(5, 0, 5, 0)
                },
                ColumnHeadersHeight = 42,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing
            };

            // Màu xen kẽ rõ ràng
            var rowColorEven = Color.White;
            var rowColorOdd = Color.FromArgb(220, 237, 200); // Xanh nhạt rõ ràng
            var separatorColor = Color.FromArgb(200, 218, 180); // Đường ngang
            var headerSepColor = Color.FromArgb(160, 185, 140); // Đường ngang header đậm hơn

            dgv.CellPainting += (s, e) =>
            {
                if (e.ColumnIndex < 0) return;

                // === HEADER ROW ===
                if (e.RowIndex == -1)
                {
                    // Vẽ nền header
                    using (var brush = new SolidBrush(TableHeaderBg))
                        e.Graphics.FillRectangle(brush, e.CellBounds);
                    // Vẽ text header
                    string headerText = dgv.Columns[e.ColumnIndex].HeaderText;
                    TextRenderer.DrawText(e.Graphics, headerText,
                        new Font("Segoe UI", 11, FontStyle.Bold),
                        e.CellBounds, DarkGreen1,
                        TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
                    // Đường ngang dưới header
                    using (var pen = new Pen(headerSepColor, 1.5f))
                        e.Graphics.DrawLine(pen, e.CellBounds.Left, e.CellBounds.Bottom - 1,
                            e.CellBounds.Right - 1, e.CellBounds.Bottom - 1);
                    e.Handled = true;
                    return;
                }

                if (e.RowIndex < 0) return;

                // === DATA ROWS === Tự vẽ nền xen kẽ + nội dung + đường ngang
                bool isOddRow = e.RowIndex % 2 == 1;
                Color bgColor = isOddRow ? rowColorOdd : rowColorEven;

                // Kiểm tra nếu row có custom style (ví dụ: warning đỏ)
                var rowStyle = dgv.Rows[e.RowIndex].DefaultCellStyle;
                if (rowStyle.BackColor != Color.Empty)
                    bgColor = rowStyle.BackColor;

                // Nếu đang selected — dùng xanh đậm rõ ràng để phân biệt
                bool isSelected = (e.State & DataGridViewElementStates.Selected) != 0;
                if (isSelected)
                    bgColor = Color.FromArgb(170, 205, 140);

                // Vẽ nền
                using (var brush = new SolidBrush(bgColor))
                    e.Graphics.FillRectangle(brush, e.CellBounds);

                // === Cột ResultValue: text + icon bút chì trong từng cell ===
                if (dgv.Columns[e.ColumnIndex].Name == "ResultValue")
                {
                    string val = e.Value?.ToString() ?? "";
                    bool isMuted = val == "---" || string.IsNullOrEmpty(val);
                    Color fgColor = isMuted ? UIConstants.TextMuted
                        : (e.CellStyle.ForeColor != Color.Empty ? e.CellStyle.ForeColor : Color.Black);
                    if (isSelected) fgColor = Color.Black;

                    // Vẽ text kết quả (để lại chỗ cho icon bên phải)
                    TextRenderer.DrawText(e.Graphics, val,
                        new Font("Segoe UI", 11, FontStyle.Bold),
                        new Rectangle(e.CellBounds.X, e.CellBounds.Y, e.CellBounds.Width - 30, e.CellBounds.Height),
                        fgColor, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);

                    // === Icon bút chì (nền tròn, đầu but xuống trái, nét đậm) ===
                    int iconSize = 22;
                    int iconX = e.CellBounds.Right - iconSize - 6;
                    int iconY = e.CellBounds.Y + (e.CellBounds.Height - iconSize) / 2;

                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    // Nền tròn đậm hơn (alpha 70)
                    using (var brush = new SolidBrush(Color.FromArgb(70, 79, 119, 45)))
                        e.Graphics.FillEllipse(brush, iconX, iconY, iconSize, iconSize);

                    // Bút chì hướng xuống-trái (eraser góc trên-phải, đầu nhọn góc dưới-trái)
                    using (var penIcon = new Pen(Color.FromArgb(35, 75, 30), 2f))
                    {
                        int ix = iconX, iy = iconY;
                        e.Graphics.DrawLine(penIcon, ix + 17, iy + 7,  ix + 9,  iy + 15); // cạnh phải thân bút
                        e.Graphics.DrawLine(penIcon, ix + 15, iy + 5,  ix + 7,  iy + 13); // cạnh trái thân bút
                        e.Graphics.DrawLine(penIcon, ix + 17, iy + 7,  ix + 15, iy + 5);  // nắp eraser (trên phải)
                        e.Graphics.DrawLine(penIcon, ix + 9,  iy + 14, ix + 11, iy + 17); // đầu nhọn phần 1
                        e.Graphics.DrawLine(penIcon, ix + 11, iy + 17, ix + 9,  iy + 15); // đầu nhọn phần 2
                    }
                    e.Graphics.SmoothingMode = SmoothingMode.Default;
                }
                else
                {
                    // === Các cột khác: vẽ text bình thường ===
                    string text = e.Value?.ToString() ?? "";
                    var cellFont = e.CellStyle.Font ?? dgv.Font;
                    Color textColor = e.CellStyle.ForeColor != Color.Empty ? e.CellStyle.ForeColor : Color.Black;
                    if (isSelected) textColor = Color.Black;

                    var textAlign = e.CellStyle.Alignment;
                    var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
                    if (textAlign == DataGridViewContentAlignment.MiddleCenter)
                        flags |= TextFormatFlags.HorizontalCenter;
                    else
                        flags |= TextFormatFlags.Left;

                    var textRect = new Rectangle(
                        e.CellBounds.X + e.CellStyle.Padding.Left + 4,
                        e.CellBounds.Y,
                        e.CellBounds.Width - e.CellStyle.Padding.Left - e.CellStyle.Padding.Right - 8,
                        e.CellBounds.Height);
                    TextRenderer.DrawText(e.Graphics, text, cellFont, textRect, textColor, flags);
                }

                // === Đường ngang dưới MỌI cell (CHỈ ngang, KHÔNG dọc) ===
                using (var pen = new Pen(separatorColor, 1f))
                    e.Graphics.DrawLine(pen, e.CellBounds.Left, e.CellBounds.Bottom - 1,
                        e.CellBounds.Right - 1, e.CellBounds.Bottom - 1);

                e.Handled = true;
            };

            typeof(DataGridView).InvokeMember("DoubleBuffered",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.SetProperty,
                null, dgv, new object[] { true });

            // Columns — Field-specific
            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ParamName",
                HeaderText = LanguageManager.Instance.Get("plan_sp_col_name"),
                FillWeight = 28,
                ReadOnly = true,
                DefaultCellStyle = new DataGridViewCellStyle { Padding = new Padding(8, 0, 8, 0) }
            });

            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Unit",
                HeaderText = LanguageManager.Instance.Get("plan_sp_col_unit"),
                FillWeight = 12,
                ReadOnly = true,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    ForeColor = Color.Black,
                    Padding = new Padding(8, 0, 8, 0)
                }
            });

            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ResultValue",
                HeaderText = LanguageManager.Instance.Get("director_col_result"),
                FillWeight = 25,
                ReadOnly = false,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    ForeColor = UIConstants.TextMuted,
                    Font = new Font("Segoe UI", 11, FontStyle.Bold)
                }
            });

            // Hidden columns
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "ParamID", Visible = false });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "ResultID", Visible = false });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "SampleID", Visible = false });

            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "QcvnLimit",
                HeaderText = LanguageManager.Instance.Get("plan_sp_col_qcvn_short"),
                FillWeight = 22,
                ReadOnly = true,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    ForeColor = Color.FromArgb(80, 80, 80),
                    Font = new Font("Segoe UI", 11)
                }
            });

            foreach (DataGridViewColumn col in dgv.Columns)
                col.SortMode = DataGridViewColumnSortMode.NotSortable;

            // Event handlers — Field-specific
            dgv.EditingControlShowing += Dgv_EditingControlShowing;
            dgv.CellValueChanged += Dgv_CellValueChanged;
            dgv.CellClick += Dgv_CellClick;
            dgv.CellEndEdit += Dgv_CellEndEdit;

            return dgv;
        }

        // =====================================================================
        // FIELD BUSINESS LOGIC — giữ nguyên
        // =====================================================================

        private async System.Threading.Tasks.Task LoadFieldParametersIntoGrid(DataGridView dgv, string orderId)
        {
            List<SampleParameterPlanDTO> parameters;
            try
            {
                parameters = await _planningService.GetParametersByOrderAsync(orderId);
            }
            catch
            {
                parameters = new List<SampleParameterPlanDTO>();
            }

            // Chỉ hiển thị thông số của Phòng Hiện Trường
            var fieldParams = parameters.Where(p => p.Department == "Hiện trường").ToList();

            // Load existing test results
            List<TestResultDTO> existingResults = new List<TestResultDTO>();
            List<SampleDTO> samples = new List<SampleDTO>();
            try
            {
                samples = await _planningService.GetSamplesByOrderAsync(orderId);
                if (samples != null && samples.Count > 0)
                {
                    foreach (var sample in samples)
                    {
                        var results = await _testingService.GetResultsForSampleAsync(sample.SampleID);
                        if (results != null) existingResults.AddRange(results);
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[EnterResultUC] Load existing results error: " + ex.Message); }

            string sampleId = samples?.FirstOrDefault()?.SampleID ?? "";

            // ── AUTO-CREATE SAMPLE nếu Order chưa có Sample nào ────────────
            // EnsureSampleExistsAsync: trả SampleID hiện có hoặc tạo mới 1 Sample
            // tuân đúng FK constraints (Barcode UNIQUE, SamplerID nullable, RegulationID nullable).
            if (string.IsNullOrEmpty(sampleId))
            {
                try
                {
                    string regulationId = fieldParams.FirstOrDefault()?.RegulationID;
                    string samplerId    = EnvContract.Common.AppState.Instance.CurrentUser?.UserID;
                    // Không gửi "unknown" vào FK Users(UserID) — để null cho an toàn
                    if (samplerId == "unknown") samplerId = null;

                    sampleId = await _planningService.EnsureSampleExistsAsync(orderId, regulationId, samplerId);
                    System.Diagnostics.Debug.WriteLine($"[EnterResultUC] EnsureSample → {sampleId}");
                }
                catch (Exception ex)
                {
                    // Hiển thị lỗi thực tế từ DB để dễ debug
                    MessageBox.Show(
                        $"[DEBUG] Không thể tạo Sample cho khu vực này.\n\nChi tiết lỗi:\n{ex.Message}",
                        "Lỗi khởi tạo mẫu", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    System.Diagnostics.Debug.WriteLine($"[EnterResultUC] EnsureSample FAILED: {ex}");
                }
            }

            dgv.Rows.Clear();
            foreach (var p in fieldParams)
            {
                int rowIdx = dgv.Rows.Add();
                var row = dgv.Rows[rowIdx];
                row.Cells["ParamName"].Value = p.ParamName;
                row.Cells["Unit"].Value = p.Unit;
                row.Cells["ParamID"].Value = p.ParamID;
                row.Cells["QcvnLimit"].Value = p.QcvnLimit;
                row.Cells["SampleID"].Value = sampleId;

                var existingResult = existingResults.FirstOrDefault(r => r.ParamID == p.ParamID);
                if (existingResult != null)
                {
                    row.Cells["ResultValue"].Value = existingResult.ResultValue.ToString("0.##");
                    row.Cells["ResultID"].Value = existingResult.ResultID;
                    row.Cells["ResultValue"].Style.ForeColor = existingResult.IsWarning
                        ? UIConstants.DangerColor
                        : Color.Black;
                }
                else
                {
                    row.Cells["ResultValue"].Value = "---";
                    row.Cells["ResultID"].Value = "";
                }
            }
        }

        private void ApplyResultFilter(int filterIndex)
        {
            foreach (Control ctrl in flpSamplingAreas.Controls)
            {
                if (ctrl is Panel areaPanel)
                {
                    var dgv = areaPanel.Controls.OfType<DataGridView>().FirstOrDefault();
                    if (dgv == null) continue;

                    foreach (DataGridViewRow row in dgv.Rows)
                    {
                        var val = row.Cells["ResultValue"].Value?.ToString() ?? "---";
                        bool hasResult = val != "---" && !string.IsNullOrEmpty(val);

                        // filterIndex: 0=Tất cả/All, 1=Chưa nhập/Not Entered, 2=Đã nhập/Entered
                        row.Visible = filterIndex switch
                        {
                            1 => !hasResult,  // Chưa nhập / Not Entered
                            2 => hasResult,   // Đã nhập / Entered
                            _ => true         // Tất cả / All
                        };
                    }

                    int visibleCount = dgv.Rows.Cast<DataGridViewRow>().Count(r => r.Visible);
                    int tableHeight = Math.Max(visibleCount * 38 + 45, 120);
                    dgv.Height = tableHeight;
                    areaPanel.Height = 55 + tableHeight + 10;
                }
            }
        }

        private void CboSampleType_SelectedIndexChanged(object sender, EventArgs e)
        {
            ApplySampleTypeFilter();
        }

        // ===== EVENT HANDLERS — Field-specific =====

        private void Dgv_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            if (e.Control is TextBox tb)
            {
                tb.KeyPress -= NumericKeyPress;
                var grid = (DataGridView)sender;
                string colName = grid.CurrentCell?.OwningColumn.Name;
                if (colName == "ResultValue")
                {
                    tb.KeyPress += NumericKeyPress;
                }
            }
        }

        private void NumericKeyPress(object sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) && e.KeyChar != '.' && e.KeyChar != ',' && e.KeyChar != '<' && e.KeyChar != '>' && e.KeyChar != '=' && e.KeyChar != '-')
            {
                e.Handled = true;
            }
        }

        private void Dgv_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            var dgv = (DataGridView)sender;

            string colName = dgv.Columns[e.ColumnIndex].Name;
            if (colName == "ResultValue")
            {
                var cell = dgv.Rows[e.RowIndex].Cells[e.ColumnIndex];
                var cur = cell.Value?.ToString();
                dgv.Rows[e.RowIndex].Tag = (cur == "---") ? null : cur; // save original
                if (cur == "---") cell.Value = "";
                dgv.CurrentCell = cell;
                dgv.BeginEdit(true);
            }
        }

        private async void Dgv_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            var dgv = (DataGridView)sender;
            string colName = dgv.Columns[e.ColumnIndex].Name;

            if (colName != "ResultValue") return;

            var val = dgv.Rows[e.RowIndex].Cells["ResultValue"].Value?.ToString()?.Trim();
            if (string.IsNullOrEmpty(val))
            {
                dgv.Rows[e.RowIndex].Cells["ResultValue"].Value = "---";
                dgv.Rows[e.RowIndex].Cells["ResultValue"].Style.ForeColor = UIConstants.TextMuted;
                return;
            }
            if (val == "---") return;

            // Kiểm tra cảnh báo QCVN ngay khi nhập xong
            if (double.TryParse(val, out double numericValue))
            {
                var qcvnLimit = dgv.Rows[e.RowIndex].Cells["QcvnLimit"].Value?.ToString() ?? "";
                var paramName = dgv.Rows[e.RowIndex].Cells["ParamName"].Value?.ToString() ?? "";
                bool isWarning = CheckQcvnViolation(numericValue, qcvnLimit);

                if (isWarning)
                {
                    dgv.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.FromArgb(255, 230, 230);
                    dgv.Rows[e.RowIndex].DefaultCellStyle.SelectionBackColor = Color.FromArgb(255, 200, 200);
                    var lmWarn = LanguageManager.Instance;
                    MessageBox.Show(
                        string.Format(lmWarn.Get("field_qcvn_warning_msg"), paramName, numericValue, qcvnLimit),
                        lmWarn.Get("field_qcvn_warning_title"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
                else
                {
                    dgv.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.Empty;
                    dgv.Rows[e.RowIndex].DefaultCellStyle.SelectionBackColor = Color.Empty;
                }

                await AutoSaveRow(dgv, e.RowIndex, numericValue, isWarning);
            }
        }

        private async System.Threading.Tasks.Task AutoSaveRow(DataGridView dgv, int rowIndex, double numericValue, bool isWarning)
        {
            try
            {
                var paramId = dgv.Rows[rowIndex].Cells["ParamID"].Value?.ToString() ?? "";
                var resultId = dgv.Rows[rowIndex].Cells["ResultID"].Value?.ToString() ?? "";
                var sampleId = dgv.Rows[rowIndex].Cells["SampleID"].Value?.ToString() ?? "";

                if (string.IsNullOrEmpty(sampleId))
                {
                    // Không nên xảy ra sau fix LoadFieldParametersIntoGrid,
                    // nhưng nếu vẫn rỗng → báo lỗi rõ ràng thay vì im lặng bỏ qua
                    MessageBox.Show(
                        LanguageManager.Instance.Get("field_autosave_error") +
                        " Sample chưa được tạo cho khu vực này. Vui lòng tải lại trang.",
                        LanguageManager.Instance.Get("msg_error"),
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Skip if value did not actually change (user clicked then escaped)
                var origRaw = dgv.Rows[rowIndex].Tag?.ToString();
                if (!string.IsNullOrEmpty(origRaw) &&
                    double.TryParse(origRaw, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out double origNum) &&
                    Math.Abs(origNum - numericValue) < 1e-9)
                    return;

                var uid = EnvContract.Common.AppState.Instance.CurrentUser?.UserID ?? "unknown";
                string reason = string.IsNullOrEmpty(resultId)
                    ? $"Insert by {uid} at {DateTime.Now:yyyy-MM-dd HH:mm}"
                    : $"Update by {uid} at {DateTime.Now:yyyy-MM-dd HH:mm}";

                var testResult = new TestResultDTO
                {
                    ResultID = string.IsNullOrEmpty(resultId) ? Guid.NewGuid().ToString() : resultId,
                    SampleID = sampleId,
                    ParamID = paramId,
                    ResultValue = numericValue,
                    IsWarning = isWarning,
                    TesterID = EnvContract.Common.AppState.Instance.CurrentUser?.UserID ?? "unknown",
                    EnteredAt = DateTime.Now
                };

                await _testingService.EnterTestResultAsync(testResult, reason);
                dgv.Rows[rowIndex].Cells["ResultID"].Value = testResult.ResultID;
            }
            catch (Exception ex)
            {
                MessageBox.Show(LanguageManager.Instance.Get("field_autosave_error") + ex.Message, LanguageManager.Instance.Get("msg_error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Dgv_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            var dgv = (DataGridView)sender;
            if (dgv.Columns[e.ColumnIndex].Name != "ResultValue") return;

            var val = dgv.Rows[e.RowIndex].Cells["ResultValue"].Value?.ToString();
            var qcvnLimit = dgv.Rows[e.RowIndex].Cells["QcvnLimit"].Value?.ToString() ?? "";
            var paramName = dgv.Rows[e.RowIndex].Cells["ParamName"].Value?.ToString() ?? "";

            if (double.TryParse(val, out double numericValue) && !string.IsNullOrEmpty(qcvnLimit))
            {
                bool isWarning = CheckQcvnViolation(numericValue, qcvnLimit);
                if (isWarning)
                {
                    dgv.Rows[e.RowIndex].Cells["ResultValue"].Style.ForeColor = UIConstants.DangerColor;
                    dgv.Rows[e.RowIndex].Cells["ResultValue"].Style.Font = new Font("Segoe UI", 11, FontStyle.Bold);
                    dgv.Rows[e.RowIndex].Cells["ResultValue"].ToolTipText =
                        $"⚠ {paramName} = {numericValue} — VƯỢT NGƯỠNG QCVN ({qcvnLimit})";
                }
                else
                {
                    dgv.Rows[e.RowIndex].Cells["ResultValue"].Style.ForeColor = Color.Black;
                    dgv.Rows[e.RowIndex].Cells["ResultValue"].Style.Font = new Font("Segoe UI", 11, FontStyle.Bold);
                    dgv.Rows[e.RowIndex].Cells["ResultValue"].ToolTipText = "";
                }
            }
            else if (val != "---")
            {
                dgv.Rows[e.RowIndex].Cells["ResultValue"].Style.ForeColor = Color.Black;
            }
        }

        private bool CheckQcvnViolation(double value, string qcvnLimit)
        {
            if (string.IsNullOrEmpty(qcvnLimit)) return false;

            try
            {
                if (qcvnLimit.StartsWith("≤"))
                {
                    double maxVal = double.Parse(qcvnLimit.Replace("≤", "").Trim());
                    return value > maxVal;
                }
                if (qcvnLimit.StartsWith("≥"))
                {
                    double minVal = double.Parse(qcvnLimit.Replace("≥", "").Trim());
                    return value < minVal;
                }
                if (qcvnLimit.Contains("-"))
                {
                    var parts = qcvnLimit.Split('-');
                    if (parts.Length == 2)
                    {
                        double min = double.Parse(parts[0].Trim());
                        double max = double.Parse(parts[1].Trim());
                        return value < min || value > max;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[EnterResult] Lỗi parse QCVN limit '{Limit}'", qcvnLimit);
            }

            return false;
        }

        private string PromptForEditReason(string paramName)
        {
            var lm = LanguageManager.Instance;
            using (var form = new Form())
            {
                form.Text = lm.Get("field_reason_title");
                form.Size = new Size(450, 220);
                form.StartPosition = FormStartPosition.CenterParent;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MaximizeBox = false;
                form.MinimizeBox = false;
                form.BackColor = PageBg;

                var lbl = new Label
                {
                    Text = string.Format(lm.Get("field_reason_label"), paramName),
                    Font = new Font("Segoe UI", 11),
                    Location = new Point(20, 20),
                    AutoSize = true,
                    ForeColor = DarkGreen1
                };

                var txt = new Guna2TextBox
                {
                    Location = new Point(20, 55),
                    Size = new Size(395, 70),
                    Font = new Font("Segoe UI", 11),
                    PlaceholderText = lm.Get("field_reason_ph"),
                    PlaceholderForeColor = Color.FromArgb(64, 64, 64),
                    BorderRadius = 8,
                    FillColor = Color.White,
                    BorderColor = DarkGreen2,
                    Multiline = true
                };

                var btnOk = new Guna2Button
                {
                    Text = lm.Get("msg_confirm"),
                    DialogResult = DialogResult.OK,
                    Location = new Point(200, 135),
                    Size = new Size(100, 35),
                    FillColor = DarkGreen2,
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 10, FontStyle.Bold),
                    BorderRadius = 10
                };

                var btnCancel = new Guna2Button
                {
                    Text = lm.Get("msg_cancel"),
                    DialogResult = DialogResult.Cancel,
                    Location = new Point(315, 135),
                    Size = new Size(100, 35),
                    FillColor = Color.Gray,
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 10, FontStyle.Bold),
                    BorderRadius = 10
                };

                form.Controls.AddRange(new Control[] { lbl, txt, btnOk, btnCancel });
                form.AcceptButton = btnOk;
                form.CancelButton = btnCancel;

                if (form.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(txt.Text))
                {
                    return txt.Text.Trim();
                }
                return null;
            }
        }
    }
}
