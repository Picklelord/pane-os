using System;
using System.Collections.Generic;
using System.Linq;

namespace PaneOS.InteractiveComputer.Core;

public sealed class DesktopShortcutLayoutItem
{
	public string Id { get; init; } = "";
	public int Index { get; init; }
}

public readonly record struct DesktopShortcutLayoutPoint( float X, float Y );

public readonly record struct DesktopShortcutSelectionRect( float Left, float Top, float Right, float Bottom )
{
	public static DesktopShortcutSelectionRect FromCorners( float xA, float yA, float xB, float yB )
	{
		return new DesktopShortcutSelectionRect(
			MathF.Min( xA, xB ),
			MathF.Min( yA, yB ),
			MathF.Max( xA, xB ),
			MathF.Max( yA, yB ) );
	}
}

public static class DesktopShortcutLayoutPolicy
{
	public const float OriginX = 14f;
	public const float OriginY = 12f;
	public const float ShortcutWidth = 82f;
	public const float ShortcutHeight = 76f;
	public const float HorizontalSpacing = 94f;
	public const float VerticalSpacing = 90f;
	public const float BottomPadding = 40f;

	public static DesktopShortcutLayoutPoint GetPosition( int index, int resolutionY )
	{
		var rows = Math.Max( 1, (int)((resolutionY - OriginY - BottomPadding) / VerticalSpacing) );
		var column = index / rows;
		var row = index % rows;
		return new DesktopShortcutLayoutPoint(
			OriginX + column * HorizontalSpacing,
			OriginY + row * VerticalSpacing );
	}

	public static bool HitTest( int index, int resolutionY, float x, float y )
	{
		var position = GetPosition( index, resolutionY );
		return x >= position.X &&
			x <= position.X + ShortcutWidth &&
			y >= position.Y &&
			y <= position.Y + ShortcutHeight;
	}

	public static IReadOnlyList<string> SelectIntersectingShortcutIds( IEnumerable<DesktopShortcutLayoutItem> shortcuts, DesktopShortcutSelectionRect selection, int resolutionY )
	{
		return shortcuts
			.Where( shortcut =>
			{
				var position = GetPosition( shortcut.Index, resolutionY );
				return position.X <= selection.Right &&
					position.X + ShortcutWidth >= selection.Left &&
					position.Y <= selection.Bottom &&
					position.Y + ShortcutHeight >= selection.Top;
			} )
			.Select( shortcut => shortcut.Id )
			.ToArray();
	}
}
