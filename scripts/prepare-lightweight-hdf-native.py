"""Build disposable native OFS fixtures from a user-supplied Workbench 1.3 ADF/ZIP.

No ROM or media is included. Output must be a new directory. The RDB fixture
first needs a guest Format/Copy boot from format-rdb.adf; subsequent boots must
omit DF0 and write the proof file from the hard disk's own Startup-Sequence.
"""
import argparse
import pathlib
import struct
import zipfile

p = argparse.ArgumentParser()
p.add_argument("workbench", type=pathlib.Path)
p.add_argument("output", type=pathlib.Path)
a = p.parse_args()
a.output.mkdir(parents=True, exist_ok=False)
if a.workbench.suffix.lower() == ".zip":
    with zipfile.ZipFile(a.workbench) as z:
        entries = [n for n in z.namelist() if n.lower().endswith(".adf")]
        assert len(entries) == 1, "Supply a ZIP with one Workbench ADF"
        original = z.read(entries[0])
else:
    original = a.workbench.read_bytes()
assert len(original) == 901120 and original[:4] == b"DOS\0", "880 KiB OFS Workbench required"

def with_startup(payload):
    data = bytearray(original)
    def get(off): return struct.unpack_from(">I", data, off)[0]
    def put(off, val): struct.pack_into(">I", data, off, val & 0xffffffff)
    def check(block):
        put(block * 512 + 20, 0)
        put(block * 512 + 20, -sum(struct.unpack_from(">128I", data, block * 512)))
    headers = [i for i in range(2, 1760) if get(i * 512) == 2 and
               data[i * 512 + 433:i * 512 + 433 + data[i * 512 + 432]].lower() == b"startup-sequence"]
    assert len(headers) == 1 and len(payload) <= 488
    header = headers[0]
    block = get(header * 512 + 308)
    assert 0 < block < 1760 and get(block * 512) == 8
    data[block * 512 + 24:(block + 1) * 512] = payload + bytes(488 - len(payload))
    put(block * 512 + 12, len(payload)); put(block * 512 + 16, 0); check(block)
    for off in range(24, 308, 4): put(header * 512 + off, 0)
    put(header * 512 + 8, 1); put(header * 512 + 16, block)
    put(header * 512 + 324, len(payload)); put(header * 512 + 504, 0); check(header)
    return data

proof = b'Echo >SYS:hdf-proof.txt "CopperHDF native OFS persistence"\nType SYS:hdf-proof.txt\nEcho "HDF COLD BOOT WRITE FINISHED"\n'
(a.output / "partition.hdf").write_bytes(with_startup(b"FailAt 21\n" + proof))
format_script = (b'FailAt 21\nIF EXISTS SYS:rdb-ready\n' + proof + b'ELSE\nEcho >RAM:yes ""\n'
                 b'SYS:System/Format <RAM:yes DRIVE DH0: NAME HDFRDB QUICK\n'
                 b'Copy DF0: DH0: ALL\nEcho >DH0:rdb-ready "Ready"\nEcho "RDB FORMAT COPY FINISHED"\nENDIF\n')
(a.output / "format-rdb.adf").write_bytes(with_startup(format_script))
rdb = bytearray(32 * 512)
def put(off, val): struct.pack_into(">I", rdb, off, val & 0xffffffff)
def check(base):
    put(base + 8, 0); put(base + 8, -sum(struct.unpack_from(">128I", rdb, base)))
for off, val in {0:0x5244534b, 4:128, 16:512, 24:0xffffffff, 28:1, 32:0xffffffff,
                 36:0xffffffff, 64:56, 68:32, 72:1, 128:0, 132:31, 136:1, 140:55, 144:32}.items(): put(off, val)
check(0)
for off, val in {512:0x50415254, 516:128, 528:0xffffffff, 532:1}.items(): put(off, val)
rdb[548:552] = b'\x03DH0'
env = [16, 128, 0, 1, 1, 32, 2, 0, 0, 1, 55, 30, 1, 0x200000, 0x7ffffffe, 0, 0x444f5300]
for i, val in enumerate(env): put(640 + i * 4, val)
check(512)
(a.output / "rdb.hdf").write_bytes(rdb + bytes(901120))
print(a.output.resolve())
