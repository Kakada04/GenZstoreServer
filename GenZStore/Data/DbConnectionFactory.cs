using System.Data;
using MySqlConnector;
using Microsoft.Extensions.Configuration;

namespace GenZStore.Data
{
    public interface IDbConnectionFactory
    {
        IDbConnection CreateConnection();
    }

    public class DbConnectionFactory : IDbConnectionFactory
    {
        private readonly IConfiguration _configuration;

        public DbConnectionFactory(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public IDbConnection CreateConnection()
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            return new MySqlConnection(connectionString);
        }
    }
}