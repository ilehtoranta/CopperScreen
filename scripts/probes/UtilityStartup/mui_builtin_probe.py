"""Native List/Listview/Cycle construction, methods, disposal and retirement probe."""
from prepare_frame_image_probe import Assembler


def command():
    a = Assembler()
    def call(lvo):
        a.words(0x4eae, lvo)
    def store(offset):
        a.words(0x2b40, offset)
    def method():
        # Native BOOPSI object header / Hook ABI, as declared by the SDK.
        a.words(0x206a, 0xfffc, 0x2668, 8, 0x43ed, 128, 0x4e93)
    def get_entries(status, value):
        a.words(0x2c47, 0x204a, 0x43ed, value)
        a.immediate(0, 0x80421654)  # MUIA_List_Entries
        call(-654)  # Intuition.GetAttr
        store(status)
    a.words(0x48e7, 0x3f3e, 0x2c78, 4)
    a.immediate(0, 256); a.immediate(1, 0x10001); call(-198)
    a.words(0x4a80); a.branch(0x6700, 'done')
    a.words(0x2a40, 0x1b7c, 8, 8)
    a.address(0, 'name'); a.words(0x43ed, 224, 0x2b49, 10, 0x7010); call(-624)
    a.words(0x224d); call(-486)
    a.address(1, 'library'); a.words(0x7004); call(-552)
    store(20); a.words(0x4a80); a.branch(0x6700, 'done'); a.words(0x2840)
    a.address(1, 'intuition'); a.words(0x7025); call(-552)
    a.words(0x2e00, 0x4a80); a.branch(0x6700, 'close_mui')
    for cycle in range(2):
        base = 32 + cycle * 32
        a.words(0x2c4c); a.address(0, 'class'); call(-78)
        store(base); a.words(0x4a80); a.branch(0x6700, 'close_intuition')
        a.address(0, 'class'); a.address(1, 'tags'); call(-30)
        store(base + 4); a.words(0x2440, 0x4a80); a.branch(0x6700, f'free_class{cycle}')
        a.immediate(0, 0x804254d5); store(128)  # MUIM_List_InsertSingle
        a.address(0, 'entry'); a.words(0x2b48, 132)
        a.words(0x42ad, 136); method(); store(base + 8)
        get_entries(base + 12, base + 16)
        a.immediate(0, 0x8042ad89); store(128)  # MUIM_List_Clear
        method(); store(base + 20)
        get_entries(base + 24, base + 28)
        # Adopt the live List into a real Listview, then dispose the composite.
        # This exercises derived construction and child/public-lease cleanup.
        a.immediate(0, 0x8042bcce); store(144)  # MUIA_Listview_List
        a.words(0x2b4a, 148, 0x42ad, 152, 0x42ad, 156, 0x2c4c)
        a.address(0, 'view'); a.words(0x43ed, 144); call(-30)
        store(96 + cycle * 4); a.words(0x4a80); a.branch(0x6700, f'dispose_leaf{cycle}')
        a.words(0x2040); call(-36)
        a.branch(0x6000, f'released{cycle}')
        a.label(f'dispose_leaf{cycle}'); a.words(0x2c4c, 0x204a); call(-36)
        a.label(f'released{cycle}')
        # Keep the Cycle's caller-owned entry vector live through disposal.
        a.address(0, 'one'); a.words(0x2b48, 176)
        a.address(0, 'two'); a.words(0x2b48, 180, 0x42ad, 184)
        a.immediate(0, 0x80420629); store(144)  # MUIA_Cycle_Entries
        a.words(0x41ed, 176, 0x2b48, 148, 0x42ad, 152, 0x42ad, 156, 0x2c4c)
        a.address(0, 'cycle'); a.words(0x43ed, 144); call(-30)
        store(104 + cycle * 4); a.words(0x2440, 0x4a80)
        a.branch(0x6700, f'free_class{cycle}')
        a.words(0x2c47, 0x204a, 0x43ed, 112 + cycle * 4)
        a.immediate(0, 0x80421788); call(-654)  # MUIA_Cycle_Active
        store(120 + cycle * 4)
        a.words(0x2c4c, 0x204a); call(-36)
        a.label(f'free_class{cycle}')
        a.words(0x2c4c, 0x206d, base); call(-84)
        a.words(0x2c78, 4, 0x7201); call(-216); store(24 + cycle * 4)
    a.immediate(0, 0x4d554233); store(16)
    a.label('close_intuition'); a.words(0x2247, 0x2c78, 4); call(-414)
    a.label('close_mui'); a.words(0x224c, 0x2c78, 4); call(-414)
    a.label('done'); a.words(0x7000, 0x4cdf, 0x7cfc, 0x4e75)
    for label, value in [('name', b'MuiBuiltinProbe\0'), ('library', b'muimaster.library\0'),
                         ('intuition', b'intuition.library\0'), ('class', b'List.mui\0'),
                         ('view', b'Listview.mui\0'), ('entry', b'probe\0'),
                         ('cycle', b'Cycle.mui\0'), ('one', b'one\0'), ('two', b'two\0')]:
        a.label(label); a.code += value
        if len(a.code) & 1:
            a.code.append(0)
    a.label('tags'); a.words(0, 0, 0, 0)
    return a.finish()
