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

[StyleSheet( "Code/InteractiveComputer/Apps/InteractiveComputerApps.scss" )]
public sealed class NotepadPanel : Panel
{
	private readonly ComputerAppContext context;
	private readonly TextEntry textEntry;

	public NotepadPanel( ComputerAppContext context )
	{
		this.context = context;
		AddClass( "notepad-app" );

		var toolbar = new Panel { Parent = this };
		toolbar.AddClass( "notepad-toolbar" );
		new Label( "File" ) { Parent = toolbar };
		new Label( "Edit" ) { Parent = toolbar };
		new Label( "Format" ) { Parent = toolbar };
		new Label( "Help" ) { Parent = toolbar };

		textEntry = new TextEntry
		{
			Parent = this,
			Text = context.LoadValue( "text" ) ?? "This note is saved in the app state between computer interactions."
		};
		textEntry.AddClass( "notepad-text" );
	}

	public override void Tick()
	{
		base.Tick();

		if ( context.LoadValue( "text" ) == textEntry.Text )
			return;

		context.SaveValue( "text", textEntry.Text );
	}
}
