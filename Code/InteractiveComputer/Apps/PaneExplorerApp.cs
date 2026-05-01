using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PaneOS.InteractiveComputer.Core;
using Sandbox;
using Sandbox.UI;

namespace PaneOS.InteractiveComputer.Apps;

[ComputerApp( "system.paneexplorer", "Pane Explorer", Icon = "PE", SortOrder = 18 )]
public sealed class PaneExplorerApp : IComputerApp
{
	public ComputerAppSession Run( ComputerAppContext context )
	{
		return new ComputerAppSession
		{
			Title = "Pane Explorer",
			Icon = "PE",
			Content = new PaneExplorerPanel( context )
		};
	}
}

[StyleSheet( "InteractiveComputerApps.scss" )]
public sealed class PaneExplorerPanel : ComputerWarmupPanel
{
	private readonly ComputerAppContext context;
	private readonly string archivePath;
	private Panel listHost = null!;
	private Panel contextMenuHost = null!;
	private Label pathLabel = null!;
	private readonly IReadOnlyList<string> documentsPath;
	private IReadOnlyList<string> currentPath;
	private string? selectedPath;
	private string? contextMenuPath;
	private readonly Stack<string[]> backHistory = new();
	private readonly Stack<string[]> forwardHistory = new();
	private readonly Dictionary<string, Panel> rowByPath = new( StringComparer.OrdinalIgnoreCase );
	private static readonly IReadOnlyList<string> RecycleBinPath = new[] { "C:", "Recycle Bin" };

	public PaneExplorerPanel( ComputerAppContext context )
	{
		this.context = context;
		AddClass( "explorer-app" );

		archivePath = context.GetArchivePath();
		PaneArchiveFileSystem.EnsureArchive(
			archivePath,
			context.Computer.ResolvePersistentArchiveUserName( context.Runtime.State ),
			context.Runtime.Apps );

		documentsPath = ParsePath( context.GetDefaultDocumentsPath() );
		currentPath = ParsePath( context.LoadValue( "path" ) ?? context.GetDefaultDocumentsPath() );
		BuildUi();
	}

	protected override void WarmupRefresh()
	{
		BuildUi();
	}

	private void BuildUi()
	{
		DeleteChildren( true );

		pathLabel = new Label { Parent = this };
		pathLabel.AddClass( "explorer-path" );

		var toolbar = new Panel { Parent = this };
		toolbar.AddClass( "explorer-toolbar" );

		CreateToolbarButton( toolbar, "<", NavigateBack, "explorer-button-small" );
		CreateToolbarButton( toolbar, ">", NavigateForward, "explorer-button-small" );
		if ( !IsAtRootPath() )
			CreateToolbarButton( toolbar, "My PC", () => NavigateTo( Array.Empty<string>() ), "explorer-button-wide" );
		if ( !IsAtDocumentsPath() )
			CreateToolbarButton( toolbar, "My Documents", () => NavigateTo( documentsPath ), "explorer-button-xl" );
		if ( !IsAtRecycleBinPath() )
			CreateToolbarButton( toolbar, "Recycle Bin", () => NavigateTo( RecycleBinPath ), "explorer-button-xl" );
		CreateToolbarButton( toolbar, "Rename", PromptRenameSelected );
		if ( IsAtRecycleBinPath() )
			CreateToolbarButton( toolbar, "Restore", RestoreSelected );

		var table = new Panel { Parent = this };
		table.AddClass( "explorer-table" );

		var header = new Panel { Parent = table };
		header.AddClass( "explorer-row explorer-header" );
		AddCell( header, "Name", "explorer-cell explorer-name-column", true );
		AddCell( header, "Type", "explorer-cell explorer-type-column", true );
		AddCell( header, "Size", "explorer-cell explorer-size-column", true );

		listHost = new ExplorerListPanel( ShowFolderContextMenu, ClearSelectionAndHideContextMenu ) { Parent = table };
		listHost.AddClass( "explorer-list" );

		contextMenuHost = new Panel { Parent = this };
		contextMenuHost.AddClass( "explorer-context-menu-host" );

		RefreshListing();
	}

	private void CreateToolbarButton( Panel parent, string label, Action onClick, string extraClass = "" )
	{
		var button = new Button( label ) { Parent = parent };
		button.AddClass( "explorer-button" );
		if ( !string.IsNullOrWhiteSpace( extraClass ) )
			button.AddClass( extraClass );
		button.AddEventListener( "onclick", onClick );
	}

	private void PromptForCreate()
	{
		contextMenuHost.DeleteChildren( true );
		context.ShowMessageBox(
			new ComputerMessageBoxOptions
			{
				Title = "Create Item",
				Message = "Enter a folder name or a file name with extension, for example Notes.txt",
				Icon = "+",
				HasTextInput = true,
				TextInputPlaceholder = "FolderName or FileName.ext",
				Buttons = new[] { "Create", "Cancel" }
			},
			result =>
			{
				if ( !result.ButtonPressed.Equals( "Create", StringComparison.OrdinalIgnoreCase ) )
					return;

				var input = result.TextValue?.Trim() ?? "";
				if ( string.IsNullOrWhiteSpace( input ) )
					return;

				var createdName = input.Trim();
				var extension = Path.GetExtension( input );
				if ( string.IsNullOrWhiteSpace( extension ) )
				{
					createdName = PaneArchiveFileSystem.CreateFolder( archivePath, currentPath, createdName );
				}
				else
				{
					var fileName = Path.GetFileNameWithoutExtension( createdName );
					createdName = PaneArchiveFileSystem.CreateFile( archivePath, currentPath, fileName, extension.TrimStart( '.' ) );
				}

				selectedPath = BuildChildVirtualPath( currentPath, createdName );
				context.Runtime.PushNotification( "Created", $"{createdName} was created.", "+" );
				context.Runtime.RefreshTransientUi();
				RefreshListing();
			} );
	}

	private void PromptRenameSelected()
	{
		var targetPath = selectedPath ?? contextMenuPath;
		if ( string.IsNullOrWhiteSpace( targetPath ) )
			return;

		contextMenuHost.DeleteChildren( true );
		var currentName = ParsePath( targetPath ).LastOrDefault() ?? "";
		context.ShowMessageBox(
			new ComputerMessageBoxOptions
			{
				Title = "Rename Item",
				Message = "Enter a new file or folder name.",
				Icon = "R",
				HasTextInput = true,
				TextInputValue = currentName,
				TextInputPlaceholder = currentName,
				Buttons = new[] { "Rename", "Cancel" }
			},
			result =>
			{
				if ( !result.ButtonPressed.Equals( "Rename", StringComparison.OrdinalIgnoreCase ) )
					return;

				var newName = result.TextValue?.Trim() ?? "";
				if ( string.IsNullOrWhiteSpace( newName ) )
					return;

				var resolvedName = PaneArchiveFileSystem.Rename( archivePath, ParsePath( targetPath ), newName );
				selectedPath = BuildChildVirtualPath( currentPath, resolvedName );
				context.Runtime.PushNotification( "Renamed", $"{currentName} is now {resolvedName}.", "R" );
				context.Runtime.RefreshTransientUi();
				RefreshListing();
			} );
	}

	private void DeleteSelected()
	{
		var target = selectedPath ?? contextMenuPath;
		if ( string.IsNullOrWhiteSpace( target ) )
			return;

		HideContextMenu();
		var targetPath = ParsePath( target );
		if ( targetPath.Count == 0 )
			return;

		context.Runtime.DeleteVirtualPath( target );
		selectedPath = null;
		RefreshListing();
	}

	private void RestoreSelected()
	{
		var target = selectedPath ?? contextMenuPath;
		if ( string.IsNullOrWhiteSpace( target ) )
			return;

		HideContextMenu();
		if ( !IsRecycleBinPath( target ) )
			return;

		context.Runtime.RestoreVirtualPath( target );
		selectedPath = null;
		RefreshListing();
	}

	private void RefreshListing()
	{
		HideContextMenu();
		pathLabel.Text = currentPath.Count == 0 ? "My PC" : string.Join( " / ", currentPath );
		listHost.DeleteChildren( true );
		rowByPath.Clear();

		if ( currentPath.Count > 0 )
			AddParentDirectoryRow();

		IReadOnlyList<PaneArchiveItem> items;
		try
		{
			items = PaneArchiveFileSystem.GetItems( archivePath, currentPath );
		}
		catch ( Exception )
		{
			currentPath = documentsPath.ToArray();
			context.SaveValue( "path", "/" + string.Join( "/", currentPath ) );
			items = PaneArchiveFileSystem.GetItems( archivePath, currentPath );
		}

		foreach ( var item in items )
		{
			var row = new ExplorerItemRow(
				item.VirtualPath,
				item.IsDirectory,
				() => SelectPath( item.VirtualPath ),
				() => ActivateItem( item ),
				() => ShowContextMenu( item.VirtualPath ) )
			{
				Parent = listHost
			};
			row.AddClass( "explorer-row" );
			rowByPath[item.VirtualPath] = row;
			row.SetClass( "selected", string.Equals( selectedPath, item.VirtualPath, StringComparison.OrdinalIgnoreCase ) );

			var nameCell = new Panel { Parent = row };
			nameCell.AddClass( "explorer-cell explorer-name-cell" );
			CreateItemIcon( nameCell, item );
			new Label( item.Name ) { Parent = nameCell }.AddClass( "explorer-item-name" );
			AddCell( row, item.IsDirectory ? "Folder" : item.Extension.TrimStart( '.' ).ToUpperInvariant() + " File", "explorer-cell explorer-type-column" );
			AddCell( row, item.IsDirectory ? "-" : FormatSize( item.SizeBytes ), "explorer-cell explorer-size-column" );
		}

		SyncSelectionStyles();
	}

	private void AddParentDirectoryRow()
	{
		var parentPath = currentPath.Take( currentPath.Count - 1 ).ToArray();
		var row = new ExplorerItemRow(
			"/" + string.Join( "/", parentPath ),
			true,
			() => selectedPath = null,
			() => NavigateTo( parentPath ),
			() => { } )
		{
			Parent = listHost
		};
		row.AddClass( "explorer-row explorer-parent-row" );

		var nameCell = new Panel { Parent = row };
		nameCell.AddClass( "explorer-cell explorer-name-cell" );
		new Panel { Parent = nameCell }.AddClass( "explorer-parent-spacer" );
		new Label( ".." ) { Parent = nameCell }.AddClass( "explorer-item-name" );
		AddCell( row, "Parent", "explorer-cell explorer-type-column" );
		AddCell( row, "-", "explorer-cell explorer-size-column" );
	}

	private void ActivateItem( PaneArchiveItem item )
	{
		if ( item.IsDirectory )
		{
			NavigateTo( ParsePath( item.VirtualPath ) );
			return;
		}

		context.OpenVirtualPath( item.VirtualPath );
	}

	private void NavigateTo( IReadOnlyList<string> path, bool pushHistory = true )
	{
		var nextPath = path.ToArray();
		if ( currentPath.SequenceEqual( nextPath ) )
			return;

		if ( pushHistory )
		{
			backHistory.Push( currentPath.ToArray() );
			forwardHistory.Clear();
		}

		HideContextMenu();
		currentPath = nextPath;
		context.SaveValue( "path", "/" + string.Join( "/", currentPath ) );
		selectedPath = null;
		RefreshListing();
	}

	private void NavigateBack()
	{
		if ( backHistory.Count == 0 )
			return;

		forwardHistory.Push( currentPath.ToArray() );
		NavigateTo( backHistory.Pop(), false );
	}

	private void NavigateUp()
	{
		if ( currentPath.Count == 0 )
			return;

		NavigateTo( currentPath.Take( currentPath.Count - 1 ).ToArray() );
	}

	private void ShowContextMenu( string targetPath )
	{
		SelectPath( targetPath );
		contextMenuPath = targetPath;
		contextMenuHost.DeleteChildren( true );
		PositionContextMenuNearCursor();

		var menu = new Panel { Parent = contextMenuHost };
		menu.AddClass( "explorer-context-menu" );

		CreateContextMenuButton( menu, "Open", () =>
		{
			HideContextMenu();
			var item = PaneArchiveFileSystem.GetItems( archivePath, currentPath )
				.FirstOrDefault( x => x.VirtualPath.Equals( targetPath, StringComparison.OrdinalIgnoreCase ) );
			if ( item is not null )
				ActivateItem( item );
		} );
		CreateContextMenuButton( menu, "Rename", PromptRenameSelected );
		CreateContextMenuButton( menu, "New File/Folder", PromptForCreate );
		if ( IsRecycleBinPath( targetPath ) )
			CreateContextMenuButton( menu, "Restore", RestoreSelected );
		CreateContextMenuButton( menu, "Delete", DeleteSelected );
	}

	private void ShowFolderContextMenu()
	{
		selectedPath = null;
		contextMenuPath = null;
		contextMenuHost.DeleteChildren( true );
		SyncSelectionStyles();
		PositionContextMenuNearCursor();

		var menu = new Panel { Parent = contextMenuHost };
		menu.AddClass( "explorer-context-menu" );
		CreateContextMenuButton( menu, "New File/Folder", PromptForCreate );
	}

	private void HideContextMenu()
	{
		contextMenuPath = null;
		contextMenuHost.DeleteChildren( true );
	}

	private void CreateContextMenuButton( Panel parent, string label, Action onClick )
	{
		var button = new Button( label ) { Parent = parent };
		button.AddClass( "explorer-context-button" );
		button.AddEventListener( "onclick", onClick );
	}

	private static string BuildChildVirtualPath( IReadOnlyList<string> parentPath, string childName )
	{
		return "/" + string.Join( "/", parentPath.Append( childName ) );
	}

	private void NavigateForward()
	{
		if ( forwardHistory.Count == 0 )
			return;

		backHistory.Push( currentPath.ToArray() );
		NavigateTo( forwardHistory.Pop(), false );
	}

	private static IReadOnlyList<string> ParsePath( string value )
	{
		return value
			.Trim()
			.TrimStart( '/' )
			.Split( '/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries );
	}

	private static bool IsRecycleBinPath( string virtualPath )
	{
		return IsRecycleBinPath( ParsePath( virtualPath ) );
	}

	private static bool IsRecycleBinPath( IReadOnlyList<string> path )
	{
		return path.Count >= RecycleBinPath.Count && path.Take( RecycleBinPath.Count )
			.SequenceEqual( RecycleBinPath, StringComparer.OrdinalIgnoreCase );
	}

	private void CreateItemIcon( Panel parent, PaneArchiveItem item )
	{
		var icon = new Label( ResolveFallbackIconText( item ) ) { Parent = parent };
		icon.AddClass( "explorer-item-icon" );

		var texturePath = ResolveTexturePath( item );
		if ( string.IsNullOrWhiteSpace( texturePath ) )
			return;

		icon.Text = "";
		icon.AddClass( "has-texture" );
		icon.Style.SetBackgroundImage( texturePath );
	}

	private string ResolveTexturePath( PaneArchiveItem item )
	{
		if ( item.IsDirectory )
		{
			if ( IsRecycleBinPath( item.VirtualPath ) && ParsePath( item.VirtualPath ).Count == RecycleBinPath.Count )
			{
				var recycleTexture = TryResolveTexturePath( "App_recycleBin" );
				if ( !string.IsNullOrWhiteSpace( recycleTexture ) )
					return recycleTexture;
			}

			return TryResolveTexturePath( "folder" );
		}

		if ( item.Extension.Equals( ".exe", StringComparison.OrdinalIgnoreCase ) )
		{
			var app = context.Runtime.Apps.FirstOrDefault( x => x.ResolvedExecutableName.Equals( item.Name, StringComparison.OrdinalIgnoreCase ) );
			if ( app is not null )
			{
				var appTexture = ResolveAppTexturePath( app );
				if ( !string.IsNullOrWhiteSpace( appTexture ) )
					return appTexture;
			}
		}

		var extensionTextureName = $"Ext_{item.Extension.TrimStart( '.' ).ToLowerInvariant()}";
		return TryResolveTexturePath( extensionTextureName );
	}

	private string TryResolveTexturePath( string textureName )
	{
		if ( string.IsNullOrWhiteSpace( textureName ) )
			return "";

		var themeName = string.IsNullOrWhiteSpace( context.Computer.ThemeName )
			? "default"
			: context.Computer.ThemeName.Trim();
		var path = $"textures/themes/{themeName}/{textureName}.png";
		try
		{
			return FileSystem.Mounted.FileExists( path ) ? path : "";
		}
		catch ( Exception )
		{
			return "";
		}
	}

	private static string ResolveAppTextureKey( ComputerAppDescriptor app )
	{
		return app.Id switch
		{
			"system.about" => "about",
			"system.calculator" => "calculator",
			"system.settings" => "controlPanel",
			"system.notepad" => "notepad",
			"system.paint" => "paint",
			"system.paneexplorer" => "paneExplorer",
			"system.ridge" => "ridge",
			"system.taskmanager" => "taskManager",
			_ => Path.GetFileNameWithoutExtension( app.ResolvedExecutableName )
		};
	}

	private string ResolveAppTexturePath( ComputerAppDescriptor app )
	{
		if ( app.Id.Equals( "system.mediaplayer", StringComparison.OrdinalIgnoreCase ) )
			return TryResolveTexturePath( "Ext_mp4" );

		return TryResolveTexturePath( $"App_{ResolveAppTextureKey( app )}" );
	}

	private static string ResolveFallbackIconText( PaneArchiveItem item )
	{
		if ( item.IsDirectory )
			return "FD";

		var extension = item.Extension.TrimStart( '.' ).ToUpperInvariant();
		if ( !string.IsNullOrWhiteSpace( extension ) )
			return extension.Length <= 3 ? extension : extension[..3];

		return "FI";
	}

	private bool IsAtRootPath()
	{
		return currentPath.Count == 0;
	}

	private bool IsAtDocumentsPath()
	{
		return PathEquals( currentPath, documentsPath );
	}

	private bool IsAtRecycleBinPath()
	{
		return IsRecycleBinPath( currentPath );
	}

	private static bool PathEquals( IReadOnlyList<string> left, IReadOnlyList<string> right )
	{
		return left.Count == right.Count && left.SequenceEqual( right, StringComparer.OrdinalIgnoreCase );
	}

	private static void AddCell( Panel row, string text, string className, bool header = false )
	{
		var cell = new Label( text ) { Parent = row };
		cell.AddClass( className );
		if ( header )
			cell.AddClass( "explorer-header-cell" );
	}

	private static void AddCell( Panel row, string text, bool header = false )
	{
		AddCell( row, text, "explorer-cell", header );
	}

	private static string FormatSize( long bytes )
	{
		if ( bytes <= 0 )
			return "0 B";

		if ( bytes < 1024 )
			return $"{bytes} B";

		if ( bytes < 1024 * 1024 )
			return $"{bytes / 1024f:0.0} KB";

		return $"{bytes / 1024f / 1024f:0.0} MB";
	}

	private void SelectPath( string virtualPath )
	{
		selectedPath = virtualPath;
		SyncSelectionStyles();
	}

	private void ClearSelectionAndHideContextMenu()
	{
		selectedPath = null;
		SyncSelectionStyles();
		HideContextMenu();
	}

	private void SyncSelectionStyles()
	{
		foreach ( var row in rowByPath )
			row.Value.SetClass( "selected", string.Equals( selectedPath, row.Key, StringComparison.OrdinalIgnoreCase ) );
	}

	private void PositionContextMenuNearCursor()
	{
		var position = MousePosition;
		contextMenuHost.Style.Left = Length.Pixels( MathF.Max( 4f, position.x + 12f ) );
		contextMenuHost.Style.Top = Length.Pixels( MathF.Max( 4f, position.y + 4f ) );
	}
}

public sealed class ExplorerItemRow : Panel
{
	private readonly string virtualPath;
	private readonly bool isDirectory;
	private readonly Action select;
	private readonly Action activate;
	private readonly Action openContextMenu;

	public ExplorerItemRow( string virtualPath, bool isDirectory, Action select, Action activate, Action openContextMenu )
	{
		this.virtualPath = virtualPath;
		this.isDirectory = isDirectory;
		this.select = select;
		this.activate = activate;
		this.openContextMenu = openContextMenu;
	}

	public override bool WantsDrag => !isDirectory;

	protected override void OnMouseDown( MousePanelEvent e )
	{
		base.OnMouseDown( e );

		if ( e.Button == "mouseright" )
			openContextMenu();
		else
			select();

		e.StopPropagation();
	}

	protected override void OnDoubleClick( MousePanelEvent e )
	{
		base.OnDoubleClick( e );

		if ( e.Button == "mouseleft" )
			activate();

		e.StopPropagation();
	}

	protected override void OnDragStart( DragEvent e )
	{
		base.OnDragStart( e );
		ComputerUiDragState.BeginDrag( virtualPath );
	}
}

public sealed class ExplorerListPanel : Panel
{
	private readonly Action openBackgroundContextMenu;
	private readonly Action hideContextMenu;

	public ExplorerListPanel( Action openBackgroundContextMenu, Action hideContextMenu )
	{
		this.openBackgroundContextMenu = openBackgroundContextMenu;
		this.hideContextMenu = hideContextMenu;
	}

	protected override void OnMouseDown( MousePanelEvent e )
	{
		base.OnMouseDown( e );

		if ( e.Button == "mouseright" )
			openBackgroundContextMenu();
		else if ( e.Button == "mouseleft" )
			hideContextMenu();

		e.StopPropagation();
	}
}
