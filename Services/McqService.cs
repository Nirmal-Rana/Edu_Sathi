using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace EduSathi.Services
{
    public class McqService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _apiUrl;

        public McqService(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            _httpClient.Timeout = TimeSpan.FromSeconds(200);

            _apiKey = config["Gemini:ApiKey"] ?? throw new ArgumentNullException("API Key is missing");
            _apiUrl = config["Gemini:Url"] ?? throw new ArgumentNullException("API URL is missing");
        }

        public async Task<string> GenerateMcqsAsync(string extractedText, int level, int questionCount = 10)
        {
            var levelName = level switch
            {
                1 => "Basic",
                2 => "Medium",
                3 => "Hard",
                _ => "Medium"
            };

            var prompt = $@"
    You are an expert educator. Based on the following text, generate exactly {questionCount} multiple-choice questions (MCQs) at a **{levelName}** difficulty level.

    Respond with ONLY a raw JSON array (no markdown code fences, no commentary, no extra text before or after).
    Each element must be an object with exactly these keys:
    - ""questionText"": string
    - ""optionA"": string
    - ""optionB"": string
    - ""optionC"": string
    - ""optionD"": string
    - ""correctOption"": one of ""A"", ""B"", ""C"", ""D""
    - ""explanation"": string

    Text to analyze:
    {extractedText}";

            var requestBody = new
            {
                contents = new[] { new { parts = new[] { new { text = prompt } } } }
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            HttpResponseMessage response = null!;
            int delay = 2000;

            for (int i = 0; i < 2; i++)
            {
                response = await _httpClient.PostAsync($"{_apiUrl}?key={_apiKey}", jsonContent);
                if (response.IsSuccessStatusCode) break;

                await Task.Delay(delay);
                delay *= 2;
            }

            if (response == null || !response.IsSuccessStatusCode)
            {
                return "The AI question generation feature is temporarily unavailable due to high server load. Please try again later.";
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();

            try
            {
                using JsonDocument doc = JsonDocument.Parse(jsonResponse);
                return doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text").GetString()?.Trim() ?? "MCQ generation failed.";
            }
            catch
            {
                return "Failed to parse the AI MCQ response.";
            }
        }
    }
}