"""Load the development MUI code hunk before the guest GUI providers.

This changes guest allocation order only. It retains a named Exec observation,
closes the temporary library reference, and leaves the normal library resident.
No class/object service is called before CopperGui installs its providers.
"""
from prepare_frame_image_probe import Assembler


def command():
    a = Assembler()
    def call(lvo): a.words(0x4eae, lvo)
    a.words(0x48e7, 0x3f3e, 0x2c78, 4)
    a.immediate(0, 128); a.immediate(1, 0x10001); call(-198)
    a.words(0x4a80); a.branch(0x6700, 'done'); a.words(0x2a40, 0x1b7c, 8, 8)
    a.address(0, 'name'); a.words(0x43ed, 64, 0x2b49, 10, 0x7014); call(-624)
    a.words(0x224d); call(-486)
    a.immediate(1, 0x20004); call(-216); a.words(0x2b40, 20)
    a.address(1, 'library'); a.words(0x7004); call(-552); a.words(0x2b40, 16, 0x4a80)
    a.branch(0x6700, 'mark')
    a.words(0x2240); call(-414)
    a.label('mark'); a.immediate(1, 0x20004); call(-216); a.words(0x2b40, 24)
    a.immediate(0, 0x4d504c31); a.words(0x2b40, 28)
    a.label('done'); a.words(0x7000, 0x4cdf, 0x7cfc, 0x4e75)
    for label, value in [('name', b'MuiPreloadProbe\0'), ('library', b'muimaster.library\0')]:
        a.label(label); a.code += value
        if len(a.code) & 1: a.code.append(0)
    return a.finish()
