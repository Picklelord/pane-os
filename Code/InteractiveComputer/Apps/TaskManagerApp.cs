using Sandbox.UI;
using PaneOS.InteractiveComputer;

namespace PaneOS.InteractiveComputer.Apps;

[ComputerApp( "system.taskmanager", "Task Manager", Icon = "TM", SortOrder = 20 )]
public sealed class TaskManagerApp : IComputerApp
{
	public ComputerAppSession Run( ComputerAppContext context )
	{
		return new ComputerAppSession
		{
			Title = "Windows Task Manager",
			Icon = "TM",
			Content = new TaskManagerPanel( context )
		};
	}
}

[StyleSheet( "Code/InteractiveComputer/Apps/InteractiveComputerApps.scss" )]
public sealed class TaskManagerPanel : Panel
{
	private readonly ComputerAppContext context;
	private int lastRuntimeVersion = -1;

	public TaskManagerPanel( ComputerAppContext context )
	{
		this.context = context;
		AddClass( "task-manager-app app-document" );
	}

	public override void Tick()
	{
		base.Tick();

		if ( lastRuntimeVersion == context.Runtime.Version )
			return;

		lastRuntimeVersion = context.Runtime.Version;
		DeleteChildren( true );

		new Label( "Applications" ) { Parent = this }.AddClass( "app-heading" );

		foreach ( var app in context.Runtime.OpenApps )
		{
			var row = new Panel { Parent = this };
			row.AddClass( "task-row" );
			new Label( app.State.Title ) { Parent = row };
			new Label( app.State.IsMinimized ? "Minimized" : "Running" ) { Parent = row };
		}
	}
}
