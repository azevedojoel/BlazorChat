using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using HtmlAgilityPack;
using OpenAI.Chat;

namespace BlazorChat.Tools
{
    public class FetchUrlsTool : ITool
    {
        private readonly HttpClient _httpClient;
        private const int MAX_CONTENT_LENGTH = 50000; // Limit content to ~50K characters

        public FetchUrlsTool()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");
        }

        public string Icon => "fas fa-globe";
        
        public string DisplayName => "Fetch URL Content";

        public static string Name => "FetchUrls";

        public string Description => "Fetch and parse HTML content from a given URL";

        public ChatTool AsTool => ChatTool.CreateFunctionTool(
            functionName: Name,
            functionDescription: Description,
            functionParameters: BinaryData.FromBytes("""
                {
                    "type": "object",
                    "properties": {
                        "url": {
                            "type": "string",
                            "description": "The URL to fetch HTML content from"
                        }
                    },
                    "required": [ "url" ]
                }
                """u8.ToArray())
        );

        public async Task<string> ExecuteAsync(string toolCallId, BinaryData arguments)
        {
            // Parse the arguments
            string url = string.Empty;
            
            try
            {
                using JsonDocument argumentsJson = JsonDocument.Parse(arguments);
                if (argumentsJson.RootElement.TryGetProperty("url", out JsonElement urlElement))
                {
                    url = urlElement.GetString() ?? string.Empty;
                }
            }
            catch (Exception ex)
            {
                return $"Error parsing tool arguments: {ex.Message}";
            }

            if (string.IsNullOrEmpty(url))
            {
                return "URL cannot be empty.";
            }

            return await FetchAndParseHtmlAsync(url);
        }

        public async Task<string> FetchAndParseHtmlAsync(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return "URL cannot be empty.";
            }

            try
            {
                HttpResponseMessage response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    return $"Failed to fetch URL: {response.StatusCode} - {response.ReasonPhrase}";
                }

                string htmlContent = await response.Content.ReadAsStringAsync();
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(htmlContent);

                var parsedText = new StringBuilder();
                parsedText.AppendLine($"Parsed content from {url}:");

                // Try to get main content first
                var mainContent = htmlDoc.DocumentNode.SelectNodes("//main//text() | //article//text() | //div[@role='main']//text()");
                
                if (mainContent == null || !mainContent.Any())
                {
                    // Fallback to body text if no main content found
                    mainContent = htmlDoc.DocumentNode.SelectNodes("//body//text()[normalize-space() != '']");
                }

                if (mainContent != null)
                {
                    foreach (var node in mainContent)
                    {
                        string text = node.InnerText.Trim();
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            parsedText.AppendLine(text);
                            
                            // Check if we've exceeded the max length
                            if (parsedText.Length > MAX_CONTENT_LENGTH)
                            {
                                parsedText.AppendLine("\n[Content truncated due to length...]");
                                break;
                            }
                        }
                    }
                }

                return parsedText.ToString();
            }
            catch (Exception ex)
            {
                return $"Error during HTML fetch and parse: {ex.Message}";
            }
        }
    }
}
