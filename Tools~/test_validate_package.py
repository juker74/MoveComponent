#!/usr/bin/env python3
"""Check GUID assembly references and missing-target failures."""
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
    reference = package / "Runtime/ValidatorReference.asmref"
    reference_data = {"reference": "GUID:9dca19d9870d14bd1b8a9840387c2776"}
    reference.write_text(json.dumps(reference_data), encoding="utf-8")
    Path(str(reference) + ".meta").write_text(
        "fileFormatVersion: 2\nguid: 66307ce29ecf45ae8bd9a9cc8ac6eb6c\n", encoding="utf-8")
    valid = subprocess.run(command, capture_output=True, text=True)
    reference_data["reference"] = "GUID:" + "0" * 32
    reference.write_text(json.dumps(reference_data), encoding="utf-8")
    invalid_reference = subprocess.run(command, capture_output=True, text=True)
    assert (valid.returncode == 0 and invalid_reference.returncode != 0 and
            "Undeclared assembly: Runtime/ValidatorReference.asmref" in invalid_reference.stderr), (
        f"Valid asmref failed:\n{valid.stderr}\nMissing asmref GUID result:\n{invalid_reference.stderr}")
    reference.unlink()
    Path(str(reference) + ".meta").unlink()
    assembly = package / "Plugins/ParadoxNotion/NodeCanvas/NodeCanvas.asmdef"
    data = json.loads(assembly.read_text(encoding="utf-8-sig"))
    assert data["references"][0] == "GUID:66c2eb417c67ad849907d0769db96dbf"
    data["references"][0] = "GUID:" + "0" * 32
    assembly.write_text(json.dumps(data), encoding="utf-8")
    invalid = subprocess.run(command, capture_output=True, text=True)
    assert invalid.returncode != 0, "Missing GUID target was accepted"
    assert "Undeclared assembly: NodeCanvas" in invalid.stderr, invalid.stderr
print("PASS: asmref and bundled GUID assemblies resolve; missing GUID targets are rejected")
