using EnvContract.DTO.Entities;
using Guna.UI2.WinForms;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace EnvContract.GUI.Forms.Planning
{
    public class AddParameterForm : Form
    {
        public List<SampleParameterPlanDTO> SelectedParameters { get; private set; } = new List<SampleParameterPlanDTO>();

        // Colors
        private static readonly Color DarkGreen1 = Color.FromArgb(19, 42, 19);
        private static readonly Color DarkGreen2 = Color.FromArgb(49, 87, 44);
        private static readonly Color PageBg = Color.FromArgb(240, 244, 235);
        private static readonly Color TableHeaderBg = Color.FromArgb(174, 196, 128);
        private static readonly Color TableRowAlt = Color.FromArgb(245, 248, 241);
        private static readonly Color SelectionGreen = Color.FromArgb(210, 233, 185);
        private static readonly Color PlaceholderColor = Color.FromArgb(64, 64, 64);

        private Guna2DataGridView dgvParams;
        private Guna2TextBox txtSearch;
        private Guna2BorderlessForm borderlessForm;
        private Label btnCloseX;

        // Dữ liệu ban đầu để so khớp
        private List<SampleParameterPlanDTO> _allPossibleParams;
        private List<string> _initiallySelectedIds;

        public AddParameterForm(List<SampleParameterPlanDTO> availableParams, List<SampleParameterPlanDTO> currentParamsInGrid)
        {
            _allPossibleParams = availableParams.ToList();
            foreach (var currentParam in currentParamsInGrid)
            {
                if (!_allPossibleParams.Any(p => p.ParamID == currentParam.ParamID))
                {
                    _allPossibleParams.Add(currentParam);
                }
            }

            _initiallySelectedIds = currentParamsInGrid.Select(p => p.ParamID).ToList();

            InitializeComponent();
            if (Program.AppIcon != null) this.Icon = Program.AppIcon;
        }

        private void InitializeComponent()
        {
            var LM = EnvContract.Common.LanguageManager.Instance;
            this.Text = LM.Get("plan_add_param_title");
            this.Size = new Size(820, 680);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = Color.White;
            this.ShowInTaskbar = false;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            borderlessForm = new Guna2BorderlessForm
            {
                ContainerControl = this,
                BorderRadius = 20,
                ShadowColor = Color.FromArgb(50, 0, 0, 0),
                DragForm = false // Tắt kéo thả
            };

            var pnlMain = new Guna2Panel
            {
                Dock = DockStyle.Fill,
                FillColor = PageBg,
                BorderRadius = 20,
                Padding = new Padding(20)
            };
            this.Controls.Add(pnlMain);

            // Header Section
            var pnlHeader = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = Color.Transparent };
            pnlMain.Controls.Add(pnlHeader);

            var lblTitle = new Label
            {
                Text = LM.Get("plan_add_param_header"),
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = DarkGreen2,
                AutoSize = true,
                Location = new Point(10, 15),
                BackColor = Color.Transparent
            };
            pnlHeader.Controls.Add(lblTitle);

            // ── Nút X đóng (neo phải) ── dùng Label để tránh lỗi Guna2Button bị cắt
            btnCloseX = new Label
            {
                Text = "✕",
                Size = new Size(32, 32),
                BackColor = Color.Transparent,
                ForeColor = Color.FromArgb(100, 100, 100),
                Font = new Font("Segoe UI", 13, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnCloseX.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };
            btnCloseX.MouseEnter += (s, e) => btnCloseX.ForeColor = Color.FromArgb(220, 50, 50);
            btnCloseX.MouseLeave += (s, e) => btnCloseX.ForeColor = Color.FromArgb(100, 100, 100);
            pnlHeader.Controls.Add(btnCloseX);

            // ── Ô tìm kiếm (neo phải, cách nút X 10px) ──
            txtSearch = new Guna2TextBox
            {
                Size = new Size(220, 36),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                PlaceholderText = LM.Get("plan_add_param_search"),
                PlaceholderForeColor = PlaceholderColor,
                ForeColor = Color.Black,
                BorderRadius = 15,
                Font = new Font("Segoe UI", 10),
                BorderColor = Color.FromArgb(174, 196, 128),
                FillColor = Color.White
            };
            txtSearch.TextChanged += (s, e) => FilterParameters();
            pnlHeader.Controls.Add(txtSearch);

            // Tính vị trí dựa trên pnlHeader width (sau khi add vào parent)
            pnlHeader.Layout += (s, e) =>
            {
                // Dịch chuyển cách biên phải 15px để tránh góc bo 20px cắt nút
                btnCloseX.Location = new Point(pnlHeader.Width - btnCloseX.Width - 15, 12);
                txtSearch.Location = new Point(btnCloseX.Left - txtSearch.Width - 10, 12);
            };

            // Center: Grid
            var pnlCenter = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(0, 0, 0, 10) };
            pnlMain.Controls.Add(pnlCenter);
            pnlCenter.BringToFront();

            var pnlGridFrame = new Guna2Panel
            {
                Dock = DockStyle.Fill,
                BorderRadius = 0,
                BorderThickness = 1,
                BorderColor = Color.FromArgb(170, 190, 150),
                FillColor = Color.White,
                BackColor = Color.Transparent,
                Padding = new Padding(2)
            };
            pnlCenter.Controls.Add(pnlGridFrame);

            dgvParams = new Guna2DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                AllowUserToAddRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                GridColor = Color.FromArgb(231, 229, 255),
                EnableHeadersVisualStyles = false,
                ThemeStyle = {
                    HeaderStyle = { BackColor = TableHeaderBg, Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = Color.Black, Height = 40 },
                    RowsStyle = { BackColor = Color.White, Font = new Font("Segoe UI", 10), ForeColor = Color.Black, SelectionBackColor = SelectionGreen, SelectionForeColor = Color.Black, Height = 35 },
                    AlternatingRowsStyle = { BackColor = TableRowAlt }
                }
            };
            dgvParams.ColumnHeadersDefaultCellStyle.SelectionBackColor = TableHeaderBg;
            dgvParams.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.Black;
            dgvParams.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Select", HeaderText = LM.Get("plan_add_param_col_select"), FillWeight = 10, TrueValue = true, FalseValue = false });
            dgvParams.Columns.Add(new DataGridViewTextBoxColumn { Name = "ParamName", HeaderText = LM.Get("plan_add_param_col_name"), FillWeight = 30, ReadOnly = true });
            dgvParams.Columns.Add(new DataGridViewTextBoxColumn { Name = "Unit", HeaderText = LM.Get("plan_add_param_col_unit"), FillWeight = 15, ReadOnly = true });
            dgvParams.Columns.Add(new DataGridViewTextBoxColumn { Name = "Department", HeaderText = LM.Get("plan_add_param_col_dept"), FillWeight = 20, ReadOnly = true });
            dgvParams.Columns.Add(new DataGridViewTextBoxColumn { Name = "QcvnLimit", HeaderText = LM.Get("plan_add_param_col_limit"), FillWeight = 25, ReadOnly = true });
            dgvParams.Columns.Add(new DataGridViewTextBoxColumn { Name = "ParamID", HeaderText = "", Visible = false });

            // SẮP XẾP: Thông số đã chọn lên trên đầu
            var sortedParams = _allPossibleParams
                .OrderByDescending(p => _initiallySelectedIds.Contains(p.ParamID))
                .ThenBy(p => p.ParamName)
                .ToList();

            foreach (var p in sortedParams)
            {
                int rowIdx = dgvParams.Rows.Add();
                bool isSelected = _initiallySelectedIds.Contains(p.ParamID);
                dgvParams.Rows[rowIdx].Cells["Select"].Value = isSelected;
                dgvParams.Rows[rowIdx].Cells["ParamName"].Value = p.ParamName;
                dgvParams.Rows[rowIdx].Cells["Unit"].Value = p.Unit;
                dgvParams.Rows[rowIdx].Cells["Department"].Value = p.Department;
                dgvParams.Rows[rowIdx].Cells["QcvnLimit"].Value = p.QcvnLimit;
                dgvParams.Rows[rowIdx].Cells["ParamID"].Value = p.ParamID;
                dgvParams.Rows[rowIdx].Tag = p;
                
                if (isSelected) 
                    dgvParams.Rows[rowIdx].DefaultCellStyle.BackColor = Color.FromArgb(245, 250, 240);
            }
            pnlGridFrame.Controls.Add(dgvParams);

            dgvParams.CellClick += (s, e) =>
            {
                if (e.RowIndex >= 0)
                {
                    bool currentVal = dgvParams.Rows[e.RowIndex].Cells["Select"].Value != null && (bool)dgvParams.Rows[e.RowIndex].Cells["Select"].Value;
                    dgvParams.Rows[e.RowIndex].Cells["Select"].Value = !currentVal;
                }
            };

            // Footer
            var pnlFooter = new Panel { Dock = DockStyle.Bottom, Height = 60, BackColor = Color.Transparent };
            pnlMain.Controls.Add(pnlFooter);

            var btnAddNew = new Guna2Button
            {
                Text = LM.Get("plan_add_param_btn_add"),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = DarkGreen2,
                FillColor = Color.FromArgb(230, 243, 218),
                BorderColor = DarkGreen2,
                BorderThickness = 1,
                Size = new Size(140, 42),
                Location = new Point(20, 8),
                BorderRadius = 15,
                Cursor = Cursors.Hand
            };
            btnAddNew.Click += (s, e) => ShowAddNewParamDialog();

            var btnConfirm = new Guna2Button
            {
                Text = LM.Get("msg_confirm"),
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = Color.White,
                FillColor = DarkGreen2,
                Size = new Size(180, 42),
                BorderRadius = 15,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnConfirm.Click += (s, e) => ConfirmAdd();

            var btnCancel = new Guna2Button
            {
                Text = LM.Get("msg_cancel"),
                Font = new Font("Segoe UI", 11),
                ForeColor = DarkGreen2,
                FillColor = Color.White,
                BorderColor = DarkGreen2,
                BorderThickness = 1,
                Size = new Size(110, 42),
                BorderRadius = 15,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };

            pnlFooter.Controls.Add(btnAddNew);
            pnlFooter.Controls.Add(btnConfirm);
            pnlFooter.Controls.Add(btnCancel);

            // Layout responsive cho footer buttons
            pnlFooter.Layout += (s, e) =>
            {
                btnCancel.Location = new Point(pnlFooter.Width - btnCancel.Width - 20, 8);
                btnConfirm.Location = new Point(btnCancel.Left - btnConfirm.Width - 10, 8);
            };
        }

        private void ShowAddNewParamDialog()
        {
            var LM = EnvContract.Common.LanguageManager.Instance;
            var dlg = new Form
            {
                Text = LM.Get("plan_add_param_new_title"),
                Size = new Size(480, 340),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = PageBg
            };
            if (Program.AppIcon != null) dlg.Icon = Program.AppIcon;

            // Dùng TableLayoutPanel để tự động căn chỉnh — tránh overlap
            var tbl = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 5, // 4 fields + 1 buttons
                Padding = new Padding(15, 15, 15, 10),
                BackColor = Color.Transparent
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // label auto-width
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); // input fill
            for (int i = 0; i < 4; i++)
                tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 55)); // buttons row

            var labelTexts = new[] { LM.Get("plan_add_param_new_name"), LM.Get("plan_add_param_new_unit"), LM.Get("plan_add_param_new_dept"), LM.Get("plan_add_param_new_limit") };
            var controls = new Control[4];

            for (int i = 0; i < labelTexts.Length; i++)
            {
                var lbl = new Label
                {
                    Text = labelTexts[i],
                    Font = new Font("Segoe UI", 10),
                    AutoSize = true,
                    Anchor = AnchorStyles.Left,
                    Margin = new Padding(0, 0, 12, 0) // gap 12px giữa label và input
                };
                tbl.Controls.Add(lbl, 0, i);

                if (i == 2)
                {
                    var cbo = new Guna2ComboBox
                    {
                        Dock = DockStyle.Fill,
                        Font = new Font("Segoe UI", 10),
                        BorderRadius = 10,
                        FillColor = Color.White,
                        BorderColor = Color.FromArgb(170, 190, 150),
                        Margin = new Padding(0, 5, 0, 5)
                    };
                    cbo.Items.AddRange(new object[] { LM.Get("plan_add_param_dept_field"), LM.Get("plan_add_param_dept_lab") });
                    cbo.SelectedIndex = 0;
                    controls[i] = cbo;
                }
                else
                {
                    var txt = new Guna2TextBox
                    {
                        Dock = DockStyle.Fill,
                        Font = new Font("Segoe UI", 10),
                        ForeColor = Color.Black,
                        BorderRadius = 10,
                        FillColor = Color.White,
                        BorderColor = Color.FromArgb(170, 190, 150),
                        Margin = new Padding(0, 5, 0, 5)
                    };
                    controls[i] = txt;
                }
                tbl.Controls.Add(controls[i], 1, i);
            }

            // Buttons row
            var pnlBtns = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(0, 5, 0, 0)
            };

            var btnDlgCancel = new Guna2Button
            {
                Text = LM.Get("msg_cancel"), Font = new Font("Segoe UI", 10),
                ForeColor = DarkGreen2, FillColor = Color.White,
                BorderColor = DarkGreen2, BorderThickness = 1,
                Size = new Size(80, 38), BorderRadius = 12, Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 10, 0)
            };
            btnDlgCancel.Click += (s, e) => dlg.Close();

            var btnOk = new Guna2Button
            {
                Text = LM.Get("msg_add"), Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.White, FillColor = DarkGreen2,
                Size = new Size(100, 38), BorderRadius = 12, Cursor = Cursors.Hand
            };
            btnOk.Click += (s, e) =>
            {
                var LM_Btn = EnvContract.Common.LanguageManager.Instance;
                string name = (controls[0] as Guna2TextBox)?.Text?.Trim() ?? "";
                if (string.IsNullOrEmpty(name))
                {
                    MessageBox.Show(LM_Btn.Get("plan_add_param_err_req"), LM_Btn.Get("msg_warning"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                string unit = (controls[1] as Guna2TextBox)?.Text?.Trim() ?? "";
                string dept = (controls[2] as Guna2ComboBox)?.SelectedItem?.ToString() ?? LM_Btn.Get("plan_add_param_dept_field");
                string qcvn = (controls[3] as Guna2TextBox)?.Text?.Trim() ?? "";
                string newId = "CUSTOM_" + Guid.NewGuid().ToString("N").Substring(0, 8);

                var newParam = new SampleParameterPlanDTO
                {
                    ParamID = newId, ParamName = name, Unit = unit,
                    Department = dept, QcvnLimit = qcvn
                };

                int rowIdx = dgvParams.Rows.Add();
                dgvParams.Rows[rowIdx].Cells["Select"].Value = true;
                dgvParams.Rows[rowIdx].Cells["ParamName"].Value = name;
                dgvParams.Rows[rowIdx].Cells["Unit"].Value = unit;
                dgvParams.Rows[rowIdx].Cells["Department"].Value = dept;
                dgvParams.Rows[rowIdx].Cells["QcvnLimit"].Value = qcvn;
                dgvParams.Rows[rowIdx].Cells["ParamID"].Value = newId;
                dgvParams.Rows[rowIdx].Tag = newParam;
                dgvParams.Rows[rowIdx].DefaultCellStyle.BackColor = Color.FromArgb(245, 250, 240);

                dlg.Close();
            };

            // RightToLeft flow: cancel first (right), then ok (left of cancel)
            pnlBtns.Controls.Add(btnDlgCancel);
            pnlBtns.Controls.Add(btnOk);
            tbl.SetColumnSpan(pnlBtns, 2);
            tbl.Controls.Add(pnlBtns, 0, 4);

            dlg.Controls.Add(tbl);
            dlg.ShowDialog(this);
        }

        private void FilterParameters()
        {
            string kw = txtSearch.Text.Trim().ToLower();
            dgvParams.CurrentCell = null;
            foreach (DataGridViewRow row in dgvParams.Rows)
            {
                string paramName = row.Cells["ParamName"].Value?.ToString() ?? "";
                row.Visible = string.IsNullOrEmpty(kw) || paramName.ToLower().Contains(kw);
            }
        }

        private void ConfirmAdd()
        {
            SelectedParameters = new List<SampleParameterPlanDTO>();
            foreach (DataGridViewRow row in dgvParams.Rows)
            {
                var val = row.Cells["Select"].Value;
                if (val != null && (bool)val == true)
                {
                    if (row.Tag is SampleParameterPlanDTO dto)
                    {
                        SelectedParameters.Add(dto);
                    }
                    else
                    {
                        // Custom param without Tag
                        SelectedParameters.Add(new SampleParameterPlanDTO
                        {
                            ParamID = row.Cells["ParamID"].Value?.ToString() ?? "",
                            ParamName = row.Cells["ParamName"].Value?.ToString() ?? "",
                            Unit = row.Cells["Unit"].Value?.ToString() ?? "",
                            Department = row.Cells["Department"].Value?.ToString() ?? "",
                            QcvnLimit = row.Cells["QcvnLimit"].Value?.ToString() ?? ""
                        });
                    }
                }
            }
            this.DialogResult = DialogResult.OK;
            this.Close();
        }


    }
}
