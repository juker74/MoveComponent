# Third-party sources and license scope

This private repository contains the MoveComponent sources exported from GameFrameX.Unity and a complete bundled ParadoxNotion plugin tree. Existing namespaces, assembly identities and script GUIDs are preserved.

The Apache-2.0 text retained in LICENSE.md applies to the GameFrameX-derived MoveComponent sources. It does not assign Apache-2.0 to the bundled NodeCanvas / CanvasCore plugin. Package metadata points to this notice because the repository contains components with separate license terms; their original authorship and applicable licenses remain in place.

## Bundled NodeCanvas / CanvasCore

The complete existing NodeCanvas and CanvasCore source and resource folders are included under `Plugins/ParadoxNotion`. The original README credits **Gavalakis Evangelos**; the project and publisher are **ParadoxNotion**. The bundled CanvasCore source reports `FRAMEWORK_VERSION = 3.30`. Official source: https://nodecanvas.paradoxnotion.com/ . No separate plugin installation is required.

NodeCanvas / CanvasCore retains its original author's applicable licensing terms. Its inclusion in this private repository does not relicense the plugin under the root Apache-2.0 text.

NodeCanvas uses a modified version of Full Serializer under the MIT License, as stated in the original `Plugins/ParadoxNotion/NodeCanvas/README.txt`. The original FullSerializer MIT copyright and license text is retained at:

`Plugins/ParadoxNotion/CanvasCore/Common/Runtime/Serialization/Full Serializer/License (FullSerializer).txt`

## External package dependencies

SimpleJSON, GameFrameX packages, Unity UGUI, TextMeshPro and Test Framework remain external dependencies; their sources are not copied into this repository and remain under their respective licenses. See `Documentation~/Dependencies.md` and `Documentation~/dependency-sources.json` for sources, pinned dependency versions and bundled-plugin details. See `Documentation~/Validation.md` for the separate validation records of each MoveComponent version.

## Sources and credits

- GameFrameX.Unity / GameFrameX — Blank (as credited by package metadata) and contributors: https://github.com/GameFrameX/GameFrameX.Unity
- NodeCanvas / CanvasCore — Gavalakis Evangelos / ParadoxNotion: https://nodecanvas.paradoxnotion.com/ (complete source and resources bundled in this private repository).
- Full Serializer — Jacob Dufault (copyright 2014); original MIT notice retained at the path above.
- MissionSystem — PrinceBiscuit (GitHub 529324416): https://github.com/529324416/MissionSystem (design reference).
- User-provided article: https://zhuanlan.zhihu.com/p/718702259 (design reference; author could not be independently verified).
- Unity.gitignore — GitHub gitignore contributors: https://github.com/github/gitignore/blob/main/Unity.gitignore (standard template used by .gitignore, with local additions).
