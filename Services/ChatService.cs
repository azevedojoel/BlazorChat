using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace BlazorChat.Services
{
    public class ChatService
    {
        private readonly Kernel _kernel;
        private readonly IChatCompletionService _chatCompletionService;
        private readonly ChatHistory _history;

        public ChatService(string? openAiApiKey = null, string? braveApiKey = null)
        {
            // Use provided keys or try to get from environment
            openAiApiKey ??= Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            braveApiKey ??= Environment.GetEnvironmentVariable("BRAVE_API_KEY");

            if (string.IsNullOrEmpty(openAiApiKey))
                throw new ArgumentNullException(nameof(openAiApiKey), "OpenAI API key is required");

            // Create kernel builder with OpenAI
            var builder = Kernel.CreateBuilder().AddOpenAIChatCompletion("gpt-4o-mini", openAiApiKey);

            // Add logging
            builder.Services.AddLogging(services => services.AddConsole().SetMinimumLevel(LogLevel.Trace));

            // Build the kernel
            _kernel = builder.Build();
            _chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();

            // Add web search plugin if Brave API key is available
            if (!string.IsNullOrEmpty(braveApiKey))
            {
                _kernel.Plugins.AddFromObject(new WebSearchPlugin(braveApiKey), "WebSearch");
            }

            // Initialize chat history
            _history = new ChatHistory();
        }

        public ChatHistory History => _history;

        public void AddSystemMessage(string message)
        {
            _history.AddSystemMessage(message);
        }

        public void AddUserMessage(string message)
        {
            _history.AddUserMessage(message);
        }

        public async IAsyncEnumerable<string> GetStreamingCompletionAsync(string userMessage)
        {
            // Add user message to history
            _history.AddUserMessage(userMessage);

            // Configure execution settings
            var executionSettings = new OpenAIPromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
                Temperature = 0.87
            };

            // Get streaming response
            var response = _chatCompletionService.GetStreamingChatMessageContentsAsync(
                _history,
                executionSettings: executionSettings,
                kernel: _kernel);

            var fullResponse = new StringBuilder();

            await foreach (var chunk in response)
            {
                if (chunk.Content is null) continue;
                fullResponse.Append(chunk.Content);
                yield return chunk.Content;
            }

            // Add the complete response to history
            _history.AddAssistantMessage(fullResponse.ToString());
        }

        public void ClearHistory()
        {
            _history.Clear();
        }
    }
}

