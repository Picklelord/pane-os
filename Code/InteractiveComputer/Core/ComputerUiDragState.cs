namespace PaneOS.InteractiveComputer.Core;

public static class ComputerUiDragState
{
	public static string DraggedVirtualPath { get; set; } = "";

	public static void BeginDrag( string virtualPath )
	{
		DraggedVirtualPath = virtualPath ?? "";
	}

	public static string ConsumeDraggedVirtualPath()
	{
		var value = DraggedVirtualPath;
		DraggedVirtualPath = "";
		return value;
	}
}
