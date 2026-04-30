using System;

namespace PaneOS.InteractiveComputer.Core;

public static class ComputerResolutionPolicy
{
	public static (int X, int Y) ResolveResolution( bool useGameSettingsResolution, int configuredWidth, int configuredHeight, float screenWidth, float screenHeight )
	{
		if ( useGameSettingsResolution )
		{
			return ((int)MathF.Max( 320f, screenWidth ), (int)MathF.Max( 240f, screenHeight ));
		}

		return (Math.Max( 320, configuredWidth ), Math.Max( 240, configuredHeight ));
	}
}
