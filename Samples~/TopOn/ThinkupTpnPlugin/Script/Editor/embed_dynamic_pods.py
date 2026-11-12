#!/usr/bin/env python3
"""Embed Pods dynamic xcframeworks into Unity-iPhone Embed Frameworks (SOURCE_ROOT).

CocoaPods with `use_frameworks! :linkage => :static` links dynamic vendor SDKs into
UnityFramework but does not embed them into the app. This script patches
Unity-iPhone.xcodeproj after `pod install`.

Run from the Xcode project root (sibling of Pods/):
  python3 embed_dynamic_pods.py
"""
from __future__ import print_function

import os
import re
import subprocess
import sys
import uuid

try:
    from pathlib import Path
except ImportError:
    sys.stderr.write("Python 3 with pathlib is required\n")
    sys.exit(1)


def new_id():
    return uuid.uuid4().hex[:24].upper()


def is_dynamic_xc(xc):
    for fw in xc.rglob("*.framework"):
        if ".dSYM" in str(fw):
            continue
        binary = fw / fw.stem
        if not binary.is_file():
            continue
        try:
            out = subprocess.check_output(["file", str(binary)], text=True)
        except Exception:
            continue
        if "dynamically linked shared library" in out:
            return True
    return False


def find_sourceroot_ref(text, path):
    pat = re.compile(
        r"([A-F0-9]{24}) /\* [^*]+ \*/ = \{isa = PBXFileReference;[^}]*path = "
        + re.escape(path)
        + r";[^}]*sourceTree = SOURCE_ROOT;"
    )
    m = pat.search(text)
    if m:
        return m.group(1)
    pat2 = re.compile(
        r"([A-F0-9]{24}) /\* [^*]+ \*/ = \{isa = PBXFileReference;[^}]*sourceTree = SOURCE_ROOT;[^}]*path = "
        + re.escape(path)
        + r";"
    )
    m = pat2.search(text)
    return m.group(1) if m else None


def main():
    root = Path(os.getcwd())
    if len(sys.argv) > 1:
        root = Path(sys.argv[1]).resolve()
        os.chdir(str(root))

    pods = root / "Pods"
    pbx_path = root / "Unity-iPhone.xcodeproj" / "project.pbxproj"
    if not pods.is_dir() or not pbx_path.is_file():
        sys.stderr.write("embed_dynamic_pods: Pods/ or Unity-iPhone.xcodeproj missing in %s\n" % root)
        return 1

    dyn = []
    for xc in pods.rglob("*.xcframework"):
        s = str(xc.relative_to(root)).replace("\\", "/")
        if "Target Support Files" in s or ".dSYM" in s:
            continue
        # Incomplete vendor packages (e.g. OMSDK_Appodeal/Dynamic) break Xcode Embed.
        if not (xc / "Info.plist").is_file():
            print("embed_dynamic_pods: skip (no Info.plist): %s" % s)
            continue
        if is_dynamic_xc(xc):
            dyn.append(s)

    if not dyn:
        print("embed_dynamic_pods: no dynamic xcframeworks found")
        return 0

    text = pbx_path.read_text()

    # Find Embed Frameworks phase (any id)
    phase_re = re.compile(
        r"([A-F0-9]{24} /\* Embed Frameworks \*/ = \{\n"
        r"\t\t\tisa = PBXCopyFilesBuildPhase;\n"
        r"\t\t\tbuildActionMask = 2147483647;\n"
        r"\t\t\tdstPath = \"\";\n"
        r"\t\t\tdstSubfolderSpec = 10;\n"
        r"\t\t\tfiles = \()(.*?)(\);\n"
        r"\t\t\tname = \"Embed Frameworks\";\n"
        r"\t\t\trunOnlyForDeploymentPostprocessing = 0;\n"
        r"\t\t\};)",
        re.S,
    )
    m = phase_re.search(text)
    if not m:
        sys.stderr.write("embed_dynamic_pods: Embed Frameworks phase not found\n")
        return 1

    # Keep UnityFramework embed line if present
    keep_lines = []
    for line in m.group(2).splitlines():
        if "UnityFramework.framework in Embed Frameworks" in line:
            keep_lines.append(line if line.endswith(",") or not line.strip() else line)
            break
    if not keep_lines:
        # Unity always has this; if missing, leave empty and let Unity recreate
        keep_lines = []

    new_build_entries = []
    new_file_refs = []
    embed_lines = list(keep_lines)

    for path in dyn:
        name = Path(path).name
        # Skip if already embedded via SOURCE_ROOT path
        if (
            ("path = %s;" % path) in text
            and "sourceTree = SOURCE_ROOT;" in text
            and ("%s in Embed Frameworks" % name) in m.group(2)
        ):
            # still rebuild cleanly below; check properly after
            pass

        fr = find_sourceroot_ref(text, path)
        if not fr:
            fr = new_id()
            new_file_refs.append(
                "\t\t%s /* %s */ = {isa = PBXFileReference; lastKnownFileType = wrapper.xcframework; "
                "name = %s; path = %s; sourceTree = SOURCE_ROOT; };\n" % (fr, name, name, path)
            )
        bid = new_id()
        new_build_entries.append(
            "\t\t%s /* %s in Embed Frameworks */ = {isa = PBXBuildFile; fileRef = %s /* %s */; "
            "settings = {ATTRIBUTES = (CodeSignOnCopy, RemoveHeadersOnCopy, ); }; };\n"
            % (bid, name, fr, name)
        )
        embed_lines.append("\t\t\t\t%s /* %s in Embed Frameworks */," % (bid, name))
        print("embed_dynamic_pods: + %s" % path)

    new_files_block = "\n" + "\n".join(embed_lines) + "\n\t\t\t"
    text = phase_re.sub(m.group(1) + new_files_block + m.group(3), text, count=1)
    if new_build_entries:
        text = text.replace(
            "/* End PBXBuildFile section */",
            "".join(new_build_entries) + "/* End PBXBuildFile section */",
        )
    if new_file_refs:
        text = text.replace(
            "/* End PBXFileReference section */",
            "".join(new_file_refs) + "/* End PBXFileReference section */",
        )

    pbx_path.write_text(text)
    print("embed_dynamic_pods: embedded %d frameworks" % len(dyn))
    return 0


if __name__ == "__main__":
    sys.exit(main())
