# Desktop Pet (桌宠)

A lightweight Windows desktop pet built with Unity 2022.3 LTS and URP.
The pet lives on your desktop as a transparent, always-on-top window, animated by a Live2D Cubism model (Natori).

---

## Features

- Transparent borderless window (no taskbar entry)
- Always on top of other windows
- Click & drag to reposition
- **Live2D Cubism animation** — Natori model with Idle / Clicked / Dragging states
- Right-click context menu (Quit)
- System tray icon

---

## Live2D Model

The pet uses **Natori**, a free sample model provided by Live2D Inc. under the
[Live2D Free Material License](https://www.live2d.com/en/terms/live2d-free-material-license-agreement/).

| State | Motion |
|-------|--------|
| Idle | `mtn_00` (looping) |
| Clicked | `mtn_01` (tap reaction, one-shot) |
| Drag | `mtn_02` (looping while dragged) |

---

## Project Structure

```
Assets/
  Scripts/
    WindowManager.cs       # Win32 API: transparent window, topmost, drag
    PetController.cs       # State machine: Idle → Clicked → Dragging
    Live2DController.cs    # Live2D Cubism model loader & motion controller
    TrayIconManager.cs     # System tray icon
    ContextMenuHandler.cs  # Right-click context menu
    PetBootstrap.cs        # Startup bootstrapper
  Scenes/
    Main.unity             # Main scene
  Resources/
    Live2D/Natori/         # Natori model3.json, moc3, textures, motions
  Live2D/
    CubismSdk/             # Live2D Cubism SDK for Unity (submodule)
ProjectSettings/
  SETTINGS_README.md       # PlayerSettings guide
.github/
  workflows/
    build.yml              # CI: build on tag v*, release to GitHub Releases
```

---

## Requirements

| Tool | Version |
|---|---|
| Unity Editor | 2022.3 LTS (2022.3.62f1) |
| Unity module | Windows Build Support (IL2CPP) |
| OS (build host) | Windows 10/11 x64 (or CI via game-ci) |
| .NET | Standard 2.1 |

---

## Unity Personal License Activation (for CI)

Unity requires a valid license to build in CI. Follow the game-ci activation flow:

### Step 1 — Request a license file locally

```bash
# Run on a machine with Unity Editor installed
/path/to/Unity -batchmode -createManualActivationFile -logFile -
# Produces Unity_v2022.alf
```

Or use the game-ci GitHub Action:

```yaml
- uses: game-ci/unity-request-activation-file@v2
  # Produces Unity_v*.alf as an artifact
```

### Step 2 — Activate on Unity's website

1. Go to https://license.unity3d.com/manual
2. Upload the `.alf` file
3. Select **Unity Personal** license
4. Download the resulting `.ulf` file

### Step 3 — Add secrets to GitHub repository

In your repo: **Settings → Secrets and variables → Actions → New repository secret**

| Secret name | Value |
|---|---|
| `UNITY_LICENSE` | Full contents of the `.ulf` file |
| `UNITY_EMAIL` | Your Unity account email |
| `UNITY_PASSWORD` | Your Unity account password |

Reference: https://game.ci/docs/github/activation

---

## Local Build

1. Open the project in Unity 2022.3 LTS (2022.3.62f1)
2. **File → Build Settings**
   - Platform: **PC, Mac & Linux Standalone**
   - Target Platform: **Windows**
   - Architecture: **x86_64**
3. **Player Settings** (see `ProjectSettings/SETTINGS_README.md`)
4. Click **Build**

---

## Triggering CI Build & Release

Push a version tag to trigger the GitHub Actions workflow:

```bash
git tag v0.1.0
git push origin v0.1.0
```

The workflow will:
1. Build `StandaloneWindows64` using `game-ci/unity-builder@v4`
2. Upload the build as a GitHub Actions artifact
3. Create a GitHub Release with the build attached

---

## Architecture Notes

- `WindowManager.cs` uses `SetWindowLong` to strip the window border and enable `WS_EX_LAYERED`.
  `SetLayeredWindowAttributes` with colour key `#000000` punches through the black camera background, producing a transparent window without a separate alpha channel.
- `PetController.cs` drives a simple state machine. Mouse input is received via Unity's `Physics2DRaycaster` + `EventSystem` — no custom input polling.
- `Live2DController.cs` loads the Natori model at runtime from `Resources/Live2D/Natori`, maps pet states to Live2D motions, and drives playback via `CubismMotionController`.
