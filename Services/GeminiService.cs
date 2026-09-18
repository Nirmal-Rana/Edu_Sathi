***REMOVED***using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using EduSathi.Models;

namespace EduSathi.Services
{
    public class GeminiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _apiUrl;

        public GeminiService(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            _apiKey = config["Gemini:ApiKey"] ?? throw new ArgumentNullException("API Key is missing");
            _apiUrl = config["Gemini:Url"] ?? throw new ArgumentNullException("API URL is missing");
        }

        public async Task<List<Question>> GenerateQuestionsAsync(string extractedText, QuestionLevel level, int questionCount = 5)
        {
            var prompt = $@"
            You are an expert tutor. Based on the provided text, generate {questionCount} multiple-choice questions at a {level} difficulty.
            You must return a raw JSON array where each object strictly follows this format:
            [{{
                ""QuestionText"": ""The actual question"",
                ""OptionA"": ""First option"",
                ""OptionB"": ""Second option"",
                ""OptionC"": ""Third option"",
                ""OptionD"": ""Fourth option"",
                ""CorrectOption"": ""OptionA"", 
                ""Explanation"": ""Detailed explanation for the correct answer""
            }}]
            Text to analyze: {extractedText}";

            var requestBody = new
            {
                contents = new[] { new { parts = new[] { new { text = prompt } } } },
                generationConfig = new { responseMimeType = "application/json" }
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            // --- THE RETRY LOOP ---
            HttpResponseMessage response = null!;
            for (int i = 0; i < 3; i++)
            {
                response = await _httpClient.PostAsync($"{_apiUrl}?key={_apiKey}", jsonContent);
                if (response.IsSuccessStatusCode) break;

                await Task.Delay(3000); // If it fails (like a 503), wait 3 seconds and try again
            }
            response.EnsureSuccessStatusCode();
            // ----------------------

            var jsonResponse = await response.Content.ReadAsStringAsync();

            using JsonDocument doc = JsonDocument.Parse(jsonResponse);
            var textNode = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text").GetString();

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var generatedQuestions = JsonSerializer.Deserialize<List<Question>>(textNode, options) ?? new List<Question>();

            foreach (var q in generatedQuestions)
            {
                q.Level = level;
            }

            return generatedQuestions;
        }

        public async Task<string> GenerateSummaryAsync(string extractedText)
        {
            var prompt = $"Please provide a concise, 3-4 sentence summary of the main topics in the following text:\n\n{extractedText}";

            var requestBody = new
            {
                contents = new[] { new { parts = new[] { new { text = prompt } } } }
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            // --- THE RETRY LOOP ---
            HttpResponseMessage response = null!;
            for (int i = 0; i < 3; i++)
            {
                response = await _httpClient.PostAsync($"{_apiUrl}?key={_apiKey}", jsonContent);
                if (response.IsSuccessStatusCode) break;

                await Task.Delay(3000); // If it fails, wait 3 seconds and try again
            }
            response.EnsureSuccessStatusCode();
            // ----------------------

            var jsonResponse = await response.Content.ReadAsStringAsync();

            using JsonDocument doc = JsonDocument.Parse(jsonResponse);
            return doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text").GetString()?.Trim() ?? "Summary generation failed.";
        }
    }
}