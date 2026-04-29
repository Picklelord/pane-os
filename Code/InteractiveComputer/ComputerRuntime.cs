using System;
using System.Collections.Generic;
using System.Linq;
using PaneOS.InteractiveComputer.Core;

namespace PaneOS.InteractiveComputer;

public sealed class ComputerRuntime
{
	private readonly Dictionary<string, ComputerRunningApp> runningApps = new();
	private readonly InteractiveComputerComponent computer;
	private int nextZIndex = 10;

	public ComputerRuntime( InteractiveComputerComponent computer, ComputerState state )
	{
		this.computer = computer;
		State = state;
		Apps = ResolveInstalledApps();
		RehydrateOpenApps();
	}

	public ComputerState State { get; }
	public IReadOnlyList<ComputerAppDescriptor> Apps { get; private set; }
	public int Version { get; private set; }

	public IReadOnlyList<ComputerRunningApp> OpenApps => runningApps.Values
		.OrderBy( x => x.State.ZIndex )
		.ToArray();

	public ComputerRunningApp? FocusedApp => string.IsNullOrWhiteSpace( State.FocusedInstanceId )
		? null
		: runningApps.GetValueOrDefault( State.FocusedInstanceId );

	public event Action? Changed;

	public bool IsScreenSaverActive => State.ScreenSaver.Enabled && State.ScreenSaver.IsActive;

	public void TickScreenSaver( float deltaSeconds, bool isPlayerInteracting )
	{
		var wasActive = State.ScreenSaver.IsActive;
		var changed = ScreenSaverSimulator.Tick( State, deltaSeconds, isPlayerInteracting );
		if ( !changed )
			return;

		if ( isPlayerInteracting || (!wasActive && State.ScreenSaver.IsActive) )
		{
			MarkChanged();
			return;
		}

		Version++;
		Changed?.Invoke();
	}

	public void NotifyUserActivity()
	{
		if ( ScreenSaverSimulator.NotifyUserActivity( State ) )
			MarkChanged();
	}

	public void ResetScreenSaverIdle()
	{
		NotifyUserActivity();
	}

	public void DisableScreenSaverWhileInteracting()
	{
		NotifyUserActivity();
	}

	public void InstallApp( string appId )
	{
		if ( State.InstalledApps.Any( x => x.AppId == appId ) )
			return;

		State.InstalledApps.Add( new ComputerInstalledAppState { AppId = appId } );
		Apps = ResolveInstalledApps();
		MarkChanged();
	}

	public void UninstallApp( string appId, bool closeRunningInstances = true )
	{
		if ( closeRunningInstances )
		{
			foreach ( var app in runningApps.Values.Where( x => x.State.AppId == appId ).ToArray() )
			{
				Close( app.State.InstanceId );
			}
		}

		State.InstalledApps.RemoveAll( x => x.AppId == appId );
		State.OpenApps.RemoveAll( x => x.AppId == appId );
		Apps = ResolveInstalledApps();
		MarkChanged();
	}

	public ComputerRunningApp? OpenApp( string appId )
	{
		NotifyUserActivity();
		State.IsSleeping = false;
		State.StartMenuOpen = false;

		var descriptor = Apps.FirstOrDefault( x => x.Id == appId );
		if ( descriptor is null )
			return null;

		var offset = State.OpenApps.Count * 22;
		var appState = new ComputerAppState
		{
			InstanceId = Guid.NewGuid().ToString( "N" ),
			AppId = descriptor.Id,
			Title = descriptor.Title,
			Icon = descriptor.Icon,
			X = 70 + offset,
			Y = 54 + offset,
			Width = Math.Min( 620, Math.Max( 420, State.ResolutionX - 150 ) ),
			Height = Math.Min( 420, Math.Max( 280, State.ResolutionY - 170 ) ),
			ZIndex = ++nextZIndex
		};

		var running = CreateRunningApp( descriptor, appState );
		State.OpenApps.Add( appState );
		runningApps[appState.InstanceId] = running;
		Focus( appState.InstanceId );
		return running;
	}

	public void Focus( string instanceId )
	{
		NotifyUserActivity();
		if ( !runningApps.TryGetValue( instanceId, out var app ) )
			return;

		app.State.IsMinimized = false;
		app.State.ZIndex = ++nextZIndex;
		State.FocusedInstanceId = instanceId;
		app.Session.OnFocused?.Invoke();
		MarkChanged();
	}

	public void Minimize( string instanceId )
	{
		NotifyUserActivity();
		if ( !runningApps.TryGetValue( instanceId, out var app ) )
			return;

		app.State.IsMinimized = true;
		app.Session.OnMinimized?.Invoke();

		if ( State.FocusedInstanceId == instanceId )
			State.FocusedInstanceId = OpenApps.LastOrDefault( x => !x.State.IsMinimized && x.State.InstanceId != instanceId )?.State.InstanceId;

		MarkChanged();
	}

	public void ToggleTaskbarApp( string instanceId )
	{
		if ( !runningApps.TryGetValue( instanceId, out var app ) )
			return;

		if ( State.FocusedInstanceId == instanceId && !app.State.IsMinimized )
			Minimize( instanceId );
		else
			Focus( instanceId );
	}

	public void Close( string instanceId )
	{
		NotifyUserActivity();
		if ( !runningApps.TryGetValue( instanceId, out var app ) )
			return;

		app.Session.OnClosed?.Invoke();
		app.Session.Content.Delete();
		runningApps.Remove( instanceId );
		State.OpenApps.RemoveAll( x => x.InstanceId == instanceId );

		if ( State.FocusedInstanceId == instanceId )
			State.FocusedInstanceId = OpenApps.LastOrDefault( x => !x.State.IsMinimized )?.State.InstanceId;

		MarkChanged();
	}

	public void MoveWindow( string instanceId, int x, int y )
	{
		if ( !runningApps.TryGetValue( instanceId, out var app ) )
			return;

		app.State.X = Math.Clamp( x, 0, Math.Max( 0, State.ResolutionX - 80 ) );
		app.State.Y = Math.Clamp( y, 0, Math.Max( 0, State.ResolutionY - 80 ) );
		MarkChanged();
	}

	public void ResizeWindow( string instanceId, int width, int height )
	{
		if ( !runningApps.TryGetValue( instanceId, out var app ) )
			return;

		app.State.Width = Math.Clamp( width, 260, State.ResolutionX );
		app.State.Height = Math.Clamp( height, 180, State.ResolutionY );
		MarkChanged();
	}

	public void ToggleStartMenu()
	{
		NotifyUserActivity();
		State.IsSleeping = false;
		State.StartMenuOpen = !State.StartMenuOpen;
		MarkChanged();
	}

	public void Sleep()
	{
		State.StartMenuOpen = false;
		State.IsSleeping = true;
		MarkChanged();
	}

	public void Wake()
	{
		NotifyUserActivity();
		if ( !State.IsSleeping )
			return;

		State.IsSleeping = false;
		MarkChanged();
	}

	public void Restart()
	{
		NotifyUserActivity();
		foreach ( var app in runningApps.Values )
		{
			app.Session.OnClosed?.Invoke();
			app.Session.Content.Delete();
		}

		runningApps.Clear();
		State.OpenApps.Clear();
		State.FocusedInstanceId = null;
		State.StartMenuOpen = false;
		State.IsSleeping = false;
		ComputerAppRegistry.Refresh();
		Apps = ResolveInstalledApps();
		MarkChanged();
	}

	public void MarkChanged()
	{
		Version++;
		Changed?.Invoke();
		computer.StoreState();
	}

	private void RehydrateOpenApps()
	{
		foreach ( var appState in State.OpenApps.ToArray() )
		{
			var descriptor = Apps.FirstOrDefault( x => x.Id == appState.AppId );
			if ( descriptor is null )
			{
				State.OpenApps.Remove( appState );
				continue;
			}

			var running = CreateRunningApp( descriptor, appState );
			runningApps[appState.InstanceId] = running;
			nextZIndex = Math.Max( nextZIndex, appState.ZIndex );
		}
	}

	private ComputerRunningApp CreateRunningApp( ComputerAppDescriptor descriptor, ComputerAppState appState )
	{
		var installedApp = GetOrCreateInstalledAppState( descriptor.Id );
		var context = new ComputerAppContext( computer, this, installedApp, appState );
		var session = descriptor.Create().Run( context );

		if ( !string.IsNullOrWhiteSpace( session.Title ) )
			appState.Title = session.Title!;

		if ( !string.IsNullOrWhiteSpace( session.Icon ) )
			appState.Icon = session.Icon!;

		return new ComputerRunningApp( descriptor, appState, session );
	}

	private ComputerInstalledAppState GetOrCreateInstalledAppState( string appId )
	{
		var installedApp = State.InstalledApps.FirstOrDefault( x => x.AppId == appId );
		if ( installedApp is not null )
			return installedApp;

		installedApp = new ComputerInstalledAppState { AppId = appId };
		State.InstalledApps.Add( installedApp );
		Apps = ResolveInstalledApps();
		return installedApp;
	}

	private IReadOnlyList<ComputerAppDescriptor> ResolveInstalledApps()
	{
		var registry = ComputerAppRegistry.Apps.ToDictionary( x => x.Id );
		return State.InstalledApps
			.Select( x => registry.TryGetValue( x.AppId, out var descriptor ) ? descriptor : null )
			.Where( x => x is not null )
			.Select( x => x! )
			.OrderBy( x => x.SortOrder )
			.ThenBy( x => x.Title )
			.ToArray();
	}
}

public sealed class ComputerRunningApp
{
	internal ComputerRunningApp( ComputerAppDescriptor descriptor, ComputerAppState state, ComputerAppSession session )
	{
		Descriptor = descriptor;
		State = state;
		Session = session;
	}

	public ComputerAppDescriptor Descriptor { get; }
	public ComputerAppState State { get; }
	public ComputerAppSession Session { get; }
}
