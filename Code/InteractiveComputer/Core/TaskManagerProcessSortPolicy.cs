using System;
using System.Collections.Generic;
using System.Linq;

namespace PaneOS.InteractiveComputer.Core;

public enum TaskManagerProcessSortField
{
	Process,
	Status,
	Cpu,
	Ram,
	Startup
}

public sealed class TaskManagerProcessSortItem
{
	public string InstanceId { get; set; } = "";
	public string ProcessName { get; set; } = "";
	public string Status { get; set; } = "";
	public float CpuPercent { get; set; }
	public float RamPercent { get; set; }
	public bool StartupProcess { get; set; }
}

public static class TaskManagerProcessSortPolicy
{
	public static IReadOnlyList<TaskManagerProcessSortItem> Sort( IEnumerable<TaskManagerProcessSortItem> items, TaskManagerProcessSortField field, bool descending )
	{
		var ordered = field switch
		{
			TaskManagerProcessSortField.Status => descending
				? items.OrderByDescending( x => x.Status, StringComparer.OrdinalIgnoreCase ).ThenBy( x => x.ProcessName, StringComparer.OrdinalIgnoreCase )
				: items.OrderBy( x => x.Status, StringComparer.OrdinalIgnoreCase ).ThenBy( x => x.ProcessName, StringComparer.OrdinalIgnoreCase ),
			TaskManagerProcessSortField.Cpu => descending
				? items.OrderByDescending( x => x.CpuPercent ).ThenBy( x => x.ProcessName, StringComparer.OrdinalIgnoreCase )
				: items.OrderBy( x => x.CpuPercent ).ThenBy( x => x.ProcessName, StringComparer.OrdinalIgnoreCase ),
			TaskManagerProcessSortField.Ram => descending
				? items.OrderByDescending( x => x.RamPercent ).ThenBy( x => x.ProcessName, StringComparer.OrdinalIgnoreCase )
				: items.OrderBy( x => x.RamPercent ).ThenBy( x => x.ProcessName, StringComparer.OrdinalIgnoreCase ),
			TaskManagerProcessSortField.Startup => descending
				? items.OrderByDescending( x => x.StartupProcess ).ThenBy( x => x.ProcessName, StringComparer.OrdinalIgnoreCase )
				: items.OrderBy( x => x.StartupProcess ).ThenBy( x => x.ProcessName, StringComparer.OrdinalIgnoreCase ),
			_ => descending
				? items.OrderByDescending( x => x.ProcessName, StringComparer.OrdinalIgnoreCase )
				: items.OrderBy( x => x.ProcessName, StringComparer.OrdinalIgnoreCase )
		};

		return ordered.ToArray();
	}
}
