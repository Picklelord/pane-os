using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox;
using Sandbox.UI;
using PaneOS.InteractiveComputer;
using PaneOS.InteractiveComputer.Core;

namespace PaneOS.InteractiveComputer.Apps;

[ComputerApp( "system.ridge", "Ridge", Icon = "RG", SortOrder = 15 )]
public sealed class RidgeBrowserApp : IComputerApp
{
	public ComputerAppSession Run( ComputerAppContext context )
	{
		return new ComputerAppSession
		{
			Title = "Ridge",
			Icon = "RG",
			Content = new RidgeBrowserPanel( context )
		};
	}
}

[StyleSheet( "Code/InteractiveComputer/Apps/InteractiveComputerApps.scss" )]
public sealed class RidgeBrowserPanel : Panel
{
	private readonly ComputerAppContext context;
	private readonly TextEntry addressBar;
	private readonly Panel contentHost;
	private readonly Label statusLabel;
	private string currentUrl;
	private string? renderedUrl;
	private RidgePolicyResult pageState = new();

	public RidgeBrowserPanel( ComputerAppContext context )
	{
		this.context = context;
		AddClass( "ridge-app" );

		currentUrl = context.LoadValue( "url" ) ?? context.LoadSetting( "home_url" ) ?? "paneos://home";

		var toolbar = new Panel { Parent = this };
		toolbar.AddClass( "ridge-toolbar" );

		var backButton = new Button( "Back" ) { Parent = toolbar };
		backButton.AddClass( "ridge-button" );
		backButton.AddEventListener( "onclick", GoHome );

		addressBar = new TextEntry
		{
			Parent = toolbar,
			Text = currentUrl
		};
		addressBar.AddClass( "ridge-address" );

		var goButton = new Button( "Go" ) { Parent = toolbar };
		goButton.AddClass( "ridge-button ridge-go" );
		goButton.AddEventListener( "onclick", NavigateFromAddressBar );

		statusLabel = new Label { Parent = this };
		statusLabel.AddClass( "ridge-status" );

		contentHost = new Panel { Parent = this };
		contentHost.AddClass( "ridge-content" );

		Navigate( currentUrl );
	}

	public override void Tick()
	{
		base.Tick();

		if ( addressBar.HasFocus && Input.Pressed( "enter" ) )
			NavigateFromAddressBar();
	}

	private void GoHome()
	{
		Navigate( context.LoadSetting( "home_url" ) ?? "paneos://home" );
	}

	private void NavigateFromAddressBar()
	{
		Navigate( addressBar.Text );
	}

	private void Navigate( string rawUrl )
	{
		pageState = ResolvePageState( rawUrl );
		currentUrl = pageState.NormalizedUrl;
		addressBar.Text = currentUrl;
		context.SaveValue( "url", currentUrl );
		context.SaveValue( "last_visited_at", DateTime.UtcNow.ToString( "O" ) );

		RenderPage();
	}

	private void RenderPage()
	{
		contentHost.DeleteChildren( true );
		statusLabel.Text = pageState.Status;

		if ( pageState.CanRenderWebPanel )
		{
			var webPanel = new WebPanel
			{
				Parent = contentHost,
				Url = currentUrl
			};
			webPanel.AddClass( "ridge-webpanel" );
			renderedUrl = currentUrl;
			return;
		}

		renderedUrl = null;
		var message = new Panel { Parent = contentHost };
		message.AddClass( $"ridge-message {pageState.MessageClass}" );

		new Label( pageState.Title ) { Parent = message }.AddClass( "ridge-message-title" );
		new Label( pageState.Body ) { Parent = message }.AddClass( "ridge-message-body" );

		if ( pageState.AllowedHosts.Count > 0 )
		{
			var list = new Panel { Parent = message };
			list.AddClass( "ridge-allow-list" );
			new Label( "Allowed hosts" ) { Parent = list }.AddClass( "ridge-allow-title" );

			foreach ( var host in pageState.AllowedHosts )
			{
				new Label( host ) { Parent = list }.AddClass( "ridge-allow-host" );
			}
		}
	}

	private RidgePolicyResult ResolvePageState( string url )
	{
		return RidgeBrowserPolicy.Evaluate(
			url,
			context.LoadSetting( "web_rendering_enabled" ),
			context.LoadSetting( "allowed_hosts" ) );
	}
}
