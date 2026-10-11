"""Prepare the bounded Kick Off 3 AGA Practice-menu investigation input."""
import argparse
import json
from pathlib import Path


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--disk2", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    disk2 = args.disk2.resolve(strict=True)
    entries = [
        {"frame": 1500, "mouseButtons": 1},
        {"frame": 1520, "mouseButtons": 0},
        {"frame": 6000, "ejectAdf": True},
        {"frame": 6200, "diskPath": disk2.as_posix()},
        {"frame": 9300, "joystickPort1": 8},
        {"frame": 9344},
        {"frame": 9400, "joystickPort1": 2},
        {"frame": 9432},
        {"frame": 9500, "joystickPort1": 16},
        {"frame": 9503},
    ]
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(entries, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
