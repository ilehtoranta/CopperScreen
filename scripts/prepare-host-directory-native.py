"""Extract a supplied OFS/FFS Workbench ADF into a NEW folder for native tests.

No licensed bytes are included. The parser checks checksums, bounds and chains.
This fixture utility accepts classic DOS0/DOS1 only; it is not a general importer.
"""
import argparse
import datetime
import json
import os
from pathlib import Path
import struct
import zipfile

p = argparse.ArgumentParser(description=__doc__)
p.add_argument("workbench", type=Path)
p.add_argument("output", type=Path)
p.add_argument("--proof-script", action="store_true")
p.add_argument("--exchange-boot-adf", type=Path, help="Create a NEW disposable ADF whose startup executes HOST0:exchange-script")
a = p.parse_args()
if a.workbench.suffix.lower() == ".zip":
    with zipfile.ZipFile(a.workbench) as z:
        members = [n for n in z.namelist() if n.lower().endswith(".adf")]
        assert len(members) == 1
        image = z.read(members[0])
else:
    image = a.workbench.read_bytes()
assert len(image) == 901120 and image[:4] in (b"DOS\0", b"DOS\1")
ffs = image[3] == 1
u32 = lambda off: struct.unpack_from(">I", image, off)[0]

def block(number, checksum=True):
    assert 1 < number < 1760, f"Invalid block {number}"
    payload = image[number * 512:(number + 1) * 512]
    if checksum:
        assert sum(struct.unpack(">128I", payload)) & 0xffffffff == 0, f"Checksum {number}"
    return payload

visited = set()
def extract(directory, number):
    assert number not in visited
    visited.add(number)
    header = block(number)
    metadata = {"Version": 1, "Entries": []}
    for head in struct.unpack(">72I", header[24:312]):
        chain = set()
        while head:
            assert head not in chain
            chain.add(head)
            b = block(head)
            name = b[433:433 + b[432]].decode("latin1")
            assert name and name not in (".", "..") and not any(c in name for c in "/\\:")
            target = directory / name
            assert not target.exists(), f"Duplicate {name}"
            protection = struct.unpack_from(">I", b, 320)[0]
            comment = b[329:329 + b[328]].decode("latin1")
            metadata["Entries"].append({"Name": name, "HostName": name, "Protection": protection, "Comment": comment})
            kind = struct.unpack_from(">i", b, 508)[0]
            if kind == 2:
                target.mkdir()
                extract(target, head)
            else:
                assert kind == -3
                size = struct.unpack_from(">I", b, 324)[0]
                contents = bytearray()
                seen = set()
                if ffs:
                    extension = head
                    while extension:
                        assert extension not in seen
                        seen.add(extension)
                        table = block(extension)
                        count = struct.unpack_from(">I", table, 8)[0]
                        assert count <= 72
                        for i in range(count):
                            data = struct.unpack_from(">I", table, 308 - i * 4)[0]
                            contents.extend(block(data, False))
                        extension = struct.unpack_from(">I", table, 504)[0]
                else:
                    data = struct.unpack_from(">I", b, 16)[0]
                    while data:
                        assert data not in seen
                        seen.add(data)
                        d = block(data)
                        length = struct.unpack_from(">I", d, 12)[0]
                        assert length <= 488
                        contents.extend(d[24:24 + length])
                        data = struct.unpack_from(">I", d, 16)[0]
                assert len(contents) >= size
                target.write_bytes(contents[:size])
            days, minutes, ticks = struct.unpack_from(">3I", b, 420)
            date = datetime.datetime(1978, 1, 1) + datetime.timedelta(days=days, minutes=minutes, seconds=ticks / 50)
            os.utime(target, (date.timestamp(), date.timestamp()))
            head = struct.unpack_from(">I", b, 496)[0]
    (directory / ".copperfs.json").write_text(json.dumps(metadata, indent=2), encoding="utf-8")

a.output.mkdir(parents=True, exist_ok=False)
extract(a.output, 880)
if a.exchange_boot_adf:
    data = bytearray(image)
    headers = [i for i in range(2, 1760) if u32(i * 512) == 2 and u32(i * 512 + 508) == 0xfffffffd and
               image[i * 512 + 433:i * 512 + 433 + image[i * 512 + 432]].lower() == b"startup-sequence"]
    assert len(headers) == 1
    header = headers[0]
    first = u32(header * 512 + 308)
    payload = b'FailAt 21\nMakeDir RAM:T\nAssign T: RAM:T\nExecute HOST0:exchange-script\n'
    def put(off, value):
        struct.pack_into(">I", data, off, value & 0xffffffff)
    def checksum(number):
        put(number * 512 + 20, 0)
        put(number * 512 + 20, -sum(struct.unpack_from(">128I", data, number * 512)))
    if ffs:
        data[first * 512:(first + 1) * 512] = payload + bytes(512 - len(payload))
    else:
        data[first * 512 + 24:(first + 1) * 512] = payload + bytes(488 - len(payload))
        put(first * 512 + 8, 1); put(first * 512 + 12, len(payload)); put(first * 512 + 16, 0)
        checksum(first)
    for off in range(24, 312, 4):
        put(header * 512 + off, 0)
    put(header * 512 + 308, first); put(header * 512 + 8, 1); put(header * 512 + 16, first)
    put(header * 512 + 324, len(payload)); put(header * 512 + 504, 0)
    checksum(header)
    with a.exchange_boot_adf.open("xb") as target:
        target.write(data)
if a.proof_script:
    (a.output / "S" / "Startup-Sequence").write_text(
        'FailAt 21\nMakeDir RAM:ENV\nMakeDir RAM:T\nAssign ENV: RAM:ENV\nAssign T: RAM:T\n'
        'IF EXISTS SYS:host-proof.txt\nType SYS:host-proof.txt\n'
        'Echo >SYS:host-reopen.txt "CopperFS reopen verified"\n'
        'ELSE\nEcho >SYS:host-proof.txt "CopperFS native folder boot"\nENDIF\n'
        'LoadWB\nEndCLI >NIL:\n', encoding="latin1", newline="\n")
print(a.output.resolve())
