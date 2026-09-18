using System;
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

            HttpResponseMessage response = null!;
            int delay = 2000; // Start with a shorter 2-second wait

            for (int i = 0; i < 2; i++) // Only retry twice instead of four times
            {
                response = await _httpClient.PostAsync($"{_apiUrl}?key={_apiKey}", jsonContent);
                if (response.IsSuccessStatusCode) break;

                await Task.Delay(delay);
                delay *= 2;
            }

            // FAIL-SAFE: If Google is still down, return a fake question instead of crashing
            if (!response.IsSuccessStatusCode)
            {
                return new List<Question>
                {
                    new Question
                    {
                        QuestionText = "Google AI is currently overloaded. Could not generate questions.",
                        OptionA = "Please",
                        OptionB = "Try",
                        OptionC = "Again",
                        OptionD = "Later",
                        CorrectOption = "OptionA",
                        Explanation = $"The API returned a {(int)response.StatusCode} error. The free tier is at capacity.",
                        Level = level
                    }
                };
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();

            try
            {
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
            catch
            {
                // Catch parsing errors if the AI returns broken JSON
                return new List<Question>();
            }
        }

        public async Task<string> GenerateSummaryAsync(string extractedText)
        {
            var prompt = $"Please provide a concise, 3-4 sentence summary of the main topics in the following text:\n\n{extractedText}";

            var requestBody = new
            {
                contents = new[] { new { parts = new[] { new { text = prompt } } } }
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            HttpResponseMessage response = null!;
            int delay = 2000; // Start with a shorter 2-second wait

            for (int i = 0; i < 2; i++) // Only retry twice instead of four times
            {
                response = await _httpClient.PostAsync($"{_apiUrl}?key={_apiKey}", jsonContent);
                if (response.IsSuccessStatusCode) break;

                await Task.Delay(delay);
                delay *= 2;
            }

            // FAIL-SAFE: If Google is still down, return a warning instead of crashing
            if (!response.IsSuccessStatusCode)
            {
                return "The AI summary feature is temporarily unavailable due to high server load on Google's free tier. Please try again later.";
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();

            try
            {
                using JsonDocument doc = JsonDocument.Parse(jsonResponse);
                return doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text").GetString()?.Trim() ?? "Summary generation failed.";
            }
            catch
            {
                return "Failed to parse the AI summary response.";
            }
        }
    }
}