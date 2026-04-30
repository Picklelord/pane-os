using Sandbox;
using Sandbox.UI;

namespace PaneOS.InteractiveComputer.Apps;

[ComputerApp( "system.paint", "Paint", Icon = "PT", SortOrder = 24 )]
public sealed class PaintApp : IComputerApp
{
	public ComputerAppSession Run( ComputerAppContext context )
	{
		return new ComputerAppSession
		{
			Title = "Paint",
			Icon = "PT",
			Content = new PaintPanel()
		};
	}
}

[StyleSheet( "Code/InteractiveComputer/Apps/InteractiveComputerApps.scss" )]
public sealed class PaintPanel : Panel
{
	private readonly PaintCanvas canvas;

	public PaintPanel()
	{
		AddClass( "paint-app" );

		var toolbar = new Panel { Parent = this };
		toolbar.AddClass( "paint-toolbar" );

		foreach ( var color in new[] { "#ff595e", "#ffca3a", "#8ac926", "#1982c4", "#6a4c93" } )
		{
			var button = new Button() { Parent = toolbar };
			button.AddClass( "paint-color-button" );
			button.Style.BackgroundColor = Color.Parse( color );
			button.AddEventListener( "onclick", () => canvas.CurrentColor = color );
		}

		var clearButton = new Button( "Clear" ) { Parent = toolbar };
		clearButton.AddClass( "paint-clear" );

		canvas = new PaintCanvas { Parent = this };
		canvas.AddClass( "paint-canvas" );
		clearButton.AddEventListener( "onclick", canvas.ClearDots );
	}
}

public sealed class PaintCanvas : Panel
{
	public string CurrentColor { get; set; } = "#1982c4";

	protected override void OnMouseDown( MousePanelEvent e )
	{
		base.OnMouseDown( e );
		var dot = new Panel { Parent = this };
		dot.AddClass( "paint-dot" );
		dot.Style.Left = Length.Pixels( e.LocalPosition.x - 8f );
		dot.Style.Top = Length.Pixels( e.LocalPosition.y - 8f );
		dot.Style.BackgroundColor = Color.Parse( CurrentColor );
	}

	public void ClearDots()
	{
		DeleteChildren( true );
	}
}
