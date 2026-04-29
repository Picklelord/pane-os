using Sandbox;

namespace PaneOS.InteractiveComputer.Display;

/// <summary>
/// Convenience component for TV/monitor prefabs. Add it to the model root, assign the computer,
/// and it will ensure the RT Screens bridge is present and configured.
/// </summary>
public sealed class PaneOSMonitorSetup : Component
{
	[Property] public InteractiveComputerComponent? Computer { get; set; }
	[Property] public GameObject? DisplayObject { get; set; }
	[Property] public string ScreenId { get; set; } = "";
	[Property] public bool SetupOnStart { get; set; } = true;

	public PaneOSRtScreenBridge? Bridge { get; private set; }

	protected override void OnStart()
	{
		base.OnStart();

		if ( SetupOnStart )
			Setup();
	}

	public void Setup()
	{
		var target = DisplayObject ?? GameObject;
		var bridge = target.Components.Get<PaneOSRtScreenBridge>( FindMode.InSelf );
		if ( bridge is null )
			bridge = target.Components.Create<PaneOSRtScreenBridge>();

		bridge.Computer = Computer ?? target.Components.Get<InteractiveComputerComponent>( FindMode.InSelf | FindMode.InAncestors | FindMode.InDescendants );
		bridge.DisplayObject = target;
		bridge.ScreenId = ScreenId;
		bridge.Setup();
		Bridge = bridge;
	}
}
