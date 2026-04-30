using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PaneOS.InteractiveComputer.Core;
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

		var toolbar = new Panel { Parent = this };
		toolbar.AddClass( "explorer-toolbar" );

		CreateToolbarButton( toolbar, "Back", NavigateBack );
		CreateToolbarButton( toolbar, "Forward", NavigateForward );
		CreateToolbarButton( toolbar, "Up", NavigateUp );
		CreateToolbarButton( toolbar, "My PC", () => NavigateTo( Array.Empty<string>() ) );
		CreateToolbarButton( toolbar, "My Documents", () => NavigateTo( documentsPath ) );
		CreateToolbarButton( toolbar, "Rename", PromptRenameSelected );
		CreateToolbarButton( toolbar, "Add Folder", PromptForCreate );
		CreateToolbarButton( toolbar, "Restore Selected", RestoreSelected );
		CreateToolbarButton( toolbar, "Delete Selected", DeleteSelected );

		pathLabel = new Label { Parent = this };
		pathLabel.AddClass( "explorer-path" );

		var table = new Panel { Parent = this };
		table.AddClass( "explorer-table" );

		var header = new Panel { Parent = table };
		header.AddClass( "explorer-row explorer-header" );
		AddCell( header, "Name", true );
		AddCell( header, "Type", true );
		AddCell( header, "Size", true );

		listHost = new Panel { Parent = table };
		listHost.AddClass( "explorer-list" );

		contextMenuHost = new Panel { Parent = this };
		contextMenuHost.AddClass( "explorer-context-menu-host" );

		RefreshListing();
	}

	private void CreateToolbarButton( Panel parent, string label, Action onClick )
	{
		var button = new Button( label ) { Parent = parent };
		button.AddClass( "explorer-button" );
		button.AddEventListener( "onclick", onClick );
	}

	private void PromptForCreate()
	{
		HideContextMenu();
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

				var input = result.TextValue.Trim();
				if ( string.IsNullOrWhiteSpace( input ) )
					return;

				var extension = Path.GetExtension( input );
				if ( string.IsNullOrWhiteSpace( extension ) )
				{
					PaneArchiveFileSystem.CreateFolder( archivePath, currentPath, input );
				}
				else
				{
					var fileName = Path.GetFileNameWithoutExtension( input );
					PaneArchiveFileSystem.CreateFile( archivePath, currentPath, fileName, extension.TrimStart( '.' ) );
				}

				context.Runtime.RefreshTransientUi();
				RefreshListing();
			} );
	}

	private void PromptRenameSelected()
	{
		var targetPath = selectedPath ?? contextMenuPath;
		if ( string.IsNullOrWhiteSpace( targetPath ) )
			return;

		HideContextMenu();
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

				var newName = result.TextValue.Trim();
				if ( string.IsNullOrWhiteSpace( newName ) )
					return;

				PaneArchiveFileSystem.Rename( archivePath, ParsePath( targetPath ), newName );
				selectedPath = BuildChildVirtualPath( currentPath, newName );
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

		foreach ( var item in PaneArchiveFileSystem.GetItems( archivePath, currentPath ) )
		{
			var row = new ExplorerItemRow( item.VirtualPath, item.IsDirectory ) { Parent = listHost };
			row.AddClass( "explorer-row" );
			rowByPath[item.VirtualPath] = row;
			row.SetClass( "selected", string.Equals( selectedPath, item.VirtualPath, StringComparison.OrdinalIgnoreCase ) );
			row.AddEventListener( "onclick", () =>
			{
				SelectPath( item.VirtualPath );
			} );
			row.AddEventListener( "oncontextmenu", () => ShowContextMenu( item.VirtualPath ) );
			row.AddEventListener( "ondblclick", () => ActivateItem( item ) );

			var nameCell = new Panel { Parent = row };
			nameCell.AddClass( "explorer-cell explorer-name-cell" );
			var icon = new Label( item.IsDirectory ? "FD" : "FI" ) { Parent = nameCell };
			icon.AddClass( "explorer-item-icon" );
			new Label( item.Name ) { Parent = nameCell }.AddClass( "explorer-item-name" );
			AddCell( row, item.IsDirectory ? "Folder" : item.Extension.TrimStart( '.' ).ToUpperInvariant() + " File" );
			AddCell( row, item.IsDirectory ? "-" : FormatSize( item.SizeBytes ) );
		}

		SyncSelectionStyles();
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
		if ( IsRecycleBinPath( targetPath ) )
			CreateContextMenuButton( menu, "Restore", RestoreSelected );
		CreateContextMenuButton( menu, "Delete", DeleteSelected );
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
		return virtualPath.StartsWith( "/C:/Recycle Bin/", StringComparison.OrdinalIgnoreCase );
	}

	private static void AddCell( Panel row, string text, bool header = false )
	{
		new Label( text ) { Parent = row }.AddClass( header ? "explorer-cell explorer-header-cell" : "explorer-cell" );
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

	private void SyncSelectionStyles()
	{
		foreach ( var row in rowByPath )
			row.Value.SetClass( "selected", string.Equals( selectedPath, row.Key, StringComparison.OrdinalIgnoreCase ) );
	}
}

public sealed class ExplorerItemRow : Panel
{
	private readonly string virtualPath;
	private readonly bool isDirectory;

	public ExplorerItemRow( string virtualPath, bool isDirectory )
	{
		this.virtualPath = virtualPath;
		this.isDirectory = isDirectory;
	}

	public override bool WantsDrag => !isDirectory;

	protected override void OnDragStart( DragEvent e )
	{
		base.OnDragStart( e );
		ComputerUiDragState.BeginDrag( virtualPath );
	}
}
