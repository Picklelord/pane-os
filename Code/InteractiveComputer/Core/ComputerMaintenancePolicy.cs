using System;
using System.Collections.Generic;
using System.Linq;

namespace PaneOS.InteractiveComputer.Core;

public sealed class ComputerMaintenanceRecord
{
	public string Title { get; init; } = "";
	public string Summary { get; init; } = "";
	public string NotificationTitle { get; init; } = "";
	public string NotificationMessage { get; init; } = "";
	public string FileName { get; init; } = "";
	public string FileContent { get; init; } = "";
}

public static class ComputerMaintenancePolicy
{
	public static ComputerMaintenanceRecord BuildUpdateScanRecord( ComputerState state, IReadOnlyList<ComputerAppDescriptor> apps, DateTime utcNow )
	{
		var installedAppCount = state.InstalledApps.Count;
		var runningProcessCount = state.OpenApps.Count;
		var hardware = state.Hardware;
		var summary = installedAppCount == 0
			? "PaneOS completed a quick system scan. No critical updates were needed."
			: $"PaneOS scanned {installedAppCount} installed apps and found no critical updates.";

		var lines = new List<string>
		{
			"PaneOS Update Report",
			$"Generated: {utcNow:O}",
			"",
			"Summary",
			summary,
			"",
			"System Snapshot",
			$"Installed apps: {installedAppCount}",
			$"Running processes: {runningProcessCount}",
			$"CPU: {hardware.CpuCoreCount} cores @ {hardware.CpuCoreGhz:0.##} GHz",
			$"RAM: {hardware.RamGb:0.##} GB",
			$"GPU: {hardware.GpuCoreGhz:0.##} GHz / {hardware.GpuVramGb:0.##} GB VRAM",
			"",
			"Registered Apps"
		};

		lines.AddRange( apps
			.OrderBy( x => x.Title, StringComparer.OrdinalIgnoreCase )
			.Select( x => $"- {x.Title} ({x.ResolvedExecutableName})" ) );

		return new ComputerMaintenanceRecord
		{
			Title = "PaneOS Update",
			Summary = summary + " A report was saved to My Documents.",
			NotificationTitle = "PaneOS Update",
			NotificationMessage = "System scan finished. The update report is ready in My Documents.",
			FileName = "PaneOS Update Report.txt",
			FileContent = string.Join( "\n", lines )
		};
	}

	public static ComputerMaintenanceRecord BuildPackageInstallRecord( string packageName, DateTime utcNow )
	{
		var safePackageName = string.IsNullOrWhiteSpace( packageName ) ? "Package" : packageName.Trim();
		var lines = new[]
		{
			$"{safePackageName} Setup Log",
			$"Generated: {utcNow:O}",
			"",
			"Status",
			"Package staged successfully.",
			"Core files copied to C:\\Apps.",
			"Shortcuts refreshed.",
			"No restart required."
		};

		return new ComputerMaintenanceRecord
		{
			Title = "Software Installation",
			Summary = $"{safePackageName} finished installing. A setup log was saved to My Documents.",
			NotificationTitle = "Install Complete",
			NotificationMessage = $"{safePackageName} is ready to use.",
			FileName = $"{safePackageName} Setup Log.txt",
			FileContent = string.Join( "\n", lines )
		};
	}
}
