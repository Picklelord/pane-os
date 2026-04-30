using System;
using Sandbox;
using Sandbox.UI;

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
			Content = new MediaPlayerPanel()
		};
	}
}

[StyleSheet( "Code/InteractiveComputer/Apps/InteractiveComputerApps.scss" )]
public sealed class MediaPlayerPanel : Panel
{
	private readonly Label statusLabel;
	private readonly Panel progressFill;
	private readonly string[] playlist = { "PaneOS Theme", "Startup Chime", "Floppy Dreams" };
	private int trackIndex;
	private float progress;
	private bool isPlaying = true;

	public MediaPlayerPanel()
	{
		AddClass( "media-player-app" );
		new Label( "Now Playing" ) { Parent = this }.AddClass( "media-player-heading" );
		statusLabel = new Label { Parent = this };
		statusLabel.AddClass( "media-player-status" );

		var progressBar = new Panel { Parent = this };
		progressBar.AddClass( "media-player-progress" );
		progressFill = new Panel { Parent = progressBar };
		progressFill.AddClass( "media-player-progress-fill" );

		var controls = new Panel { Parent = this };
		controls.AddClass( "media-player-controls" );
		CreateButton( controls, "Prev", PreviousTrack );
		CreateButton( controls, "Play/Pause", TogglePlay );
		CreateButton( controls, "Next", NextTrack );

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

	private void CreateButton( Panel parent, string text, Action onClick )
	{
		var button = new Button( text ) { Parent = parent };
		button.AddClass( "media-player-button" );
		button.AddEventListener( "onclick", onClick );
	}

	private void TogglePlay()
	{
		isPlaying = !isPlaying;
		UpdateDisplay();
	}

	private void PreviousTrack()
	{
		trackIndex = (trackIndex + playlist.Length - 1) % playlist.Length;
		progress = 0f;
		UpdateDisplay();
	}

	private void NextTrack()
	{
		trackIndex = (trackIndex + 1) % playlist.Length;
		progress = 0f;
		UpdateDisplay();
	}

	private void UpdateDisplay()
	{
		statusLabel.Text = $"{playlist[trackIndex]} {(isPlaying ? "Playing" : "Paused")}";
		progressFill.Style.Width = Length.Percent( progress * 100f );
	}
}
