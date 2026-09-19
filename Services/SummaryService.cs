using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace EduSathi.Services
{
    public class SummaryService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _apiUrl;

        public SummaryService(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            _httpClient.Timeout = TimeSpan.FromSeconds(200);

            _apiKey = config["Gemini:ApiKey"] ?? throw new ArgumentNullException("API Key is missing");
            _apiUrl = config["Gemini:Url"] ?? throw new ArgumentNullException("API URL is missing");
        }

        public async Task<string> GenerateSummaryAsync(string extractedText)
        {
            var prompt = $@"
    You are an expert, friendly educator who specializes in making complex technical topics extremely simple and easy to understand for students. 
    
    Based on the provided text, generate a comprehensive, deep, and structured study guide. Follow this exact format:

    ### 1. Simple Overview
    - Explain what this text is about in plain, everyday language (like you're explaining it to a beginner).

    ### 2. Detailed Topic Explanations
    - Break down each major concept or architecture step-by-step in detail so the student misses nothing.

    ### 3. Real-Life Examples & Analogies
    - Provide clear, relatable, everyday real-life examples or analogies for the core concepts.

    ### 4. Visual Structure / Diagram
    - Where applicable, create a simple text-based flowchart or structural map using Markdown formatting (or a Mermaid.js code block) to visually represent how the system/concepts connect.

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

            if (!response.IsSuccessStatusCode)
            {
                return "The AI summary feature is temporarily unavailable due to high server load. Please try again later.";
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