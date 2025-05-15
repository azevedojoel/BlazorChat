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
        private readonly List<ITool> _tools = new();

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

            // Add web search plugin if Brave API key is available
            if (!string.IsNullOrEmpty(braveApiKey))
            {
                _tools.Add(new WebSearchTool(braveApiKey));
            }

            _tools.Add(new FetchUrlsTool());
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

            // Add tools to the options
            foreach (var tool in _tools)
            {
                options.Tools.Add(tool.AsTool);
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
                            // Find the tool that can handle this call
                            ITool? matchingTool = _tools.FirstOrDefault(t => 
                                t.AsTool.FunctionName == toolCall.FunctionName);

                            if (matchingTool != null)
                            {
                                try
                                {
                                    // Execute the tool with its arguments
                                    string toolResult = await matchingTool.ExecuteAsync(
                                        toolCall.Id, 
                                        toolCall.FunctionArguments);

                                    // Add tool message to history
                                    _history.Add(new ToolChatMessage(toolCall.Id, toolResult));
                                    
                                    requiresAction = true;
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, $"Error executing tool {toolCall.FunctionName}");
                                    _history.Add(new ToolChatMessage(toolCall.Id, 
                                        $"Error executing tool: {ex.Message}"));
                                    requiresAction = true;
                                }
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

