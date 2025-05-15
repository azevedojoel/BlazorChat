using System.ClientModel;
using System.Threading.Tasks;
using OpenAI.Chat;

namespace BlazorChat.Tools
{
    public interface ITool
    {
        string Icon { get; }
        string DisplayName { get; }
        static string Name { get; } = string.Empty;
        string Description { get; }
        ChatTool AsTool { get; }
        Task<string> ExecuteAsync(string toolCallId, BinaryData arguments);
    }
} 