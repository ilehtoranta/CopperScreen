"""Owned classic icons and guest file snapshots for the original Icon Update.

The HDF remains read-only. A small guest DOS command copies the inputs to RAM;
the unchanged utility performs the update and guest DOS reads publish evidence.
"""
import struct
from prepare_frame_image_probe import Assembler
from native_fixture_copy import command as copy_command

def icon(source):
    header = bytearray(78)
    def w(offset, value): struct.pack_into('>H', header, offset, value)
    def l(offset, value): struct.pack_into('>I', header, offset, value)
    w(0, 0xE310); w(2, 1)
    w(12, 16); w(14, 16); w(16, 4); w(20, 1)  # Gadget width/height/image flag/bool type.
    l(22, 1)  # Serialized pointer is presence, never a live address.
    header[48] = 3 if source else 4
    l(58, 17 if source else 31); l(62, 19 if source else 37)
    l(74, 8192 if source else 16384)
    image = struct.pack('>hhhhhIBBI', 0, 0, 16, 16, 1, 1, 1, 0, 0)
    pixels = b''.join(struct.pack('>H', (0xAA55 if y % 2 == 0 else 0x55AA) if source else 0x0FF0) for y in range(16))
    return bytes(header) + image + pixels

def snapshot_command(before=False):
    a = Assembler()
    def call(lvo): a.words(0x4eae, lvo)
    a.words(0x48e7, 0x3f3e, 0x2c78, 4)
    a.address(1, 'dos'); a.words(0x7000); call(-552)
    a.words(0x2840, 0x4a80); a.branch(0x6700, 'done')
    a.immediate(0, 1280); a.immediate(1, 0x10001); call(-198)
    a.words(0x2a40, 0x4a80); a.branch(0x6700, 'close')
    a.words(0x1b7c, 8, 8, 0x43ed, 64, 0x2b49, 10)
    a.address(0, 'name'); a.immediate(0, 16); call(-624)
    a.words(0x224d); call(-486)
    a.words(0x2b7c, 0x4955, 0x4231 if before else 0x4131, 16)
    if not before:
        # The original SAS/C executable detaches into this named process.
        # Returning to Startup-Sequence is not evidence that its GUI has quit.
        # Each Delay(1) waits at least a PAL field. Keep this witness alive
        # beyond the complete 500,000-field functional run: an early return
        # restores the CLI over the detached GUI and invalidates mouse input.
        a.words(0x7c00); a.immediate(5, 1000000)
        a.label('wait_task'); a.words(0x2b45, 48, 0x2c78, 4); a.address(1, 'task'); call(-294)
        a.words(0x4a80); a.branch(0x6700, 'task_absent')
        a.words(0x7c01, 0x2b46, 40); a.branch(0x6000, 'delay_task')
        a.label('task_absent'); a.words(0x4a86); a.branch(0x6700, 'delay_task')
        a.words(0x2b7c, 0, 1, 44); a.branch(0x6000, 'read_files')
        a.label('delay_task'); a.words(0x2c4c, 0x7201); call(-198)
        a.words(0x5385); a.branch(0x6600, 'wait_task')
        a.words(0x2b7c, 0, 1, 52); a.branch(0x6000, 'close')
        a.label('read_files')
    a.words(0x2c4c)
    for i, (name, offset) in enumerate([('src', 128), ('dst', 640)]):
        a.address(0, name); a.words(0x2208); a.immediate(2, 1005); call(-30)
        a.words(0x2e00, 0x2b40, 32+i*4, 0x4a80); a.branch(0x6700, 'next'+str(i))
        a.words(0x2207, 0x41ed, offset, 0x2408); a.immediate(3, 512); call(-42)
        a.words(0x2b40, 24+i*4, 0x2207); call(-36)
        a.label('next'+str(i))
    a.words(0x2b7c, 0, 1, 20)
    a.label('close'); a.words(0x2c78, 4, 0x224c); call(-414)
    a.label('done'); a.words(0x7000, 0x4cdf, 0x7cfc, 0x4e75)
    for label, value in [('name', 'MUI_IconBefore' if before else 'MUI_IconAfter'), ('dos', 'dos.library'), ('src', 'RAM:src.info'), ('dst', 'RAM:dst.info'), ('task', 'MUI_Icon_Update_process')]:
        a.label(label); a.code += value.encode() + b'\0'
        if label == 'name': a.code += bytes(16-len(value)-1)
        if len(a.code) & 1: a.code.append(0)
    return a.finish()

def files():
    return {'fixtures/IconSource': b'CopperScreen owned source object\n',
            'fixtures/IconSource.info': icon(True),
            'fixtures/IconTarget': b'CopperScreen owned target object\n',
            'fixtures/IconTarget.info': icon(False),
            'C/IconBefore': snapshot_command(True), 'C/IconAfter': snapshot_command(False),
            'C/IconInputs': copy_command([
                ('SYS:fixtures/IconSource', 'RAM:src', len(b'CopperScreen owned source object\n')),
                ('SYS:fixtures/IconSource.info', 'RAM:src.info', len(icon(True))),
                ('SYS:fixtures/IconTarget', 'RAM:dst', len(b'CopperScreen owned target object\n')),
                ('SYS:fixtures/IconTarget.info', 'RAM:dst.info', len(icon(False)))])}

COPY_STARTUP = b'C:IconInputs\nC:IconBefore\n'
