using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PaneOS.InteractiveComputer.Core;

public sealed class ComputerFileLaunchTarget
{
	public string AppId { get; init; } = "";
	public IReadOnlyDictionary<string, string> InitialData { get; init; } = new Dictionary<string, string>();
}

public static class ComputerFileAssociationPolicy
{
	public static ComputerFileLaunchTarget? ResolveLaunchTarget( string virtualPath, string fileName, string fileContent, IReadOnlyList<ComputerAppDescriptor> apps )
	{
		var extension = Path.GetExtension( fileName ).ToLowerInvariant();
		return extension switch
		{
			".txt" => new ComputerFileLaunchTarget
			{
				AppId = "system.notepad",
				InitialData = new Dictionary<string, string>
				{
					["file_path"] = virtualPath
				}
			},
			".url" => new ComputerFileLaunchTarget
			{
				AppId = "system.ridge",
				InitialData = new Dictionary<string, string>
				{
					["url"] = ResolveUrlFileTarget( fileContent )
				}
			},
			".exe" => ResolveExecutableTarget( fileName, apps ),
			_ => null
		};
	}

	public static string ResolveUrlFileTarget( string fileContent )
	{
		if ( string.IsNullOrWhiteSpace( fileContent ) )
			return "paneos://default";

		foreach ( var line in fileContent.Split( new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries ) )
		{
			if ( line.StartsWith( "url=", StringComparison.OrdinalIgnoreCase ) )
				return line["url=".Length..].Trim();
		}

		return fileContent.Trim();
	}

	private static ComputerFileLaunchTarget? ResolveExecutableTarget( string fileName, IReadOnlyList<ComputerAppDescriptor> apps )
	{
		var app = apps.FirstOrDefault( x => x.ResolvedExecutableName.Equals( fileName, StringComparison.OrdinalIgnoreCase ) );
		if ( app is null )
			return null;

		return new ComputerFileLaunchTarget
		{
			AppId = app.Id
		};
	}
}
