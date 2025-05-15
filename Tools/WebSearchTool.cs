using System.ComponentModel;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenAI.Chat;

namespace BlazorChat.Tools;

public class WebSearchTool : ITool
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private const string BraveSearchEndpoint = "https://api.search.brave.com/res/v1/web/search";

    public WebSearchTool(string apiKey)
    {
        _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        _httpClient.DefaultRequestHeaders.Add("X-Subscription-Token", _apiKey);
    }

    public string Icon => "fas fa-globe";
    
    public string DisplayName => "Web Search";

    public static string Name => "WebSearch";

    public string Description => "Search the web for the given query";

    public ChatTool AsTool => ChatTool.CreateFunctionTool(
    functionName: Name,
    functionDescription: Description,
    functionParameters: BinaryData.FromBytes("""
                {
                    "type": "object",
                    "properties": {
                        "query": {
                            "type": "string",   
                            "description": "The search query to find information on the web"
                        },
                        "count": {
                            "type": "integer",
                            "description": "The number of results to return (default: 5)"
                        }   
                    },
                    "required": [ "query" ]
                }
                """u8.ToArray())
    );

    public async Task<string> ExecuteAsync(string toolCallId, BinaryData arguments)
    {
        // Parse the arguments
        string query = string.Empty;
        int count = 5; // Default count
        
        try
        {
            using JsonDocument argumentsJson = JsonDocument.Parse(arguments);
            if (argumentsJson.RootElement.TryGetProperty("query", out JsonElement queryElement))
            {
                query = queryElement.GetString() ?? string.Empty;
            }
            
            if (argumentsJson.RootElement.TryGetProperty("count", out JsonElement countElement))
            {
                count = countElement.GetInt32();
            }
        }
        catch (Exception ex)
        {
            return $"Error parsing tool arguments: {ex.Message}";
        }

        if (string.IsNullOrEmpty(query))
        {
            return "Search query cannot be empty.";
        }

        return await SearchWebAsync(query, count);
    }

    public async Task<string> SearchWebAsync(string query, int count = 5)
    {
        if (string.IsNullOrEmpty(query))
        {
            return "Search query cannot be empty.";
        }

        try
        {
            string url = $"{BraveSearchEndpoint}?q={Uri.EscapeDataString(query)}&count={count}";
            HttpResponseMessage response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                return $"Failed to search: {response.StatusCode} - {response.ReasonPhrase}";
            }

            string jsonResponse = await response.Content.ReadAsStringAsync();
            var searchResult = JsonSerializer.Deserialize<BraveSearchResponse>(jsonResponse);

            if (searchResult?.Web?.Results == null || !searchResult.Web.Results.Any())
            {
                return "No results found.";
            }

            // Format the results
            var formattedResults = new System.Text.StringBuilder();
            formattedResults.AppendLine($"Search results for \"{query}\":\n");

            foreach (var result in searchResult.Web.Results.Take(count))
            {
                formattedResults.AppendLine($"Title: {result.Title}");
                formattedResults.AppendLine($"URL: {result.Url}");
                formattedResults.AppendLine($"Description: {result.Description}");
                formattedResults.AppendLine();
            }

            return formattedResults.ToString();
        }
        catch (Exception ex)
        {
            return $"Error during web search: {ex.Message}";
        }
    }
}

public class BraveSearchResponse
{
    [JsonPropertyName("web")]
    public WebResults? Web { get; set; }
}

public class WebResults
{
    [JsonPropertyName("results")]
    public List<SearchResult> Results { get; set; } = new();
}

public class SearchResult
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
}