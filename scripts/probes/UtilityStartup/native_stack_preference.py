"""Guest Stack preference control using the public Process/CLI structures.

This changes the CLI default for subsequent commands, in longwords, like Stack.
It does not replace or write the current Task stack or patch the running program.
The runner must independently confirm the following command's actual bounds.
"""
from prepare_frame_image_probe import Assembler


def command(stack_bytes=32768):
    assert 4096 <= stack_bytes <= 1024*1024 and stack_bytes % 4 == 0
    a = Assembler()
    a.words(0x2f0e, 0x2c78, 4, 0x93c9, 0x4eae, -294)  # FindTask(NULL)
    a.words(0x4a80); a.branch(0x6700, 'fail')
    a.words(0x2040, 0x2028, 172, 0x4a80)  # Process.pr_CLI is a BPTR.
    a.branch(0x6700, 'fail')
    a.words(0xe588, 0x2040)  # BPTR to address.
    a.words(0x217c, (stack_bytes//4) >> 16, (stack_bytes//4) & 0xffff, 52)
    a.words(0x7000); a.branch(0x6000, 'done')
    a.label('fail'); a.words(0x7014)
    a.label('done'); a.words(0x2c5f, 0x4e75)
    return a.finish()
