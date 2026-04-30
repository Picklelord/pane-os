using PaneOS.InteractiveComputer;
using PaneOS.InteractiveComputer.Core;

var tests = new (string Name, Action Body)[]
{
	("Ridge normalizes bare hosts to https", RidgeNormalizesBareHosts),
	("Ridge blocks external websites by default", RidgeBlocksByDefault),
	("Ridge allows exact whitelisted host when enabled", RidgeAllowsExactHost),
	("Ridge allows wildcard subdomains only", RidgeAllowsWildcardSubdomainsOnly),
	("Ridge blocks unsupported protocols", RidgeBlocksUnsupportedProtocols),
	("Ridge parses and deduplicates allowed hosts", RidgeParsesAllowedHosts),
	("Screensaver activates after configured delay", ScreenSaverActivatesAfterDelay),
	("Screensaver does not run while sleeping", ScreenSaverDoesNotRunWhileSleeping),
	("Screensaver pauses and resets while interacting", ScreenSaverPausesWhileInteracting),
	("Screensaver bounces off top edge toward bottom", ScreenSaverBouncesTop),
	("Screensaver bounces off right edge toward left", ScreenSaverBouncesRight),
	("Screensaver bounces off bottom edge toward top", ScreenSaverBouncesBottom),
	("Screensaver bounces off left edge toward right", ScreenSaverBouncesLeft),
	("CPU input delay suppresses only the focused app", CpuDelaySuppressesFocusedAppOnly),
	("Task Manager refresh policy scopes updates by visible tab", TaskManagerRefreshPolicyScopesByTab),
	("Task Manager process sorting prefers highest CPU first", TaskManagerSortingUsesSelectedField),
	("Archive user policy prefers Steam name and falls back to USERNAME", ArchiveUserPolicyPrefersSteamThenUsername),
	("Archive ensure migrates legacy Player folder to persisted username", ArchiveMigratesLegacyPlayerFolder),
	("File dialog policy normalizes extension filters", FileDialogPolicyNormalizesExtensions),
	("File dialog policy resolves save paths in current folder", FileDialogPolicyResolvesSavePath),
	("Wallpaper policy normalizes known wallpapers", WallpaperPolicyNormalizesKnownValues),
	("Wallpaper policy uses the desktop background image by default", WallpaperPolicyUsesDesktopImageByDefault),
	("Archive text files round-trip through My Documents", ArchiveTextFilesRoundTrip),
	("Archive rename updates file names in place", ArchiveRenameMovesEntries),
	("Archive delete can move items to recycle bin and restore them", ArchiveRecycleBinRoundTrip),
	("File associations open text files in Notepad", FileAssociationsOpenTextFiles),
	("File associations open url files in Ridge", FileAssociationsOpenUrlFiles),
	("Corrupted url shortcuts are rejected with a specific dialog", CorruptedUrlShortcutsAreRejected),
	("File associations launch executables by resolved name", FileAssociationsLaunchExecutables),
	("Missing executables are flagged as corrupted applications", MissingExecutablesAreRejected),
	("Desktop shortcut layout wraps into additional columns", DesktopShortcutLayoutWrapsColumns),
	("Desktop selection rectangle captures intersecting shortcuts", DesktopSelectionCapturesIntersectingShortcuts),
	("Window layout policy honors app defaults and cascades offsets", WindowLayoutPolicyHonorsDescriptorDefaults),
	("Maintenance policy generates visible update and install logs", MaintenancePolicyGeneratesLogs),
	("Resolution policy prefers game settings by default and clamps minimum sizes", ResolutionPolicyUsesGameSettingsWhenEnabled),
	("Computer state defaults include screensaver and app lists", ComputerStateDefaults),
};

var failed = 0;
foreach ( var test in tests )
{
	try
	{
		test.Body();
		Console.WriteLine( $"PASS {test.Name}" );
	}
	catch ( Exception ex )
	{
		failed++;
		Console.WriteLine( $"FAIL {test.Name}: {ex.Message}" );
	}
}

Console.WriteLine();
Console.WriteLine( failed == 0 ? $"All {tests.Length} tests passed." : $"{failed} of {tests.Length} tests failed." );
return failed == 0 ? 0 : 1;

static void RidgeNormalizesBareHosts()
{
	Equal( "https://sbox.game", RidgeBrowserPolicy.NormalizeUrl( "sbox.game" ) );
	Equal( "paneos://home", RidgeBrowserPolicy.NormalizeUrl( "" ) );
}

static void RidgeBlocksByDefault()
{
	var result = RidgeBrowserPolicy.Evaluate( "https://sbox.game", null, "sbox.game" );
	False( result.CanRenderWebPanel );
	Equal( "Rendering disabled", result.Status );
}

static void RidgeAllowsExactHost()
{
	var result = RidgeBrowserPolicy.Evaluate( "sbox.game", "true", "sbox.game docs.facepunch.com" );
	True( result.CanRenderWebPanel );
	Equal( "https://sbox.game", result.NormalizedUrl );
	Equal( "Loaded sbox.game", result.Status );
}

static void RidgeAllowsWildcardSubdomainsOnly()
{
	True( RidgeBrowserPolicy.Evaluate( "https://docs.example.com", "on", "*.example.com" ).CanRenderWebPanel );
	False( RidgeBrowserPolicy.Evaluate( "https://example.com", "on", "*.example.com" ).CanRenderWebPanel );
}

static void RidgeBlocksUnsupportedProtocols()
{
	var result = RidgeBrowserPolicy.Evaluate( "ftp://example.com", "true", "example.com" );
	False( result.CanRenderWebPanel );
	Equal( "Blocked", result.Status );
}

static void RidgeParsesAllowedHosts()
{
	var hosts = RidgeBrowserPolicy.ParseHostList( "sbox.game, SBOX.game;docs.facepunch.com\n*.example.com" );
	Equal( 3, hosts.Count );
	True( hosts.Contains( "sbox.game" ) );
	True( hosts.Contains( "docs.facepunch.com" ) );
	True( hosts.Contains( "*.example.com" ) );
}

static void ScreenSaverActivatesAfterDelay()
{
	var state = NewScreenSaverState();
	var changedBefore = ScreenSaverSimulator.Tick( state, 59f, false );
	False( changedBefore );
	False( state.ScreenSaver.IsActive );

	var changedAtDelay = ScreenSaverSimulator.Tick( state, 1f, false );
	True( changedAtDelay );
	True( state.ScreenSaver.IsActive );
}

static void ScreenSaverDoesNotRunWhileSleeping()
{
	var state = NewScreenSaverState();
	state.IsSleeping = true;

	var changed = ScreenSaverSimulator.Tick( state, 120f, false );
	False( changed );
	False( state.ScreenSaver.IsActive );
	Equal( 0f, state.ScreenSaver.IdleSeconds );
}

static void ScreenSaverPausesWhileInteracting()
{
	var state = NewScreenSaverState();
	state.ScreenSaver.IdleSeconds = 12f;
	state.ScreenSaver.IsActive = true;

	var changed = ScreenSaverSimulator.Tick( state, 1f, true );
	True( changed );
	False( state.ScreenSaver.IsActive );
	Equal( 0f, state.ScreenSaver.IdleSeconds );
}

static void ScreenSaverBouncesTop()
{
	var state = NewScreenSaverState();
	state.ScreenSaver.LogoY = 2f;
	state.ScreenSaver.VelocityY = -50f;

	ScreenSaverSimulator.MoveLogo( state, 1f );
	Equal( 0f, state.ScreenSaver.LogoY );
	True( state.ScreenSaver.VelocityY > 0f );
}

static void ScreenSaverBouncesRight()
{
	var state = NewScreenSaverState();
	state.ScreenSaver.LogoX = 780f;
	state.ScreenSaver.VelocityX = 50f;

	ScreenSaverSimulator.MoveLogo( state, 1f );
	Equal( 800f, state.ScreenSaver.LogoX );
	True( state.ScreenSaver.VelocityX < 0f );
}

static void ScreenSaverBouncesBottom()
{
	var state = NewScreenSaverState();
	state.ScreenSaver.LogoY = 580f;
	state.ScreenSaver.VelocityY = 50f;

	ScreenSaverSimulator.MoveLogo( state, 1f );
	Equal( 600f, state.ScreenSaver.LogoY );
	True( state.ScreenSaver.VelocityY < 0f );
}

static void ScreenSaverBouncesLeft()
{
	var state = NewScreenSaverState();
	state.ScreenSaver.LogoX = 4f;
	state.ScreenSaver.VelocityX = -50f;

	ScreenSaverSimulator.MoveLogo( state, 1f );
	Equal( 0f, state.ScreenSaver.LogoX );
	True( state.ScreenSaver.VelocityX > 0f );
}

static void CpuDelaySuppressesFocusedAppOnly()
{
	True( ComputerInputDelayPolicy.ShouldSuppressFocusedAppInput( true, true, true ) );
	False( ComputerInputDelayPolicy.ShouldSuppressFocusedAppInput( false, true, true ) );
	False( ComputerInputDelayPolicy.ShouldSuppressFocusedAppInput( true, false, true ) );
	False( ComputerInputDelayPolicy.ShouldSuppressFocusedAppInput( true, true, false ) );
}

static void TaskManagerRefreshPolicyScopesByTab()
{
	var processesA = TaskManagerRefreshPolicy.GetRefreshVersion( TaskManagerTab.Processes, 3, 10, 100 );
	var processesB = TaskManagerRefreshPolicy.GetRefreshVersion( TaskManagerTab.Processes, 3, 11, 100 );
	var storageA = TaskManagerRefreshPolicy.GetRefreshVersion( TaskManagerTab.Storage, 3, 10, 100 );
	var storageB = TaskManagerRefreshPolicy.GetRefreshVersion( TaskManagerTab.Storage, 3, 11, 100 );
	var storageC = TaskManagerRefreshPolicy.GetRefreshVersion( TaskManagerTab.Storage, 3, 10, 101 );

	NotEqual( processesA, processesB );
	Equal( storageA, storageB );
	NotEqual( storageA, storageC );
}

static void TaskManagerSortingUsesSelectedField()
{
	var rows = new[]
	{
		new TaskManagerProcessSortItem { InstanceId = "1", ProcessName = "Calculator", CpuPercent = 12f, RamPercent = 10f, Status = "Running", StartupProcess = false },
		new TaskManagerProcessSortItem { InstanceId = "2", ProcessName = "Networking", CpuPercent = 3f, RamPercent = 22f, Status = "Running", StartupProcess = true },
		new TaskManagerProcessSortItem { InstanceId = "3", ProcessName = "PaneOS32", CpuPercent = 88f, RamPercent = 18f, Status = "Running", StartupProcess = true }
	};

	var byCpu = TaskManagerProcessSortPolicy.Sort( rows, TaskManagerProcessSortField.Cpu, true );
	var byRam = TaskManagerProcessSortPolicy.Sort( rows, TaskManagerProcessSortField.Ram, true );

	Equal( "PaneOS32", byCpu[0].ProcessName );
	Equal( "Networking", byRam[0].ProcessName );
}

static void ArchiveUserPolicyPrefersSteamThenUsername()
{
	Equal( "Alice", ComputerArchiveUserPolicy.ResolveInitialUserName( "Alice", "WindowsUser" ) );
	Equal( "WindowsUser", ComputerArchiveUserPolicy.ResolveInitialUserName( "Player", "WindowsUser" ) );
	Equal( "Player", ComputerArchiveUserPolicy.ResolveInitialUserName( "Player", "" ) );
}

static void ArchiveMigratesLegacyPlayerFolder()
{
	var tempPath = Path.Combine( Path.GetTempPath(), $"paneos-test-{Guid.NewGuid():N}.datc" );
	try
	{
		var apps = Array.Empty<ComputerAppDescriptor>();
		PaneArchiveFileSystem.EnsureArchive( tempPath, "Player", apps );
		PaneArchiveFileSystem.CreateFile( tempPath, new[] { "C:", "Users", "Player", "My Documents" }, "Notes", "txt", "hello" );

		PaneArchiveFileSystem.EnsureArchive( tempPath, "WindowsUser", apps );

		var rootUsers = PaneArchiveFileSystem.GetItems( tempPath, new[] { "C:", "Users" } );
		True( rootUsers.Any( x => x.Name == "WindowsUser" ) );
		False( rootUsers.Any( x => x.Name == "Player" ) );

		var docs = PaneArchiveFileSystem.GetItems( tempPath, new[] { "C:", "Users", "WindowsUser", "My Documents" } );
		True( docs.Any( x => x.Name == "Notes.txt" ) );
	}
	finally
	{
		if ( File.Exists( tempPath ) )
			File.Delete( tempPath );
	}
}

static void FileDialogPolicyNormalizesExtensions()
{
	var options = new ComputerFileDialogOptions
	{
		AllowedExtensions = new[] { ".txt", "Url" }
	};

	True( ComputerFileDialogPolicy.AllowsExtension( options, ".TXT" ) );
	True( ComputerFileDialogPolicy.AllowsExtension( options, "url" ) );
	False( ComputerFileDialogPolicy.AllowsExtension( options, ".exe" ) );
}

static void FileDialogPolicyResolvesSavePath()
{
	var openOptions = new ComputerFileDialogOptions
	{
		Mode = ComputerFileDialogMode.Open
	};
	var saveOptions = new ComputerFileDialogOptions
	{
		Mode = ComputerFileDialogMode.Save
	};

	Equal(
		"/C:/Users/Alice/My Documents/Notes.txt",
		ComputerFileDialogPolicy.ResolvePath(
			openOptions,
			new[] { "C:", "Users", "Alice", "My Documents" },
			"/C:/Users/Alice/My Documents/Notes.txt",
			"" ) );

	Equal(
		"/C:/Users/Alice/My Documents/Todo.txt",
		ComputerFileDialogPolicy.ResolvePath(
			saveOptions,
			new[] { "C:", "Users", "Alice", "My Documents" },
			"",
			"Todo.txt" ) );

	Equal(
		"",
		ComputerFileDialogPolicy.ResolvePath(
			saveOptions,
			new[] { "C:", "Users", "Alice", "My Documents" },
			"",
			"   " ) );
}

static void WallpaperPolicyNormalizesKnownValues()
{
	Equal( "blue", ComputerWallpaperPolicy.Normalize( "Blue" ) );
	Equal( "sunset", ComputerWallpaperPolicy.Normalize( "SUNSET" ) );
	Equal( "default", ComputerWallpaperPolicy.Normalize( "something-else" ) );
}

static void WallpaperPolicyUsesDesktopImageByDefault()
{
	var style = ComputerWallpaperPolicy.GetBackgroundStyle( "default" );
	AssertContains( "/Assets/desktopBackground.png", style );
	AssertContains( "background-size: cover", style );
}

static void ArchiveTextFilesRoundTrip()
{
	var tempPath = Path.Combine( Path.GetTempPath(), $"paneos-text-{Guid.NewGuid():N}.datc" );
	try
	{
		PaneArchiveFileSystem.EnsureArchive( tempPath, "Alice", Array.Empty<ComputerAppDescriptor>() );
		var filePath = new[] { "C:", "Users", "Alice", "My Documents", "Journal.txt" };

		PaneArchiveFileSystem.WriteTextFile( tempPath, filePath, "Day one" );
		Equal( "Day one", PaneArchiveFileSystem.ReadTextFile( tempPath, filePath ) );
		True( PaneArchiveFileSystem.Exists( tempPath, filePath ) );
	}
	finally
	{
		if ( File.Exists( tempPath ) )
			File.Delete( tempPath );
	}
}

static void ArchiveRenameMovesEntries()
{
	var tempPath = Path.Combine( Path.GetTempPath(), $"paneos-rename-{Guid.NewGuid():N}.datc" );
	try
	{
		PaneArchiveFileSystem.EnsureArchive( tempPath, "Alice", Array.Empty<ComputerAppDescriptor>() );
		var originalPath = new[] { "C:", "Users", "Alice", "My Documents", "Notes.txt" };
		var renamedPath = new[] { "C:", "Users", "Alice", "My Documents", "Todo.txt" };
		PaneArchiveFileSystem.WriteTextFile( tempPath, originalPath, "todo" );

		PaneArchiveFileSystem.Rename( tempPath, originalPath, "Todo.txt" );

		False( PaneArchiveFileSystem.Exists( tempPath, originalPath ) );
		True( PaneArchiveFileSystem.Exists( tempPath, renamedPath ) );
		Equal( "todo", PaneArchiveFileSystem.ReadTextFile( tempPath, renamedPath ) );
	}
	finally
	{
		if ( File.Exists( tempPath ) )
			File.Delete( tempPath );
	}
}

static void ArchiveRecycleBinRoundTrip()
{
	var tempPath = Path.Combine( Path.GetTempPath(), $"paneos-trash-{Guid.NewGuid():N}.datc" );
	try
	{
		PaneArchiveFileSystem.EnsureArchive( tempPath, "Alice", Array.Empty<ComputerAppDescriptor>() );
		var originalPath = new[] { "C:", "Users", "Alice", "My Documents", "Draft.txt" };
		PaneArchiveFileSystem.WriteTextFile( tempPath, originalPath, "draft" );

		var recycledPath = PaneArchiveFileSystem.MoveToRecycleBin( tempPath, originalPath );
		False( PaneArchiveFileSystem.Exists( tempPath, originalPath ) );
		True( PaneArchiveFileSystem.Exists( tempPath, recycledPath.TrimStart( '/' ).Split( '/' ) ) );

		var restoredPath = PaneArchiveFileSystem.RestoreFromRecycleBin( tempPath, recycledPath.TrimStart( '/' ).Split( '/' ) );
		True( PaneArchiveFileSystem.Exists( tempPath, originalPath ) );
		Equal( "/C:/Users/Alice/My Documents/Draft.txt", restoredPath );
		Equal( "draft", PaneArchiveFileSystem.ReadTextFile( tempPath, originalPath ) );
	}
	finally
	{
		if ( File.Exists( tempPath ) )
			File.Delete( tempPath );
	}
}

static void FileAssociationsOpenTextFiles()
{
	var target = ComputerFileAssociationPolicy.ResolveLaunchTarget(
		"/C:/Users/Alice/My Documents/Notes.txt",
		"Notes.txt",
		"hello",
		Array.Empty<ComputerAppDescriptor>() );

	Equal( "system.notepad", target?.AppId );
	Equal( "/C:/Users/Alice/My Documents/Notes.txt", target?.InitialData["file_path"] );
}

static void FileAssociationsOpenUrlFiles()
{
	var target = ComputerFileAssociationPolicy.ResolveLaunchTarget(
		"/C:/Users/Alice/My Documents/Search.url",
		"Search.url",
		"url=https://example.com",
		Array.Empty<ComputerAppDescriptor>() );

	Equal( "system.ridge", target?.AppId );
	Equal( "https://example.com", target?.InitialData["url"] );
}

static void CorruptedUrlShortcutsAreRejected()
{
	var result = ComputerFileAssociationPolicy.ResolveOpenResult(
		"/C:/Users/Alice/My Documents/Broken.url",
		"Broken.url",
		"   ",
		Array.Empty<ComputerAppDescriptor>() );

	False( result.CanOpen );
	Equal( "Corrupted Shortcut", result.FailureTitle );
	AssertContains( "corrupted", result.FailureMessage );
}

static void FileAssociationsLaunchExecutables()
{
	var apps = new[]
	{
		new ComputerAppDescriptor
		{
			Id = "system.calc",
			Title = "Calculator",
			ExecutableName = "Calc.exe",
			Factory = () => new StubComputerApp()
		}
	};

	var target = ComputerFileAssociationPolicy.ResolveLaunchTarget(
		"/C:/Apps/Calculator/Calc.exe",
		"Calc.exe",
		"",
		apps );

	Equal( "system.calc", target?.AppId );
}

static void MissingExecutablesAreRejected()
{
	var result = ComputerFileAssociationPolicy.ResolveOpenResult(
		"/C:/Apps/Unknown/Missing.exe",
		"Missing.exe",
		"",
		Array.Empty<ComputerAppDescriptor>() );

	False( result.CanOpen );
	Equal( "Corrupted Application", result.FailureTitle );
	AssertContains( "missing", result.FailureMessage );
}

static void DesktopShortcutLayoutWrapsColumns()
{
	var first = DesktopShortcutLayoutPolicy.GetPosition( 0, 768 );
	var eighth = DesktopShortcutLayoutPolicy.GetPosition( 7, 768 );

	Equal( DesktopShortcutLayoutPolicy.OriginX, first.X );
	True( eighth.X > first.X );
	Equal( DesktopShortcutLayoutPolicy.OriginY, eighth.Y );
}

static void DesktopSelectionCapturesIntersectingShortcuts()
{
	var items = new[]
	{
		new DesktopShortcutLayoutItem { Id = "a", Index = 0 },
		new DesktopShortcutLayoutItem { Id = "b", Index = 1 },
		new DesktopShortcutLayoutItem { Id = "c", Index = 7 }
	};

	var rect = DesktopShortcutSelectionRect.FromCorners( 0f, 0f, 100f, 190f );
	var selected = DesktopShortcutLayoutPolicy.SelectIntersectingShortcutIds( items, rect, 768 );

	Equal( 2, selected.Count );
	True( selected.Contains( "a" ) );
	True( selected.Contains( "b" ) );
	False( selected.Contains( "c" ) );
}

static void WindowLayoutPolicyHonorsDescriptorDefaults()
{
	var descriptor = new ComputerAppDescriptor
	{
		Id = "system.calc",
		Title = "Calculator",
		Icon = "CA",
		DefaultWindowOffsetX = 32,
		DefaultWindowOffsetY = 24,
		DefaultWindowWidth = 320,
		DefaultWindowHeight = 360,
		Factory = () => new StubComputerApp()
	};

	var bounds = ComputerWindowLayoutPolicy.ResolveInitialBounds( descriptor, 1024, 768, 2 );

	Equal( 76, bounds.X );
	Equal( 68, bounds.Y );
	Equal( 320, bounds.Width );
	Equal( 360, bounds.Height );
}

static void MaintenancePolicyGeneratesLogs()
{
	var state = new ComputerState();
	state.InstalledApps.Add( new ComputerInstalledAppState { AppId = "system.notepad" } );
	state.OpenApps.Add( new ComputerAppState { AppId = "system.notepad", Title = "Notepad" } );

	var apps = new[]
	{
		new ComputerAppDescriptor
		{
			Id = "system.notepad",
			Title = "Notepad",
			Factory = () => new StubComputerApp()
		}
	};

	var timestamp = new DateTime( 2026, 4, 30, 1, 2, 3, DateTimeKind.Utc );
	var updateRecord = ComputerMaintenancePolicy.BuildUpdateScanRecord( state, apps, timestamp );
	var installRecord = ComputerMaintenancePolicy.BuildPackageInstallRecord( "Media Codec Pack", timestamp );

	Equal( "PaneOS Update Report.txt", updateRecord.FileName );
	AssertContains( "Installed apps: 1", updateRecord.FileContent );
	AssertContains( "Notepad", updateRecord.FileContent );
	Equal( "Media Codec Pack Setup Log.txt", installRecord.FileName );
	AssertContains( "Package staged successfully.", installRecord.FileContent );
}

static void ResolutionPolicyUsesGameSettingsWhenEnabled()
{
	var gameResolution = ComputerResolutionPolicy.ResolveResolution( true, 1024, 768, 1920f, 1080f );
	var manualResolution = ComputerResolutionPolicy.ResolveResolution( false, 800, 600, 1920f, 1080f );
	var clampedResolution = ComputerResolutionPolicy.ResolveResolution( true, 10, 10, 200f, 120f );

	Equal( (1920, 1080), gameResolution );
	Equal( (800, 600), manualResolution );
	Equal( (320, 240), clampedResolution );
}

static void ComputerStateDefaults()
{
	var state = new ComputerState();
	Equal( 1024, state.ResolutionX );
	Equal( 768, state.ResolutionY );
	True( state.ScreenSaver.Enabled );
	Equal( 60f, state.ScreenSaver.DelaySeconds );
	Equal( 2f, state.Hardware.RamGb );
	Equal( 3.7f, state.Hardware.CpuCoreGhz );
	Equal( 4, state.Hardware.CpuCoreCount );
	Equal( 256f, state.Hardware.HddStorageGb );
	Equal( 100f, state.Hardware.InternetSpeedGbps );
	Equal( 1.54f, state.Hardware.GpuCoreGhz );
	Equal( 4f, state.Hardware.GpuVramGb );
	Equal( 0f, state.RestartLogSecondsRemaining );
	Equal( 0f, state.BootSplashSecondsRemaining );
	Equal( 0, state.RestartLogLines.Count );
	Equal( 0, state.InstalledApps.Count );
	Equal( 0, state.OpenApps.Count );
}

static ComputerState NewScreenSaverState()
{
	return new ComputerState
	{
		ResolutionX = 1000,
		ResolutionY = 700,
		ScreenSaver = new ComputerScreenSaverState
		{
			Enabled = true,
			DelaySeconds = 60f,
			LogoWidth = 200f,
			LogoHeight = 100f,
			LogoX = 400f,
			LogoY = 300f,
			VelocityX = 40f,
			VelocityY = -30f
		}
	};
}

static void True( bool value )
{
	if ( !value )
		throw new InvalidOperationException( "Expected true." );
}

static void False( bool value )
{
	if ( value )
		throw new InvalidOperationException( "Expected false." );
}

static void Equal<T>( T expected, T actual )
{
	if ( !EqualityComparer<T>.Default.Equals( expected, actual ) )
		throw new InvalidOperationException( $"Expected '{expected}', got '{actual}'." );
}

static void NotEqual<T>( T left, T right )
{
	if ( EqualityComparer<T>.Default.Equals( left, right ) )
		throw new InvalidOperationException( $"Expected '{left}' and '{right}' to differ." );
}

static void AssertContains( string expectedSubstring, string actual )
{
	if ( actual.Contains( expectedSubstring, StringComparison.OrdinalIgnoreCase ) )
		return;

	throw new InvalidOperationException( $"Expected '{actual}' to contain '{expectedSubstring}'." );
}

file sealed class StubComputerApp : IComputerApp
{
}
