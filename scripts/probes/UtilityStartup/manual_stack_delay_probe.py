"""Native diagnostic: DOS Delay after an application moves SP to allocated RAM.

This mirrors MUImem's startup stack convention without changing tc_SPLower/Upper.
The result is a reset-lived named Exec resource, never a host OS shim.
"""
import json,shutil,sys
from pathlib import Path
from prepare_frame_image_probe import Assembler
from prepare_mui_calc import put
from refresh_utility_gui import inventory,sha
from amitools.fs.ADFSVolume import ADFSVolume
from amitools.fs.blkdev.BlkDevFactory import BlkDevFactory

def command():
 a=Assembler()
 def call(n):a.words(0x4eae,n)
 def store(off,val):a.words(0x2b7c,val>>16,val,off)
 a.words(0x48e7,0x3f3e,0x2c78,4)
 a.address(1,'dos');a.words(0x7000);call(-552);a.words(0x2840,0x4a80);a.branch(0x6700,'done')
 a.immediate(0,128);a.immediate(1,0x10001);call(-198)
 a.words(0x2a40,0x4a80);a.branch(0x6700,'close')
 a.words(0x1b7c,8,8,0x43ed,96,0x2b49,10);a.address(0,'name');a.words(0x7010);call(-624)
 a.words(0x224d);call(-486);store(20,0x53545031)
 a.words(0x93c9);call(-294);a.words(0x2640,0x2b4b,32,0x2b6b,58,36,0x2b6b,62,40)
 a.immediate(0,10808);a.immediate(1,0x10001);call(-198)
 a.words(0x4a80);a.branch(0x6700,'close')
 a.words(0x2440,0x2b40,24,0x2e0f,0x2b47,28,0x2e40,0xdffc,0,10808)
 store(16,1);a.words(0x2c4c,0x7201);call(-198) # DOS Delay(1) on the alternate stack.
 store(16,2);a.words(0x2e47,0x2c78,4,0x224a);a.immediate(0,10808);call(-210)
 store(16,3)
 a.label('close');a.words(0x2c78,4,0x224c);call(-414)
 a.label('done');a.words(0x7000,0x4cdf,0x7cfc,0x4e75)
 for label,value in [('name',b'StackDelayProbe\0'),('dos',b'dos.library\0')]:
  a.label(label);a.code+=value
  if len(a.code)&1:a.code.append(0)
 return a.finish()

if __name__=='__main__':
 root=Path(sys.argv[1]);root.mkdir(parents=True,exist_ok=True);guest=command()
 for kind,base,expected in [('copperstart','mui-calc-copperos-ffs-v50-backfill.hdf','4473c347e20345e0f13171876b0147bbd63c4cb00cd79a5e19a21ab63b084695'),('kickstart31','mui-calc-copperos-ofs-v48.hdf','e1733f3cf0922dc1d1f10f9b10de37e16b2f486d108811a1d7e3d0a03a10e799')]:
  src=Path('artifacts/utility-mui-2026-10-31')/base;assert sha(src.read_bytes())==expected
  out=root/(kind+'.hdf')
  if out.exists():raise FileExistsError(out)
  shutil.copyfile(src,out);dev=BlkDevFactory().open(str(out));vol=ADFSVolume(dev);vol.open()
  try:
   before=inventory(vol);writes={'C/ManualStackDelayProbe':guest,'S/Startup-Sequence':b'C:ManualStackDelayProbe\n'}
   for path,data in writes.items():put(vol,path,data)
   after=inventory(vol);wanted={k.casefold():v for k,v in before.items()};wanted.update({k.casefold():sha(v) for k,v in writes.items()});assert {k.casefold():v for k,v in after.items()}==wanted
  finally:vol.close();dev.close()
  Path(str(out)+'.json').write_text(json.dumps(dict(protocol='manual-stack-delay-v1',baseSha256=expected,sha256=sha(out.read_bytes()),commandSha256=sha(guest),installedFiles={k:sha(v) for k,v in writes.items()},allGuestFiles=after),indent=2)+'\n')
  print(out)
