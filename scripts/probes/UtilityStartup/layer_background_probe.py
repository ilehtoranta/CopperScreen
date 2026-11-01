"""Native Intuition close/default LayerInfo background probe; no MUI dependency."""
from prepare_frame_image_probe import Assembler


def command(suppress=False, native_state=False):
    a = Assembler()
    def call(lvo): a.words(0x4eae, lvo)
    def put(offset, value): a.words(0x2b7c, value >> 16, value, offset)
    def store(offset): a.words(0x2b40, offset)
    def pixel(offset):
        a.words(0x2c47, 0x226d, 20, 0xd3fc, 0, 84)  # graphics A6, screen.RastPort A1
        a.immediate(0, 20); a.immediate(1, 30); call(-318); store(offset)
    a.words(0x2c78, 4)
    a.immediate(0, 512); a.immediate(1, 0x10001); call(-198)
    a.words(0x4a80); a.branch(0x6700, 'done')
    a.words(0x2a40, 0x1b7c, 8, 8, 0x43ed, 400, 0x2b49, 10)
    a.address(0, 'resource'); a.words(0x7015); call(-624)
    a.words(0x224d); call(-486)
    put(48, int(suppress))
    a.address(1, 'intuition'); a.words(0x7000); call(-552); a.words(0x2840)
    a.address(1, 'graphics'); a.words(0x7000); call(-552); a.words(0x2e00)
    a.address(1, 'layers'); a.words(0x7000); call(-552); a.words(0x2c00); store(16)
    put(44, 1)
    a.words(0x2c4c); a.address(0, 'window'); call(-204)
    store(24); a.words(0x4a80); a.branch(0x6700, 'done')
    a.words(0x2440, 0x202a, 46); store(20)  # A2 window, screen copied
    put(44, 2)
    if suppress:
        a.words(0x2040, 0xd1fc, 0, 224, 0x227c, 0, 1, 0x2c46); call(-204)
    a.words(0x2c47, 0x226a, 50, 0x7001); call(-342)  # SetAPen
    a.words(0x7000, 0x7200); a.immediate(2, 319); a.immediate(3, 179); call(-306)
    pixel(28); put(44, 3)
    a.words(0x2c4c, 0x204a); call(-72)  # CloseWindow
    put(44, 4)
    a.words(0x206d, 20, 0x2028, 4); store(36)
    pixel(32)
    if native_state:  # Explicit opt-in for the development provider's private ABI.
        a.words(0x2046, 0x2068, 40)
        for offset in range(0, 256, 4): a.words(0x2028, offset, 0x2b40, 64 + offset)
    put(40, 0x42474631)
    a.label('done'); a.words(0x2c78, 4, 0x7000); call(-318)  # Wait(0)
    a.branch(0x6000, 'done')
    a.label('window')
    a.words(12, 16, 320, 180, 1, 0, 0, 0, 0x104e)
    for _ in range(5): a.words(0, 0)
    a.words(0, 0, -1, -1, 1)  # Workbench screen, no backdrop required
    for label, value in [('resource', b'LayerBackgroundProbe\0'), ('intuition', b'intuition.library\0'),
                         ('graphics', b'graphics.library\0'), ('layers', b'layers.library\0')]:
        a.label(label); a.code += value
        if len(a.code) & 1: a.code.append(0)
    return a.finish()
