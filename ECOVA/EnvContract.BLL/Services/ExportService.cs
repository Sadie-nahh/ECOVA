using EnvContract.BLL.Interfaces;
using EnvContract.Common.Helpers;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EnvContract.BLL.Services
{
    public class ExportService : IExportService
    {
        /// <summary>
        /// Backward-compatible: Xuất PDF text-only (không có bảng cấu trúc).
        /// </summary>
        public async Task<string> ExportOrderResultToPdfAsync(string orderId, string customerName, string resultDataText, string outputPath)
        {
            return await Task.Run(() =>
                PdfExportHelper.GenerateTestResultPdf(orderId, customerName, resultDataText, outputPath));
        }

        /// <summary>
        /// Xuất PDF có cấu trúc bảng đầy đủ: Thông tin chung + Bảng kết quả + Watermark + Chữ ký.
        /// Gọi PdfExportHelper.GenerateStructuredPdf() với dữ liệu thực từ UI.
        /// </summary>
        public async Task<string> ExportStructuredPdfAsync(
            string orderId,
            string customerName,
            string customerAddress,
            string sampleType,
            List<string> areaNames,
            List<PdfTestResultRow> resultRows,
            string outputDirectory,
            DateTime? sampleDate = null,
            DateTime? analysisDate = null,
            DateTime? returnDate = null)
        {
            return await Task.Run(() =>
                PdfExportHelper.GenerateStructuredPdf(
                    orderId,
                    customerName,
                    customerAddress,
                    sampleType,
                    areaNames,
                    resultRows,
                    outputDirectory,
                    sampleDate,
                    analysisDate,
                    returnDate));
        }
    }
}
