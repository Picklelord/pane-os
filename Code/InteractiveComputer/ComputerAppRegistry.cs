using System;
using System.Collections.Generic;
using System.Linq;
using PaneOS.InteractiveComputer.Apps;

namespace PaneOS.InteractiveComputer;

public static class ComputerAppRegistry
{
	private static readonly List<ComputerAppDescriptor> registeredApps = new()
	{
		new ComputerAppDescriptor
		{
			Id = "system.about",
			Title = "My Computer",
			Icon = "PC",
			SortOrder = 0,
			Factory = () => new AboutComputerApp()
		},
		new ComputerAppDescriptor
		{
			Id = "system.notepad",
			Title = "Notepad",
			Icon = "NP",
			SortOrder = 10,
			Factory = () => new NotepadApp()
		},
		new ComputerAppDescriptor
		{
			Id = "system.ridge",
			Title = "Ridge",
			Icon = "RG",
			SortOrder = 15,
			Factory = () => new RidgeBrowserApp()
		},
		new ComputerAppDescriptor
		{
			Id = "system.taskmanager",
			Title = "Task Manager",
			Icon = "TM",
			SortOrder = 20,
			Factory = () => new TaskManagerApp()
		}
	};

	public static IReadOnlyList<ComputerAppDescriptor> Apps => registeredApps
		.OrderBy( x => x.SortOrder )
		.ThenBy( x => x.Title )
		.ToArray();

	public static void Refresh()
	{
	}

	public static void Register( ComputerAppDescriptor descriptor )
	{
		registeredApps.RemoveAll( x => x.Id == descriptor.Id );
		registeredApps.Add( descriptor );
	}
}
