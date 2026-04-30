using System;
using System.Collections.Generic;
using System.Linq;

namespace PaneOS.InteractiveComputer.Core;

public static class ComputerFileDialogPolicy
{
	public static bool AllowsExtension( ComputerFileDialogOptions options, string extension )
	{
		if ( options.AllowedExtensions.Count == 0 )
			return true;

		var normalizedExtension = extension.Trim().TrimStart( '.' ).ToLowerInvariant();
		return options.AllowedExtensions.Any( x => x.Trim().TrimStart( '.' ).Equals( normalizedExtension, StringComparison.OrdinalIgnoreCase ) );
	}

	public static string ResolvePath( ComputerFileDialogOptions options, IReadOnlyList<string> currentPathSegments, string selectedVirtualPath, string currentFileName )
	{
		if ( options.Mode == ComputerFileDialogMode.Open )
			return selectedVirtualPath;

		var fileName = (currentFileName ?? "").Trim();
		if ( string.IsNullOrWhiteSpace( fileName ) )
			return "";

		return "/" + string.Join( "/", currentPathSegments.Append( fileName ) );
	}
}
