using System.Threading.Tasks;

namespace EnvContract.BLL.Interfaces
{
    /// <summary>
    /// Interface cho service xử lý phê duyệt kết quả quan trắc bởi Giám đốc.
    /// Sau khi duyệt: Orders.IsApproved = 1 → unlock xuất PDF phiếu kết quả.
    /// </summary>
    public interface IApprovalService
    {
        /// <summary>
        /// Phê duyệt một đơn hàng. Ghi nhận thời gian và người duyệt.
        /// </summary>
        Task ApproveOrderAsync(string orderId, string approvedByUserId);

        /// <summary>
        /// Từ chối phê duyệt đơn hàng và ghi lý do từ chối.
        /// </summary>
        Task RejectOrderAsync(string orderId, string rejectedByUserId, string reason);

        /// <summary>
        /// Kiểm tra trạng thái phê duyệt của một đơn hàng.
        /// Trả về true nếu IsApproved = 1.
        /// </summary>
        Task<bool> IsApprovedAsync(string orderId);
    }
}
