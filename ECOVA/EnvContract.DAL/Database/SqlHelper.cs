using Dapper;
using System.Collections.Generic;
using System.Data;

namespace EnvContract.DAL.Database
{
    /// <summary>
    /// Helper wrapper cho Dapper — các method sẽ được mở rộng khi implement từng Module.
    /// ConfigureAwait(false) is used on all async calls to prevent WinForms SynchronizationContext deadlocks.
    /// </summary>
    public static class SqlHelper
    {
        public static void ExecuteNonQuery(string sql, object? parameters = null)
        {
            using var connection = DbConnectionFactory.CreateConnection();
            connection.Execute(sql, parameters);
        }

        public static async Task ExecuteNonQueryAsync(string sql, object? parameters = null)
        {
            using var connection = DbConnectionFactory.CreateConnection();
            await connection.ExecuteAsync(sql, parameters).ConfigureAwait(false);
        }

        public static IEnumerable<T> Query<T>(string sql, object? parameters = null)
        {
            using var connection = DbConnectionFactory.CreateConnection();
            return connection.Query<T>(sql, parameters);
        }

        public static async Task<IEnumerable<T>> QueryAsync<T>(string sql, object? parameters = null)
        {
            using var connection = DbConnectionFactory.CreateConnection();
            return await connection.QueryAsync<T>(sql, parameters).ConfigureAwait(false);
        }

        public static T? QuerySingleOrDefault<T>(string sql, object? parameters = null)
        {
            using var connection = DbConnectionFactory.CreateConnection();
            return connection.QuerySingleOrDefault<T>(sql, parameters);
        }

        public static async Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? parameters = null)
        {
            using var connection = DbConnectionFactory.CreateConnection();
            return await connection.QuerySingleOrDefaultAsync<T>(sql, parameters).ConfigureAwait(false);
        }

        /// <summary>
        /// Mở connection + transaction cho multi-step operations.
        /// Caller có trách nhiệm Commit/Rollback và Dispose connection + transaction.
        /// </summary>
        public static (IDbConnection Connection, IDbTransaction Transaction) BeginTransaction()
        {
            var connection = DbConnectionFactory.CreateConnection();
            connection.Open();
            var transaction = connection.BeginTransaction();
            return (connection, transaction);
        }

        /// <summary>
        /// Execute SQL trong một transaction đã mở sẵn.
        /// </summary>
        public static async Task ExecuteInTransactionAsync(
            IDbConnection conn, IDbTransaction tx,
            string sql, object? parameters = null)
        {
            await conn.ExecuteAsync(sql, parameters, tx).ConfigureAwait(false);
        }

        // ── Stored Procedure helpers ────────────────────────────────────────

        /// <summary>
        /// Thực thi Stored Procedure không trả về dữ liệu.
        /// </summary>
        public static async Task ExecuteSpAsync(string spName, object? parameters = null)
        {
            using var connection = DbConnectionFactory.CreateConnection();
            await connection.ExecuteAsync(spName, parameters,
                commandType: CommandType.StoredProcedure).ConfigureAwait(false);
        }

        /// <summary>
        /// Thực thi Stored Procedure trả về danh sách.
        /// </summary>
        public static async Task<IEnumerable<T>> QuerySpAsync<T>(
            string spName, object? parameters = null)
        {
            using var connection = DbConnectionFactory.CreateConnection();
            return await connection.QueryAsync<T>(spName, parameters,
                commandType: CommandType.StoredProcedure).ConfigureAwait(false);
        }

        /// <summary>
        /// Thực thi Stored Procedure trả về 1 bản ghi (hoặc null).
        /// </summary>
        public static async Task<T?> QuerySingleOrDefaultSpAsync<T>(
            string spName, object? parameters = null)
        {
            using var connection = DbConnectionFactory.CreateConnection();
            return await connection.QuerySingleOrDefaultAsync<T>(spName, parameters,
                commandType: CommandType.StoredProcedure).ConfigureAwait(false);
        }

        /// <summary>
        /// Thực thi Stored Procedure với OUTPUT parameters (DynamicParameters).
        /// Caller tự đọc output qua parameters.Get<T>("@ParamName").
        /// </summary>
        public static async Task ExecuteSpWithOutputAsync(
            string spName, DynamicParameters parameters)
        {
            using var connection = DbConnectionFactory.CreateConnection();
            await connection.ExecuteAsync(spName, parameters,
                commandType: CommandType.StoredProcedure).ConfigureAwait(false);
        }

        /// <summary>
        /// Thực thi Stored Procedure đồng bộ không trả về dữ liệu.
        /// Dùng trong code khởi động (không async context).
        /// </summary>
        public static void ExecuteSpSync(string spName, object? parameters = null)
        {
            using var connection = DbConnectionFactory.CreateConnection();
            connection.Execute(spName, parameters, commandType: CommandType.StoredProcedure);
        }

        /// <summary>
        /// Thực thi Stored Procedure đồng bộ trả về 1 bản ghi (hoặc null).
        /// Dùng trong code khởi động (không async context).
        /// </summary>
        public static T? QuerySingleOrDefaultSp<T>(string spName, object? parameters = null)
        {
            using var connection = DbConnectionFactory.CreateConnection();
            return connection.QuerySingleOrDefault<T>(spName, parameters,
                commandType: CommandType.StoredProcedure);
        }
    }
}

