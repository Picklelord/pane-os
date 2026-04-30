using System;
using System.Collections.Generic;

namespace PaneOS.InteractiveComputer.Core;

public enum ComputerMediaRepeatMode
{
	Playlist,
	Single,
	None
}

public static class ComputerMediaPlaylistPolicy
{
	public static int ResolveNextIndex( int currentIndex, int playlistCount, ComputerMediaRepeatMode repeatMode )
	{
		if ( playlistCount <= 0 )
			return -1;

		if ( repeatMode == ComputerMediaRepeatMode.Single )
			return Math.Clamp( currentIndex, 0, playlistCount - 1 );

		var nextIndex = currentIndex + 1;
		if ( nextIndex < playlistCount )
			return nextIndex;

		return repeatMode == ComputerMediaRepeatMode.Playlist ? 0 : playlistCount - 1;
	}

	public static IReadOnlyList<string> Shuffle( IReadOnlyList<string> source, int seed )
	{
		var list = new List<string>( source );
		var random = new Random( seed );
		for ( var index = list.Count - 1; index > 0; index-- )
		{
			var swapIndex = random.Next( index + 1 );
			(list[index], list[swapIndex]) = (list[swapIndex], list[index]);
		}

		return list;
	}
}
