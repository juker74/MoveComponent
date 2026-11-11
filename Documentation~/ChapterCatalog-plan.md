# Chapter catalog editor implementation plan

> Execute the user-approved editor expansion in this session with subagent implementation and independent review.

**Goal:** Edit and exchange ordered chapter catalogs referencing existing ComposerGraph assets.

**Architecture:** Pure catalog documents/codec live in Runtime/VideoNodes; editor asset, Inspector and bundle file service live in Editor/VideoNodes. Reuse existing graph adapters and loader. Current VideoFlowDriver remains unchanged.

**Tech Stack:** Unity 2022.3.62f3, C#, existing SimpleJSON codec, NUnit EditMode.

**Spec:** ChapterCatalog-design.md (same folder).

## Global constraints

- Preserve existing generic graphs, node APIs, all third-party plugin files and GUIDs.
- Runtime catalog contains no editor assets or UnityEditor/NodeCanvas types.
- Keep chapter IDs stable on rename/reorder and preserve all identities on JSON round-trip.
- Validate complete content before publishing export or creating imported assets; no path escape or unrelated overwrite.
- Editor only; no chapter runner, cross-chapter variables, end/jump nodes, or new dependencies.

## Task 1: chapter catalog editor and data exchange

Files: Runtime/VideoNodes/ChapterCatalogDocument.cs and ChapterCatalogJsonCodec.cs; Editor/VideoNodes/ChapterCatalogAsset.cs, ChapterCatalogInspector.cs, ChapterCatalogFileService.cs; Tests/Editor/VideoNodesIntegration/ChapterCatalogTests.cs; corresponding metadata and required assembly references; Documentation~/ChapterCatalog.md and README.md.

- [x] Add meaningful failing EditMode checks for catalog round-trip, stable ordered IDs, duplicate/unsafe identity rejection, missing entries and partial import/export preservation. Observe a failure against missing feature before implementation.
- [x] Implement strict catalog schema/codec using GraphJsonCodec.DecodeValue/EncodeValue and existing GraphResult<T>.
- [x] Implement asset and file service using existing ComposerGraphAdapter, GraphJsonCodec and VideoFlowLoader; serialize all graphs before publishing a dedicated bundle directory. Imports create fresh Assets folders with rollback.
- [x] Implement Inspector with SerializedObject and ReorderableList, explicit new IDs on add, stable entry dropdown selection, open-graph button and start chapter dropdown. Provide Tools/GameFrameX/Node Composer/创建章节目录 and import menu entries.
- [x] Document menu usage and bundle schema. Add metadata for every new Unity asset.
- [x] Verify new tests, existing module tests, package validator and diff whitespace. Independently review requirements/correctness and fix material issues.

## Task 2: integrate and verify the package revision

- [x] Integrate reviewed changes into /Users/user/MoveComponent without touching unrelated project edits.
- [x] Make the current Unity project consume the verified package revision while preserving Git-based package installation; verify compilation and entry menus in that project.
- [x] Record exact test results, revision and user instructions.
