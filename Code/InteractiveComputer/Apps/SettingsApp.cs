using System;
using System.Linq;
using Sandbox.UI;

namespace PaneOS.InteractiveComputer.Apps;

[ComputerApp( "system.settings", "Control Panel", Icon = "CP", SortOrder = 5 )]
public sealed class SettingsApp : IComputerApp
{
	public ComputerAppSession Run( ComputerAppContext context )
	{
		return new ComputerAppSession
		{
			Title = "Control Panel",
			Icon = "CP",
			Content = new SettingsPanel( context )
		};
	}
}

[StyleSheet( "InteractiveComputerApps.scss" )]
public sealed class SettingsPanel : ComputerWarmupPanel
{
	private readonly ComputerAppContext context;
	private int lastVersion = -1;

	public SettingsPanel( ComputerAppContext context )
	{
		this.context = context;
		AddClass( "settings-app app-document" );
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

		new Label( "Control Panel" ) { Parent = this }.AddClass( "app-heading" );
		BuildHardwareSection();
		BuildWallpaperSection();
		BuildBrowserSection();
		BuildSystemSection();
		BuildAppsSection();
	}

	private void BuildHardwareSection()
	{
		var section = CreateSection( "Hardware" );
		CreateSettingRow( section, $"Screen saver delay: {context.Runtime.State.ScreenSaver.DelaySeconds:0}s",
			("-15s", () => AdjustScreenSaverDelay( -15f )),
			("+15s", () => AdjustScreenSaverDelay( 15f )) );
	}

	private void BuildWallpaperSection()
	{
		var section = CreateSection( "Wallpaper" );
		CreateSettingRow( section, $"Current wallpaper: {context.Runtime.State.DesktopWallpaper}",
			("Default", () => SetWallpaper( "default" )),
			("Blue", () => SetWallpaper( "blue" )),
			("Sunset", () => SetWallpaper( "sunset" )) );
	}

	private void BuildBrowserSection()
	{
		var section = CreateSection( "Ridge" );
		var homeUrl = context.Runtime.LoadAppSetting( "system.ridge", "home_url" ) ?? "paneos://default";
		CreateSettingRow( section, $"Home page: {homeUrl}",
			("Poodle", () => SetRidgeHome( "paneos://default" )),
			("Blank", () => SetRidgeHome( "paneos://home" )),
			("Custom...", PromptForCustomHome) );
	}

	private void BuildAppsSection()
	{
		var section = CreateSection( "Apps" );
		foreach ( var app in ComputerAppRegistry.Apps.Where( x => !x.IsBackgroundProcess ).OrderBy( x => x.SortOrder ).ThenBy( x => x.Title, StringComparer.OrdinalIgnoreCase ) )
		{
			var installed = context.Runtime.IsAppInstalled( app.Id );
			CreateSettingRow(
				section,
				$"{app.Title} {(installed ? "(Installed)" : "(Not installed)")}",
				(installed ? "Uninstall" : "Install", () =>
				{
					if ( installed )
						context.Runtime.UninstallApp( app.Id );
					else
						context.Runtime.InstallApp( app.Id );
				}) );
		}
	}

	private void BuildSystemSection()
	{
		var section = CreateSection( "System" );
		CreateSettingRow( section, "PaneOS maintenance and install flows",
			("Check Updates", () => context.Runtime.RunSystemUpdateScan()),
			("Install Codec", () => context.Runtime.RunPackageInstall( "Media Codec Pack" )),
			("Install Tools", () => context.Runtime.RunPackageInstall( "Gamer Tools" )) );
		CreateSettingRow( section, $"Dark mode: {(context.Runtime.State.DarkModeEnabled ? "On" : "Off")}",
			(context.Runtime.State.DarkModeEnabled ? "Disable" : "Enable", ToggleDarkMode) );
	}

	private Panel CreateSection( string title )
	{
		new Label( title ) { Parent = this }.AddClass( "settings-section-title" );
		var section = new Panel { Parent = this };
		section.AddClass( "settings-section" );
		return section;
	}

	private static void CreateSettingRow( Panel section, string label, params (string Label, Action Handler)[] buttons )
	{
		var row = new Panel { Parent = section };
		row.AddClass( "settings-row" );
		new Label( label ) { Parent = row }.AddClass( "settings-row-label" );

		var actions = new Panel { Parent = row };
		actions.AddClass( "settings-actions" );
		foreach ( var buttonDef in buttons )
		{
			var button = new Button( buttonDef.Label ) { Parent = actions };
			button.AddClass( "settings-button" );
			button.AddEventListener( "onclick", buttonDef.Handler );
		}
	}

	private void AdjustScreenSaverDelay( float delta )
	{
		context.Runtime.State.ScreenSaver.DelaySeconds = MathF.Max( 5f, context.Runtime.State.ScreenSaver.DelaySeconds + delta );
		context.Runtime.MarkChanged();
	}

	private void SetWallpaper( string wallpaper )
	{
		context.Runtime.State.DesktopWallpaper = wallpaper;
		context.Runtime.MarkChanged();
	}

	private void SetRidgeHome( string url )
	{
		context.Runtime.SaveAppSetting( "system.ridge", "home_url", url );
	}

	private void ToggleDarkMode()
	{
		context.Runtime.State.DarkModeEnabled = !context.Runtime.State.DarkModeEnabled;
		context.Runtime.MarkChanged();
	}

	private void PromptForCustomHome()
	{
		context.ShowMessageBox(
			new ComputerMessageBoxOptions
			{
				Title = "Custom Home Page",
				Message = "Enter a URL or a local PaneOS address.",
				Icon = "RG",
				HasTextInput = true,
				TextInputValue = context.Runtime.LoadAppSetting( "system.ridge", "home_url" ) ?? "paneos://default",
				TextInputPlaceholder = "https://example.com or paneos://default",
				Buttons = new[] { "Save", "Cancel" }
			},
			result =>
			{
				if ( !result.ButtonPressed.Equals( "Save", StringComparison.OrdinalIgnoreCase ) )
					return;

				SetRidgeHome( string.IsNullOrWhiteSpace( result.TextValue ) ? "paneos://default" : result.TextValue.Trim() );
			} );
	}
}
