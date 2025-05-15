using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;
using System.Text.Json;
using System.ClientModel;
using BlazorChat.Tools;

namespace BlazorChat.Services
{
    public class ChatService
    {
        private readonly ChatClient _chatClient;
        private readonly ILogger<ChatService> _logger;
        private readonly List<ChatMessage> _history;
        private readonly WebSearchTool? _webSearchPlugin;
        private readonly FetchUrlsTool? _fetchUrlsPlugin;

        public ChatService(string? openAiApiKey = null, string? braveApiKey = null, ILogger<ChatService>? logger = null)
        {
            // Use provided keys or try to get from environment
            openAiApiKey ??= Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            braveApiKey ??= Environment.GetEnvironmentVariable("BRAVE_API_KEY");

            if (string.IsNullOrEmpty(openAiApiKey))
                throw new ArgumentNullException(nameof(openAiApiKey), "OpenAI API key is required");

            // Create OpenAI client
            _chatClient = new ChatClient("gpt-4o-mini", openAiApiKey);
            _logger = logger ?? LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<ChatService>();
            
            // Initialize chat history
            _history = new List<ChatMessage>();

            _history.Add(new SystemChatMessage(@"
You are a helpful assistant with access to real-time web search and document retrieval tools. When answering user questions, first use web_search to find relevant sources. Then, if the search results include potentially useful links, call fetch_url on the most relevant ones to extract detailed information before answering. Only answer after gathering enough supporting context.

Prioritize:
	•	Official documentation and reputable sources
	•	Pages that match the user’s question closely
	•	Fast and informative summaries

Be concise and clear in your final answer. Use the tools independently and intelligently to support accurate, helpful responses.
            "));

            // Add web search plugin if Brave API key is available
            if (!string.IsNullOrEmpty(braveApiKey))
            {
                _webSearchPlugin = new WebSearchTool(braveApiKey);
            }

            _fetchUrlsPlugin = new FetchUrlsTool();
        }

        public IReadOnlyList<ChatMessage> History => _history.AsReadOnly();

        public void AddSystemMessage(string message)
        {
            _history.Add(new SystemChatMessage(message));
        }

        public void AddUserMessage(string message)
        {
            _history.Add(new UserChatMessage(message));
        }

        public async IAsyncEnumerable<StreamingChatCompletionUpdate> GetStreamingCompletionAsync(string userMessage)
        {
            // Add user message to history
            AddUserMessage(userMessage);

            // Create chat options
            var options = new ChatCompletionOptions
            {
                Temperature = 0.87f
            };

            // Add web search tool if plugin is available
            if (_webSearchPlugin != null)
            {
                options.Tools.Add(_webSearchPlugin.AsTool);
            }

            if (_fetchUrlsPlugin != null)
            {
                options.Tools.Add(_fetchUrlsPlugin.AsTool);
            }

            bool requiresAction;

            do
            {
                requiresAction = false;
                StringBuilder contentBuilder = new();
                var toolCallsBuilder = new StreamingChatToolCallsBuilder();


                    // Get streaming response
                    AsyncCollectionResult<StreamingChatCompletionUpdate> completionUpdates = 
                        _chatClient.CompleteChatStreamingAsync(_history, options);

                    await foreach (StreamingChatCompletionUpdate update in completionUpdates)
                    {
                        // Forward the update to the caller
                        yield return update;

                        // Accumulate the text content as new updates arrive
                        foreach (ChatMessageContentPart contentPart in update.ContentUpdate)
                        {
                            contentBuilder.Append(contentPart.Text);
                        }

                        // Build the tool calls as new updates arrive
                        foreach (StreamingChatToolCallUpdate toolCallUpdate in update.ToolCallUpdates)
                        {
                            toolCallsBuilder.Append(toolCallUpdate);
                        }

                        if (update.FinishReason == ChatFinishReason.ToolCalls)
                        {
                            // First, collect the accumulated function arguments into complete tool calls
                            IReadOnlyList<ChatToolCall> toolCalls = toolCallsBuilder.Build();

                            // Add the assistant message with tool calls to history
                            AssistantChatMessage assistantMessage = new(toolCalls);
                            if (contentBuilder.Length > 0)
                            {
                                assistantMessage.Content.Add(ChatMessageContentPart.CreateTextPart(contentBuilder.ToString()));
                            }
                            _history.Add(assistantMessage);

                            // Process each tool call
                            foreach (ChatToolCall toolCall in toolCalls)
                            {

                                if (toolCall.FunctionName == "FetchUrls" && _fetchUrlsPlugin != null)
                                {
                                    // Parse the arguments
                                    string url = userMessage; // Default to user message
                                    
                                    try
                                    {
                                        using JsonDocument argumentsJson = JsonDocument.Parse(toolCall.FunctionArguments);
                                        if (argumentsJson.RootElement.TryGetProperty("url", out JsonElement urlElement))
                                        {
                                            url = urlElement.GetString() ?? userMessage;
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogError(ex, "Error parsing tool arguments");
                                    }

                                    // Execute the URL fetch
                                    string fetchResult = await _fetchUrlsPlugin.FetchAndParseHtmlAsync(url);
                                    
                                    // Add tool message to history
                                    _history.Add(new ToolChatMessage(toolCall.Id, fetchResult));
                                    
                                    requiresAction = true;
                                }


                                if (toolCall.FunctionName == "WebSearch" && _webSearchPlugin != null)
                                {
                                    // Parse the arguments
                                    string query = userMessage; // Default to user message
                                    int count = 5; // Default count
                                    
                                    try
                                    {
                                        using JsonDocument argumentsJson = JsonDocument.Parse(toolCall.FunctionArguments);
                                        if (argumentsJson.RootElement.TryGetProperty("query", out JsonElement queryElement))
                                        {
                                            query = queryElement.GetString() ?? userMessage;
                                        }
                                        
                                        if (argumentsJson.RootElement.TryGetProperty("count", out JsonElement countElement))
                                        {
                                            count = countElement.GetInt32();
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogError(ex, "Error parsing tool arguments");
                                    }

                                    // Execute the web search
                                    string searchResult = await _webSearchPlugin.SearchWebAsync(query, count);
                                    
                                    // Add tool message to history
                                    _history.Add(new ToolChatMessage(toolCall.Id, searchResult));
                                    
                                    requiresAction = true;
                                }
                            }
                        }
                        else if (update.FinishReason == ChatFinishReason.Stop)
                        {
                            // Add the assistant message to history
                            _history.Add(new AssistantChatMessage(contentBuilder.ToString()));
                        }
                    }

                
            } while (requiresAction);
        }

        public void ClearHistory()
        {
            _history.Clear();
        }
    }
}

