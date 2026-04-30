using System;
using System.Text;

#if PANEOS_UNIT_TESTS
using System.IO;
#else
using Sandbox;
#endif

namespace PaneOS.InteractiveComputer.Core;

internal static class ComputerSandboxStorage
{
	public static string ResolveArchiveStoragePath( string computerId, string? configuredPath )
	{
		if ( !string.IsNullOrWhiteSpace( configuredPath ) )
		{
			if ( IsSandboxRelativePath( configuredPath ) )
				return NormalizePath( configuredPath );

			return $"/paneos/imported/{EncodeOpaqueName( configuredPath )}.datc";
		}

		return $"/paneos/saves/{SanitizeSegment( computerId )}.datc";
	}

	public static string ResolveArchiveUserNameStoragePath( string archivePath )
	{
		return $"{NormalizePath( archivePath )}.user.txt";
	}

	public static bool FileExists( string path )
	{
#if PANEOS_UNIT_TESTS
		return File.Exists( NormalizeTestPath( path ) );
#else
		return FileSystem.Data.FileExists( NormalizePath( path ) );
#endif
	}

	public static byte[] ReadAllBytes( string path )
	{
#if PANEOS_UNIT_TESTS
		var resolvedPath = NormalizeTestPath( path );
		return File.Exists( resolvedPath ) ? File.ReadAllBytes( resolvedPath ) : Array.Empty<byte>();
#else
		var normalizedPath = NormalizePath( path );
		return FileSystem.Data.FileExists( normalizedPath ) ? FileSystem.Data.ReadAllBytes( normalizedPath ).ToArray() : Array.Empty<byte>();
#endif
	}

	public static void WriteAllBytes( string path, byte[] bytes )
	{
#if PANEOS_UNIT_TESTS
		var resolvedPath = NormalizeTestPath( path );
		var directory = Path.GetDirectoryName( resolvedPath );
		if ( !string.IsNullOrWhiteSpace( directory ) )
			Directory.CreateDirectory( directory );

		File.WriteAllBytes( resolvedPath, bytes );
#else
		var normalizedPath = NormalizePath( path );
		EnsureParentDirectory( normalizedPath );
		FileSystem.Data.WriteAllBytes( normalizedPath, bytes );
#endif
	}

	public static string ReadAllText( string path )
	{
#if PANEOS_UNIT_TESTS
		var resolvedPath = NormalizeTestPath( path );
		return File.Exists( resolvedPath ) ? File.ReadAllText( resolvedPath ) : "";
#else
		var normalizedPath = NormalizePath( path );
		return FileSystem.Data.FileExists( normalizedPath ) ? FileSystem.Data.ReadAllText( normalizedPath ) : "";
#endif
	}

	public static void WriteAllText( string path, string content )
	{
#if PANEOS_UNIT_TESTS
		var resolvedPath = NormalizeTestPath( path );
		var directory = Path.GetDirectoryName( resolvedPath );
		if ( !string.IsNullOrWhiteSpace( directory ) )
			Directory.CreateDirectory( directory );

		File.WriteAllText( resolvedPath, content );
#else
		var normalizedPath = NormalizePath( path );
		EnsureParentDirectory( normalizedPath );
		FileSystem.Data.WriteAllText( normalizedPath, content );
#endif
	}

	public static string GetLocalUserNameFallback()
	{
#if PANEOS_UNIT_TESTS
		return Environment.GetEnvironmentVariable( "USERNAME" ) ?? "";
#else
		return Sandbox.Utility.Steam.PersonaName ?? "";
#endif
	}

#if !PANEOS_UNIT_TESTS
	private static void EnsureParentDirectory( string normalizedPath )
	{
		var lastSlash = normalizedPath.LastIndexOf( '/' );
		if ( lastSlash <= 0 )
			return;

		FileSystem.Data.CreateDirectory( normalizedPath[..lastSlash] );
	}
#endif

	private static string NormalizePath( string path )
	{
		var normalized = path.Replace( '\\', '/' ).Trim();
		if ( string.IsNullOrWhiteSpace( normalized ) )
			return "/paneos/saves/default.datc";

		if ( !normalized.StartsWith( "/", StringComparison.Ordinal ) )
			normalized = "/" + normalized.TrimStart( '/' );

		return normalized;
	}

	private static bool IsSandboxRelativePath( string configuredPath )
	{
		return configuredPath.StartsWith( "/", StringComparison.Ordinal ) &&
			!configuredPath.Contains( ":", StringComparison.Ordinal );
	}

	private static string SanitizeSegment( string value )
	{
		var source = string.IsNullOrWhiteSpace( value ) ? "computer" : value.Trim();
		var builder = new StringBuilder( source.Length );
		foreach ( var character in source )
		{
			builder.Append( char.IsLetterOrDigit( character ) || character is '-' or '_' ? character : '_' );
		}

		return builder.Length == 0 ? "computer" : builder.ToString();
	}

	private static string EncodeOpaqueName( string value )
	{
		var bytes = Encoding.UTF8.GetBytes( value.Trim() );
		var builder = new StringBuilder( bytes.Length * 2 );
		foreach ( var currentByte in bytes )
			builder.Append( currentByte.ToString( "x2" ) );

		return builder.ToString();
	}

#if PANEOS_UNIT_TESTS
	private static string NormalizeTestPath( string path )
	{
		if ( string.IsNullOrWhiteSpace( path ) )
			return path;

		if ( Path.IsPathRooted( path ) )
			return path;

		return Path.Combine( Path.GetTempPath(), NormalizePath( path ).TrimStart( '/' ).Replace( '/', Path.DirectorySeparatorChar ) );
	}
#endif
}
