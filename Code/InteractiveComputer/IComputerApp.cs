using System;
using Sandbox.UI;

namespace PaneOS.InteractiveComputer;

public interface IComputerApp
{
	ComputerAppSession Run( ComputerAppContext context );
}

public sealed class ComputerAppContext
{
	internal ComputerAppContext( InteractiveComputerComponent computer, ComputerRuntime runtime, ComputerInstalledAppState installedApp, ComputerAppState state )
	{
		Computer = computer;
		Runtime = runtime;
		InstalledApp = installedApp;
		State = state;
	}

	public InteractiveComputerComponent Computer { get; }
	public ComputerRuntime Runtime { get; }
	public ComputerInstalledAppState InstalledApp { get; }
	public ComputerAppState State { get; }

	public void SaveValue( string key, string value )
	{
		State.Data[key] = value;
		Runtime.MarkChanged();
	}

	public string? LoadValue( string key )
	{
		return State.Data.TryGetValue( key, out var value ) ? value : null;
	}

	public void SaveSetting( string key, string value )
	{
		InstalledApp.Settings[key] = value;
		Runtime.MarkChanged();
	}

	public string? LoadSetting( string key )
	{
		return InstalledApp.Settings.TryGetValue( key, out var value ) ? value : null;
	}
}

public sealed class ComputerAppSession
{
	public string? Title { get; init; }
	public string? Icon { get; init; }
	public Panel Content { get; init; } = new();
	public bool CanMinimize { get; init; } = true;
	public bool CanClose { get; init; } = true;
	public Action? OnFocused { get; init; }
	public Action? OnMinimized { get; init; }
	public Action? OnClosed { get; init; }
}
