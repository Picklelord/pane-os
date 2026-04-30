using System;
using Sandbox.UI;

namespace PaneOS.InteractiveComputer.Apps;

[StyleSheet( "InteractiveComputerApps.scss" )]
public sealed class ComputerInputAwareTextEntry : TextEntry
{
	private readonly Func<bool> shouldSuppressInput;
	private bool shouldRestoreFocusWhenUnblocked;
	private bool wasBlockedLastTick;

	public ComputerInputAwareTextEntry( Func<bool> shouldSuppressInput )
	{
		this.shouldSuppressInput = shouldSuppressInput;
	}

	public override void Tick()
	{
		base.Tick();

		var isBlocked = shouldSuppressInput();
		Disabled = isBlocked;

		if ( isBlocked )
		{
			if ( HasFocus )
			{
				shouldRestoreFocusWhenUnblocked = true;
				Blur();
			}

			wasBlockedLastTick = true;
			return;
		}

		if ( wasBlockedLastTick && shouldRestoreFocusWhenUnblocked )
		{
			Focus();
			shouldRestoreFocusWhenUnblocked = false;
		}

		wasBlockedLastTick = false;
	}
}
