using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using HtmlAgilityPack;
using OpenAI.Chat;

namespace BlazorChat.Tools
{
    public class FetchUrlsTool
    {
        private readonly HttpClient _httpClient;

        public FetchUrlsTool()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");
        }

        public ChatTool AsTool => ChatTool.CreateFunctionTool(
            functionName: "FetchUrls",
            functionDescription: "Fetch and parse HTML content from a given URL",
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

                foreach (var node in htmlDoc.DocumentNode.SelectNodes("//body//text()[normalize-space() != '']"))
                {
                    parsedText.AppendLine(node.InnerText.Trim());
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
