using System;
using System.Collections.Generic;
using System.Linq;
using PaneOS.InteractiveComputer.Core;
using Sandbox.UI;

namespace PaneOS.InteractiveComputer.Apps;

[ComputerApp( "system.taskmanager", "Task Manager", Icon = "TM", SortOrder = 20 )]
public sealed class TaskManagerApp : IComputerApp
{
	public ComputerAppSession Run( ComputerAppContext context )
	{
		return new ComputerAppSession
		{
			Title = "Task Manager",
			Icon = "TM",
			Content = new TaskManagerPanel( context )
		};
	}
}

[StyleSheet( "InteractiveComputerApps.scss" )]
public sealed class TaskManagerPanel : ComputerWarmupPanel
{
	private readonly ComputerAppContext context;
	private readonly Panel contentHost;
	private readonly Button processesButton;
	private readonly Button performanceButton;
	private readonly Button storageButton;
	private TaskManagerTab activeTab = TaskManagerTab.Processes;
	private TaskManagerProcessSortField sortField = TaskManagerProcessSortField.Cpu;
	private bool sortDescending = true;
	private int lastVersion = -1;

	public TaskManagerPanel( ComputerAppContext context )
	{
		this.context = context;
		AddClass( "task-manager-app" );

		var tabs = new Panel { Parent = this };
		tabs.AddClass( "task-tabs" );

		processesButton = CreateTabButton( tabs, "Processes", TaskManagerTab.Processes );
		performanceButton = CreateTabButton( tabs, "Performance", TaskManagerTab.Performance );
		storageButton = CreateTabButton( tabs, "Storage", TaskManagerTab.Storage );

		contentHost = new Panel { Parent = this };
		contentHost.AddClass( "task-tab-content" );

		RenderActiveTab( true );
	}

	public override void Tick()
	{
		base.Tick();

		var refreshVersion = GetRefreshVersion();
		if ( lastVersion == refreshVersion )
			return;

		RenderActiveTab( false );
	}

	protected override void WarmupRefresh()
	{
		RenderActiveTab( true );
	}

	private Button CreateTabButton( Panel parent, string label, TaskManagerTab tab )
	{
		var button = new Button( label ) { Parent = parent };
		button.AddClass( "task-tab-button" );
		button.AddEventListener( "onclick", () =>
		{
			activeTab = tab;
			RequestWarmupRefresh();
			RenderActiveTab( true );
		} );
		return button;
	}

	private void RenderActiveTab( bool force )
	{
		var refreshVersion = GetRefreshVersion();
		if ( !force && lastVersion == refreshVersion )
			return;

		lastVersion = refreshVersion;
		processesButton.SetClass( "active", activeTab == TaskManagerTab.Processes );
		performanceButton.SetClass( "active", activeTab == TaskManagerTab.Performance );
		storageButton.SetClass( "active", activeTab == TaskManagerTab.Storage );
		contentHost.DeleteChildren( true );

		switch ( activeTab )
		{
			case TaskManagerTab.Processes:
				RenderProcessesTab();
				break;
			case TaskManagerTab.Performance:
				RenderPerformanceTab();
				break;
			case TaskManagerTab.Storage:
				RenderStorageTab();
				break;
		}
	}

	private void RenderProcessesTab()
	{
		var table = new Panel { Parent = contentHost };
		table.AddClass( "task-table" );
		AddProcessHeader( table );

		var rows = context.Runtime.OpenApps
			.Select( app =>
			{
				var metrics = context.Runtime.GetProcessMetrics( app.State.InstanceId );
				return new
				{
					App = app,
					Metrics = metrics,
					Item = new TaskManagerProcessSortItem
					{
						InstanceId = app.State.InstanceId,
						ProcessName = app.State.Title,
						Status = FormatStatus( context.Runtime.GetEffectiveStatus( app.State.InstanceId ) ),
						CpuPercent = metrics.CpuPercent,
						RamPercent = metrics.RamPercent,
						StartupProcess = app.Descriptor.RunOnStartup
					}
				};
			} )
			.ToArray();

		var sortedRows = TaskManagerProcessSortPolicy.Sort( rows.Select( x => x.Item ), sortField, sortDescending )
			.Join(
				rows,
				sorted => sorted.InstanceId,
				row => row.Item.InstanceId,
				( _, row ) => row,
				StringComparer.OrdinalIgnoreCase );

		foreach ( var entry in sortedRows )
		{
			var app = entry.App;
			var metrics = entry.Metrics;
			var row = new Panel { Parent = table };
			row.AddClass( "task-table-row" );
			row.SetClass( "not-responding", context.Runtime.GetEffectiveStatus( app.State.InstanceId ) == ComputerProcessStatus.NotResponding );

			AddCell( row, app.State.Title );
			AddCell( row, app.Descriptor.ResolvedExecutableName );
			AddCell( row, $"/C:/Apps/{app.Descriptor.Title}/{app.Descriptor.ResolvedExecutableName}" );
			AddCell( row, FormatStatus( context.Runtime.GetEffectiveStatus( app.State.InstanceId ) ) );
			AddCell( row, $"{metrics.CpuPercent:0.0}%" );
			AddCell( row, $"{metrics.RamPercent:0.0}%" );
			AddCell( row, app.Descriptor.RunOnStartup ? "Yes" : "No" );

			var cell = new Panel { Parent = row };
			cell.AddClass( "task-cell task-action-cell" );
			var killButton = new Button( "Kill" ) { Parent = cell };
			killButton.AddClass( "task-kill-button" );
			killButton.AddEventListener( "onclick", () =>
			{
				context.Runtime.Close( app.State.InstanceId );
				RenderActiveTab( true );
			} );
		}
	}

	private void RenderPerformanceTab()
	{
		var metrics = context.Runtime.Metrics;
		var grid = new Panel { Parent = contentHost };
		grid.AddClass( "perf-grid" );

		AddPerformanceCard( grid, "CPU Usage", $"{metrics.CpuPercent:0.0}%" );
		AddPerformanceCard( grid, "RAM Usage", $"{metrics.RamPercent:0.0}%" );
		AddPerformanceCard( grid, "GPU Core Usage", $"{metrics.GpuCorePercent:0.0}%" );
		AddPerformanceCard( grid, "GPU VRAM Usage", $"{metrics.GpuVramPercent:0.0}%" );
		AddSparkline( contentHost, "CPU History", context.Runtime.MetricHistory.CpuSamples );
		AddSparkline( contentHost, "RAM History", context.Runtime.MetricHistory.RamSamples );
		AddSparkline( contentHost, "GPU History", context.Runtime.MetricHistory.GpuSamples );
		AddSparkline( contentHost, "GPU VRAM History", context.Runtime.MetricHistory.GpuVramSamples );

		var note = new Label( $"Live hardware: {context.Runtime.State.Hardware.CpuCoreCount} cores @ {context.Runtime.State.Hardware.CpuCoreGhz:0.##} GHz, {context.Runtime.State.Hardware.RamGb:0.##} GB RAM" )
		{
			Parent = contentHost
		};
		note.AddClass( "task-storage-note" );
	}

	private void RenderStorageTab()
	{
		var metrics = context.Runtime.Metrics;
		var hardware = context.Runtime.State.Hardware;
		var breakdown = context.Runtime.GetStorageBreakdown();

		var driveCard = new Panel { Parent = contentHost };
		driveCard.AddClass( "storage-drive-card" );
		var driveHeader = new Panel { Parent = driveCard };
		driveHeader.AddClass( "storage-drive-header" );
		new Label( "C:" ) { Parent = driveHeader }.AddClass( "storage-drive-title" );
		new Label( "Primary Drive" ) { Parent = driveHeader }.AddClass( "storage-drive-subtitle" );
		var driveStats = new Panel { Parent = driveCard };
		driveStats.AddClass( "storage-drive-stats" );
		new Label( $"HDD: {hardware.HddStorageGb:0.##} GB total" ) { Parent = driveStats };
		new Label( $"Used: {metrics.UsedStorageGb:0.###} GB" ) { Parent = driveStats };
		new Label( $"Free: {metrics.UnusedStorageGb:0.###} GB" ) { Parent = driveStats };

		var table = new Panel { Parent = contentHost };
		table.AddClass( "task-table" );

		var header = new Panel { Parent = table };
		header.AddClass( "task-table-header" );
		AddHeaderCell( header, "Category" );
		AddHeaderCell( header, "Used (GB)" );

		foreach ( var item in breakdown.OrderByDescending( x => x.SizeGb ).ThenBy( x => x.Name, StringComparer.OrdinalIgnoreCase ) )
		{
			var row = new Panel { Parent = table };
			row.AddClass( "task-table-row" );
			AddCell( row, item.Name );
			AddCell( row, item.SizeGb.ToString( "0.###" ) );
		}

		var unusedRow = new Panel { Parent = table };
		unusedRow.AddClass( "task-table-row" );
		AddCell( unusedRow, "Unused" );
		AddCell( unusedRow, metrics.UnusedStorageGb.ToString( "0.###" ) );

		new Label( context.Runtime.GetArchivePath() ) { Parent = contentHost }.AddClass( "task-storage-note" );
	}

	private void AddProcessHeader( Panel table )
	{
		var header = new Panel { Parent = table };
		header.AddClass( "task-table-header" );
		AddSortableHeaderCell( header, "Process", TaskManagerProcessSortField.Process );
		AddHeaderCell( header, "Executable" );
		AddHeaderCell( header, "Path" );
		AddSortableHeaderCell( header, "Status", TaskManagerProcessSortField.Status );
		AddSortableHeaderCell( header, "CPU % Used", TaskManagerProcessSortField.Cpu );
		AddSortableHeaderCell( header, "RAM % Used", TaskManagerProcessSortField.Ram );
		AddSortableHeaderCell( header, "Startup", TaskManagerProcessSortField.Startup );
		AddHeaderCell( header, "Kill" );
	}

	private static void AddPerformanceCard( Panel parent, string title, string value )
	{
		var card = new Panel { Parent = parent };
		card.AddClass( "perf-card" );
		new Label( title ) { Parent = card }.AddClass( "perf-title" );
		new Label( value ) { Parent = card }.AddClass( "perf-value" );
	}

	private static void AddHeaderCell( Panel row, string text )
	{
		new Label( text ) { Parent = row }.AddClass( "task-cell task-header-cell" );
	}

	private void AddSortableHeaderCell( Panel row, string text, TaskManagerProcessSortField field )
	{
		var button = new Button( text ) { Parent = row };
		button.AddClass( "task-cell task-header-cell task-sort-header" );
		button.AddEventListener( "onclick", () =>
		{
			if ( sortField == field )
				sortDescending = !sortDescending;
			else
			{
				sortField = field;
				sortDescending = field is TaskManagerProcessSortField.Cpu or TaskManagerProcessSortField.Ram;
			}

			RenderActiveTab( true );
		} );
	}

	private static void AddCell( Panel row, string text )
	{
		new Label( text ) { Parent = row }.AddClass( "task-cell" );
	}

	private static void AddSparkline( Panel parent, string title, IReadOnlyList<float> samples )
	{
		var container = new Panel { Parent = parent };
		container.AddClass( "task-history-card" );
		new Label( title ) { Parent = container }.AddClass( "task-history-title" );
		var barRow = new Panel { Parent = container };
		barRow.AddClass( "task-history-bars" );

		foreach ( var sample in samples.DefaultIfEmpty( 0f ) )
		{
			var bar = new Panel { Parent = barRow };
			bar.AddClass( "task-history-bar" );
			bar.Style.Height = Length.Pixels( MathF.Max( 6f, sample * 0.48f ) );
		}
	}

	private static string FormatStatus( ComputerProcessStatus status )
	{
		return status switch
		{
			ComputerProcessStatus.NotResponding => "Not Responding",
			ComputerProcessStatus.Suspended => "Suspended",
			_ => "Running"
		};
	}

	private int GetRefreshVersion()
	{
		return TaskManagerRefreshPolicy.GetRefreshVersion(
			activeTab,
			context.Runtime.Version,
			context.Runtime.MetricsVersion,
			context.Runtime.StorageVersion );
	}
}
