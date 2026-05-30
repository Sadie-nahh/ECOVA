using EnvContract.BLL.Interfaces;
using EnvContract.Common.Constants;
using EnvContract.DTO.Entities;
using EnvContract.GUI.Forms.Main;
using EnvContract.GUI.Helpers;
using EnvContract.GUI.Services;
using Guna.UI2.WinForms;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using EnvContract.Common;

namespace EnvContract.GUI.UserControls.Sales
{
    public partial class CustomerManagementUC : UserControl
    {
        private readonly ICustomerService _customerService;
        private readonly IContractService _contractService;
        private readonly VoiceSearchService _voiceService;
        private Guna2TextBox txtSearch;
        private Guna2Button btnAdd;
        private DataGridView dgvContracts;
        private Panel pnlGridWrapper;
        private bool _isLoadingData = false;
        private Image _watermarkImage;
        private Bitmap _cachedWatermark;
        private Action _langHandler;

        public CustomerManagementUC(ICustomerService customerService, IContractService contractService)
        {
            _customerService = customerService;
            _contractService = contractService;
            _voiceService = Program.ServiceProvider?.GetService<VoiceSearchService>();
            InitializeComponent();
            this.Load += (s, e) => ApplyReadOnlyIfNeeded();
        }

        private System.ComponentModel.IContainer _components = null;

        private void InitializeComponent()
        {
            _components = new System.ComponentModel.Container();
            this.SuspendLayout();
            this.Dock = DockStyle.Fill;
            this.BackColor = UIConstants.WhiteBackground;
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            this.Load += CustomerManagementUC_Load;

            // Load watermark logo
            try
            {
                string logoPath = FindAssetPath("Icon.png");
                if (!string.IsNullOrEmpty(logoPath))
                    _watermarkImage = Image.FromFile(logoPath);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[CustomerUC] Logo load error: {ex.Message}"); }

            // ── 1. Header ─────────────────────────────────────────────────────
            var LM = LanguageManager.Instance;
            var lblTitle = new Label
            {
                Text = LM.Get("sales_title"),
                Font = new Font("Segoe UI", 24, FontStyle.Bold),
                Location = new Point(30, 25),
                AutoSize = true,
                ForeColor = Color.Black
            };
            this.Controls.Add(lblTitle);

            var lblSubTitle = new Label
            {
                Text = LM.Get("sales_subtitle"),
                UseMnemonic = false,
                Font = new Font("Segoe UI", 12),
                Location = new Point(35, 73),
                AutoSize = true,
                ForeColor = UIConstants.TextDark
            };
            this.Controls.Add(lblSubTitle);

            // ── 2. Toolbar ────────────────────────────────────────────────────
            int toolY = 120;
            txtSearch = new Guna2TextBox
            {
                PlaceholderText = LM.Get("sales_search"),
                PlaceholderForeColor = Color.FromArgb(64, 64, 64),
                Size = new Size(450, 45),
                Location = new Point(30, toolY),
                BorderRadius = 10,
                BorderThickness = 1,
                BorderColor = UIConstants.DarkGreenBackground,
                Font = new Font("Segoe UI", 11),
                FillColor = Color.FromArgb(226, 232, 219),
                ForeColor = UIConstants.TextDark,
                TextOffset = new Point(10, 0),
                IconRight = null
            };
            txtSearch.TextChanged += async (s, e) =>
            {
                if (txtSearch.Text.Length > 2 || txtSearch.Text.Length == 0)
                    await LoadDataAsync(txtSearch.Text);
            };
            this.Controls.Add(txtSearch);
            VoiceSearchHelper.AttachVoiceButton(txtSearch, this, _voiceService,
                () => VoiceSearchHelper.ExtractGridContext(dgvContracts, "colCompanyName", "colRepresentative", "colContractId"));

            btnAdd = new Guna2Button
            {
                Text = LM.Get("sales_add_contract"),
                Size = new Size(220, 45),
                BorderRadius = 10,
                FillColor = Color.FromArgb(226, 232, 219),
                ForeColor = Color.Black,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Cursor = Cursors.Hand,
                BorderThickness = 1,
                BorderColor = UIConstants.DarkGreenBackground
            };
            btnAdd.Click += BtnAdd_Click;
            this.Controls.Add(btnAdd);

            // ── 3. DataGridView ───────────────────────────────────────────────
            dgvContracts = new DataGridView
            {
                Location = new Point(30, 190),
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeColumns = false,
                AllowUserToResizeRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = UIConstants.WhiteBackground,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.None,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
                EnableHeadersVisualStyles = false,
                RowHeadersVisible = false,
                RowTemplate = { Height = 60 },
                AutoGenerateColumns = false,
                AlternatingRowsDefaultCellStyle = { BackColor = UIConstants.VeryLightGreen }
            };

            dgvContracts.AdvancedCellBorderStyle.All = DataGridViewAdvancedCellBorderStyle.None;
            dgvContracts.AdvancedColumnHeadersBorderStyle.All = DataGridViewAdvancedCellBorderStyle.None;
            
            dgvContracts.ColumnHeadersHeight = 50;
            dgvContracts.ColumnHeadersDefaultCellStyle.BackColor = UIConstants.SuccessColor;
            dgvContracts.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgvContracts.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 11, FontStyle.Bold);
            dgvContracts.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvContracts.ColumnHeadersDefaultCellStyle.SelectionBackColor = UIConstants.SuccessColor;

            // Cell style
            dgvContracts.DefaultCellStyle.BackColor = UIConstants.SoftLightGreen;
            dgvContracts.DefaultCellStyle.SelectionBackColor = UIConstants.LightGreenAccent;
            dgvContracts.DefaultCellStyle.SelectionForeColor = Color.Black;
            dgvContracts.DefaultCellStyle.Font = new Font("Segoe UI", 10);
            dgvContracts.DefaultCellStyle.ForeColor = Color.Black;
            dgvContracts.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

            // ── Columns ───────────────────────────────────────────────────────
            dgvContracts.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colContractId",
                DataPropertyName = "ContractId",
                HeaderText = "Mã HĐ",
                Width = 120
            });
            dgvContracts.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colCompanyName",
                DataPropertyName = "CompanyName",
                HeaderText = "Doanh nghiệp",
                Width = 220,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleLeft, Padding = new Padding(15, 0, 0, 0) }
            });
            dgvContracts.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colRepresentative",
                DataPropertyName = "Representative",
                HeaderText = "Đại diện",
                Width = 160,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleLeft }
            });
            dgvContracts.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colPhone",
                DataPropertyName = "PhoneNumber",
                HeaderText = "Liên hệ",
                Width = 130
            });
            dgvContracts.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colSignedDate",
                DataPropertyName = "SignedDate",
                HeaderText = "Ngày ký",
                Width = 110
            });
            dgvContracts.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colValidTo",
                DataPropertyName = "ValidTo",
                HeaderText = "Ngày trả KQ",
                Width = 110
            });
            dgvContracts.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colAddress",
                DataPropertyName = "Address",
                HeaderText = "Địa điểm",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleLeft }
            });
            dgvContracts.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colRenewalScore",
                DataPropertyName = "RenewalScore",
                HeaderText = "Tái ký (%)",
                Width = 100
            });
            dgvContracts.Columns.Add(new DataGridViewButtonColumn
            {
                Name = "colAction",
                HeaderText = "Thao tác",
                Width = 180,
                UseColumnTextForButtonValue = false
            });

            foreach (DataGridViewColumn col in dgvContracts.Columns)
                col.SortMode = DataGridViewColumnSortMode.NotSortable;

            dgvContracts.CellFormatting += DgvContracts_CellFormatting;
            dgvContracts.CellPainting += DgvContracts_CellPainting;
            dgvContracts.CellClick += DgvContracts_CellClick;
            dgvContracts.CellMouseMove += (s, e) =>
            {
                if (e.RowIndex >= 0 && dgvContracts.Columns[e.ColumnIndex].Name == "colAction")
                    dgvContracts.Cursor = Cursors.Hand;
                else
                    dgvContracts.Cursor = Cursors.Default;
            };

            // Bọc DGV trong Panel để clip viền ngoài Guna2 vẽ (-1px mỗi phía)
            pnlGridWrapper = new Panel
            {
                BackColor = UIConstants.WhiteBackground,
                BorderStyle = BorderStyle.None,
                Location = new Point(30, 190),
                Padding = new Padding(0)
            };
            dgvContracts.Location = new Point(-1, -1); // lùi 1px vào trong wrapper
            pnlGridWrapper.Controls.Add(dgvContracts);
            this.Controls.Add(pnlGridWrapper);
            this.Resize += (s, e) => DoLayout();

            _langHandler = () =>
            {
                if (this.IsDisposed) return;
                var lm = LanguageManager.Instance;
                lblTitle.Text = lm.Get("sales_title");
                lblSubTitle.Text = lm.Get("sales_subtitle");
                txtSearch.PlaceholderText = lm.Get("sales_search");
                btnAdd.Text = lm.Get("sales_add_contract");

                if (dgvContracts.Columns.Contains("colContractId")) dgvContracts.Columns["colContractId"].HeaderText = lm.Get("sales_col_contract_id");
                if (dgvContracts.Columns.Contains("colCompanyName")) dgvContracts.Columns["colCompanyName"].HeaderText = lm.Get("sales_col_company");
                if (dgvContracts.Columns.Contains("colRepresentative")) dgvContracts.Columns["colRepresentative"].HeaderText = lm.Get("sales_col_representative");
                if (dgvContracts.Columns.Contains("colPhone")) dgvContracts.Columns["colPhone"].HeaderText = lm.Get("sales_col_contact");
                if (dgvContracts.Columns.Contains("colSignedDate")) dgvContracts.Columns["colSignedDate"].HeaderText = lm.Get("sales_col_signed");
                if (dgvContracts.Columns.Contains("colValidTo")) dgvContracts.Columns["colValidTo"].HeaderText = lm.Get("sales_col_valid_to");
                if (dgvContracts.Columns.Contains("colAddress")) dgvContracts.Columns["colAddress"].HeaderText = lm.Get("sales_col_address");
                if (dgvContracts.Columns.Contains("colRenewalScore")) dgvContracts.Columns["colRenewalScore"].HeaderText = lm.Get("sales_col_renewal");
                if (dgvContracts.Columns.Contains("colAction")) dgvContracts.Columns["colAction"].HeaderText = lm.Get("sales_col_action");
                
                dgvContracts.Invalidate();
            };
            LanguageManager.Instance.LanguageChanged += _langHandler;
            _langHandler(); // Apply immediately

            this.ResumeLayout(false);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_components != null) _components.Dispose();
                if (_langHandler != null) LanguageManager.Instance.LanguageChanged -= _langHandler;
            }
            base.Dispose(disposing);
        }

        // ══════════════════════════════════════════════════════════════════════
        // LAYOUT
        // ══════════════════════════════════════════════════════════════════════
        private void DoLayout()
        {
            if (this.Width == 0 || this.Height == 0) return;

            int pad = 30;
            int w = Math.Max(this.Width, 1000);
            int h = Math.Max(this.Height, 700);

            btnAdd.Location = new Point(w - pad - btnAdd.Width, 120);

            // Wrapper chiếm đúng khu vực, DGV +2px mỗi chiều để border bị clip
            int wrapW = w - pad * 2;
            int wrapH = h - 190 - pad;
            pnlGridWrapper.SetBounds(pad, 190, wrapW, wrapH);
            dgvContracts.Size = new Size(wrapW + 2, wrapH + 2);

            _cachedWatermark?.Dispose();
            _cachedWatermark = null;
        }

        // ══════════════════════════════════════════════════════════════════════
        // CELL FORMATTING — dates + renewal score color
        // ══════════════════════════════════════════════════════════════════════
        private void DgvContracts_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var colName = dgvContracts.Columns[e.ColumnIndex].Name;

            if ((colName == "colSignedDate" || colName == "colValidTo") && e.Value is DateTime dt)
            {
                e.Value = dt.ToString("dd/MM/yyyy");
                e.FormattingApplied = true;
            }

            // Color the renewal score
            if (colName == "colRenewalScore")
            {
                var dataItem = dgvContracts.Rows[e.RowIndex].DataBoundItem as ContractCardDTO;
                if (dataItem != null)
                {
                    if (dataItem.RenewalScore >= 70)
                    {
                        e.CellStyle.ForeColor = UIConstants.PrimaryColor;
                        e.CellStyle.SelectionForeColor = UIConstants.PrimaryColor;
                    }
                    else
                    {
                        e.CellStyle.ForeColor = UIConstants.DangerColor;
                        e.CellStyle.SelectionForeColor = UIConstants.DangerColor;
                    }
                    e.CellStyle.Font = new Font("Segoe UI", 10, FontStyle.Bold);
                    e.Value = $"{dataItem.RenewalScore}%";
                    e.FormattingApplied = true;
                }
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // CELL PAINTING — company name with avatar initials + action buttons
        // ══════════════════════════════════════════════════════════════════════
        private void DgvContracts_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            var colName = dgvContracts.Columns[e.ColumnIndex].Name;
            var dataItem = dgvContracts.Rows[e.RowIndex].DataBoundItem as ContractCardDTO;

            // ── Company name col ────────────────────────
            // Avatar removed natively, drawn by DataGridView natively

            // ── Action buttons: Cập nhật | Phân tích ──────────────────────────
            if (colName == "colAction")
            {
                e.PaintBackground(e.ClipBounds, true);

                int btnWidth = 80;
                int btnHeight = 30;
                int space = 8;
                int totalWidth = btnWidth * 2 + space;
                int startX = e.CellBounds.Left + (e.CellBounds.Width - totalWidth) / 2;
                int startY = e.CellBounds.Top + (e.CellBounds.Height - btnHeight) / 2;

                Rectangle rectUpdate = new Rectangle(startX, startY, btnWidth, btnHeight);
                Rectangle rectAnalyze = new Rectangle(startX + btnWidth + space, startY, btnWidth, btnHeight);

                using (var pathUpdate = GetRoundedRect(rectUpdate, 8))
                using (var pathAnalyze = GetRoundedRect(rectAnalyze, 8))
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                    // "Cập nhật" button
                    e.Graphics.FillPath(new SolidBrush(Color.FromArgb(130, 190, 100)), pathUpdate);
                    // "Phân tích" button
                    e.Graphics.FillPath(new SolidBrush(Color.FromArgb(49, 87, 44)), pathAnalyze);

                    var font = new Font("Segoe UI", 9, FontStyle.Bold);
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    var btnTextColor = Color.White;

                    var lm = LanguageManager.Instance;
                    e.Graphics.DrawString(lm.Get("sales_btn_update"), font, new SolidBrush(btnTextColor), rectUpdate, sf);
                    e.Graphics.DrawString(lm.Get("sales_btn_analyze"), font, new SolidBrush(btnTextColor), rectAnalyze, sf);
                }

                e.Handled = true;
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // CELL CLICK — handle action buttons
        // ══════════════════════════════════════════════════════════════════════
        private async void DgvContracts_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (dgvContracts.Columns[e.ColumnIndex].Name != "colAction") return;

            var dataItem = dgvContracts.Rows[e.RowIndex].DataBoundItem as ContractCardDTO;
            if (dataItem == null) return;

            var cellBounds = dgvContracts.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, true);
            var mousePos = dgvContracts.PointToClient(Cursor.Position);

            int btnWidth = 80;
            int space = 8;
            int totalWidth = btnWidth * 2 + space;
            int startX = cellBounds.Left + (cellBounds.Width - totalWidth) / 2;
            int midX = startX + btnWidth + space / 2;

            if (mousePos.X < midX)
            {
                // "Cập nhật" clicked
                using (var form = new EnvContract.GUI.Forms.Sales.ContractEditForm(
                    _contractService, _customerService, dataItem.ContractId))
                {
                    form.ShowDialog(this.FindForm());
                }
                await LoadDataAsync(txtSearch.Text);
            }
            else
            {
                // "Phân tích" clicked
                ShowAnalysisDialog(dataItem);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // DATA LOADING
        // ══════════════════════════════════════════════════════════════════════
        private async void CustomerManagementUC_Load(object sender, EventArgs e)
        {
            await LoadDataAsync();
        }

        private async System.Threading.Tasks.Task LoadDataAsync(string keyword = "")
        {
            if (_isLoadingData) return;
            _isLoadingData = true;
            try
            {
                List<ContractCardDTO> data;
                if (string.IsNullOrWhiteSpace(keyword))
                    data = await _contractService.GetContractCardsAsync();
                else
                    data = await _contractService.SearchContractCardsAsync(keyword);

                dgvContracts.DataSource = null;
                dgvContracts.DataSource = data;
                dgvContracts.Refresh();
            }
            catch (Exception ex)
            {
                var LM = LanguageManager.Instance;
                MessageBox.Show($"{LM.Get("sales_msg_load_err")}{ex.Message}", LM.Get("msg_error"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                _isLoadingData = false;
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // ADD + ANALYSIS DIALOG
        // ══════════════════════════════════════════════════════════════════════
        private async void BtnAdd_Click(object sender, EventArgs e)
        {
            using (var form = new EnvContract.GUI.Forms.Sales.ContractCreateForm(
                _contractService, _customerService,
                EnvContract.Common.AppState.Instance.CurrentUser?.UserID ?? "1"))
            {
                form.ShowDialog(this.FindForm());
            }
            await LoadDataAsync(txtSearch.Text);
        }

        private void ShowAnalysisDialog(ContractCardDTO contract)
        {
            var form = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.CenterParent,
                Size = new Size(500, 525), // Tăng chiều cao để chứa thêm dòng Liên hệ
                BackColor = Color.White,
                ShowInTaskbar = false
            };

            var elipse = new Guna2Elipse { TargetControl = form, BorderRadius = 15 };
            var shadowForm = new Guna2ShadowForm(form) { TargetForm = form };
            var drag = new Guna2DragControl { TargetControl = form };

            // 1. Header & Close Button
            var LM = LanguageManager.Instance;
            var lblTitle = new Label
            {
                Text = LM.Get("sales_anlz_title"),
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = UIConstants.PrimaryColor,
                Location = new Point(0, 20),
                Size = new Size(form.Width, 35),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            form.Controls.Add(lblTitle);

            var btnCloseX = new Label
            {
                Text = "✕",
                Size = new Size(32, 32),
                Location = new Point(form.Width - 45, 12),
                BackColor = Color.Transparent,
                ForeColor = Color.Black,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand
            };
            btnCloseX.Click += (s, e) => form.Close();
            btnCloseX.MouseEnter += (s, e) => btnCloseX.ForeColor = Color.Red;
            btnCloseX.MouseLeave += (s, e) => btnCloseX.ForeColor = Color.Black;
            form.Controls.Add(btnCloseX);

            // 2. Client Info Card (Panel)
            var pnlInfo = new Guna2Panel
            {
                Location = new Point(25, 65),
                Size = new Size(450, 245),
                BorderRadius = 10,
                BorderThickness = 1,
                BorderColor = Color.FromArgb(230, 230, 230),
                FillColor = Color.FromArgb(250, 252, 248)
            };
            form.Controls.Add(pnlInfo);

            void AddLabel(string title, string val, int y, Control parent)
            {
                var lblT = new Label { 
                    Text = title, 
                    Font = new Font("Segoe UI", 9, FontStyle.Bold), 
                    ForeColor = UIConstants.TextDark, 
                    Location = new Point(15, y), 
                    AutoSize = true,
                    BackColor = Color.Transparent
                };
                var lblV = new Label { 
                    Text = val, 
                    Font = new Font("Segoe UI", 10, FontStyle.Regular), 
                    ForeColor = Color.Black, 
                    Location = new Point(15, y + 18), 
                    AutoSize = true, 
                    MaximumSize = new Size(420, 0),
                    BackColor = Color.Transparent
                };
                parent.Controls.Add(lblT);
                parent.Controls.Add(lblV);
            }

            // Client info section
            AddLabel(LM.Get("sales_anlz_comp"), contract.CompanyName, 12, pnlInfo);
            
            // Representative line - Dynamic positioning
            var lblRepresent = new Label { Text = LM.Get("sales_anlz_rep"), Font = new Font("Segoe UI", 9), ForeColor = UIConstants.TextDark, Location = new Point(15, 65), AutoSize = true, BackColor = Color.Transparent };
            pnlInfo.Controls.Add(lblRepresent);
            var lblRepVal = new Label { Text = contract.Representative, Font = new Font("Segoe UI", 9, FontStyle.Bold), Location = new Point(lblRepresent.Right + 5, 65), AutoSize = true, BackColor = Color.Transparent };
            pnlInfo.Controls.Add(lblRepVal);

            // Contact line - Dynamic positioning
            var lblPhone = new Label { Text = LM.Get("sales_anlz_contact"), Font = new Font("Segoe UI", 9), ForeColor = UIConstants.TextDark, Location = new Point(15, 90), AutoSize = true, BackColor = Color.Transparent };
            pnlInfo.Controls.Add(lblPhone);
            var lblPhoneVal = new Label { Text = contract.PhoneNumber, Font = new Font("Segoe UI", 9, FontStyle.Bold), Location = new Point(lblPhone.Right + 5, 90), AutoSize = true, BackColor = Color.Transparent };
            pnlInfo.Controls.Add(lblPhoneVal);

            AddLabel(LM.Get("sales_anlz_addr"), contract.Address, 115, pnlInfo);
            
            var lblDates = new Label { 
                Text = string.Format(LM.Get("sales_anlz_dur"), contract.SignedDate.ToString("dd/MM/yyyy"), contract.ValidTo.ToString("dd/MM/yyyy")), 
                Font = new Font("Segoe UI", 9, FontStyle.Bold), 
                ForeColor = UIConstants.TextDark, 
                Location = new Point(15, 175), 
                AutoSize = true,
                BackColor = Color.Transparent
            };
            pnlInfo.Controls.Add(lblDates);

            string statusText = (contract.Status == (int)ContractStatus.Active) ? LM.Get("sales_anlz_st_active") : LM.Get("sales_anlz_st_pend");
            var lblStatus = new Guna2Button {
                Text = statusText.ToUpper(),
                Size = new Size(160, 28),
                Location = new Point(15, 205),
                BorderRadius = 14,
                FillColor = contract.Status == (int)ContractStatus.Active ? Color.FromArgb(235, 245, 235) : Color.FromArgb(220, 220, 220),
                ForeColor = contract.Status == (int)ContractStatus.Active ? Color.FromArgb(0, 100, 0) : UIConstants.TextDark,
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                Enabled = false
            };
            pnlInfo.Controls.Add(lblStatus);

            // 3. AI Prediction Section
            var lblAIHeader = new Label
            {
                Text = LM.Get("sales_anlz_predict"),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.Black,
                Location = new Point(30, 330),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            form.Controls.Add(lblAIHeader);

            Color scoreColor = contract.RenewalScore >= 70 ? UIConstants.PrimaryColor : 
                              (contract.RenewalScore >= 40 ? Color.FromArgb(215, 160, 70) : UIConstants.DangerColor);

            var lblScore = new Label
            {
                Text = $"{contract.RenewalScore}%",
                Font = new Font("Segoe UI", 42, FontStyle.Bold),
                ForeColor = scoreColor,
                Location = new Point(0, 350),
                Size = new Size(form.Width, 90), // Tăng chiều cao để không bị cắt chữ % ở dưới
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            form.Controls.Add(lblScore);

            var progressBig = new Guna2ProgressBar
            {
                Value = contract.RenewalScore,
                Maximum = 100,
                Location = new Point(50, 445),
                Size = new Size(400, 12),
                BorderRadius = 6,
                ProgressColor = scoreColor,
                ProgressColor2 = Color.FromArgb(scoreColor.R, scoreColor.G, scoreColor.B + 20),
                FillColor = Color.FromArgb(240, 240, 240)
            };
            form.Controls.Add(progressBig);

            // 4. Action Button
            var btnClose = new Guna2Button
            {
                Text = LM.Get("sales_anlz_btn_ok"),
                Size = new Size(200, 45),
                Location = new Point((form.Width - 200) / 2, 465),
                BorderRadius = 10,
                FillColor = UIConstants.SuccessColor, // Màu xanh đậm thành công
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnClose.Click += (s2, e2) => form.Close();
            form.Controls.Add(btnClose);

            form.ShowDialog(this.FindForm());
        }

        // ══════════════════════════════════════════════════════════════════════
        // ROLE-BASED ACCESS
        // ══════════════════════════════════════════════════════════════════════
        private void ApplyReadOnlyIfNeeded()
        {
            if (MainForm.IsReadOnlyForRole("R03"))
            {
                if (btnAdd != null)
                {
                    btnAdd.Enabled = false;
                    btnAdd.FillColor = Color.FromArgb(180, 180, 180);
                    btnAdd.ForeColor = Color.FromArgb(100, 100, 100);
                }
                if (dgvContracts.Columns.Contains("colAction"))
                    dgvContracts.Columns["colAction"].Visible = false;
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // HELPERS
        // ══════════════════════════════════════════════════════════════════════
        private string FindAssetPath(string filename)
        {
            string[] candidates = {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "assets", "images", filename),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "images", filename)
            };
            foreach (var c in candidates)
                if (File.Exists(c)) return Path.GetFullPath(c);

            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null)
            {
                var path = Path.Combine(dir.FullName, "assets", "images", filename);
                if (File.Exists(path)) return path;
                dir = dir.Parent;
            }
            return null;
        }

        private static string GetInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
                return $"{parts[0][0]}{parts[^1][0]}".ToUpper();
            return parts[0][0].ToString().ToUpper();
        }

        private GraphicsPath GetRoundedRect(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            Size size = new Size(diameter, diameter);
            Rectangle arc = new Rectangle(bounds.Location, size);
            GraphicsPath path = new GraphicsPath();

            if (radius == 0)
            {
                path.AddRectangle(bounds);
                return path;
            }

            path.AddArc(arc, 180, 90);
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            base.OnPaintBackground(e);

            if (_watermarkImage == null) return;

            int logoSize = Math.Min(this.Width, this.Height) * 2 / 3;
            if (logoSize < 200) logoSize = 200;

            if (_cachedWatermark == null || _cachedWatermark.Width != logoSize)
            {
                _cachedWatermark?.Dispose();
                _cachedWatermark = new Bitmap(logoSize, logoSize, PixelFormat.Format32bppPArgb);
                using (var g = Graphics.FromImage(_cachedWatermark))
                {
                    g.SmoothingMode = SmoothingMode.HighQuality;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;

                    var cm = new ColorMatrix { Matrix33 = 0.08f };
                    var ia = new ImageAttributes();
                    ia.SetColorMatrix(cm, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);

                    g.DrawImage(_watermarkImage,
                        new Rectangle(0, 0, logoSize, logoSize),
                        0, 0, _watermarkImage.Width, _watermarkImage.Height,
                        GraphicsUnit.Pixel, ia);
                    ia.Dispose();
                }
            }

            int x = (this.Width - logoSize) / 2 + 60;
            int y = (this.Height - logoSize) / 2 + 40;
            e.Graphics.DrawImage(_cachedWatermark, x, y);
        }
    }
}

