# FlexCanvas

**English** | [简体中文](README.zh-CN.md)

**FlexCanvas** is a spatial window container and workspace management system for Windows. It hosts the real windows of external applications inside a unified canvas, preserves genuine native interaction, and lets you freely arrange, snap, and switch between hosted windows — all without faking content with screenshots, DWM thumbnails, or video streams.

> Status: phases S0–S3 are implemented and verified (feasibility POC, discovery, production embedding/recovery, and the spatial canvas). Workspace persistence (S4) and release hardening (S5) are not implemented yet.

## Highlights

- **Real HWND embedding** — cross-process windows are re-parented into FlexCanvas host surfaces via Win32 `SetParent`, with full capture of the original parent, styles, placement, DPI, and process identity so windows can be restored exactly.
- **Guaranteed safe recovery** — every embedding stores a rollback snapshot; detach is idempotent, failures keep the host alive for retry, and closing FlexCanvas never kills hosted applications.
- **Dual-mode spatial canvas** (4096 × 4096 DIP, overlapping allowed):
  - **Geometry overview** — Windows-Display-Settings-style rectangles showing each session's real name, logical size, original physical size, and DPI; drag with snap guides.
  - **Native 1:1 mode** — the actual windows, at their real size, clipped natively to the visible viewport. View zoom/pan only changes what you see; it never resizes a hosted window.
- **Snapping engine** — edge alignment, seamless adjacency, and equal-gap snapping with a strict 12 DIP threshold, deterministic tie-breaking, and zero drift over repeated operations.
- **Explicit resize semantics** — a window's real size only changes through explicit user action; externally driven size changes are read back into the shared layout with echo-tolerant loop prevention.
- **Non-fullscreen focus** — selecting a container raises its host in the Z order without maximizing, moving other windows, or rebuilding any session.

## Compatibility Boundary (important)

FlexCanvas deliberately embeds only windows that pass strict gates:

- The target must be a top-level window with **Per-Monitor V2 DPI awareness** matching the host.
- The target must run at the **same privilege/integrity level** as FlexCanvas.

Windows that fail these checks (most legacy Win32 apps, DPI-unaware apps, elevated windows) are rejected with an explanatory code such as `DPI_MISMATCH` or `ACCESS_DENIED` instead of being embedded incorrectly. FlexCanvas never injects code, bypasses UIPI, elevates itself, or kills external processes. See [docs/COMPATIBILITY_MATRIX.md](docs/COMPATIBILITY_MATRIX.md) for the verified support matrix.

## Architecture

```text
FlexCanvas.App              WPF entry, views, dual-mode canvas, session UI
FlexCanvas.Application      View models, embedding engine orchestration, ports
FlexCanvas.Domain           Pure layout, snapping, geometry projection, state machines
FlexCanvas.Infrastructure   Win32 P/Invoke, embedding API, discovery, diagnostics
tools/Probes                Self-contained Win32 / WPF / WinForms test targets
tools/WindowHostProbe       Standalone POC host
```

- Reference direction: `App → Application → Domain`, `Infrastructure → Application + Domain`; the Domain layer has no WPF/Win32 dependencies.
- All native mutations are serialized through a dedicated STA dispatcher; WinEvent hooks are registered and released in pairs.

## Requirements

- Windows 10/11 x64
- [.NET SDK 8.0.426](https://dotnet.microsoft.com/download/dotnet/8.0) (pinned by `global.json`)
- An interactive desktop session (tests launch real windows)

## Build & Test

```powershell
dotnet restore FlexCanvas.sln
dotnet build FlexCanvas.sln -c Release
dotnet test FlexCanvas.sln -c Release --filter "Category!=NativeIntegration"
```

The full test suite (131 tests at the time of writing) also includes native integration tests that launch the bundled Probe executables, embed, resize, and restore real windows, and shut them down cleanly:

```powershell
dotnet test FlexCanvas.sln -c Release
```

## Run

```powershell
dotnet run --project src\FlexCanvas.App\FlexCanvas.App.csproj -c Release --no-build
```

1. On the **Window Discovery** tab, refresh and select a target (use the bundled probes for a known-good target).
2. Click **Check Selected Window** for the read-only compatibility report.
3. Embed either into an independent host window (**Embed Selected Window**) or directly onto the canvas at 1:1 (**Embed to Canvas (1:1)**).
4. On the **Spatial Canvas** tab, switch between the geometry overview and the native 1:1 mode; arrange in the overview, interact in native mode.
5. Close normally — hosted windows are restored to their original state.

Repeatable UI smoke scripts live in `tests/Invoke-S1UiSmoke.ps1`, `Invoke-S2UiSmoke.ps1`, and `Invoke-S3CanvasUiSmoke.ps1`.

## Project Documentation

- [Development specification](FlexCanvas_Project_Development_Specification.md) — full product/engineering specification
- [Task registry](TASKS.md) / [Acceptance criteria](ACCEPTANCE.md)
- [Development changelog](CHANGELOG_DEV.md) / [Project context](PROJECT_CONTEXT.md) / [Phase plan](TASK_PLAN.md)
- Reports: [S0](docs/S0_POC_REPORT.md) · [S1](docs/S1_REPORT.md) · [S2](docs/S2_REPORT.md) · [S3](docs/S3_REPORT.md)
- Decisions: [ADR 0001](docs/ADR/0001-s0-prototype.md) · [0002](docs/ADR/0002-s1-discovery.md) · [0003](docs/ADR/0003-s2-embedding.md) · [0004](docs/ADR/0004-s3-canvas.md)
- [Compatibility matrix](docs/COMPATIBILITY_MATRIX.md)

## Roadmap

- **S4 — Workspaces**: versioned JSON persistence, atomic writes, partial restore.
- **S5 — Hardening**: extended application/DPI/privilege compatibility matrix, diagnostics export, release build.

## License

Released under the [MIT License](LICENSE).
