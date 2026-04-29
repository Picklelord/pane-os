using Sandbox.UI;
using PaneOS.InteractiveComputer;

namespace PaneOS.InteractiveComputer.Apps;

[ComputerApp( "system.about", "My Computer", Icon = "PC", SortOrder = 0 )]
public sealed class AboutComputerApp : IComputerApp
{
	public ComputerAppSession Run( ComputerAppContext context )
	{
		return new ComputerAppSession
		{
			Title = "My Computer",
			Icon = "PC",
			Content = new AboutComputerPanel( context )
		};
	}
}

[StyleSheet( "Code/InteractiveComputer/Apps/InteractiveComputerApps.scss" )]
public sealed class AboutComputerPanel : Panel
{
	public AboutComputerPanel( ComputerAppContext context )
	{
		AddClass( "about-app app-document" );

		new Label( "System" )
		{
			Parent = this
		}.AddClass( "app-heading" );

		new Label( $"Computer ID: {context.Computer.ComputerId}" ) { Parent = this };
		new Label( $"Resolution: {context.Runtime.State.ResolutionX} x {context.Runtime.State.ResolutionY}" ) { Parent = this };
		new Label( $"Installed apps: {context.Runtime.State.InstalledApps.Count}" ) { Parent = this };
		new Label( $"Open apps: {context.Runtime.State.OpenApps.Count}" ) { Parent = this };
		new Label( "Register IComputerApp descriptors with ComputerAppRegistry to add desktop shortcuts." )
		{
			Parent = this
		}.AddClass( "app-note" );
	}
}
