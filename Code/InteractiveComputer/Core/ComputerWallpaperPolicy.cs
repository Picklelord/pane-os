using System;

namespace PaneOS.InteractiveComputer.Core;

public static class ComputerWallpaperPolicy
{
	public static string GetBackgroundStyle( string? wallpaper )
	{
		return Normalize( wallpaper ) switch
		{
			"blue" => "background: linear-gradient(180deg, #8ec5ff 0%, #3876d6 45%, #123a7e 100%);",
			"sunset" => "background: linear-gradient(180deg, #ffd7a6 0%, #f58d66 46%, #81386f 100%);",
			_ => "background: linear-gradient(180deg, #2c7cb7 0%, #1b5f98 40%, #0b3868 100%);"
		};
	}

	public static string Normalize( string? wallpaper )
	{
		if ( string.IsNullOrWhiteSpace( wallpaper ) )
			return "default";

		var normalized = wallpaper.Trim().ToLowerInvariant();
		return normalized is "blue" or "sunset" ? normalized : "default";
	}
}
