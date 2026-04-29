using System;
using System.Linq;
using Sandbox;

namespace PaneOS.InteractiveComputer.Display;

/// <summary>
/// Creates the PaneOS world-panel/camera side of an in-world computer display.
/// s&amp;box's whitelist blocks reflection, so RT Screens package components should be assigned/configured in the editor.
/// </summary>
public sealed class PaneOSRtScreenBridge : Component
{
	[Property] public InteractiveComputerComponent? Computer { get; set; }
	[Property] public GameObject? DisplayObject { get; set; }
	[Property] public bool CreatePaneOSScreenChild { get; set; } = true;
	[Property] public string ScreenChildName { get; set; } = "PaneOS Screen";
	[Property] public bool EnsureWorldPanel { get; set; } = true;

	[Property] public string ScreenId { get; set; } = "";
	[Property] public bool AutoGenerateScreenId { get; set; } = true;
	[Property] public bool CreateCameraSource { get; set; } = true;
	[Property] public string CameraChildName { get; set; } = "PaneOS RT Camera";
	[Property] public bool ConfigureEveryStart { get; set; } = true;
	[Property] public Component? RtScreenComponent { get; set; }
	public GameObject? PaneOSScreenObject { get; private set; }
	public CameraComponent? SourceCamera { get; private set; }
	public Texture? RenderTarget { get; private set; }

	protected override void OnStart()
	{
		base.OnStart();
		Setup();
	}

	protected override void OnValidate()
	{
		base.OnValidate();

		if ( ConfigureEveryStart )
			Setup();
	}

	public void Setup()
	{
		var display = DisplayObject ?? GameObject;
		var computer = Computer ?? display.Components.Get<InteractiveComputerComponent>( FindMode.InSelf | FindMode.InAncestors | FindMode.InDescendants );

		if ( computer is null )
		{
			Log.Warning( $"PaneOS RT screen bridge on {GameObject.Name} has no computer assigned." );
			return;
		}

		Computer = computer;
		var screenObject = ResolvePaneOSScreenObject( display, computer );
		ResolveCameraSource( display, computer );
		WarnIfRtScreenNeedsManualSetup( screenObject );
	}

	private GameObject ResolvePaneOSScreenObject( GameObject display, InteractiveComputerComponent computer )
	{
		if ( !CreatePaneOSScreenChild )
		{
			PaneOSScreenObject = display;
			return display;
		}

		var existing = display.Children.FirstOrDefault( x => x.Name == ScreenChildName );
		var screen = existing ?? new GameObject( ScreenChildName );
		if ( existing is null )
			screen.SetParent( display );

		PaneOSScreenObject = screen;
		ConfigurePaneOSWorldPanel( screen, computer );

		var desktop = ResolvePaneOSDesktopComponent( screen );
		if ( desktop is not null )
		{
			desktop.Computer = computer;
			desktop.VisibleOnlyWhenInteracting = false;
		}

		return screen;
	}

	private void ConfigurePaneOSWorldPanel( GameObject screen, InteractiveComputerComponent computer )
	{
		if ( !EnsureWorldPanel )
			return;

		var worldPanel = screen.Components.Get<WorldPanel>( FindMode.InSelf );
		if ( worldPanel is null )
			worldPanel = screen.Components.Create<WorldPanel>();

		worldPanel.PanelSize = new Vector2( computer.Runtime.State.ResolutionX, computer.Runtime.State.ResolutionY );
		worldPanel.RenderScale = 1.0f;
		worldPanel.LookAtCamera = false;
		worldPanel.InteractionRange = 0.0f;
	}

	private CameraComponent? ResolveCameraSource( GameObject display, InteractiveComputerComponent computer )
	{
		if ( !CreateCameraSource )
			return null;

		var cameraObject = display.Children.FirstOrDefault( x => x.Name == CameraChildName );
		if ( cameraObject is null )
		{
			cameraObject = new GameObject( CameraChildName );
			cameraObject.SetParent( display );
		}

		var camera = cameraObject.Components.Get<CameraComponent>( FindMode.InSelf );
		if ( camera is null )
			camera = cameraObject.Components.Create<CameraComponent>();

		var targetSize = new Vector2( computer.Runtime.State.ResolutionX, computer.Runtime.State.ResolutionY );
		RenderTarget = Texture.CreateRenderTarget( ResolveScreenId( computer ), ImageFormat.RGBA8888, targetSize, camera.RenderTarget );
		camera.RenderTarget = RenderTarget;
		camera.CustomSize = targetSize;
		SourceCamera = camera;
		return camera;
	}

	private ComputerDesktop? ResolvePaneOSDesktopComponent( GameObject screen )
	{
		var desktop = screen.Components.Get<ComputerDesktop>( FindMode.InSelf );
		if ( desktop is not null )
			return desktop;

		return screen.Components.Create<ComputerDesktop>();
	}

	private void WarnIfRtScreenNeedsManualSetup( GameObject screenObject )
	{
		if ( RtScreenComponent is null )
			return;

		Log.Info( $"PaneOS generated screen '{screenObject.Name}', camera '{SourceCamera?.GameObject.Name}', render target '{ResolveScreenId( Computer! )}'. Assign those values to the RT Screens component in the editor." );
	}

	private string ResolveScreenId( InteractiveComputerComponent computer )
	{
		if ( !string.IsNullOrWhiteSpace( ScreenId ) )
			return ScreenId;

		var generated = $"paneos-{computer.ComputerId}";
		if ( AutoGenerateScreenId )
			ScreenId = generated;

		return generated;
	}
}
