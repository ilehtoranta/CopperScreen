"""Guest LoadSeg owner for the linked icon writer; retains code without copying it."""
from prepare_frame_image_probe import Assembler


def command():
    a=Assembler()
    def call(lvo):a.words(0x4eae,lvo)
    a.words(0x48e7,0x3f3e,0x2c78,4)
    a.address(1,'dos');a.words(0x7025);call(-552)
    a.words(0x2840,0x4a80);a.branch(0x6700,'failed')
    a.words(0x2c4c);a.address(0,'provider');a.words(0x2208);call(-150)
    a.words(0x2e00,0x4a80);a.branch(0x6700,'close_failed')
    a.words(0x2007,0xe588,0x2040,0x5888,0x2f07,0x2f0c,0x4e90,0x285f,0x2e1f)
    a.words(0x2c78,4);a.address(1,'resource');call(-498)
    a.words(0x2440,0x4a80);a.branch(0x6700,'unload_failed')
    a.words(0x0caa,0x4957,0x5031,16);a.branch(0x6600,'unload_failed')
    a.words(0x0caa,0,1,28);a.branch(0x6600,'unload_failed')
    a.words(0x4aaa,36);a.branch(0x6700,'unload_failed')
    a.words(0x4aaa,32);a.branch(0x6600,'duplicate')
    a.words(0x2547,32);a.branch(0x6000,'success')
    a.label('duplicate');a.words(0x2c4c,0x2207);call(-156)
    a.label('success');a.words(0x2c78,4,0x224c);call(-414)
    a.words(0x7000);a.branch(0x6000,'done')
    a.label('unload_failed');a.words(0x2c4c,0x2207);call(-156)
    a.label('close_failed');a.words(0x2c78,4,0x224c);call(-414)
    a.label('failed');a.words(0x7014)
    a.label('done');a.words(0x4cdf,0x7cfc,0x4e75)
    for label,text in [('dos','dos.library'),('provider','C:IconWriteProvider'),('resource','CopperStart.IconWriter')]:
        a.label(label);a.code+=text.encode()+b'\0'
        if len(a.code)&1:a.code.append(0)
    return a.finish()
