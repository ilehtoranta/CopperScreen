"""Verify completed native Detroit replays and compare their captured outputs."""
import argparse
import hashlib
import json
import re
import struct
from pathlib import Path


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def captures(directory):
    return {p.name: sha256(p) for p in sorted(directory.iterdir()) if p.is_file()}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('normal', 'scalar', 'normal-log', 'scalar-log', 'output'):
        parser.add_argument('--' + name, type=Path, required=True)
    parser.add_argument('--exploration', type=Path)
    parser.add_argument('--frames', type=int, default=34000)
    args = parser.parse_args()
    expected_frames = [1, *range(200, args.frames + 1, 200)]
    if args.frames % 200:
        expected_frames.append(args.frames)
    fingerprints = []
    states = []
    for directory, log in [(args.normal, args.normal_log), (args.scalar, args.scalar_log)]:
        snapshots = [json.loads(p.read_text()) for p in sorted(directory.glob('frame-*.json'))]
        if [s['frame'] for s in snapshots] != expected_frames:
            raise ValueError(f'Incomplete native captures in {directory}')
        if any(s['unsupported'] or s['videoUnsupported'] for s in snapshots):
            raise ValueError(f'Unsupported execution in {directory}')
        profile = json.loads((directory / 'cpu-profile.json').read_text())
        if not profile['copper68k'].startswith('1.5.4-detroit-dev.2+'):
            raise ValueError('Unexpected CPU package; preserve version-specific evidence.')
        text = log.read_text()
        summaries = [line for line in text.splitlines() if line.startswith('engine=')]
        if len(summaries) != 1 or 'Unhandled exception' in text:
            raise ValueError(f'Missing successful runner summary in {log}')
        summary = dict(re.findall(r'(\w+)=([^\s]+)', summaries[0]))
        if summary.get('completed') != str(args.frames) or summary.get('unsupported') != 'none':
            raise ValueError(f'Early or unsupported native exit in {log}')
        fingerprints.append({k: summary[k] for k in ('cycle', 'cpu', 'hardware', 'output')})
        states.append(snapshots[-1])
    normal, scalar = captures(args.normal), captures(args.scalar)
    mismatches = [n for n in sorted(normal.keys() | scalar.keys()) if normal.get(n) != scalar.get(n)]
    expected_names = {'cpu-profile.json'}
    for frame in expected_frames:
        expected_names.add(f'aga-display-{frame:06d}.json')
        expected_names.update(f'frame-{frame:06d}.{ext}' for ext in ('json', 'bmp', 'chipram', 'slowram', 'pcm'))
    if set(normal) != expected_names or set(scalar) != expected_names:
        raise ValueError('Unexpected or missing native capture files')
    if mismatches or fingerprints[0] != fingerprints[1]:
        raise ValueError(f'Normal/scalar divergence: {mismatches}')
    # Byte identities tie visually inspected exploratory milestones to production.
    milestone_frames = [20400, 22800, 24400, 27000, 28800, 32000, 34000]
    milestones = {}
    for frame in milestone_frames:
        if frame > args.frames:
            continue
        names = [f'frame-{frame:06d}.{ext}' for ext in ('bmp', 'json', 'chipram', 'pcm')]
        if args.exploration:
            if any(sha256(args.exploration / name) != normal[name] for name in names):
                raise ValueError(f'Exploratory milestone differs from production at {frame}')
        bmp = (args.normal / names[0]).read_bytes()
        offset = struct.unpack_from('<I', bmp, 10)[0]
        if struct.unpack_from('<H', bmp, 28)[0] != 32:
            raise ValueError('Unexpected capture bitmap format')
        if not any(bmp[i] or bmp[i + 1] or bmp[i + 2] for i in range(offset, len(bmp), 4)):
            raise ValueError(f'Black milestone at {frame}; visual playability is unproven')
        milestones[frame] = {name: normal[name] for name in names}
    result = {
        'schemaVersion': 1, 'completedFields': args.frames,
        'capturesPerMode': len(normal), 'captureMismatches': mismatches,
        'fingerprints': fingerprints[0], 'finalState': states[0],
        'milestoneHashes': milestones, 'allCaptureHashes': normal,
        'limits': 'Capture parity and nonblack milestones are not a gameplay classifier or hardware oracle; inspect milestone contents separately.'
    }
    args.output.write_text(json.dumps(result, indent=2) + '\n')
    print(json.dumps({k: result[k] for k in ('completedFields', 'capturesPerMode', 'captureMismatches', 'fingerprints')}, indent=2))


if __name__ == '__main__':
    main()
