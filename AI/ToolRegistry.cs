using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CivilGPT.AI.Tools;

namespace CivilGPT.AI
{
    /// <summary>
    /// Реестр всех инструментов плагина.
    /// Инструмент — атомарная операция, которую может вызвать ИИ или код команды.
    /// </summary>
    public class ToolRegistry
    {
        private readonly Dictionary<string, IPluginTool> _tools;

        public ToolRegistry()
        {
            _tools = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => typeof(IPluginTool).IsAssignableFrom(t)
                            && !t.IsAbstract
                            && !t.IsInterface
                            && t.GetConstructor(Type.EmptyTypes) != null)
                .Select(t => (IPluginTool)Activator.CreateInstance(t))
                .ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
        }

        public IReadOnlyCollection<IPluginTool> All => _tools.Values.ToList();

        public IPluginTool Find(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            _tools.TryGetValue(name, out IPluginTool tool);
            return tool;
        }
    }
}
