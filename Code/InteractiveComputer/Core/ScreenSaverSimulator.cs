using System;

namespace PaneOS.InteractiveComputer.Core;

public static class ScreenSaverSimulator
{
	public static bool Tick( ComputerState state, float deltaSeconds, bool isPlayerInteracting )
	{
		var saver = state.ScreenSaver;
		if ( isPlayerInteracting )
		{
			var changed = saver.IsActive || saver.IdleSeconds != 0f;
			saver.IdleSeconds = 0f;
			saver.IsActive = false;
			return changed;
		}

		if ( !saver.Enabled || state.IsSleeping )
			return false;

		saver.IdleSeconds += MathF.Max( 0f, deltaSeconds );
		if ( !saver.IsActive && saver.IdleSeconds >= saver.DelaySeconds )
		{
			saver.IsActive = true;
			ClampLogo( state );
			return true;
		}

		if ( !saver.IsActive )
			return false;

		MoveLogo( state, deltaSeconds );
		return true;
	}

	public static bool NotifyUserActivity( ComputerState state )
	{
		var saver = state.ScreenSaver;
		var changed = saver.IsActive || saver.IdleSeconds != 0f;
		saver.IsActive = false;
		saver.IdleSeconds = 0f;
		return changed;
	}

	public static void MoveLogo( ComputerState state, float deltaSeconds )
	{
		var saver = state.ScreenSaver;
		var maxX = MathF.Max( 0f, state.ResolutionX - saver.LogoWidth );
		var maxY = MathF.Max( 0f, state.ResolutionY - saver.LogoHeight );

		saver.LogoX += saver.VelocityX * deltaSeconds;
		saver.LogoY += saver.VelocityY * deltaSeconds;

		if ( saver.LogoX <= 0f )
		{
			saver.LogoX = 0f;
			saver.VelocityX = MathF.Abs( saver.VelocityX );
		}
		else if ( saver.LogoX >= maxX )
		{
			saver.LogoX = maxX;
			saver.VelocityX = -MathF.Abs( saver.VelocityX );
		}

		if ( saver.LogoY <= 0f )
		{
			saver.LogoY = 0f;
			saver.VelocityY = MathF.Abs( saver.VelocityY );
		}
		else if ( saver.LogoY >= maxY )
		{
			saver.LogoY = maxY;
			saver.VelocityY = -MathF.Abs( saver.VelocityY );
		}
	}

	public static void ClampLogo( ComputerState state )
	{
		var saver = state.ScreenSaver;
		saver.LogoX = Math.Clamp( saver.LogoX, 0f, MathF.Max( 0f, state.ResolutionX - saver.LogoWidth ) );
		saver.LogoY = Math.Clamp( saver.LogoY, 0f, MathF.Max( 0f, state.ResolutionY - saver.LogoHeight ) );
	}
}
