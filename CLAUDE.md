# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this repo is

Desktop app (C#/.NET 8) that plays back a configurable, ordered sequence of key presses
triggered by a global hotkey - e.g. hotkey `SPACE` runs `F1` -> `F3` -> `ALT+A`. It's a
generalized sibling of [`combo-app`](../combo-app) (a Dota 2-specific version of the same
idea): no fixed skill/item slots, no automatic left-click per step, no action-queue
Shift-hold. Just an ordered list of key tokens and a delay between them. See `plan.md`
in `combo-app` for the original design decisions this project inherits (Avalonia UI,
code-behind architecture, `SendInput` for input, JSON save/load) - they weren't re-litigated
here, only the domain model was simplified.

The UI both previews the sequence (`OnPreviewClick` fills `PreviewBox`) and executes it for
real: a global low-level keyboard hook (`GlobalKeyboardHook`) detects the configured hotkey
system-wide (independent of window focus) and triggers `ISequenceExecutor`, which sends the
actual key events via `SendInput`.

## Commands

```
dotnet build KeySequencer.sln          # build everything
dotnet test                            # run all tests (tests/KeySequencer.Tests)
dotnet test --filter FullyQualifiedName~RunAsync_PressesKeysInConfiguredOrder
dotnet run --project src/KeySequencer.UI   # launch the Avalonia app
```

## Architecture

Three projects under one solution:

- `src/KeySequencer.Core` — plain .NET class library, no UI dependency.
  - `Domain/`: `HotkeyConfig` (the trigger key + Ctrl/Alt), `SequenceConfiguration`
    (the ordered `Keys` list + hotkey + `PingMs` - the list's order *is* the execution
    order, there's no separate sequencing/ordering step like the Dota app's
    slot-position model). `SavedConfig` + `HotkeyConfigDto` are the mutable,
    JSON-serializable shape used for persistence.
  - `Services/VirtualKeyMapper`: resolves a single key token (e.g. `"Q"`, `"SPACE"`,
    `"F5"`) to its Win32 virtual-key code - named keys via a lookup table, single
    printable characters via `VkKeyScan`. Shared by `Win32InputSender` (key tokens can
    be `"+"`-joined with modifiers, e.g. `"ALT+A"`) and `GlobalKeyboardHook` (the
    hotkey string). Keys are always uppercased upstream (`MainWindow.NormalizeKey`),
    but letter tokens are lowercased again right before `VkKeyScan` here -
    `VkKeyScan('Q')` reports "needs Shift" because Shift is what makes the physical key
    type a *capital* Q, which is irrelevant for a hotkey/sequence token (the VK code is
    identical either way). Scanning the uppercase form directly would silently add a
    Shift keypress to every single-letter step in `Win32InputSender.PressKeyAsync`.
  - `Services/IInputSender` / `Win32InputSender`: sends real key presses via the Win32
    `SendInput` API (down+up pair, modifiers pressed before and released after the main
    key). Windows-only P/Invoke living in an otherwise-plain `net8.0` library -
    acceptable since the whole app is Windows-only. `PressKeyAsync` holds the main key
    down for `KeyHoldMs` (20ms) before releasing it - sending a down+up pair back-to-back
    with 0ms in between is unreliable for apps/games that poll input once per frame, so
    this is a deliberate minimum, not a magic number to shrink for "faster" sequences.
    Unlike `combo-app`'s `Win32InputSender`, there is no mouse click and no Shift-hold
    method - this app has no per-step click concept and no Dota-specific action-queue
    modifier; a modifier that needs to be held is just part of the key token itself
    (e.g. `"ALT+A"`), handled entirely within `PressKeyAsync`.
  - `Services/ISequenceExecutor` / `SequenceExecutor`: plays back `SequenceConfiguration.Keys`
    in list order, awaiting each `PressKeyAsync` call and then delaying `PingMs` before the
    next step (no delay after the last step). Uses `Interlocked.Exchange` as a reentrancy
    guard, so a second trigger while a sequence is already running is a silent no-op.
    Unlike `combo-app`'s `ComboExecutor`, there's no key/click concurrency to manage since
    there's no click at all - this is a plain sequential loop.
  - `Services/ConfigService`: `Save(filePath, config)` writes `SavedConfig` as camelCase
    JSON to an exact path - no filename derivation/sanitization here, unlike
    `combo-app`'s `ConfigService`. The UI's "Salvar" button opens a native Save-As dialog
    (see below) so the caller always supplies a real path the user picked; `ConfigDirectory`
    (`<app base dir>/configs`) still exists as the dialog's *suggested* starting folder and
    the target of "Abrir pasta", but a config can be saved anywhere. `Load(filePath)` is
    unchanged from `combo-app`.

- `src/KeySequencer.UI` — Avalonia desktop app (net8.0, WinExe), single-window
  code-behind (no MVVM), same pattern as `combo-app`.
  - `MainWindow.axaml.cs` is the entire UI logic. The key sequence itself is edited as
    plain text in `KeysTextBox` (one token per line, `AcceptsReturn="True"`) rather than
    a fixed set of named controls per slot - `ParseKeys` splits on newlines, normalizes
    each line via `NormalizeKey`, and drops blanks. This is deliberately simpler than
    `combo-app`'s per-slot checkbox/textbox/order-box control wiring, since there's no
    fixed slot count or type here.
  - `UpdateHotkeyTarget()` pushes the current hotkey text + Ctrl/Alt checkboxes into the
    hook, **and also sets `_hotkeyHook.Suppress = _enabled`**. This second part matters:
    `GlobalKeyboardHook.Suppress` defaults to `true` and, if left there unconditionally
    (as `combo-app` does - it never toggles `Suppress` at all), the hotkey key is eaten
    system-wide *regardless of the app's own enabled/disabled toggle* - e.g. with the
    default hotkey `SPACE`, you'd be unable to type a space anywhere on the system,
    even with the app "DESLIGADO". `UpdateHotkeyTarget()` is called from the hotkey
    controls' `PropertyChanged` handler, from `OnToggleStatusClick`, and from
    `ResetToDefaults`/`ApplySavedConfig` - every place `_enabled` or the hotkey
    definition can change, so `Suppress` never drifts out of sync with the toggle.
  - `OnSaveConfigClick` uses `StorageProvider.SaveFilePickerAsync` (same
    `IStorageFolder`/`FilePickerFileType` pattern as `OnLoadConfigClick`'s
    `OpenFilePickerAsync`) instead of writing straight to `ConfigDirectory` - lets the
    user pick any save location, not just the fixed `configs/` folder. `ConfigNameBox`
    only supplies the dialog's *suggested* filename; after the dialog returns, the
    actual chosen file's name (without extension) becomes `config.Name` and is written
    back into `ConfigNameBox`, so the two never drift apart.
  - Only suppress while armed is intentional, not just "less surprising": if the
    physical key also worked normally while armed, every ordinary press of it -
    typing a chat message, and in a game, whatever action that key already performs -
    would *also* re-fire the whole sequence on top of the normal keystroke. Consuming
    the key while armed is what makes it usable as a dedicated trigger instead of a
    key that does two unrelated things at once.
  - System tray: same `TrayIcon`/`Closing`/`WindowState` pattern as `combo-app`
    (`CreateTrayIcon`, `HideToTray`, `RestoreFromTray`, `_reallyClosing` gate) - closing
    the window (X) or minimizing hides to tray instead of exiting; only the tray menu's
    "Sair" really quits. `Assets/app.ico` is a generated placeholder (blue rounded
    square, three connected dots representing "a sequence of steps") wired as the
    `.exe`'s icon, the window icon, and the tray icon - not reused Dota artwork like
    `combo-app`'s icon, since this app isn't Dota-specific.
  - `app.manifest`, `Program.cs` (`timeBeginPeriod(1)`/`AboveNormal` process priority)
    are carried over from `combo-app` unchanged - the reasoning (tight `Task.Delay`
    timing for key holds/`PingMs`, resilience to a CPU-heavy foreground app) applies
    identically here.
  - `App.axaml` forces `RequestedThemeVariant="Dark"` for the same reason as
    `combo-app`: the UI is hand-styled for dark, and default-variant Fluent parts (e.g.
    the hotkey `ComboBox`'s internal `PART_EditableTextBox`) would otherwise fall back
    to Light-theme resources when the OS is in light mode.

- `tests/KeySequencer.Tests` — xUnit, references only `KeySequencer.Core` (UI is
  untested). `VirtualKeyMapperTests` is carried over from `combo-app` unchanged.
  `SequenceExecutorTests` is new and possible specifically because this app's executor
  only depends on `IInputSender` (no `Win32InputSender`/hook involved) - a fake
  `IInputSender` records calls without sending real keystrokes, letting both the
  execution order and the reentrancy guard be tested directly (`combo-app` deliberately
  leaves its executor untested for the same class of reason this one *can* be tested:
  no real OS-level side effects here to avoid).

## Distribution

No installer is set up. To share the app, publish a self-contained single-file build and
send the `.exe` directly (see `README.md`'s "Publicando" section for the full explanation
of each flag):

```
dotnet publish src/KeySequencer.UI -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None
```

`IncludeNativeLibrariesForSelfExtract` matters here specifically because Avalonia/SkiaSharp
ship native (non-.NET) libraries - without it, `PublishSingleFile` still leaves
`libSkiaSharp.dll` etc. sitting next to the `.exe` instead of bundling them in. Verified by
actually running the published `.exe` standalone (background-launched, confirmed the
process stayed alive, killed it) - not just inspecting the publish output.

The result is unsigned, so Windows SmartScreen will warn on first run on another machine
("more info" -> "run anyway") - expected for a personal-use tool, not a bug to fix here.

## Shell

The operator is on Windows using PowerShell/Git Bash. `dotnet` commands work the same in
either.
