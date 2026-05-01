using System.Collections.Generic;
using Sandbox.UI;

namespace PaneOS.InteractiveComputer.Apps;

[ComputerApp( "system.about", "My Computer", Icon = "PC", SortOrder = 0 )]
public sealed class AboutComputerApp : IComputerApp
{
	public ComputerAppSession Run( ComputerAppContext context )
	{
		return new ComputerAppSession
		{
			Title = "About PC",
			Icon = "PC",
			Content = new AboutComputerPanel( context )
		};
	}
}

[StyleSheet( "InteractiveComputerApps.scss" )]
public sealed class AboutComputerPanel : ComputerWarmupPanel
{
	private sealed record CreditLink( string Label, string Url );

	private static readonly IReadOnlyList<CreditLink> Credits = new[]
	{
		new CreditLink( "Pane OS, created by Daniel Garnier", "https://github.com/Picklelord/pane-os" ),
		new CreditLink( "Video icons by riajulislam - Flaticon", "https://www.flaticon.com/free-icons/video" ),
		new CreditLink( "Radio icons by Freepik - Flaticon", "https://www.flaticon.com/free-icons/radio" ),
		new CreditLink( "Mp3 icons by Freepik - Flaticon", "https://www.flaticon.com/free-icons/mp3" ),
		new CreditLink( "Exe icons by Freepik - Flaticon", "https://www.flaticon.com/free-icons/exe" ),
		new CreditLink( "Document icons by Freepik - Flaticon", "https://www.flaticon.com/free-icons/document" ),
		new CreditLink( "Painting icons by Freepik - Flaticon", "https://www.flaticon.com/free-icons/painting" ),
		new CreditLink( "Seo and web icons by Freepik - Flaticon", "https://www.flaticon.com/free-icons/seo-and-web" ),
		new CreditLink( "Search icons by Freepik - Flaticon", "https://www.flaticon.com/free-icons/search" ),
		new CreditLink( "Notepad icons by Freepik - Flaticon", "https://www.flaticon.com/free-icons/notepad" ),
		new CreditLink( "Calculator icons by Freepik - Flaticon", "https://www.flaticon.com/free-icons/calculator" ),
		new CreditLink( "Checklist icons by Freepik - Flaticon", "https://www.flaticon.com/free-icons/checklist" ),
		new CreditLink( "Recycle bin icons by Freepik - Flaticon", "https://www.flaticon.com/free-icons/recycle-bin" ),
		new CreditLink( "Control panel icons by Freepik - Flaticon", "https://www.flaticon.com/free-icons/control-panel" ),
		new CreditLink( "Computer icons by Freepik - Flaticon", "https://www.flaticon.com/free-icons/computer" ),
		new CreditLink( "Folder icons by Freepik - Flaticon", "https://www.flaticon.com/free-icons/folder" ),
		new CreditLink( "Picture icons by Freepik - Flaticon", "https://www.flaticon.com/free-icons/picture" ),
		new CreditLink( "Film icons by Freepik - Flaticon", "https://www.flaticon.com/free-icons/film" )
	};

	private readonly ComputerAppContext context;
	private int lastVersion = -1;

	public AboutComputerPanel( ComputerAppContext context )
	{
		this.context = context;
		AddClass( "about-app app-document" );
		Rebuild();
	}

	public override void Tick()
	{
		base.Tick();

		if ( lastVersion == context.Runtime.Version )
			return;

		Rebuild();
	}

	protected override void WarmupRefresh()
	{
		Rebuild();
	}

	private void Rebuild()
	{
		lastVersion = context.Runtime.Version;
		DeleteChildren( true );

		var topSection = new Panel { Parent = this };
		topSection.AddClass( "about-top" );

		var summary = new Panel { Parent = topSection };
		summary.AddClass( "about-summary" );
		new Label( "About PC" ) { Parent = summary }.AddClass( "app-heading" );
		new Label( $"Computer ID: {context.Computer.ComputerId}" ) { Parent = summary };
		new Label( $"Resolution: {context.Runtime.State.ResolutionX} x {context.Runtime.State.ResolutionY}" ) { Parent = summary };

		var hero = new Panel { Parent = topSection };
		hero.AddClass( "about-hero" );
		var heroIcon = new Panel { Parent = hero };
		heroIcon.AddClass( "about-hero-icon" );
		ApplyAboutIcon( heroIcon );

		var hardware = context.Runtime.State.Hardware;
		var hardwarePanel = new Panel { Parent = this };
		hardwarePanel.AddClass( "about-grid" );

		AddMetric( hardwarePanel, "RAM", $"{hardware.RamGb:0.##} GB" );
		AddMetric( hardwarePanel, "CPU", $"{hardware.CpuCoreGhz:0.##} GHz" );
		AddMetric( hardwarePanel, "CPU cores", $"{hardware.CpuCoreCount}" );
		AddMetric( hardwarePanel, "GPU", $"{hardware.GpuCoreGhz:0.##} GHz" );
		AddMetric( hardwarePanel, "GPU VRAM", $"{hardware.GpuVramGb:0.##} GB" );
		AddMetric( hardwarePanel, "HDD", $"{hardware.HddStorageGb:0.##} GB" );
		AddMetric( hardwarePanel, "Internet", $"{hardware.InternetSpeedGbps:0.##} Gb/s" );

		var creditsPanel = new Panel { Parent = this };
		creditsPanel.AddClass( "about-credits" );
		new Label( "Credits" ) { Parent = creditsPanel }.AddClass( "about-credits-title" );

		foreach ( var credit in Credits )
		{
			var button = new Button( credit.Label ) { Parent = creditsPanel };
			button.AddClass( "about-credit-link" );
			button.AddEventListener( "onclick", () => OpenCreditLink( credit.Url ) );
		}
	}

	private void ApplyAboutIcon( Panel icon )
	{
		var texturePath = TryResolveTexturePath( "App_about" );
		if ( string.IsNullOrWhiteSpace( texturePath ) )
			return;

		icon.Style.SetBackgroundImage( texturePath );
	}

	private string TryResolveTexturePath( string textureName )
	{
		var themeName = string.IsNullOrWhiteSpace( context.Computer.ThemeName )
			? "default"
			: context.Computer.ThemeName.Trim();
		var path = $"textures/themes/{themeName}/{textureName}.png";
		try
		{
			return Sandbox.FileSystem.Mounted.FileExists( path ) ? path : "";
		}
		catch
		{
			return "";
		}
	}

	private void OpenCreditLink( string url )
	{
		context.Runtime.OpenApp( "system.ridge", new Dictionary<string, string>
		{
			["url"] = url
		} );
	}

	private static void AddMetric( Panel parent, string label, string value )
	{
		var row = new Panel { Parent = parent };
		row.AddClass( "about-row" );
		new Label( label ) { Parent = row }.AddClass( "about-label" );
		new Label( value ) { Parent = row }.AddClass( "about-value" );
	}
}
