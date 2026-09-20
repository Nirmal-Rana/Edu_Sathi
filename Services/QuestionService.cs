using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace EduSathi.Services
{
    public class QuestionService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public QuestionService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _apiKey = configuration["Gemini:ApiKey"] ?? "";
        }

        public async Task<string> GenerateFlashcardsAsync(string documentText)
        {
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={_apiKey}";

            var prompt = "You are an expert educator. Based on the following document text, generate 5 to 7 short, high-yield active-recall flashcard pairs. " +
                         "Keep the answers short, sweet, and memorable so students can review them quickly. " +
                         "Format your response strictly as a JSON array of objects with 'q' and 'a' keys, like this: " +
                         "[{\"q\": \"Question text here?\", \"a\": \"Short answer here.\"}] " +
                         "\n\nDocument Text:\n" + documentText;

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = prompt }
                        }
                    }
                }
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(url, jsonContent);

            if (!response.IsSuccessStatusCode)
            {
                return "[]";
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            // Parse Gemini response text structure if needed, or return raw json
            return jsonResponse;
        }
    }
}