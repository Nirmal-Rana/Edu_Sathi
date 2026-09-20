using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace EduSathi.Services
{
    // Shape returned by GET /api/v1/Documents/{id}/summary's "data" field.
    public class DocumentSummaryResult
    {
        [JsonPropertyName("overview")]
        public List<string> Overview { get; set; } = new();

        [JsonPropertyName("keyTakeaways")]
        public List<KeyTakeaway> KeyTakeaways { get; set; } = new();
    }

    public class KeyTakeaway
    {
        [JsonPropertyName("label")]
        public string Label { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        public string Value { get; set; } = string.Empty;
    }

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

        // --- Existing method, used by the desktop Summary/Exam controllers. Untouched. ---
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
    - Where applicable, create a simple text-based flowchart or structural map using clean Markdown formatting to visually represent how the system/concepts connect.

    ### 5. 🌐 Deep Dive & Further Reading
    - Format this entire final section inside a styled HTML block like this:
      <div style=""background: #f0f4ff; border-left: 4px solid #5d5cf5; padding: 16px; border-radius: 8px; margin-top: 15px;"">
          <h4 style=""color: #3b35be; margin-top: 0; font-size: 16px;"">🌐 Deep Dive & Further Reading</h4>
          <p style=""margin-bottom: 8px; font-size: 14px;"">Explore these curated resources to master the topic:</p>
          <ul style=""margin: 0; padding-left: 20px; font-size: 14px;"">
              <li><a href=""https://en.wikipedia.org/wiki/Artificial_intelligence"" target=""_blank"" style=""color: #5d5cf5; font-weight: 600; text-decoration: none;"">Artificial Intelligence Overview - Wikipedia</a></li>
              <li><a href=""https://cloud.google.com/learn/what-is-artificial-intelligence"" target=""_blank"" style=""color: #5d5cf5; font-weight: 600; text-decoration: none;"">What is AI? - Google Cloud Learning</a></li>
              <li><a href=""https://www.ibm.com/topics/artificial-intelligence"" target=""_blank"" style=""color: #5d5cf5; font-weight: 600; text-decoration: none;"">IBM Artificial Intelligence Guide</a></li>
          </ul>
      </div>

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
                try
                {
                    response = await _httpClient.PostAsync($"{_apiUrl}?key={_apiKey}", jsonContent);
                    if (response.IsSuccessStatusCode) break;
                }
                catch { /* Ignore network blips and retry */ }

                await Task.Delay(delay);
                delay *= 2;
            }

            // IF API FAILS (503 / Rate Limit / Timeout), RETURN A CLEAN FALLBACK STUDY GUIDE FOR THE DEMO!
            if (response == null || !response.IsSuccessStatusCode)
            {
                return GetFallbackSummary();
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();

            try
            {
                using JsonDocument doc = JsonDocument.Parse(jsonResponse);
                return doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text").GetString()?.Trim() ?? GetFallbackSummary();
            }
            catch
            {
                return GetFallbackSummary();
            }
        }

        // --- New: structured overview + key-takeaways JSON for the mobile Answers tab. ---
        // Returns null on any failure (no fallback) so the caller can mark the
        // document Failed instead of silently persisting placeholder content.
        public async Task<DocumentSummaryResult?> GenerateStructuredSummaryAsync(string extractedText)
        {
            var prompt = $@"
    You are an expert educator creating a study aid from the text below.

    Respond with ONLY a raw JSON object (no markdown code fences, no commentary, no extra text before or after) matching exactly this shape:
    {{
      ""overview"": [""point 1"", ""point 2"", ""point 3""],
      ""keyTakeaways"": [
        {{ ""label"": ""Concept"", ""value"": ""Explanation text"" }}
      ]
    }}

    - ""overview"" should contain 3 to 6 concise bullet points summarizing the main ideas of the text.
    - ""keyTakeaways"" should contain 4 to 8 entries, each a short label (a term, formula, or concept name) paired with a one- or two-sentence explanation.

    Text to analyze:
    {extractedText}";

            var requestBody = new
            {
                contents = new[] { new { parts = new[] { new { text = prompt } } } }
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            HttpResponseMessage? response = null;
            int delay = 2000;

            for (int i = 0; i < 2; i++)
            {
                try
                {
                    response = await _httpClient.PostAsync($"{_apiUrl}?key={_apiKey}", jsonContent);
                    if (response.IsSuccessStatusCode) break;
                }
                catch { /* ignore network blips and retry */ }

                await Task.Delay(delay);
                delay *= 2;
            }

            if (response == null || !response.IsSuccessStatusCode)
                return null;

            var jsonResponse = await response.Content.ReadAsStringAsync();

            try
            {
                using JsonDocument doc = JsonDocument.Parse(jsonResponse);
                var rawText = doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text").GetString();

                if (string.IsNullOrWhiteSpace(rawText))
                    return null;

                var cleaned = StripMarkdownFences(rawText);

                return JsonSerializer.Deserialize<DocumentSummaryResult>(
                    cleaned,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch
            {
                return null;
            }
        }

        // Gemini sometimes wraps JSON in ```json ... ``` despite instructions not to.
        private static string StripMarkdownFences(string text)
        {
            var trimmed = text.Trim();
            if (!trimmed.StartsWith("```"))
                return trimmed;

            int firstNewline = trimmed.IndexOf('\n');
            if (firstNewline < 0) return trimmed;

            var withoutOpeningFence = trimmed.Substring(firstNewline + 1);
            int lastFence = withoutOpeningFence.LastIndexOf("```");
            return lastFence >= 0 ? withoutOpeningFence.Substring(0, lastFence).Trim() : withoutOpeningFence.Trim();
        }

        private string GetFallbackSummary()
        {
            return @"### 1. Simple Overview
- Artificial Intelligence (AI) is the simulation of human cognition by computer machines, enabling automated learning, reasoning, and problem-solving.

### 2. Detailed Topic Explanations
- **Machine Learning (ML):** Systems that learn from massive datasets without explicit hard-coded rules.
- **Deep Learning:** Multi-layered neural networks inspired by the human brain that handle advanced computer vision and natural language processing.
- **Core Applications:** Spans across modern healthcare diagnostics, financial fraud detection, and adaptive EdTech platforms like EduSathi.

### 3. Real-Life Examples & Analogies
- Similar to an apprentice learning a craft by observing thousands of examples rather than following a strict manual.

### 4. Visual Structure / Diagram
- [Input Data] ➔ [AI Model Processing] ➔ [Generated Insights & Actions]

<div style=""background: #f0f4ff; border-left: 4px solid #5d5cf5; padding: 16px; border-radius: 8px; margin-top: 15px;"">
    <h4 style=""color: #3b35be; margin-top: 0; font-size: 16px;"">🌐 Deep Dive & Further Reading</h4>
    <p style=""margin-bottom: 8px; font-size: 14px;"">Explore these curated resources to master the topic:</p>
    <ul style=""margin: 0; padding-left: 20px; font-size: 14px;"">
        <li><a href=""https://en.wikipedia.org/wiki/Artificial_intelligence"" target=""_blank"" style=""color: #5d5cf5; font-weight: 600; text-decoration: none;"">Artificial Intelligence Overview - Wikipedia</a></li>
        <li><a href=""https://cloud.google.com/learn/what-is-artificial-intelligence"" target=""_blank"" style=""color: #5d5cf5; font-weight: 600; text-decoration: none;"">What is AI? - Google Cloud Learning</a></li>
        <li><a href=""https://www.ibm.com/topics/artificial-intelligence"" target=""_blank"" style=""color: #5d5cf5; font-weight: 600; text-decoration: none;"">IBM Artificial Intelligence Guide</a></li>
    </ul>
</div>";
        }
    }
}