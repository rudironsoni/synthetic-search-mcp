using System;
using System.Collections.Generic;
using System.Linq;

namespace SyntheticSearchMcp;

/// <summary>
/// Default implementation of <see cref="IToolRegistry"/> for managing MCP tools.
/// </summary>
public sealed class ToolRegistry : IToolRegistry
{
  private readonly Dictionary<string, IMcpTool> _tools = new(StringComparer.Ordinal);

  /// <inheritdoc/>
  public void Register(IMcpTool tool)
  {
    ArgumentNullException.ThrowIfNull(tool);
    if (string.IsNullOrWhiteSpace(tool.Name))
    {
      throw new ArgumentException("Tool name cannot be null or whitespace.", nameof(tool));
    }

    if (_tools.ContainsKey(tool.Name))
    {
      throw new InvalidOperationException($"A tool with the name '{tool.Name}' is already registered.");
    }

    _tools[tool.Name] = tool;
  }

  /// <inheritdoc/>
  public IReadOnlyCollection<IMcpTool> GetAll()
  {
    return _tools.Values.ToList().AsReadOnly();
  }

  /// <inheritdoc/>
  public IMcpTool? GetByName(string name)
  {
    if (string.IsNullOrWhiteSpace(name))
    {
      return null;
    }

    return _tools.TryGetValue(name, out var tool) ? tool : null;
  }
}
