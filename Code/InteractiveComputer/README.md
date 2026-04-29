# Interactive Computer System

This module provides an XP-styled PaneOS interactive desktop for S&Box projects.

## Scene setup

1. Add `InteractiveComputerComponent` to the in-world computer GameObject.
2. Add a S&Box `ScreenPanel` or `WorldPanel` plus `ComputerDesktop` to the screen/overlay GameObject.
3. Assign the `InteractiveComputerComponent` to `ComputerDesktop.Computer`.
4. Either call `BeginInteraction( playerGameObject )` from your player controller or attach `ComputerUseRaycaster` to the player and assign its camera.

`ComputerDesktop.VisibleOnlyWhenInteracting` controls whether the desktop only appears after interaction or is always rendered on the panel.

## TV/monitor model setup with Sandbox: RT Screens

1. Add the Sandbox: RT Screens package to the project.
2. Add `PaneOSMonitorSetup` to the TV/monitor model root or to the model's screen surface GameObject.
3. Assign `Computer` to the matching `InteractiveComputerComponent`.
4. If the display surface is a child object, assign it to `DisplayObject`.
5. Optionally set `ScreenId`; otherwise it becomes `paneos-{ComputerId}`.

`PaneOSMonitorSetup` creates/configures `PaneOSRtScreenBridge`. The bridge creates a `PaneOS Screen` child with `ComputerDesktop` attached, makes the desktop always render, and creates a camera/render target for monitor workflows. Because s&box's whitelist blocks reflection in game code, PaneOS does not auto-create or auto-configure arbitrary RT Screens package components. Add/configure the RT Screens component in the editor and assign the generated screen, camera, render target, or `ScreenId` to the fields that component expects.

The bridge also creates a `PaneOS RT Camera` child with a render target sized to the computer resolution and offers that camera/texture to the RT Screens component. If the monitor shows the wrong crop or a blank image, adjust the `PaneOS RT Camera` transform so it frames the generated `PaneOS Screen` child, or disable `CreateCameraSource` if your RT Screens setup supplies its own camera.

## Per-computer app setup

Each `InteractiveComputerComponent` has its own saved `ComputerState`.

- `ComputerId` separates one in-world computer's state from another.
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
