using Sandbox;

namespace PaneOS.InteractiveComputer;

/// <summary>
/// Optional player-side helper that disables assigned movement/look components
/// while that player is interacting with a PaneOS computer.
/// </summary>
public sealed class ComputerInteractionPlayerLock : Component
{
	[Property] public Component? MovementComponent { get; set; }
	[Property] public Component? RotationComponent { get; set; }

	private bool? originalMovementEnabled;
	private bool? originalRotationEnabled;
	private bool wasLocked;

	protected override void OnUpdate()
	{
		var shouldLock = InteractiveComputerComponent.GetActiveComputerForPlayer( GameObject ) is not null;
		if ( shouldLock == wasLocked )
			return;

		ApplyLockState( shouldLock );
		wasLocked = shouldLock;
	}

	protected override void OnDisabled()
	{
		base.OnDisabled();

		if ( wasLocked )
		{
			ApplyLockState( false );
			wasLocked = false;
		}
	}

	private void ApplyLockState( bool shouldLock )
	{
		if ( MovementComponent is not null )
		{
			if ( shouldLock )
			{
				originalMovementEnabled ??= MovementComponent.Enabled;
				MovementComponent.Enabled = false;
			}
			else if ( originalMovementEnabled.HasValue )
			{
				MovementComponent.Enabled = originalMovementEnabled.Value;
				originalMovementEnabled = null;
			}
		}

		if ( RotationComponent is not null )
		{
			if ( shouldLock )
			{
				originalRotationEnabled ??= RotationComponent.Enabled;
				RotationComponent.Enabled = false;
			}
			else if ( originalRotationEnabled.HasValue )
			{
				RotationComponent.Enabled = originalRotationEnabled.Value;
				originalRotationEnabled = null;
			}
		}
	}
}
