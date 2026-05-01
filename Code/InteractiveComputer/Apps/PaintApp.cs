using System;
using Sandbox;
using Sandbox.UI;
using PaneOS.InteractiveComputer.Core;

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
public sealed class PaintPanel : ComputerWarmupPanel
{
	private PaintCanvas canvas = null!;
	private int brushSize = 16;

	public PaintPanel()
	{
		AddClass( "paint-app" );
		BuildUi();
	}

	protected override void WarmupRefresh()
	{
		BuildUi();
	}

	private void BuildUi()
	{
		DeleteChildren( true );

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
		var smallerBrushButton = new Button( "-" ) { Parent = toolbar };
		smallerBrushButton.AddClass( "paint-clear" );
		smallerBrushButton.AddEventListener( "onclick", () => SetBrushSize( brushSize - 2 ) );
		var largerBrushButton = new Button( "+" ) { Parent = toolbar };
		largerBrushButton.AddClass( "paint-clear" );
		largerBrushButton.AddEventListener( "onclick", () => SetBrushSize( brushSize + 2 ) );
		var sizeLabel = new Label( $"Brush {brushSize}px" ) { Parent = toolbar };
		sizeLabel.AddClass( "paint-size-label" );

		canvas = new PaintCanvas { Parent = this };
		canvas.AddClass( "paint-canvas" );
		canvas.BrushSize = brushSize;
		clearButton.AddEventListener( "onclick", canvas.ClearDots );
	}

	private void SetBrushSize( int nextSize )
	{
		brushSize = Math.Clamp( nextSize, 3, 36 );
		BuildUi();
	}
}

public sealed class PaintCanvas : Panel
{
	private bool isPainting;
	private Vector2? lastPaintPosition;

	public string CurrentColor { get; set; } = "#1982c4";
	public int BrushSize { get; set; } = 16;

	protected override void OnMouseDown( MousePanelEvent e )
	{
		base.OnMouseDown( e );
		var position = GetCanvasMousePosition( e );
		if ( !IsInsideCanvas( position ) )
			return;

		isPainting = true;
		lastPaintPosition = position;
		StampDot( position );
	}

	protected override void OnMouseMove( MousePanelEvent e )
	{
		base.OnMouseMove( e );

		if ( !isPainting )
			return;

		var position = GetCanvasMousePosition( e );
		if ( !IsInsideCanvas( position ) )
		{
			isPainting = false;
			lastPaintPosition = null;
			return;
		}

		foreach ( var point in ComputerPaintStrokePolicy.BuildStampPositions( lastPaintPosition, position, MathF.Max( 2f, BrushSize * 0.35f ) ) )
		{
			StampDot( point );
		}

		lastPaintPosition = position;
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
		if ( !IsInsideCanvas( position ) )
			return;

		var dot = new Panel { Parent = this };
		dot.AddClass( "paint-dot" );
		var radius = BrushSize * 0.5f;
		dot.Style.Left = Length.Pixels( position.x - radius );
		dot.Style.Top = Length.Pixels( position.y - radius );
		dot.Style.Width = Length.Pixels( BrushSize );
		dot.Style.Height = Length.Pixels( BrushSize );
		dot.Style.BackgroundColor = Color.Parse( CurrentColor );
	}

	private bool IsInsideCanvas( Vector2 position )
	{
		var rect = Box.Rect;
		return position.x >= 0f &&
			position.y >= 0f &&
			position.x <= rect.Width &&
			position.y <= rect.Height;
	}

	private Vector2 GetCanvasMousePosition( MousePanelEvent e )
	{
		var screenPosition = e.Target is not null
			? e.Target.PanelPositionToScreenPosition( e.LocalPosition )
			: e.LocalPosition;
		return ScreenPositionToPanelPosition( screenPosition );
	}
}
