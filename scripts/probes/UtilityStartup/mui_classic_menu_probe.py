"""Real native classic Application_Menu conversion and owned-tree disposal probe."""
from prepare_frame_image_probe import Assembler


def command(open_windows=False, text_roots=False, pointer_hook=False, tall_text=False, update_text=False, indexed_background=False, list_rows=False, array_insert=False):
    if array_insert and not list_rows:
        raise ValueError('Classic array insertion requires the List row probe')
    if list_rows and not indexed_background:
        raise ValueError('List row qualification requires the indexed Text update probe')
    if indexed_background and not update_text:
        raise ValueError('Indexed background qualification requires the Text update probe')
    if update_text and (not pointer_hook or tall_text):
        raise ValueError('Text update qualification requires the ordinary pointer probe')
    if tall_text and not pointer_hook:
        raise ValueError('Resize qualification requires the extended pointer result record')
    if pointer_hook and not (open_windows and text_roots):
        raise ValueError('Pointer qualification requires rendered Text roots')
    a = Assembler()
    def call(lvo): a.words(0x4eae, lvo)
    def store(offset): a.words(0x2b40, offset)
    def get(attribute, obj_register, status, output):
        a.words(0x2c47, 0x2048 + obj_register, 0x43ed, output)
        a.immediate(0, attribute); call(-654); store(status)
    a.words(0x48e7, 0x3f3e, 0x2c78, 4)
    a.immediate(0, 768 if pointer_hook else 512); a.immediate(1, 0x10001); call(-198)
    a.words(0x4a80); a.branch(0x6700, 'done'); a.words(0x2a40, 0x1b7c, 8, 8)
    a.address(0, 'name'); a.words(0x43ed, 480, 0x2b49, 10, 0x7014); call(-624)
    a.words(0x224d); call(-486)
    a.address(1, 'library'); a.words(0x7004); call(-552)
    store(20); a.words(0x4a80); a.branch(0x6700, 'done'); a.words(0x2840)
    a.address(1, 'intuition'); a.words(0x7025); call(-552)
    a.words(0x2e00, 0x4a80); a.branch(0x6700, 'close_mui')
    if text_roots:
        a.address(1, 'graphics'); a.words(0x7025); call(-552)
        a.words(0x2c00, 0x4a80); a.branch(0x6700, 'close_intuition')
    # Caller-owned NewMenu includes a separator and an explicitly disabled item.
    a.words(0x3b7c, 0x0100, 256); a.address(0, 'project'); a.words(0x2b48, 258)
    a.words(0x3b7c, 0x0200, 276); a.address(0, 'item'); a.words(0x2b48, 278)
    a.immediate(0, 17); store(292)  # Item UserData, preserved by the builder.
    a.words(0x3b7c, 0x0200, 296); a.immediate(0, 0xffffffff); store(298)
    a.words(0x3b7c, 0x0200, 316); a.address(0, 'disabled'); a.words(0x2b48, 318)
    a.words(0x3b7c, 0x0010, 326); a.immediate(0, 33); store(332)
    a.immediate(0, 0x80420e1f); store(128)
    a.words(0x41ed, 256, 0x2b48, 132)  # MUIA_Application_Menu = NewMenu*.
    for cycle in range(2):
        base = 32 + cycle * 32
        a.words(0x2c4c); a.address(0, 'class'); call(-78); store(base)
        a.words(0x4a80); a.branch(0x6700, 'close_graphics' if text_roots else 'close_intuition')
        for window in range(2):
            if list_rows and window == 1:
                a.immediate(0, 0x8042a308); store(608)  # List.Stripes.
                a.words(0x7001); store(612)
                a.words(0x7000); store(616)
                a.address(0, 'list'); a.words(0x43ed, 608); call(-30)
                store(448 + cycle*8 + window*4)
                a.immediate(1, 0x8042cba5); a.words(0x2b41, 192, 0x2b40, 196)
            elif text_roots:
                a.immediate(0, 0x8042f8dc); store(216)  # Text.Contents.
                a.address(0, 'label'); a.words(0x2b48, 220)
                if pointer_hook:
                    a.immediate(0, 0x8042fb04); store(224)  # MUIA_InputMode.
                    a.words(0x7001); store(228)
                if indexed_background:
                    a.immediate(0, 0x8042545b); store(232)  # Calc's Text background.
                    a.words(0x7004); store(236)
                a.address(0, 'text'); a.words(0x43ed, 216); call(-30)
                store(448 + cycle*8 + window*4)
                a.immediate(1, 0x8042cba5); a.words(0x2b41, 192, 0x2b40, 196)
            a.address(0, 'window'); a.words(0x43ed, 192); call(-30)
            store(96 + cycle * 8 + window * 4)
            a.words(0x4a80); a.branch(0x6700, f'free{cycle}')
            a.immediate(1, 0x8042bfe0)  # MUIA_Application_Window.
            a.words(0x2b41, 136 + window * 8, 0x2b40, 140 + window * 8)
        a.address(0, 'class'); a.words(0x43ed, 128); call(-30)
        store(base + 4); a.words(0x2440, 0x4a80); a.branch(0x6700, f'free{cycle}')
        get(0x804252d9, 2, base + 8, base + 12)  # Application.Menustrip.
        a.words(0x266d, base + 12, 0x200b, 0x4a80)
        a.branch(0x6700, f'dispose{cycle}')
        get(0x8042b25a, 3, base + 16, base + 20)  # Strip Family_ChildCount.
        get(0x80420e1f, 2, base + 24, base + 28)  # Obsolete alias getter.
        get(0x8042b25a, 2, 120 + cycle * 4, 112 + cycle * 4)  # App owns strip and both windows.
        if open_windows:
            result = 360 + cycle * 48
            a.words(0x266d, 96 + cycle * 8)  # First Window object in A3.
            a.immediate(0, 0x80428aa0); store(192)  # Window_Open tag.
            a.immediate(0, 1); store(196)
            a.words(0x2c47, 0x204b, 0x43ed, 192); call(-648); store(result)
            get(0x80428aa0, 3, result + 4, result + 8)
            get(0x80426a42, 3, result + 12, result + 16)
            if tall_text:
                a.words(0x206d, result + 16, 0x7000, 0x3028, 8); store(680 + cycle*8)
                a.words(0x7000, 0x3028, 10); store(684 + cycle*8)
            if pointer_hook:
                # Standard BOOPSI DoMethod inline ABI: _Object.o_Class at
                # object-4; IClass starts with Hook, whose Entry is at +8.
                def method(obj_offset, packet_offset):
                    a.words(0x246d, obj_offset, 0x206a, 0xfffc, 0x2668, 8,
                            0x43ed, packet_offset, 0x2c4c, 0x4e93)
                a.address(0, 'hook'); a.words(0x2b48, 168)
                a.words(0x41ed, 184, 0x2b48, 176)  # Hook.Data -> counter.
                # Notify(PRESSED, FALSE, self, CallHook, hook, argument).
                for off, value in [(512,0x8042c9cb),(516,0x80423535),(520,0),(528,3),(532,0x8042b96b),(540,5)]:
                    a.immediate(0, value); store(off)
                a.words(0x202d, 448 + cycle*8); store(524)
                a.words(0x41ed, 160, 0x2b48, 536)
                method(448 + cycle*8, 512); store(620 + cycle*4)
                # Real HandleEvent packets and IntuiMessage coordinates. This
                # proves the guest ABI/Notify bridge, separately from CIA input.
                a.immediate(0, 8); store(564)  # IntuiMessage.Class = MOUSEBUTTONS.
                # IntuiMessage includes IAddress at +28 before MouseX/Y.
                a.words(0x3b7c, 50, 576, 0x3b7c, 50, 578)
                a.immediate(0, 0x80426d66); store(596)
                a.words(0x41ed, 544, 0x2b48, 600)
                for index, code in enumerate((0x68, 0xe8, 0x68, 0xe8)):
                    a.words(0x3b7c, code, 568)
                    a.immediate(0, 0 if code == 0x68 else 2); store(604)
                    a.words(0x3b7c, 999 if index == 3 else 50, 576)
                    method(96 + cycle*8, 596)
                    store(640 + cycle*16 + index*4)
                a.words(0x202d, 184); store(628 + cycle*4)
                a.words(0x266d, 96 + cycle*8)  # Restore the GetAttr operand.
                a.words(0x246d, base + 4)  # Restore the owning Application.
            if update_text:
                # Real SetAttrs after Open must repaint the copied Text value.
                a.address(0, 'updated'); a.words(0x2b48, 220)
                a.words(0x2c47, 0x206d, 448 + cycle*8, 0x43ed, 216)
                call(-648); store(700 + cycle*16)
                a.words(0x266d, 448 + cycle*8)
                get(0x8042f8dc, 3, 704 + cycle*16, 708 + cycle*16)
                a.words(0x206d, 708 + cycle*16, 0x7000, 0x3010); store(712 + cycle*16)
                if indexed_background:
                    get(0x8042545b, 3, 736 + cycle*8, 740 + cycle*8)
                a.words(0x266d, 96 + cycle*8)
            if text_roots:
                # Keep the rendered window live for deterministic screenshots.
                a.words(0x3a3c, 999)
                a.label(f'hold{cycle}'); a.words(0x2c46); call(-270)
                a.branch(0x51cd, f'hold{cycle}')  # DBF D5, 1,000 fields.
            a.words(0x7000); store(196)
            a.words(0x2c47, 0x204b, 0x43ed, 192); call(-648); store(result + 20)
            get(0x80428aa0, 3, result + 24, result + 28)
            get(0x80426a42, 3, result + 32, result + 36)
            if list_rows:
                # The second owned Window has a real List root. Native List
                # mutation dispatch must paint both ordinary and striped rows.
                a.words(0x266d, 100 + cycle*8, 0x7001); store(196)
                a.words(0x2c47, 0x204b, 0x43ed, 192); call(-648)
                get(0x80426a42, 3, 608, 752 + cycle*4)
                if array_insert:
                    a.immediate(0, 0x80426c87); store(512)  # Classic List.Insert.
                    a.words(0x41ed, 248, 0x2b48, 516)
                    a.words(0x7001); store(520)  # one entry in the array
                    a.immediate(0, 0xfffffffd); store(524)  # bottom, as used by Calc
                else:
                    a.immediate(0, 0x804254d5); store(512)  # List.InsertSingle.
                    a.immediate(0, 0xffffffff); store(520)  # active/default position
                for label in ('updated', 'second_row'):
                    a.address(0, label); a.words(0x2b48, 248 if array_insert else 516)
                    method(452 + cycle*8, 512)
                a.words(0x266d, 452 + cycle*8)
                get(0x80421654, 3, 608, 760 + cycle*4)
                a.words(0x266d, 100 + cycle*8, 0x3a3c, 999)
                a.label(f'list_hold{cycle}'); a.words(0x2c46); call(-270)
                a.branch(0x51cd, f'list_hold{cycle}')
                a.words(0x7000); store(196)
                a.words(0x2c47, 0x204b, 0x43ed, 192); call(-648)
                get(0x80426a42, 3, 608, 616)
                a.words(0x246d, base + 4)  # Restore owning Application.
            a.words(0x7000); store(192)  # Next cycle constructs closed windows.
        a.label(f'dispose{cycle}'); a.words(0x2c4c, 0x204a); call(-36)
        a.label(f'free{cycle}'); a.words(0x2c4c, 0x206d, base); call(-84)
        a.words(0x2c78, 4, 0x7201); call(-216); store(24 + cycle * 4)
    a.immediate(0, 0x4d4d4341 if array_insert else 0x4d4d4339 if list_rows else 0x4d4d4338 if indexed_background else 0x4d4d4337 if update_text else 0x4d4d4336 if tall_text else 0x4d4d4335 if pointer_hook else 0x4d4d4334 if text_roots else 0x4d4d4333 if open_windows else 0x4d4d4332); store(16)
    if text_roots:
        a.label('close_graphics'); a.words(0x2246, 0x2c78, 4); call(-414)
    a.label('close_intuition'); a.words(0x2247, 0x2c78, 4); call(-414)
    a.label('close_mui'); a.words(0x224c, 0x2c78, 4); call(-414)
    a.label('done'); a.words(0x7000, 0x4cdf, 0x7cfc, 0x4e75)
    if pointer_hook:
        a.label('hook'); a.words(0x2068, 16, 0x5290, 0x7001, 0x4e75)
    for label, value in [('name', b'MuiClassicMenuProbe\0'), ('library', b'muimaster.library\0'),
                         ('intuition', b'intuition.library\0'), ('class', b'Application.mui\0'),
                         ('window', b'Window.mui\0'),
                         ('project', b'Project\0'), ('item', b'Item\0'), ('disabled', b'Disabled\0')]:
        a.label(label); a.code += value
        if len(a.code) & 1: a.code.append(0)
    if text_roots:
        contents = b'\n'.join([b'Native MUI', b'Resize check'] + [f'Line {n:02}'.encode('ascii') for n in range(3, 21)]) if tall_text else b'Native MUI'
        for label, value in [('graphics', b'graphics.library\0'), ('text', b'Text.mui\0'), ('label', contents + b'\0')]:
            a.label(label); a.code += value
            if len(a.code) & 1: a.code.append(0)
    if update_text:
        a.label('updated'); a.code += b'11\0\0'
    if list_rows:
        a.label('list'); a.code += b'List.mui\0\0'
        a.label('second_row'); a.code += b'12\0\0'
    return a.finish()
