# PaneOS Interactive Computer

PaneOS Interactive Computer is an s&box library package that provides an XP-styled in-world desktop. It includes a persistent computer runtime, app launcher, movable windows, screensaver, notepad, task manager, about panel, and locked-down Ridge browser shell.

This repository is structured as a library package:

```text
paneosinteractivecomputer.sbproj
Assets/
Code/
Editor/
UnitTests/
```

## Install In A Project

Copy or clone this package folder into your game's `Libraries` directory:

```text
YourGame/
  Libraries/
    PaneOSInteractiveComputer/
      paneosinteractivecomputer.sbproj
      Assets/
      Code/
      Editor/
      UnitTests/
```

For the current local project, the package target is:

```text
D:\sboxProjects\getalife\Libraries\PaneOSInteractiveComputer
```

After copying, reload or recompile the project in the s&box editor. The PaneOS components should appear in the component picker from the library assembly.

## What Is Included

- `InteractiveComputerComponent` owns one computer's state and runtime.
- `ComputerDesktop` renders the PaneOS Razor/Panel desktop.
- `ComputerUseRaycaster` is an optional player-side use-key helper.
- `ComputerInteractionPlayerLock` is an optional player-side helper that disables your movement/look components while the desktop is in use.
- `PaneOSMonitorSetup` and `PaneOSRtScreenBridge` create the generated screen, camera, and render target used by monitor or TV setups.
- Built-in apps:
  - `system.about`
  - `system.notepad`
  - `system.ridge`
  - `system.taskmanager`

## Basic Editor Setup

Use this setup when the screen can directly host s&box UI:

1. Create or select the in-world computer GameObject.
2. Add `InteractiveComputerComponent`.
3. Set a unique `ComputerId`, for example `office-pc-01`.
4. Create or select the screen surface GameObject.
5. Add a built-in s&box `WorldPanel` or `ScreenPanel` component to that screen object.
6. Add `ComputerDesktop` to the same screen object.
7. Assign the computer GameObject's `InteractiveComputerComponent` to `ComputerDesktop.Computer`.
8. Set `ComputerDesktop.VisibleOnlyWhenInteracting`:
   - `true` shows the desktop only while the player is interacting.
   - `false` keeps the desktop rendered on the screen all the time.
9. Set the panel size to match the computer resolution. The default PaneOS resolution is `1024 x 768`.

Recommended starting values:

```text
InteractiveComputerComponent.ResolutionX = 1024
InteractiveComputerComponent.ResolutionY = 768
WorldPanel.PanelSize = 1024, 768
WorldPanel.RenderScale = 1
WorldPanel.LookAtCamera = false
```

## Player Interaction

You can wire interaction manually from your player controller:

```csharp
computer.BeginInteraction( playerGameObject );
computer.EndInteraction();
computer.ToggleInteraction( playerGameObject );
```

Or use the included helper:

1. Add `ComputerUseRaycaster` to the player GameObject.
2. Assign the player's camera to `PlayerCamera`.
3. Set `UseButton` to your input action, usually `use`.
4. Set `UseDistance` to the desired interaction range.
5. Optionally add `ComputerInteractionPlayerLock` to the same player and assign your movement and look components.

While interacting, the computer wakes from sleep, the screensaver is dismissed, idle time resets, and pressing `escape` ends interaction.

## Monitor Or TV Setup

Use this setup when a monitor or TV material expects a render target from another screen system.

1. Add `InteractiveComputerComponent` to the computer root.
2. Add `PaneOSMonitorSetup` to the monitor model root or screen surface GameObject.
3. Assign `PaneOSMonitorSetup.Computer`.
4. If the display surface is a child object, assign it to `PaneOSMonitorSetup.DisplayObject`.
5. Optionally set `ScreenId`; otherwise it uses `paneos-{ComputerId}`.
6. Press play or run setup.

`PaneOSMonitorSetup` creates/configures `PaneOSRtScreenBridge`. The bridge:

- Creates a `PaneOS Screen` child.
- Adds/configures a `WorldPanel`.
- Adds/configures `ComputerDesktop`.
- Creates a `PaneOS RT Camera` child.
- Creates a render target sized to the computer resolution.
- Exposes the generated screen, camera, render target, and optional `RtScreenComponent` reference in the inspector.

s&box's whitelist blocks reflection in game code, so PaneOS does not auto-create or auto-write properties on arbitrary RT Screens package components. Add/configure the RT Screens component in the editor and assign the generated `PaneOS Screen`, `PaneOS RT Camera`, render target, and/or `ScreenId` to the fields your screen component expects.

## Computer Configuration

Important `InteractiveComputerComponent` properties:

- `ComputerId`: separates one computer's state from another. Give every placed computer a unique value.
- `ResolutionX` and `ResolutionY`: desktop resolution. Keep these in sync with your panel/render target size.
- `StartsSleeping`: starts the computer behind the sleep overlay.
- `ScreenSaverEnabled`: enables the PaneOS screensaver.
- `ScreenSaverDelaySeconds`: idle time before the screensaver appears.
- `ScreenSaverLogoSize`: size of the bouncing PaneOS logo.
- `ScreenSaverVelocity`: screensaver movement speed.
- `InstalledAppIds`: comma, space, tab, or newline separated app IDs.
- `InstallAllAppsWhenListIsEmpty`: installs every registered app if `InstalledAppIds` is blank.
- `SavedStateJson`: serialized state mirror for save systems and editor inspection.

Example `InstalledAppIds`:

```text
system.about
system.notepad
system.ridge
system.taskmanager
```

Runtime persistence helpers:

```csharp
var json = computer.ExportStateJson();
computer.ImportStateJson( json );
computer.ResetStateFromCreationSettings();
```

## Apps

Apps implement `IComputerApp` and usually return a `Sandbox.UI.Panel`.

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

Register app descriptors explicitly:

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

An app appears on a specific computer only when its app ID is installed in that computer's state.

## Ridge Browser

Ridge is intentionally locked down. By default it does not render external websites.

To enable website rendering for a specific computer, add settings to the installed app state for `system.ridge`:

```json
{
  "AppId": "system.ridge",
  "Settings": {
    "web_rendering_enabled": "true",
    "allowed_hosts": "sbox.game docs.facepunch.com *.example.com"
  }
}
```

Only `http` and `https` URLs whose host matches `allowed_hosts` are passed to `WebPanel`.

## Testing

Run the framework-free unit test runner:

```powershell
dotnet run --project UnitTests\PaneOS.Tests.csproj
```

The tests cover Ridge browser policy, screensaver timing/movement, and core state defaults.

## Troubleshooting

Desktop does not appear:

- Check that `ComputerDesktop.Computer` is assigned.
- Check `VisibleOnlyWhenInteracting`; if it is `true`, call `BeginInteraction`.
- Check the panel size and screen transform.
- Check that the library compiled in the editor.

Apps do not appear:

- Check `InstalledAppIds`.
- If `InstalledAppIds` is blank, check `InstallAllAppsWhenListIsEmpty`.
- Confirm custom apps are registered with `ComputerAppRegistry.Register(...)`.

State seems shared between multiple computers:

- Give each `InteractiveComputerComponent` a unique `ComputerId`.

RT monitor is blank:

- Verify the generated `PaneOS Screen` exists.
- Verify `PaneOS RT Camera` frames the generated screen.
- Assign the generated camera/render target/`ScreenId` to your RT screen component in the editor.

PaneOSRtScreenBridge shows an `OnValidate` warning:

- This usually means the bridge validated before the computer runtime had fully awoken in the editor.
- PaneOS now falls back to the component resolution during validation, then finishes normal setup at runtime.

PaneOS uses s&box Panel/Razor UI. It does not use Qt.
