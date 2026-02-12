using GenZStore.Data;
using GenZStore.DTOs;
using GenZStore.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace GenZStore.Commands
{
    // Interface
    public interface IProductCommand
    {
        Task<Guid> CreateProductAsync(ProductUploadDto dto);
        Task<bool> UpdateProductAsync(Guid productId, ProductDto dto);
        Task<bool> DeleteProductAsync(Guid productId);
    }

    // Implementation
    public class ProductCommand : IProductCommand
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly EmbeddingService _embeddingService; // 🔑 inject embedding service

        public ProductCommand(AppDbContext context, IWebHostEnvironment env, EmbeddingService embeddingService)
        {
            _context = context;
            _env = env;
            _embeddingService = embeddingService;
        }

        public async Task<Guid> CreateProductAsync(ProductUploadDto dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            if (dto.Image == null) throw new ArgumentException("Image is required.");

            // 📁 Save image to wwwroot/images/products
            var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var uploadFolder = Path.Combine(webRoot, "images", "products");
            Directory.CreateDirectory(uploadFolder);

            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(dto.Image.FileName)}";
            var physicalPath = Path.Combine(uploadFolder, fileName);

            using (var stream = new FileStream(physicalPath, FileMode.Create))
            {
                await dto.Image.CopyToAsync(stream);
            }

            var product = new Product
            {
                Id = Guid.NewGuid(),
                Name = dto.Name,
                Description = dto.Description,
                Usage = dto.Usage,
                Price = dto.Price,
                Quantity = dto.Quantity,
                CategoryId = dto.CategoryId,
                ImageUrl = $"/images/products/{fileName}",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _context.Products.AddAsync(product);
            await _context.SaveChangesAsync();

            // 🔑 Convert description to embedding and save in ProductEmbeddings table
            string textToEmbed = $"Product: {dto.Name}. Price: ${dto.Price}. Description: {dto.Description}. Usage: {dto.Usage}";
            var embedding = await _embeddingService.GenerateEmbeddingAsync(textToEmbed);
            // We removed the model name parameter because EmbeddingService now handles it internally
            await _embeddingService.SaveEmbeddingAsync(product.Id, embedding);
            return product.Id;
        }

        public async Task<bool> UpdateProductAsync(Guid productId, ProductDto dto)
        {
            var product = await _context.Products.FindAsync(productId);
            if (product == null) return false;

            // 1. Update the Product fields
            product.Name = dto.Name;
            product.Description = dto.Description;
            product.Usage = dto.Usage;
            product.Price = dto.Price;
            product.Quantity = dto.Quantity;
            product.CategoryId = dto.CategoryId;
            product.UpdatedAt = DateTime.UtcNow;

            _context.Products.Update(product);

            // 2. Remove OLD embeddings so we don't have duplicates
            var oldEmbeddings = _context.ProductEmbeddings.Where(e => e.ProductId == productId);
            _context.ProductEmbeddings.RemoveRange(oldEmbeddings);

            // 3. Save the changes (Product update + Embedding removal)
            await _context.SaveChangesAsync();

            // 4. Generate and Add the NEW embedding
            string textToEmbed = $"Product: {dto.Name}. Price: ${dto.Price}. Description: {dto.Description}. Usage: {dto.Usage}";
            var embedding = await _embeddingService.GenerateEmbeddingAsync(textToEmbed);

            // This now adds the *only* valid embedding for this product
            await _embeddingService.SaveEmbeddingAsync(product.Id, embedding);

            return true;
        }

        public async Task<bool> DeleteProductAsync(Guid productId)
        {
            var product = await _context.Products.FindAsync(productId);
            if (product == null) return false;

            _context.Products.Remove(product);

            // also remove embeddings
            var embeddings = _context.ProductEmbeddings.Where(e => e.ProductId == productId);
            _context.ProductEmbeddings.RemoveRange(embeddings);

            await _context.SaveChangesAsync();
            return true;
        }
    }
}