using GenZStore.Data;
using GenZStore.Models; // Ensure this namespace exists
using Microsoft.EntityFrameworkCore;

namespace GenZStore.Commands
{
    // DTO for category input
    public class CategoryDto
    {
        public string Name { get; set; }
        public string Description { get; set; }
    }

    // Interface for category commands
    public interface ICategoryCommand
    {
        Task<Guid> CreateCategoryAsync(CategoryDto dto);
        Task<bool> UpdateCategoryAsync(Guid categoryId, CategoryDto dto);
        Task<bool> DeleteCategoryAsync(Guid categoryId);
    }

    // Implementation
    public class CategoryCommand : ICategoryCommand
    {
        private readonly AppDbContext _context;
        private readonly EmbeddingService _embeddingService; // ?? 1. Inject EmbeddingService

        public CategoryCommand(AppDbContext context, EmbeddingService embeddingService)
        {
            _context = context;
            _embeddingService = embeddingService;
        }

        public async Task<Guid> CreateCategoryAsync(CategoryDto dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            if (string.IsNullOrWhiteSpace(dto.Name))
                throw new ArgumentException("Category name is required.");

            var category = new Category
            {
                Id = Guid.NewGuid(),
                Name = dto.Name,
                Description = dto.Description,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _context.Categories.AddAsync(category);
            await _context.SaveChangesAsync();

            // ?? 2. Generate Embedding for Category
            // This helps the AI understand "Footwear" = "Shoes"
            string textToEmbed = $"Category: {dto.Name}. Description: {dto.Description}";
            var embedding = await _embeddingService.GenerateEmbeddingAsync(textToEmbed);

            // ?? 3. Save to Database (See note below!)
            await _embeddingService.SaveCategoryEmbeddingAsync(category.Id, embedding);

            return category.Id;
        }

        public async Task<bool> UpdateCategoryAsync(Guid categoryId, CategoryDto dto)
        {
            var category = await _context.Categories.FindAsync(categoryId);
            if (category == null) return false;

            category.Name = dto.Name;
            category.Description = dto.Description;
            category.UpdatedAt = DateTime.UtcNow;

            _context.Categories.Update(category);

            // ?? 4. Update Embedding: Remove OLD, Add NEW
            // We need to implement DeleteCategoryEmbeddingAsync in EmbeddingService
            await _embeddingService.DeleteCategoryEmbeddingAsync(categoryId);

            await _context.SaveChangesAsync();

            // Generate New
            string textToEmbed = $"Category: {dto.Name}. Description: {dto.Description}";
            var embedding = await _embeddingService.GenerateEmbeddingAsync(textToEmbed);
            await _embeddingService.SaveCategoryEmbeddingAsync(category.Id, embedding);

            return true;
        }

        public async Task<bool> DeleteCategoryAsync(Guid categoryId)
        {
            var category = await _context.Categories.FindAsync(categoryId);
            if (category == null) return false;

            _context.Categories.Remove(category);

            // ?? 5. Remove Embedding
            await _embeddingService.DeleteCategoryEmbeddingAsync(categoryId);

            await _context.SaveChangesAsync();

            return true;
        }
    }
}