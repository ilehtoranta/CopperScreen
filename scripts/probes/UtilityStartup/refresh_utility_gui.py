"""Refresh only CopperGui/Intuition on a hash-pinned local utility filesystem image."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil

from amitools.fs.ADFSVolume import ADFSVolume
from amitools.fs.FSString import FSString
from amitools.fs.blkdev.BlkDevFactory import BlkDevFactory


def sha(data):
    return hashlib.sha256(data).hexdigest()


def inventory(volume):
    result = {}

    def walk(node, prefix=""):
        for child in node.get_entries():
            path = prefix + child.name.get_name().get_unicode()
            if child.is_dir():
                walk(child, path + "/")
            else:
                result[path] = sha(bytes(volume.read_file(FSString(path))))

    walk(volume.get_root_dir())
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base", type=Path, required=True)
    parser.add_argument("--expected-sha256", required=True)
    parser.add_argument("--provider-root", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    base_hash = sha(args.base.read_bytes())
    if base_hash != args.expected_sha256.lower():
        raise ValueError("Base image differs from its pinned receipt")
    if args.output.exists():
        raise FileExistsError("Preserve earlier utility images")
    provider = (args.provider_root / "CopperIntuition.hunk").read_bytes()
    loader = (args.provider_root / "CopperGui").read_bytes()
    manifest = json.loads((args.provider_root / "intuition.json").read_text(encoding="utf-8-sig"))
    receipt = json.loads((args.provider_root / "CopperGui.json").read_text(encoding="utf-8-sig"))
    if (sha(provider) != manifest["sha256"].lower()
            or sha(provider) != receipt["providerSha256"].lower()
            or sha(loader) != receipt["sha256"].lower()
            or manifest["classicEntry"] != receipt["classicEntry"]):
        raise ValueError("Provider/loader receipt mismatch")
    args.output.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(args.base, args.output)
    device = BlkDevFactory().open(str(args.output))
    volume = ADFSVolume(device)
    try:
        volume.open()
        before = inventory(volume)
        names = {path.casefold(): path for path in before}
        updates = {"c/intuition": provider, "c/coppergui": loader}
        if not updates.keys() <= names.keys():
            raise ValueError("Base must already contain the guest GUI installation")
        updates = {names[path]: data for path, data in updates.items()}
        for path, data in updates.items():
            volume.delete(FSString(path))
            volume.write_file(data, FSString(path))
        after = inventory(volume)
        expected = before | {path: sha(data) for path, data in updates.items()}
        if after != expected:
            raise ValueError("Unexpected guest-file changes")
    finally:
        volume.close()
        device.close()
    result = {"kind": "controlled-utility-gui-refresh", "baseSha256": base_hash,
              "sha256": sha(args.output.read_bytes()), "before": before, "after": after,
              "provider": manifest, "loader": receipt}
    Path(str(args.output) + ".json").write_text(json.dumps(result, indent=2) + "\n")
    print(result["sha256"])


if __name__ == "__main__":
    main()
