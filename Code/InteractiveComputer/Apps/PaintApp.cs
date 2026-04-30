using System;
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

[StyleSheet( "InteractiveComputerApps.scss" )]
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
	private bool isPainting;
	private Vector2? lastPaintPosition;

	public string CurrentColor { get; set; } = "#1982c4";

	protected override void OnMouseDown( MousePanelEvent e )
	{
		base.OnMouseDown( e );
		isPainting = true;
		lastPaintPosition = e.LocalPosition;
		StampDot( e.LocalPosition );
	}

	protected override void OnMouseMove( MousePanelEvent e )
	{
		base.OnMouseMove( e );

		if ( !isPainting )
			return;

		if ( lastPaintPosition.HasValue )
		{
			var delta = e.LocalPosition - lastPaintPosition.Value;
			var distance = delta.Length;
			var steps = Math.Max( 1, (int)(distance / 6f) );
			for ( var step = 1; step <= steps; step++ )
			{
				var t = step / (float)steps;
				StampDot( lastPaintPosition.Value + delta * t );
			}
		}
		else
		{
			StampDot( e.LocalPosition );
		}

		lastPaintPosition = e.LocalPosition;
	}

	protected override void OnMouseUp( MousePanelEvent e )
	{
		base.OnMouseUp( e );
		isPainting = false;
		lastPaintPosition = null;
	}

	public void ClearDots()
	{
		DeleteChildren( true );
	}

	private void StampDot( Vector2 position )
	{
		var dot = new Panel { Parent = this };
		dot.AddClass( "paint-dot" );
		dot.Style.Left = Length.Pixels( position.x - 8f );
		dot.Style.Top = Length.Pixels( position.y - 8f );
		dot.Style.BackgroundColor = Color.Parse( CurrentColor );
	}
}
