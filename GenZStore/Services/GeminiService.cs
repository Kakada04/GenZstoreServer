using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GenZStore.Services
{
    public class GeminiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _baseUrl;

        public GeminiService(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            _apiKey = config["Gemini:ApiKey"];
            _baseUrl = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent";
        }

        public async Task<string> GenerateContentAsync(string prompt)
        {
            // 1. Manually create a fresh client (Bypasses any bad config in Program.cs)
            using var client = new HttpClient();

            // 2. Use your hardcoded URL
            var requestUrl = $"{_baseUrl}?key={_apiKey}";

            var payload = new
            {
                contents = new[]
                {
            new { parts = new[] { new { text = prompt } } }
        }
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // 3. Send using the fresh client
            var response = await client.PostAsync(requestUrl, content);

            // If this fails, print the actual error content to the debug console
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Google API Error: {response.StatusCode} - {error}");
            }

            var result = await response.Content.ReadFromJsonAsync<GeminiResponse>();

            return result?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text
                    ?? "Sorry, I couldn't generate a response.";
        }
        // Add this new method to your existing GeminiService class
        public async Task<string> TranscribeAudioAsync(byte[] audioData)
        {
            // 1. Convert Audio to Base64
            var base64Audio = Convert.ToBase64String(audioData);

            // 2. Build the Payload (Multimodal: Text + Audio)
            var payload = new
            {
                contents = new[]
                {
            new
            {
                parts = new object[]
                {
                    // Instruction for the AI
                    new { text = "Listen to this audio. Accurately transcribe it to text. If it is in Khmer, transcribe in Khmer. Do not add any other words, just the transcription." },
                    // The Audio File
                    new
                    {
                        inline_data = new
                        {
                            mime_type = "audio/ogg", // Telegram sends OGG files
                            data = base64Audio
                        }
                    }
                }
            }
        }
            };

            var requestUrl = $"{_baseUrl}?key={_apiKey}";
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(requestUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Gemini Audio Error: {response.StatusCode} - {error}");
            }

            var result = await response.Content.ReadFromJsonAsync<GeminiResponse>();
            return result?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text
                   ?? "";
        }
        // Helper classes to read Google's complex JSON
        private class GeminiResponse { [JsonPropertyName("candidates")] public GeminiCandidate[] Candidates { get; set; } }
        private class GeminiCandidate { [JsonPropertyName("content")] public GeminiContent Content { get; set; } }
        private class GeminiContent { [JsonPropertyName("parts")] public GeminiPart[] Parts { get; set; } }
        private class GeminiPart { [JsonPropertyName("text")] public string Text { get; set; } }
    }
}