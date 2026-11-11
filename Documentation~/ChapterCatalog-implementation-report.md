# Chapter catalog implementation report

Status: delivered in private remote Git package v1.1.0, code commit 2b008ad8fad7d3af61745c8d812573da35e4ea72. Implementation, independent review, isolated local-file and remote-Git Unity test runs, and target GameFrameX.Unity integration verification are complete. Release publication was performed by the controller; this worker only updates the documentation follow-up and does not change the published tag.

## Delivered

- Runtime/VideoNodes/ChapterCatalogDocument.cs: pure ordered catalog and chapter records.
- Runtime/VideoNodes/ChapterCatalogJsonCodec.cs: strict versioned JSON, stable IDs, start selection, unique identities/resource keys and safe relative paths; reuses GraphJsonCodec value codec and GraphResult.
- Editor/VideoNodes/ChapterCatalogAsset.cs: editor-only asset referencing ComposerGraph assets.
- Editor/VideoNodes/ChapterCatalogInspector.cs: SerializedObject/ReorderableList editing with undo, generated IDs, entry selection by document ID, graph opening, validation, bundle export and import menus.
- Editor/VideoNodes/ChapterCatalogFileService.cs: validation through ComposerGraphAdapter and VideoFlowLoader(ComposerGraph.Registry), full-batch directory publication, owned-output checks, metadata retention, fresh-asset import, path/symlink checks and rollback diagnostics. Imported graph assets live under Graphs/ to avoid collisions with ChapterCatalog.asset.
- Tests/Editor/VideoNodesIntegration/ChapterCatalogTests.cs: typed public-behavior tests, including actual filesystem exchange and AssetDatabase import.
- Documentation~/ChapterCatalog.md and README.md: entry points, configuration, bundle format, import behavior and explicit editor-only scope.

The only existing assembly change is the video Editor assembly's references to the existing Game.VideoFlow.Runtime and Game.VideoFlow.Contracts assemblies. Driver code, generic graph APIs, plugin files and existing metadata were not modified. Six new Unity files have metadata.

## Verification evidence

Controller observed the real RED run before implementation: Unity 2022.3.62f3, one NUnit test failed with the missing chapter codec assertion; compilation succeeded. Evidence: /tmp/MoveComponent-chapters-red-tests.xml.

Controller's first complete Unity run compiled successfully and reported 105 tests, 104 passed, one fixture failure at ChapterCatalogTests.cs:103. Root cause: the duplicate-field test's string replacement expected colon spacing different from the real SimpleJSON pretty writer, so it accidentally fed valid JSON. Fixed by inserting a duplicate field directly after the root opening brace and asserting the fixture changed. The codec was already using the strict parser and required no change for this finding. Evidence: /tmp/MoveComponent-chapters-tests.xml.

Project-root resolution was made independent of the process working directory, and rollback failures now report the retained asset folder instead of silently assuming cleanup succeeded. An attempted test that changed Unity's process working directory caused a native abort because Unity forbids that operation. That invalid test was removed; production never changes the working directory. Evidence: /tmp/MoveComponent-chapters-tests-final.log.

Independent review found that NodeCanvas records construction Undo while adapter.Create builds graphs. A controlled regression passed without import but failed with import: the next Undo changed imported graph content rather than reverting the user's preceding catalog rename. Per-object ClearUndo did not remove the plugin's diff-based RecordObject history. The importer now constructs temporary graphs in a fresh Undo group, saves their native JSON and Unity object reference lists, reverts that group in finally, and destroys the temporary graphs. Only afterward does it create final empty graph objects and Deserialize the saved data before asset creation. Source inspection confirms that this Deserialize path does not record construction Undo. The operation never clears global Undo or reverts the caller's earlier group. Evidence for the failed prior approach: /tmp/MoveComponent-chapters-undo-fixed.xml.

The controller's focused rerun passed both cases, 2/2 with no failures: /tmp/MoveComponent-chapters-undo-clean.xml. The import case reports NodeCanvas's empty-JSON warning when Undo restores temporary graphs to their initial empty state; final assets and the preceding user Undo both remain intact. The plugin and generic adapter are unchanged.

The round-trip check also unloads and reloads persisted catalog/graph assets before asserting identities and entry selections; that check passed in /tmp/MoveComponent-chapters-tests-verified.xml (106 tests, 105 passed, with only the import Undo regression failing).

The final isolated Unity 2022.3.62f3 local-file run passed all 107 tests across five EditMode assemblies, with zero failures and zero skips. Evidence: /tmp/MoveComponent-chapters-complete.xml. Per assembly: Game.VideoFlow.Tests 42/42, Game.VideoFlow.Unity.Tests 8/8, GameFrameX.NodeComposer.Tests 16/16, GameFrameX.NodeComposer.Video.Editor.Tests 30/30 and GameFrameX.NodeComposer.Video.Tests 11/11. The controller confirmed final independent review passed.

The isolated project then installed the private remote Git package at v1.1.0, resolving code commit 2b008ad8fad7d3af61745c8d812573da35e4ea72. The full five-assembly suite passed again with 107/107, zero failures and zero skips. Evidence: /tmp/MoveComponent-chapters-git.xml. The published v1.1.0 tag remains fixed at that code revision; these final validation notes are a later documentation update.

The target GameFrameX.Unity project refreshed successfully with package source Git and version 1.1.0. Its verification report confirms all 15 package assemblies, seven bundled Editor resources, the retained ComposerGraph script GUID 22a41af39b6684b1087cc91c914c50b7, existing graph asset loading, ChapterCatalogInspector binding, both Tools menu entries and the Chapter Catalog CreateAssetMenu. Evidence: /tmp/MoveComponent-chapter-project-report.json (status Passed). Temporary validation helpers and metadata were removed. This target-project check verifies integration and Editor entry points; the 107-test results above are from the isolated project.

`python3 Tools~/validate_package.py` currently passes: 507 C# files, 15 assemblies, 764 unique GUIDs. `git diff --check` passes.

## Deliberate scope

- ChapterRunner, automatic chapter transitions, chapter-end/jump nodes, save state and cross-chapter variable scopes remain outside this editor task.
- Import creates new assets and preserves document identities; it does not overwrite existing assets or preserve their Unity GUIDs.
- Export replaces only empty directories or recognized tool-owned bundles with no unrelated files. Existing metadata for retained JSON files is preserved; removed chapters are removed from the dedicated output.
- A duplicated Graph document ID is an authoring validation error; it is not silently rewritten.
