using System;
using System.Collections.Generic;
using PaneOS.InteractiveComputer.Core;

namespace PaneOS.InteractiveComputer;

public sealed class ComputerProcessMetrics
{
	public float CpuPercent { get; set; }
	public float RamMb { get; set; }
	public float RamPercent { get; set; }
	public float GpuCorePercent { get; set; }
	public float GpuVramPercent { get; set; }
	public float StorageGb { get; set; }
}

public sealed class ComputerSystemMetrics
{
	public float CpuPercent { get; set; }
	public float RamPercent { get; set; }
	public float RamUsedMb { get; set; }
	public float RamTotalMb { get; set; }
	public float GpuCorePercent { get; set; }
	public float GpuVramPercent { get; set; }
	public float UsedStorageGb { get; set; }
	public float UnusedStorageGb { get; set; }
}

public sealed class ComputerStorageBreakdownItem
{
	public string Name { get; set; } = "";
	public float SizeGb { get; set; }
}

public sealed class ComputerMessageBoxOptions
{
	public string Title { get; set; } = "Message";
	public string Message { get; set; } = "";
	public string Icon { get; set; } = "i";
	public bool HasTextInput { get; set; }
	public string TextInputValue { get; set; } = "";
	public string TextInputPlaceholder { get; set; } = "";
	public IReadOnlyList<string> Buttons { get; set; } = new[] { "OK" };
}

public sealed class ComputerMessageBoxResult
{
	public string ButtonPressed { get; set; } = "";
	public string TextValue { get; set; } = "";
}

public sealed class ComputerActiveMessageBox
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public ComputerMessageBoxOptions Options { get; set; } = new();
	public string CurrentText { get; set; } = "";
	public Action<ComputerMessageBoxResult>? OnClosed { get; set; }
}

public enum ComputerFileDialogMode
{
	Open,
	Save
}

public sealed class ComputerFileDialogOptions
{
	public string Title { get; set; } = "File Dialog";
	public ComputerFileDialogMode Mode { get; set; } = ComputerFileDialogMode.Open;
	public string InitialPath { get; set; } = "";
	public string DefaultFileName { get; set; } = "";
	public IReadOnlyList<string> AllowedExtensions { get; set; } = Array.Empty<string>();
	public string ConfirmButtonText { get; set; } = "";
}

public sealed class ComputerFileDialogResult
{
	public bool Confirmed { get; set; }
	public string VirtualPath { get; set; } = "";
	public string FileName { get; set; } = "";
}

public sealed class ComputerActiveFileDialog
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public ComputerFileDialogOptions Options { get; set; } = new();
	public List<string> CurrentPathSegments { get; set; } = new();
	public string SelectedVirtualPath { get; set; } = "";
	public string CurrentFileName { get; set; } = "";
	public Action<ComputerFileDialogResult>? OnClosed { get; set; }

	public string CurrentPathDisplay => CurrentPathSegments.Count == 0 ? "My PC" : "/" + string.Join( "/", CurrentPathSegments );
	public IReadOnlyList<PaneArchiveItem> VisibleItems { get; set; } = Array.Empty<PaneArchiveItem>();
}

public enum TaskManagerTab
{
	Processes,
	Performance,
	Storage
}

public sealed class ComputerMetricHistory
{
	public IReadOnlyList<float> CpuSamples { get; set; } = Array.Empty<float>();
	public IReadOnlyList<float> RamSamples { get; set; } = Array.Empty<float>();
	public IReadOnlyList<float> GpuSamples { get; set; } = Array.Empty<float>();
	public IReadOnlyList<float> GpuVramSamples { get; set; } = Array.Empty<float>();
}

public sealed class ComputerNotification
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public string Title { get; set; } = "";
	public string Message { get; set; } = "";
	public string Icon { get; set; } = "i";
	public float RemainingSeconds { get; set; } = 4f;
}
