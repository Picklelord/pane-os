# PaneOS Interactive Computer

PaneOS Interactive Computer is an s&box library package that provides an XP-styled in-world desktop. It includes a persistent computer runtime, app launcher, movable windows, screensaver, notepad, task manager, about panel, and locked-down Ridge browser shell.

Try it out in the [Test Project here.](https://github.com/Picklelord/pane-os-test-project/)

<img src="https://github.com/Picklelord/pane-os/tree/master/Docs/DefaultStartState.png"/>
<img src="https://github.com/Picklelord/pane-os/tree/master/Docs/AfterInteracting.png"/>
<img src="https://github.com/Picklelord/pane-os/tree/master/Docs/AppsOpenBeforeNotResponding.png"/>
<img src="https://github.com/Picklelord/pane-os/tree/master/Docs/AppsOpenNotRespondingAndStartMenu.png"/>
<img src="https://github.com/Picklelord/pane-os/tree/master/Docs/AppsOpenOutsideOfComputer.png"/>
<img src="https://github.com/Picklelord/pane-os/tree/master/Docs/ClassicDvDPaneOSScreenSaver.png"/>
<img src="https://github.com/Picklelord/pane-os/tree/master/Docs/AfterInteracting.png"/>

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
  - `system.paneexplorer`
  - `system.settings`
  - `system.calculator`
  - `system.paint`
  - `system.mediaplayer`

## Basic Editor Setup

Use this setup when the screen can directly host s&box UI:

1. Create or select the in-world computer GameObject.
2. Add `InteractiveComputerComponent`.
3. Set a unique `ComputerId`, for example `office-pc-01`.
4. Leave `UseGameSettingsResolution = true` if you want PaneOS to match the player's current game resolution automatically. Turn it off only when you want to force a fixed desktop size with `ResolutionX` and `ResolutionY`.
5. Create or select the screen surface GameObject.
6. Add a built-in s&box `WorldPanel` or `ScreenPanel` component to that screen object.
7. Add `ComputerDesktop` to the same screen object.
8. Assign the computer GameObject's `InteractiveComputerComponent` to `ComputerDesktop.Computer`.
9. Set `ComputerDesktop.VisibleOnlyWhenInteracting`:
   - `true` shows the desktop only while the player is interacting.
   - `false` keeps the desktop rendered on the screen all the time.
10. If `UseGameSettingsResolution = false`, set the panel size to match `ResolutionX` and `ResolutionY`.

Recommended starting values:

```text
InteractiveComputerComponent.UseGameSettingsResolution = true
InteractiveComputerComponent.ResolutionX = 1024
InteractiveComputerComponent.ResolutionY = 768
WorldPanel.PanelSize = 1024, 768
WorldPanel.RenderScale = 1
WorldPanel.LookAtCamera = false
```

Sanity-check this path before adding any RT/TV package:

1. Set `ComputerDesktop.VisibleOnlyWhenInteracting = false`.
2. Press play.
3. Confirm you can already see the PaneOS desktop directly on the `WorldPanel` or `ScreenPanel`.

If this direct panel setup works, but your monitor/TV overlay stays blank later, the PaneOS runtime is fine and the remaining issue is in the render-target hookup.

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

While interacting, the computer wakes from sleep, the screensaver is dismissed, idle time resets, and pressing the configured `ExitInteractionInputAction` ends interaction. The default is `use`, which matches the usual button/use flow.

If your monitor model already uses a built-in s&box `Button Component`, a straightforward setup is to call `InteractiveComputerComponent.ToggleInteraction( playerGameObject )` from that button's use/click event. PaneOS no longer shows a fake `E` prompt inside its lock or sleep overlays, so you can reserve the `use` key entirely for your own interaction flow.

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

Important: `PaneOSMonitorSetup` does not finish the third-party RT package setup for you. It only generates the PaneOS side. You still need to wire the generated objects into the monitor package component manually.

Expected runtime result after pressing play:

1. Your display object should contain a child named `PaneOS Screen`.
2. Your display object should contain a child named `PaneOS RT Camera`.
3. `PaneOS Rt Screen Bridge.RenderTarget` should be populated in the inspector.
4. Your RT Screens package component should reference that generated camera/render target or the matching `ScreenId`.

If those generated children never appear, the bridge did not set up correctly. If they do appear but the monitor stays blank or flat blue, the RT package is still pointing at the wrong source.

## Computer Configuration

Important `InteractiveComputerComponent` properties:

- `ComputerId`: separates one computer's state from another. Give every placed computer a unique value.
- `ResolutionX` and `ResolutionY`: desktop resolution. Keep these in sync with your panel/render target size.
- `UseGameSettingsResolution`: when `true`, PaneOS follows the current game resolution and ignores manual `ResolutionX` / `ResolutionY` values at runtime.
- `StartsSleeping`: starts the computer behind the sleep overlay.
- `ScreenSaverEnabled`: enables the PaneOS screensaver.
- `ScreenSaverDelaySeconds`: idle time before the screensaver appears.
- `ScreenSaverLogoSize`: size of the bouncing PaneOS logo.
- `ScreenSaverVelocity`: screensaver movement speed.
- `ThemeName`: texture theme folder name. The default is `default`, which resolves icons from `Assets/textures/themes/default`.
- `InstalledAppIds`: comma, space, tab, or newline separated app IDs.
- `InstallAllAppsWhenListIsEmpty`: installs every registered app if `InstalledAppIds` is blank.
- `SavedStateJson`: serialized state mirror for save systems and editor inspection.

Theme icon convention:

```text
Assets/textures/themes/{ThemeName}/App_{appName}.png
Assets/textures/themes/{ThemeName}/Ext_{extension}.png
Assets/textures/themes/{ThemeName}/folder.png
```

Example default theme icon names include `App_notepad.png`, `App_paneExplorer.png`, `Ext_txt.png`, and `folder.png`.

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

PaneOS credit links are allow-listed by default so the About PC credits can open in Ridge without requiring every placed computer to duplicate those hosts. The default credit hosts are:

- `github.com`
- `flaticon.com`
- `www.flaticon.com`

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

- First prove the direct `WorldPanel` setup works before using RT screens.
- Verify the generated `PaneOS Screen` exists.
- Verify the generated `PaneOS Screen` has both `WorldPanel` and `ComputerDesktop`.
- Verify `PaneOS RT Camera` frames the generated screen.
- Assign the generated camera/render target/`ScreenId` to your RT screen component in the editor.
- If you only see a flat blue screen, the most common causes are:
  - The RT screen package is not using PaneOS's generated render target yet.
  - The RT camera is not aimed at `PaneOS Screen`.
  - The monitor material is still bound to a different screen source.
  - The visible area is cropped so far in that only desktop background is on screen.
- Temporarily set `ComputerDesktop.VisibleOnlyWhenInteracting = false` while debugging, so you are not also fighting interaction state.
- Temporarily bypass the RT package and point the display at the generated `WorldPanel` directly. If PaneOS appears there, the problem is definitely the RT hookup rather than the computer runtime.

PaneOSRtScreenBridge shows an `OnValidate` warning:

- This usually means the bridge validated before the computer runtime had fully awoken in the editor.
- PaneOS now falls back to the component resolution during validation, then finishes normal setup at runtime.

PaneOS uses s&box Panel/Razor UI. It does not use Qt.

## Credits

- [Pane OS, created by Daniel Garnier](https://github.com/Picklelord/pane-os)
- [Video icons created by riajulislam - Flaticon](https://www.flaticon.com/free-icons/video)
- [Radio icons created by Freepik - Flaticon](https://www.flaticon.com/free-icons/radio)
- [Mp3 icons created by Freepik - Flaticon](https://www.flaticon.com/free-icons/mp3)
- [Exe icons created by Freepik - Flaticon](https://www.flaticon.com/free-icons/exe)
- [Document icons created by Freepik - Flaticon](https://www.flaticon.com/free-icons/document)
- [Painting icons created by Freepik - Flaticon](https://www.flaticon.com/free-icons/painting)
- [Seo and web icons created by Freepik - Flaticon](https://www.flaticon.com/free-icons/seo-and-web)
- [Search icons created by Freepik - Flaticon](https://www.flaticon.com/free-icons/search)
- [Notepad icons created by Freepik - Flaticon](https://www.flaticon.com/free-icons/notepad)
- [Calculator icons created by Freepik - Flaticon](https://www.flaticon.com/free-icons/calculator)
- [Checklist icons created by Freepik - Flaticon](https://www.flaticon.com/free-icons/checklist)
- [Recycle bin icons created by Freepik - Flaticon](https://www.flaticon.com/free-icons/recycle-bin)
- [Control panel icons created by Freepik - Flaticon](https://www.flaticon.com/free-icons/control-panel)
- [Computer icons created by Freepik - Flaticon](https://www.flaticon.com/free-icons/computer)
- [Folder icons created by Freepik - Flaticon](https://www.flaticon.com/free-icons/folder)
- [Picture icons created by Freepik - Flaticon](https://www.flaticon.com/free-icons/picture)
- [Film icons created by Freepik - Flaticon](https://www.flaticon.com/free-icons/film)
