"""A bounded guest DOS copy command for owned fixture files (no CLI parser)."""
from prepare_frame_image_probe import Assembler


def wait_command():
    a = Assembler()
    a.words(0x2c78, 4)
    a.label('wait'); a.immediate(0, 0x80000000); a.words(0x4eae, -318)
    a.branch(0x6000, 'wait')
    return a.finish()


def command(pairs, capacity=16384, directories=()):
    """Copy (source, target, expected length) through real guest DOS calls.

    The separate before-file witness must validate bytes. A command return is
    insufficient evidence that fixture setup succeeded.
    """
    assert pairs and all(0 < size <= capacity for _, _, size in pairs)
    a = Assembler()
    def call(lvo): a.words(0x4eae, lvo)
    a.words(0x48e7, 0x3f3e, 0x2c78, 4, 0x7c14)
    a.address(1, 'dos'); a.words(0x7000); call(-552)
    a.words(0x2840, 0x4a80); a.branch(0x6700, 'done')
    a.immediate(0, capacity); a.immediate(1, 0x10001); call(-198)
    a.words(0x2a40, 0x4a80); a.branch(0x6700, 'close_lib')
    a.words(0x2c4c)
    for i, _ in enumerate(directories):
        a.address(0, 'dir'+str(i)); a.words(0x2208); call(-120)
        a.words(0x4a80); a.branch(0x6700, 'free')
        a.words(0x2200); call(-90)
    for i, (_, _, size) in enumerate(pairs):
        a.address(0, 'src'+str(i)); a.words(0x2208); a.immediate(2, 1005); call(-30)
        a.words(0x2e00, 0x4a80); a.branch(0x6700, 'free')
        a.words(0x2207, 0x240d); a.immediate(3, capacity); call(-42)
        a.words(0x2800, 0x2207); call(-36)
        a.immediate(0, size); a.words(0xb084); a.branch(0x6600, 'free')
        a.address(0, 'dst'+str(i)); a.words(0x2208); a.immediate(2, 1006); call(-30)
        a.words(0x2e00, 0x4a80); a.branch(0x6700, 'free')
        a.words(0x2207, 0x240d); a.immediate(3, size); call(-48)
        a.words(0x2800, 0x2207); call(-36)
        a.immediate(0, size); a.words(0xb084); a.branch(0x6600, 'free')
    a.words(0x7c00)
    a.label('free'); a.words(0x2c78, 4, 0x224d); a.immediate(0, capacity); call(-210)
    a.label('close_lib'); a.words(0x2c78, 4, 0x224c); call(-414)
    a.label('done'); a.words(0x2006, 0x4cdf, 0x7cfc, 0x4e75)
    for label,text in [('dos','dos.library')] + [('dir'+str(i),text) for i,text in enumerate(directories)] + [item for i,(src,dst,_) in enumerate(pairs) for item in [('src'+str(i),src),('dst'+str(i),dst)]]:
        a.label(label); a.code += text.encode()+b'\0'
        if len(a.code)&1: a.code.append(0)
    return a.finish()
