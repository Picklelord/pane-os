using System;

namespace PaneOS.InteractiveComputer.Core;

public readonly record struct ComputerWindowBounds( int X, int Y, int Width, int Height );

public static class ComputerWindowLayoutPolicy
{
	public static ComputerWindowBounds ResolveInitialBounds( ComputerAppDescriptor descriptor, int resolutionX, int resolutionY, int visibleWindowCount )
	{
		return ResolveInitialBounds( descriptor, resolutionX, resolutionY, visibleWindowCount, descriptor.DefaultWindowWidth, descriptor.DefaultWindowHeight );
	}

	public static ComputerWindowBounds ResolveInitialBounds( ComputerAppDescriptor descriptor, int resolutionX, int resolutionY, int visibleWindowCount, int? defaultWidthOverride, int? defaultHeightOverride )
	{
		var cascadeOffset = Math.Max( 0, visibleWindowCount ) * 22;
		var width = defaultWidthOverride ?? Math.Min( 620, Math.Max( 420, resolutionX - 150 ) );
		var height = defaultHeightOverride ?? Math.Min( 420, Math.Max( 280, resolutionY - 170 ) );
		return new ComputerWindowBounds(
			descriptor.DefaultWindowOffsetX + cascadeOffset,
			descriptor.DefaultWindowOffsetY + cascadeOffset,
			Math.Clamp( width, 260, Math.Max( 260, resolutionX ) ),
			Math.Clamp( height, 180, Math.Max( 180, resolutionY ) ) );
	}
}
