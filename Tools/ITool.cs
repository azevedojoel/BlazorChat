using System.ClientModel;
using System.Threading.Tasks;
using OpenAI.Chat;

namespace BlazorChat.Tools
{
    public interface ITool
    {
        ChatTool AsTool { get; }
        Task<string> ExecuteAsync(string toolCallId, BinaryData arguments);
    }
} 