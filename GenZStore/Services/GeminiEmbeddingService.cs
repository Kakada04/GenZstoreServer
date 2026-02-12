using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GenZStore.Services
{
    public class GeminiEmbeddingService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        // 🚨 CHANGE THIS: Use the model from your list (gemini-embedding-001)
        private readonly string _baseUrl = "https://generativelanguage.googleapis.com/v1beta/models/gemini-embedding-001:embedContent";

        public GeminiEmbeddingService(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            _apiKey = config["Gemini:ApiKey"];
        }

        public async Task<float[]> GetEmbeddingAsync(string text)
        {
            var requestUrl = $"{_baseUrl}?key={_apiKey}";

            var payload = new
            {
                // 🚨 OPTIONAL: You can match the URL, or remove this line entirely
                model = "models/gemini-embedding-001",
                content = new { parts = new[] { new { text = text } } }
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(requestUrl, content);

            // Debugging: If it fails, show the real Google error message
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Google Embedding Error: {response.StatusCode} - {error}");
            }

            var result = await response.Content.ReadFromJsonAsync<GeminiEmbeddingResponse>();
            return result?.Embedding?.Values ?? Array.Empty<float>();
        }

        private class GeminiEmbeddingResponse { [JsonPropertyName("embedding")] public EmbeddingData Embedding { get; set; } }
        private class EmbeddingData { [JsonPropertyName("values")] public float[] Values { get; set; } }
    }
}