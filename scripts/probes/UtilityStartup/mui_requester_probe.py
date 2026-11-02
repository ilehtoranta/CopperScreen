"""Genuine guest MUI_RequestA probe, with copied Exec-resource observations."""
from prepare_frame_image_probe import Assembler


def command(reference_window=False):
    a=Assembler()
    def call(lvo): a.words(0x4eae,lvo)
    a.words(0x48e7,0x3f3e,0x2c78,4)
    a.immediate(0,512);a.immediate(1,0x10001);call(-198)
    a.words(0x4a80);a.branch(0x6700,'done')
    a.words(0x2a40,0x1b7c,8,8,0x43ed,64,0x2b49,10)
    a.address(0,'name');a.immediate(0,20);call(-624)
    a.words(0x224d);call(-486)
    a.words(0x2b7c,0x4d55,0x5031,16)
    a.address(1,'library');a.words(0x700c);call(-552)
    a.words(0x2b40,20,0x4a80);a.branch(0x6700,'wait')
    a.words(0x2840)
    if reference_window:
        a.address(1,'intuition');a.words(0x7025);call(-552)
        a.words(0x2e00,0x4a80);a.branch(0x6700,'close')
        a.words(0x2b7c,0x8042,0xf8dc,128);a.address(0,'reference');a.words(0x2b48,132)
        a.words(0x2c4c,0x43ed,128);a.address(0,'text');call(-30)
        a.words(0x2b40,40,0x4a80);a.branch(0x6700,'close')
        a.words(0x2b7c,0x8042,0xcba5,128,0x2b40,132,0x43ed,128)
        a.address(0,'window');call(-30)
        a.words(0x2b40,44,0x4a80);a.branch(0x6700,'close')
        a.words(0x2b7c,0x8042,0xbfe0,128,0x2b40,132,0x43ed,128)
    else:a.words(0x93c9)
    a.words(0x2c4c);a.address(0,'application');call(-30)
    a.words(0x2b40,24,0x4a80);a.branch(0x6700,'close')
    if reference_window:
        a.words(0x2b7c,0x8042,0x8aa0,128,0x2b7c,0,1,132)
        a.words(0x2c47,0x206d,44,0x43ed,128);call(-648)
        a.immediate(0,0x80426a42);a.words(0x206d,44,0x43ed,52);call(-654)
        a.words(0x2b40,56)
    a.words(0x2b7c,0,1,28)
    # Classic muimaster FD: d0/d1/d2/a0/a1/a2/a3, not all address registers.
    a.words(0x202d,24)
    a.words(0x222d,44) if reference_window else a.words(0x7200)
    a.words(0x7400,0x97cb,0x2c4c)
    a.address(0,'title');a.address(1,'gadgets');a.address(2,'format');call(-42)
    a.words(0x2b40,32,0x2b7c,0,2,28,0x206d,24,0x2c4c);call(-36)
    a.label('close');a.words(0x2c78,4)
    if reference_window:
        a.words(0x4a87);a.branch(0x6700,'close_mui')
        a.words(0x2247);call(-414)
        a.label('close_mui')
    a.words(0x224c);call(-414)
    a.words(0x2b7c,0,3,28,0x2b7c,0,1,36)
    a.label('wait');a.words(0x2c78,4);a.immediate(0,0x80000000);call(-318)
    a.branch(0x6000,'wait')
    a.label('done');a.words(0x7000,0x4cdf,0x7cfc,0x4e75)
    strings=[('name','MUI_RequestProbe'),('library','muimaster.library'),
                       ('application','Application.mui'),('title','Requester probe'),
                       ('gadgets','OK'),('format','Update Successful.')]
    if reference_window:strings += [('intuition','intuition.library'),('text','Text.mui'),('window','Window.mui'),('reference','Reference window')]
    for label,text in strings:
        a.label(label);a.code+=text.encode()+b'\0'
        if label=='name':a.code+=bytes(20-len(text)-1)
        if len(a.code)&1:a.code.append(0)
    return a.finish()
