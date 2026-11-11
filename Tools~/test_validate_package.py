#!/usr/bin/env python3
"""Check the real bundled plugin's GUID reference and a missing-target failure."""
import json
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

root = Path(__file__).resolve().parents[1]
with tempfile.TemporaryDirectory(prefix="movecomponent-validator-") as temporary:
    package = Path(temporary) / "package"
    shutil.copytree(root, package, ignore=shutil.ignore_patterns(".git", "__pycache__"))
    command = [sys.executable, str(package / "Tools~/validate_package.py")]
    valid = subprocess.run(command, capture_output=True, text=True)
    assert valid.returncode == 0, valid.stderr
    assembly = package / "Plugins/ParadoxNotion/NodeCanvas/NodeCanvas.asmdef"
    data = json.loads(assembly.read_text(encoding="utf-8-sig"))
    assert data["references"][0] == "GUID:66c2eb417c67ad849907d0769db96dbf"
    data["references"][0] = "GUID:" + "0" * 32
    assembly.write_text(json.dumps(data), encoding="utf-8")
    invalid = subprocess.run(command, capture_output=True, text=True)
    assert invalid.returncode != 0, "Missing GUID target was accepted"
    assert "Undeclared assembly: NodeCanvas" in invalid.stderr, invalid.stderr
print("PASS: bundled GUID assembly resolves; missing GUID target is rejected")
