using System;

namespace EnvContract.Common.Exceptions
{
    /// <summary>
    /// Exception cơ sở cho tất cả lỗi nghiệp vụ trong hệ thống ECOVA.
    /// Dùng để phân biệt lỗi business logic với lỗi kỹ thuật (NullRef, IO, SQL...).
    /// </summary>
    public class BusinessException : Exception
    {
        public string? ErrorCode { get; }

        public BusinessException(string message)
            : base(message) { }

        public BusinessException(string message, string errorCode)
            : base(message) => ErrorCode = errorCode;

        public BusinessException(string message, Exception innerException)
            : base(message, innerException) { }
    }

    /// <summary>
    /// Lỗi validation dữ liệu đầu vào (VD: thiếu trường bắt buộc, giá trị ngoài phạm vi).
    /// Nên bắt ở GUI layer để hiển thị thông báo thân thiện.
    /// </summary>
    public class ValidationException : BusinessException
    {
        public string? FieldName { get; }

        public ValidationException(string message)
            : base(message) { }

        public ValidationException(string fieldName, string message)
            : base(message) => FieldName = fieldName;
    }

    /// <summary>
    /// Lỗi khi không tìm thấy entity trong DB (VD: ContractID không tồn tại).
    /// </summary>
    public class NotFoundException : BusinessException
    {
        public NotFoundException(string entityName, string id)
            : base($"{entityName} với ID '{id}' không tồn tại.") { }
    }
}
