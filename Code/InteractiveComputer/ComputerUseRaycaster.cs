using Sandbox;

namespace PaneOS.InteractiveComputer;

/// <summary>
/// Optional bridge for games that want a simple use-key trace into an in-world computer.
/// Attach to the player and set PlayerCamera. Custom controllers can call BeginInteraction directly instead.
/// </summary>
public sealed class ComputerUseRaycaster : Component
{
	[Property] public CameraComponent? PlayerCamera { get; set; }
	[Property] public float UseDistance { get; set; } = 96f;
	[Property] public string UseButton { get; set; } = "use";

	protected override void OnUpdate()
	{
		if ( PlayerCamera is null || !Input.Pressed( UseButton ) )
			return;

		var start = PlayerCamera.WorldPosition;
		var end = start + PlayerCamera.WorldRotation.Forward * UseDistance;
		var trace = Scene.Trace.Ray( start, end ).Run();
		var computer = trace.GameObject?.Components.Get<InteractiveComputerComponent>();

		computer?.ToggleInteraction( GameObject );
	}
}
