using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace PaneOS.InteractiveComputer.Core;

public sealed class PaneArchiveItem
{
	public string Name { get; set; } = "";
	public bool IsDirectory { get; set; }
	public string Extension { get; set; } = "";
	public string VirtualPath { get; set; } = "";
	public long SizeBytes { get; set; }
}

public static class PaneArchiveFileSystem
{
	private sealed class ArchiveEntryModel
	{
		public List<string> Segments { get; set; } = new();
		public bool IsDirectory { get; set; }
		public byte[] Content { get; set; } = Array.Empty<byte>();
	}

	public static void EnsureArchive( string archivePath, string userName, IEnumerable<ComputerAppDescriptor> apps )
	{
		var entries = ReadEntries( archivePath );
		var normalizedUserName = NormalizeDisplayName( userName );
		MigrateLegacyPlayerFolder( entries, normalizedUserName );

		EnsureDirectory( entries, "C:" );
		EnsureDirectory( entries, "C:", "Users" );
		EnsureDirectory( entries, "C:", "Users", normalizedUserName );
		EnsureDirectory( entries, "C:", "Users", normalizedUserName, "My Documents" );
		EnsureDirectory( entries, "C:", "Recycle Bin" );
		EnsureDirectory( entries, "C:", "Apps" );

		foreach ( var app in apps )
		{
			EnsureDirectory( entries, "C:", "Apps", app.Title );
			EnsureFile(
				entries,
				Encoding.UTF8.GetBytes( $"app_id={app.Id}\nexecutable={app.ResolvedExecutableName}\n" ),
				"C:",
				"Apps",
				app.Title,
				app.ResolvedExecutableName );
		}

		WriteEntries( archivePath, entries );
	}

	public static List<PaneArchiveItem> GetItems( string archivePath, IReadOnlyList<string> displayPath )
	{
		var prefix = displayPath.ToArray();
		var entries = ReadEntries( archivePath );
		var children = new Dictionary<string, PaneArchiveItem>( StringComparer.OrdinalIgnoreCase );

		foreach ( var entry in entries )
		{
			if ( entry.Segments.Count < prefix.Length || !entry.Segments.Take( prefix.Length ).SequenceEqual( prefix ) )
				continue;

			if ( entry.Segments.Count == prefix.Length )
				continue;

			var childName = entry.Segments[prefix.Length];
			var childSegments = entry.Segments.Take( prefix.Length + 1 ).ToArray();
			var childKey = string.Join( "/", childSegments );

			if ( !children.TryGetValue( childKey, out var child ) )
			{
				var extension = Path.GetExtension( childName );
				child = new PaneArchiveItem
				{
					Name = childName,
					IsDirectory = entry.IsDirectory || entry.Segments.Count > prefix.Length + 1,
					Extension = extension,
					VirtualPath = "/" + string.Join( "/", childSegments )
				};
				children[childKey] = child;
			}

			if ( !child.IsDirectory && entry.Segments.SequenceEqual( childSegments ) )
				child.SizeBytes = entry.Content.LongLength;
		}

		return children.Values
			.Where( x => !x.Name.StartsWith( "$paneos-", StringComparison.OrdinalIgnoreCase ) )
			.OrderByDescending( x => x.IsDirectory )
			.ThenBy( x => x.Name, StringComparer.OrdinalIgnoreCase )
			.ToList();
	}

	public static void CreateFolder( string archivePath, IReadOnlyList<string> parentPath, string folderName )
	{
		if ( string.IsNullOrWhiteSpace( folderName ) )
			return;

		var entries = ReadEntries( archivePath );
		EnsureDirectory( entries, parentPath.Concat( new[] { folderName.Trim() } ).ToArray() );
		WriteEntries( archivePath, entries );
	}

	public static void CreateFile( string archivePath, IReadOnlyList<string> parentPath, string fileName, string extension, string? content = null )
	{
		if ( string.IsNullOrWhiteSpace( fileName ) )
			return;

		var safeExtension = extension?.Trim().TrimStart( '.' ) ?? "";
		var fullName = string.IsNullOrWhiteSpace( safeExtension )
			? fileName.Trim()
			: $"{fileName.Trim()}.{safeExtension}";

		var entries = ReadEntries( archivePath );
		EnsureFile( entries, Encoding.UTF8.GetBytes( content ?? "" ), parentPath.Concat( new[] { fullName } ).ToArray() );
		WriteEntries( archivePath, entries );
	}

	public static string ReadTextFile( string archivePath, IReadOnlyList<string> filePath )
	{
		var entries = ReadEntries( archivePath );
		var file = entries.FirstOrDefault( x => !x.IsDirectory && x.Segments.SequenceEqual( filePath ) );
		return file is null ? "" : Encoding.UTF8.GetString( file.Content );
	}

	public static void WriteTextFile( string archivePath, IReadOnlyList<string> filePath, string content )
	{
		if ( filePath.Count == 0 )
			return;

		var entries = ReadEntries( archivePath );
		EnsureFile( entries, Encoding.UTF8.GetBytes( content ), filePath.ToArray() );
		WriteEntries( archivePath, entries );
	}

	public static void Rename( string archivePath, IReadOnlyList<string> targetPath, string newName )
	{
		if ( targetPath.Count == 0 || string.IsNullOrWhiteSpace( newName ) )
			return;

		var entries = ReadEntries( archivePath );
		var sourcePrefix = targetPath.ToArray();
		var destinationPrefix = targetPath.Take( targetPath.Count - 1 ).Append( newName.Trim() ).ToArray();

		foreach ( var entry in entries.Where( x => x.Segments.Count >= sourcePrefix.Length && x.Segments.Take( sourcePrefix.Length ).SequenceEqual( sourcePrefix ) ) )
		{
			entry.Segments = destinationPrefix.Concat( entry.Segments.Skip( sourcePrefix.Length ) ).ToList();
		}

		WriteEntries( archivePath, entries );
	}

	public static void Move( string archivePath, IReadOnlyList<string> sourcePath, IReadOnlyList<string> destinationPath )
	{
		if ( sourcePath.Count == 0 || destinationPath.Count == 0 )
			return;

		var entries = ReadEntries( archivePath );
		MoveEntries( entries, sourcePath.ToArray(), destinationPath.ToArray() );
		WriteEntries( archivePath, entries );
	}

	public static bool Exists( string archivePath, IReadOnlyList<string> targetPath )
	{
		var entries = ReadEntries( archivePath );
		return entries.Any( x => x.Segments.SequenceEqual( targetPath ) );
	}

	public static void Delete( string archivePath, IReadOnlyList<string> targetPath )
	{
		if ( targetPath.Count == 0 )
			return;

		var entries = ReadEntries( archivePath );
		entries.RemoveAll( x => x.Segments.Count >= targetPath.Count && x.Segments.Take( targetPath.Count ).SequenceEqual( targetPath ) );
		DeleteRecycleBinMetadata( entries, targetPath );
		WriteEntries( archivePath, entries );
	}

	public static string MoveToRecycleBin( string archivePath, IReadOnlyList<string> targetPath )
	{
		if ( targetPath.Count == 0 )
			return "";

		var entries = ReadEntries( archivePath );
		var recycleRoot = new[] { "C:", "Recycle Bin" };
		EnsureDirectory( entries, recycleRoot );
		EnsureDirectory( entries, recycleRoot.Concat( new[] { "$paneos-meta" } ).ToArray() );
		var sourceName = targetPath.Last();
		var recycleName = ResolveUniqueChildName( entries, recycleRoot, sourceName );
		var destinationPath = recycleRoot.Concat( new[] { recycleName } ).ToArray();
		var originalParentPath = "/" + string.Join( "/", targetPath.Take( targetPath.Count - 1 ) );
		MoveEntries( entries, targetPath.ToArray(), destinationPath );
		EnsureFile(
			entries,
			Encoding.UTF8.GetBytes( originalParentPath ),
			"C:",
			"Recycle Bin",
			"$paneos-meta",
			$"{recycleName}.restore.txt" );
		WriteEntries( archivePath, entries );
		return "/" + string.Join( "/", destinationPath );
	}

	public static string RestoreFromRecycleBin( string archivePath, IReadOnlyList<string> recyclePath )
	{
		if ( recyclePath.Count < 3 || !IsUnderPath( recyclePath, new[] { "C:", "Recycle Bin" } ) )
			return "";

		var entries = ReadEntries( archivePath );
		var recycleName = recyclePath.Last();
		var metadataPath = new[] { "C:", "Recycle Bin", "$paneos-meta", $"{recycleName}.restore.txt" };
		var originalParentPath = ReadTextFileFromEntries( entries, metadataPath );
		if ( string.IsNullOrWhiteSpace( originalParentPath ) )
			originalParentPath = "/C:/Users";

		var parentPath = ParseVirtualPath( originalParentPath );
		EnsureDirectory( entries, parentPath.ToArray() );
		var desiredDestination = parentPath.Concat( new[] { recycleName } ).ToArray();
		if ( IsEmptyDirectory( entries, desiredDestination ) )
			entries.RemoveAll( x => x.IsDirectory && x.Segments.SequenceEqual( desiredDestination ) );

		var restoredName = ResolveUniqueChildName( entries, parentPath, recycleName );
		var destinationPath = parentPath.Concat( new[] { restoredName } ).ToArray();
		MoveEntries( entries, recyclePath.ToArray(), destinationPath );
		entries.RemoveAll( x => !x.IsDirectory && x.Segments.SequenceEqual( metadataPath ) );
		WriteEntries( archivePath, entries );
		return "/" + string.Join( "/", destinationPath );
	}

	public static List<ComputerStorageBreakdownItem> BuildStorageBreakdown( string archivePath, IEnumerable<ComputerAppDescriptor> apps )
	{
		var entries = ReadEntries( archivePath );
		var breakdown = new List<ComputerStorageBreakdownItem>();

		foreach ( var app in apps.OrderBy( x => x.Title ) )
		{
			breakdown.Add( new ComputerStorageBreakdownItem
			{
				Name = app.Title,
				SizeGb = app.StorageSpaceUsedGb
			} );
		}

		var fileTypes = entries
			.Where( x => !x.IsDirectory )
			.Where( x => x.Segments.Count >= 2 && !(x.Segments[0] == "C:" && x.Segments[1] == "Apps") )
			.GroupBy( x =>
			{
				var ext = Path.GetExtension( x.Segments.Last() );
				return string.IsNullOrWhiteSpace( ext ) ? "[no extension]" : ext.ToLowerInvariant();
			} )
			.OrderBy( x => x.Key, StringComparer.OrdinalIgnoreCase );

		foreach ( var fileType in fileTypes )
		{
			breakdown.Add( new ComputerStorageBreakdownItem
			{
				Name = fileType.Key,
				SizeGb = BytesToGb( fileType.Sum( x => (double)x.Content.LongLength ) )
			} );
		}

		return breakdown;
	}

	public static bool IsDirectory( string archivePath, IReadOnlyList<string> targetPath )
	{
		var entries = ReadEntries( archivePath );
		return entries.Any( x => x.IsDirectory && x.Segments.SequenceEqual( targetPath ) );
	}

	public static string NormalizeDisplayName( string? value )
	{
		if ( string.IsNullOrWhiteSpace( value ) )
			return "Player";

		var cleaned = new string( value.Trim().Select( x => Path.GetInvalidFileNameChars().Contains( x ) ? '_' : x ).ToArray() );
		return string.IsNullOrWhiteSpace( cleaned ) ? "Player" : cleaned;
	}

	private static List<ArchiveEntryModel> ReadEntries( string archivePath )
	{
		if ( !ComputerSandboxStorage.FileExists( archivePath ) )
			return new List<ArchiveEntryModel>();

		var entries = new List<ArchiveEntryModel>();
		using var stream = new MemoryStream( ComputerSandboxStorage.ReadAllBytes( archivePath ), writable: false );
		using var archive = new ZipArchive( stream, ZipArchiveMode.Read );

		foreach ( var entry in archive.Entries )
		{
			var normalizedName = entry.FullName.Replace( '\\', '/' );
			var isDirectory = normalizedName.EndsWith( "/" );
			var trimmedName = normalizedName.TrimEnd( '/' );
			var segments = trimmedName.Split( '/', StringSplitOptions.RemoveEmptyEntries )
				.Select( DecodeName )
				.ToList();

			using var entryStream = entry.Open();
			using var memoryStream = new MemoryStream();
			entryStream.CopyTo( memoryStream );

			entries.Add( new ArchiveEntryModel
			{
				Segments = segments,
				IsDirectory = isDirectory,
				Content = memoryStream.ToArray()
			} );
		}

		return entries;
	}

	private static void WriteEntries( string archivePath, List<ArchiveEntryModel> entries )
	{
		using var stream = new MemoryStream();
		{
			using var archive = new ZipArchive( stream, ZipArchiveMode.Create, leaveOpen: true );

			foreach ( var entry in entries.OrderBy( x => EncodePath( x.Segments, x.IsDirectory ), StringComparer.OrdinalIgnoreCase ) )
			{
				var zipEntry = archive.CreateEntry( EncodePath( entry.Segments, entry.IsDirectory ), CompressionLevel.Fastest );
				if ( entry.IsDirectory )
					continue;

				using var entryStream = zipEntry.Open();
				entryStream.Write( entry.Content, 0, entry.Content.Length );
			}
		}

		ComputerSandboxStorage.WriteAllBytes( archivePath, stream.ToArray() );
	}

	private static void EnsureDirectory( List<ArchiveEntryModel> entries, params string[] segments )
	{
		if ( entries.Any( x => x.IsDirectory && x.Segments.SequenceEqual( segments ) ) )
			return;

		entries.Add( new ArchiveEntryModel
		{
			Segments = segments.ToList(),
			IsDirectory = true
		} );
	}

	private static void MigrateLegacyPlayerFolder( List<ArchiveEntryModel> entries, string normalizedUserName )
	{
		if ( normalizedUserName.Equals( "Player", StringComparison.OrdinalIgnoreCase ) )
			return;

		var sourcePrefix = new[] { "C:", "Users", "Player" };
		var destinationPrefix = new[] { "C:", "Users", normalizedUserName };
		var hasSource = entries.Any( x => x.Segments.Count >= sourcePrefix.Length && x.Segments.Take( sourcePrefix.Length ).SequenceEqual( sourcePrefix ) );
		var hasDestination = entries.Any( x => x.Segments.Count >= destinationPrefix.Length && x.Segments.Take( destinationPrefix.Length ).SequenceEqual( destinationPrefix ) );
		if ( !hasSource || hasDestination )
			return;

		foreach ( var entry in entries.Where( x => x.Segments.Count >= sourcePrefix.Length && x.Segments.Take( sourcePrefix.Length ).SequenceEqual( sourcePrefix ) ) )
		{
			entry.Segments = destinationPrefix
				.Concat( entry.Segments.Skip( sourcePrefix.Length ) )
				.ToList();
		}
	}

	private static void EnsureFile( List<ArchiveEntryModel> entries, byte[] content, params string[] segments )
	{
		var existing = entries.FirstOrDefault( x => !x.IsDirectory && x.Segments.SequenceEqual( segments ) );
		if ( existing is not null )
		{
			existing.Content = content;
			return;
		}

		entries.Add( new ArchiveEntryModel
		{
			Segments = segments.ToList(),
			Content = content
		} );
	}

	private static void MoveEntries( List<ArchiveEntryModel> entries, string[] sourcePrefix, string[] destinationPrefix )
	{
		foreach ( var entry in entries.Where( x => x.Segments.Count >= sourcePrefix.Length && x.Segments.Take( sourcePrefix.Length ).SequenceEqual( sourcePrefix ) ) )
		{
			entry.Segments = destinationPrefix.Concat( entry.Segments.Skip( sourcePrefix.Length ) ).ToList();
		}
	}

	private static string ResolveUniqueChildName( List<ArchiveEntryModel> entries, IReadOnlyList<string> parentPath, string desiredName )
	{
		var name = desiredName;
		var stem = Path.GetFileNameWithoutExtension( desiredName );
		var extension = Path.GetExtension( desiredName );
		var suffix = 2;
		while ( entries.Any( x => x.Segments.Count >= parentPath.Count + 1 &&
			x.Segments.Take( parentPath.Count ).SequenceEqual( parentPath ) &&
			x.Segments[parentPath.Count].Equals( name, StringComparison.OrdinalIgnoreCase ) ) )
		{
			name = string.IsNullOrWhiteSpace( extension )
				? $"{stem} ({suffix++})"
				: $"{stem} ({suffix++}){extension}";
		}

		return name;
	}

	private static bool IsEmptyDirectory( List<ArchiveEntryModel> entries, IReadOnlyList<string> path )
	{
		return entries.Any( x => x.IsDirectory && x.Segments.SequenceEqual( path ) ) &&
			!entries.Any( x => x.Segments.Count > path.Count && x.Segments.Take( path.Count ).SequenceEqual( path ) );
	}

	private static bool IsUnderPath( IReadOnlyList<string> path, IReadOnlyList<string> parentPath )
	{
		return path.Count >= parentPath.Count && path.Take( parentPath.Count ).SequenceEqual( parentPath );
	}

	private static string ReadTextFileFromEntries( List<ArchiveEntryModel> entries, IReadOnlyList<string> filePath )
	{
		var file = entries.FirstOrDefault( x => !x.IsDirectory && x.Segments.SequenceEqual( filePath ) );
		return file is null ? "" : Encoding.UTF8.GetString( file.Content );
	}

	private static IReadOnlyList<string> ParseVirtualPath( string virtualPath )
	{
		return virtualPath
			.Trim()
			.TrimStart( '/' )
			.Split( '/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries );
	}

	private static void DeleteRecycleBinMetadata( List<ArchiveEntryModel> entries, IReadOnlyList<string> targetPath )
	{
		if ( targetPath.Count < 3 || !IsUnderPath( targetPath, new[] { "C:", "Recycle Bin" } ) )
			return;

		var recycleName = targetPath[2];
		entries.RemoveAll( x => !x.IsDirectory && x.Segments.SequenceEqual( new[] { "C:", "Recycle Bin", "$paneos-meta", $"{recycleName}.restore.txt" } ) );
	}

	private static string EncodePath( IReadOnlyList<string> segments, bool isDirectory )
	{
		var encoded = string.Join( "/", segments.Select( EncodeName ) );
		return isDirectory ? $"{encoded}/" : encoded;
	}

	private static string EncodeName( string name )
	{
		return Convert.ToBase64String( Encoding.UTF8.GetBytes( name ) );
	}

	private static string DecodeName( string encoded )
	{
		return Encoding.UTF8.GetString( Convert.FromBase64String( encoded ) );
	}

	private static float BytesToGb( double bytes )
	{
		return (float)(bytes / 1024d / 1024d / 1024d);
	}
}
