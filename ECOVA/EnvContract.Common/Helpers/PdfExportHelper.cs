using iText.IO.Font;
using iText.IO.Font.Constants;
using iText.IO.Image;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using iText.Kernel.Pdf.Canvas.Draw;
using iText.Kernel.Pdf.Extgstate;
using iText.Layout;
using iText.Layout.Borders;
using iText.Layout.Element;
using iText.Layout.Properties;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace EnvContract.Common.Helpers
{
    /// <summary>
    /// DTO dùng cho truyền dữ liệu vào PdfExportHelper
    /// </summary>
    public class PdfTestResultRow
    {
        public int STT { get; set; }
        public string ParamName { get; set; } = "";
        public string Unit { get; set; } = "";
        public string Method { get; set; } = "";
        public Dictionary<string, string> AreaResults { get; set; } = new Dictionary<string, string>();
        public string QcvnLimit { get; set; } = "";
    }

    public static class PdfExportHelper
    {
        // ── Brand colors ──────────────────────────────────────────────────────
        private static readonly DeviceRgb GreenHeader = new DeviceRgb(49,  87,  44);
        private static readonly DeviceRgb BorderGray  = new DeviceRgb(160, 160, 160);
        private static readonly DeviceRgb TextBlack   = new DeviceRgb(30,  30,  30);

        // ── Font sizes — nhỏ gọn để vừa 1 trang A4 ───────────────────────────
        private const float FS_COMPANY  = 14f;   // Tên công ty
        private const float FS_ADDR     = 9.5f;  // Địa chỉ / liên hệ
        private const float FS_TITLE    = 17f;   // PHIẾU KẾT QUẢ KIỂM THỬ
        private const float FS_SECTION  = 11f;   // I. / II.
        private const float FS_BODY     = 9.5f;  // Nội dung bảng
        private const float FS_SMALL    = 9f;    // Số phiếu, footer

        /// <summary>
        /// Backward-compatible: gọi GenerateStructuredPdf với dữ liệu tối giản.
        /// </summary>
        public static string GenerateTestResultPdf(
            string orderId, string customerName,
            string resultDataText, string outputDirectory)
        {
            return GenerateStructuredPdf(orderId, customerName, "", "",
                new List<string> { "KQ" }, new List<PdfTestResultRow>(), outputDirectory);
        }

        /// <summary>
        /// Tạo PHIẾU KẾT QUẢ KIỂM THỬ — toàn bộ nội dung vừa 1 trang A4.
        /// Watermark: Icon.png ở giữa trang, opacity 15%.
        /// </summary>
        public static string GenerateStructuredPdf(
            string orderId,
            string customerName,
            string customerAddress,
            string sampleType,
            List<string> areaNames,
            List<PdfTestResultRow> resultRows,
            string outputDirectory,
            DateTime? sampleDate   = null,
            DateTime? analysisDate = null,
            DateTime? returnDate   = null)
        {
            if (!Directory.Exists(outputDirectory))
                Directory.CreateDirectory(outputDirectory);

            string fileName = $"PhieuKetQua_{orderId}_{DateTime.Now:yyyyMMddHHmmss}.pdf";
            string filePath = System.IO.Path.Combine(outputDirectory, fileName);

            using (PdfWriter   writer = new PdfWriter(filePath))
            using (PdfDocument pdf    = new PdfDocument(writer))
            {
                // Margin nhỏ để nội dung vừa 1 trang (usable height ≈ 752pt)
                Document doc = new Document(pdf, PageSize.A4);
                doc.SetMargins(28, 36, 42, 36);

                PdfFont font     = SafeCreateFont(false);
                PdfFont boldFont = SafeCreateFont(true);

                // ── 0. HEADER: Tên công ty ─────────────────────────────────
                doc.Add(new Paragraph("CÔNG TY MÔI TRƯỜNG ĐẠI NAM")
                    .SetFont(boldFont).SetFontSize(FS_COMPANY)
                    .SetFontColor(GreenHeader)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetMarginBottom(1));

                doc.Add(new Paragraph("Địa chỉ: 19 Nguyễn Hữu THọ, Phường Tân Hưng, Thành Phố Hồ Chí Minh")
                    .SetFont(font).SetFontSize(FS_ADDR)
                    .SetFontColor(TextBlack)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetMarginBottom(1));

                // Điện thoại | Email
                var contactTbl = new Table(UnitValue.CreatePercentArray(new float[] { 50, 50 }))
                    .UseAllAvailableWidth().SetMarginBottom(3);
                contactTbl.AddCell(NoBorderCell("Điện thoại: 028 3775 5052",      font, FS_ADDR, TextAlignment.LEFT));
                contactTbl.AddCell(NoBorderCell("Email: ecova.tdtu@gmail.com", font, FS_ADDR, TextAlignment.RIGHT));
                doc.Add(contactTbl);

                doc.Add(new LineSeparator(new SolidLine(0.8f)).SetMarginTop(1).SetMarginBottom(8));

                // ── 1. TIÊU ĐỀ ────────────────────────────────────────────
                doc.Add(new Paragraph("PHIẾU KẾT QUẢ KIỂM THỬ")
                    .SetFont(boldFont).SetFontSize(FS_TITLE)
                    .SetFontColor(TextBlack)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetMarginBottom(1));

                doc.Add(new Paragraph($"Số: {orderId}MVD")
                    .SetFont(font).SetFontSize(FS_SMALL)
                    .SetFontColor(TextBlack)
                    .SetTextAlignment(TextAlignment.RIGHT)
                    .SetMarginBottom(6));

                // ── 2. SECTION I: Thông tin chung ─────────────────────────
                doc.Add(new Paragraph("I. Thông tin chung")
                    .SetFont(boldFont).SetFontSize(FS_SECTION)
                    .SetFontColor(TextBlack)
                    .SetMarginBottom(2));

                var infoTbl = new Table(UnitValue.CreatePercentArray(new float[] { 28, 72 }))
                    .UseAllAvailableWidth().SetMarginBottom(8);

                InfoRow(infoTbl, "Tên khách hàng",     customerName,    font, boldFont);
                InfoRow(infoTbl, "Địa chỉ",            customerAddress, font, boldFont);
                InfoRow(infoTbl, "Địa điểm quan trắc", "Trụ sở chính", font, boldFont);
                InfoRow(infoTbl, "Loại mẫu",
                    string.IsNullOrEmpty(sampleType) ? "Không khí xung quanh" : sampleType,
                    font, boldFont);
                InfoRow(infoTbl, "Vị trí quan trắc",   "", font, boldFont);
                InfoRow(infoTbl, "Ngày quan trắc",
                    sampleDate.HasValue   ? sampleDate.Value.ToString("dd/MM/yyyy")   : "", font, boldFont);
                InfoRow(infoTbl, "Ngày phân tích",
                    analysisDate.HasValue ? analysisDate.Value.ToString("dd/MM/yyyy") : "", font, boldFont);
                InfoRow(infoTbl, "Ngày trả kết quả",
                    returnDate.HasValue   ? returnDate.Value.ToString("dd/MM/yyyy")   : "", font, boldFont);

                doc.Add(infoTbl);

                // ── 3. SECTION II: Kết quả ────────────────────────────────
                doc.Add(new Paragraph("II. Kết quả")
                    .SetFont(boldFont).SetFontSize(FS_SECTION)
                    .SetFontColor(TextBlack)
                    .SetMarginBottom(2));

                int areaCols  = Math.Max(areaNames.Count, 1);
                int totalCols = 4 + areaCols + 1;   // STT|TênTS|ĐV|PP|[area…]|QCVN

                float[] cw = new float[totalCols];
                cw[0] = 6f;    // STT
                cw[1] = 20f;   // Thông số
                cw[2] = 10f;   // Đơn vị
                cw[3] = 24f;   // PP phân tích
                for (int i = 0; i < areaCols; i++)
                    cw[4 + i] = 22f / areaCols;     // Kết quả (chia đều)
                cw[totalCols - 1] = 18f;             // QCVN

                var resTbl = new Table(UnitValue.CreatePercentArray(cw))
                    .UseAllAvailableWidth()
                    .SetMarginBottom(10)
                    .SetBorder(new SolidBorder(BorderGray, 0.5f));

                // Header hàng 1
                resTbl.AddHeaderCell(HdrCell("STT",      boldFont, rowSpan: 2));
                resTbl.AddHeaderCell(HdrCell("Thông số", boldFont, rowSpan: 2));
                resTbl.AddHeaderCell(HdrCell("Đơn vị",   boldFont, rowSpan: 2));
                resTbl.AddHeaderCell(HdrCell("Phương pháp phân tích", boldFont, rowSpan: 2));
                resTbl.AddHeaderCell(HdrCell("Kết quả",  boldFont, rowSpan: 1, colSpan: areaCols));
                resTbl.AddHeaderCell(HdrCell("QCVN",     boldFont, rowSpan: 2));

                // Header hàng 2 — tên khu vực
                if (areaNames.Count > 0)
                    foreach (var an in areaNames)
                        resTbl.AddHeaderCell(HdrCell(an, font, rowSpan: 1));
                else
                    resTbl.AddHeaderCell(HdrCell("Khuvuc1", font, rowSpan: 1));

                // Data rows
                if (resultRows.Count > 0)
                {
                    foreach (var row in resultRows)
                    {
                        resTbl.AddCell(DCell(row.STT.ToString(), font, TextAlignment.CENTER));
                        resTbl.AddCell(DCell(row.ParamName,      font, TextAlignment.LEFT));
                        resTbl.AddCell(DCell(row.Unit,           font, TextAlignment.CENTER));
                        resTbl.AddCell(DCell(row.Method,         font, TextAlignment.CENTER));

                        foreach (var an in areaNames)
                        {
                            string val = row.AreaResults.ContainsKey(an) ? row.AreaResults[an] : "";
                            resTbl.AddCell(DCell(val, font, TextAlignment.CENTER));
                        }
                        if (areaNames.Count == 0)
                            resTbl.AddCell(DCell("", font, TextAlignment.CENTER));

                        resTbl.AddCell(DCell(row.QcvnLimit, font, TextAlignment.CENTER));
                    }
                }
                else
                {
                    // 9 hàng trống (preview / mẫu)
                    for (int i = 1; i <= 9; i++)
                    {
                        resTbl.AddCell(DCell(i.ToString(), font, TextAlignment.CENTER, minH: 20));
                        resTbl.AddCell(DCell("", font, TextAlignment.LEFT,   minH: 20));
                        resTbl.AddCell(DCell("", font, TextAlignment.CENTER, minH: 20));
                        resTbl.AddCell(DCell("", font, TextAlignment.CENTER, minH: 20));
                        for (int z = 0; z < areaCols; z++)
                            resTbl.AddCell(DCell("", font, TextAlignment.CENTER, minH: 20));
                        resTbl.AddCell(DCell("", font, TextAlignment.CENTER, minH: 20));
                    }
                }

                doc.Add(resTbl);

                // ── 4. KÝ TÊN — 2 cột: Khách hàng | Đại diện PTN ───────
                var signDate = DateTime.Now.ToString("dd/MM/yyyy");
                doc.Add(new Paragraph($"TP. Hồ Chí Minh, ngày {DateTime.Now.Day} tháng {DateTime.Now.Month} năm {DateTime.Now.Year}")
                    .SetFont(font).SetFontSize(FS_BODY)
                    .SetFontColor(TextBlack)
                    .SetTextAlignment(TextAlignment.RIGHT)
                    .SetMarginBottom(4).SetMarginTop(6));

                var signTbl = new Table(UnitValue.CreatePercentArray(new float[] { 50, 50 }))
                    .UseAllAvailableWidth()
                    .SetMarginBottom(10);

                // Cột trái — Khách hàng
                var cellLeft = new Cell()
                    .SetBorder(Border.NO_BORDER)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetPaddingTop(2);
                cellLeft.Add(new Paragraph("ĐẠI DIỆN KHÁCH HÀNG")
                    .SetFont(boldFont).SetFontSize(FS_BODY)
                    .SetFontColor(TextBlack));
                cellLeft.Add(new Paragraph("(Ký, ghi rõ họ tên)")
                    .SetFont(font).SetFontSize(FS_SMALL)
                    .SetFontColor(new DeviceRgb(120, 120, 120)));
                cellLeft.Add(new Paragraph("\n\n\n")
                    .SetFont(font).SetFontSize(FS_BODY));
                cellLeft.Add(new Paragraph(".................................")
                    .SetFont(font).SetFontSize(FS_BODY)
                    .SetFontColor(TextBlack));
                signTbl.AddCell(cellLeft);

                // Cột phải — Đại diện PTN
                var cellRight = new Cell()
                    .SetBorder(Border.NO_BORDER)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetPaddingTop(2);
                cellRight.Add(new Paragraph("ĐẠI DIỆN PHÒNG THÍ NGHIỆM")
                    .SetFont(boldFont).SetFontSize(FS_BODY)
                    .SetFontColor(TextBlack));
                cellRight.Add(new Paragraph("(Ký, ghi rõ họ tên)")
                    .SetFont(font).SetFontSize(FS_SMALL)
                    .SetFontColor(new DeviceRgb(120, 120, 120)));
                cellRight.Add(new Paragraph("\n\n\n")
                    .SetFont(font).SetFontSize(FS_BODY));
                cellRight.Add(new Paragraph(".................................")
                    .SetFont(font).SetFontSize(FS_BODY)
                    .SetFontColor(TextBlack));
                signTbl.AddCell(cellRight);

                doc.Add(signTbl);

                // ── 5. WATERMARK: Icon.png ────────────────────────────────
                AddImageWatermark(pdf);

                // ── 6. FOOTER ─────────────────────────────────────────────
                AddFooter(pdf, font, boldFont);

                doc.Close();
            }

            return filePath;
        }

        // ══════════════════════════════════════════════════════════════════════
        // WATERMARK — dùng assets/images/Icon.png
        // ══════════════════════════════════════════════════════════════════════
        private static void AddImageWatermark(PdfDocument pdf)
        {
            // Tìm file Icon.png theo thứ tự ưu tiên
            string basedir = AppDomain.CurrentDomain.BaseDirectory;
            string[] candidates = new[]
            {
                System.IO.Path.Combine(basedir, "assets", "images", "Icon.png"),
                System.IO.Path.Combine(basedir, "..", "..", "..", "..", "assets", "images", "Icon.png"),
                System.IO.Path.Combine(basedir, "..", "..", "..", "assets", "images", "Icon.png"),
            };

            string? iconPath = null;
            foreach (var c in candidates)
            {
                string full = System.IO.Path.GetFullPath(c);
                if (File.Exists(full)) { iconPath = full; break; }
            }

            if (iconPath == null) return;

            ImageData imgData;
            try { imgData = ImageDataFactory.Create(iconPath); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PDF] Watermark load error '{iconPath}': {ex.Message}"); return; }

            int pages = pdf.GetNumberOfPages();
            for (int p = 1; p <= pages; p++)
            {
                PdfPage   page  = pdf.GetPage(p);
                Rectangle pgSz  = page.GetPageSize();

                float wm = 220f;
                float hm = 220f;
                float x  = (pgSz.GetWidth()  - wm) / 2f;
                float y  = (pgSz.GetHeight() - hm) / 2f;

                // Chỉ vẽ 1 lần duy nhất — dùng Canvas.Image để SetOpacity hoạt động đúng
                PdfCanvas pdfCanvas = new PdfCanvas(page);
                Canvas c = new Canvas(pdfCanvas, pgSz);
                var img = new iText.Layout.Element.Image(imgData)
                    .SetFixedPosition(x, y)
                    .SetWidth(wm)
                    .SetHeight(hm)
                    .SetOpacity(0.15f);
                c.Add(img);
                c.Close();
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // FOOTER
        // ══════════════════════════════════════════════════════════════════════
        private static void AddFooter(PdfDocument pdf, PdfFont font, PdfFont boldFont)
        {
            // Tìm Icon.png cho footer logo
            string basedir = AppDomain.CurrentDomain.BaseDirectory;
            string[] candidates = new[]
            {
                System.IO.Path.Combine(basedir, "assets", "images", "Icon.png"),
                System.IO.Path.Combine(basedir, "..", "..", "..", "..", "assets", "images", "Icon.png"),
                System.IO.Path.Combine(basedir, "..", "..", "..", "assets", "images", "Icon.png"),
            };
            ImageData? footerLogo = null;
            foreach (var cp in candidates)
            {
                string full = System.IO.Path.GetFullPath(cp);
                if (File.Exists(full))
                {
                    try { footerLogo = ImageDataFactory.Create(full); break; }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PDF] Footer logo load error '{full}': {ex.Message}"); }
                }
            }

            int pages = pdf.GetNumberOfPages();
            for (int p = 1; p <= pages; p++)
            {
                PdfPage   page  = pdf.GetPage(p);
                Rectangle pgSz  = page.GetPageSize();
                PdfCanvas pdfC  = new PdfCanvas(page);

                // Đường kẻ trên footer
                pdfC.SetStrokeColor(new DeviceRgb(180, 180, 180));
                pdfC.SetLineWidth(0.5f);
                pdfC.MoveTo(36, 34).LineTo(pgSz.GetWidth() - 36, 34).Stroke();

                Canvas c = new Canvas(pdfC, pgSz);

                // Logo nhỏ 18×18pt bên trái, sau đó chữ "Ecova"
                if (footerLogo != null)
                {
                    var logoImg = new iText.Layout.Element.Image(footerLogo)
                        .SetFixedPosition(p, 36, 12)
                        .SetWidth(18)
                        .SetHeight(18);
                    c.Add(logoImg);
                }

                // Chữ "Ecova" cách logo ~21pt
                c.Add(new Paragraph("Ecova")
                    .SetFont(boldFont).SetFontSize(10f)
                    .SetFontColor(new DeviceRgb(49, 87, 44))
                    .SetFixedPosition(p, 58, 15, 60));

                c.Close();
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // CELL HELPERS
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>Header cell — border đầy đủ, căn giữa, có thể span rows/cols.</summary>
        private static Cell HdrCell(string text, PdfFont font,
            int rowSpan = 1, int colSpan = 1)
        {
            return new Cell(rowSpan, colSpan)
                .Add(SmartParagraph(text, font, FS_BODY))
                .SetTextAlignment(TextAlignment.CENTER)
                .SetVerticalAlignment(VerticalAlignment.MIDDLE)
                .SetPaddingTop(3).SetPaddingBottom(3)
                .SetPaddingLeft(2).SetPaddingRight(2)
                .SetBorder(new SolidBorder(BorderGray, 0.5f));
        }

        /// <summary>Data cell — border nhẹ, tuỳ chọn chiều cao tối thiểu.</summary>
        private static Cell DCell(string text, PdfFont font,
            TextAlignment align = TextAlignment.CENTER, float minH = 0)
        {
            var cell = new Cell()
                .Add(SmartParagraph(text ?? "", font, FS_BODY))
                .SetTextAlignment(align)
                .SetVerticalAlignment(VerticalAlignment.MIDDLE)
                .SetPaddingTop(2).SetPaddingBottom(2)
                .SetPaddingLeft(align == TextAlignment.LEFT ? 5 : 2)
                .SetPaddingRight(2)
                .SetBorder(new SolidBorder(BorderGray, 0.5f));
            if (minH > 0) cell.SetMinHeight(minH);
            return cell;
        }

        /// <summary>Cell không có border (dùng cho contact table).</summary>
        private static Cell NoBorderCell(string text, PdfFont font, float size, TextAlignment align)
        {
            return new Cell()
                .Add(new Paragraph(text ?? "").SetFont(font).SetFontSize(size))
                .SetTextAlignment(align)
                .SetVerticalAlignment(VerticalAlignment.MIDDLE)
                .SetPadding(1)
                .SetBorder(Border.NO_BORDER);
        }

        /// <summary>Thêm hàng 2-cột vào bảng Thông tin chung.</summary>
        private static void InfoRow(Table table, string label, string value,
            PdfFont font, PdfFont boldFont)
        {
            table.AddCell(new Cell()
                .Add(new Paragraph(label).SetFont(font).SetFontSize(FS_BODY))
                .SetPaddingTop(3).SetPaddingBottom(3).SetPaddingLeft(5).SetPaddingRight(3)
                .SetBorder(new SolidBorder(BorderGray, 0.5f)));

            table.AddCell(new Cell()
                .Add(new Paragraph(value ?? "").SetFont(font).SetFontSize(FS_BODY))
                .SetPaddingTop(3).SetPaddingBottom(3).SetPaddingLeft(5).SetPaddingRight(3)
                .SetBorder(new SolidBorder(BorderGray, 0.5f)));
        }

        // ══════════════════════════════════════════════════════════════════════
        // SUBSCRIPT / SUPERSCRIPT — render chỉ số trên/dưới đúng cách
        // ══════════════════════════════════════════════════════════════════════

        // Unicode subscript → digit thường
        private static readonly Dictionary<char, char> SubscriptMap = new()
        {
            {'₀','0'}, {'₁','1'}, {'₂','2'}, {'₃','3'}, {'₄','4'},
            {'₅','5'}, {'₆','6'}, {'₇','7'}, {'₈','8'}, {'₉','9'},
            {'₊','+'}, {'₋','-'}, {'₌','='}, {'₍','('}, {'₎',')'}
        };

        // Unicode superscript → digit thường
        private static readonly Dictionary<char, char> SuperscriptMap = new()
        {
            {'⁰','0'}, {'¹','1'}, {'²','2'}, {'³','3'}, {'⁴','4'},
            {'⁵','5'}, {'⁶','6'}, {'⁷','7'}, {'⁸','8'}, {'⁹','9'},
            {'⁺','+'}, {'⁻','-'}, {'⁼','='}, {'⁽','('}, {'⁾',')'}
        };

        /// <summary>
        /// Tạo Paragraph với chỉ số trên/dưới render đúng bằng SetTextRise().
        /// Ví dụ: "SO₂" → "SO" (normal) + "2" (subscript, nhỏ hơn, hạ xuống).
        /// </summary>
        private static Paragraph SmartParagraph(string text, PdfFont font, float fontSize)
        {
            if (string.IsNullOrEmpty(text))
                return new Paragraph("").SetFont(font).SetFontSize(fontSize);

            // Kiểm tra nhanh: nếu không có ký tự sub/sup → trả paragraph thường
            bool hasSub = text.Any(c => SubscriptMap.ContainsKey(c));
            bool hasSup = text.Any(c => SuperscriptMap.ContainsKey(c));
            if (!hasSub && !hasSup)
                return new Paragraph(text).SetFont(font).SetFontSize(fontSize);

            var para = new Paragraph().SetFont(font).SetFontSize(fontSize);
            float subSize = fontSize * 0.7f;   // Chỉ số nhỏ hơn 70%
            float subRise = -fontSize * 0.2f;   // Hạ xuống 20%
            float supRise = fontSize * 0.35f;   // Nâng lên 35%

            // Duyệt từng ký tự, gom nhóm normal/sub/sup
            var buffer = new System.Text.StringBuilder();
            int mode = 0; // 0=normal, 1=sub, 2=sup

            void Flush()
            {
                if (buffer.Length == 0) return;
                var t = new Text(buffer.ToString()).SetFont(font);
                switch (mode)
                {
                    case 1: t.SetFontSize(subSize).SetTextRise(subRise); break;
                    case 2: t.SetFontSize(subSize).SetTextRise(supRise); break;
                    default: t.SetFontSize(fontSize); break;
                }
                para.Add(t);
                buffer.Clear();
            }

            foreach (char ch in text)
            {
                if (SubscriptMap.TryGetValue(ch, out char subCh))
                {
                    if (mode != 1) { Flush(); mode = 1; }
                    buffer.Append(subCh);
                }
                else if (SuperscriptMap.TryGetValue(ch, out char supCh))
                {
                    if (mode != 2) { Flush(); mode = 2; }
                    buffer.Append(supCh);
                }
                else
                {
                    if (mode != 0) { Flush(); mode = 0; }
                    buffer.Append(ch);
                }
            }
            Flush();

            return para;
        }

        // ══════════════════════════════════════════════════════════════════════
        // FONT — hỗ trợ tiếng Việt (Arial TTF ưu tiên)
        // ══════════════════════════════════════════════════════════════════════
        private static PdfFont SafeCreateFont(bool bold)
        {
            string[] names = bold
                ? new[] { "arialbd.ttf", "Arial Bold.ttf", "segoeuib.ttf", "calibrib.ttf" }
                : new[] { "arial.ttf",   "Arial.ttf",      "segoeui.ttf",  "calibri.ttf"  };

            string winFonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            foreach (var fn in names)
            {
                string fp = System.IO.Path.Combine(winFonts, fn);
                if (File.Exists(fp))
                {
                    try
                    {
                        return PdfFontFactory.CreateFont(fp,
                            PdfEncodings.IDENTITY_H,
                            PdfFontFactory.EmbeddingStrategy.PREFER_EMBEDDED);
                    }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PDF] Font load error '{fp}': {ex.Message}"); }
                }
            }
            try { return PdfFontFactory.CreateFont(bold ? StandardFonts.HELVETICA_BOLD : StandardFonts.HELVETICA); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PDF] Helvetica fallback error: {ex.Message}"); return PdfFontFactory.CreateFont(); }
        }
    }
}
