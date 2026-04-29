using System;

namespace PaneOS.InteractiveComputer;

public sealed class ComputerAppDescriptor
{
	public required string Id { get; init; }
	public required string Title { get; init; }
	public string Icon { get; init; } = "[]";
	public int SortOrder { get; init; }
	public required Func<IComputerApp> Factory { get; init; }

	public IComputerApp Create()
	{
		return Factory();
	}
}
