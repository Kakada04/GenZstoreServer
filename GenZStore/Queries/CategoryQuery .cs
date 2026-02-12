using Dapper;
using GenZStore.Models;
using GenZStore.Data;
using GenZStore.DTOs;

namespace GenZStore.Queries
{
    public class CategoryResultDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public interface ICategoryQuery
    {
        // ? UPDATE: Add 'searchTerm' parameter
        Task<PagedResult<CategoryResultDto>> GetAllAsync(int page, int pageSize, string? searchTerm = null);

        Task<CategoryResultDto?> GetByIdAsync(Guid categoryId);
    }

    public class CategoryQuery : ICategoryQuery
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public CategoryQuery(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<PagedResult<CategoryResultDto>> GetAllAsync(int page, int pageSize, string? searchTerm = null)
        {
            using var conn = _connectionFactory.CreateConnection();

            // ? Dynamic SQL for Search
            var searchSql = "";
            var parameters = new DynamicParameters();
            parameters.Add("Offset", (page - 1) * pageSize);
            parameters.Add("PageSize", pageSize);

            if (!string.IsNullOrEmpty(searchTerm))
            {
                searchSql = "WHERE Name LIKE @SearchTerm";
                parameters.Add("SearchTerm", $"%{searchTerm}%");
            }

            string sql = $@"
                -- 1. Get Paginated Items
                SELECT Id, Name, Description, CreatedAt, UpdatedAt 
                FROM Categories 
                {searchSql}
                ORDER BY CreatedAt DESC
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

                -- 2. Get Total Count (for correct pagination)
                SELECT COUNT(*) FROM Categories {searchSql};";

            using var multi = await conn.QueryMultipleAsync(sql, parameters);

            var items = await multi.ReadAsync<CategoryResultDto>();
            var totalCount = await multi.ReadFirstAsync<int>();

            return new PagedResult<CategoryResultDto>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<CategoryResultDto?> GetByIdAsync(Guid categoryId)
        {
            using var conn = _connectionFactory.CreateConnection();
            string sql = "SELECT Id, Name, Description, CreatedAt, UpdatedAt FROM Categories WHERE Id = @Id";
            return await conn.QueryFirstOrDefaultAsync<CategoryResultDto>(sql, new { Id = categoryId });
        }
    }
}