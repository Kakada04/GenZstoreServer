using Microsoft.AspNetCore.Http;

namespace GenZStore.DTOs
{
    public class ProductUploadDto
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Usage { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public Guid CategoryId { get; set; }
        public IFormFile Image { get; set; }
    }

    public class ProductDto
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Usage { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public Guid CategoryId { get; set; }
    }
    public class PagedResult<T>
    {
        public IEnumerable<T> Items { get; set; }
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    }
}