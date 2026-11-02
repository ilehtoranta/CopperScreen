"""Owned small Split/rejoin inputs and actual guest DOS file witnesses."""
import hashlib
from prepare_frame_image_probe import Assembler
from native_fixture_copy import command as copy_command

INPUT = bytes(((i*73 + (i >> 3)*19) ^ 0xA5) & 255 for i in range(12345))
PARTS = [INPUT[i:i+4096] for i in range(0, len(INPUT), 4096)]
DRAWER = 'RAM:muijoin'
BASE = DRAWER+'/muifix'
PATHS = [BASE] + [BASE+'.ja'+c for c in 'abcd'] + [BASE+'.out']
EXPECTED = [INPUT] + PARTS + [INPUT]
STRIDE = 16384


def snapshot_command(before):
    a = Assembler()
    def call(lvo): a.words(0x4eae, lvo)
    a.words(0x48e7, 0x3f3e, 0x2c78, 4)
    a.address(1, 'dos'); a.words(0x7000); call(-552)
    a.words(0x2840, 0x4a80); a.branch(0x6700, 'done')
    a.immediate(0, 128+STRIDE*len(PATHS)); a.immediate(1, 0x10001); call(-198)
    a.words(0x2a40, 0x4a80); a.branch(0x6700, 'close')
    a.words(0x1b7c, 8, 8, 0x43ed, 96, 0x2b49, 10)
    a.address(0, 'name'); a.immediate(0, 16); call(-624)
    a.words(0x224d); call(-486)
    a.words(0x2b7c, 0x4a57, 0x4232 if before else 0x4132, 16, 0x2c4c)
    # A detached client may still be running. Retry actual DOS reads until
    # every output has its full expected length; the host independently checks
    # every byte. Do not treat returning to Startup-Sequence as client exit.
    a.immediate(5, 1 if before else 6000)
    a.label('retry'); a.words(0x7c01, 0x52ad, 72)
    for i in range(1 if before else len(PATHS)):
        a.address(0, 'path'+str(i)); a.words(0x2208); a.immediate(2, 1005); call(-30)
        a.words(0x2e00, 0x2b40, 48+i*4, 0x4a80); a.branch(0x6700, 'bad'+str(i))
        a.words(0x2207, 0x41ed, 128+STRIDE*i, 0x2408) if i < 2 else a.words(0x2207)
        if i >= 2:
            a.words(0x240d); a.immediate(0, 128+STRIDE*i); a.words(0xd480)
        a.immediate(3, STRIDE); call(-42)
        a.words(0x2b40, 24+i*4)
        a.immediate(0, len(EXPECTED[i])); a.words(0xb0ad, 24+i*4)
        a.branch(0x6700, 'close_file'+str(i)); a.words(0x7c00)
        a.label('close_file'+str(i)); a.words(0x2207); call(-36)
        a.branch(0x6000, 'next'+str(i))
        a.label('bad'+str(i)); a.words(0x7c00)
        a.label('next'+str(i))
    a.words(0x4a86); a.branch(0x6600, 'complete')
    a.words(0x7232); call(-198)
    a.words(0x5385); a.branch(0x6600, 'retry'); a.branch(0x6000, 'close')
    a.label('complete'); a.words(0x2b7c, 0, 1, 20)
    a.label('close'); a.words(0x2c78, 4, 0x224c); call(-414)
    a.label('done'); a.words(0x7000, 0x4cdf, 0x7cfc, 0x4e75)
    name = 'MUI_JoinBefore2' if before else 'MUI_JoinAfter2'
    for label, text in [('name', name), ('dos', 'dos.library')] + [('path'+str(i), path) for i,path in enumerate(PATHS)]:
        a.label(label); a.code += text.encode()+b'\0'
        if label == 'name': a.code += bytes(16-len(text)-1)
        if len(a.code) & 1: a.code.append(0)
    return a.finish()


def files():
    # Original executable's fscanf at file offset 0xFAE reads postfix, append,
    # chunk bytes, buffer bytes, Max. See its unchanged guide's PWindow node.
    return {'fixtures/JoinInput': INPUT,
            'S/MUIJoin.prefs': b'j\nout\n4096\n4096\n0\n',
            'C/JoinBefore': snapshot_command(True),
            'C/JoinInputs': copy_command([('SYS:fixtures/JoinInput', BASE, len(INPUT))], directories=[DRAWER]),
            'C/JoinAfter': snapshot_command(False)}


COPY_STARTUP = b'C:JoinInputs\nC:JoinBefore\n'
INPUT_SHA256 = hashlib.sha256(INPUT).hexdigest()
