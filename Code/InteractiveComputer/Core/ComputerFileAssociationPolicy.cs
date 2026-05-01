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

public sealed class ComputerFileOpenResult
{
	public ComputerFileLaunchTarget? LaunchTarget { get; init; }
	public string FailureTitle { get; init; } = "Cannot Open File";
	public string FailureMessage { get; init; } = "";
	public bool CanOpen => LaunchTarget is not null;
}

public static class ComputerFileAssociationPolicy
{
	public static ComputerFileLaunchTarget? ResolveLaunchTarget( string virtualPath, string fileName, string fileContent, IReadOnlyList<ComputerAppDescriptor> apps )
	{
		return ResolveOpenResult( virtualPath, fileName, fileContent, apps ).LaunchTarget;
	}

	public static ComputerFileOpenResult ResolveOpenResult( string virtualPath, string fileName, string fileContent, IReadOnlyList<ComputerAppDescriptor> apps )
	{
		var extension = Path.GetExtension( fileName ).ToLowerInvariant();
		if ( extension == ".url" )
			return ResolveUrlOpenResult( virtualPath, fileContent );

		if ( extension == ".exe" )
			return ResolveExecutableOpenResult( fileName, apps );

		if ( extension == ".txt" )
			return new ComputerFileOpenResult
			{
				LaunchTarget = new ComputerFileLaunchTarget
				{
					AppId = "system.notepad",
					InitialData = new Dictionary<string, string>
					{
						["file_path"] = virtualPath
					}
				}
			};

		var associatedApp = apps.FirstOrDefault( x => x.AssociatedFileExtensions.Any( y => NormalizeExtension( y ) == extension ) );
		if ( associatedApp is not null )
		{
			return new ComputerFileOpenResult
			{
				LaunchTarget = new ComputerFileLaunchTarget
				{
					AppId = associatedApp.Id,
					InitialData = new Dictionary<string, string>
					{
						["file_path"] = virtualPath
					}
				}
			};
		}

		return new ComputerFileOpenResult
		{
			FailureTitle = "Cannot Open File",
			FailureMessage = $"PaneOS does not know how to open {fileName}."
		};
	}

	public static string ResolveUrlFileTarget( string fileContent )
	{
		return TryResolveUrlFileTarget( fileContent, out var targetUrl ) ? targetUrl : "paneos://default";
	}

	public static bool TryResolveUrlFileTarget( string fileContent, out string targetUrl )
	{
		targetUrl = "";
		if ( string.IsNullOrWhiteSpace( fileContent ) )
			return false;

		foreach ( var line in fileContent.Split( new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries ) )
		{
			if ( line.StartsWith( "url=", StringComparison.OrdinalIgnoreCase ) )
			{
				var value = line["url=".Length..].Trim();
				if ( string.IsNullOrWhiteSpace( value ) )
					return false;

				targetUrl = value;
				return true;
			}
		}

		var fallback = fileContent.Trim();
		if ( string.IsNullOrWhiteSpace( fallback ) )
			return false;

		targetUrl = fallback;
		return true;
	}

	private static ComputerFileOpenResult ResolveUrlOpenResult( string virtualPath, string fileContent )
	{
		if ( !TryResolveUrlFileTarget( fileContent, out var urlTarget ) )
		{
			return new ComputerFileOpenResult
			{
				FailureTitle = "Corrupted Shortcut",
				FailureMessage = $"{Path.GetFileName( virtualPath )} appears to be empty or corrupted."
			};
		}

		return new ComputerFileOpenResult
		{
			LaunchTarget = new ComputerFileLaunchTarget
			{
				AppId = "system.ridge",
				InitialData = new Dictionary<string, string>
				{
					["url"] = urlTarget
				}
			}
		};
	}

	private static ComputerFileOpenResult ResolveExecutableOpenResult( string fileName, IReadOnlyList<ComputerAppDescriptor> apps )
	{
		var app = apps.FirstOrDefault( x => x.ResolvedExecutableName.Equals( fileName, StringComparison.OrdinalIgnoreCase ) );
		if ( app is not null )
		{
			return new ComputerFileOpenResult
			{
				LaunchTarget = new ComputerFileLaunchTarget
				{
					AppId = app.Id
				}
			};
		}

		return new ComputerFileOpenResult
		{
			FailureTitle = "Corrupted Application",
			FailureMessage = $"{fileName} points to an app that is missing or no longer installed."
		};
	}

	private static string NormalizeExtension( string extension )
	{
		if ( string.IsNullOrWhiteSpace( extension ) )
			return "";

		var value = extension.Trim().ToLowerInvariant();
		return value.StartsWith( ".", StringComparison.Ordinal ) ? value : $".{value}";
	}
}
