using System;
using System.Linq;
using Sandbox.UI;
using PaneOS.InteractiveComputer;

namespace PaneOS.InteractiveComputer.Apps;

[ComputerApp( "system.notepad", "Notepad", Icon = "NP", SortOrder = 10 )]
public sealed class NotepadApp : IComputerApp
{
	public ComputerAppSession Run( ComputerAppContext context )
	{
		return new ComputerAppSession
		{
			Title = "Untitled - Notepad",
			Icon = "NP",
			Content = new NotepadPanel( context )
		};
	}
}

[StyleSheet( "InteractiveComputerApps.scss" )]
public sealed class NotepadPanel : ComputerWarmupPanel
{
	private readonly ComputerAppContext context;
	private ComputerInputAwareTextEntry textEntry = null!;
	private string currentFilePath;

	public NotepadPanel( ComputerAppContext context )
	{
		this.context = context;
		AddClass( "notepad-app" );
		BuildUi();
	}

	protected override void WarmupRefresh()
	{
		BuildUi();
	}

	private void BuildUi()
	{
		var currentText = textEntry?.Text;
		var caretPosition = textEntry?.CaretPosition ?? 0;
		DeleteChildren( true );

		var toolbar = new Panel { Parent = this };
		toolbar.AddClass( "notepad-toolbar" );

		var openButton = new Button( "Open" ) { Parent = toolbar };
		openButton.AddClass( "notepad-toolbar-button" );
		openButton.AddEventListener( "onclick", OpenFile );

		var saveButton = new Button( "Save" ) { Parent = toolbar };
		saveButton.AddClass( "notepad-toolbar-button" );
		saveButton.AddEventListener( "onclick", SaveFile );

		var saveAsButton = new Button( "Save As" ) { Parent = toolbar };
		saveAsButton.AddClass( "notepad-toolbar-button" );
		saveAsButton.AddEventListener( "onclick", SaveFileAs );

		textEntry = new ComputerInputAwareTextEntry( () => context.Runtime.ShouldBlockInput( context.State.InstanceId ) )
		{
			Parent = this,
			Text = currentText ?? "",
			Multiline = true,
			Placeholder = ""
		};
		textEntry.AddClass( "notepad-text" );

		if ( currentText is null )
		{
			LoadInitialDocument();
		}
		else
		{
			textEntry.CaretPosition = Math.Clamp( caretPosition, 0, textEntry.TextLength );
		}
	}

	public override void Tick()
	{
		base.Tick();

		if ( context.LoadValue( "text" ) == textEntry.Text )
			return;

		context.SaveValue( "text", textEntry.Text );
	}

	private void LoadInitialDocument()
	{
		currentFilePath = context.LoadValue( "file_path" ) ?? "";
		textEntry.Text = string.IsNullOrWhiteSpace( currentFilePath )
			? context.LoadValue( "text" ) ?? ""
			: context.ReadTextFile( currentFilePath );
		textEntry.CaretPosition = textEntry.TextLength;
		textEntry.Focus();
	}

	private void OpenFile()
	{
		context.ShowOpenFileDialog(
			new ComputerFileDialogOptions
			{
				Title = "Open Text File",
				InitialPath = context.GetDefaultDocumentsPath(),
				AllowedExtensions = new[] { "txt" },
				ConfirmButtonText = "Open"
			},
			result =>
			{
				if ( !result.Confirmed )
					return;

				currentFilePath = result.VirtualPath;
				context.SaveValue( "file_path", currentFilePath );
				textEntry.Text = context.ReadTextFile( currentFilePath );
				textEntry.CaretPosition = textEntry.TextLength;
				textEntry.Focus();
			} );
	}

	private void SaveFile()
	{
		if ( string.IsNullOrWhiteSpace( currentFilePath ) )
		{
			SaveFileAs();
			return;
		}

		context.WriteTextFile( currentFilePath, textEntry.Text );
		context.SaveValue( "file_path", currentFilePath );
	}

	private void SaveFileAs()
	{
		context.ShowSaveFileDialog(
			new ComputerFileDialogOptions
			{
				Title = "Save Text File",
				InitialPath = context.GetDefaultDocumentsPath(),
				DefaultFileName = string.IsNullOrWhiteSpace( currentFilePath ) ? "Untitled.txt" : currentFilePath.Split( '/' ).Last(),
				AllowedExtensions = new[] { "txt" },
				ConfirmButtonText = "Save"
			},
			result =>
			{
				if ( !result.Confirmed )
					return;

				currentFilePath = EnsureTxtExtension( result.VirtualPath );
				context.WriteTextFile( currentFilePath, textEntry.Text );
				context.SaveValue( "file_path", currentFilePath );
				textEntry.Focus();
			} );
	}

	private static string EnsureTxtExtension( string virtualPath )
	{
		return virtualPath.EndsWith( ".txt", StringComparison.OrdinalIgnoreCase )
			? virtualPath
			: $"{virtualPath}.txt";
	}
}
