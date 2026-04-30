using System;

namespace PaneOS.InteractiveComputer.Core;

public static class ComputerArchiveUserPolicy
{
	public static string ResolveInitialUserName( string? steamDisplayName, string? environmentUserName )
	{
		var normalizedSteamName = PaneArchiveFileSystem.NormalizeDisplayName( steamDisplayName );
		if ( !normalizedSteamName.Equals( "Player", StringComparison.OrdinalIgnoreCase ) )
			return normalizedSteamName;

		var normalizedEnvironmentName = PaneArchiveFileSystem.NormalizeDisplayName( environmentUserName );
		if ( !normalizedEnvironmentName.Equals( "Player", StringComparison.OrdinalIgnoreCase ) )
			return normalizedEnvironmentName;

		return normalizedSteamName;
	}
}
