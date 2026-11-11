# Changelog

## 1.2.0 — 2026-11-11

- Add the GameFrameX MovieComponent → IMovieManager → MovieManager entry with explicit catalog loading, multiple cached chapter graphs and one executing chapter.
- Forward module operations, immutable snapshots and chapter-aware events through the scene component; bind catalog/graph TextAssets, UI services, video output and clips explicitly.
- Define Stop and unload scopes, fresh values on chapter replay, retained values in graph loops, and stale callback rejection across chapter switches.
- Keep VideoFlowDriver as a thin compatibility adapter with existing Host/FSM entry points; the GF module is updated and shut down only by the framework.
- Document v1.2.0 installation and resource boundaries. Local and private remote Git Unity validation each pass all 165 tests across five assemblies; package checks include assembly references. GameFrameX.Unity Git import and MovieComponent/MovieManager integration pass; evidence is recorded in Documentation~/Validation.md.

## 1.1.0 — 2026-11-11

- Add a chapter catalog asset and Inspector with stable IDs, ordered chapters, graph references, explicit entry selection and Undo/Redo.
- Add strict runtime catalog DTO/JSON interchange, complete chapter-bundle export, and import into fresh assets with validation and rollback.
- Keep the existing single-graph driver unchanged; chapter scheduling is outside this editor release.

## 1.0.1 — 2026-11-11

- Bundle the complete existing ParadoxNotion NodeCanvas / CanvasCore source tree and editor resources under Plugins/ParadoxNotion, preserving original assemblies and .meta GUIDs.
- Use a private Git repository with v1.0.1 installation examples and existing Git credentials; migrate duplicate host-project plugin copies out of Assets.
- Retain Gavalakis Evangelos / ParadoxNotion credits and the original FullSerializer MIT notice; clarify that the root Apache-2.0 text does not relicense NodeCanvas / CanvasCore.
- Verify all 15 bundled assemblies, editor Resources and 87 EditMode tests in an isolated UPM file installation without a local plugin copy.
- Resolve GUID assembly references in package validation and reject missing targets with a runnable regression check.

## 1.0.0 — 2026-11-11

- Export the complete typed node editor, strict JSON interchange and node view extensions.
- Include video previews, drag-and-drop VideoClips, frame outputs and dynamic UI branch ports.
- Include flow loading, immutable directed graph, per-activation lifecycle and state-driven video/UI execution.
- Include shared runtime data cells, typed mutations/conditions and extensible value operations.
- Include GameFrameX MovieUIForm and optional FSM update integration.
- Convert test resource references to package paths and define explicit Undo test group boundaries.
- Keep video preview controls stable while the native decoder prepares between IMGUI events.
- Verify local Git UPM installation and all 87 package tests in an isolated Unity project, with NodeCanvas installed separately (historical 1.0.0 validation).
