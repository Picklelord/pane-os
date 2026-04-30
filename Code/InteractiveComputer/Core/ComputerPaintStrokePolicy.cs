using System;
using System.Collections.Generic;
using Sandbox;

namespace PaneOS.InteractiveComputer.Core;

public static class ComputerPaintStrokePolicy
{
	public static IReadOnlyList<Vector2> BuildStampPositions( Vector2? lastPosition, Vector2 currentPosition, float spacing )
	{
		var points = new List<Vector2>();
		var safeSpacing = MathF.Max( 1f, spacing );

		if ( !lastPosition.HasValue )
		{
			points.Add( currentPosition );
			return points;
		}

		var start = lastPosition.Value;
		var delta = currentPosition - start;
		var distance = delta.Length;
		var steps = Math.Max( 1, (int)MathF.Ceiling( distance / safeSpacing ) );
		for ( var step = 1; step <= steps; step++ )
		{
			var t = step / (float)steps;
			points.Add( start + delta * t );
		}

		return points;
	}
}
