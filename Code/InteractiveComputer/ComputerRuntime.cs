using System;
using System.Collections.Generic;
using System.Linq;
using PaneOS.InteractiveComputer.Core;

namespace PaneOS.InteractiveComputer;

public sealed class ComputerRuntime
{
	private sealed class ProcessSimulationState
	{
		public float LeakMultiplier { get; set; } = 1f;
		public bool LeakTriggered { get; set; }
		public ComputerProcessMetrics Metrics { get; } = new();
	}

	private readonly Dictionary<string, ComputerRunningApp> runningApps = new();
	private readonly Dictionary<string, ProcessSimulationState> simulationByInstance = new();
	private readonly InteractiveComputerComponent computer;
	private readonly Queue<float> cpuHistory = new();
	private readonly Queue<float> ramHistory = new();
	private readonly Queue<float> gpuHistory = new();
	private readonly Queue<float> gpuVramHistory = new();
	private readonly List<ComputerNotification> notifications = new();
	private static readonly string[] RestartLogTemplates =
	{
		"[init] mounting system32 package registry",
		"[init] probing virtual storage controller",
		"[kern] syncing desktop shell state",
		"[kern] loading process scheduler tables",
		"[svc] starting networking.exe",
		"[svc] starting pvchost.exe",
		"[svc] starting paneos32.exe",
		"[ui ] loading explorer shell resources",
		"[drv] warming gpu compositor",
		"[sec] validating user profile archive",
		"[fs ] replaying recycle bin journal",
		"[net] binding local loopback bridge"
	};
	private readonly Random random = new();
	private int nextZIndex = 10;
	private float systemTickAccumulator;
	private float inputDelayRemaining;
	private float restartLogAccumulator;
	private bool lowMemoryNotificationActive;
	private ComputerActiveMessageBox? activeMessageBox;
	private ComputerActiveFileDialog? activeFileDialog;

	public ComputerRuntime( InteractiveComputerComponent computer, ComputerState state )
	{
		this.computer = computer;
		State = state;
		EnsureRequiredBackgroundAppsInstalled();
		Apps = ResolveInstalledApps();
		RehydrateOpenApps();
		EnsureStartupProcessesRunning();
		RefreshStorageMetrics();
	}

	public ComputerState State { get; }
	public IReadOnlyList<ComputerAppDescriptor> Apps { get; private set; }
	public int Version { get; private set; }
	public int DesktopVersion { get; private set; }
	public int MetricsVersion { get; private set; }
	public int StorageVersion { get; private set; }
	public ComputerSystemMetrics Metrics { get; } = new();
	public ComputerActiveMessageBox? ActiveMessageBox => activeMessageBox;
	public ComputerActiveFileDialog? ActiveFileDialog => activeFileDialog;
	public bool IsInputDelayed => inputDelayRemaining > 0f;
	public IReadOnlyList<ComputerNotification> Notifications => notifications.ToArray();
	public ComputerMetricHistory MetricHistory => new()
	{
		CpuSamples = cpuHistory.ToArray(),
		RamSamples = ramHistory.ToArray(),
		GpuSamples = gpuHistory.ToArray(),
		GpuVramSamples = gpuVramHistory.ToArray()
	};

	public IReadOnlyList<ComputerRunningApp> OpenApps => runningApps.Values
		.OrderBy( x => x.State.ZIndex )
		.ThenBy( x => x.State.Title, StringComparer.OrdinalIgnoreCase )
		.ToArray();

	public ComputerRunningApp? FocusedApp => string.IsNullOrWhiteSpace( State.FocusedInstanceId )
		? null
		: runningApps.GetValueOrDefault( State.FocusedInstanceId );

	public event Action? Changed;

	public bool IsScreenSaverActive => State.ScreenSaver.Enabled && State.ScreenSaver.IsActive;
	public bool IsRestarting => State.RestartLogSecondsRemaining > 0f || State.BootSplashSecondsRemaining > 0f;

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

		MarkDesktopChanged();
	}

	public void TickSystem( float deltaSeconds )
	{
		var desktopShouldRefresh = false;
		if ( inputDelayRemaining > 0f )
		{
			var wasDelayed = IsInputDelayed;
			inputDelayRemaining = MathF.Max( 0f, inputDelayRemaining - MathF.Max( 0f, deltaSeconds ) );
			if ( wasDelayed != IsInputDelayed )
				desktopShouldRefresh = true;
		}

		if ( State.RestartLogSecondsRemaining > 0f )
		{
			State.RestartLogSecondsRemaining = MathF.Max( 0f, State.RestartLogSecondsRemaining - MathF.Max( 0f, deltaSeconds ) );
			TickRestartLogs( deltaSeconds );
			desktopShouldRefresh = true;

			if ( State.RestartLogSecondsRemaining <= 0f )
			{
				State.RestartLogLines.Clear();
				State.BootSplashSecondsRemaining = 1.5f;
				restartLogAccumulator = 0f;
			}
		}

		if ( State.BootSplashSecondsRemaining > 0f )
		{
			State.BootSplashSecondsRemaining = MathF.Max( 0f, State.BootSplashSecondsRemaining - MathF.Max( 0f, deltaSeconds ) );
			desktopShouldRefresh = true;
		}

		var notificationsChanged = TickNotifications( deltaSeconds );
		desktopShouldRefresh |= notificationsChanged;

		systemTickAccumulator += MathF.Max( 0f, deltaSeconds );
		if ( systemTickAccumulator < 0.35f )
		{
			if ( desktopShouldRefresh )
				MarkDesktopChanged();

			return;
		}

		var sampledSeconds = systemTickAccumulator;
		systemTickAccumulator = 0f;
		var stateChanged = UpdateResourceSimulation( sampledSeconds );
		MetricsVersion++;
		if ( stateChanged )
		{
			MarkChanged();
			return;
		}

		if ( desktopShouldRefresh )
			MarkDesktopChanged();
		else
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
		EnsureStartupProcessesRunning();
		RefreshStorageMetrics();
		PushNotification( "Installed", $"{Apps.FirstOrDefault( x => x.Id == appId )?.Title ?? appId} is now available.", "+" );
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
		RefreshStorageMetrics();
		PushNotification( "Removed", $"{appId} was uninstalled.", "-" );
		MarkChanged();
	}

	public ComputerRunningApp? OpenApp( string appId )
	{
		NotifyUserActivity();
		State.IsSleeping = false;
		State.StartMenuOpen = false;
		return OpenAppInternal( appId, true, true, null );
	}

	public ComputerRunningApp? OpenApp( string appId, IReadOnlyDictionary<string, string> initialData )
	{
		NotifyUserActivity();
		State.IsSleeping = false;
		State.StartMenuOpen = false;
		return OpenAppInternal( appId, true, true, initialData );
	}

	public void Focus( string instanceId )
	{
		NotifyUserActivity();
		if ( !runningApps.TryGetValue( instanceId, out var app ) || !app.Descriptor.HasWindow )
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
		if ( !runningApps.TryGetValue( instanceId, out var app ) || !app.Descriptor.HasWindow )
			return;

		app.State.IsMinimized = true;
		app.Session.OnMinimized?.Invoke();

		if ( State.FocusedInstanceId == instanceId )
			State.FocusedInstanceId = OpenApps.LastOrDefault( x => x.Descriptor.HasWindow && !x.State.IsMinimized && x.State.InstanceId != instanceId )?.State.InstanceId;

		MarkChanged();
	}

	public void ToggleTaskbarApp( string instanceId )
	{
		if ( !runningApps.TryGetValue( instanceId, out var app ) || !app.Descriptor.HasWindow )
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
		simulationByInstance.Remove( instanceId );
		State.OpenApps.RemoveAll( x => x.InstanceId == instanceId );

		if ( State.FocusedInstanceId == instanceId )
			State.FocusedInstanceId = OpenApps.LastOrDefault( x => x.Descriptor.HasWindow && !x.State.IsMinimized )?.State.InstanceId;

		RefreshStorageMetrics();
		UpdateResourceSimulation( 0.35f );
		MarkChanged();
	}

	public void MoveWindow( string instanceId, int x, int y )
	{
		if ( !runningApps.TryGetValue( instanceId, out var app ) || !app.Descriptor.HasWindow )
			return;

		app.State.X = Math.Clamp( x, 0, Math.Max( 0, State.ResolutionX - 80 ) );
		app.State.Y = Math.Clamp( y, 0, Math.Max( 0, State.ResolutionY - 80 ) );
		MarkChanged();
	}

	public void ResizeWindow( string instanceId, int width, int height )
	{
		if ( !runningApps.TryGetValue( instanceId, out var app ) || !app.Descriptor.HasWindow )
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

	public void Lock()
	{
		State.StartMenuOpen = false;
		State.IsLocked = true;
		MarkChanged();
	}

	public void Unlock()
	{
		if ( !State.IsLocked )
			return;

		NotifyUserActivity();
		State.IsLocked = false;
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
		simulationByInstance.Clear();
		State.OpenApps.Clear();
		State.FocusedInstanceId = null;
		State.StartMenuOpen = false;
		State.IsSleeping = false;
		State.IsLocked = false;
		State.RestartLogSecondsRemaining = 5f;
		State.BootSplashSecondsRemaining = 0f;
		State.RestartLogLines.Clear();
		activeMessageBox = null;
		activeFileDialog = null;
		inputDelayRemaining = 0f;
		systemTickAccumulator = 0f;
		restartLogAccumulator = 0f;
		cpuHistory.Clear();
		ramHistory.Clear();
		gpuHistory.Clear();
		gpuVramHistory.Clear();
		notifications.Clear();
		lowMemoryNotificationActive = false;
		ComputerAppRegistry.Refresh();
		EnsureRequiredBackgroundAppsInstalled();
		Apps = ResolveInstalledApps();
		EnsureStartupProcessesRunning();
		RefreshStorageMetrics();
		MarkChanged();
	}

	public void SetStatus( string instanceId, ComputerProcessStatus status )
	{
		if ( !runningApps.TryGetValue( instanceId, out var app ) )
			return;

		if ( app.State.Status == status )
			return;

		app.State.Status = status;
		MarkChanged();
	}

	public ComputerProcessStatus GetEffectiveStatus( string instanceId )
	{
		return runningApps.TryGetValue( instanceId, out var app )
			? GetEffectiveStatus( app )
			: ComputerProcessStatus.Running;
	}

	public ComputerProcessMetrics GetProcessMetrics( string instanceId )
	{
		if ( !simulationByInstance.TryGetValue( instanceId, out var simulation ) )
			return new ComputerProcessMetrics();

		return new ComputerProcessMetrics
		{
			CpuPercent = simulation.Metrics.CpuPercent,
			RamMb = simulation.Metrics.RamMb,
			RamPercent = simulation.Metrics.RamPercent,
			GpuCorePercent = simulation.Metrics.GpuCorePercent,
			GpuVramPercent = simulation.Metrics.GpuVramPercent,
			StorageGb = simulation.Metrics.StorageGb
		};
	}

	public IReadOnlyList<ComputerStorageBreakdownItem> GetStorageBreakdown()
	{
		EnsureArchiveExists();
		var items = PaneArchiveFileSystem.BuildStorageBreakdown( GetArchivePath(), Apps );
		Metrics.UsedStorageGb = items.Sum( x => x.SizeGb );
		Metrics.UnusedStorageGb = MathF.Max( 0f, State.Hardware.HddStorageGb - Metrics.UsedStorageGb );
		return items;
	}

	public string GetDefaultDocumentsPath()
	{
		return $"/C:/Users/{ResolvePlayerFolderName()}/My Documents";
	}

	public string GetArchivePath()
	{
		return computer.ResolveArchivePath();
	}

	public string? LoadAppSetting( string appId, string key )
	{
		var installedApp = State.InstalledApps.FirstOrDefault( x => x.AppId == appId );
		return installedApp is not null && installedApp.Settings.TryGetValue( key, out var value ) ? value : null;
	}

	public void SaveAppSetting( string appId, string key, string value )
	{
		var installedApp = GetOrCreateInstalledAppState( appId );
		installedApp.Settings[key] = value;
		MarkChanged();
	}

	public bool IsAppInstalled( string appId )
	{
		return State.InstalledApps.Any( x => x.AppId == appId );
	}

	public bool OpenVirtualPath( string virtualPath )
	{
		var path = ParseVirtualPath( virtualPath );
		if ( path.Count == 0 )
			return false;

		EnsureArchiveExists();
		if ( PaneArchiveFileSystem.IsDirectory( GetArchivePath(), path ) )
			return false;

		var fileName = path.LastOrDefault() ?? "";
		var fileContent = PaneArchiveFileSystem.ReadTextFile( GetArchivePath(), path );
		var openResult = ComputerFileAssociationPolicy.ResolveOpenResult( virtualPath, fileName, fileContent, Apps );
		if ( !openResult.CanOpen || openResult.LaunchTarget is null )
		{
			ShowMessageBox( new ComputerMessageBoxOptions
			{
				Title = openResult.FailureTitle,
				Message = string.IsNullOrWhiteSpace( openResult.FailureMessage )
					? $"PaneOS does not know how to open {fileName}."
					: openResult.FailureMessage,
				Icon = "!",
				Buttons = new[] { "OK" }
			} );
			return false;
		}

		OpenApp( openResult.LaunchTarget.AppId, openResult.LaunchTarget.InitialData );
		return true;
	}

	public string DeleteVirtualPath( string virtualPath )
	{
		var path = ParseVirtualPath( virtualPath );
		if ( path.Count == 0 )
			return "";

		EnsureArchiveExists();
		if ( IsRecycleBinPath( path ) )
		{
			PaneArchiveFileSystem.Delete( GetArchivePath(), path );
			PushNotification( "Deleted", $"{path.Last()} was removed permanently.", "X" );
			RefreshTransientUi();
			return "";
		}

		var recycledPath = PaneArchiveFileSystem.MoveToRecycleBin( GetArchivePath(), path );
		PushNotification( "Moved To Recycle Bin", $"{path.Last()} can be restored later.", "RB" );
		RefreshTransientUi();
		return recycledPath;
	}

	public string RestoreVirtualPath( string virtualPath )
	{
		var path = ParseVirtualPath( virtualPath );
		if ( path.Count == 0 || !IsRecycleBinPath( path ) )
			return "";

		EnsureArchiveExists();
		var restoredPath = PaneArchiveFileSystem.RestoreFromRecycleBin( GetArchivePath(), path );
		if ( !string.IsNullOrWhiteSpace( restoredPath ) )
		{
			PushNotification( "Restored", $"{path.Last()} returned from the Recycle Bin.", "RB" );
			RefreshTransientUi();
		}

		return restoredPath;
	}

	public string RunSystemUpdateScan()
	{
		EnsureArchiveExists();
		var record = ComputerMaintenancePolicy.BuildUpdateScanRecord( State, Apps, DateTime.UtcNow );
		var reportPath = WriteMaintenanceRecord( record );
		PushNotification( record.NotificationTitle, record.NotificationMessage, "UP" );
		ShowMessageBox(
			new ComputerMessageBoxOptions
			{
				Title = record.Title,
				Message = record.Summary,
				Icon = "UP",
				Buttons = new[] { "Open Report", "Close" }
			},
			result =>
			{
				if ( result.ButtonPressed.Equals( "Open Report", StringComparison.OrdinalIgnoreCase ) )
					OpenVirtualPath( reportPath );
			} );
		return reportPath;
	}

	public string RunPackageInstall( string packageName )
	{
		EnsureArchiveExists();
		var record = ComputerMaintenancePolicy.BuildPackageInstallRecord( packageName, DateTime.UtcNow );
		var logPath = WriteMaintenanceRecord( record );
		PushNotification( record.NotificationTitle, record.NotificationMessage, "+" );
		ShowMessageBox(
			new ComputerMessageBoxOptions
			{
				Title = record.Title,
				Message = record.Summary,
				Icon = "+",
				Buttons = new[] { "Open Log", "Close" }
			},
			result =>
			{
				if ( result.ButtonPressed.Equals( "Open Log", StringComparison.OrdinalIgnoreCase ) )
					OpenVirtualPath( logPath );
			} );
		return logPath;
	}

	public bool ShouldBlockInput( string instanceId )
	{
		if ( activeMessageBox is not null )
			return true;

		if ( activeFileDialog is not null )
			return true;

		return ComputerInputDelayPolicy.ShouldSuppressFocusedAppInput(
			State.Hardware.SimulateCpuInputDelayWhenMaxed,
			IsInputDelayed,
			State.FocusedInstanceId == instanceId );
	}

	public ComputerActiveMessageBox ShowMessageBox( ComputerMessageBoxOptions options, Action<ComputerMessageBoxResult>? onClosed = null )
	{
		activeFileDialog = null;
		activeMessageBox = new ComputerActiveMessageBox
		{
			Options = options,
			CurrentText = options.TextInputValue,
			OnClosed = onClosed
		};
		MarkDesktopChanged();
		return activeMessageBox;
	}

	public ComputerActiveFileDialog ShowFileDialog( ComputerFileDialogOptions options, Action<ComputerFileDialogResult>? onClosed = null )
	{
		activeMessageBox = null;
		activeFileDialog = new ComputerActiveFileDialog
		{
			Options = options,
			CurrentPathSegments = ParseVirtualPath( string.IsNullOrWhiteSpace( options.InitialPath ) ? GetDefaultDocumentsPath() : options.InitialPath ).ToList(),
			CurrentFileName = options.DefaultFileName,
			OnClosed = onClosed
		};
		RefreshActiveFileDialogItems();
		MarkDesktopChanged();
		return activeFileDialog;
	}

	public void NavigateFileDialogTo( string virtualPath )
	{
		if ( activeFileDialog is null )
			return;

		activeFileDialog.CurrentPathSegments = ParseVirtualPath( virtualPath ).ToList();
		activeFileDialog.SelectedVirtualPath = "";
		RefreshActiveFileDialogItems();
		MarkDesktopChanged();
	}

	public void MoveFileDialogUp()
	{
		if ( activeFileDialog is null || activeFileDialog.CurrentPathSegments.Count == 0 )
			return;

		activeFileDialog.CurrentPathSegments = activeFileDialog.CurrentPathSegments.Take( activeFileDialog.CurrentPathSegments.Count - 1 ).ToList();
		activeFileDialog.SelectedVirtualPath = "";
		RefreshActiveFileDialogItems();
		MarkDesktopChanged();
	}

	public void SelectFileDialogItem( string virtualPath )
	{
		if ( activeFileDialog is null )
			return;

		activeFileDialog.SelectedVirtualPath = virtualPath;
		var fileName = ParseVirtualPath( virtualPath ).LastOrDefault() ?? "";
		if ( activeFileDialog.Options.Mode == ComputerFileDialogMode.Save )
			activeFileDialog.CurrentFileName = fileName;

		MarkDesktopChanged();
	}

	public void UpdateFileDialogFileName( string fileName )
	{
		if ( activeFileDialog is null )
			return;

		activeFileDialog.CurrentFileName = fileName ?? "";
		MarkDesktopChanged();
	}

	public void ActivateFileDialogItem( string virtualPath )
	{
		if ( activeFileDialog is null )
			return;

		var path = ParseVirtualPath( virtualPath );
		if ( PaneArchiveFileSystem.IsDirectory( GetArchivePath(), path ) )
		{
			NavigateFileDialogTo( virtualPath );
			return;
		}

		SelectFileDialogItem( virtualPath );
		if ( activeFileDialog.Options.Mode == ComputerFileDialogMode.Open )
			ConfirmFileDialog();
	}

	public void ConfirmFileDialog()
	{
		if ( activeFileDialog is null )
			return;

		var dialog = activeFileDialog;
		activeFileDialog = null;
		var resolvedPath = ResolveFileDialogPath( dialog );
		dialog.OnClosed?.Invoke( new ComputerFileDialogResult
		{
			Confirmed = !string.IsNullOrWhiteSpace( resolvedPath ),
			VirtualPath = resolvedPath,
			FileName = ParseVirtualPath( resolvedPath ).LastOrDefault() ?? ""
		} );
		MarkDesktopChanged();
	}

	public void CancelFileDialog()
	{
		if ( activeFileDialog is null )
			return;

		var dialog = activeFileDialog;
		activeFileDialog = null;
		dialog.OnClosed?.Invoke( new ComputerFileDialogResult() );
		MarkDesktopChanged();
	}

	public void UpdateMessageBoxText( string value )
	{
		if ( activeMessageBox is null )
			return;

		activeMessageBox.CurrentText = value;
		MarkDesktopChanged();
	}

	public void CloseMessageBox( string button )
	{
		if ( activeMessageBox is null )
			return;

		var messageBox = activeMessageBox;
		activeMessageBox = null;
		messageBox.OnClosed?.Invoke( new ComputerMessageBoxResult
		{
			ButtonPressed = button,
			TextValue = messageBox.CurrentText
		} );
		MarkDesktopChanged();
	}

	public void RefreshTransientUi()
	{
		RefreshStorageMetrics();
		StorageVersion++;
		Changed?.Invoke();
	}

	public void PushNotification( string title, string message, string icon = "i", float lifetimeSeconds = 4f )
	{
		notifications.Add( new ComputerNotification
		{
			Title = title,
			Message = message,
			Icon = icon,
			RemainingSeconds = MathF.Max( 1f, lifetimeSeconds )
		} );
		MarkDesktopChanged();
	}

	public void MarkChanged()
	{
		Version++;
		DesktopVersion++;
		StorageVersion++;
		Changed?.Invoke();
		computer.StoreState();
	}

	private void MarkDesktopChanged()
	{
		DesktopVersion++;
		Changed?.Invoke();
	}

	private void RehydrateOpenApps()
	{
		var duplicateSingleInstances = new HashSet<string>( StringComparer.OrdinalIgnoreCase );

		foreach ( var appState in State.OpenApps.ToArray() )
		{
			var descriptor = Apps.FirstOrDefault( x => x.Id == appState.AppId );
			if ( descriptor is null )
			{
				State.OpenApps.Remove( appState );
				continue;
			}

			if ( descriptor.SingleInstance && !duplicateSingleInstances.Add( descriptor.Id ) )
			{
				State.OpenApps.Remove( appState );
				continue;
			}

			var running = CreateRunningApp( descriptor, appState );
			runningApps[appState.InstanceId] = running;
			simulationByInstance[appState.InstanceId] = new ProcessSimulationState();
			nextZIndex = Math.Max( nextZIndex, appState.ZIndex );
		}
	}

	private ComputerRunningApp? OpenAppInternal( string appId, bool focusWindow, bool markChanged, IReadOnlyDictionary<string, string>? initialData )
	{
		var descriptor = Apps.FirstOrDefault( x => x.Id == appId );
		if ( descriptor is null )
			return null;

		if ( descriptor.SingleInstance )
		{
			var existing = OpenApps.FirstOrDefault( x => x.State.AppId == appId );
			if ( existing is not null )
			{
				if ( focusWindow && descriptor.HasWindow )
					Focus( existing.State.InstanceId );

				return existing;
			}
		}

		var visibleWindowCount = State.OpenApps.Count( x => GetDescriptor( x.AppId )?.HasWindow == true );
		var offset = visibleWindowCount * 22;
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
			ZIndex = ++nextZIndex,
			Status = ComputerProcessStatus.Running,
			Data = initialData is null ? new Dictionary<string, string>() : new Dictionary<string, string>( initialData )
		};

		var running = CreateRunningApp( descriptor, appState );
		State.OpenApps.Add( appState );
		runningApps[appState.InstanceId] = running;
		simulationByInstance[appState.InstanceId] = new ProcessSimulationState();

		if ( focusWindow && descriptor.HasWindow )
		{
			appState.IsMinimized = false;
			appState.ZIndex = ++nextZIndex;
			State.FocusedInstanceId = appState.InstanceId;
			running.Session.OnFocused?.Invoke();
		}

		RefreshStorageMetrics();
		if ( markChanged )
			MarkChanged();
		else
			MarkDesktopChanged();

		return running;
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
			.ThenBy( x => x.Title, StringComparer.OrdinalIgnoreCase )
			.ToArray();
	}

	private void EnsureRequiredBackgroundAppsInstalled()
	{
		foreach ( var app in ComputerAppRegistry.Apps.Where( x => x.RunOnStartup || x.IsBackgroundProcess ) )
		{
			if ( State.InstalledApps.Any( x => x.AppId == app.Id ) )
				continue;

			State.InstalledApps.Add( new ComputerInstalledAppState { AppId = app.Id } );
		}
	}

	private void EnsureStartupProcessesRunning()
	{
		foreach ( var app in Apps.Where( x => x.RunOnStartup ) )
		{
			if ( OpenApps.Any( x => x.State.AppId == app.Id ) )
				continue;

			OpenAppInternal( app.Id, false, false, null );
		}
	}

	private bool UpdateResourceSimulation( float deltaSeconds )
	{
		foreach ( var instanceId in simulationByInstance.Keys.Except( runningApps.Keys ).ToArray() )
		{
			simulationByInstance.Remove( instanceId );
		}

		var ramTotalMb = MathF.Max( 256f, State.Hardware.RamGb * 1024f );
		var gpuVramTotalMb = MathF.Max( 256f, State.Hardware.GpuVramGb * 1024f );
		var rawCpuTotal = 0f;
		var ramUsedMb = 0f;
		var rawGpuTotal = 0f;
		var gpuVramUsedMb = 0f;
		var persistentStateChanged = false;

		foreach ( var app in OpenApps )
		{
			var simulation = GetOrCreateSimulation( app.State.InstanceId );
			var descriptor = app.Descriptor;
			var metrics = simulation.Metrics;

			if ( app.State.Status != ComputerProcessStatus.NotResponding && descriptor.ChanceToStopRespondingPerMinute > 0f )
			{
				if ( random.NextSingle() < ToPerSampleChance( descriptor.ChanceToStopRespondingPerMinute, deltaSeconds ) )
				{
					app.State.Status = ComputerProcessStatus.NotResponding;
					PushNotification( "Application Error", $"{app.State.Title} has stopped responding.", "!" );
					ShowStopRespondingDialog( app );
					persistentStateChanged = true;
				}
			}

			if ( !simulation.LeakTriggered && descriptor.ChanceOfMemoryLeakPerMinute > 0f )
			{
				if ( random.NextSingle() < ToPerSampleChance( descriptor.ChanceOfMemoryLeakPerMinute, deltaSeconds ) )
				{
					simulation.LeakTriggered = true;
					simulation.LeakMultiplier = 1.18f;
					PushNotification( "Memory Warning", $"{app.State.Title} is using more memory than expected.", "!" );
					ShowMemoryLeakDialog( app );
				}
			}
			else if ( simulation.LeakTriggered )
			{
				simulation.LeakMultiplier += random.NextSingle() * 0.12f;
			}

			var cpuPercent = GetBaseCpuUsage( descriptor ) * RandomRange( 0.86f, 1.18f );
			var ramMb = GetBaseRamUsageMb( descriptor, ramTotalMb ) * simulation.LeakMultiplier * RandomRange( 0.95f, 1.08f );
			var gpuPercent = descriptor.ExpectedAvgGpuCoreUsagePercent * RandomRange( 0.85f, 1.2f );
			var gpuVramMb = (gpuVramTotalMb * (descriptor.ExpectedAvgGpuVramUsagePercent / 100f)) * RandomRange( 0.9f, 1.1f );

			if ( app.State.IsMinimized && descriptor.HasWindow )
			{
				cpuPercent *= 0.3f;
				gpuPercent *= 0.25f;
			}

			if ( GetEffectiveStatus( app ) == ComputerProcessStatus.NotResponding )
			{
				cpuPercent *= 0.22f;
				gpuPercent *= 0.2f;
			}

			metrics.CpuPercent = MathF.Max( 0f, cpuPercent );
			metrics.RamMb = MathF.Max( 1f, ramMb );
			metrics.RamPercent = MathF.Max( 0f, metrics.RamMb / ramTotalMb * 100f );
			metrics.GpuCorePercent = MathF.Max( 0f, gpuPercent );
			metrics.GpuVramPercent = MathF.Max( 0f, gpuVramMb / gpuVramTotalMb * 100f );
			metrics.StorageGb = descriptor.StorageSpaceUsedGb;

			rawCpuTotal += metrics.CpuPercent;
			ramUsedMb += metrics.RamMb;
			rawGpuTotal += metrics.GpuCorePercent;
			gpuVramUsedMb += gpuVramMb;
		}

		var hadResourceStarvedApps = OpenApps.Any( x => x.State.IsResourceStarved );
		if ( ramUsedMb > ramTotalMb && FocusedApp is not null )
		{
			foreach ( var app in OpenApps )
			{
				var shouldStarve = app.State.InstanceId == FocusedApp.State.InstanceId;
				if ( app.State.IsResourceStarved == shouldStarve )
					continue;

				app.State.IsResourceStarved = shouldStarve;
				persistentStateChanged = true;
			}
		}
		else if ( hadResourceStarvedApps )
		{
			foreach ( var app in OpenApps.Where( x => x.State.IsResourceStarved ) )
			{
				app.State.IsResourceStarved = false;
				persistentStateChanged = true;
			}
		}

		Metrics.CpuPercent = MathF.Min( 100f, rawCpuTotal );
		Metrics.RamTotalMb = ramTotalMb;
		Metrics.RamUsedMb = ramUsedMb;
		Metrics.RamPercent = MathF.Min( 100f, ramUsedMb / ramTotalMb * 100f );
		Metrics.GpuCorePercent = MathF.Min( 100f, rawGpuTotal );
		Metrics.GpuVramPercent = MathF.Min( 100f, gpuVramUsedMb / gpuVramTotalMb * 100f );
		RecordMetricSample( cpuHistory, Metrics.CpuPercent );
		RecordMetricSample( ramHistory, Metrics.RamPercent );
		RecordMetricSample( gpuHistory, Metrics.GpuCorePercent );
		RecordMetricSample( gpuVramHistory, Metrics.GpuVramPercent );
		MaybeNotifyResourcePressure();

		if ( rawCpuTotal >= 100f && State.Hardware.SimulateCpuInputDelayWhenMaxed )
			inputDelayRemaining = MathF.Max( inputDelayRemaining, RandomRange( 0.18f, 0.75f ) );

		return persistentStateChanged;
	}

	private ProcessSimulationState GetOrCreateSimulation( string instanceId )
	{
		if ( simulationByInstance.TryGetValue( instanceId, out var simulation ) )
			return simulation;

		simulation = new ProcessSimulationState();
		simulationByInstance[instanceId] = simulation;
		return simulation;
	}

	private float GetBaseCpuUsage( ComputerAppDescriptor descriptor )
	{
		var cpuCores = Math.Max( 1, State.Hardware.CpuCoreCount );
		return MathF.Max( 0f, descriptor.ExpectedCoreCountUsageAvg * descriptor.ExpectedAvgCpuCoreUsagePercent / cpuCores );
	}

	private static float GetBaseRamUsageMb( ComputerAppDescriptor descriptor, float totalRamMb )
	{
		if ( descriptor.ExpectedAvgRamUsagePercentOverride.HasValue )
			return totalRamMb * descriptor.ExpectedAvgRamUsagePercentOverride.Value / 100f;

		return descriptor.ExpectedAvgRamUsageMb;
	}

	private ComputerProcessStatus GetEffectiveStatus( ComputerRunningApp app )
	{
		if ( app.State.Status == ComputerProcessStatus.NotResponding || app.State.IsResourceStarved )
			return ComputerProcessStatus.NotResponding;

		if ( app.State.Status == ComputerProcessStatus.Suspended )
			return ComputerProcessStatus.Suspended;

		return app.State.IsMinimized && app.Descriptor.HasWindow
			? ComputerProcessStatus.Suspended
			: ComputerProcessStatus.Running;
	}

	private ComputerAppDescriptor? GetDescriptor( string appId )
	{
		return Apps.FirstOrDefault( x => x.Id == appId );
	}

	private void RefreshStorageMetrics()
	{
		var usedStorage = Apps.Sum( x => x.StorageSpaceUsedGb );
		Metrics.UsedStorageGb = usedStorage;
		Metrics.UnusedStorageGb = MathF.Max( 0f, State.Hardware.HddStorageGb - usedStorage );
	}

	private string WriteMaintenanceRecord( ComputerMaintenanceRecord record )
	{
		var virtualPath = $"{GetDefaultDocumentsPath().TrimEnd( '/')}/{record.FileName}";
		PaneArchiveFileSystem.WriteTextFile( GetArchivePath(), ParseVirtualPath( virtualPath ), record.FileContent );
		RefreshTransientUi();
		return virtualPath;
	}

	private void ShowStopRespondingDialog( ComputerRunningApp app )
	{
		if ( activeMessageBox is not null || !app.Descriptor.HasWindow )
			return;

		ShowMessageBox(
			new ComputerMessageBoxOptions
			{
				Title = "Application Error",
				Message = $"{app.State.Title} has stopped responding. You can wait for it or close it now.",
				Icon = "!",
				Buttons = new[] { "Wait", "Close App" }
			},
			result =>
			{
				if ( result.ButtonPressed.Equals( "Close App", StringComparison.OrdinalIgnoreCase ) )
					Close( app.State.InstanceId );
			} );
	}

	private void ShowMemoryLeakDialog( ComputerRunningApp app )
	{
		if ( activeMessageBox is not null || !app.Descriptor.HasWindow )
			return;

		ShowMessageBox(
			new ComputerMessageBoxOptions
			{
				Title = "Memory Warning",
				Message = $"{app.State.Title} may have a memory leak. Restarting it can recover RAM.",
				Icon = "!",
				Buttons = new[] { "Ignore", "Restart App" }
			},
			result =>
			{
				if ( result.ButtonPressed.Equals( "Restart App", StringComparison.OrdinalIgnoreCase ) )
					RestartAppInstance( app.State.InstanceId );
			} );
	}

	private void RestartAppInstance( string instanceId )
	{
		if ( !runningApps.TryGetValue( instanceId, out var app ) )
			return;

		var appId = app.State.AppId;
		var data = new Dictionary<string, string>( app.State.Data, StringComparer.OrdinalIgnoreCase );
		var focusWindow = app.Descriptor.HasWindow;
		Close( instanceId );
		OpenAppInternal( appId, focusWindow, true, data );
	}

	private void EnsureArchiveExists()
	{
		PaneArchiveFileSystem.EnsureArchive( GetArchivePath(), ResolvePlayerFolderName(), Apps );
	}

	private string ResolvePlayerFolderName()
	{
		return PaneArchiveFileSystem.NormalizeDisplayName( computer.ResolvePersistentArchiveUserName( State ) );
	}

	private static bool IsRecycleBinPath( IReadOnlyList<string> path )
	{
		return path.Count >= 2 &&
			path[0].Equals( "C:", StringComparison.OrdinalIgnoreCase ) &&
			path[1].Equals( "Recycle Bin", StringComparison.OrdinalIgnoreCase );
	}

	private void RefreshActiveFileDialogItems()
	{
		if ( activeFileDialog is null )
			return;

		EnsureArchiveExists();
		activeFileDialog.VisibleItems = PaneArchiveFileSystem.GetItems( GetArchivePath(), activeFileDialog.CurrentPathSegments )
			.Where( x => x.IsDirectory || ComputerFileDialogPolicy.AllowsExtension( activeFileDialog.Options, x.Extension ) )
			.ToArray();
	}

	private static IReadOnlyList<string> ParseVirtualPath( string virtualPath )
	{
		if ( string.IsNullOrWhiteSpace( virtualPath ) )
			return Array.Empty<string>();

		return virtualPath
			.Trim()
			.TrimStart( '/' )
			.Split( '/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries );
	}

	private static string ResolveFileDialogPath( ComputerActiveFileDialog dialog )
	{
		return ComputerFileDialogPolicy.ResolvePath(
			dialog.Options,
			dialog.CurrentPathSegments,
			dialog.SelectedVirtualPath,
			dialog.CurrentFileName );
	}

	private float RandomRange( float min, float max )
	{
		return min + (max - min) * random.NextSingle();
	}

	private static float ToPerSampleChance( float perMinuteChance, float deltaSeconds )
	{
		var normalizedChance = Math.Clamp( perMinuteChance, 0f, 1f );
		return 1f - MathF.Pow( 1f - normalizedChance, MathF.Max( 0f, deltaSeconds ) / 60f );
	}

	private static void RecordMetricSample( Queue<float> history, float value )
	{
		history.Enqueue( value );
		while ( history.Count > 24 )
			history.Dequeue();
	}

	private bool TickNotifications( float deltaSeconds )
	{
		var changed = false;
		for ( var index = notifications.Count - 1; index >= 0; index-- )
		{
			notifications[index].RemainingSeconds -= MathF.Max( 0f, deltaSeconds );
			if ( notifications[index].RemainingSeconds > 0f )
				continue;

			notifications.RemoveAt( index );
			changed = true;
		}

		return changed;
	}

	private void MaybeNotifyResourcePressure()
	{
		if ( Metrics.RamPercent >= 90f && !lowMemoryNotificationActive )
		{
			lowMemoryNotificationActive = true;
			PushNotification( "Low Memory", "PaneOS is running low on RAM. Some apps may stop responding.", "!" );
		}
		else if ( Metrics.RamPercent <= 75f )
		{
			lowMemoryNotificationActive = false;
		}
	}

	private void TickRestartLogs( float deltaSeconds )
	{
		restartLogAccumulator -= MathF.Max( 0f, deltaSeconds );
		if ( restartLogAccumulator > 0f )
			return;

		restartLogAccumulator = RandomRange( 0.18f, 0.55f );
		var nextLine = RestartLogTemplates[random.Next( RestartLogTemplates.Length )];
		State.RestartLogLines.Add( $"{DateTime.UtcNow:HH:mm:ss.fff} {nextLine}" );
		while ( State.RestartLogLines.Count > 12 )
			State.RestartLogLines.RemoveAt( 0 );
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
