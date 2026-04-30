namespace PaneOS.InteractiveComputer.Core;

public static class ComputerInputDelayPolicy
{
	public static bool ShouldSuppressFocusedAppInput( bool simulateCpuDelayWhenMaxed, bool isInputDelayed, bool isFocusedApp )
	{
		return simulateCpuDelayWhenMaxed && isInputDelayed && isFocusedApp;
	}
}
