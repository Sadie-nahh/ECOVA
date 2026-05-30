using EnvContract.BLL.Interfaces;
using EnvContract.GUI.Helpers;
using EnvContract.GUI.Services;
using Guna.UI2.WinForms;
using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using EnvContract.Common;

namespace EnvContract.GUI.UserControls.Planning
{
    public class ParameterConfigUC : UserControl
    {
        private SplitContainer splitC;
        private Guna2TextBox txtSearchSample;
        private ListBox lstSamples;
        private Guna2Panel pnlRightDetail;
        private Label lblSelectedSampleTitle;
        private FlowLayoutPanel flpParameters; // Chứa các CheckBox / Nhom thong so
        private Guna2Button btnSaveParams;
        private VoiceSearchService _voiceService;
        
        public ParameterConfigUC()
        {
            _voiceService = Program.ServiceProvider?.GetService<VoiceSearchService>();
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.Dock = DockStyle.Fill;
            this.BackColor = UIConstants.WhiteBackground;

            // Header
            var LM = LanguageManager.Instance;
            var pnlTop = new Guna2Panel { Dock = DockStyle.Top, Height = 100, FillColor = UIConstants.WhiteBackground };
            var lblTitle = new Label { Text = LM.Get("paramconfig_title"), Font = new Font("Segoe UI", 24, FontStyle.Bold), Location = new Point(30, 20), AutoSize = true, ForeColor = Color.Black, BackColor = Color.Transparent };
            var lblSub = new Label { Text = LM.Get("paramconfig_subtitle"), Font = new Font("Segoe UI", 12), Location = new Point(35, 65), AutoSize = true, ForeColor = UIConstants.TextDark, BackColor = Color.Transparent };
            pnlTop.Controls.Add(lblTitle);
            pnlTop.Controls.Add(lblSub);
            this.Controls.Add(pnlTop);

            // Split Layout
            splitC = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 350,
                FixedPanel = FixedPanel.Panel1,
                BorderStyle = BorderStyle.None,
                SplitterWidth = 20, // Khoảng cách giả lập theo Figma
                BackColor = UIConstants.WhiteBackground
            };
            splitC.Panel1.BackColor = UIConstants.WhiteBackground;
            splitC.Panel2.BackColor = UIConstants.WhiteBackground;

            // ---- PANEL 1 (LEFT): Danh sách Mẫu ----
            var pnlLeftContainer = new Guna2Panel { Dock = DockStyle.Fill, BorderRadius = 15, FillColor = Color.White };
            pnlLeftContainer.Padding = new Padding(20);
            
            pnlLeftContainer.ShadowDecoration.Enabled = true;
            pnlLeftContainer.ShadowDecoration.Depth = 5;

            var lblLeftTitle = new Label { Text = LM.Get("paramconfig_sample_list"), Font = new Font("Segoe UI", 14, FontStyle.Bold), ForeColor = UIConstants.DarkGreenBackground, Dock = DockStyle.Top, Height = 40 };
            
            txtSearchSample = new Guna2TextBox
            {
                PlaceholderText = LM.Get("paramconfig_search"),
                PlaceholderForeColor = Color.FromArgb(64, 64, 64),
                Dock = DockStyle.Top,
                Height = 45,
                BorderRadius = 8,
                Font = new Font("Segoe UI", 11),
                Margin = new Padding(0,0,0,10)
            };
            
            lstSamples = new ListBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 11),
                BorderStyle = BorderStyle.None,
                ItemHeight = 25,
                ForeColor = UIConstants.TextDark
            };
            lstSamples.Items.Add("SMPL-24-001 (Nước thải)");
            lstSamples.Items.Add("SMPL-24-002 (Khí thải)");
            lstSamples.Items.Add("SMPL-24-003 (Đất)");
            lstSamples.Items.Add("SMPL-24-004 (Bùn thải)");
            lstSamples.SelectedIndex = 0;
            lstSamples.SelectedIndexChanged += LstSamples_SelectedIndexChanged;

            // Custom spacing panel for left inside top
            var padPnlLeft = new Panel { Dock = DockStyle.Top, Height = 15, BackColor = Color.White };

            pnlLeftContainer.Controls.Add(lstSamples);
            pnlLeftContainer.Controls.Add(padPnlLeft);
            pnlLeftContainer.Controls.Add(txtSearchSample);
            pnlLeftContainer.Controls.Add(lblLeftTitle);
            VoiceSearchHelper.AttachVoiceButtonInPanel(txtSearchSample, pnlLeftContainer, _voiceService,
                () =>
                {
                    if (lstSamples == null || lstSamples.Items.Count == 0) return null;
                    var sb = new System.Text.StringBuilder();
                    foreach (var item in lstSamples.Items)
                    {
                        var val = item?.ToString()?.Trim();
                        if (string.IsNullOrWhiteSpace(val)) continue;
                        if (sb.Length + val.Length + 2 > 200) break;
                        if (sb.Length > 0) sb.Append(", ");
                        sb.Append(val);
                    }
                    return sb.Length > 0 ? sb.ToString() : null;
                });

            // Fake margin bên ngoài panel left (Padding inside SplitC Panel1 không work với Guna Panel DockFill hoàn hảo)
            var wrapperLeft = new Panel { Dock = DockStyle.Fill, Padding = new Padding(30, 10, 0, 30) };
            wrapperLeft.Controls.Add(pnlLeftContainer);
            splitC.Panel1.Controls.Add(wrapperLeft);

            // ---- PANEL 2 (RIGHT): Chi tiết Cấu hình ----
            pnlRightDetail = new Guna2Panel { Dock = DockStyle.Fill, BorderRadius = 15, FillColor = Color.White };
            pnlRightDetail.Padding = new Padding(30);
            pnlRightDetail.ShadowDecoration.Enabled = true;
            pnlRightDetail.ShadowDecoration.Depth = 5;

            var pnlRightHeader = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = Color.White };
            lblSelectedSampleTitle = new Label { Text = LM.Get("paramconfig_selected") + " SMPL-24-001", Font = new Font("Segoe UI", 16, FontStyle.Bold), ForeColor = Color.Black, AutoSize = true, Location = new Point(0,0) };
            var lblGuide = new Label { Text = LM.Get("paramconfig_guide"), Font = new Font("Segoe UI", 11), ForeColor = UIConstants.TextMuted, Location = new Point(0, 35), AutoSize = true };
            pnlRightHeader.Controls.Add(lblSelectedSampleTitle);
            pnlRightHeader.Controls.Add(lblGuide);
            
            flpParameters = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.White,
                Padding = new Padding(0, 20, 0, 0)
            };
            PopulateParameters();

            var pnlRightBottom = new Panel { Dock = DockStyle.Bottom, Height = 80, BackColor = Color.White };
            btnSaveParams = new Guna2Button 
            { 
                Text = LM.Get("paramconfig_save"), 
                Size = new Size(160, 45), 
                BorderRadius = 10, 
                FillColor = UIConstants.DarkGreenBackground, 
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Anchor = AnchorStyles.Right | AnchorStyles.Top,
                Location = new Point(pnlRightDetail.Width - 190, 20) // approx
            };
            btnSaveParams.Click += (s, e) => MessageBox.Show(LM.Get("paramconfig_saved"), LM.Get("info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            pnlRightBottom.Controls.Add(btnSaveParams);

            pnlRightDetail.Controls.Add(flpParameters);
            pnlRightDetail.Controls.Add(pnlRightHeader);
            pnlRightDetail.Controls.Add(pnlRightBottom);

            // Handle Resize for Save button
            pnlRightBottom.Resize += (s, e) => {
                btnSaveParams.Left = pnlRightBottom.Width - btnSaveParams.Width - 30;
            };

            var wrapperRight = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 10, 30, 30) };
            wrapperRight.Controls.Add(pnlRightDetail);
            splitC.Panel2.Controls.Add(wrapperRight);

            this.Controls.Add(splitC);
            splitC.BringToFront();

            LanguageManager.Instance.LanguageChanged += () =>
            {
                var lm = LanguageManager.Instance;
                lblTitle.Text = lm.Get("paramconfig_title");
                lblSub.Text = lm.Get("paramconfig_subtitle");
                lblLeftTitle.Text = lm.Get("paramconfig_sample_list");
                txtSearchSample.PlaceholderText = lm.Get("paramconfig_search");
                lblGuide.Text = lm.Get("paramconfig_guide");
                btnSaveParams.Text = lm.Get("paramconfig_save");
            };

            this.ResumeLayout(false);
        }

        private void LstSamples_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lstSamples.SelectedItem != null && lblSelectedSampleTitle != null)
            {
                var LM = LanguageManager.Instance;
                lblSelectedSampleTitle.Text = string.Format(LM.Get("plan_param_selecting"), lstSamples.SelectedItem.ToString().Split(' ')[0]);
            }
        }

        private void PopulateParameters()
        {
            var LM = LanguageManager.Instance;
            flpParameters.Controls.Clear();
            string[] groups = { LM.Get("plan_param_grp_field"), LM.Get("plan_param_grp_lab_water"), LM.Get("plan_param_grp_heavy_metal") };
            string[][] items = {
                new string[] { "pH", "Nhiệt độ", "Độ đục", "DO" },
                new string[] { "BOD5", "COD", "TSS", "Tổng Nitơ", "Tổng Phốt pho", "Amoni", "Coliform" },
                new string[] { "Chì (Pb)", "Kẽm (Zn)", "Đồng (Cu)", "Thủy ngân (Hg)", "Asen (As)" }
            };

            for (int i = 0; i < groups.Length; i++)
            {
                // Group Header
                var lblGroup = new Label
                {
                    Text = groups[i],
                    Font = new Font("Segoe UI", 12, FontStyle.Bold),
                    ForeColor = UIConstants.LightGreenAccent, // Olive green
                    AutoSize = true,
                    Margin = new Padding(0, 15, 0, 10)
                };
                flpParameters.Controls.Add(lblGroup);
                flpParameters.SetFlowBreak(lblGroup, true);

                // Items Flow inside a sub-panel to allow multi-column checking
                var flpItems = new FlowLayoutPanel
                {
                    Width = 800,
                    Height = (int)Math.Ceiling(items[i].Length / 3.0) * 40,
                    WrapContents = true,
                    BackColor = Color.Transparent
                };

                foreach (var param in items[i])
                {
                    var chk = new Guna2CheckBox
                    {
                        Text = param,
                        Font = new Font("Segoe UI", 11),
                        ForeColor = UIConstants.TextDark,
                        CheckedState = { BorderColor = UIConstants.DarkGreenBackground, BorderRadius = 3, FillColor = UIConstants.DarkGreenBackground },
                        UncheckedState = { BorderColor = Color.Gray, BorderRadius = 3, FillColor = Color.White },
                        AutoSize = true,
                        Margin = new Padding(0, 0, 40, 15)
                    };
                    // Random check for demo
                    chk.Checked = (param == "pH" || param == "COD" || param == "TSS");
                    flpItems.Controls.Add(chk);
                }
                
                flpParameters.Controls.Add(flpItems);
                flpParameters.SetFlowBreak(flpItems, true);
            }
        }
    }
}
