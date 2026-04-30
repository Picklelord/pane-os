using Sandbox.UI;

namespace PaneOS.InteractiveComputer.Apps;

public abstract class ComputerWarmupPanel : Panel
{
	private int warmupRefreshPasses;

	public override void Tick()
	{
		base.Tick();

		if ( warmupRefreshPasses >= 3 )
			return;

		if ( Box.Rect.Width <= 0f || Box.Rect.Height <= 0f )
			return;

		warmupRefreshPasses++;
		WarmupRefresh();
		MarkRenderDirty();
	}

	protected virtual void WarmupRefresh()
	{
	}
}
