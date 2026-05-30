using EnvContract.BLL.Interfaces;
using EnvContract.DTO.Entities;
using EnvContract.GUI.Forms.Main;
using EnvContract.GUI.Forms.Planning;
using EnvContract.GUI.Helpers;
using EnvContract.GUI.Services;
using Guna.UI2.WinForms;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using EnvContract.Common;

namespace EnvContract.GUI.UserControls.Planning
{
    public class SampleConfigUC : UserControl
    {
        private IPlanningService _planningService;
        private VoiceSearchService _voiceService;

        // Left panel
        private Guna2TextBox txtSearchContract;
        private FlowLayoutPanel flpContractList;
        private Panel _pnlListClip;

        // Right panel
        private Panel pnlRightContent;
        private Label lblContractId;
        private Label lblCustomerName;
        private Label lblContractBadge;
        private string _contractBadgeText = ""; // Used for custom drawing
        private Guna2ComboBox cboSampleType;
        private Guna2ComboBox cboAssignment;
        private Guna2Button btnAddEnvType;   // Nút + Thêm nền mẫu
        private FlowLayoutPanel flpSamplingAreas;
        private Guna2Panel pnlFilterWrap;
        private Guna2Panel pnlFrame;

        // Dynamic Language Labels
        private Label _lblTitle;
        private Label _lblSampleTypeLabel;
        private Label _lblAssignmentLabel;
        private Action _langHandler;

        // Data
        private List<ContractDisplayDTO> _allContracts = new List<ContractDisplayDTO>();
        private ContractDisplayDTO _selectedContract;
        private Panel _selectedContractPanel;
        private List<SampleParameterPlanDTO> _cachedParameters = new List<SampleParameterPlanDTO>();

        // Color constants from Figma
        private static readonly Color DarkGreen1 = Color.FromArgb(19, 42, 19);
        private static readonly Color DarkGreen2 = Color.FromArgb(49, 87, 44);
        private static readonly Color MediumGreen = Color.FromArgb(79, 119, 45);
        private static readonly Color LightGreenBg = Color.FromArgb(220, 237, 200);
        private static readonly Color YellowGreen = Color.FromArgb(236, 243, 158);
        private static readonly Color PageBg = Color.FromArgb(240, 244, 235);
        private static readonly Color InputBg = Color.FromArgb(226, 231, 220);
        private static readonly Color TableHeaderBg = Color.FromArgb(174, 196, 128); // Đậm hơn chút giống figma
        private static readonly Color TableRowAlt = Color.FromArgb(245, 248, 241);
        private static readonly Color BadgeGreen = Color.FromArgb(79, 119, 45);

        public SampleConfigUC()
        {
            _voiceService = Program.ServiceProvider?.GetService<VoiceSearchService>(); // Phải gán TRƯỚC InitializeComponent
            InitializeComponent();
            this.DoubleBuffered = true;
            if (!this.DesignMode)
            {
                _planningService = Program.ServiceProvider.GetRequiredService<IPlanningService>();
            }
            this.Disposed += (s, e) =>
            {
                if (_langHandler != null) LanguageManager.Instance.LanguageChanged -= _langHandler;
            };
        }

        // Tắt WS_EX_COMPOSITED vì nó quá nặng đối với UI phức tạp, gây High CPU
        // protected override CreateParams CreateParams ... (Đã gỡ bỏ)

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

            if (MainForm.IsReadOnlyForRole("R06"))
                ApplyReadOnlyToAllButtons();
        }

        private void ApplyReadOnlyToAllButtons()
        {
            ApplyReadOnlyRecursive(this);
        }

        private void ApplyReadOnlyRecursive(Control ctrl)
        {
            if (ctrl is Guna.UI2.WinForms.Guna2Button btn &&
                (btn.Text.Contains("Lưu")      || btn.Text.Contains("Thêm") ||
                 btn.Text.Contains("Xóa")      || btn.Text.Contains("Tạo") ||
                 btn.Text.Contains("Cập nhật")))
            {
                btn.Enabled = false;
                btn.FillColor = System.Drawing.Color.FromArgb(180, 180, 180);
                btn.ForeColor = System.Drawing.Color.FromArgb(100, 100, 100);
            }
            foreach (Control child in ctrl.Controls)
                ApplyReadOnlyRecursive(child);
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.Dock = DockStyle.Fill;
            this.BackColor = PageBg;
            this.Padding = new Padding(0);

            // === TITLE (Global cho toàn bộ màn hình) ===
            var pnlTitle = new Panel
            {
                Dock = DockStyle.Top,
                Height = 90, 
                BackColor = PageBg // Thay Transparent bằng PageBg để giảm flicker
            };
            var LM = LanguageManager.Instance;
            _lblTitle = new Label
            {
                Text = LM.Get("planning_title"),
                Font = new Font("Segoe UI", 26, FontStyle.Bold),
                ForeColor = Color.Black,
                AutoSize = true,
                BackColor = PageBg, // Thay Transparent
                Location = new Point(30, 30)
            };
            pnlTitle.Controls.Add(_lblTitle);

            // === LEFT SIDEBAR (fixed width, Dock.Left) ===
            var pnlLeftWrapper = new Panel
            {
                Dock = DockStyle.Left,
                Width = 295,
                Padding = new Padding(30, 10, 5, 15),
                BackColor = PageBg // Thay Transparent
            };

            var pnlLeft = new Guna2Panel
            {
                Dock = DockStyle.Fill,
                FillColor = LightGreenBg,
                BackColor = PageBg, // Thay Transparent mang lại hiệu năng cao hơn khi render border radius
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
                BorderColor = DarkGreen2,
                BorderThickness = 1,
                ForeColor = Color.Black
            };
            txtSearchContract.TextChanged += TxtSearchContract_TextChanged;
            pnlSearch.Controls.Add(txtSearchContract);
            VoiceSearchHelper.AttachVoiceButtonInPanel(txtSearchContract, pnlSearch, _voiceService,
                () => VoiceSearchHelper.ExtractCardContext(flpContractList, "ContractId", "CustomerName"));
            pnlLeft.Controls.Add(pnlSearch);

            _pnlListClip = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0), BackColor = LightGreenBg };
            flpContractList = new DoubleBufferedFlowLayoutPanel
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
                BackColor = PageBg, // Thay Transparent
                Visible = false // Ẩn tạm thời trong khi BuildLayout
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
            
            pnlFrame = new Guna2Panel // Sử dụng field
            {
                Dock = DockStyle.Fill,
                BorderRadius = 15,
                BorderThickness = 1,
                BorderColor = Color.FromArgb(170, 190, 150),
                FillColor = Color.White,
                BackColor = PageBg, // Đổi từ Transparent sang PageBg
                Padding = new Padding(5, 10, 5, 5),
                Visible = false // Ẩn lúc mới load
            };

            BuildRightHeader(pnlHeader, pnlFrame);
            pnlRightInner.Controls.Add(pnlFrame);
            pnlRightInner.Controls.Add(pnlHeader);

            flpSamplingAreas = new DoubleBufferedFlowLayoutPanel
            {
                Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(5, 5, 5, 5)
            };
            // Tắt thanh cuộn ngang — tránh xuất hiện khi scrollbar dọc xuất hiện
            flpSamplingAreas.AutoScroll = false;
            flpSamplingAreas.HorizontalScroll.Enabled = false;
            flpSamplingAreas.HorizontalScroll.Visible = false;
            flpSamplingAreas.AutoScroll = true;
            // Đăng ký Resize DUY NHẤT một lần ở đây cho toàn bộ khu vực
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

            pnlRightContent.Controls.Add(pnlRightInner);
            pnlRightWrapper.Controls.Add(pnlRightContent);

            // Add vào the controls TRONG MỘT LẦN VỚI THỨ TỰ Z-ORDER ĐÚNG NHẤT
            // 1. pnlRightWrapper (Fill)
            // 2. pnlLeftWrapper (Left)
            // 3. pnlTitle (Top)
            this.Controls.Add(pnlRightWrapper);
            this.Controls.Add(pnlLeftWrapper);
            this.Controls.Add(pnlTitle);

            pnlRightWrapper.Visible = true; // Hiện lại sau khi đã dựng xong
            this.ResumeLayout(false);

            _langHandler = () =>
            {
                if (this.IsDisposed) return;
                var lm = LanguageManager.Instance;
                if (_lblTitle != null) _lblTitle.Text = lm.Get("planning_title");
                if (txtSearchContract != null) txtSearchContract.PlaceholderText = lm.Get("planning_search");

                if (_lblSampleTypeLabel != null) _lblSampleTypeLabel.Text = lm.Get("plan_sp_env_type");
                if (_lblAssignmentLabel != null) _lblAssignmentLabel.Text = lm.Get("plan_sp_assign");

                // Reposition filter controls dynamically based on label widths
                if (_lblSampleTypeLabel != null && cboSampleType != null &&
                    _lblAssignmentLabel != null && cboAssignment != null)
                {
                    var fnt = _lblSampleTypeLabel.Font;
                    int lbl1W = TextRenderer.MeasureText(_lblSampleTypeLabel.Text, fnt).Width + 4;
                    int lbl2W = TextRenderer.MeasureText(_lblAssignmentLabel.Text, fnt).Width + 4;
                    int iconEnd = 44;  // icon at x=8, w=30, gap=6
                    int lbl1X = iconEnd;
                    int cbo1X = lbl1X + lbl1W + 4;
                    int lbl2X = cbo1X + cboSampleType.Width + 10;
                    int cbo2X = lbl2X + lbl2W + 4;
                    int wrapW = cbo2X + cboAssignment.Width + 10;
                    _lblSampleTypeLabel.Location = new Point(lbl1X, _lblSampleTypeLabel.Location.Y);
                    cboSampleType.Location       = new Point(cbo1X, cboSampleType.Location.Y);
                    _lblAssignmentLabel.Location = new Point(lbl2X, _lblAssignmentLabel.Location.Y);
                    cboAssignment.Location       = new Point(cbo2X, cboAssignment.Location.Y);
                    if (pnlFilterWrap != null) pnlFilterWrap.Width = Math.Max(wrapW, 400);
                }

                if (cboSampleType != null && cboSampleType.Items.Count >= 4)
                {
                    int sel1 = cboSampleType.SelectedIndex;
                    cboSampleType.Items.Clear();
                    cboSampleType.Items.AddRange(new object[] { lm.Get("plan_sp_all"), lm.Get("plan_sp_air"), lm.Get("plan_sp_water"), lm.Get("plan_sp_soil") });
                    if (sel1 >= 0 && sel1 <= 3) cboSampleType.SelectedIndex = sel1;
                }

                if (cboAssignment != null && cboAssignment.Items.Count >= 3)
                {
                    int sel2 = cboAssignment.SelectedIndex;
                    cboAssignment.Items.Clear();
                    cboAssignment.Items.AddRange(new object[] { lm.Get("plan_sp_all"), lm.Get("plan_sp_field"), lm.Get("plan_sp_lab") });
                    if (sel2 >= 0 && sel2 <= 2) cboAssignment.SelectedIndex = sel2;
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
                                        if (dgv.Columns.Contains("Department")) dgv.Columns["Department"].HeaderText = lm.Get("plan_sp_col_dept");
                                        if (dgv.Columns.Contains("QcvnLimit")) dgv.Columns["QcvnLimit"].HeaderText = lm.Get("plan_sp_col_qcvn_short");
                                        if (dgv.Columns.Contains("Actions")) dgv.Columns["Actions"].HeaderText = lm.Get("plan_sp_col_actions");
                                        dgv.Invalidate();
                                    }
                                }
                            }
                        }
                    }
                }

                // ── Cập nhật nút tĩnh "Thêm nền mẫu" (field-level) ──────────────
                if (btnAddEnvType != null)
                    btnAddEnvType.Text = lm.Get("plan_sp_btn_add_env");

                // ── Cập nhật nút động "Thêm khu vực" & "Thêm thông số" ──────────
                // Các nút này tạo mới mỗi lần LoadSamplingAreas() nên phải scan đệ quy
                UpdateDynamicButtonsText(lm);
            };
            LanguageManager.Instance.LanguageChanged += _langHandler;
            _langHandler();
        }

        /// <summary>
        /// Quét đệ quy toàn bộ flpSamplingAreas để cập nhật Text cho các nút
        /// được tạo động (btnAddArea, btnAddParam) khi ngôn ngữ thay đổi.
        /// Các nút tĩnh như btnAddEnvType được cập nhật trực tiếp trong _langHandler.
        /// </summary>
        private void UpdateDynamicButtonsText(LanguageManager lm)
        {
            if (flpSamplingAreas == null) return;
            UpdateButtonsInControl(flpSamplingAreas, lm);
        }

        private void UpdateButtonsInControl(Control parent, LanguageManager lm)
        {
            foreach (Control child in parent.Controls)
            {
                if (child is Guna.UI2.WinForms.Guna2Button btn)
                {
                    switch (btn.Name)
                    {
                        case "btnAddArea":  btn.Text = lm.Get("plan_sp_btn_add_area");  break;
                        case "btnAddParam": btn.Text = lm.Get("plan_sp_btn_add_param"); break;
                    }
                }
                // Đệ quy vào các container con
                if (child.HasChildren)
                    UpdateButtonsInControl(child, lm);
            }
        }

        private void BuildRightHeader(Panel pnlHeader, Guna2Panel pnlFrame)
        {
            // Row 1: Contract Info
            var pnlContractInfo = new Panel
            {
                Height = 85, // Đủ rộng cho Badge và text
                Dock = DockStyle.Top,
                Padding = new Padding(5, 5, 0, 0)
            };

            // Custom Avatar Badge (Nền nhạt, Viền đậm, Chữ đậm theo feedback)
            lblContractBadge = new Label
            {
                Text = "", // Keep text empty since we use Paint to custom draw
                Size = new Size(48, 48), 
                Location = new Point(5, 5),
                BackColor = Color.Transparent,
                Visible = false // Ẩn lúc mới load
            };
            lblContractBadge.Paint += (s, e) =>
            {
                var lbl = (Label)s;
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                // Fill background
                using (var brush = new SolidBrush(LightGreenBg))
                    e.Graphics.FillEllipse(brush, 0, 0, lbl.Width - 1, lbl.Height - 1);
                
                // Draw Border
                using (var pen = new Pen(DarkGreen2, 2f)) // Viền chữ xanh đậm
                    e.Graphics.DrawEllipse(pen, 1, 1, lbl.Width - 3, lbl.Height - 3);

                // Draw Text
                using (var font = new Font("Segoe UI", 12, FontStyle.Bold))
                {
                    // Thêm TextFormatFlags.NoPadding và chỉnh nhẹ Rectangle để chữ thực sự cân giữa vòng tròn
                    TextRenderer.DrawText(e.Graphics, _contractBadgeText, font, 
                        new Rectangle(0, 0, lbl.Width, lbl.Height), 
                        DarkGreen2, 
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                }
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
                Visible = false // Ẩn lúc mới load
            };
            pnlContractInfo.Controls.Add(lblContractId);

            lblCustomerName = new Label
            {
                Text = "", // Xóa câu hướng dẫn dư thừa
                Font = new Font("Segoe UI", 11),
                ForeColor = DarkGreen1,
                AutoSize = true,
                Location = new Point(67, 42), 
                BackColor = Color.Transparent,
                Visible = false // Ẩn lúc mới load
            };
            pnlContractInfo.Controls.Add(lblCustomerName);

            pnlHeader.Controls.Add(pnlContractInfo);

            // Row 2: Filter bar
            var pnlFilter = new Panel
            {
                Height = 48,
                Dock = DockStyle.Top,
                BackColor = Color.Transparent
            };

            // Wrapped Panel bo góc quanh Filter UI
            pnlFilterWrap = new Guna2Panel
            {
                FillColor = Color.FromArgb(245, 248, 241),
                BorderRadius = 15,
                BorderThickness = 1,
                BorderColor = Color.FromArgb(170, 190, 150),
                Size = new Size(600, 42),
                Location = new Point(5, 0),
                Visible = false // Ẩn lúc chưa chọn HD
            };

            // Icon phễu lọc (chỉ icon, không chữ)
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
                // Vẽ nền tròn
                using (var brush = new SolidBrush(MediumGreen))
                    e.Graphics.FillEllipse(brush, 0, 0, lbl.Width - 1, lbl.Height - 1);
                // Vẽ icon phễu lọc (funnel)
                using (var pen = new Pen(Color.White, 2f))
                {
                    // Phễu: tam giác trên + ống dưới
                    var points = new PointF[]
                    {
                        new PointF(7, 8),    // top-left
                        new PointF(23, 8),   // top-right
                        new PointF(17, 16),  // mid-right
                        new PointF(17, 22),  // bottom-right
                        new PointF(13, 22),  // bottom-left
                        new PointF(13, 16),  // mid-left
                    };
                    e.Graphics.FillPolygon(new SolidBrush(Color.White), points);
                }
            };
            pnlFilterWrap.Controls.Add(lblFilterIcon);

            var LM = LanguageManager.Instance;
            _lblSampleTypeLabel = new Label
            {
                Text = LM.Get("plan_sp_env_type"),
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
            cboSampleType.Items.AddRange(new object[] { LM.Get("plan_sp_all"), LM.Get("plan_sp_air"), LM.Get("plan_sp_water"), LM.Get("plan_sp_soil") });
            cboSampleType.SelectedIndex = 0;
            cboSampleType.SelectedIndexChanged += CboSampleType_SelectedIndexChanged;
            pnlFilterWrap.Controls.Add(cboSampleType);

            _lblAssignmentLabel = new Label
            {
                Text = LM.Get("plan_sp_assign"),
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.FromArgb(80, 80, 80),
                AutoSize = true,
                Location = new Point(320, 12),
                BackColor = Color.Transparent
            };
            pnlFilterWrap.Controls.Add(_lblAssignmentLabel);

            cboAssignment = new Guna2ComboBox
            {
                Location = new Point(418, 3),
                Size = new Size(165, 36),
                Font = new Font("Segoe UI", 10),
                BorderRadius = 15,
                FillColor = Color.White,
                BorderColor = Color.FromArgb(170, 190, 150),
                ForeColor = Color.Black
            };
            cboAssignment.Items.AddRange(new object[] { LM.Get("plan_sp_all"), LM.Get("plan_sp_field"), LM.Get("plan_sp_lab") });
            cboAssignment.SelectedIndex = 0;
            cboAssignment.SelectedIndexChanged += CboAssignment_SelectedIndexChanged;
            pnlFilterWrap.Controls.Add(cboAssignment);

            pnlFilter.Controls.Add(pnlFilterWrap);

            // Nút "+ Thêm nền mẫu" — bên phải thanh lọc
            btnAddEnvType = new Guna2Button
            {
                Text         = LM.Get("plan_sp_btn_add_env"),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = YellowGreen,
                FillColor = DarkGreen2,
                Size = new Size(160, 38),
                BorderRadius = 12,
                Cursor = Cursors.Hand,
                Visible = false,
                Anchor       = AnchorStyles.Top | AnchorStyles.Right

            };

            btnAddEnvType.Click += BtnAddEnvType_Click;
            pnlFilter.Controls.Add(btnAddEnvType);


            // Lu\u00f4n c\u1eadp nh\u1eadt v\u1ecb tr\u00ed n\u00fat s\u00e1t c\u1ea1nh ph\u1ea3i khi panel thay \u0111\u1ed5i k\u00edch th\u01b0\u1edbc
            pnlFilter.Resize += (s, e) =>
            {
                if (btnAddEnvType != null)
                    btnAddEnvType.Location = new Point(pnlFilter.Width - btnAddEnvType.Width - 10, 5);
            };
            pnlFrame.Controls.Add(pnlFilter);
        }

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

            // Hiện các thành phần Header sau khi HD được Resolve
            lblContractBadge.Visible = true;
            lblContractId.Visible = true;
            lblCustomerName.Visible = true;
            if (pnlFilterWrap != null) pnlFilterWrap.Visible = true;
            if (pnlFrame != null) pnlFrame.Visible = true;
            if (btnAddEnvType != null) btnAddEnvType.Visible = true;

            _contractBadgeText = contract.ContractId.Length > 2
                ? contract.ContractId.Substring(0, 2)
                : contract.ContractId;
            lblContractBadge.Invalidate(); // trigger repaint for avatar

            lblContractId.Text = contract.ContractId;
            lblCustomerName.Text = contract.CustomerName;

            await RefreshCachedParameters();
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

        private async Task RefreshCachedParameters()
        {
            // Dùng SelectedIndex — map sang DB value (không phụ thuộc ngôn ngữ)
            // 0=All/Tất cả, 1=Không khí/Air, 2=Nước thải/Wastewater, 3=Đất/Soil
            string[] dbEnvTypes = { "Không khí", "Không khí", "Nước thải", "Đất" };
            int idx = cboSampleType?.SelectedIndex ?? 0;
            var envType = (idx >= 0 && idx < dbEnvTypes.Length) ? dbEnvTypes[idx] : "Không khí";
            try
            {
                _cachedParameters = await _planningService.GetParametersForPlanAsync(envType);
                if (_cachedParameters == null)
                {
                    _cachedParameters = new List<SampleParameterPlanDTO>();
                }
            }
            catch
            {
                _cachedParameters = new List<SampleParameterPlanDTO>();
            }
        }

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

        // ─────────────────────────────────────────────────────────────────────
        // THÊM NỀN MẪU — ContextMenu 3 lựa chọn, disable nếu đã tồn tại
        // ─────────────────────────────────────────────────────────────────────
        private static readonly string[] DefaultEnvTypes = { "Không khí", "Nước thải", "Đất" };

        private void BtnAddEnvType_Click(object sender, EventArgs e)
        {
            if (_selectedContract == null) return;

            // Lấy danh sách nền mẫu đã tồn tại từ Tag các section panel
            var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Control ctrl in flpSamplingAreas.Controls)
            {
                if (ctrl.Tag is string tag && tag.StartsWith("ENV:"))
                    existing.Add(tag.Substring(4));
            }

            var LM = LanguageManager.Instance;
            // Nếu tất cả đã tồn tại → thông báo và thoát
            if (existing.Count >= DefaultEnvTypes.Length)
            {
                MessageBox.Show(
                    LM.Get("plan_sp_msg_all_env_added"),
                    LM.Get("msg_info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Xây ContextMenuStrip — item nào đã có thì disable và đổi màu
            var menu = new ContextMenuStrip();
            menu.Font = new Font("Segoe UI", 10);

            foreach (var envType in DefaultEnvTypes)
            {
                bool alreadyExists = existing.Contains(envType);
                string icon = envType switch
                {
                    "Không khí" => "🌬  ",
                    "Nước thải" => "💧  ",
                    "Đất"       => "🌱  ",
                    _           => "📌  "
                };

                var item = new ToolStripMenuItem(icon + envType)
                {
                    Enabled     = !alreadyExists,
                    ToolTipText = alreadyExists
                        ? string.Format(LM.Get("plan_sp_msg_env_added"), envType)
                        : string.Format(LM.Get("plan_sp_msg_add_env"), envType)
                };

                if (alreadyExists)
                    item.ForeColor = Color.FromArgb(160, 160, 160);

                string capturedEnv = envType;
                item.Click += async (s2, e2) => await AddNewEnvTypeSection(capturedEnv);
                menu.Items.Add(item);
            }

            // Hiện menu ngay dưới nút
            menu.Show(btnAddEnvType, new Point(0, btnAddEnvType.Height));
        }

        private async Task AddNewEnvTypeSection(string envType)
        {
            if (_selectedContract == null) return;
            try
            {
                // Tạo khu vực đầu tiên cho nền mẫu mới
                await _planningService.CreateSamplingAreaAsync(
                    _selectedContract.ContractId,
                    "Khu vực 1",
                    envType);

                // Reload toàn bộ danh sách
                await LoadSamplingAreas(_selectedContract.ContractId);
            }
            catch (Exception ex)
            {
                var LM = LanguageManager.Instance;
                MessageBox.Show(
                    string.Format(LM.Get("plan_sp_err_add_env"), envType, ex.Message),
                    LM.Get("msg_error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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

            var LM = LanguageManager.Instance;
            var lblSectionCount = new Label
            {
                Name = "lblEnvCount",
                Tag = orders.Count.ToString(),
                Text = string.Format(LM.Get("plan_sp_area_count"), orders.Count),
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
                var parameters = await _planningService.GetParametersByOrderAsync(order.OrderID);
                string locationName = !string.IsNullOrEmpty(order.OrderName) ? order.OrderName : $"{LM.Get("plan_sp_area_1").Replace("1", areaIndex.ToString())}";
                var areaPanel = CreateSamplingAreaPanel(areaIndex, locationName, order.OrderID, parameters);
                areaPanel.Margin = new Padding(0); 
                sectionPanel.Controls.Add(areaPanel);
                areaIndex++;
            }

            var btnAddInSection = new Guna.UI2.WinForms.Guna2Button
            {
                Name = "btnAddArea",
                Text = LM.Get("plan_sp_btn_add_area"),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.White,
                FillColor = Color.FromArgb(79, 119, 45),
                Size = new Size(175, 36),
                BorderRadius = 10,
                Cursor = Cursors.Hand,
                Margin = new Padding(15, 10, 0, 15)
            };

            btnAddInSection.Click += async (s, e) =>
            {
                if (_selectedContract == null) return;
                var form = new EnvContract.GUI.Forms.Planning.AddAreaForm();
                if (form.ShowDialog(this.FindForm()) == DialogResult.OK)
                {
                    try
                    {
                        await _planningService.CreateSamplingAreaAsync(_selectedContract.ContractId, form.AreaName, envType);
                        await LoadSamplingAreas(_selectedContract.ContractId);
                    }
                    catch (Exception ex)
                    {
                        var ErrorLM = LanguageManager.Instance;
                        MessageBox.Show(string.Format(ErrorLM.Get("plan_sp_msg_err_add_area"), ex.Message), ErrorLM.Get("msg_error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            };

            sectionPanel.Controls.Add(btnAddInSection);
            return sectionPanel;
        }

        private Panel CreateSamplingAreaPanel(int index, string locationName, string orderId, List<SampleParameterPlanDTO> initialParams = null)
        {
            var pnlArea = new Panel
            {
                AutoSize = false,
                Margin = new Padding(0, 5, 0, 15),
                Padding = new Padding(0),
                Tag = orderId
            };

            // ---- HEADER (Bọc nền nhạt bo góc xanh lá) ----
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
                Location = new Point(15, 13), // Căn Y=13 đồng bộ với tiêu đề
                BackColor = Color.Transparent
            };
            lblBadge.Paint += (s, e) =>
            {
                var lbl = (Label)s;
                var g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
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

            var LM = LanguageManager.Instance;
            var btnAddParam = new Guna2Button
            {
                Name = "btnAddParam",
                Text = LM.Get("plan_sp_btn_add_param"),
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = DarkGreen1,
                FillColor = Color.White,
                BorderColor = DarkGreen1,
                BorderThickness = 1,
                Size = new Size(180, 36),
                BorderRadius = 10,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Right | AnchorStyles.Top,
                BackColor = Color.Transparent
            };

            var btnClose = new Guna2Button
            {
                Text = LM.Get("plan_sp_btn_del"), 
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = Color.Black,
                FillColor = Color.Transparent, 
                HoverState = { FillColor = Color.Transparent, ForeColor = Color.Black },
                Size = new Size(50, 36), 
                BorderRadius = 10,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Right | AnchorStyles.Top,
                Padding = new Padding(0),
                BackColor = Color.Transparent,
                UseTransparentBackground = true
            };

            pnlAreaHeader.Resize += (s, e) =>
            {
                btnClose.Location = new Point(pnlAreaHeader.Width - 60, 9);
                int btnAddParamX = pnlAreaHeader.Width - 290;
                btnAddParam.Location = new Point(btnAddParamX, 9);
            };
            pnlAreaHeader.Controls.Add(btnAddParam);
            pnlAreaHeader.Controls.Add(btnClose);

            pnlArea.Controls.Add(pnlAreaHeader);

            // ---- DATA TABLE ----
            var dgv = CreateParameterDataGridView();
            pnlArea.Controls.Add(dgv);
            dgv.BringToFront();

            // NẠP DỮ LIỆU: Chỉ nạp khi có initialParams từ DB (khu vực đã lưu trước đó)
            // Khu vực mới tạo sẽ trống — user tự thêm thông số qua nút "Thêm thông số"
            if (initialParams != null && initialParams.Count > 0)
            {
                dgv.Rows.Clear();
                foreach (var p in initialParams)
                {
                    int rowIdx = dgv.Rows.Add();
                    var row = dgv.Rows[rowIdx];
                    row.Cells["ParamName"].Value = p.ParamName;
                    row.Cells["Unit"].Value = p.Unit;
                    row.Cells["Department"].Value = DeptToUI(p.Department);
                    row.Cells["QcvnLimit"].Value = p.QcvnLimit;
                    row.Cells["ParamID"].Value = p.ParamID;
                    row.Cells["RegulationID"].Value = p.RegulationID;
                    row.Tag = p;
                }
            }

            RecalculateAreaPanelSize(pnlArea, dgv);

            pnlArea.Width = Math.Max(flpSamplingAreas.ClientSize.Width - 10, 200);
            dgv.Width = pnlArea.Width;
            pnlAreaHeader.Dock = DockStyle.Top; 
            dgv.Dock = DockStyle.Top;

            btnAddParam.Click += async (s, e) =>
            {
                var currentItemsInGrid = new List<SampleParameterPlanDTO>();
                foreach (DataGridViewRow row in dgv.Rows)
                {
                    currentItemsInGrid.Add(new SampleParameterPlanDTO
                    {
                        ParamID = row.Cells["ParamID"].Value?.ToString() ?? "",
                        ParamName = row.Cells["ParamName"].Value?.ToString() ?? "",
                        Unit = row.Cells["Unit"].Value?.ToString() ?? "",
                        Department = DeptToDB(row.Cells["Department"].Value?.ToString() ?? ""),
                        QcvnLimit = row.Cells["QcvnLimit"].Value?.ToString() ?? ""
                    });
                }

                var form = new AddParameterForm(_cachedParameters, currentItemsInGrid);
                if (form.ShowDialog(this.FindForm()) == DialogResult.OK)
                {
                    // Lấy danh sách ParamID hiện tại trong grid
                    var existingIds = new HashSet<string>();
                    foreach (DataGridViewRow existingRow in dgv.Rows)
                    {
                        var id = existingRow.Cells["ParamID"].Value?.ToString() ?? "";
                        if (!string.IsNullOrEmpty(id)) existingIds.Add(id);
                    }

                    // Xóa các thông số đã bỏ chọn
                    var selectedIds = new HashSet<string>(form.SelectedParameters.Select(p => p.ParamID));
                    for (int i = dgv.Rows.Count - 1; i >= 0; i--)
                    {
                        var rowId = dgv.Rows[i].Cells["ParamID"].Value?.ToString() ?? "";
                        if (!string.IsNullOrEmpty(rowId) && !selectedIds.Contains(rowId))
                            dgv.Rows.RemoveAt(i);
                    }

                    // Thêm những thông số mới (chưa có trong grid)
                    foreach (var p in form.SelectedParameters)
                    {
                        if (!existingIds.Contains(p.ParamID))
                        {
                            int rowIdx = dgv.Rows.Add();
                            var row = dgv.Rows[rowIdx];
                            row.Cells["ParamName"].Value = p.ParamName;
                            row.Cells["Unit"].Value = p.Unit;
                            row.Cells["Department"].Value = DeptToUI(p.Department);
                            row.Cells["QcvnLimit"].Value = p.QcvnLimit;
                            row.Cells["ParamID"].Value = p.ParamID;
                            row.Cells["RegulationID"].Value = p.RegulationID;
                        }
                    }

                    RecalculateAreaPanelSize(pnlArea, dgv);
                    ApplyAssignmentFilter(dgv);

                    if (dgv.Rows.Count > 0)
                    {
                        int lastVisibleIdx = -1;
                        for (int i = dgv.Rows.Count - 1; i >= 0; i--)
                        {
                            if (dgv.Rows[i].Visible) { lastVisibleIdx = i; break; }
                        }
                        if (lastVisibleIdx >= 0)
                            dgv.FirstDisplayedScrollingRowIndex = lastVisibleIdx;
                        dgv.ClearSelection();
                        dgv.CurrentCell = null;
                    }

                    // Auto-save sau khi thêm thông số
                    await AutoSaveAreaAsync(pnlArea, dgv);
                }
            };

            // CellClick cho cột Thao tác (Xóa)
            dgv.CellClick += async (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                string colName = dgv.Columns[e.ColumnIndex].Name;

                if (colName == "Actions")
                {
                    var paramName = dgv.Rows[e.RowIndex].Cells["ParamName"].Value?.ToString() ?? "thông số này";
                    var LM_Btn = LanguageManager.Instance;
                    var confirm = MessageBox.Show(
                        string.Format(LM_Btn.Get("plan_sp_msg_del_param"), paramName),
                        LM_Btn.Get("msg_confirm_del"),
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);
                    if (confirm != DialogResult.Yes) return;
                    dgv.Rows.RemoveAt(e.RowIndex);
                    RecalculateAreaPanelSize(pnlArea, dgv);
                    await AutoSaveAreaAsync(pnlArea, dgv);
                }
            };

            btnClose.Click += async (s, e) =>
            {
                var LM_Btn = LanguageManager.Instance;
                var result = MessageBox.Show(string.Format(LM_Btn.Get("plan_sp_msg_del_area"), locationName), LM_Btn.Get("msg_confirm"),
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                {
                    try
                    {
                        await _planningService.DeleteSamplingAreaAsync(orderId);
                        flpSamplingAreas.Controls.Remove(pnlArea);
                        pnlArea.Dispose();
                        RenumberAreas();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error deleting area: {ex.Message}");
                    }
                }
            };

            // Auto-save khi đổi Phòng ban hoặc sửa QCVN + auto-replace <=→≤, >=→≥
            dgv.CellEndEdit += async (s, e) =>
            {
                if (e.ColumnIndex < 0) return;
                string colName = dgv.Columns[e.ColumnIndex].Name;

                if (colName == "QcvnLimit")
                {
                    // Auto-replace ký tự
                    string val = dgv.Rows[e.RowIndex].Cells["QcvnLimit"].Value?.ToString() ?? "";
                    val = val.Replace("<=", "≤").Replace(">=", "≥");
                    dgv.Rows[e.RowIndex].Cells["QcvnLimit"].Value = val;
                    await AutoSaveAreaAsync(pnlArea, dgv);
                }
                else if (colName == "Department")
                {
                    await AutoSaveAreaAsync(pnlArea, dgv);
                }
            };

            return pnlArea;
        }

        private void RecalculateAreaPanelSize(Panel pnlArea, DataGridView dgv)
        {
            int rowCount = dgv.Rows.Count;
            int tableHeight = Math.Max(rowCount * 38 + 45, 120);
            dgv.Height = tableHeight;
            // AreaHeight = Header(55) + tableHeight + BottomPadding(10)
            pnlArea.Height = 55 + tableHeight + 10;
        }

        private void RenumberAreas()
        {
            int idx = 1;
            foreach (Control ctrl in flpSamplingAreas.Controls)
            {
                if (ctrl is Panel areaPanel)
                {
                    foreach (Control headerCtrl in areaPanel.Controls)
                    {
                        if (headerCtrl is Guna2Panel headerPanel)
                        {
                            foreach (Control c in headerPanel.Controls)
                            {
                                if (c is Label lbl && lbl.Size == new Size(30, 30) && lbl.BackColor == DarkGreen2)
                                {
                                    lbl.Text = idx.ToString();
                                    break;
                                }
                            }
                            break;
                        }
                    }
                    idx++;
                }
            }
        }

        // Map DB value → UI short label (always returns "HT" or "PTN")
        private static string DeptToUI(string dbValue)
        {
            if (string.IsNullOrWhiteSpace(dbValue)) return "HT";
            return dbValue.Trim() switch
            {
                "Hiện trường"  => "HT",
                "Thí nghiệm"  => "PTN",
                "HT"           => "HT",
                "PTN"          => "PTN",
                _              => "HT"   // fallback an toàn, tránh crash ComboBox
            };
        }

        // Map UI short label → DB value
        private static string DeptToDB(string uiValue)
        {
            return uiValue switch
            {
                "HT"  => "Hiện trường",
                "PTN" => "Thí nghiệm",
                _     => "Hiện trường"  // fallback
            };
        }

        private DataGridView CreateParameterDataGridView()
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
                GridColor = Color.White,
                CellBorderStyle = DataGridViewCellBorderStyle.None,
                RowTemplate = { Height = 38 },
                ScrollBars = ScrollBars.None,
                EnableHeadersVisualStyles = false,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.White,
                    ForeColor = Color.Black,
                    SelectionBackColor = Color.FromArgb(210, 233, 185),
                    SelectionForeColor = Color.Black
                },
                AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(220, 237, 200),
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

            // Màu xen kẽ
            var rowColorEven = Color.White;
            var rowColorOdd = Color.FromArgb(220, 237, 200);
            var separatorColor = Color.FromArgb(200, 218, 180);
            var headerSepColor = Color.FromArgb(160, 185, 140);

            // Full custom painting — tự vẽ 100%, không dùng border mặc định
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
                bool isSelected = (e.State & DataGridViewElementStates.Selected) != 0;
                if (isSelected)
                    bgColor = Color.FromArgb(170, 205, 140);

                // Vẽ nền
                using (var brush = new SolidBrush(bgColor))
                    e.Graphics.FillRectangle(brush, e.CellBounds);

                string colName = dgv.Columns[e.ColumnIndex].Name;

                // === DEPARTMENT: Text + dropdown arrow ===
                if (colName == "Department")
                {
                    string rawVal = e.Value?.ToString() ?? "";
                    // Dịch HT/PTN sang ngôn ngữ hiện tại khi vẽ
                    string text = rawVal switch
                    {
                        "HT"  => LanguageManager.Instance.Get("plan_sp_field"),
                        "PTN" => LanguageManager.Instance.Get("plan_sp_lab"),
                        _     => rawVal
                    };
                    if (!string.IsNullOrEmpty(rawVal))
                    {
                        TextRenderer.DrawText(e.Graphics, text, dgv.Font,
                            new Rectangle(e.CellBounds.X + 5, e.CellBounds.Y, e.CellBounds.Width - 22, e.CellBounds.Height),
                            Color.Black, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);

                        int arrowX = e.CellBounds.Right - 18;
                        int arrowY = e.CellBounds.Y + e.CellBounds.Height / 2 - 2;
                        using (var pen = new Pen(Color.FromArgb(130, 130, 130), 1.5f))
                        {
                            e.Graphics.DrawLine(pen, arrowX, arrowY, arrowX + 4, arrowY + 4);
                            e.Graphics.DrawLine(pen, arrowX + 4, arrowY + 4, arrowX + 8, arrowY);
                        }
                    }
                }
                // === ACTIONS: Nút Xóa ===
                else if (colName == "Actions")
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    var smallFont = new Font("Segoe UI", 8.5f, FontStyle.Bold);
                    int btnW = 60, btnH = 24;
                    int startX = e.CellBounds.X + (e.CellBounds.Width - btnW) / 2;
                    int btnY = e.CellBounds.Y + (e.CellBounds.Height - btnH) / 2;

                    var delRect = new Rectangle(startX, btnY, btnW, btnH);
                    using (var path = RoundedRect(delRect, 6))
                    {
                        using (var b = new SolidBrush(Color.FromArgb(255, 235, 235))) e.Graphics.FillPath(b, path);
                        using (var p = new Pen(Color.FromArgb(210, 130, 130), 1f)) e.Graphics.DrawPath(p, path);
                    }
                    TextRenderer.DrawText(e.Graphics, LanguageManager.Instance.Get("msg_del"), smallFont, delRect, Color.FromArgb(180, 50, 50),
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    smallFont.Dispose();
                    e.Graphics.SmoothingMode = SmoothingMode.Default;
                }
                // === Các cột khác: text bình thường ===
                else
                {
                    string text = e.Value?.ToString() ?? "";
                    var cellFont = e.CellStyle.Font ?? dgv.Font;
                    Color textColor = e.CellStyle.ForeColor != Color.Empty ? e.CellStyle.ForeColor : Color.Black;

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

                // === Đường ngang (CHỈ ngang, KHÔNG dọc) ===
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

            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ParamName",
                HeaderText = LanguageManager.Instance.Get("plan_sp_col_name"),
                FillWeight = 28,
                ReadOnly = true,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Padding = new Padding(8, 0, 8, 0)
                }
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

            var cboDeptCol = new DataGridViewComboBoxColumn
            {
                Name = "Department",
                HeaderText = LanguageManager.Instance.Get("plan_sp_col_dept"),
                FillWeight = 18,
                FlatStyle = FlatStyle.Flat,
                DisplayStyle = DataGridViewComboBoxDisplayStyle.Nothing,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter
                }
            };
            cboDeptCol.Items.AddRange("HT", "PTN");
            dgv.Columns.Add(cboDeptCol);

            // ── Suppress DataGridViewComboBoxCell invalid-value error ──
            // Xảy ra khi Cell.Value không khớp Items list (e.g. dữ liệu cũ từ DB)
            dgv.DataError += (s, e) =>
            {
                if (e.ColumnIndex >= 0 &&
                    dgv.Columns[e.ColumnIndex].Name == "Department" &&
                    (e.Context & DataGridViewDataErrorContexts.Display) != 0)
                {
                    // Clamp giá trị về "HT" nếu không hợp lệ
                    try
                    {
                        var cell = dgv.Rows[e.RowIndex].Cells["Department"];
                        string cur = cell.Value?.ToString() ?? "";
                        cell.Value = DeptToUI(cur);
                    }
                    catch (InvalidOperationException) { /* DeptToUI conversion skipped on invalid cell data */ }
                    e.ThrowException = false;  // KHÔNG throw, KHÔNG hiện dialog
                }
                else
                {
                    e.ThrowException = false;  // suppress tất cả để tránh Default Error Dialog
                }
            };

            // Khi chọn giá trị mới → commit ngay lập tức
            dgv.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (dgv.IsCurrentCellDirty && dgv.CurrentCell?.OwningColumn?.Name == "Department")
                {
                    dgv.CommitEdit(DataGridViewDataErrorContexts.Commit);
                    dgv.BeginInvoke(new Action(() =>
                    {
                        dgv.EndEdit();
                        dgv.CurrentCell = null;
                    }));
                }
            };

            dgv.EditingControlShowing += (s, e) =>
            {
                if (dgv.CurrentCell?.OwningColumn?.Name == "Department" && e.Control is ComboBox cb)
                {
                    cb.FlatStyle = FlatStyle.Flat;
                    cb.BackColor = Color.FromArgb(228, 238, 218);
                    cb.ForeColor = Color.Black;

                    if (cb.Parent != null)
                    {
                        bool isAlt = dgv.CurrentCell.RowIndex % 2 == 1;
                        cb.Parent.BackColor = isAlt ? rowColorOdd : Color.White;
                    }
                }
                else if (e.Control is TextBox tb)
                {
                    Color selColor = Color.FromArgb(210, 233, 185);
                    e.CellStyle.BackColor = selColor;
                    e.CellStyle.SelectionBackColor = selColor;

                    tb.BackColor = selColor;
                    tb.ForeColor = Color.Black;
                    tb.BorderStyle = BorderStyle.None;

                    if (tb.Parent != null)
                    {
                        tb.Parent.BackColor = selColor;
                    }
                }
            };

            dgv.CellClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.ColumnIndex >= 0 && dgv.Columns[e.ColumnIndex].Name == "Department")
                {
                    dgv.BeginEdit(true);
                    dgv.BeginInvoke(new Action(() =>
                    {
                        if (dgv.EditingControl is ComboBox cb && !cb.DroppedDown)
                            cb.DroppedDown = true;
                    }));
                }
            };

            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "QcvnLimit",
                HeaderText = LanguageManager.Instance.Get("plan_sp_col_qcvn_short"),
                FillWeight = 18,
                ReadOnly = false,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    ForeColor = Color.Black
                }
            });

            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ParamID",
                HeaderText = "",
                Visible = false
            });

            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "RegulationID",
                HeaderText = "",
                Visible = false
            });

            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Actions",
                HeaderText = LanguageManager.Instance.Get("plan_sp_col_actions"),
                FillWeight = 14,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });

            return dgv;
        }

        private static GraphicsPath RoundedRect(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private string ShowQcvnEditPopup(string currentValue, DataGridView dgv, int rowIndex, int colIndex)
        {
            var cellRect = dgv.GetCellDisplayRectangle(colIndex, rowIndex, true);
            var screenPos = dgv.PointToScreen(new Point(cellRect.X, cellRect.Bottom));

            var dlg = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.Manual,
                Size = new Size(280, 125),
                BackColor = Color.White,
                ShowInTaskbar = false
            };
            dlg.Location = screenPos;
            dlg.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(170, 190, 150), 1.5f))
                    e.Graphics.DrawRectangle(pen, 0, 0, dlg.Width - 1, dlg.Height - 1);
            };

            var LM = LanguageManager.Instance;
            // Operator dropdown
            var cboOp = new ComboBox
            {
                Location = new Point(10, 12), Size = new Size(80, 28),
                Font = new Font("Segoe UI", 10), DropDownStyle = ComboBoxStyle.DropDownList
            };
            cboOp.Items.AddRange(new object[] { "≤", "≥", LM.Get("plan_sp_range"), "=" });

            // Value 1
            var txtVal1 = new Guna2TextBox
            {
                Location = new Point(100, 10), Size = new Size(80, 32),
                Font = new Font("Segoe UI", 10), ForeColor = Color.Black,
                BorderRadius = 8, FillColor = Color.White,
                BorderColor = Color.FromArgb(170, 190, 150),
                PlaceholderText = LM.Get("plan_sp_val")
            };

            // Label dash
            var lblDash = new Label
            {
                Text = "–", Font = new Font("Segoe UI", 12, FontStyle.Bold),
                Location = new Point(185, 14), AutoSize = true, Visible = false
            };

            // Value 2 (for range)
            var txtVal2 = new Guna2TextBox
            {
                Location = new Point(200, 10), Size = new Size(70, 32),
                Font = new Font("Segoe UI", 10), ForeColor = Color.Black,
                BorderRadius = 8, FillColor = Color.White,
                BorderColor = Color.FromArgb(170, 190, 150),
                PlaceholderText = LM.Get("plan_sp_max"), Visible = false
            };

            // Parse current value
            if (currentValue.StartsWith("≤"))
            {
                cboOp.SelectedIndex = 0;
                txtVal1.Text = currentValue.Replace("≤", "").Trim();
            }
            else if (currentValue.StartsWith("≥"))
            {
                cboOp.SelectedIndex = 1;
                txtVal1.Text = currentValue.Replace("≥", "").Trim();
            }
            else if (currentValue.Contains(" - "))
            {
                cboOp.SelectedIndex = 2;
                var parts = currentValue.Split(new[] { " - " }, StringSplitOptions.None);
                txtVal1.Text = parts[0].Trim();
                if (parts.Length > 1) txtVal2.Text = parts[1].Trim();
                lblDash.Visible = true;
                txtVal2.Visible = true;
            }
            else
            {
                cboOp.SelectedIndex = 3;
                txtVal1.Text = currentValue.Replace("=", "").Trim();
            }

            cboOp.SelectedIndexChanged += (s, e) =>
            {
                bool isRange = cboOp.SelectedIndex == 2;
                lblDash.Visible = isRange;
                txtVal2.Visible = isRange;
                txtVal1.PlaceholderText = isRange ? LM.Get("plan_sp_min") : LM.Get("plan_sp_val");
                txtVal1.Size = new Size(isRange ? 65 : 160, 32);
            };
            // Trigger initial layout
            cboOp.SelectedIndex = cboOp.SelectedIndex;

            string result = null;

            var btnOk = new Guna2Button
            {
                Text = "✓", Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = Color.White, FillColor = DarkGreen2,
                Size = new Size(50, 34), Location = new Point(10, 52),
                BorderRadius = 8, Cursor = Cursors.Hand
            };
            btnOk.Click += (s, e) =>
            {
                string v1 = txtVal1.Text.Trim();
                switch (cboOp.SelectedIndex)
                {
                    case 0: result = string.IsNullOrEmpty(v1) ? "" : $"≤ {v1}"; break;
                    case 1: result = string.IsNullOrEmpty(v1) ? "" : $"≥ {v1}"; break;
                    case 2:
                        string v2 = txtVal2.Text.Trim();
                        result = (string.IsNullOrEmpty(v1) && string.IsNullOrEmpty(v2)) ? "" : $"{v1} - {v2}";
                        break;
                    case 3: result = v1; break;
                }
                dlg.Close();
            };

            var btnCancel = new Guna2Button
            {
                Text = "✕", Font = new Font("Segoe UI", 10),
                ForeColor = Color.FromArgb(150, 60, 60), FillColor = Color.FromArgb(255, 240, 240),
                Size = new Size(40, 34), Location = new Point(65, 52),
                BorderRadius = 8, Cursor = Cursors.Hand,
                BorderColor = Color.FromArgb(210, 150, 150), BorderThickness = 1
            };
            btnCancel.Click += (s, e) => dlg.Close();

            dlg.Controls.AddRange(new Control[] { cboOp, txtVal1, lblDash, txtVal2, btnOk, btnCancel });
            dlg.Deactivate += (s, e) => dlg.Close();
            dlg.ShowDialog(dgv.FindForm());

            return result;
        }

        private void LoadParametersFromCache(DataGridView dgv)
        {
            dgv.Rows.Clear();
            foreach (var p in _cachedParameters)
            {
                int rowIdx = dgv.Rows.Add();
                var row = dgv.Rows[rowIdx];
                row.Cells["ParamName"].Value = p.ParamName;
                row.Cells["Unit"].Value = p.Unit;
                row.Cells["Department"].Value = DeptToUI(p.Department);
                row.Cells["QcvnLimit"].Value = p.QcvnLimit;
                row.Cells["ParamID"].Value = p.ParamID;
            }
            ApplyAssignmentFilter(dgv);

            // Bỏ chọn ô đầu tiên để không bị đổi màu khi vừa vào form
            dgv.ClearSelection();
            dgv.CurrentCell = null;
        }

        private void ApplyAssignmentFilter(DataGridView dgv)
        {
            int filterIdx = cboAssignment?.SelectedIndex ?? 0;
            
            try
            {
                if (dgv.IsCurrentCellInEditMode) dgv.EndEdit();
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[SampleConfigUC] Error: {ex.Message}"); }

            var rowVisibility = new bool[dgv.Rows.Count];
            int firstVisibleIndex = -1;
            for (int i = 0; i < dgv.Rows.Count; i++)
            {
                if (filterIdx == 0) rowVisibility[i] = true;
                else
                {
                    var dept = dgv.Rows[i].Cells["Department"].Value?.ToString() ?? "";
                    // idx 1 = Field (HT), idx 2 = Lab (PTN)
                    rowVisibility[i] = (filterIdx == 1 && dept == "HT") || (filterIdx == 2 && dept == "PTN");
                }
                if (rowVisibility[i] && firstVisibleIndex == -1) firstVisibleIndex = i;
            }

            try
            {
                if (firstVisibleIndex != -1) dgv.CurrentCell = dgv.Rows[firstVisibleIndex].Cells[0];
                else dgv.CurrentCell = null;
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[SampleConfigUC] Error: {ex.Message}"); }

            for (int i = 0; i < dgv.Rows.Count; i++)
            {
                if (dgv.Rows[i].Visible != rowVisibility[i])
                {
                    try { dgv.Rows[i].Visible = rowVisibility[i]; } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[SampleConfigUC] Error: {ex.Message}"); }
                }
            }
        }

        private void ApplyAssignmentFilterAllAreas()
        {
            this.Focus();
            foreach (Control ctrl in flpSamplingAreas.Controls)
            {
                if (ctrl is FlowLayoutPanel sectionPanel)
                {
                    foreach (Control child in sectionPanel.Controls)
                    {
                        if (child is Panel areaPanel)
                        {
                            var dgv = FindDgvInPanel(areaPanel);
                            if (dgv != null)
                            {
                                ApplyAssignmentFilter(dgv);
                            }
                        }
                    }
                }
            }
            flpSamplingAreas.PerformLayout();
        }

        private DataGridView FindDgvInPanel(Panel panel)
        {
            foreach (Control c in panel.Controls)
            {
                if (c is DataGridView dgv) return dgv;
            }
            return null;
        }

        private async void CboSampleType_SelectedIndexChanged(object sender, EventArgs e)
        {
            await RefreshCachedParameters();
            ApplySampleTypeFilter();
        }

        private void ApplySampleTypeFilter()
        {
            // Dùng SelectedIndex — map sang DB value (không phụ thuộc ngôn ngữ)
            // 0=Tất cả/All, 1=Không khí/Air, 2=Nước thải/Wastewater, 3=Đất/Soil
            int selectedIdx = cboSampleType?.SelectedIndex ?? 0;
            string[] dbEnvTypes = { "", "Không khí", "Nước thải", "Đất" }; // DB values (bất biến)
            string dbFilter = selectedIdx > 0 && selectedIdx < dbEnvTypes.Length ? dbEnvTypes[selectedIdx] : "";

            foreach (Control ctrl in flpSamplingAreas.Controls)
            {
                if (ctrl is FlowLayoutPanel p && p.Tag?.ToString().StartsWith("ENV:") == true)
                {
                    string envType = p.Tag.ToString().Substring(4); // envType là DB value
                    p.Visible = selectedIdx == 0 || envType == dbFilter;
                }
            }
            flpSamplingAreas.PerformLayout();
        }

        private void CboAssignment_SelectedIndexChanged(object sender, EventArgs e)
        {
            ApplyAssignmentFilterAllAreas();
        }

        private void BtnAddArea_Click(object sender, EventArgs e)
        {
            // Bỏ: Nút thêm khu vực giờ nằm trong từng section nền mẫu
        }

        private async Task AutoSaveAreaAsync(Panel areaPanel, DataGridView dgv)
        {
            string orderId = areaPanel.Tag?.ToString() ?? "";
            if (string.IsNullOrEmpty(orderId)) return;

            try
            {
                var parameters = new List<SampleParameterPlanDTO>();
                foreach (DataGridViewRow row in dgv.Rows)
                {
                    parameters.Add(new SampleParameterPlanDTO
                    {
                        ParamID = row.Cells["ParamID"].Value?.ToString() ?? "",
                        RegulationID = row.Cells["RegulationID"].Value?.ToString() ?? "",
                        ParamName = row.Cells["ParamName"].Value?.ToString() ?? "",
                        Unit = row.Cells["Unit"].Value?.ToString() ?? "",
                        Department = DeptToDB(row.Cells["Department"].Value?.ToString() ?? ""),
                        QcvnLimit = row.Cells["QcvnLimit"].Value?.ToString() ?? ""
                    });
                }
                await _planningService.SaveSamplingPlanAsync(orderId, parameters);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Auto-save error: {ex.Message}");
                var lm = LanguageManager.Instance;
                MessageBox.Show(lm.Get("field_autosave_error") + ex.Message, lm.Get("msg_error"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private async void BtnSave_Click(object sender, EventArgs e)
        {
            // Giữ lại nhưng không còn gọn với nút nào (dự phòng)
        }
    }

    internal class DoubleBufferedFlowLayoutPanel : FlowLayoutPanel
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int ShowScrollBar(IntPtr hWnd, int wBar, bool bShow);
        private const int SB_HORZ = 0;

        public DoubleBufferedFlowLayoutPanel()
        {
            this.DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            UpdateStyles();
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            // Sau mỗi lần Windows vẽ lại window frame hoặc resize — ẩn hẳn scrollbar ngang
            if (m.Msg == 0x0005 /* WM_SIZE */ || m.Msg == 0x0083 /* WM_NCCALCSIZE */)
                ShowScrollBar(this.Handle, SB_HORZ, false);
        }
    }
}
