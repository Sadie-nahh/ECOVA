using EnvContract.BLL.Interfaces;
using EnvContract.BLL.Services;
using EnvContract.Common.Constants;
using EnvContract.Common.Helpers;
using EnvContract.DAL.Interfaces;
using EnvContract.DTO.Entities;
using EnvContract.DTO.Responses;
using Guna.UI2.WinForms;
using Guna.Charts.WinForms;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using EnvContract.GUI.Helpers;
using EnvContract.GUI.Services;
using EnvContract.Common;

namespace EnvContract.GUI.UserControls.Dashboards
{
    public class DashboardUC : UserControl
    {
        // Layout
        private Action _langHandler;
        private Guna2TextBox    txtSearch;
        private Guna2Panel      pnlBox1, pnlBox2, pnlBox3;
        private Guna2Panel      pnlChart;
        private GunaChart       chart;
        private Panel dotGreen, dotOlive;
        private Label lblNew, lblDone;
        private Label _lblHeader, _lblBox1Title, _lblBox1Sub, _lblBox2Title, _lblChartTitle;

        // AI / Voice
        private AiIntegrationService _aiService;
        private VoiceSearchService   _voiceService;
        private List<string> _companyNames = new();

        // AI Box 3 — top 3 cao / top 3 thấp
        private List<AiPredictionResponse> _aiTopHigh = new();
        private List<AiPredictionResponse> _aiTopLow  = new();
        private string _aiUpdatedAt = "";

        // === DU LIEU THAT ===
        private int _pollutionRiskPercent = 0;
        private int _totalOrders = 0;          // Tong so don quan trac
        private int _pendingOrders = 0;        // So don chua hoan thanh

        // Hoat dong quan trac: so orders theo 5 moc
        private readonly int[] _activityPoints = { 0, 0, 0, 0, 0 };
        // Nhan thoi gian cho tung moc (hien thi trong tooltip)
        private readonly string[] _activityLabels = { "", "", "", "", "" };

        // Chart hop dong theo quy (rolling 4 quarters)
        private readonly int[] _newByQuarter   = { 0, 0, 0, 0 };
        private readonly int[] _doneByQuarter  = { 0, 0, 0, 0 };
        private readonly string[] _quarterLabels = { "Q1", "Q2", "Q3", "Q4" };

        // Tooltip
        private ToolTip _tooltip;
        private Point   _box2HoverPoint = Point.Empty;  // Toa do chuot trong pnlBox2
        private int     _box2HoverIdx   = -1;           // Index diem gan nhat

        public DashboardUC()
        {
            _voiceService = Program.ServiceProvider?.GetService<VoiceSearchService>();
            _tooltip = new ToolTip
            {
                AutoPopDelay = 5000,
                InitialDelay = 300,
                ReshowDelay  = 200,
                ShowAlways   = true,
                OwnerDraw    = false
            };
            InitializeComponent();
            _ = LoadAllDashboardDataAsync();
        }

        // =====================================================================
        //  BUILD UI
        // =====================================================================
        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.Dock       = DockStyle.Fill;
            this.BackColor  = UIConstants.WhiteBackground;
            this.AutoScroll = true;

            var LM = LanguageManager.Instance;
            _lblHeader = new Label
            {
                Text      = LM.Get("dashboard_header"),
                Font      = new Font("Segoe UI", 24, FontStyle.Bold),
                ForeColor = UIConstants.DarkGreenBackground,
                Location  = new Point(30, 25),
                AutoSize  = true
            };
            this.Controls.Add(_lblHeader);

            // Search bar
            txtSearch = new Guna2TextBox
            {
                Location             = new Point(410, 25),
                Size                 = new Size(650, 45),
                BorderRadius         = 15,
                PlaceholderText      = LM.Get("dashboard_search"),
                PlaceholderForeColor = Color.FromArgb(64, 64, 64),
                Font                 = new Font("Segoe UI", 11),
                FillColor            = Color.FromArgb(226, 232, 219),
                ForeColor            = UIConstants.TextDark,
                BorderColor          = Color.Black,
                BorderThickness      = 1,
                TextOffset           = new Point(10, 0),
            };
            this.Controls.Add(txtSearch);
            VoiceSearchHelper.AttachVoiceButton(txtSearch, this, _voiceService, () =>
            {
                if (_companyNames.Count == 0) return null;
                var sb = new System.Text.StringBuilder();
                foreach (var name in _companyNames)
                {
                    if (sb.Length + name.Length + 2 > 200) break;
                    if (sb.Length > 0) sb.Append(", ");
                    sb.Append(name);
                }
                return sb.Length > 0 ? sb.ToString() : null;
            });

            // ── BOX 1: Nguy co o nhiem Hom nay ──────────────────────────────
            pnlBox1 = new Guna2Panel
            {
                Location     = new Point(30, 100),
                Size         = new Size(350, 250),
                BorderRadius = 15,
                FillColor    = UIConstants.SoftLightGreen
            };
            _lblBox1Title = new Label { Text = LM.Get("dashboard_risk"), Font = new Font("Segoe UI", 11), ForeColor = UIConstants.TextDark, Location = new Point(20, 20), AutoSize = true, BackColor = Color.Transparent };
            _lblBox1Sub = new Label { Text = LM.Get("dashboard_today"), Font = new Font("Segoe UI", 14, FontStyle.Bold), ForeColor = Color.Black, Location = new Point(20, 40), AutoSize = true, BackColor = Color.Transparent };
            pnlBox1.Controls.Add(_lblBox1Title);
            pnlBox1.Controls.Add(_lblBox1Sub);

            pnlBox1.Paint += DrawGauge;
            // Tooltip khi hover vao gauge
            pnlBox1.MouseMove += (_, me) =>
            {
                var lm = LanguageManager.Instance;
                string eval = _pollutionRiskPercent < 30 ? lm.Get("dashboard_tip_low") : _pollutionRiskPercent < 60 ? lm.Get("dashboard_tip_med") : lm.Get("dashboard_tip_high");
                string tip = string.Format(lm.Get("dashboard_tip_risk"), _pollutionRiskPercent) + "\n" +
                             string.Format(lm.Get("dashboard_tip_pending"), _pendingOrders, _totalOrders) + "\n" +
                             string.Format(lm.Get("dashboard_tip_eval"), eval);
                _tooltip.SetToolTip(pnlBox1, tip);
            };
            this.Controls.Add(pnlBox1);

            // ── BOX 2: Hoat dong quan trac ──────────────────────────────────
            pnlBox2 = new Guna2Panel
            {
                Location     = new Point(410, 100),
                Size         = new Size(390, 250),
                BorderRadius = 15,
                FillColor    = UIConstants.LightGreenAccent
            };
            _lblBox2Title = new Label { Text = LM.Get("dashboard_activity"), Font = new Font("Segoe UI", 14, FontStyle.Bold), ForeColor = Color.Black, Location = new Point(20, 20), AutoSize = true, BackColor = Color.Transparent };
            pnlBox2.Controls.Add(_lblBox2Title);
            pnlBox2.Paint += DrawActivityChart;
            // Tooltip + hover highlight cho activity chart
            pnlBox2.MouseMove += OnBox2MouseMove;
            pnlBox2.MouseLeave += (_, __) =>
            {
                _box2HoverIdx = -1;
                _box2HoverPoint = Point.Empty;
                pnlBox2.Invalidate();
            };
            this.Controls.Add(pnlBox2);

            // ── BOX 3: Du bao AI ───────────────────────────────────────────────────
            pnlBox3 = new Guna2Panel
            {
                Location     = new Point(830, 100),
                Size         = new Size(240, 620),
                BorderRadius = 15,
                FillColor    = UIConstants.VeryLightGreen
            };
            pnlBox3.Paint += DrawAiBox3;
            this.Controls.Add(pnlBox3);

            // ── BOX 4: Chart Hop Dong ────────────────────────────────────────
            pnlChart = new Guna2Panel
            {
                Location     = new Point(30, 380),
                Size         = new Size(770, 340),
                BorderRadius = 15,
                FillColor    = Color.White
            };
            _lblChartTitle = new Label { Text = LM.Get("dashboard_chart_title"), Font = new Font("Segoe UI", 14, FontStyle.Bold), ForeColor = Color.Black, Location = new Point(20, 20), AutoSize = true, BackColor = Color.Transparent };
            pnlChart.Controls.Add(_lblChartTitle);

            dotGreen = new Panel { Location = new Point(480, 25), Size = new Size(15, 15), BackColor = Color.White };
            lblNew   = new Label { Text = LM.Get("dashboard_new_contract"), Location = new Point(505, 23), Font = new Font("Segoe UI", 10), AutoSize = true, ForeColor = Color.Black };
            dotOlive = new Panel { Location = new Point(620, 25), Size = new Size(15, 15), BackColor = Color.White };
            lblDone  = new Label { Text = LM.Get("dashboard_completed"),     Location = new Point(645, 23), Font = new Font("Segoe UI", 10), AutoSize = true, ForeColor = Color.Black };

            dotGreen.Paint += (_, e) => { e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; e.Graphics.Clear(Color.White); e.Graphics.FillEllipse(new SolidBrush(UIConstants.DarkGreenBackground), 0, 0, 14, 14); };
            dotOlive.Paint += (_, e) => { e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; e.Graphics.Clear(Color.White); e.Graphics.FillEllipse(new SolidBrush(UIConstants.LightGreenAccent), 0, 0, 14, 14); };

            pnlChart.Controls.AddRange(new Control[] { dotGreen, lblNew, dotOlive, lblDone });

            chart = new GunaChart
            {
                Location  = new Point(20, 60),
                Size      = new Size(730, 260),
                BackColor = Color.White
            };
            chart.YAxes.GridLines.Display = true;
            chart.YAxes.GridLines.Color   = Color.FromArgb(230, 230, 230);
            chart.XAxes.GridLines.Display = false;
            chart.Legend.Display          = false;
            BuildChartDatasets();

            pnlChart.Controls.Add(chart);
            this.Controls.Add(pnlChart);

            this.Resize += (_, __) => DoLayout();

            // Default language handler definition
            _langHandler = () =>
            {
                if (this.IsDisposed) return;

                var lm = LanguageManager.Instance;
                if (_lblHeader != null) _lblHeader.Text = lm.Get("dashboard_header");
                if (txtSearch != null) txtSearch.PlaceholderText = lm.Get("dashboard_search");
                if (_lblBox1Title != null) _lblBox1Title.Text = lm.Get("dashboard_risk");
                if (_lblBox1Sub != null) _lblBox1Sub.Text = lm.Get("dashboard_today");
                if (_lblBox2Title != null) _lblBox2Title.Text = lm.Get("dashboard_activity");
                if (_lblChartTitle != null) _lblChartTitle.Text = lm.Get("dashboard_chart_title");
                if (lblNew != null) lblNew.Text = lm.Get("dashboard_new_contract");
                if (lblDone != null) lblDone.Text = lm.Get("dashboard_completed");
                
                // Cập nhật nhãn ToolTip nếu có hover
                if (_box2HoverIdx >= 0) OnBox2MouseMove(this, new MouseEventArgs(MouseButtons.None, 0, _box2HoverPoint.X, _box2HoverPoint.Y, 0));

                BuildChartDatasets(); // Chart labels will reload using LanguageManager inside
                pnlBox1?.Invalidate(); 
                pnlBox2?.Invalidate();
                pnlBox3?.Invalidate(); 
            };
            LanguageManager.Instance.LanguageChanged += _langHandler;
            _langHandler(); // Apply immediately

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
        //  VE GAUGE: Nguy Co O Nhiem (du lieu that)
        // =====================================================================
        private void DrawGauge(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int arcW = 180, arcH = 180;
            int arcX = (pnlBox1.Width - arcW) / 2, arcY = 78;
            var arc = new Rectangle(arcX, arcY, arcW, arcH);

            // Mau nen cung (xam nhat)
            using var penBg = new Pen(Color.FromArgb(200, 200, 200), 22)
            {
                StartCap = LineCap.Round, EndCap = LineCap.Round
            };
            g.DrawArc(penBg, arc, 180, 180);

            // Mau ket qua: xanh (<30%), vang (30-60%), do (>60%)
            Color gaugeColor = _pollutionRiskPercent < 30
                ? UIConstants.DarkGreenBackground
                : _pollutionRiskPercent < 60
                    ? Color.Orange
                    : Color.Crimson;

            using var penFg = new Pen(gaugeColor, 22)
            {
                StartCap = LineCap.Round, EndCap = LineCap.Round
            };
            float sweepAngle = (_pollutionRiskPercent / 100f) * 180f;
            if (sweepAngle > 0)
                g.DrawArc(penFg, arc, 180, sweepAngle);

            // % text
            string pct = $"{_pollutionRiskPercent}%";
            using var fPct = new Font("Segoe UI", 22, FontStyle.Bold);
            var sz = g.MeasureString(pct, fPct);
            g.DrawString(pct, fPct, new SolidBrush(gaugeColor), arcX + arcW / 2 - sz.Width / 2, arcY + 50);

            // Stats phia duoi
            var LM = LanguageManager.Instance;
            using var fL = new Font("Segoe UI", 9.5f);
            using var fV = new Font("Segoe UI", 10.5f, FontStyle.Bold);
            using var bL = new SolidBrush(Color.FromArgb(80, 80, 80));
            g.DrawString(LM.Get("dashboard_env_wind"),     fL, bL, 22,                    200); g.DrawString("12km/h",  fV, Brushes.Black, 10,                    217);
            g.DrawString(LM.Get("dashboard_env_humidity"),  fL, bL, 148,                   200); g.DrawString("86%",     fV, Brushes.Black, 153,                   217);
            g.DrawString(LM.Get("dashboard_env_pressure"), fL, bL, pnlBox1.Width - 80, 200); g.DrawString("1007hPa", fV, Brushes.Black, pnlBox1.Width - 88, 217);
        }

        // =====================================================================
        //  VE ACTIVITY CHART: Hoat Dong Quan Trac (du lieu that)
        // =====================================================================
        private void DrawActivityChart(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int w2 = pnlBox2.Width, h2 = pnlBox2.Height;

            // Nen bong mo
            using var bgBrush = new SolidBrush(Color.FromArgb(35, 255, 255, 255));
            g.FillEllipse(bgBrush, new Rectangle(-w2 / 4, h2 / 2, w2 + w2 / 2, h2));

            // Ve duong line dua vao du lieu that (_activityPoints)
            int pad = 25, topPad = 55, bottomPad = 30;
            int plotW = w2 - pad * 2;
            int plotH = h2 - topPad - bottomPad;

            int maxVal = _activityPoints.Max() == 0 ? 1 : _activityPoints.Max();
            int n = _activityPoints.Length;

            // Tinh toa do cac diem
            Point[] pts = new Point[n];
            for (int i = 0; i < n; i++)
            {
                int px = pad + (plotW * i / (n - 1));
                int py = topPad + plotH - (plotH * _activityPoints[i] / maxVal);
                pts[i] = new Point(px, py);
            }

            // Ve duong cong mo
            using var shadowPen = new Pen(Color.FromArgb(60, UIConstants.DarkGreenBackground), 10)
            {
                StartCap = LineCap.Round, EndCap = LineCap.Round
            };
            g.DrawCurve(shadowPen, pts, 0.35f);

            // Ve duong cong chinh
            using var linePen = new Pen(UIConstants.DarkGreenBackground, 4)
            {
                StartCap = LineCap.Round, EndCap = LineCap.Round
            };
            g.DrawCurve(linePen, pts, 0.35f);

            // Ve cac diem noi bat (chi hien diem cao nhat va thap nhat)
            int maxIdx = Array.IndexOf(_activityPoints, _activityPoints.Max());
            int minIdx = Array.IndexOf(_activityPoints, _activityPoints.Min());
            foreach (int idx in new[] { maxIdx, minIdx })
            {
                g.FillEllipse(Brushes.White, pts[idx].X - 8, pts[idx].Y - 8, 16, 16);
                g.DrawEllipse(linePen,       pts[idx].X - 8, pts[idx].Y - 8, 16, 16);
            }

            // Highlight dot theo vi tri hover (neu co)
            if (_box2HoverIdx >= 0 && _box2HoverIdx < n)
            {
                using var hPen = new Pen(Color.White, 2);
                g.FillEllipse(new SolidBrush(Color.FromArgb(220, UIConstants.DarkGreenBackground)),
                    pts[_box2HoverIdx].X - 10, pts[_box2HoverIdx].Y - 10, 20, 20);
                g.DrawEllipse(hPen, pts[_box2HoverIdx].X - 10, pts[_box2HoverIdx].Y - 10, 20, 20);

                // Hien so lieu tai diem hover (popup nho ngay tren diem)
                using var fNum = new Font("Segoe UI", 9, FontStyle.Bold);
                var lm = LanguageManager.Instance;
                string hoverLabel = $"{_activityPoints[_box2HoverIdx]} {lm.Get("dashboard_tip_count_suffix")}";
                var szH = g.MeasureString(hoverLabel, fNum);
                float hx = pts[_box2HoverIdx].X - szH.Width / 2;
                float hy = pts[_box2HoverIdx].Y - 32;
                // Clamp khong ra ngoai box
                hx = Math.Clamp(hx, 4, w2 - szH.Width - 4);

                var bg = new RectangleF(hx - 5, hy - 2, szH.Width + 10, szH.Height + 4);
                using var bgBrush2 = new SolidBrush(Color.FromArgb(230, UIConstants.DarkGreenBackground));
                g.FillRectangle(bgBrush2, bg);
                g.DrawString(hoverLabel, fNum, Brushes.White, hx, hy);
            }
        }


        // Xu ly MouseMove tren activity chart (pnlBox2)
        private void OnBox2MouseMove(object sender, MouseEventArgs me)
        {
            int w2 = pnlBox2.Width;
            int pad = 25;
            int plotW = w2 - pad * 2;
            int n = _activityPoints.Length;

            // Tim diem gan nhat theo chieu ngang
            int newIdx = -1;
            int minDist = int.MaxValue;
            for (int i = 0; i < n; i++)
            {
                int px = pad + (plotW * i / (n - 1));
                int dist = Math.Abs(me.X - px);
                if (dist < minDist) { minDist = dist; newIdx = i; }
            }

            // Chi cap nhat neu khoang cach < 40px
            int finalIdx = minDist < 40 ? newIdx : -1;

            if (finalIdx != _box2HoverIdx)
            {
                _box2HoverIdx = finalIdx;
                pnlBox2.Invalidate();

                if (finalIdx >= 0)
                {
                    string label = _activityLabels[finalIdx];
                    int    count = _activityPoints[finalIdx];
                    var lm = LanguageManager.Instance;
                    string tip = $"{label}\n" + string.Format(lm.Get("dashboard_tip_activity"), count, lm.Get("dashboard_tip_count_suffix"));
                    _tooltip.SetToolTip(pnlBox2, tip);
                }
                else
                {
                    _tooltip.SetToolTip(pnlBox2, "");
                }
            }
        }

        // =====================================================================
        //  CHART DATASETS
        // =====================================================================
        private void BuildChartDatasets()
        {
            chart.Datasets.Clear();

            var LM = LanguageManager.Instance;
            var dsNew = new GunaSplineDataset
            {
                Label       = LM.Get("dashboard_new_contract"),
                BorderColor = UIConstants.DarkGreenBackground,
                FillColor   = Color.Transparent,
                BorderWidth = 3,
                PointRadius = 3
            };
            var dsDone = new GunaSplineDataset
            {
                Label       = LM.Get("dashboard_completed"),
                BorderColor = UIConstants.LightGreenAccent,
                FillColor   = Color.Transparent,
                BorderWidth = 3,
                PointRadius = 3
            };

            // Dung nhan dong ten quy thuc te (Q2/25, Q3/25... Q1/26)
            for (int i = 0; i < 4; i++)
            {
                dsNew.DataPoints.Add(_quarterLabels[i],  _newByQuarter[i]);
                dsDone.DataPoints.Add(_quarterLabels[i], _doneByQuarter[i]);
            }
            chart.Datasets.Add(dsNew);
            chart.Datasets.Add(dsDone);

            // Style tooltip dep
            chart.Tooltips.Enabled         = true;
            chart.Tooltips.CornerRadius    = 8;
            chart.Tooltips.BackgroundColor = UIConstants.DarkGreenBackground;
            chart.Tooltips.TitleForeColor  = Color.White;
            chart.Tooltips.BodyForeColor   = Color.FromArgb(220, 255, 220);
            chart.Tooltips.DisplayColors   = true;

            chart.Update();
        }


        // =====================================================================
        //  LOAD DU LIEU THAT TU DATABASE
        // =====================================================================
        private async Task LoadAllDashboardDataAsync()
        {
            try
            {
                // Lay cac service tu DI
                var contractService   = Program.ServiceProvider.GetService<IContractService>();
                var orderRepo         = Program.ServiceProvider.GetService<IOrderRepository>();
                var sampleRepo        = Program.ServiceProvider.GetService<ISampleRepository>();
                var testResultRepo    = Program.ServiceProvider.GetService<ITestResultRepository>();

                try { _aiService = Program.ServiceProvider.GetRequiredService<AiIntegrationService>(); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[DashboardUC] Error: {ex.Message}"); }

                // Chay song song de giam thoi gian load
                var taskContracts = contractService != null
                    ? Task.Run(() => contractService.GetContractCardsAsync())
                    : Task.FromResult(new List<ContractCardDTO>());

                var taskOrders = orderRepo != null
                    ? Task.Run(() => orderRepo.GetAllOrdersAsync())
                    : Task.FromResult(new List<OrderDTO>());

                await Task.WhenAll(taskContracts, taskOrders);

                var cards  = taskContracts.Result ?? new List<ContractCardDTO>();
                var orders = taskOrders.Result ?? new List<OrderDTO>();

                // ── 1. TINH NGUY CO O NHIEM (tu TestResults.IsWarning) ──────
                // Lay tat ca samples de lay SampleID => lay TestResults
                // Vi khong co GetAll trong TestResultRepo, dung du lieu tu orders
                // Tinh: % orders co IsApproved = 0 (chua duoc duyet / co van de) trong 30 ngay
                _pollutionRiskPercent = CalculatePollutionRisk(orders);

                // ── 2. HOAT DONG QUAN TRAC: So Orders theo 5 moc (moi 2 thang) ──
                CalculateActivityPoints(orders);

                // ── 3. HOP DONG THEO QUY ────────────────────────────────────
                _companyNames = cards
                    .Where(c => !string.IsNullOrWhiteSpace(c.CompanyName))
                    .Select(c => c.CompanyName)
                    .Distinct().Take(30).ToList();

                CalculateQuarterlyData(cards);

                // Tinh AI risk — group by CustomerId de tranh trung lap
                List<(string, string, float, string, float, float)> aiCustomers = new();
                if (_aiService != null && cards.Count > 0)
                {
                    // Gop theo CustomerId: lay 1 ban ghi / khach (chon HD gia tri cao nhat)
                    var perCustomer = cards
                        .GroupBy(c => c.CustomerId ?? c.CompanyName)
                        .Select(grp =>
                        {
                            var best = grp.OrderByDescending(c => c.TotalContractValue).First();
                            
                            return (
                                best.CustomerId ?? "",
                                best.CompanyName ?? "",
                                (float)best.TotalContractValue,
                                best.IndustryType ?? "Manufacturing",
                                best.ResponseTime,
                                best.PreviousViolations
                            );
                        })
                        .ToList();
                    aiCustomers = perCustomer;
                }

                // Cap nhat UI tren UI thread
                Action updateUI = () =>
                {
                    BuildChartDatasets();
                    pnlBox1?.Invalidate();  // Ve lai gauge voi du lieu that
                    pnlBox2?.Invalidate();  // Ve lai activity chart
                    UpdateAiUI(aiCustomers);
                };

                if (this.InvokeRequired) this.Invoke(updateUI);
                else updateUI();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Dashboard] L\u1ed7i load data: {ex.Message}");
            }
        }

        // =====================================================================
        //  TINH NGUY CO O NHIEM
        //  Logic: Day la % don quan trac CHA duoc phe duyet (IsApproved=0)
        //  trong 60 ngay gan nhat → phan anh "con dong" chua xu ly = rui ro
        //  Se duoc thay the bang TestResults.IsWarning khi co API cap nhat
        // =====================================================================
        private int CalculatePollutionRisk(List<OrderDTO> orders)
        {
            if (orders.Count == 0) { _totalOrders = 0; _pendingOrders = 0; return 0; }

            var cutoff  = DateTime.Now.AddDays(-60);
            var recent  = orders.Where(o => o.OrderDate.HasValue && o.OrderDate.Value >= cutoff).ToList();
            if (recent.Count == 0) recent = orders.ToList();

            _totalOrders   = recent.Count;
            int completed  = recent.Count(o => o.Status == (int)OrderStatus.Completed);
            _pendingOrders = _totalOrders - completed;

            int riskRaw = _totalOrders > 0
                ? (int)Math.Round((double)_pendingOrders / _totalOrders * 100)
                : 0;

            return Math.Clamp(riskRaw, 0, 99);
        }

        // =====================================================================
        //  TINH HOAT DONG QUAN TRAC
        //  5 diem, moi diem = so orders trong moi khung 2.4 thang (12 thang / 5)
        // =====================================================================
        private void CalculateActivityPoints(List<OrderDTO> orders)
        {
            for (int i = 0; i < 5; i++) { _activityPoints[i] = 0; _activityLabels[i] = ""; }
            if (orders.Count == 0) return;

            var now       = DateTime.Now;
            double stepDays = 365.0 / 5;   // ~73 ngay / moc

            for (int i = 0; i < 5; i++)
            {
                var from = now.AddDays(-(stepDays * (5 - i)));
                var to   = now.AddDays(-(stepDays * (4 - i)));

                _activityPoints[i] = orders.Count(o =>
                    o.OrderDate.HasValue &&
                    o.OrderDate.Value >= from &&
                    o.OrderDate.Value <  to);

                // Nhan thoi gian: "Th[from] - Th[to]"
                _activityLabels[i] = $"T{from:MM/yy} – T{to:MM/yy}";
            }

            // Fallback neu tat ca = 0
            if (_activityPoints.All(x => x == 0))
            {
                var sorted = orders.OrderBy(o => o.OrderDate).ToList();
                int n   = sorted.Count;
                int per = Math.Max(1, n / 5);
                for (int i = 0; i < 5; i++)
                {
                    var slice = sorted.Skip(i * per).Take(per).ToList();
                    _activityPoints[i] = slice.Count;
                    if (slice.Count > 0 && slice[0].OrderDate.HasValue)
                    {
                        var f = slice[0].OrderDate!.Value;
                        var t = slice[^1].OrderDate!.Value;
                        _activityLabels[i] = $"T{f:MM/yy} – T{t:MM/yy}";
                    }
                    else
                    {
                        var lm = LanguageManager.Instance;
                        _activityLabels[i] = $"{lm.Get("dashboard_phase")} {i + 1}";
                    }
                }
            }
        }

        // =====================================================================
        //  TINH HOP DONG THEO QUY (12 THANG GAN NHAT)
        // =====================================================================
        private void CalculateQuarterlyData(List<ContractCardDTO> cards)
        {
            for (int q = 0; q < 4; q++) { _newByQuarter[q] = 0; _doneByQuarter[q] = 0; }
            if (cards.Count == 0) return;

            // Quy hien tai va nam hien tai
            var now    = DateTime.Now;
            int curM   = now.Month;    // 1-12
            int curY   = now.Year;
            // Quarter hien tai tinh theo "quy tich luy": Y*4 + (M-1)/3
            int curQKey = curY * 4 + (curM - 1) / 3;

            // Tao 4 rolling quarter anh xa: qIdx=3=moi nhat, qIdx=0=cu nhat
            // Build quarter labels
            for (int qi = 0; qi < 4; qi++)
            {
                int qKey    = curQKey - 3 + qi;
                int qYear   = qKey / 4;
                int qNum    = (qKey % 4 + 4) % 4 + 1; // 1..4
                _quarterLabels[qi] = $"Q{qNum}/{qYear % 100:D2}";
            }

            // Phan loai tung hop dong vao 1 trong 4 rolling quarters
            var cutoff = now.AddMonths(-15); // mo rong de lay het du lieu
            foreach (var c in cards)
            {
                if (c.SignedDate < cutoff) continue;
                int cQKey = c.SignedDate.Year * 4 + (c.SignedDate.Month - 1) / 3;
                int qIdx  = cQKey - curQKey + 3; // qIdx=3 la hien tai
                if (qIdx < 0 || qIdx > 3) continue;

                _newByQuarter[qIdx]++;
                // "Hoan thanh" = HD da xu ly xong (Expiring/Expired), KHONG phai Active
                if (c.Status == (int)ContractStatus.Expiring || c.Status == (int)ContractStatus.Expired)
                    _doneByQuarter[qIdx]++;
            }

            // Fallback neu tat ca = 0
            if (_newByQuarter.All(x => x == 0) && cards.Count > 0)
            {
                var sorted = cards.OrderBy(c => c.SignedDate).ToList();
                int total  = sorted.Count;
                int[] splits = {
                    total * 20 / 100, total * 25 / 100, total * 30 / 100,
                    total - (total * 20 / 100 + total * 25 / 100 + total * 30 / 100)
                };
                int offset = 0;
                for (int qi = 0; qi < 4; qi++)
                {
                    var slice = sorted.Skip(offset).Take(splits[qi]).ToList();
                    _newByQuarter[qi]  = slice.Count;
                    _doneByQuarter[qi] = slice.Count(c => c.Status == (int)ContractStatus.Expiring
                                                       || c.Status == (int)ContractStatus.Expired);
                    offset += splits[qi];
                }
            }
        }

        // =====================================================================
        //  CAP NHAT AI BOX 3 — Top 3 cao + Top 3 thap
        // =====================================================================
        private void UpdateAiUI(List<(string, string, float, string, float, float)> customers)
        {
            _aiTopHigh.Clear();
            _aiTopLow.Clear();

            if (customers.Count == 0)
            {
                _aiUpdatedAt = "Chưa có dữ liệu";
                pnlBox3?.Invalidate();
                return;
            }
            try
            {
                if (_aiService != null)
                {
                    // Chay predict cho tat ca
                    var allPredictions = new List<AiPredictionResponse>();
                    foreach (var c in customers)
                    {
                        var pred = _aiService.PredictRenewal(
                            c.Item3, c.Item4, c.Item5, c.Item6,
                            c.Item1, c.Item2);
                        allPredictions.Add(pred);
                    }

                    // Top 3 kha nang TAI KY CAO NHAT
                    _aiTopHigh = allPredictions
                        .OrderByDescending(r => r.RenewalProbabilityScore)
                        .Take(3).ToList();

                    // Top 3 kha nang TAI KY THAP NHAT (nguy co mat)
                    _aiTopLow = allPredictions
                        .OrderBy(r => r.RenewalProbabilityScore)
                        .Take(3).ToList();

                    _aiUpdatedAt = DateTime.Now.ToString("HH:mm dd/MM/yyyy");
                }
                // Gauge o nhiem KHONG bi AI de — giu nguyen _pollutionRiskPercent tu orders
                pnlBox3?.Invalidate();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Dashboard] Lỗi AI: {ex.Message}");
                _aiUpdatedAt = "Lỗi dự đoán AI";
                pnlBox3?.Invalidate();
            }
        }

        // =====================================================================
        //  VE AI BOX 3: Custom Paint — Top 3 Cao + Top 3 Thap
        // =====================================================================
        private void DrawAiBox3(object sender, PaintEventArgs e)
        {
            var g  = e.Graphics;
            g.SmoothingMode     = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int W  = pnlBox3.Width;
            int H  = pnlBox3.Height;
            int cx = W / 2;

            using var fTitle  = new Font("Segoe UI", 11, FontStyle.Bold);
            using var fSub    = new Font("Segoe UI", 8.5f);
            using var fName   = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            using var fPct    = new Font("Segoe UI", 10, FontStyle.Bold);
            using var fSmall  = new Font("Segoe UI", 7.5f);
            using var fStats  = new Font("Segoe UI", 9, FontStyle.Bold);
            using var fStatsV = new Font("Segoe UI", 14, FontStyle.Bold);

            using var bDark    = new SolidBrush(UIConstants.DarkGreenBackground);
            using var bGray    = new SolidBrush(Color.FromArgb(100, 100, 100));
            using var penDiv   = new Pen(Color.FromArgb(180, 180, 180), 1);
            using var bHighHdr = new SolidBrush(Color.FromArgb(200, 235, 200));
            using var bLowHdr  = new SolidBrush(Color.FromArgb(255, 220, 210));
            using var bHighTxt = new SolidBrush(Color.FromArgb(30, 100, 30));
            using var bLowTxt  = new SolidBrush(Color.FromArgb(160, 40, 20));

            // ── ICON AI VẼ TAY (chip mạch) ───────────────────────────────────
            DrawAiChipIcon(g, cx, 8);

            var LM = LanguageManager.Instance;
            // ── Tiêu đề ──────────────────────────────────────────────────────
            string titleTxt = LM.Get("dashboard_ai_title");
            var szT = g.MeasureString(titleTxt, fTitle);
            g.DrawString(titleTxt, fTitle, bDark, cx - szT.Width / 2, 46);

            string subTxt = LM.Get("dashboard_ai_sub");
            var szS = g.MeasureString(subTxt, fSub);
            g.DrawString(subTxt, fSub, bGray, cx - szS.Width / 2, 66);

            g.DrawLine(penDiv, 15, 86, W - 15, 86);

            // Nếu chưa có dữ liệu
            if (_aiTopHigh.Count == 0 && _aiTopLow.Count == 0)
            {
                g.DrawString(LM.Get("dashboard_ai_analyzing"), fSub, bGray, 20, 103);
                return;
            }

            int y     = 94;
            int cardH = 52;
            int barH  = 7;
            int padX  = 12;

            // ── SECTION: KHẢ NĂNG CAO ─────────────────────────────────────────
            g.FillRectangle(bHighHdr, 8, y, W - 16, 20);
            using var penCheck = new Pen(Color.FromArgb(30, 100, 30), 2f) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
            g.DrawLine(penCheck, 14, y + 12, 17, y + 15);
            g.DrawLine(penCheck, 17, y + 15, 22, y + 8);
            g.DrawString("  " + LM.Get("dashboard_ai_high"), fSmall, bHighTxt, 14, y + 3);
            y += 22;

            foreach (var r in _aiTopHigh)
            {
                DrawAiCard(g, r, y, cardH, barH, padX, W, fName, fPct, fSmall, isHigh: true);
                y += cardH + 3;
            }

            y += 4;
            g.DrawLine(penDiv, 15, y, W - 15, y);
            y += 6;

            // ── SECTION: NGUY CƠ THẤP ────────────────────────────────────────
            g.FillRectangle(bLowHdr, 8, y, W - 16, 20);
            using var penWarn   = new Pen(Color.FromArgb(160, 40, 20), 1.5f);
            using var brushWarn = new SolidBrush(Color.FromArgb(220, 80, 30));
            var warnPts = new PointF[] {
                new PointF(14, y + 16), new PointF(18, y + 8), new PointF(22, y + 16)
            };
            g.FillPolygon(brushWarn, warnPts);
            g.DrawPolygon(penWarn, warnPts);
            using var fWarnDot = new Font("Segoe UI", 5.5f, FontStyle.Bold);
            g.DrawString("!", fWarnDot, Brushes.White, 16.5f, y + 9.5f);
            g.DrawString("  " + LM.Get("dashboard_ai_low"), fSmall, bLowTxt, 14, y + 3);
            y += 22;

            foreach (var r in _aiTopLow)
            {
                DrawAiCard(g, r, y, cardH, barH, padX, W, fName, fPct, fSmall, isHigh: false);
                y += cardH + 3;
            }

            // ── BLOCK TONG KET (lap day phan trong) ───────────────────────────
            int summaryY = y + 10;
            int allCount = _aiTopHigh.Count + _aiTopLow.Count;
            double avgHigh = _aiTopHigh.Count > 0 ? _aiTopHigh.Average(r => r.RenewalProbabilityScore) : 0;
            double avgLow  = _aiTopLow.Count  > 0 ? _aiTopLow.Average(r => r.RenewalProbabilityScore)  : 0;

            g.DrawLine(penDiv, 15, summaryY, W - 15, summaryY);
            summaryY += 8;

            // Tieu de tong ket
            string sumLabel = LM.Get("dashboard_ai_summary");
            var szSum = g.MeasureString(sumLabel, fSmall);
            g.DrawString(sumLabel, fSmall, bGray, cx - szSum.Width / 2, summaryY);
            summaryY += 16;

            // 2 stat blocks: Trung binh cao / Trung binh thap
            int statW  = (W - 26) / 2;
            int statH  = 52;
            int stat1X = 8;
            int stat2X = stat1X + statW + 6;

            using var bStatBg1 = new SolidBrush(Color.FromArgb(180, 225, 180));
            using var bStatBg2 = new SolidBrush(Color.FromArgb(255, 210, 200));

            // Stat 1 (Average High)
            g.FillRectangle(bStatBg1, stat1X, summaryY, statW, statH);
            string v1 = $"{avgHigh:F0}%";
            var sz1 = g.MeasureString(v1, fStatsV);
            g.DrawString(v1, fStatsV, new SolidBrush(Color.FromArgb(30, 120, 50)),
                stat1X + statW / 2 - sz1.Width / 2, summaryY + 6);
            string l1 = LM.Get("dashboard_ai_avg_high");
            var sl1 = g.MeasureString(l1, fSmall);
            g.DrawString(l1, fSmall, bHighTxt, stat1X + statW / 2 - sl1.Width / 2, summaryY + statH - 18);

            // Stat 2 (Average Low)
            g.FillRectangle(bStatBg2, stat2X, summaryY, statW, statH);
            string v2 = $"{(100 - avgLow):F0}%";
            var sz2 = g.MeasureString(v2, fStatsV);
            g.DrawString(v2, fStatsV, new SolidBrush(Color.Crimson),
                stat2X + statW / 2 - sz2.Width / 2, summaryY + 6);
            string l2 = LM.Get("dashboard_ai_avg_low");
            var sl2 = g.MeasureString(l2, fSmall);
            g.DrawString(l2, fSmall, bLowTxt, stat2X + statW / 2 - sl2.Width / 2, summaryY + statH - 18);

            // ── FOOTER ─────────────────────────────────────────────────────────
            g.DrawLine(penDiv, 15, H - 22, W - 15, H - 22);
            string footerPrefix = LM.IsVietnamese ? "Cập nhật:" : "Updated:";
            g.DrawString($"{footerPrefix} {_aiUpdatedAt}", fSmall, bGray,
                new RectangleF(8, H - 19, W - 16, 18));
        }

        /// <summary>Vẽ icon chip AI bằng GDI+ thuần — không dùng emoji</summary>
        private void DrawAiChipIcon(Graphics g, int cx, int topY)
        {
            int sz   = 28;    // kich thuoc chip chinh
            int x    = cx - sz / 2;
            int y    = topY;
            int pLen = 5;     // do dai chan chip
            int pGap = 6;     // khoang cach giua cac chan

            using var penChip  = new Pen(UIConstants.DarkGreenBackground, 1.5f);
            using var brushChip = new SolidBrush(Color.FromArgb(220, 240, 215));
            using var penPin   = new Pen(Color.FromArgb(80, 130, 80), 1.5f);
            using var brushCore = new SolidBrush(UIConstants.DarkGreenBackground);
            using var fCore    = new Font("Segoe UI", 6.5f, FontStyle.Bold);

            // Than chip (hinh chu nhat goc tron)
            var chipRect = new Rectangle(x, y, sz, sz);
            g.FillRectangle(brushChip, chipRect);
            g.DrawRectangle(penChip, chipRect);

            // Chan chip (3 chan tren + 3 chan duoi)
            for (int i = 0; i < 3; i++)
            {
                int px = x + 4 + i * pGap;
                // Chan tren
                g.DrawLine(penPin, px, y, px, y - pLen);
                g.FillRectangle(new SolidBrush(Color.FromArgb(100, 140, 90)),
                    new Rectangle(px - 1, y - pLen - 2, 3, 3));
                // Chan duoi
                g.DrawLine(penPin, px, y + sz, px, y + sz + pLen);
                g.FillRectangle(new SolidBrush(Color.FromArgb(100, 140, 90)),
                    new Rectangle(px - 1, y + sz + pLen, 3, 3));
            }
            // Chan trai + phai (2 chan)
            for (int i = 0; i < 2; i++)
            {
                int py = y + 6 + i * pGap;
                g.DrawLine(penPin, x, py, x - pLen, py);
                g.FillRectangle(new SolidBrush(Color.FromArgb(100, 140, 90)),
                    new Rectangle(x - pLen - 2, py - 1, 3, 3));
                g.DrawLine(penPin, x + sz, py, x + sz + pLen, py);
                g.FillRectangle(new SolidBrush(Color.FromArgb(100, 140, 90)),
                    new Rectangle(x + sz + pLen, py - 1, 3, 3));
            }

            // Loi chip: hinh tron xanh voi chu "AI"
            int coreR = 9;
            int coreX = x + sz / 2 - coreR;
            int coreY = y + sz / 2 - coreR;
            g.FillEllipse(brushCore, coreX, coreY, coreR * 2, coreR * 2);
            var szAI = g.MeasureString("AI", fCore);
            g.DrawString("AI", fCore, Brushes.White,
                x + sz / 2 - szAI.Width / 2,
                y + sz / 2 - szAI.Height / 2);
        }


        private void DrawAiCard(
            Graphics g, AiPredictionResponse r,
            int y, int cardH, int barH, int padX, int W,
            Font fName, Font fPct, Font fSmall, bool isHigh)
        {
            double score = Math.Clamp(r.RenewalProbabilityScore, 0, 100);

            // Nen card
            using var cardBg = new SolidBrush(Color.FromArgb(20, isHigh ? Color.LimeGreen : Color.OrangeRed));
            g.FillRectangle(cardBg, padX - 2, y, W - padX * 2 + 4, cardH - 2);

            // % score — do truoc de tinh vung cho ten
            Color pctColor = score >= 70 ? Color.FromArgb(30, 130, 60)
                           : score >= 50 ? Color.FromArgb(180, 120, 0)
                                         : Color.Crimson;
            using var bPct = new SolidBrush(pctColor);
            string pctTxt = $"{score:F0}%";
            var szPct = g.MeasureString(pctTxt, fPct);
            g.DrawString(pctTxt, fPct, bPct, W - padX - szPct.Width, y + 4);

            // Ten cong ty — hien thi DAY DU, wrap neu can trong pham vi con lai
            string name = r.CompanyName ?? "?";
            int nameMaxW = (int)(W - padX * 2 - szPct.Width - 6);  // tranh de len %
            g.DrawString(name, fName, Brushes.Black,
                new RectangleF(padX, y + 4, nameMaxW, cardH - 8),
                new StringFormat
                {
                    Trimming    = StringTrimming.EllipsisWord,
                    FormatFlags = StringFormatFlags.NoWrap
                });

            // Progress bar
            int barY    = y + 23;
            int barXEnd = W - padX - (int)szPct.Width - 4;
            int actualW = Math.Max(barXEnd - padX, 10);

            using var bgBar = new SolidBrush(Color.FromArgb(210, 210, 210));
            g.FillRectangle(bgBar, new Rectangle(padX, barY, actualW, barH));

            int fillW = Math.Max((int)(actualW * score / 100.0), 1);
            using var fgBar = new SolidBrush(pctColor);
            g.FillRectangle(fgBar, new Rectangle(padX, barY, fillW, barH));

            // Mo ta ngan gon (1 dong)
            var LM = LanguageManager.Instance;
            string detail = score >= 80 ? LM.Get("dashboard_ai_high_detail") 
                          : score >= 50 ? LM.Get("dashboard_ai_mid_detail") 
                                        : LM.Get("dashboard_ai_low_detail");
            using var bDetail = new SolidBrush(Color.FromArgb(85, 85, 85));
            g.DrawString(detail, fSmall, bDetail, padX, barY + barH + 3);

        }

        // =====================================================================
        //  LAYOUT RESPONSIVE
        // =====================================================================
        protected override void OnLayout(LayoutEventArgs e) { base.OnLayout(e); DoLayout(); }

        private void DoLayout()
        {
            if (this.Width == 0 || this.Height == 0) return;

            int w  = Math.Max(this.Width, 1000);
            int h  = Math.Max(this.Height, 700);
            int pad = 30, gap = 20, hdrH = 90;

            int availW  = w - 2 * pad - 2 * gap;
            int box3W   = (int)(availW * 0.25);
            int box1W   = (int)(availW * 0.35);
            int box2W   = availW - box1W - box3W;
            int topRowH = Math.Max(250, (h - hdrH - pad * 2) * 40 / 100);
            int topY    = hdrH;

            txtSearch.SetBounds(pad + box1W + gap, 22, box2W, 45);

            pnlBox1.SetBounds(pad, topY, box1W, topRowH);
            pnlBox2.SetBounds(pad + box1W + gap, topY, box2W, topRowH);
            pnlBox3.SetBounds(pad + box1W + gap + box2W + gap, topY, box3W, h - topY - pad);


            int chartY = topY + topRowH + gap;
            int chartW = w - pad - box3W - gap - pad;
            int chartH = h - chartY - pad;
            pnlChart.SetBounds(pad, chartY, chartW, chartH);

            int innerW = Math.Max(50, chartW - 40);
            int innerH = Math.Max(40, chartH - 80);
            chart.SetBounds(20, 60, innerW, innerH);
            chart.Invalidate();

            lblDone.Left  = chartW - 120;
            dotOlive.Left = lblDone.Left  - 22;
            lblNew.Left   = dotOlive.Left - lblNew.Width - 15;
            dotGreen.Left = lblNew.Left   - 22;

            this.Invalidate();
        }
    }
}
