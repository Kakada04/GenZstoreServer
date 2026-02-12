using GenZStore.Data;
using GenZStore.Models;
using GenZStore.Services;
using Microsoft.EntityFrameworkCore;

public class ChatService
{
    private readonly EmbeddingService _embeddingService;
    private readonly GeminiService _gemini;
    private readonly AppDbContext _context; // 1. Inject DB to get Categories

    public ChatService(EmbeddingService embeddingService, GeminiService gemini, AppDbContext context)
    {
        _embeddingService = embeddingService;
        _gemini = gemini;
        _context = context;
    }

    public async Task<string> GetBotResponseAsync(string userQuestion)
    {
        // A. GET CATEGORIES
        var categories = await _context.Categories.Select(c => c.Name).ToListAsync();
        var categoryText = string.Join(", ", categories);

        // B. SEARCH PRODUCTS
        var relevantProducts = await _embeddingService.SemanticSearchAsync(userQuestion);
        var topProducts = relevantProducts.Take(3).Where(x => x != null).ToList();

        // C. BUILD CONTEXT
        var productText = topProducts.Any()
            ? string.Join("\n", topProducts.Select(p => $"• {p.Name} (${p.Price}) - Stock: {p.Quantity}"))
            : "No specific products matched this query.";

        // D. THE SMART PROMPT (Updated Instructions)
        var prompt = $@"
        You are the AI Assistant for 'GenZStore'.
        
        [YOUR KNOWLEDGE]
        1. We sell these Categories: {categoryText}
        2. Relevant Products found for this query:
        {productText}

        [USER QUESTION]
        ""{userQuestion}""

        [INSTRUCTIONS]
        - If the user asks for a product we have, showcase it and tell them to buy!
        - IMPORTANT: If the user wants to BUY or order, guide them exactly with these steps:
            1. Open our Telegram Mini App.
            2. Click 'Add to Cart' on the product.
            3. Go to Check Out.
            4. Confirm the order.
        - If we don't have the product, apologize and suggest one of our Categories: {categoryText}.
        - If general chat (Hi/Who are you), be cool and Gen-Z friendly. 😎
        - Language: Match the user's language (Khmer or English).
        ";

        var answer = await _gemini.GenerateContentAsync(prompt);

        // 💾 Save the interaction
        var log = new ChatLog
        {
            Id = Guid.NewGuid(),
            UserQuestion = userQuestion,
            AiAnswer = answer,
            CreatedAt = DateTime.UtcNow
        };
        _context.ChatLogs.Add(log);
        await _context.SaveChangesAsync();

        return answer;
    }
}