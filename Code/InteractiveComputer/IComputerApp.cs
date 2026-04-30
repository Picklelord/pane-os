using System;
using System.Collections.Generic;
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

	public void SetStatus( ComputerProcessStatus status )
	{
		Runtime.SetStatus( State.InstanceId, status );
	}

	public ComputerProcessStatus GetStatus()
	{
		return Runtime.GetEffectiveStatus( State.InstanceId );
	}

	public ComputerProcessMetrics GetProcessMetrics()
	{
		return Runtime.GetProcessMetrics( State.InstanceId );
	}

	public ComputerActiveMessageBox ShowMessageBox( ComputerMessageBoxOptions options, Action<ComputerMessageBoxResult>? onClosed = null )
	{
		return Runtime.ShowMessageBox( options, onClosed );
	}

	public ComputerActiveFileDialog ShowOpenFileDialog( ComputerFileDialogOptions options, Action<ComputerFileDialogResult>? onClosed = null )
	{
		options.Mode = ComputerFileDialogMode.Open;
		return Runtime.ShowFileDialog( options, onClosed );
	}

	public ComputerActiveFileDialog ShowSaveFileDialog( ComputerFileDialogOptions options, Action<ComputerFileDialogResult>? onClosed = null )
	{
		options.Mode = ComputerFileDialogMode.Save;
		return Runtime.ShowFileDialog( options, onClosed );
	}

	public string GetDefaultDocumentsPath()
	{
		return Runtime.GetDefaultDocumentsPath();
	}

	public string GetArchivePath()
	{
		return Runtime.GetArchivePath();
	}

	public string ReadTextFile( string virtualPath )
	{
		return PaneOS.InteractiveComputer.Core.PaneArchiveFileSystem.ReadTextFile( Runtime.GetArchivePath(), ParseVirtualPath( virtualPath ) );
	}

	public void WriteTextFile( string virtualPath, string content )
	{
		PaneOS.InteractiveComputer.Core.PaneArchiveFileSystem.WriteTextFile( Runtime.GetArchivePath(), ParseVirtualPath( virtualPath ), content );
		Runtime.RefreshTransientUi();
	}

	public bool OpenVirtualPath( string virtualPath )
	{
		return Runtime.OpenVirtualPath( virtualPath );
	}

	private static IReadOnlyList<string> ParseVirtualPath( string virtualPath )
	{
		return virtualPath
			.Trim()
			.TrimStart( '/' )
			.Split( '/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries );
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
