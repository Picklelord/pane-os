# Interactive Computer System

This module provides an XP-styled PaneOS interactive desktop for S&Box projects.

## Scene setup

1. Add `InteractiveComputerComponent` to the in-world computer GameObject.
2. Add a S&Box `ScreenPanel` or `WorldPanel` plus `ComputerDesktop` to the screen/overlay GameObject.
3. Assign the `InteractiveComputerComponent` to `ComputerDesktop.Computer`.
4. Leave `UseGameSettingsResolution = true` unless you specifically want a fixed desktop resolution.
5. Either call `BeginInteraction( playerGameObject )` from your player controller, call `ToggleInteraction( playerGameObject )` from a model `Button Component`, or attach `ComputerUseRaycaster` to the player and assign its camera.

`ComputerDesktop.VisibleOnlyWhenInteracting` controls whether the desktop only appears after interaction or is always rendered on the panel.

Before adding any RT-screen workflow, do this quick check:

1. Put `ComputerDesktop` on a normal `WorldPanel`.
2. Set `VisibleOnlyWhenInteracting = false`.
3. Press play and confirm PaneOS is visible directly.

If that works, PaneOS itself is configured correctly and any later blank monitor issue is in the RT handoff.

If you want the player frozen while the desktop is open, add `ComputerInteractionPlayerLock` to the player GameObject and assign the movement/look components you want disabled during interaction.

## TV/monitor model setup with Sandbox: RT Screens

1. Add the Sandbox: RT Screens package to the project.
2. Add `PaneOSMonitorSetup` to the TV/monitor model root or to the model's screen surface GameObject.
3. Assign `Computer` to the matching `InteractiveComputerComponent`.
4. If the display surface is a child object, assign it to `DisplayObject`.
5. Optionally set `ScreenId`; otherwise it becomes `paneos-{ComputerId}`.

`PaneOSMonitorSetup` creates/configures `PaneOSRtScreenBridge`. The bridge creates a `PaneOS Screen` child with `ComputerDesktop` attached, makes the desktop always render, and creates a camera/render target for monitor workflows. Because s&box's whitelist blocks reflection in game code, PaneOS does not auto-create or auto-configure arbitrary RT Screens package components. Add/configure the RT Screens component in the editor and assign the generated screen, camera, render target, or `ScreenId` to the fields that component expects.

The bridge also creates a `PaneOS RT Camera` child with a render target sized to the computer resolution and offers that camera/texture to the RT Screens component. If the monitor shows the wrong crop or a blank image, adjust the `PaneOS RT Camera` transform so it frames the generated `PaneOS Screen` child, or disable `CreateCameraSource` if your RT Screens setup supplies its own camera.

Expected result after entering play mode:

1. A child named `PaneOS Screen` exists under the display object.
2. A child named `PaneOS RT Camera` exists under the display object.
3. `PaneOSRtScreenBridge.RenderTarget` is populated.
4. Your RT Screens package component is manually pointed at the generated camera/render target or matching `ScreenId`.

If you only get a blue blank monitor, the usual causes are:

- The RT screen package is still using a different source.
- The generated camera is not looking at `PaneOS Screen`.
- The display is cropped so only background is visible.
- `PaneOS Screen` exists, but your monitor material/package is not actually showing it yet.

## Per-computer app setup

Each `InteractiveComputerComponent` has its own saved `ComputerState`.

- `ComputerId` separates one in-world computer's state from another.
- `UseGameSettingsResolution` makes PaneOS follow the active game resolution and is the default.
- `InstalledAppIds` is a comma, space, or newline separated list of app IDs to install on that computer.
- `InstallAllAppsWhenListIsEmpty` installs every registered app when `InstalledAppIds` is blank.
- `SavedStateJson` stores resolution, sleep/start-menu state, installed apps, installed app settings, open windows, window positions, minimized/focused state, and each window's `Data`.
- `ExportStateJson()` and `ImportStateJson( json )` let your game save system persist or restore the full computer state at runtime.
- `ScreenSaverEnabled`, `ScreenSaverDelaySeconds`, `ScreenSaverLogoSize`, and `ScreenSaverVelocity` configure the per-computer PureOS screensaver. The timer defaults to 60 seconds and is paused entirely while a player is interacting with the computer.

Example `InstalledAppIds`:

```text
system.about
system.notepad
```

## Adding an app

Drop a C# file into your library or game code:

```csharp
using Sandbox.UI;
using PaneOS.InteractiveComputer;

public sealed class PaintApp : IComputerApp
{
	public ComputerAppSession Run( ComputerAppContext context )
	{
		return new ComputerAppSession
		{
			Title = "Paint",
			Icon = "PT",
			Content = new Panel()
		};
	}
}
```

Register app descriptors explicitly with `ComputerAppRegistry.Register(...)`. The built-in apps are registered by default. The app only becomes visible on a given computer when that computer has the app ID in its installed app state.

```csharp
ComputerAppRegistry.Register( new ComputerAppDescriptor
{
	Id = "vendor.paint",
	Title = "Paint",
	Icon = "PT",
	SortOrder = 50,
	Factory = () => new PaintApp()
} );
```

## Ridge browser policy

Ridge (`system.ridge`) is installed like any other app. By default it does not render external websites. To allow real web rendering on a specific computer, set Ridge's installed app settings in that computer state:

```json
{
  "AppId": "system.ridge",
  "Settings": {
    "web_rendering_enabled": "true",
    "allowed_hosts": "sbox.game docs.facepunch.com *.example.com"
  }
}
```

Only `http` and `https` URLs whose host matches `allowed_hosts` will be passed into S&Box `WebPanel`.

## State

`ComputerRuntime` keeps open windows, focus, minimized state, resolution, sleep state, installed app settings, and per-window app `Data`. `InteractiveComputerComponent.SavedStateJson` mirrors the current state so it can be persisted by your save system.

Qt is intentionally not used here. S&Box runtime UI is Panel/Razor based and can render to ScreenPanel or WorldPanel surfaces, which is the path that supports in-world interaction and gamepad/mouse focus cleanly.
