using EnvContract.Common.Helpers;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EnvContract.BLL.Interfaces
{
    public interface IExportService
    {
        Task<string> ExportOrderResultToPdfAsync(string orderId, string customerName, string resultDataText, string outputPath);

        /// <summary>
        /// Xuất PDF có cấu trúc bảng đầy đủ theo mẫu ECOVA:
        /// Section I: Thông tin chung, Section II: Bảng kết quả theo khu vực, Watermark, Chữ ký.
        /// </summary>
        Task<string> ExportStructuredPdfAsync(
            string orderId,
            string customerName,
            string customerAddress,
            string sampleType,
            List<string> areaNames,
            List<PdfTestResultRow> resultRows,
            string outputDirectory,
            DateTime? sampleDate = null,
            DateTime? analysisDate = null,
            DateTime? returnDate = null);
    }
}
