using System.Collections.Generic;

namespace PaneOS.InteractiveComputer;

public sealed class ComputerState
{
	public int ResolutionX { get; set; } = 1024;
	public int ResolutionY { get; set; } = 768;
	public bool IsSleeping { get; set; }
	public bool IsLocked { get; set; }
	public float RestartLogSecondsRemaining { get; set; }
	public float BootSplashSecondsRemaining { get; set; }
	public List<string> RestartLogLines { get; set; } = new();
	public bool StartMenuOpen { get; set; }
	public string? FocusedInstanceId { get; set; }
	public string ArchiveUserName { get; set; } = "";
	public string DesktopWallpaper { get; set; } = "default";
	public bool DarkModeEnabled { get; set; }
	public ComputerHardwareState Hardware { get; set; } = new();
	public ComputerScreenSaverState ScreenSaver { get; set; } = new();
	public List<ComputerInstalledAppState> InstalledApps { get; set; } = new();
	public List<ComputerAppState> OpenApps { get; set; } = new();
}

public sealed class ComputerHardwareState
{
	public float RamGb { get; set; } = 2f;
	public float CpuCoreGhz { get; set; } = 3.7f;
	public int CpuCoreCount { get; set; } = 4;
	public float HddStorageGb { get; set; } = 256f;
	public float InternetSpeedGbps { get; set; } = 100f;
	public float GpuCoreGhz { get; set; } = 1.54f;
	public float GpuVramGb { get; set; } = 4f;
	public bool SimulateCpuInputDelayWhenMaxed { get; set; } = true;
}

public sealed class ComputerScreenSaverState
{
	public bool Enabled { get; set; } = true;
	public bool IsActive { get; set; }
	public float DelaySeconds { get; set; } = 60f;
	public float IdleSeconds { get; set; }
	public float LogoX { get; set; } = 80f;
	public float LogoY { get; set; } = 80f;
	public float VelocityX { get; set; } = 160f;
	public float VelocityY { get; set; } = -120f;
	public float LogoWidth { get; set; } = 220f;
	public float LogoHeight { get; set; } = 72f;
}

public sealed class ComputerInstalledAppState
{
	public string AppId { get; set; } = "";
	public Dictionary<string, string> Settings { get; set; } = new();
}

public sealed class ComputerAppState
{
	public string InstanceId { get; set; } = "";
	public string AppId { get; set; } = "";
	public string Title { get; set; } = "";
	public string Icon { get; set; } = "[]";
	public ComputerProcessStatus Status { get; set; } = ComputerProcessStatus.Running;
	public bool IsResourceStarved { get; set; }
	public bool IsMinimized { get; set; }
	public int X { get; set; } = 72;
	public int Y { get; set; } = 58;
	public int Width { get; set; } = 560;
	public int Height { get; set; } = 380;
	public int ZIndex { get; set; } = 1;
	public Dictionary<string, string> Data { get; set; } = new();
}

public enum ComputerProcessStatus
{
	Running,
	Suspended,
	NotResponding
}
