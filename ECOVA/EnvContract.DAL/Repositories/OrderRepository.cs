using EnvContract.DAL.Database;
using EnvContract.DAL.Interfaces;
using EnvContract.DTO.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EnvContract.DAL.Repositories
{
    public class OrderRepository : IOrderRepository
    {
        public async Task AddOrderAsync(OrderDTO order)
        {
            await SqlHelper.ExecuteSpAsync("sp_AddOrder", new
            {
                order.OrderID, order.ContractID, order.OrderName, order.OrderDate,
                order.Deadline, order.FinalReportPath, order.IsApproved, order.Status,
                order.EnvironmentType
            });
        }

        public async Task DeleteOrderAsync(string orderId)
        {
            await SqlHelper.ExecuteSpAsync("sp_DeleteOrder", new { OrderID = orderId });
        }

        public async Task<List<OrderDTO>> GetAllOrdersAsync()
        {
            var result = await SqlHelper.QuerySpAsync<OrderDTO>("sp_GetAllOrders");
            return result.ToList();
        }

        public async Task<OrderDTO?> GetByIdAsync(string orderId)
        {
            return await SqlHelper.QuerySingleOrDefaultSpAsync<OrderDTO>(
                "sp_GetOrderById", new { OrderID = orderId });
        }

        public async Task<List<OrderDTO>> GetOrdersByContractIdAsync(string contractId)
        {
            var result = await SqlHelper.QuerySpAsync<OrderDTO>(
                "sp_GetOrdersByContract", new { ContractID = contractId });
            return result.ToList();
        }

        public async Task UpdateOrderAsync(OrderDTO order)
        {
            await SqlHelper.ExecuteSpAsync("sp_UpdateOrder", new
            {
                order.OrderID, order.ContractID, order.OrderName, order.OrderDate,
                order.Deadline, order.FinalReportPath, order.IsApproved, order.Status,
                order.EnvironmentType
            });
        }
    }
}
