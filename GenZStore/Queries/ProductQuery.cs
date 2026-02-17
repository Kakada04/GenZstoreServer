using Dapper;
using GenZStore.Data;
using GenZStore.DTOs;
using System.Data;

namespace GenZStore.Queries
{
    // DTO for returning product data
    public class ProductResultDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Usage { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public Guid CategoryId { get; set; }
        public string ImageUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    // Interface for product queries
    public interface IProductQuery
    {
        Task<PagedResult<ProductResultDto>> GetAllAsync(int page, int pageSize);
        Task<ProductResultDto?> GetByIdAsync(Guid productId);
        Task<IEnumerable<ProductResultDto>> SearchByNameAsync(string name);
    }

    // Implementation using Dapper
    public class ProductQuery : IProductQuery
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public ProductQuery(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<PagedResult<ProductResultDto>> GetAllAsync(int page, int pageSize)
        {
            using var conn = _connectionFactory.CreateConnection();

            // For MySQL/MariaDB use LIMIT/OFFSET. Run two separate queries (items + count)
            string sqlItems = @"
                SELECT `Id`, `Name`, `Description`, `Usage`, `Price`, `Quantity`,
                       `CategoryId`, `ImageUrl`, `CreatedAt`, `UpdatedAt`
                FROM `Products`
                ORDER BY `CreatedAt` DESC
                LIMIT @PageSize OFFSET @Offset;";

            string sqlCount = "SELECT COUNT(*) FROM `Products`;";

            var parameters = new
            {
                Offset = (page - 1) * pageSize,
                PageSize = pageSize
            };

            var items = await conn.QueryAsync<ProductResultDto>(sqlItems, parameters);
            var totalCount = await conn.QuerySingleAsync<int>(sqlCount);

            return new PagedResult<ProductResultDto>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<ProductResultDto?> GetByIdAsync(Guid productId)
        {
            using var conn = _connectionFactory.CreateConnection();
            string sql = @"SELECT `Id`, `Name`, `Description`, `Usage`, `Price`, `Quantity`,
                                  `CategoryId`, `ImageUrl`, `CreatedAt`, `UpdatedAt`
                           FROM `Products`
                           WHERE `Id` = @Id";
            return await conn.QueryFirstOrDefaultAsync<ProductResultDto>(sql, new { Id = productId });
        }

        public async Task<IEnumerable<ProductResultDto>> SearchByNameAsync(string name)
        {
            using var conn = _connectionFactory.CreateConnection();
            string sql = @"SELECT `Id`, `Name`, `Description`, `Usage`, `Price`, `Quantity`,
                                  `CategoryId`, `ImageUrl`, `CreatedAt`, `UpdatedAt`
                           FROM `Products`
                           WHERE `Name` LIKE @Name";
            return await conn.QueryAsync<ProductResultDto>(sql, new { Name = $"%{name}%" });
        }
    }
}