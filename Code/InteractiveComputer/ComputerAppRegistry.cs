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
			DefaultWindowWidth = 560,
			DefaultWindowHeight = 420,
			StorageSpaceUsedGb = 0.08f,
			ExpectedCoreCountUsageAvg = 0.08f,
			ExpectedAvgCpuCoreUsagePercent = 7f,
			ExpectedAvgRamUsageMb = 88f,
			ExpectedAvgGpuCoreUsagePercent = 0.5f,
			ExpectedAvgGpuVramUsagePercent = 0.5f,
			SingleInstance = false,
			Factory = () => new AboutComputerApp()
		},
		new ComputerAppDescriptor
		{
			Id = "system.settings",
			Title = "Control Panel",
			Icon = "CP",
			SortOrder = 5,
			DefaultWindowWidth = 640,
			DefaultWindowHeight = 560,
			StorageSpaceUsedGb = 0.09f,
			ExpectedCoreCountUsageAvg = 0.14f,
			ExpectedAvgCpuCoreUsagePercent = 8f,
			ExpectedAvgRamUsageMb = 94f,
			ExpectedAvgGpuCoreUsagePercent = 0.4f,
			ExpectedAvgGpuVramUsagePercent = 0.4f,
			SingleInstance = true,
			Factory = () => new SettingsApp()
		},
		new ComputerAppDescriptor
		{
			Id = "system.notepad",
			Title = "Notepad",
			Icon = "NP",
			SortOrder = 10,
			DefaultWindowWidth = 680,
			DefaultWindowHeight = 520,
			StorageSpaceUsedGb = 0.06f,
			ExpectedCoreCountUsageAvg = 0.1f,
			ExpectedAvgCpuCoreUsagePercent = 8f,
			ExpectedAvgRamUsageMb = 72f,
			ExpectedAvgGpuCoreUsagePercent = 0.25f,
			ExpectedAvgGpuVramUsagePercent = 0.25f,
			AssociatedFileExtensions = new[] { ".txt", ".log", ".md", ".json" },
			SingleInstance = false,
			Factory = () => new NotepadApp()
		},
		new ComputerAppDescriptor
		{
			Id = "system.ridge",
			Title = "Ridge",
			Icon = "RG",
			SortOrder = 15,
			DefaultWindowWidth = 760,
			DefaultWindowHeight = 520,
			StorageSpaceUsedGb = 0.24f,
			ExpectedCoreCountUsageAvg = 0.3f,
			ExpectedAvgCpuCoreUsagePercent = 14f,
			ExpectedAvgRamUsageMb = 196f,
			ExpectedAvgGpuCoreUsagePercent = 4f,
			ExpectedAvgGpuVramUsagePercent = 5f,
			ChanceToStopRespondingPerMinute = 0.006f,
			ChanceOfMemoryLeakPerMinute = 0.01f,
			AssociatedFileExtensions = new[] { ".url", ".html", ".htm" },
			SingleInstance = false,
			Factory = () => new RidgeBrowserApp()
		},
		new ComputerAppDescriptor
		{
			Id = "system.paneexplorer",
			Title = "Pane Explorer",
			Icon = "PE",
			SortOrder = 18,
			DefaultWindowWidth = 720,
			DefaultWindowHeight = 500,
			StorageSpaceUsedGb = 0.18f,
			ExpectedCoreCountUsageAvg = 0.2f,
			ExpectedAvgCpuCoreUsagePercent = 9f,
			ExpectedAvgRamUsageMb = 132f,
			ExpectedAvgGpuCoreUsagePercent = 1.2f,
			ExpectedAvgGpuVramUsagePercent = 1f,
			SingleInstance = false,
			Factory = () => new PaneExplorerApp()
		},
		new ComputerAppDescriptor
		{
			Id = "system.taskmanager",
			Title = "Task Manager",
			Icon = "TM",
			SortOrder = 20,
			DefaultWindowWidth = 720,
			DefaultWindowHeight = 500,
			StorageSpaceUsedGb = 0.09f,
			ExpectedCoreCountUsageAvg = 0.2f,
			ExpectedAvgCpuCoreUsagePercent = 11f,
			ExpectedAvgRamUsageMb = 118f,
			ExpectedAvgGpuCoreUsagePercent = 1f,
			ExpectedAvgGpuVramUsagePercent = 1f,
			Factory = () => new TaskManagerApp()
		},
		new ComputerAppDescriptor
		{
			Id = "system.calculator",
			Title = "Calculator",
			Icon = "CA",
			SortOrder = 22,
			DefaultWindowWidth = 300,
			DefaultWindowHeight = 330,
			StorageSpaceUsedGb = 0.04f,
			ExpectedCoreCountUsageAvg = 0.08f,
			ExpectedAvgCpuCoreUsagePercent = 5f,
			ExpectedAvgRamUsageMb = 58f,
			ExpectedAvgGpuCoreUsagePercent = 0.15f,
			ExpectedAvgGpuVramUsagePercent = 0.15f,
			SingleInstance = false,
			Factory = () => new CalculatorApp()
		},
		new ComputerAppDescriptor
		{
			Id = "system.paint",
			Title = "Paint",
			Icon = "PT",
			SortOrder = 24,
			DefaultWindowWidth = 700,
			DefaultWindowHeight = 520,
			StorageSpaceUsedGb = 0.07f,
			ExpectedCoreCountUsageAvg = 0.16f,
			ExpectedAvgCpuCoreUsagePercent = 8f,
			ExpectedAvgRamUsageMb = 104f,
			ExpectedAvgGpuCoreUsagePercent = 2f,
			ExpectedAvgGpuVramUsagePercent = 1.5f,
			AssociatedFileExtensions = new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif" },
			SingleInstance = false,
			Factory = () => new PaintApp()
		},
		new ComputerAppDescriptor
		{
			Id = "system.mediaplayer",
			Title = "Media Player",
			Icon = "MP",
			SortOrder = 26,
			DefaultWindowWidth = 640,
			DefaultWindowHeight = 420,
			StorageSpaceUsedGb = 0.11f,
			ExpectedCoreCountUsageAvg = 0.2f,
			ExpectedAvgCpuCoreUsagePercent = 10f,
			ExpectedAvgRamUsageMb = 126f,
			ExpectedAvgGpuCoreUsagePercent = 2.5f,
			ExpectedAvgGpuVramUsagePercent = 2f,
			AssociatedFileExtensions = new[] { ".mp3", ".wav", ".ogg", ".mp4", ".webm", ".avi" },
			SingleInstance = false,
			Factory = () => new MediaPlayerApp()
		},
		new ComputerAppDescriptor
		{
			Id = "system.paneos32",
			Title = "PaneOS32",
			Icon = "32",
			SortOrder = 1000,
			ShowOnDesktop = false,
			ShowInStartMenu = false,
			ShowInTaskbar = false,
			HasWindow = false,
			IsBackgroundProcess = true,
			RunOnStartup = true,
			StorageSpaceUsedGb = 0.9f,
			ExpectedCoreCountUsageAvg = 1f,
			ExpectedAvgCpuCoreUsagePercent = 72f,
			ExpectedAvgRamUsageMb = 368f,
			ExpectedAvgRamUsagePercentOverride = 18f,
			ExpectedAvgGpuCoreUsagePercent = 0.2f,
			ExpectedAvgGpuVramUsagePercent = 0.2f,
			Factory = () => new BackgroundProcessApp( "PaneOS32.exe", "32" )
		},
		new ComputerAppDescriptor
		{
			Id = "system.networking",
			Title = "Networking",
			Icon = "NW",
			SortOrder = 1001,
			ShowOnDesktop = false,
			ShowInStartMenu = false,
			ShowInTaskbar = false,
			HasWindow = false,
			IsBackgroundProcess = true,
			RunOnStartup = true,
			StorageSpaceUsedGb = 0.14f,
			ExpectedCoreCountUsageAvg = 0.12f,
			ExpectedAvgCpuCoreUsagePercent = 55f,
			ExpectedAvgRamUsageMb = 150f,
			ExpectedAvgGpuCoreUsagePercent = 0f,
			ExpectedAvgGpuVramUsagePercent = 0f,
			Factory = () => new BackgroundProcessApp( "Networking.exe", "NW" )
		},
		new ComputerAppDescriptor
		{
			Id = "system.pvchost",
			Title = "PvcHost",
			Icon = "PV",
			SortOrder = 1002,
			ShowOnDesktop = false,
			ShowInStartMenu = false,
			ShowInTaskbar = false,
			HasWindow = false,
			IsBackgroundProcess = true,
			RunOnStartup = true,
			StorageSpaceUsedGb = 0.12f,
			ExpectedCoreCountUsageAvg = 0.1f,
			ExpectedAvgCpuCoreUsagePercent = 58f,
			ExpectedAvgRamUsageMb = 160f,
			ExpectedAvgGpuCoreUsagePercent = 0f,
			ExpectedAvgGpuVramUsagePercent = 0f,
			Factory = () => new BackgroundProcessApp( "PvcHost.exe", "PV" )
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
