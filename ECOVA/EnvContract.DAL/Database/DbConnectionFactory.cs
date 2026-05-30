using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;
using System.IO;

namespace EnvContract.DAL.Database
{
    public static class DbConnectionFactory
    {
        private static readonly string _connectionString;

        static DbConnectionFactory()
        {
            try
            {
                var config = new ConfigurationBuilder()
                    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                    .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false)
                    .Build();

                _connectionString = config.GetConnectionString("Default")
                    ?? @"Server=.\SQLEXPRESS;Database=ECOVA;Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=5;";
            }
            catch
            {
                // Fallback nếu không đọc được file config
                _connectionString = @"Server=.\SQLEXPRESS;Database=ECOVA;Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=5;";
            }
        }

        public static IDbConnection CreateConnection()
        {
            return new SqlConnection(_connectionString);
        }
    }
}
