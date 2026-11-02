"""Native launch witness for the original MUIMenu fixture command."""
from prepare_frame_image_probe import Assembler

def command():
 a=Assembler()
 def call(n):a.words(0x4eae,n)
 a.words(0x48e7,0x3f3e,0x2c78,4);a.address(1,'name');call(-498)
 a.words(0x4a80);a.branch(0x6600,'existing')
 a.immediate(0,128);a.immediate(1,0x10001);call(-198)
 a.words(0x4a80);a.branch(0x6700,'done')
 a.words(0x2a40,0x1b7c,8,8,0x43ed,96,0x2b49,10)
 a.address(0,'name');a.words(0x7010);call(-624)
 a.words(0x224d);call(-486);a.words(0x200d)
 a.label('existing');a.words(0x2a40,0x2b7c,0x4d4e,0x5531,16,0x202d,20,0x5280,0x2b40,20)
 a.words(0x93c9);call(-294);a.words(0x2b40,24)
 a.label('done');a.words(0x7000,0x4cdf,0x7cfc,0x4e75)
 a.label('name');a.code+=b'MUI_MenuWitness\0'
 if len(a.code)&1:a.code.append(0)
 return a.finish()
