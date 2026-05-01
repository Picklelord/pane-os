using System;
using System.Collections.Generic;
using System.Linq;

namespace PaneOS.InteractiveComputer;

public sealed class ComputerAppDescriptor
{
	public required string Id { get; init; }
	public required string Title { get; init; }
	public string Icon { get; init; } = "[]";
	public int SortOrder { get; init; }
	public string? ExecutableName { get; init; }
	public float StorageSpaceUsedGb { get; init; } = 0.02f;
	public float ExpectedCoreCountUsageAvg { get; init; } = 0.1f;
	public float ExpectedAvgCpuCoreUsagePercent { get; init; } = 5f;
	public float ExpectedAvgRamUsageMb { get; init; } = 64f;
	public float? ExpectedAvgRamUsagePercentOverride { get; init; }
	public float ExpectedAvgGpuCoreUsagePercent { get; init; } = 1f;
	public float ExpectedAvgGpuVramUsagePercent { get; init; } = 1f;
	public float ChanceToStopRespondingPerMinute { get; init; }
	public float ChanceOfMemoryLeakPerMinute { get; init; }
	public bool ShowOnDesktop { get; init; } = true;
	public bool ShowInStartMenu { get; init; } = true;
	public bool ShowInTaskbar { get; init; } = true;
	public bool ShowInControlPanel { get; init; } = true;
	public bool HasWindow { get; init; } = true;
	public bool RunOnStartup { get; init; }
	public bool IsBackgroundProcess { get; init; }
	public bool SingleInstance { get; init; } = true;
	public bool CanUninstallFromControlPanel { get; init; } = true;
	public bool AllowControlPanelWindowSizing { get; init; } = true;
	public IReadOnlyList<string> AssociatedFileExtensions { get; init; } = Array.Empty<string>();
	public int DefaultWindowOffsetX { get; init; } = 70;
	public int DefaultWindowOffsetY { get; init; } = 54;
	public int? DefaultWindowWidth { get; init; }
	public int? DefaultWindowHeight { get; init; }
	public required Func<IComputerApp> Factory { get; init; }

	public string ResolvedExecutableName => !string.IsNullOrWhiteSpace( ExecutableName )
		? ExecutableName!
		: $"{new string( Title.Where( x => !char.IsWhiteSpace( x ) ).ToArray() )}.exe";

	public IComputerApp Create()
	{
		return Factory();
	}
}
