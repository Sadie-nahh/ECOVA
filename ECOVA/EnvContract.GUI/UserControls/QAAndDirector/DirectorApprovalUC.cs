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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using EnvContract.Common;

namespace EnvContract.GUI.UserControls.QAAndDirector
{
    public class DirectorApprovalUC : UserControl
    {
        private IPlanningService _planningService;
        private ITestingService _testingService;
        private IExportService _exportService;
        private VoiceSearchService _voiceService;
        private Action _langHandler;

        // Left panel
        private Guna2TextBox txtSearchContract;
        private FlowLayoutPanel flpContractList;
        private Panel _pnlListClip;

        // Right panel
        private Panel pnlRightContent;
        private FlowLayoutPanel flpSamplingAreas;
        private Guna2ComboBox cboSampleType;
        private Guna2ComboBox cboResultFilter;
        private Guna2Panel pnlFilterWrap;
        private Guna2Panel pnlFrame;
        private Label lblContractId;
        private Label lblCustomerName;
        private Label lblContractBadge;
        private string _contractBadgeText = "";
        private Label _lblTitle;

        // Dynamic Language Labels
        private Label _lblSampleTypeLabel;
        private Label _lblResultLabel;

        // Data
        private List<ContractDisplayDTO> _allContracts = new List<ContractDisplayDTO>();
        private ContractDisplayDTO _selectedContract;
        private Panel _selectedContractPanel;

        // Color constants matching Figma design
        private static readonly Color DarkGreen1 = Color.FromArgb(19, 42, 19);
        private static readonly Color DarkGreen2 = Color.FromArgb(49, 87, 44);
        private static readonly Color MediumGreen = Color.FromArgb(79, 119, 45);
        private static readonly Color LightGreenBg = Color.FromArgb(220, 237, 200);
        private static readonly Color YellowGreen = Color.FromArgb(236, 243, 158);
        private static readonly Color PageBg = Color.FromArgb(240, 244, 235);
        private static readonly Color InputBg = Color.FromArgb(226, 231, 220);
        private static readonly Color TableHeaderBg = Color.FromArgb(174, 196, 128);
        private static readonly Color TableRowAlt = Color.FromArgb(245, 248, 241);

        public DirectorApprovalUC()
        {
            _voiceService = Program.ServiceProvider?.GetService<VoiceSearchService>();
            InitializeComponent();
            this.DoubleBuffered = true;
            if (!this.DesignMode)
            {
                _planningService = Program.ServiceProvider.GetRequiredService<IPlanningService>();
                _testingService = Program.ServiceProvider.GetRequiredService<ITestingService>();
                _exportService = Program.ServiceProvider.GetRequiredService<IExportService>();
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

            // Phân quyền: chỉ R07 (Phòng Kết quả) được phê duyệt kết quả
            if (MainForm.IsReadOnlyForRole("R07"))
                ApplyReadOnlyRecursive(this);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_langHandler != null) LanguageManager.Instance.LanguageChanged -= _langHandler;
            }
            base.Dispose(disposing);
        }

        private void ApplyReadOnlyRecursive(Control ctrl)
        {
            if (ctrl is Guna.UI2.WinForms.Guna2Button btn &&
                (btn.Text.Contains("Lưu")      || btn.Text.Contains("Duyệt")    ||
                 btn.Text.Contains("Từ chối") || btn.Text.Contains("Thêm")    ||
                 btn.Text.Contains("Xóa")     || btn.Text.Contains("Xuất file") ||
                 btn.Text.Contains("Cập nhật") || btn.Text.Contains("Export")))
            {
                btn.Enabled = false;
                btn.FillColor = System.Drawing.Color.FromArgb(180, 180, 180);
                btn.ForeColor = System.Drawing.Color.FromArgb(100, 100, 100);
            }
            foreach (Control child in ctrl.Controls)
                ApplyReadOnlyRecursive(child);
        }

        // =====================================================================
        // UI LAYOUT — Cấu trúc giống SampleConfigUC (Planning) + nhóm theo nền mẫu
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
                Text = LM.Get("director_title"),
                Font = new Font("Segoe UI", 26, FontStyle.Bold),
                ForeColor = Color.Black,
                AutoSize = true,
                BackColor = PageBg,
                Location = new Point(30, 30)
            };
            pnlTitle.Controls.Add(_lblTitle);

            // === LEFT SIDEBAR ===
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

            // === RIGHT CONTENT ===
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

            pnlRightWrapper.Visible = true;
            this.ResumeLayout(false);

            _langHandler = () =>
            {
                if (this.IsDisposed) return;
                var lm = LanguageManager.Instance;
                if (_lblTitle != null) _lblTitle.Text = lm.Get("director_title");
                if (txtSearchContract != null) txtSearchContract.PlaceholderText = lm.Get("planning_search");

                if (_lblSampleTypeLabel != null) _lblSampleTypeLabel.Text = lm.Get("planning_filter_sample");
                if (_lblResultLabel != null) _lblResultLabel.Text = lm.Get("director_filter_location");

                // Reposition filter controls dynamically based on label widths
                if (_lblSampleTypeLabel != null && cboSampleType != null &&
                    _lblResultLabel != null && cboResultFilter != null)
                {
                    var fnt = _lblSampleTypeLabel.Font;
                    int lbl1W = TextRenderer.MeasureText(_lblSampleTypeLabel.Text, fnt).Width + 4;
                    int lbl2W = TextRenderer.MeasureText(_lblResultLabel.Text, fnt).Width + 4;
                    int iconEnd = 44;
                    int lbl1X = iconEnd;
                    int cbo1X = lbl1X + lbl1W + 4;
                    int lbl2X = cbo1X + cboSampleType.Width + 10;
                    int cbo2X = lbl2X + lbl2W + 4;
                    int wrapW = cbo2X + cboResultFilter.Width + 10;
                    _lblSampleTypeLabel.Location = new Point(lbl1X, _lblSampleTypeLabel.Location.Y);
                    cboSampleType.Location       = new Point(cbo1X, cboSampleType.Location.Y);
                    _lblResultLabel.Location     = new Point(lbl2X, _lblResultLabel.Location.Y);
                    cboResultFilter.Location    = new Point(cbo2X, cboResultFilter.Location.Y);
                    if (pnlFilterWrap != null) pnlFilterWrap.Width = Math.Max(wrapW, 400);
                }

                if (cboSampleType != null && cboSampleType.Items.Count >= 4)
                {
                    int sel1 = cboSampleType.SelectedIndex;
                    cboSampleType.Items.Clear();
                    cboSampleType.Items.AddRange(new object[] { lm.Get("planning_filter_all"), lm.Get("planning_env_air"), lm.Get("planning_env_water"), lm.Get("planning_env_soil") });
                    if (sel1 >= 0 && sel1 < cboSampleType.Items.Count) cboSampleType.SelectedIndex = sel1;
                }

                if (cboResultFilter != null && cboResultFilter.Items.Count >= 3)
                {
                    int sel2 = cboResultFilter.SelectedIndex;
                    cboResultFilter.Items.Clear();
                    cboResultFilter.Items.AddRange(new object[] { lm.Get("planning_filter_all"), lm.Get("field_filter_not_entered"), lm.Get("field_filter_entered") });
                    if (sel2 >= 0 && sel2 < cboResultFilter.Items.Count) cboResultFilter.SelectedIndex = sel2;
                }

                if (flpSamplingAreas != null)
                {
                    foreach (Control pnl in flpSamplingAreas.Controls)
                    {
                        if (pnl is FlowLayoutPanel sectionPanel)
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
                                    lblCount.Text = string.Format(lm.Get("plan_sp_area_count"), lblCount.Tag.ToString());
                                }

                                // Update export button text on section header
                                foreach (var btnExp in headerPnl.Controls.OfType<Guna2Button>().Where(b => b.Name == "btnExportSection"))
                                    btnExp.Text = lm.Get("director_export");
                            }

                            foreach (Control areaPnl in sectionPanel.Controls)
                            {
                                if (areaPnl is Panel ap)
                                {
                                    var dgv = ap.Controls.OfType<DataGridView>().FirstOrDefault();
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
        }

        // =====================================================================
        // RIGHT HEADER — Filter bar: Nền mẫu + Hiển thị (giống EnterResultUC)
        // Không còn nút Export toàn cục — mỗi section sẽ có nút riêng
        // =====================================================================

        private void BuildRightHeader(Panel pnlHeader, Guna2Panel pnlFrame)
        {
            var LM = LanguageManager.Instance;
            // Contract Info
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

            // Filter bar — nằm trong pnlFrame
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

            // Icon phễu lọc
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
                Text = LM.Get("planning_filter_sample"),
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
                LM.Get("planning_filter_all"),
                LM.Get("planning_env_air"),
                LM.Get("planning_env_water"),
                LM.Get("planning_env_soil")
            });
            cboSampleType.SelectedIndex = 0;
            cboSampleType.SelectedIndexChanged += CboSampleType_SelectedIndexChanged;
            pnlFilterWrap.Controls.Add(cboSampleType);

            // Filter: Vị trí
            _lblResultLabel = new Label
            {
                Text = LM.Get("director_filter_location"),
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.FromArgb(80, 80, 80),
                AutoSize = true,
                Location = new Point(320, 12),
                BackColor = Color.Transparent
            };
            pnlFilterWrap.Controls.Add(_lblResultLabel);

            cboResultFilter = new Guna2ComboBox
            {
                Location = new Point(418, 3),
                Size = new Size(165, 36),
                Font = new Font("Segoe UI", 10),
                BorderRadius = 15,
                FillColor = Color.White,
                BorderColor = Color.FromArgb(170, 190, 150),
                ForeColor = Color.Black
            };
            cboResultFilter.Items.Add(LM.Get("planning_filter_all"));
            cboResultFilter.SelectedIndex = 0;
            cboResultFilter.SelectedIndexChanged += (s, e) => ApplyResultFilter(cboResultFilter.SelectedIndex);
            pnlFilterWrap.Controls.Add(cboResultFilter);

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

            // Hiện các thành phần Header
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
        // SAMPLING AREAS — Nhóm theo nền mẫu (giống SampleConfigUC)
        // =====================================================================

        private async Task LoadSamplingAreas(string contractId)
        {
            flpSamplingAreas.SuspendLayout();
            flpSamplingAreas.Controls.Clear();

            try
            {
                var orders = await _planningService.GetOrdersByContractAsync(contractId);
                if (orders != null && orders.Count > 0)
                {
                    var grouped = orders
                        .GroupBy(o => string.IsNullOrEmpty(o.EnvironmentType) ? "Không khí" : o.EnvironmentType)
                        .OrderBy(g => g.Key == "Không khí" ? 0 : g.Key == "Nước thải" ? 1 : 2);

                    int maxLocations = grouped.Max(g => g.Count());
                    UpdateLocationFilterDropdown(maxLocations);

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

        private async Task<Panel> CreateSampleTypeSection(string envType, List<EnvContract.DTO.Entities.OrderDTO> orders)
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
                "Không khí" => "🌬",
                "Nước thải" => "💧",
                "Đất"       => "🌱",
                _           => "📌"
            };

            string envTypeLocalized = envType switch
            {
                "Không khí" => LanguageManager.Instance.Get("planning_env_air"),
                "Nước thải" => LanguageManager.Instance.Get("planning_env_water"),
                "Đất"       => LanguageManager.Instance.Get("planning_env_soil"),
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
                Location = new Point(pnlSectionHeader.Width - 310, 10)
            };
            pnlSectionHeader.Controls.Add(lblSectionCount);

            // Nút "Xuất file" trên mỗi section header — BackColor = nền xanh header để bỏ viền trắng
            var btnExportSection = new Guna2Button
            {
                Name = "btnExportSection",
                Text = LanguageManager.Instance.Get("director_export"),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.FromArgb(79, 119, 45),
                FillColor = Color.FromArgb(236, 243, 158),
                BackColor = Color.FromArgb(79, 119, 45),
                Size = new Size(100, 28),
                BorderRadius = 8,
                BorderThickness = 0,
                Cursor = Cursors.Hand,
                Location = new Point(pnlSectionHeader.Width - 120, 8)
            };
            btnExportSection.HoverState.FillColor = Color.FromArgb(220, 237, 200);
            btnExportSection.HoverState.ForeColor = Color.FromArgb(49, 87, 44);

            string capturedEnvType = envType;
            btnExportSection.Click += async (s, e) => await ExportSingleEnvType(capturedEnvType);
            pnlSectionHeader.Controls.Add(btnExportSection);

            pnlSectionHeader.Resize += (s, e) =>
            {
                lblSectionCount.Location = new Point(pnlSectionHeader.Width - 270, 10);
                btnExportSection.Location = new Point(pnlSectionHeader.Width - 120, 8);
            };

            sectionPanel.Controls.Add(pnlSectionHeader);

            int areaIndex = 1;
            foreach (var order in orders)
            {
                string locationName = !string.IsNullOrEmpty(order.OrderName) ? order.OrderName : $"Khu vực {areaIndex}";
                var areaPanel = await CreateSamplingAreaPanel(areaIndex, locationName, order.OrderID);
                areaPanel.Margin = new Padding(0);
                areaPanel.Name = $"AreaPanel_{areaIndex}";
                areaPanel.Tag = areaIndex; // Save the area index for filtering
                sectionPanel.Controls.Add(areaPanel);
                areaIndex++;
            }

            return sectionPanel;
        }

        private async Task<Panel> CreateSamplingAreaPanel(int index, string locationName, string orderId)
        {
            var pnlArea = new Panel
            {
                AutoSize = false,
                Margin = new Padding(0, 5, 0, 15),
                Padding = new Padding(0),
                Tag = orderId
            };

            // ---- HEADER ----
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

            // ---- DATA TABLE ----
            var dgv = CreateResultDataGridView();
            dgv.Tag = orderId;
            pnlArea.Controls.Add(dgv);
            dgv.BringToFront();

            await LoadParametersIntoGrid(dgv, orderId);

            RecalculateAreaPanelSize(pnlArea, dgv);

            pnlArea.Width = Math.Max(flpSamplingAreas.ClientSize.Width - 10, 200);
            dgv.Width = pnlArea.Width - 10;
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
        // DATA GRID VIEW — ReadOnly kết quả (giống cấu trúc EnterResultUC)
        // =====================================================================

        private DataGridView CreateResultDataGridView()
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
                EditMode = DataGridViewEditMode.EditProgrammatically,
                Font = new Font("Segoe UI", 11),
                GridColor = Color.White,
                CellBorderStyle = DataGridViewCellBorderStyle.None,
                RowTemplate = { Height = 38 },
                ScrollBars = ScrollBars.None,
                EnableHeadersVisualStyles = false,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
                ReadOnly = true,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.White,
                    ForeColor = Color.Black,
                    SelectionBackColor = Color.FromArgb(210, 233, 185),
                    SelectionForeColor = Color.Black
                },
                AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = LightGreenBg,
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

            var rowColorEven = Color.White;
            var rowColorOdd = Color.FromArgb(220, 237, 200);
            var separatorColor = Color.FromArgb(200, 218, 180);
            var headerSepColor = Color.FromArgb(160, 185, 140);

            dgv.CellPainting += (s, e) =>
            {
                if (e.ColumnIndex < 0) return;

                // === HEADER ROW ===
                if (e.RowIndex == -1)
                {
                    using (var brush = new SolidBrush(TableHeaderBg))
                        e.Graphics.FillRectangle(brush, e.CellBounds);
                    string headerText = dgv.Columns[e.ColumnIndex].HeaderText;
                    TextRenderer.DrawText(e.Graphics, headerText,
                        new Font("Segoe UI", 11, FontStyle.Bold),
                        e.CellBounds, DarkGreen1,
                        TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
                    using (var pen = new Pen(headerSepColor, 1.5f))
                        e.Graphics.DrawLine(pen, e.CellBounds.Left, e.CellBounds.Bottom - 1,
                            e.CellBounds.Right - 1, e.CellBounds.Bottom - 1);
                    e.Handled = true;
                    return;
                }

                if (e.RowIndex < 0) return;

                // === DATA ROWS ===
                bool isOddRow = e.RowIndex % 2 == 1;
                Color bgColor = isOddRow ? rowColorOdd : rowColorEven;

                var rowStyle = dgv.Rows[e.RowIndex].DefaultCellStyle;
                if (rowStyle.BackColor != Color.Empty)
                    bgColor = rowStyle.BackColor;

                bool isSelected = (e.State & DataGridViewElementStates.Selected) != 0;
                if (isSelected)
                    bgColor = Color.FromArgb(170, 205, 140);

                using (var brush = new SolidBrush(bgColor))
                    e.Graphics.FillRectangle(brush, e.CellBounds);

                string text = e.Value?.ToString() ?? "";
                var cellFont = e.CellStyle.Font ?? dgv.Font;
                Color textColor = e.CellStyle.ForeColor != Color.Empty ? e.CellStyle.ForeColor : Color.Black;
                if (isSelected) textColor = Color.Black;

                // Mute "---" text
                if (text == "---" && dgv.Columns[e.ColumnIndex].Name == "ResultValue")
                    textColor = UIConstants.TextMuted;

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

            var LM = LanguageManager.Instance;
            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ParamName",
                HeaderText = LM.Get("plan_sp_col_name"),
                FillWeight = 28,
                ReadOnly = true,
                DefaultCellStyle = new DataGridViewCellStyle { Padding = new Padding(8, 0, 8, 0) }
            });

            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Unit",
                HeaderText = LM.Get("plan_sp_col_unit"),
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
                HeaderText = LM.Get("director_col_result"),
                FillWeight = 25,
                ReadOnly = true,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI", 11, FontStyle.Bold)
                }
            });

            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "QcvnLimit",
                HeaderText = LM.Get("plan_sp_col_qcvn_short"),
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

            return dgv;
        }

        // =====================================================================
        // LOAD DATA — Tất cả thông số (Hiện trường + Thí nghiệm)
        // =====================================================================

        private async Task LoadParametersIntoGrid(DataGridView dgv, string orderId)
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

            if (parameters.Count == 0) return;

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
            catch (Exception ex)
            {
                Log.Warning(ex, "[Director] Lỗi load kết quả thử nghiệm cho Order {OrderId}", orderId);
            }

            dgv.Rows.Clear();
            foreach (var p in parameters)
            {
                int rowIdx = dgv.Rows.Add();
                var row = dgv.Rows[rowIdx];
                row.Cells["ParamName"].Value = p.ParamName;
                row.Cells["Unit"].Value = p.Unit;
                row.Cells["QcvnLimit"].Value = string.IsNullOrEmpty(p.QcvnLimit) ? "---" : p.QcvnLimit;

                var existingResult = existingResults.FirstOrDefault(r => r.ParamID == p.ParamID);
                if (existingResult != null)
                {
                    row.Cells["ResultValue"].Value = existingResult.ResultValue.ToString("0.##");

                    bool isWarning = CheckQcvnViolation(existingResult.ResultValue, p.QcvnLimit);
                    if (isWarning || existingResult.IsWarning)
                    {
                        row.Cells["ResultValue"].Style.ForeColor = UIConstants.DangerColor;
                        row.DefaultCellStyle.BackColor = Color.FromArgb(255, 240, 240);
                    }
                }
                else
                {
                    row.Cells["ResultValue"].Value = "---";
                }
            }
        }

        // =====================================================================
        // FILTERS
        // =====================================================================

        private void CboSampleType_SelectedIndexChanged(object sender, EventArgs e)
        {
            ApplySampleTypeFilter();
        }

        private void ApplySampleTypeFilter()
        {
            int selectedIdx = cboSampleType?.SelectedIndex ?? 0;
            string[] dbEnvTypes = { "", "Không khí", "Nước thải", "Đất" };
            string dbFilter = selectedIdx > 0 && selectedIdx < dbEnvTypes.Length ? dbEnvTypes[selectedIdx] : "";

            foreach (Control ctrl in flpSamplingAreas.Controls)
            {
                if (ctrl is FlowLayoutPanel p && p.Tag?.ToString().StartsWith("ENV:") == true)
                {
                    string envType = p.Tag.ToString().Substring(4);
                    p.Visible = selectedIdx == 0 || envType == dbFilter;
                }
            }
            flpSamplingAreas.PerformLayout();
        }

        private void UpdateLocationFilterDropdown(int maxLocations)
        {
            var lm = LanguageManager.Instance;
            if (cboResultFilter == null) return;
            
            cboResultFilter.Tag = "UPDATING";
            cboResultFilter.Items.Clear();
            cboResultFilter.Items.Add(lm.Get("planning_filter_all"));
            
            for (int i = 1; i <= maxLocations; i++)
            {
                cboResultFilter.Items.Add(string.Format(lm.Get("director_filter_area"), i));
            }
            
            cboResultFilter.SelectedIndex = 0;
            cboResultFilter.Tag = null;
        }

        private void ApplyResultFilter(int filterIndex)
        {
            if (cboResultFilter?.Tag?.ToString() == "UPDATING") return;
            this.Focus();

            foreach (Control ctrl in flpSamplingAreas.Controls)
            {
                if (ctrl is FlowLayoutPanel sectionPanel)
                {
                    foreach (Control child in sectionPanel.Controls)
                    {
                        if (child is Panel areaPanel && areaPanel.Tag is int areaIdx)
                        {
                            bool show = filterIndex == 0 || areaIdx == filterIndex;
                            areaPanel.Visible = show;
                        }
                    }
                }
            }
            flpSamplingAreas.PerformLayout();
        }

        // =====================================================================
        // QCVN CHECK
        // =====================================================================

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
                else
                {
                    if (double.TryParse(qcvnLimit, out double limit))
                    {
                        return value > limit;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[Director] Lỗi parse QCVN limit '{Limit}'", qcvnLimit);
            }

            return false;
        }

        // =====================================================================
        // EXPORT — Nút "Xuất file" toàn cục → Popup chọn nền mẫu
        // =====================================================================

        private void BtnExportAll_Click(object sender, EventArgs e)
        {
            if (_selectedContract == null)
            {
                var LM = LanguageManager.Instance;
                MessageBox.Show(LM.Get("director_msg_select_to_export"));
                return;
            }

            ShowExportPopup();
        }

        /// <summary>
        /// Hiện popup chọn nền mẫu để xuất PDF. Mỗi nền mẫu là 1 file riêng.
        /// </summary>
        private void ShowExportPopup()
        {
            var LM = LanguageManager.Instance;

            // Lấy danh sách nền mẫu hiện có trong hợp đồng
            var envTypes = new List<string>();
            foreach (Control ctrl in flpSamplingAreas.Controls)
            {
                if (ctrl is FlowLayoutPanel p && p.Tag?.ToString().StartsWith("ENV:") == true)
                {
                    envTypes.Add(p.Tag.ToString().Substring(4));
                }
            }

            if (envTypes.Count == 0)
            {
                MessageBox.Show(LM.Get("director_no_orders"), LM.Get("msg_info"),
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Tạo popup form
            var dlg = new Form
            {
                Text = LM.Get("director_export_select_title"),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                Size = new Size(400, 200 + envTypes.Count * 45),
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = PageBg,
                Font = new Font("Segoe UI", 11)
            };

            // Header
            var lblHeader = new Label
            {
                Text = LM.Get("director_export_select_desc"),
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = DarkGreen2,
                AutoSize = true,
                Location = new Point(20, 20)
            };
            dlg.Controls.Add(lblHeader);

            var lblContract = new Label
            {
                Text = $"{_selectedContract.ContractId} — {_selectedContract.CustomerName}",
                Font = new Font("Segoe UI", 10),
                ForeColor = DarkGreen1,
                AutoSize = true,
                Location = new Point(20, 50)
            };
            dlg.Controls.Add(lblContract);

            // Checkboxes cho từng nền mẫu
            var checkboxes = new List<(CheckBox cb, string envType)>();
            int yPos = 85;
            foreach (var envType in envTypes)
            {
                string envIcon = envType switch
                {
                    "Không khí" => "🌬",
                    "Nước thải" => "💧",
                    "Đất"       => "🌱",
                    _           => "📌"
                };
                string envLocalized = envType switch
                {
                    "Không khí" => LM.Get("planning_env_air"),
                    "Nước thải" => LM.Get("planning_env_water"),
                    "Đất"       => LM.Get("planning_env_soil"),
                    _           => envType
                };

                var cb = new CheckBox
                {
                    Text = $"  {envIcon}  {envLocalized}",
                    Font = new Font("Segoe UI", 11),
                    ForeColor = DarkGreen1,
                    AutoSize = true,
                    Checked = true,
                    Location = new Point(30, yPos),
                    BackColor = Color.Transparent
                };
                dlg.Controls.Add(cb);
                checkboxes.Add((cb, envType));
                yPos += 40;
            }

            // Buttons
            var btnExport = new Guna2Button
            {
                Text = LM.Get("director_export"),
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = Color.White,
                FillColor = DarkGreen2,
                Size = new Size(140, 40),
                BorderRadius = 12,
                Cursor = Cursors.Hand,
                Location = new Point(80, yPos + 15)
            };
            btnExport.Click += async (s, e) =>
            {
                var selectedEnvTypes = checkboxes
                    .Where(x => x.cb.Checked)
                    .Select(x => x.envType)
                    .ToList();

                if (selectedEnvTypes.Count == 0)
                {
                    MessageBox.Show(LM.Get("director_export_select_none"), LM.Get("msg_warning"),
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                dlg.Close();

                foreach (var envType in selectedEnvTypes)
                {
                    await ExportSingleEnvType(envType);
                }
            };
            dlg.Controls.Add(btnExport);

            var btnCancel = new Guna2Button
            {
                Text = LM.Get("msg_cancel"),
                Font = new Font("Segoe UI", 11),
                ForeColor = DarkGreen1,
                FillColor = Color.FromArgb(230, 238, 220),
                Size = new Size(120, 40),
                BorderRadius = 12,
                Cursor = Cursors.Hand,
                Location = new Point(240, yPos + 15)
            };
            btnCancel.Click += (s, e) => dlg.Close();
            dlg.Controls.Add(btnCancel);

            dlg.ShowDialog(this.FindForm());
        }

        /// <summary>
        /// Xuất PDF cho một nền mẫu cụ thể.
        /// </summary>
        private async Task ExportSingleEnvType(string envType)
        {
            if (_selectedContract == null) return;

            var LM = LanguageManager.Instance;
            string contractId = _selectedContract.ContractId;
            string customerName = _selectedContract.CustomerName;
            string outDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "ECOVA_Reports");

            try
            {
                // Tìm section panel cho nền mẫu này
                FlowLayoutPanel targetSection = null;
                foreach (Control ctrl in flpSamplingAreas.Controls)
                {
                    if (ctrl is FlowLayoutPanel p && p.Tag?.ToString() == "ENV:" + envType)
                    {
                        targetSection = p;
                        break;
                    }
                }

                if (targetSection == null) return;

                // Thu thập dữ liệu từ DataGridView
                var areaNames = new List<string>();
                var resultRows = new List<EnvContract.Common.Helpers.PdfTestResultRow>();
                int stt = 1;

                foreach (Control child in targetSection.Controls)
                {
                    if (child is Panel areaPanel && areaPanel.Controls.OfType<DataGridView>().Any())
                    {
                        // Tìm tên khu vực
                        string areaName = $"KV{areaNames.Count + 1}";
                        var headerPnl = areaPanel.Controls.OfType<Guna2Panel>().FirstOrDefault();
                        if (headerPnl != null)
                        {
                            var lblName = headerPnl.Controls.OfType<Label>().FirstOrDefault(l => l.Font.Size >= 14);
                            if (lblName != null) areaName = lblName.Text;
                        }
                        areaNames.Add(areaName);

                        var dgv = areaPanel.Controls.OfType<DataGridView>().FirstOrDefault();
                        if (dgv != null)
                        {
                            foreach (DataGridViewRow row in dgv.Rows)
                            {
                                string paramName = row.Cells["ParamName"].Value?.ToString() ?? "";
                                string unit = row.Cells["Unit"].Value?.ToString() ?? "";
                                string resultValue = row.Cells["ResultValue"].Value?.ToString() ?? "---";
                                string qcvnLimit = row.Cells["QcvnLimit"].Value?.ToString() ?? "";

                                var existingRow = resultRows.FirstOrDefault(r => r.ParamName == paramName);
                                if (existingRow != null)
                                {
                                    existingRow.AreaResults[areaName] = resultValue;
                                }
                                else
                                {
                                    var pdfRow = new EnvContract.Common.Helpers.PdfTestResultRow
                                    {
                                        STT = stt++,
                                        ParamName = paramName,
                                        Unit = unit,
                                        Method = "",
                                        QcvnLimit = qcvnLimit
                                    };
                                    pdfRow.AreaResults[areaName] = resultValue;
                                    resultRows.Add(pdfRow);
                                }
                            }
                        }
                    }
                }

                string envLocalized = envType switch
                {
                    "Không khí" => LM.Get("planning_env_air"),
                    "Nước thải" => LM.Get("planning_env_water"),
                    "Đất"       => LM.Get("planning_env_soil"),
                    _           => envType
                };

                string filePath;
                if (_exportService != null && areaNames.Count > 0 && resultRows.Count > 0)
                {
                    filePath = await _exportService.ExportStructuredPdfAsync(
                        orderId:          contractId,
                        customerName:     customerName,
                        customerAddress:  "",
                        sampleType:       envLocalized,
                        areaNames:        areaNames,
                        resultRows:       resultRows,
                        outputDirectory:  outDir,
                        sampleDate:       _selectedContract.SampleDate,
                        analysisDate:     DateTime.Now,
                        returnDate:       DateTime.Now.AddDays(7));
                }
                else
                {
                    filePath = Path.Combine(outDir, $"{contractId}_{envType}.pdf");
                }

                MessageBox.Show(string.Format(LM.Get("director_msg_export_success"), filePath),
                    LM.Get("msg_success"), MessageBoxButtons.OK, MessageBoxIcon.Information);

                // Tự động mở file PDF
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = filePath,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[DirectorUC] Cannot open PDF: " + ex.Message); }
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(LM.Get("director_msg_export_fail"), ex.Message),
                    LM.Get("msg_error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
