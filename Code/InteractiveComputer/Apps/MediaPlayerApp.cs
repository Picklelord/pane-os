using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sandbox;
using Sandbox.UI;
using PaneOS.InteractiveComputer.Core;

namespace PaneOS.InteractiveComputer.Apps;

[ComputerApp( "system.mediaplayer", "Media Player", Icon = "MP", SortOrder = 26 )]
public sealed class MediaPlayerApp : IComputerApp
{
	public ComputerAppSession Run( ComputerAppContext context )
	{
		return new ComputerAppSession
		{
			Title = "Media Player",
			Icon = "MP",
			Content = new MediaPlayerPanel( context )
		};
	}
}

[StyleSheet( "InteractiveComputerApps.scss" )]
public sealed class MediaPlayerPanel : ComputerWarmupPanel
{
	private readonly ComputerAppContext context;
	private readonly List<string> basePlaylist = new();
	private Label statusLabel = null!;
	private Label nowPlayingLabel = null!;
	private Label repeatLabel = null!;
	private Label shuffleLabel = null!;
	private Panel progressFill = null!;
	private Panel playlistHost = null!;
	private Panel playlistSection = null!;
	private Panel videoMeta = null!;
	private int trackIndex;
	private float progress;
	private bool isPlaying = true;
	private bool showPlaylist;
	private bool shuffleEnabled;
	private ComputerMediaRepeatMode repeatMode = ComputerMediaRepeatMode.Playlist;

	public MediaPlayerPanel( ComputerAppContext context )
	{
		this.context = context;
		AddClass( "media-player-app" );
		SeedDefaultPlaylist();
		RestoreSessionState();
		BuildUi();
		UpdateDisplay();
	}

	protected override void WarmupRefresh()
	{
		BuildUi();
		UpdateDisplay();
	}

	public override void Tick()
	{
		base.Tick();

		if ( !isPlaying )
			return;

		progress = MathF.Min( 1f, progress + Time.Delta * 0.12f );
		if ( progress >= 1f )
			NextTrack();

		UpdateDisplay();
	}

	private void BuildUi()
	{
		DeleteChildren( true );

		var header = new Panel { Parent = this };
		header.AddClass( "media-player-header" );
		new Label( "Media Player" ) { Parent = header }.AddClass( "media-player-heading" );
		statusLabel = new Label { Parent = header };
		statusLabel.AddClass( "media-player-status" );

		var stage = new Panel { Parent = this };
		stage.AddClass( "media-player-stage" );

		var videoPanel = new MediaDropZone( AddDraggedMediaFile ) { Parent = stage };
		videoPanel.AddClass( "media-player-video" );
		var scanline = new Panel { Parent = videoPanel };
		scanline.AddClass( "media-player-video-scanline" );
		nowPlayingLabel = new Label { Parent = videoPanel };
		nowPlayingLabel.AddClass( "media-player-video-title" );
		videoMeta = new Panel { Parent = videoPanel };
		videoMeta.AddClass( "media-player-video-meta" );
		new Label( "Drop files from Pane Explorer here" ) { Parent = videoMeta };
		new Label( "Playlist and video preview share the same queue" ) { Parent = videoMeta };

		playlistSection = new Panel { Parent = stage };
		playlistSection.AddClass( "media-player-playlist-section" );
		playlistSection.SetClass( "hidden", !showPlaylist );

		var playlistHeader = new Panel { Parent = playlistSection };
		playlistHeader.AddClass( "media-player-playlist-header" );
		new Label( "Playlist" ) { Parent = playlistHeader };
		repeatLabel = new Label { Parent = playlistHeader };
		repeatLabel.AddClass( "media-player-playlist-mode" );
		shuffleLabel = new Label { Parent = playlistHeader };
		shuffleLabel.AddClass( "media-player-playlist-mode" );

		playlistHost = new Panel { Parent = playlistSection };
		playlistHost.AddClass( "media-player-playlist" );

		var progressBar = new Panel { Parent = this };
		progressBar.AddClass( "media-player-progress" );
		progressFill = new Panel { Parent = progressBar };
		progressFill.AddClass( "media-player-progress-fill" );

		var controls = new Panel { Parent = this };
		controls.AddClass( "media-player-controls" );
		CreateButton( controls, "Prev", PreviousTrack );
		CreateButton( controls, "Play/Pause", TogglePlay );
		CreateButton( controls, "Next", NextTrack );
		CreateButton( controls, showPlaylist ? "Hide Playlist" : "Playlist", TogglePlaylist );
		CreateButton( controls, $"Repeat: {repeatMode}", CycleRepeatMode );
		CreateButton( controls, shuffleEnabled ? "Shuffle On" : "Shuffle Off", ToggleShuffle );
	}

	private void CreateButton( Panel parent, string text, Action onClick )
	{
		var button = new Button( text ) { Parent = parent };
		button.AddClass( "media-player-button" );
		button.AddEventListener( "onclick", onClick );
	}

	private void TogglePlay()
	{
		isPlaying = !isPlaying;
		PersistSessionState();
		UpdateDisplay();
	}

	private void PreviousTrack()
	{
		var playlist = GetActivePlaylist();
		trackIndex = playlist.Count == 0 ? 0 : (trackIndex + playlist.Count - 1) % playlist.Count;
		progress = 0f;
		PersistSessionState();
		UpdateDisplay();
	}

	private void NextTrack()
	{
		trackIndex = ComputerMediaPlaylistPolicy.ResolveNextIndex( trackIndex, GetActivePlaylist().Count, repeatMode );
		if ( trackIndex < 0 )
			trackIndex = 0;
		progress = 0f;
		PersistSessionState();
		UpdateDisplay();
	}

	private void UpdateDisplay()
	{
		var playlist = GetActivePlaylist();
		var trackTitle = playlist.Count == 0 ? "No media loaded" : FormatTrackTitle( playlist[Math.Clamp( trackIndex, 0, playlist.Count - 1 )] );
		statusLabel.Text = $"{trackTitle} {(isPlaying ? "Playing" : "Paused")}";
		nowPlayingLabel.Text = trackTitle;
		repeatLabel.Text = $"Repeat {repeatMode}";
		shuffleLabel.Text = shuffleEnabled ? "Shuffle enabled" : "Shuffle off";
		progressFill.Style.Width = Length.Percent( progress * 100f );
		playlistSection?.SetClass( "hidden", !showPlaylist );
		RebuildPlaylist();
	}

	private void TogglePlaylist()
	{
		showPlaylist = !showPlaylist;
		PersistSessionState();
		BuildUi();
		UpdateDisplay();
	}

	private void CycleRepeatMode()
	{
		repeatMode = repeatMode switch
		{
			ComputerMediaRepeatMode.Playlist => ComputerMediaRepeatMode.Single,
			ComputerMediaRepeatMode.Single => ComputerMediaRepeatMode.None,
			_ => ComputerMediaRepeatMode.Playlist
		};

		PersistSessionState();
		BuildUi();
		UpdateDisplay();
	}

	private void ToggleShuffle()
	{
		shuffleEnabled = !shuffleEnabled;
		trackIndex = 0;
		progress = 0f;
		PersistSessionState();
		BuildUi();
		UpdateDisplay();
	}

	private void AddDraggedMediaFile( string virtualPath )
	{
		if ( string.IsNullOrWhiteSpace( virtualPath ) )
			return;

		if ( !IsSupportedMediaPath( virtualPath ) )
			return;

		if ( basePlaylist.Contains( virtualPath, StringComparer.OrdinalIgnoreCase ) )
			return;

		basePlaylist.Add( virtualPath );
		showPlaylist = true;
		PersistSessionState();
		BuildUi();
		UpdateDisplay();
	}

	private void SelectTrack( int index )
	{
		trackIndex = Math.Clamp( index, 0, Math.Max( 0, GetActivePlaylist().Count - 1 ) );
		progress = 0f;
		isPlaying = true;
		PersistSessionState();
		UpdateDisplay();
	}

	private void RebuildPlaylist()
	{
		if ( playlistHost is null )
			return;

		playlistHost.DeleteChildren( true );
		var playlist = GetActivePlaylist();
		if ( playlist.Count == 0 )
		{
			new Label( "No files queued yet." ) { Parent = playlistHost }.AddClass( "media-player-empty" );
			return;
		}

		for ( var index = 0; index < playlist.Count; index++ )
		{
			var row = new Button { Parent = playlistHost };
			row.AddClass( "media-player-playlist-row" );
			row.SetClass( "active", index == trackIndex );
			var slot = index + 1;
			var itemPath = playlist[index];
			row.Text = $"{slot:00}  {FormatTrackTitle( itemPath )}";
			var capturedIndex = index;
			row.AddEventListener( "onclick", () => SelectTrack( capturedIndex ) );
		}
	}

	private IReadOnlyList<string> GetActivePlaylist()
	{
		if ( basePlaylist.Count == 0 )
			return Array.Empty<string>();

		return shuffleEnabled
			? ComputerMediaPlaylistPolicy.Shuffle( basePlaylist, context.State.InstanceId.GetHashCode() )
			: basePlaylist.ToArray();
	}

	private void SeedDefaultPlaylist()
	{
		var documentsRoot = context.GetDefaultDocumentsPath().TrimEnd( '/' );
		basePlaylist.Clear();
		basePlaylist.AddRange( new[]
		{
			$"{documentsRoot}/PaneOS Theme.mp3",
			$"{documentsRoot}/Startup Chime.wav",
			$"{documentsRoot}/Floppy Dreams.ogg"
		} );
	}

	private void RestoreSessionState()
	{
		showPlaylist = string.Equals( context.LoadValue( "showPlaylist" ), "1", StringComparison.Ordinal );
		shuffleEnabled = string.Equals( context.LoadValue( "shuffleEnabled" ), "1", StringComparison.Ordinal );
		isPlaying = !string.Equals( context.LoadValue( "isPaused" ), "1", StringComparison.Ordinal );
		trackIndex = int.TryParse( context.LoadValue( "trackIndex" ), out var parsedTrackIndex ) ? Math.Max( 0, parsedTrackIndex ) : 0;
		progress = float.TryParse( context.LoadValue( "trackProgress" ), out var parsedProgress ) ? Math.Clamp( parsedProgress, 0f, 1f ) : 0f;
		if ( Enum.TryParse<ComputerMediaRepeatMode>( context.LoadValue( "repeatMode" ), true, out var parsedRepeatMode ) )
			repeatMode = parsedRepeatMode;

		var persistedPlaylist = context.LoadValue( "playlist" );
		if ( !string.IsNullOrWhiteSpace( persistedPlaylist ) )
		{
			basePlaylist.Clear();
			basePlaylist.AddRange( persistedPlaylist.Split( '|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries ) );
		}
	}

	private void PersistSessionState()
	{
		context.SaveValue( "showPlaylist", showPlaylist ? "1" : "0" );
		context.SaveValue( "shuffleEnabled", shuffleEnabled ? "1" : "0" );
		context.SaveValue( "isPaused", isPlaying ? "0" : "1" );
		context.SaveValue( "trackIndex", trackIndex.ToString() );
		context.SaveValue( "trackProgress", progress.ToString( "0.###" ) );
		context.SaveValue( "repeatMode", repeatMode.ToString() );
		context.SaveValue( "playlist", string.Join( "|", basePlaylist ) );
	}

	private static bool IsSupportedMediaPath( string virtualPath )
	{
		var extension = Path.GetExtension( virtualPath );
		return extension.Equals( ".mp3", StringComparison.OrdinalIgnoreCase ) ||
			extension.Equals( ".wav", StringComparison.OrdinalIgnoreCase ) ||
			extension.Equals( ".ogg", StringComparison.OrdinalIgnoreCase ) ||
			extension.Equals( ".mp4", StringComparison.OrdinalIgnoreCase ) ||
			extension.Equals( ".avi", StringComparison.OrdinalIgnoreCase ) ||
			extension.Equals( ".webm", StringComparison.OrdinalIgnoreCase );
	}

	private static string FormatTrackTitle( string playlistItem )
	{
		var fileName = Path.GetFileNameWithoutExtension( playlistItem );
		return string.IsNullOrWhiteSpace( fileName ) ? playlistItem : fileName;
	}
}

public sealed class MediaDropZone : Panel
{
	private readonly Action<string> onDropped;

	public MediaDropZone( Action<string> onDropped )
	{
		this.onDropped = onDropped;
	}

	protected override void OnMouseUp( MousePanelEvent e )
	{
		base.OnMouseUp( e );
		var draggedPath = ComputerUiDragState.ConsumeDraggedVirtualPath();
		if ( string.IsNullOrWhiteSpace( draggedPath ) )
			return;

		onDropped( draggedPath );
	}
}
