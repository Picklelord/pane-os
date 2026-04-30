using System;

namespace PaneOS.InteractiveComputer.Core;

public static class TaskManagerRefreshPolicy
{
	public static int GetRefreshVersion( TaskManagerTab activeTab, int stateVersion, int metricsVersion, int storageVersion )
	{
		return activeTab switch
		{
			TaskManagerTab.Performance => HashCode.Combine( stateVersion, metricsVersion ),
			TaskManagerTab.Storage => HashCode.Combine( stateVersion, storageVersion ),
			_ => HashCode.Combine( stateVersion, metricsVersion )
		};
	}
}
