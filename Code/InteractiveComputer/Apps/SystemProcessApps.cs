using Sandbox.UI;

namespace PaneOS.InteractiveComputer.Apps;

public sealed class BackgroundProcessApp : IComputerApp
{
	private readonly string title;
	private readonly string icon;

	public BackgroundProcessApp( string title, string icon )
	{
		this.title = title;
		this.icon = icon;
	}

	public ComputerAppSession Run( ComputerAppContext context )
	{
		return new ComputerAppSession
		{
			Title = title,
			Icon = icon,
			CanMinimize = false,
			Content = new Panel()
		};
	}
}
