using GenZStore.Data;
using GenZStore.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using GenZStore.Services;

public class EmbeddingService
{
    private readonly AppDbContext _context;
    private readonly GeminiEmbeddingService _geminiEmbedder; // Changed from OllamaClient

    public EmbeddingService(AppDbContext context, GeminiEmbeddingService geminiEmbedder)
    {
        _context = context;
        _geminiEmbedder = geminiEmbedder;
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text)
    {
        // Now calls Google instead of Local Ollama
        return await _geminiEmbedder.GetEmbeddingAsync(text);
    }

    public async Task SaveEmbeddingAsync(Guid productId, float[] embedding)
    {
        var entity = new ProductEmbedding
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            ModelName = "text-embedding-004", // Updated model name
            Embedding = JsonSerializer.Serialize(embedding),
            CreatedAt = DateTime.UtcNow
        };

        _context.ProductEmbeddings.Add(entity);
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<Product>> SemanticSearchAsync(string query)
    {
        // 1. Convert user question to numbers using Google
        var queryEmbedding = await _geminiEmbedder.GetEmbeddingAsync(query);

        var products = await _context.ProductEmbeddings
            .Include(e => e.Product)
            .ToListAsync();

        // 2. Compare numbers (Cosine Similarity)
        var results = products
            .Select(pe => new
            {
                Product = pe.Product,
                Score = CosineSimilarity(queryEmbedding, JsonSerializer.Deserialize<float[]>(pe.Embedding))
            })
            .Where(x => x.Score > 0.35) // Filter out bad matches
            .OrderByDescending(x => x.Score)
            .Select(x => x.Product);

        return results;
    }
    // Add inside EmbeddingService.cs

    public async Task SaveCategoryEmbeddingAsync(Guid categoryId, float[] embedding)
    {
        var entity = new CategoryEmbedding // Make sure you create this Model class too!
        {
            Id = Guid.NewGuid(),
            CategoryId = categoryId,
            ModelName = "text-embedding-004",
            Embedding = JsonSerializer.Serialize(embedding),
            CreatedAt = DateTime.UtcNow
        };

        _context.CategoryEmbeddings.Add(entity); // Add DbSet<CategoryEmbedding> to AppDbContext
        await _context.SaveChangesAsync();
    }

    public async Task DeleteCategoryEmbeddingAsync(Guid categoryId)
    {
        var embeddings = _context.CategoryEmbeddings.Where(e => e.CategoryId == categoryId);
        _context.CategoryEmbeddings.RemoveRange(embeddings);
        await _context.SaveChangesAsync();
    }
    private double CosineSimilarity(float[] v1, float[] v2)
    {
        if (v1.Length != v2.Length) return 0;
        double dot = 0, mag1 = 0, mag2 = 0;
        for (int i = 0; i < v1.Length; i++)
        {
            dot += v1[i] * v2[i];
            mag1 += v1[i] * v1[i];
            mag2 += v2[i] * v2[i];
        }
        return dot / (Math.Sqrt(mag1) * Math.Sqrt(mag2));
    }
}