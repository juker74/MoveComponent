#!/usr/bin/env python3
"""Validate the repository layout using only Python's standard library."""
import json
import re
import sys
from pathlib import Path

root = Path(__file__).resolve().parents[1]
errors = []

def check(condition, message):
    if not condition:
        errors.append(message)

manifest = json.loads((root / "package.json").read_text(encoding="utf-8"))
check(manifest["name"] == "com.gameframex.unity.movecomponent", "Unexpected package identity")
check(re.fullmatch(r"\d+\.\d+\.\d+", manifest["version"]), "Invalid package version")
for name, version in manifest["dependencies"].items():
    check(re.fullmatch(r"\d+\.\d+\.\d+", version), f"Dependency must use a version: {name}")
for folder in ("Runtime", "Editor", "Tests", "Samples~", "Documentation~",
               "Plugins/ParadoxNotion/CanvasCore", "Plugins/ParadoxNotion/NodeCanvas"):
    check((root / folder).is_dir(), f"Missing directory: {folder}")
for sample in manifest["samples"]:
    check((root / sample["path"]).is_dir(), "Missing registered sample")

assemblies = {}
assembly_guids = {}
for path in root.rglob("*.asmdef"):
    data = json.loads(path.read_text(encoding="utf-8-sig"))
    check(data["name"] not in assemblies, f"Duplicate assembly: {data['name']}")
    assemblies[data["name"]] = data
    metadata = Path(str(path) + ".meta")
    if metadata.exists():
        guid = re.search(r"^guid: ([0-9a-f]{32})$", metadata.read_text(), re.M)
        if guid:
            assembly_guids[guid.group(1)] = data["name"]
    if "Editor" in path.relative_to(root).parts:
        check(data.get("includePlatforms") == ["Editor"], f"Editor assembly not isolated: {path}")
external = {"SimpleJSON.Runtime", "GameFrameX.UI.Runtime",
            "GameFrameX.UI.UGUI.Runtime", "GameFrameX.FSM.Runtime"}
for name, data in assemblies.items():
    for reference in data.get("references", []):
        target = assembly_guids.get(reference[5:]) if reference.startswith("GUID:") else reference
        check(target in assemblies or target in external, f"Undeclared assembly: {name} -> {reference}")

seen_guids = {}
for path in root.rglob("*"):
    relative = path.relative_to(root)
    if any(part.endswith("~") for part in relative.parts) or any(part.startswith(".") for part in relative.parts):
        continue
    if path.suffix == ".meta":
        match = re.search(r"^guid: ([0-9a-f]{32})$", path.read_text(), re.M)
        check(match is not None, f"Invalid GUID: {relative}")
        if match:
            guid = match.group(1)
            check(guid not in seen_guids, f"Duplicate GUID: {relative} / {seen_guids.get(guid)}")
            seen_guids[guid] = relative
        check(Path(str(path)[:-5]).exists(), f"Orphan metadata: {relative}")
    else:
        check(Path(str(path) + ".meta").exists(), f"Missing metadata: {relative}")

package_prefix = "Packages/" + manifest["name"] + "/"
for path in list(root.rglob("*.cs")) + list(root.rglob("*.asmdef")) + [root / "package.json"]:
    text = path.read_text(encoding="utf-8-sig")
    check("/Users/" not in text and "/tmp/" not in text, f"Machine-specific path: {path.relative_to(root)}")
    check("Assets/Scripts/Extensions/NodeComposerVideo/Tests" not in text, f"Old fixture path: {path}")
    if path.is_relative_to(root / "Runtime"):
        check(not re.search(r"^\s*using UnityEditor", text, re.M), f"Editor API in Runtime: {path}")
    for package_path in re.findall(r'"(Packages/[^"\n]+)"', text):
        if package_path.startswith(package_prefix):
            check((root / package_path[len(package_prefix):]).exists(), f"Missing package resource: {package_path}")

for sample in (root / "Samples~").rglob("*.json"):
    graph = json.loads(sample.read_text())
    check(graph["schemaVersion"] == 1, f"Unsupported sample schema: {sample}")
    ids = {node["id"] for node in graph["nodes"]}
    for edge in graph["edges"]:
        check(edge["sourceNodeId"] in ids and edge["targetNodeId"] in ids, f"Dangling sample edge: {sample}")

if errors:
    print("\n".join(errors), file=sys.stderr)
    sys.exit(1)
print(f"PASS: {len(list(root.rglob('*.cs')))} C# files, {len(assemblies)} assemblies, "
      f"{len(seen_guids)} unique GUIDs, all package resources and declared references present")
