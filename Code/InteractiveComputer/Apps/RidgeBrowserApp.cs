using System;
using System.Collections.Generic;
using Sandbox;
using Sandbox.UI;
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
	private RidgePolicyResult pageState = new();

	public RidgeBrowserPanel( ComputerAppContext context )
	{
		this.context = context;
		AddClass( "ridge-app" );

		currentUrl = context.LoadValue( "url" ) ?? context.LoadSetting( "home_url" ) ?? "paneos://default";

		var toolbar = new Panel { Parent = this };
		toolbar.AddClass( "ridge-toolbar" );

		var homeButton = new Button( "Home" ) { Parent = toolbar };
		homeButton.AddClass( "ridge-button" );
		homeButton.AddEventListener( "onclick", GoHome );

		addressBar = new ComputerInputAwareTextEntry( () => context.Runtime.ShouldBlockInput( context.State.InstanceId ) )
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
		Navigate( context.LoadSetting( "home_url" ) ?? "paneos://default" );
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

		if ( IsLocalDefaultPage( currentUrl ) )
		{
			var localPage = new PoodleSearchPanel( context, OnPoodleSearch )
			{
				Parent = contentHost
			};
			localPage.AddClass( "ridge-local-page" );
			statusLabel.Text = "Poodle search ready";
			return;
		}

		if ( pageState.CanRenderWebPanel )
		{
			var webPanel = new WebPanel
			{
				Parent = contentHost,
				Url = currentUrl
			};
			webPanel.AddClass( "ridge-webpanel" );
			return;
		}

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

	private void OnPoodleSearch( string query )
	{
		context.SaveValue( "poodle_query", query );
		statusLabel.Text = string.IsNullOrWhiteSpace( query )
			? "Poodle search ready"
			: $"Poodle sniffed out results for \"{query}\"";
	}

	private RidgePolicyResult ResolvePageState( string url )
	{
		var normalized = RidgeBrowserPolicy.NormalizeUrl( string.IsNullOrWhiteSpace( url ) ? "paneos://default" : url );
		if ( IsLocalDefaultPage( normalized ) )
		{
			return new RidgePolicyResult
			{
				NormalizedUrl = "paneos://default",
				Status = "Poodle search ready",
				Title = "Poodle",
				Body = "A local search page"
			};
		}

		return RidgeBrowserPolicy.Evaluate(
			normalized,
			context.LoadSetting( "web_rendering_enabled" ),
			context.LoadSetting( "allowed_hosts" ) );
	}

	private static bool IsLocalDefaultPage( string url )
	{
		return url.Equals( "paneos://default", StringComparison.OrdinalIgnoreCase )
			|| url.Equals( "paneos://home", StringComparison.OrdinalIgnoreCase )
			|| url.Equals( "paneos://poodle", StringComparison.OrdinalIgnoreCase );
	}
}

public sealed class PoodleSearchPanel : Panel
{
	private readonly ComputerAppContext context;
	private readonly Action<string> onSearch;
	private readonly TextEntry searchEntry;
	private readonly Panel resultsHost;

	public PoodleSearchPanel( ComputerAppContext context, Action<string> onSearch )
	{
		this.context = context;
		this.onSearch = onSearch;
		AddClass( "poodle-page" );

		var logo = new Label( "Poodle" ) { Parent = this };
		logo.AddClass( "poodle-logo" );

		var tagline = new Label( "Search the local kennel." ) { Parent = this };
		tagline.AddClass( "poodle-tagline" );

		var searchRow = new Panel { Parent = this };
		searchRow.AddClass( "poodle-search-row" );

		searchEntry = new ComputerInputAwareTextEntry( () => context.Runtime.ShouldBlockInput( context.State.InstanceId ) )
		{
			Parent = searchRow,
			Text = context.LoadValue( "poodle_query" ) ?? "",
			Placeholder = "Search PaneOS"
		};
		searchEntry.AddClass( "poodle-input" );

		var searchButton = new Button( "Let loose the dogs!" ) { Parent = searchRow };
		searchButton.AddClass( "poodle-button" );
		searchButton.AddEventListener( "onclick", Search );

		resultsHost = new Panel { Parent = this };
		resultsHost.AddClass( "poodle-results" );

		RenderResults();
	}

	public override void Tick()
	{
		base.Tick();

		if ( searchEntry.HasFocus && Input.Pressed( "enter" ) )
			Search();
	}

	private void Search()
	{
		context.SaveValue( "poodle_query", searchEntry.Text );
		onSearch( searchEntry.Text );
		RenderResults();
	}

	private void RenderResults()
	{
		resultsHost.DeleteChildren( true );
		var query = context.LoadValue( "poodle_query" ) ?? "";

		if ( string.IsNullOrWhiteSpace( query ) )
		{
			new Label( "Poodle is waiting for a scent." ) { Parent = resultsHost }.AddClass( "poodle-empty" );
			return;
		}

		foreach ( var result in BuildResults( query ) )
		{
			var row = new Panel { Parent = resultsHost };
			row.AddClass( "poodle-result" );
			new Label( result.Title ) { Parent = row }.AddClass( "poodle-result-title" );
			new Label( result.Url ) { Parent = row }.AddClass( "poodle-result-url" );
			new Label( result.Body ) { Parent = row }.AddClass( "poodle-result-body" );
		}
	}

	private static IReadOnlyList<(string Title, string Url, string Body)> BuildResults( string query )
	{
		return new[]
		{
			($"Poodle result for {query}", $"paneos://search/{query.Replace( ' ', '-' ).ToLowerInvariant() }", $"The local dogs found the strongest scent trail for {query}."),
			($"Best of {query}", "paneos://apps", $"Try checking your apps, notes, and documents for {query}."),
			($"PaneOS knowledge: {query}", "paneos://help", $"No internet needed. Poodle keeps things local and playful.")
		};
	}
}
