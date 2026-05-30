using EnvContract.DTO.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EnvContract.DAL.Interfaces
{
    public interface IOrderRepository
    {
        Task AddOrderAsync(OrderDTO order);
        Task UpdateOrderAsync(OrderDTO order);
        Task DeleteOrderAsync(string orderId);
        Task<OrderDTO?> GetByIdAsync(string orderId);
        Task<List<OrderDTO>> GetAllOrdersAsync();
        Task<List<OrderDTO>> GetOrdersByContractIdAsync(string contractId);
    }
}
