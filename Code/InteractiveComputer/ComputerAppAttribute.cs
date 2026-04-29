using System;

namespace PaneOS.InteractiveComputer;

/// <summary>
/// Marks a compiled class as a desktop app that should appear on interactive computers.
/// Drop app classes in Code/InteractiveComputer/Apps and implement IComputerApp.
/// </summary>
[AttributeUsage( AttributeTargets.Class, Inherited = false )]
public sealed class ComputerAppAttribute : Attribute
{
	public ComputerAppAttribute( string id, string title )
	{
		Id = id;
		Title = title;
	}

	public string Id { get; }
	public string Title { get; }
	public string Icon { get; init; } = "[]";
	public int SortOrder { get; init; }
}
