using EnvContract.BLL.Interfaces;
using EnvContract.DAL.Database;
using EnvContract.DAL.Interfaces;
using Serilog;
using System;
using System.Threading.Tasks;

namespace EnvContract.BLL.Services
{
    /// <summary>
    /// Service xử lý quy trình phê duyệt kết quả quan trắc bởi Giám đốc/QA.
    /// 
    /// Quy trình:
    ///   Phòng Kết quả nhập đầy đủ → Giám đốc xem xét → Duyệt (IsApproved=1) → Xuất PDF
    ///   Nếu từ chối → Phòng liên quan chỉnh sửa lại → Submit lại
    /// </summary>
    public class ApprovalService : IApprovalService
    {
        private readonly IOrderRepository _orderRepository;

        public ApprovalService(IOrderRepository orderRepository)
        {
            _orderRepository = orderRepository;
        }

        /// <summary>
        /// Phê duyệt đơn hàng: cập nhật IsApproved=1 và ghi nhận người duyệt.
        /// </summary>
        public async Task ApproveOrderAsync(string orderId, string approvedByUserId)
        {
            try
            {
                var order = await _orderRepository.GetByIdAsync(orderId);
                if (order == null)
                {
                    Log.Warning("[Approval] Không tìm thấy đơn hàng {OrderId} để phê duyệt.", orderId);
                    return;
                }

                order.IsApproved = 1;
                await _orderRepository.UpdateOrderAsync(order);

                Log.Information("[Approval] Đơn hàng {OrderId} đã được phê duyệt bởi UserID={UserId}.",
                    orderId, approvedByUserId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[Approval] Lỗi khi phê duyệt đơn hàng {OrderId}.", orderId);
                throw;
            }
        }

        /// <summary>
        /// Từ chối phê duyệt: giữ IsApproved=0, ghi nhận lý do từ chối.
        /// </summary>
        public async Task RejectOrderAsync(string orderId, string rejectedByUserId, string reason)
        {
            try
            {
                var order = await _orderRepository.GetByIdAsync(orderId);
                if (order == null)
                {
                    Log.Warning("[Approval] Không tìm thấy đơn hàng {OrderId} để từ chối.", orderId);
                    return;
                }

                // IsApproved giữ 0, log lý do từ chối
                Log.Warning("[Approval] Đơn hàng {OrderId} bị từ chối bởi UserID={UserId}. Lý do: {Reason}",
                    orderId, rejectedByUserId, reason);

                // Cập nhật lại nếu cần (ví dụ: reset về trạng thái pending)
                await _orderRepository.UpdateOrderAsync(order);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[Approval] Lỗi khi từ chối đơn hàng {OrderId}.", orderId);
                throw;
            }
        }

        /// <summary>
        /// Kiểm tra đơn hàng đã được phê duyệt chưa.
        /// </summary>
        public async Task<bool> IsApprovedAsync(string orderId)
        {
            var order = await _orderRepository.GetByIdAsync(orderId);
            return order?.IsApproved == 1;
        }
    }
}
