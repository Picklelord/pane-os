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
			_ => "background-image: url(\"/Assets/textures/desktopbackground.png\"); background-size: cover; background-position: center; background-repeat: no-repeat; background-color: #2c7cb7;"
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
