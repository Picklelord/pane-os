using Sandbox.UI;

namespace PaneOS.InteractiveComputer.Apps;

[ComputerApp( "system.about", "My Computer", Icon = "PC", SortOrder = 0 )]
public sealed class AboutComputerApp : IComputerApp
{
	public ComputerAppSession Run( ComputerAppContext context )
	{
		return new ComputerAppSession
		{
			Title = "About PC",
			Icon = "PC",
			Content = new AboutComputerPanel( context )
		};
	}
}

[StyleSheet( "InteractiveComputerApps.scss" )]
public sealed class AboutComputerPanel : ComputerWarmupPanel
{
	private readonly ComputerAppContext context;
	private int lastVersion = -1;

	public AboutComputerPanel( ComputerAppContext context )
	{
		this.context = context;
		AddClass( "about-app app-document" );
		Rebuild();
	}

	public override void Tick()
	{
		base.Tick();

		if ( lastVersion == context.Runtime.Version )
			return;

		Rebuild();
	}

	protected override void WarmupRefresh()
	{
		Rebuild();
	}

	private void Rebuild()
	{
		lastVersion = context.Runtime.Version;
		DeleteChildren( true );

		new Label( "About PC" ) { Parent = this }.AddClass( "app-heading" );
		new Label( $"Computer ID: {context.Computer.ComputerId}" ) { Parent = this };
		new Label( $"Resolution: {context.Runtime.State.ResolutionX} x {context.Runtime.State.ResolutionY}" ) { Parent = this };
		new Label( $"Installed apps: {context.Runtime.State.InstalledApps.Count}" ) { Parent = this };
		new Label( $"Running processes: {context.Runtime.OpenApps.Count}" ) { Parent = this };

		var hardware = context.Runtime.State.Hardware;
		var hardwarePanel = new Panel { Parent = this };
		hardwarePanel.AddClass( "about-grid" );

		AddMetric( hardwarePanel, "RAM", $"{hardware.RamGb:0.##} GB" );
		AddMetric( hardwarePanel, "CPU", $"{hardware.CpuCoreGhz:0.##} GHz" );
		AddMetric( hardwarePanel, "CPU cores", $"{hardware.CpuCoreCount}" );
		AddMetric( hardwarePanel, "GPU", $"{hardware.GpuCoreGhz:0.##} GHz" );
		AddMetric( hardwarePanel, "GPU VRAM", $"{hardware.GpuVramGb:0.##} GB" );
		AddMetric( hardwarePanel, "HDD", $"{hardware.HddStorageGb:0.##} GB" );
		AddMetric( hardwarePanel, "Internet", $"{hardware.InternetSpeedGbps:0.##} Gb/s" );
		AddMetric( hardwarePanel, "CPU lag sim", hardware.SimulateCpuInputDelayWhenMaxed ? "Enabled" : "Disabled" );

		new Label( "PaneOS now uses the live hardware profile here and in Task Manager." )
		{
			Parent = this
		}.AddClass( "app-note" );
	}

	private static void AddMetric( Panel parent, string label, string value )
	{
		var row = new Panel { Parent = parent };
		row.AddClass( "about-row" );
		new Label( label ) { Parent = row }.AddClass( "about-label" );
		new Label( value ) { Parent = row }.AddClass( "about-value" );
	}
}
