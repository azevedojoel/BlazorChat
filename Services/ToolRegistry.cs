using System.Collections.Generic;
using System.Linq;
using BlazorChat.Tools;

namespace BlazorChat.Services
{
    public class ToolRegistry
    {
        private readonly Dictionary<string, ITool> _tools = new();

        public ToolRegistry(IEnumerable<ITool> tools)
        {
            foreach (var tool in tools)
            {
                _tools[tool.AsTool.FunctionName] = tool;
            }
        }

        public bool TryGetTool(string toolName, out ITool? tool)
        {
            return _tools.TryGetValue(toolName, out tool);
        }

        public string GetIcon(string toolName)
        {
            if (_tools.TryGetValue(toolName, out var tool))
            {
                return tool.Icon;
            }
            return "fas fa-tools"; // Default icon
        }

        public string GetDisplayName(string toolName)
        {
            if (_tools.TryGetValue(toolName, out var tool))
            {
                return tool.DisplayName;
            }
            return toolName; // Default to the tool name
        }

        public IEnumerable<ITool> GetAllTools()
        {
            return _tools.Values;
        }
    }
} 