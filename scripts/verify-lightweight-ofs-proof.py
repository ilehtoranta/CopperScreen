"""Independently verify a native-written OFS proof file, without CopperDisk.

Usage: python verify-lightweight-ofs-proof.py image.hdf --offset-sectors 32
The offset is zero for partition images. RDB partition offsets must come from
the identified native-test fixture, not a guessed scan of arbitrary media.
"""
import argparse
import hashlib
import json
import pathlib
import struct

parser = argparse.ArgumentParser()
parser.add_argument("image", type=pathlib.Path)
parser.add_argument("--offset-sectors", type=int, default=0)
parser.add_argument("--root-block", type=int, help="Defaults to the middle block of this OFS partition")
parser.add_argument("--name", default="hdf-proof.txt")
parser.add_argument("--expected", default="CopperHDF native OFS persistence\n")
args = parser.parse_args()
image = args.image.read_bytes()
disk = memoryview(image)[args.offset_sectors * 512:]
assert args.offset_sectors >= 0 and len(disk) % 512 == 0
assert bytes(disk[:4]) == b"DOS\0", "OFS is required"
count = len(disk) // 512

def block(number):
    assert 0 < number < count, "Block outside partition"
    data = disk[number * 512:(number + 1) * 512]
    assert sum(struct.unpack(">128I", data)) & 0xffffffff == 0, f"Bad checksum in block {number}"
    return data

def u32(data, offset):
    return struct.unpack_from(">I", data, offset)[0]

# Hardfile boot discovery uses RDB metadata; a formatted partition need not
# contain an installed floppy boot block or a root pointer at boot offset 8.
root_number = args.root_block if args.root_block is not None else count // 2
root = block(root_number)
assert u32(root, 0) == 2 and u32(root, 508) == 1
header = None
header_number = None
seen = set()
for offset in range(24, 312, 4):
    number = u32(root, offset)
    while number:
        assert number not in seen, "Cyclic directory hash chain"
        seen.add(number)
        entry = block(number)
        name = bytes(entry[433:433 + entry[432]]).decode("latin-1")
        if name.lower() == args.name.lower():
            header, header_number = entry, number
        number = u32(entry, 496)
assert header is not None, "Proof file absent from root directory"
assert u32(header, 0) == 2 and u32(header, 508) == 0xfffffffd
assert u32(header, 500) == root_number
number, sequence = u32(header, 16), 1
contents = bytearray()
seen = set()
while number:
    assert number not in seen, "Cyclic file data chain"
    seen.add(number)
    data = block(number)
    assert u32(data, 0) == 8 and u32(data, 4) == header_number
    assert u32(data, 8) == sequence and u32(data, 12) <= 488
    contents.extend(data[24:24 + u32(data, 12)])
    number, sequence = u32(data, 16), sequence + 1
assert len(contents) == u32(header, 324), "File size differs from data chain"
assert contents == args.expected.encode("latin-1"), "Proof contents differ"
print(json.dumps({"image": str(args.image), "offset_sectors": args.offset_sectors,
    "image_sha256": hashlib.sha256(image).hexdigest(), "file": args.name,
    "file_sha256": hashlib.sha256(contents).hexdigest(), "bytes": len(contents),
    "root_block": root_number, "header_block": header_number,
    "verified_data_blocks": len(seen)}, indent=2))
