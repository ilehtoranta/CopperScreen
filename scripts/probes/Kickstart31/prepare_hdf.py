"""Create a NEW disposable Workbench 3.1 FFS/RDB hardfile from a supplied ADF/ZIP.

The guest startup script writes a proof file on first boot and a reopen marker
on subsequent boots. Run without floppy media. No ROM or OS bytes are included.
"""
import argparse
import hashlib
import json
import struct
import zipfile
from pathlib import Path

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("workbench", type=Path)
parser.add_argument("output", type=Path)
args = parser.parse_args()
if args.workbench.suffix.lower() == ".zip":
    with zipfile.ZipFile(args.workbench) as archive:
        entries = [name for name in archive.namelist() if name.lower().endswith(".adf")]
        assert len(entries) == 1, "Supply a ZIP with one Workbench ADF"
        original = archive.read(entries[0])
else:
    original = args.workbench.read_bytes()
assert len(original) == 901120 and original[:4] == b"DOS\1", "880 KiB FFS Workbench required"
data = bytearray(original)

def get(offset):
    return struct.unpack_from(">I", data, offset)[0]

def put(offset, value):
    struct.pack_into(">I", data, offset, value & 0xffffffff)

headers = [i for i in range(2, 1760) if get(i * 512) == 2 and
           get(i * 512 + 508) == 0xfffffffd and
           data[i * 512 + 433:i * 512 + 433 + data[i * 512 + 432]].lower() == b"startup-sequence"]
assert len(headers) == 1, "Expected one Startup-Sequence"
header = headers[0]
block = get(header * 512 + 308)
assert 0 < block < 1760
payload = (b'FailAt 21\nMakeDir RAM:ENV RAM:T\nAssign ENV: RAM:ENV\nAssign T: RAM:T\n'
           b'Version\nIF EXISTS SYS:ks31-hd-proof.txt\n'
           b'Type SYS:ks31-hd-proof.txt\n'
           b'Echo >SYS:ks31-hd-reopen.txt "CopperHDF Kickstart 3.1 FFS reopen verified"\n'
           b'ELSE\nEcho >SYS:ks31-hd-proof.txt "CopperHDF Kickstart 3.1 native FFS persistence"\n'
           b'Type SYS:ks31-hd-proof.txt\nENDIF\nEcho "HD BOOT TEST FINISHED"\nLoadWB\n')
assert len(payload) <= 512
data[block * 512:(block + 1) * 512] = payload + bytes(512 - len(payload))
for offset in range(24, 312, 4):
    put(header * 512 + offset, 0)
put(header * 512 + 308, block)
put(header * 512 + 8, 1)
put(header * 512 + 16, block)
put(header * 512 + 324, len(payload))
put(header * 512 + 504, 0)
put(header * 512 + 20, 0)
put(header * 512 + 20, -sum(struct.unpack_from(">128I", data, header * 512)))

# Same bounded geometry as the existing native OFS fixture: 32 reserved sectors,
# one bootable DH0 partition, cylinders 1..55, 32 blocks per cylinder.
rdb = bytearray(32 * 512)
def rput(offset, value):
    struct.pack_into(">I", rdb, offset, value & 0xffffffff)

def checksum(base):
    rput(base + 8, 0)
    rput(base + 8, -sum(struct.unpack_from(">128I", rdb, base)))

for offset, value in {0:0x5244534b, 4:128, 16:512, 24:0xffffffff, 28:1,
                      32:0xffffffff, 36:0xffffffff, 64:56, 68:32, 72:1,
                      128:0, 132:31, 136:1, 140:55, 144:32}.items():
    rput(offset, value)
checksum(0)
for offset, value in {512:0x50415254, 516:128, 528:0xffffffff, 532:1}.items():
    rput(offset, value)
rdb[548:552] = b"\x03DH0"
environment = [16, 128, 0, 1, 1, 32, 2, 0, 0, 1, 55, 30, 1, 0x200000, 0x7ffffffe, 0, 0x444f5301]
for i, value in enumerate(environment):
    rput(640 + i * 4, value)
checksum(512)
image = rdb + data
with args.output.open("xb") as output:
    output.write(image)
print(json.dumps({"sourceSha256": hashlib.sha256(original).hexdigest(),
                  "imageSha256": hashlib.sha256(image).hexdigest(), "bytes": len(image),
                  "partitionOffsetSectors": 32, "dosType": "DOS1", "rootBlock": 880,
                  "startup": payload.decode(), "startupHeader": header,
                  "startupData": block}, indent=2))
