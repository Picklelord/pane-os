using PaneOS.InteractiveComputer;
using PaneOS.InteractiveComputer.Core;

var tests = new (string Name, Action Body)[]
{
	("Ridge normalizes bare hosts to https", RidgeNormalizesBareHosts),
	("Ridge blocks external websites by default", RidgeBlocksByDefault),
	("Ridge allows exact whitelisted host when enabled", RidgeAllowsExactHost),
	("Ridge allows wildcard subdomains only", RidgeAllowsWildcardSubdomainsOnly),
	("Ridge blocks unsupported protocols", RidgeBlocksUnsupportedProtocols),
	("Ridge parses and deduplicates allowed hosts", RidgeParsesAllowedHosts),
	("Screensaver activates after configured delay", ScreenSaverActivatesAfterDelay),
	("Screensaver does not run while sleeping", ScreenSaverDoesNotRunWhileSleeping),
	("Screensaver pauses and resets while interacting", ScreenSaverPausesWhileInteracting),
	("Screensaver bounces off top edge toward bottom", ScreenSaverBouncesTop),
	("Screensaver bounces off right edge toward left", ScreenSaverBouncesRight),
	("Screensaver bounces off bottom edge toward top", ScreenSaverBouncesBottom),
	("Screensaver bounces off left edge toward right", ScreenSaverBouncesLeft),
	("Computer state defaults include screensaver and app lists", ComputerStateDefaults),
};

var failed = 0;
foreach ( var test in tests )
{
	try
	{
		test.Body();
		Console.WriteLine( $"PASS {test.Name}" );
	}
	catch ( Exception ex )
	{
		failed++;
		Console.WriteLine( $"FAIL {test.Name}: {ex.Message}" );
	}
}

Console.WriteLine();
Console.WriteLine( failed == 0 ? $"All {tests.Length} tests passed." : $"{failed} of {tests.Length} tests failed." );
return failed == 0 ? 0 : 1;

static void RidgeNormalizesBareHosts()
{
	Equal( "https://sbox.game", RidgeBrowserPolicy.NormalizeUrl( "sbox.game" ) );
	Equal( "paneos://home", RidgeBrowserPolicy.NormalizeUrl( "" ) );
}

static void RidgeBlocksByDefault()
{
	var result = RidgeBrowserPolicy.Evaluate( "https://sbox.game", null, "sbox.game" );
	False( result.CanRenderWebPanel );
	Equal( "Rendering disabled", result.Status );
}

static void RidgeAllowsExactHost()
{
	var result = RidgeBrowserPolicy.Evaluate( "sbox.game", "true", "sbox.game docs.facepunch.com" );
	True( result.CanRenderWebPanel );
	Equal( "https://sbox.game", result.NormalizedUrl );
	Equal( "Loaded sbox.game", result.Status );
}

static void RidgeAllowsWildcardSubdomainsOnly()
{
	True( RidgeBrowserPolicy.Evaluate( "https://docs.example.com", "on", "*.example.com" ).CanRenderWebPanel );
	False( RidgeBrowserPolicy.Evaluate( "https://example.com", "on", "*.example.com" ).CanRenderWebPanel );
}

static void RidgeBlocksUnsupportedProtocols()
{
	var result = RidgeBrowserPolicy.Evaluate( "ftp://example.com", "true", "example.com" );
	False( result.CanRenderWebPanel );
	Equal( "Blocked", result.Status );
}

static void RidgeParsesAllowedHosts()
{
	var hosts = RidgeBrowserPolicy.ParseHostList( "sbox.game, SBOX.game;docs.facepunch.com\n*.example.com" );
	Equal( 3, hosts.Count );
	True( hosts.Contains( "sbox.game" ) );
	True( hosts.Contains( "docs.facepunch.com" ) );
	True( hosts.Contains( "*.example.com" ) );
}

static void ScreenSaverActivatesAfterDelay()
{
	var state = NewScreenSaverState();
	var changedBefore = ScreenSaverSimulator.Tick( state, 59f, false );
	False( changedBefore );
	False( state.ScreenSaver.IsActive );

	var changedAtDelay = ScreenSaverSimulator.Tick( state, 1f, false );
	True( changedAtDelay );
	True( state.ScreenSaver.IsActive );
}

static void ScreenSaverDoesNotRunWhileSleeping()
{
	var state = NewScreenSaverState();
	state.IsSleeping = true;

	var changed = ScreenSaverSimulator.Tick( state, 120f, false );
	False( changed );
	False( state.ScreenSaver.IsActive );
	Equal( 0f, state.ScreenSaver.IdleSeconds );
}

static void ScreenSaverPausesWhileInteracting()
{
	var state = NewScreenSaverState();
	state.ScreenSaver.IdleSeconds = 12f;
	state.ScreenSaver.IsActive = true;

	var changed = ScreenSaverSimulator.Tick( state, 1f, true );
	True( changed );
	False( state.ScreenSaver.IsActive );
	Equal( 0f, state.ScreenSaver.IdleSeconds );
}

static void ScreenSaverBouncesTop()
{
	var state = NewScreenSaverState();
	state.ScreenSaver.LogoY = 2f;
	state.ScreenSaver.VelocityY = -50f;

	ScreenSaverSimulator.MoveLogo( state, 1f );
	Equal( 0f, state.ScreenSaver.LogoY );
	True( state.ScreenSaver.VelocityY > 0f );
}

static void ScreenSaverBouncesRight()
{
	var state = NewScreenSaverState();
	state.ScreenSaver.LogoX = 780f;
	state.ScreenSaver.VelocityX = 50f;

	ScreenSaverSimulator.MoveLogo( state, 1f );
	Equal( 800f, state.ScreenSaver.LogoX );
	True( state.ScreenSaver.VelocityX < 0f );
}

static void ScreenSaverBouncesBottom()
{
	var state = NewScreenSaverState();
	state.ScreenSaver.LogoY = 580f;
	state.ScreenSaver.VelocityY = 50f;

	ScreenSaverSimulator.MoveLogo( state, 1f );
	Equal( 600f, state.ScreenSaver.LogoY );
	True( state.ScreenSaver.VelocityY < 0f );
}

static void ScreenSaverBouncesLeft()
{
	var state = NewScreenSaverState();
	state.ScreenSaver.LogoX = 4f;
	state.ScreenSaver.VelocityX = -50f;

	ScreenSaverSimulator.MoveLogo( state, 1f );
	Equal( 0f, state.ScreenSaver.LogoX );
	True( state.ScreenSaver.VelocityX > 0f );
}

static void ComputerStateDefaults()
{
	var state = new ComputerState();
	Equal( 1024, state.ResolutionX );
	Equal( 768, state.ResolutionY );
	True( state.ScreenSaver.Enabled );
	Equal( 60f, state.ScreenSaver.DelaySeconds );
	Equal( 0, state.InstalledApps.Count );
	Equal( 0, state.OpenApps.Count );
}

static ComputerState NewScreenSaverState()
{
	return new ComputerState
	{
		ResolutionX = 1000,
		ResolutionY = 700,
		ScreenSaver = new ComputerScreenSaverState
		{
			Enabled = true,
			DelaySeconds = 60f,
			LogoWidth = 200f,
			LogoHeight = 100f,
			LogoX = 400f,
			LogoY = 300f,
			VelocityX = 40f,
			VelocityY = -30f
		}
	};
}

static void True( bool value )
{
	if ( !value )
		throw new InvalidOperationException( "Expected true." );
}

static void False( bool value )
{
	if ( value )
		throw new InvalidOperationException( "Expected false." );
}

static void Equal<T>( T expected, T actual )
{
	if ( !EqualityComparer<T>.Default.Equals( expected, actual ) )
		throw new InvalidOperationException( $"Expected '{expected}', got '{actual}'." );
}
