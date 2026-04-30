using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Sandbox;

namespace PaneOS.InteractiveComputer;

public sealed class InteractiveComputerComponent : Component
{
	private static readonly Dictionary<string, ComputerState> statesByComputerId = new();
	private static readonly Dictionary<GameObject, InteractiveComputerComponent> activeComputersByPlayer = new();

	[Property] public string ComputerId { get; set; } = "computer-01";
	[Property] public int ResolutionX { get; set; } = 1024;
	[Property] public int ResolutionY { get; set; } = 768;
	[Property] public bool StartsSleeping { get; set; }
	[Property] public bool ScreenSaverEnabled { get; set; } = true;
	[Property] public float ScreenSaverDelaySeconds { get; set; } = 60f;
	[Property] public Vector2 ScreenSaverLogoSize { get; set; } = new( 220f, 72f );
	[Property] public Vector2 ScreenSaverVelocity { get; set; } = new( 160f, -120f );
	[Property] public bool InstallAllAppsWhenListIsEmpty { get; set; } = true;
	[Property, TextArea] public string InstalledAppIds { get; set; } = "";
	[Property, TextArea] public string SavedStateJson { get; set; } = "";

	public ComputerRuntime Runtime { get; private set; } = null!;
	public bool IsPlayerInteracting { get; private set; }
	public GameObject? InteractingPlayer { get; private set; }

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
		Runtime?.TickScreenSaver( Time.Delta, IsPlayerInteracting );

		if ( IsPlayerInteracting && Input.Pressed( "escape" ) )
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
		Runtime.MarkChanged();
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
			ResolutionX = ResolutionX,
			ResolutionY = ResolutionY,
			IsSleeping = StartsSleeping
		};

		state.ResolutionX = ResolutionX;
		state.ResolutionY = ResolutionY;
		ApplyCreationScreenSaverSettings( state, loadedFromSavedState );
		ApplyCreationAppList( state, loadedFromSavedState );
		statesByComputerId[ComputerId] = state;
		return state;
	}

	public void ResetStateFromCreationSettings()
	{
		var state = new ComputerState
		{
			ResolutionX = ResolutionX,
			ResolutionY = ResolutionY,
			IsSleeping = StartsSleeping
		};

		ApplyCreationScreenSaverSettings( state, false );
		ApplyCreationAppList( state, false );
		statesByComputerId[ComputerId] = state;
		Runtime = new ComputerRuntime( this, state );
		StoreState();
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
}
