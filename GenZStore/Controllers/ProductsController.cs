using GenZStore.Commands;
using GenZStore.DTOs;
using GenZStore.Queries;
using GenZStore.Models;
using GenZStore.Services;
using Microsoft.AspNetCore.Mvc;

namespace GenZStore.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController : ControllerBase
    {
        private readonly IProductCommand _productCommand;
        private readonly IProductQuery _productQuery;
        private readonly EmbeddingService _embeddingService;
        private readonly ChatService _chatService;

        public ProductsController(IProductCommand productCommand,
                                  IProductQuery productQuery,
                                  EmbeddingService embeddingService,
                                  ChatService chatService)
        {
            _productCommand = productCommand;
            _productQuery = productQuery;
            _embeddingService = embeddingService;
            _chatService = chatService;
        }

        // ✅ HELPER: Generates dynamic full URL based on Hosting Environment
        private string? GetFullImageUrl(string? relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return null;
            if (relativePath.StartsWith("https")) return relativePath; // Already a full URL (e.g. S3)

            // Dynamic Scheme (http/https) and Host (localhost:7000 or mydomain.com)
            var request = HttpContext.Request;
            var baseUrl = $"{request.Scheme}://{request.Host}";

            return $"{baseUrl}/{relativePath.TrimStart('/')}";
        }

        public class AskRequest
        {
            public string Question { get; set; }
        }

        [HttpPost("ask")]
        public async Task<IActionResult> AskBot([FromBody] AskRequest request)
        {
            var answer = await _chatService.GetBotResponseAsync(request.Question);
            return Ok(new { answer });
        }

        // ✅ Create product
        [HttpPost("create")]
        public async Task<IActionResult> CreateProduct([FromForm] ProductUploadDto dto)
        {
            var productId = await _productCommand.CreateProductAsync(dto);
            return Ok(new { ProductId = productId });
        }

        // ✅ Update product
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateProduct(Guid id, [FromBody] ProductDto dto)
        {
            var success = await _productCommand.UpdateProductAsync(id, dto);
            return success ? Ok(new { Message = "Updated successfully" }) : NotFound();
        }

        // ✅ Delete product
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProduct(Guid id)
        {
            var success = await _productCommand.DeleteProductAsync(id);
            return success ? Ok(new { Message = "Deleted successfully" }) : NotFound();
        }

        // ✅ Get all products (With Full Image URL)
        [HttpGet]
        public async Task<IActionResult> GetAllProducts([FromQuery] int page = 1, [FromQuery] int pageSize = 8)
        {
            // 1. Get Paged Data from DB
            var result = await _productQuery.GetAllAsync(page, pageSize);

            // 2. Enrich Image URLs (Loop through the Items list)
            foreach (var p in result.Items)
            {
                p.ImageUrl = GetFullImageUrl(p.ImageUrl);
            }

            // 3. Return the wrapper (Items + TotalCount)
            return Ok(result);
        }

        // ✅ Get product by Id (With Full Image URL)
        [HttpGet("{id}")]
        public async Task<IActionResult> GetProductById(Guid id)
        {
            var product = await _productQuery.GetByIdAsync(id);

            if (product != null)
            {
                product.ImageUrl = GetFullImageUrl(product.ImageUrl);
                return Ok(product);
            }

            return NotFound();
        }

        // ✅ Search product by name (With Full Image URL)
        [HttpGet("search")]
        public async Task<IActionResult> SearchProduct([FromQuery] string name)
        {
            var products = await _productQuery.SearchByNameAsync(name);

            foreach (var p in products)
            {
                p.ImageUrl = GetFullImageUrl(p.ImageUrl);
            }

            return Ok(products);
        }

        // ✅ Semantic search (With Full Image URL)
        [HttpGet("semantic-search")]
        public async Task<IActionResult> SemanticSearch([FromQuery] string query)
        {
            var results = await _embeddingService.SemanticSearchAsync(query);

            // Assuming results map back to Products, you might need to fetch full details
            // If EmbeddingService returns Product entities, loop them too:
            /*
            foreach (var p in results) 
            {
                p.ImageUrl = GetFullImageUrl(p.ImageUrl);
            }
            */

            return Ok(results);
        }
    }
}