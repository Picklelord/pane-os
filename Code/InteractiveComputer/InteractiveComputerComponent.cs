using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;
using PaneOS.InteractiveComputer.Core;
using Sandbox;

namespace PaneOS.InteractiveComputer;

public sealed class InteractiveComputerComponent : Component
{
	private static readonly Dictionary<string, ComputerState> statesByComputerId = new();
	private static readonly Dictionary<GameObject, InteractiveComputerComponent> activeComputersByPlayer = new();

	[Property] public string ComputerId { get; set; } = "computer-01";
	[Property] public bool UseGameSettingsResolution { get; set; } = true;
	[Property] public int ResolutionX { get; set; } = 1024;
	[Property] public int ResolutionY { get; set; } = 768;
	[Property] public bool StartsSleeping { get; set; }
	[Property] public float RamGb { get; set; } = 2f;
	[Property] public float CpuCoreGhz { get; set; } = 3.7f;
	[Property] public int CpuCoreCount { get; set; } = 4;
	[Property] public float HddStorageGb { get; set; } = 256f;
	[Property] public float InternetSpeedGbps { get; set; } = 100f;
	[Property] public float GpuCoreGhz { get; set; } = 1.54f;
	[Property] public float GpuVramGb { get; set; } = 4f;
	[Property] public bool SimulateCpuInputDelayWhenMaxed { get; set; } = true;
	[Property] public bool ScreenSaverEnabled { get; set; } = true;
	[Property] public float ScreenSaverDelaySeconds { get; set; } = 60f;
	[Property] public Vector2 ScreenSaverLogoSize { get; set; } = new( 220f, 72f );
	[Property] public Vector2 ScreenSaverVelocity { get; set; } = new( 160f, -120f );
	[Property] public string ThemeName { get; set; } = "default";
	[Property] public string ExitInteractionInputAction { get; set; } = "use";
	[Property] public bool InstallAllAppsWhenListIsEmpty { get; set; } = true;
	[Property, TextArea] public string InstalledAppIds { get; set; } = "";
	[Property] public string ExplorerArchivePath { get; set; } = "";
	[Property, TextArea] public string SavedStateJson { get; set; } = "";

	public ComputerRuntime Runtime { get; private set; } = null!;
	public bool IsPlayerInteracting { get; private set; }
	public GameObject? InteractingPlayer { get; private set; }
	private int pendingExitRefreshFrames;

	public static InteractiveComputerComponent? GetActiveComputerForPlayer( GameObject? player )
	{
		if ( player is null )
			return null;

		return activeComputersByPlayer.GetValueOrDefault( player );
	}

	protected override void OnAwake()
	{
		var state = LoadState();
		Runtime = new ComputerRuntime( this, state );
	}

	protected override void OnUpdate()
	{
		RefreshResolutionFromSettings();
		Runtime?.TickScreenSaver( Time.Delta, IsPlayerInteracting );
		Runtime?.TickSystem( Time.Delta );
		if ( pendingExitRefreshFrames > 0 )
		{
			pendingExitRefreshFrames--;
			Runtime?.MarkChanged();
		}

		if ( IsPlayerInteracting &&
			!string.IsNullOrWhiteSpace( ExitInteractionInputAction ) &&
			Input.Pressed( ExitInteractionInputAction ) )
			EndInteraction();
	}

	public void BeginInteraction( GameObject? player )
	{
		if ( player is not null && activeComputersByPlayer.TryGetValue( player, out var activeComputer ) && activeComputer != this )
			activeComputer.EndInteraction();

		InteractingPlayer = player;
		IsPlayerInteracting = true;

		if ( player is not null )
			activeComputersByPlayer[player] = this;

		Runtime.DisableScreenSaverWhileInteracting();
		Runtime.Wake();
		Runtime.RefreshWindowAppSessions();
		Runtime.MarkChanged();
	}

	public void EndInteraction()
	{
		if ( InteractingPlayer is not null &&
			activeComputersByPlayer.TryGetValue( InteractingPlayer, out var activeComputer ) &&
			activeComputer == this )
		{
			activeComputersByPlayer.Remove( InteractingPlayer );
		}

		IsPlayerInteracting = false;
		InteractingPlayer = null;
		Runtime.ResetScreenSaverIdle();
		Runtime.RefreshWindowAppSessions();
		Runtime.MarkChanged();
		pendingExitRefreshFrames = 4;
	}

	public void ToggleInteraction( GameObject? player )
	{
		if ( IsPlayerInteracting )
			EndInteraction();
		else
			BeginInteraction( player );
	}

	public string ExportStateJson()
	{
		return JsonSerializer.Serialize( Runtime.State, JsonOptions );
	}

	public bool ImportStateJson( string stateJson )
	{
		try
		{
			var state = JsonSerializer.Deserialize<ComputerState>( stateJson, JsonOptions );
			if ( state is null )
				return false;

			statesByComputerId[ComputerId] = state;
			Runtime = new ComputerRuntime( this, state );
			SavedStateJson = JsonSerializer.Serialize( state, JsonOptions );
			return true;
		}
		catch ( Exception ex )
		{
			Log.Warning( $"Failed to import computer state for {ComputerId}: {ex.Message}" );
			return false;
		}
	}

	internal void StoreState()
	{
		if ( Runtime is null )
			return;

		statesByComputerId[ComputerId] = Runtime.State;
		SavedStateJson = ExportStateJson();
	}

	public string ReadArchiveTextFile( string virtualPath )
	{
		EnsureArchiveReady();
		return PaneArchiveFileSystem.ReadTextFile( ResolveArchivePath(), ParseVirtualPath( virtualPath ) );
	}

	public void WriteArchiveTextFile( string virtualPath, string content )
	{
		EnsureArchiveReady();
		PaneArchiveFileSystem.WriteTextFile( ResolveArchivePath(), ParseVirtualPath( virtualPath ), content );
		Runtime?.RefreshTransientUi();
	}

	public IReadOnlyList<string> ListArchiveItems( string virtualPath )
	{
		EnsureArchiveReady();
		return PaneArchiveFileSystem.GetItems( ResolveArchivePath(), ParseVirtualPath( virtualPath ) )
			.Select( x => x.VirtualPath )
			.ToArray();
	}

	public void CreateArchiveFolder( string parentVirtualPath, string folderName )
	{
		EnsureArchiveReady();
		PaneArchiveFileSystem.CreateFolder( ResolveArchivePath(), ParseVirtualPath( parentVirtualPath ), folderName );
		Runtime?.RefreshTransientUi();
	}

	public void CreateArchiveFile( string parentVirtualPath, string fileName, string extension, string content = "" )
	{
		EnsureArchiveReady();
		PaneArchiveFileSystem.CreateFile( ResolveArchivePath(), ParseVirtualPath( parentVirtualPath ), fileName, extension, content );
		Runtime?.RefreshTransientUi();
	}

	private ComputerState LoadState()
	{
		if ( statesByComputerId.TryGetValue( ComputerId, out var existingState ) )
			return existingState;

		ComputerState? state = null;
		var loadedFromSavedState = false;
		if ( !string.IsNullOrWhiteSpace( SavedStateJson ) )
		{
			try
			{
				state = JsonSerializer.Deserialize<ComputerState>( SavedStateJson, JsonOptions );
				loadedFromSavedState = state is not null;
			}
			catch ( Exception ex )
			{
				Log.Warning( $"Failed to parse saved computer state for {ComputerId}: {ex.Message}" );
			}
		}

		state ??= new ComputerState
		{
			IsSleeping = StartsSleeping
		};

		ApplyConfiguredResolution( state );
		ApplyCreationScreenSaverSettings( state, loadedFromSavedState );
		ApplyCreationAppList( state, loadedFromSavedState );
		statesByComputerId[ComputerId] = state;
		return state;
	}

	public void ResetStateFromCreationSettings()
	{
		var state = new ComputerState
		{
			IsSleeping = StartsSleeping
		};

		ApplyConfiguredResolution( state );
		ApplyCreationScreenSaverSettings( state, false );
		ApplyCreationAppList( state, false );
		statesByComputerId[ComputerId] = state;
		Runtime = new ComputerRuntime( this, state );
		StoreState();
	}

	internal string ResolveSteamDisplayName()
	{
		var name = InteractingPlayer?.Name;
		if ( string.IsNullOrWhiteSpace( name ) )
			return "Player";

		const string playerPrefix = "Player - ";
		if ( name.StartsWith( playerPrefix, StringComparison.OrdinalIgnoreCase ) )
			return name[playerPrefix.Length..];

		return name;
	}

	internal string ResolvePersistentArchiveUserName( ComputerState state )
	{
		if ( !string.IsNullOrWhiteSpace( state.ArchiveUserName ) )
			return state.ArchiveUserName;

		var archiveUserNamePath = ResolveArchiveUserNamePath();
		if ( ComputerSandboxStorage.FileExists( archiveUserNamePath ) )
		{
			var persistedValue = PaneArchiveFileSystem.NormalizeDisplayName( ComputerSandboxStorage.ReadAllText( archiveUserNamePath ).Trim() );
			if ( !string.IsNullOrWhiteSpace( persistedValue ) )
			{
				state.ArchiveUserName = persistedValue;
				StoreState();
				return state.ArchiveUserName;
			}
		}

		state.ArchiveUserName = ComputerArchiveUserPolicy.ResolveInitialUserName(
			ResolveSteamDisplayName(),
			ComputerSandboxStorage.GetLocalUserNameFallback() );

		ComputerSandboxStorage.WriteAllText( archiveUserNamePath, state.ArchiveUserName );
		StoreState();
		return state.ArchiveUserName;
	}

	internal string ResolveArchivePath()
	{
		return ComputerSandboxStorage.ResolveArchiveStoragePath( ComputerId, ExplorerArchivePath );
	}

	internal string ResolveArchiveUserNamePath()
	{
		return ComputerSandboxStorage.ResolveArchiveUserNameStoragePath( ResolveArchivePath() );
	}

	private void ApplyCreationAppList( ComputerState state, bool loadedFromSavedState )
	{
		if ( loadedFromSavedState && state.InstalledApps.Count > 0 )
			return;

		var configuredIds = ParseInstalledAppIds();
		var appIds = configuredIds.Count > 0
			? configuredIds
			: InstallAllAppsWhenListIsEmpty
				? ComputerAppRegistry.Apps.Select( x => x.Id ).ToList()
				: new List<string>();

		if ( appIds.Count == 0 )
			return;

		var existingSettings = state.InstalledApps
			.GroupBy( x => x.AppId )
			.ToDictionary( x => x.Key, x => x.First().Settings );

		state.InstalledApps = appIds
			.Where( x => !string.IsNullOrWhiteSpace( x ) )
			.Distinct()
			.Select( x => new ComputerInstalledAppState
			{
				AppId = x,
				Settings = existingSettings.TryGetValue( x, out var settings ) ? settings : new Dictionary<string, string>()
			} )
			.ToList();

		state.OpenApps.RemoveAll( x => !state.InstalledApps.Any( app => app.AppId == x.AppId ) );
	}

	private void ApplyCreationScreenSaverSettings( ComputerState state, bool loadedFromSavedState )
	{
		ApplyHardwareSettings( state );

		if ( loadedFromSavedState )
		{
			state.ScreenSaver.DelaySeconds = MathF.Max( 1f, state.ScreenSaver.DelaySeconds );
			state.ScreenSaver.LogoWidth = MathF.Max( 1f, state.ScreenSaver.LogoWidth );
			state.ScreenSaver.LogoHeight = MathF.Max( 1f, state.ScreenSaver.LogoHeight );
			return;
		}

		state.ScreenSaver.Enabled = ScreenSaverEnabled;
		state.ScreenSaver.DelaySeconds = MathF.Max( 1f, ScreenSaverDelaySeconds );
		state.ScreenSaver.LogoWidth = MathF.Max( 1f, ScreenSaverLogoSize.x );
		state.ScreenSaver.LogoHeight = MathF.Max( 1f, ScreenSaverLogoSize.y );
		state.ScreenSaver.VelocityX = ScreenSaverVelocity.x == 0f ? 160f : ScreenSaverVelocity.x;
		state.ScreenSaver.VelocityY = ScreenSaverVelocity.y == 0f ? -120f : ScreenSaverVelocity.y;
		state.ScreenSaver.LogoX = MathF.Max( 0f, (state.ResolutionX - state.ScreenSaver.LogoWidth) * 0.5f );
		state.ScreenSaver.LogoY = MathF.Max( 0f, (state.ResolutionY - state.ScreenSaver.LogoHeight) * 0.5f );
	}

	private void ApplyConfiguredResolution( ComputerState state )
	{
		var resolution = ResolveConfiguredResolution();
		state.ResolutionX = resolution.X;
		state.ResolutionY = resolution.Y;
	}

	private void ApplyHardwareSettings( ComputerState state )
	{
		state.Hardware.RamGb = MathF.Max( 0.25f, RamGb );
		state.Hardware.CpuCoreGhz = MathF.Max( 0.1f, CpuCoreGhz );
		state.Hardware.CpuCoreCount = Math.Max( 1, CpuCoreCount );
		state.Hardware.HddStorageGb = MathF.Max( 1f, HddStorageGb );
		state.Hardware.InternetSpeedGbps = MathF.Max( 0.1f, InternetSpeedGbps );
		state.Hardware.GpuCoreGhz = MathF.Max( 0.1f, GpuCoreGhz );
		state.Hardware.GpuVramGb = MathF.Max( 0.25f, GpuVramGb );
		state.Hardware.SimulateCpuInputDelayWhenMaxed = SimulateCpuInputDelayWhenMaxed;
	}

	private List<string> ParseInstalledAppIds()
	{
		return InstalledAppIds
			.Split( new[] { ',', ';', '\r', '\n', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries )
			.Where( x => !string.IsNullOrWhiteSpace( x ) )
			.ToList();
	}

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		WriteIndented = true
	};

	private void EnsureArchiveReady()
	{
		PaneArchiveFileSystem.EnsureArchive( ResolveArchivePath(), ResolvePersistentArchiveUserName( Runtime?.State ?? LoadState() ), Runtime?.Apps ?? ComputerAppRegistry.Apps );
	}

	private void RefreshResolutionFromSettings()
	{
		if ( Runtime is null )
			return;

		var resolution = ResolveConfiguredResolution();
		if ( Runtime.State.ResolutionX == resolution.X && Runtime.State.ResolutionY == resolution.Y )
			return;

		Runtime.State.ResolutionX = resolution.X;
		Runtime.State.ResolutionY = resolution.Y;
		ScreenSaverSimulator.ClampLogo( Runtime.State );
		Runtime.MarkChanged();
	}

	private (int X, int Y) ResolveConfiguredResolution()
	{
		return ComputerResolutionPolicy.ResolveResolution( UseGameSettingsResolution, ResolutionX, ResolutionY, Screen.Width, Screen.Height );
	}

	private static IReadOnlyList<string> ParseVirtualPath( string virtualPath )
	{
		return virtualPath
			.Trim()
			.TrimStart( '/' )
			.Split( '/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries );
	}
}
