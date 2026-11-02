"""Inspect a named alternate-stack probe resource in completed RAM captures."""
import argparse,json
from pathlib import Path
from capture_memory import banks
p=argparse.ArgumentParser(description=__doc__);p.add_argument('capture',type=Path);p.add_argument('--name',default='StackTimerProbe');p.add_argument('--output',type=Path,required=True);a=p.parse_args();bs=banks(a.capture)
def rd(address,length):
 for base,data,_ in bs:
  if base<=address and address+length<=base+len(data):return data[address-base:address-base+length]
 return bytes(length)
def u(address):return int.from_bytes(rd(address,4),'big')
found=0
for base,data,_ in bs:
 start=0
 while (idx:=data.find(bytes.fromhex('53545031'),start))>=0:
  start=idx+4;node=base+idx-20
  if rd(node+8,1)==b'\x08' and rd(u(node+10),32).split(b'\0')[0]==a.name.encode():found=node;break
if not found:raise ValueError('Probe resource unavailable')
phase=u(found+16);task=u(found+32);timer=a.name=='StackTimerProbe'
r=dict(protocol='manual-stack-timer-v1' if timer else 'manual-stack-delay-v1',capture=str(a.capture),resource=hex(found),stage=phase,alternateStack=hex(u(found+24)),oldSp=hex(u(found+28)),task=hex(task),recordedLower=hex(u(found+36)),recordedUpper=hex(u(found+40)),savedContext=hex(u(task+54)),taskState=rd(task+15,1).hex(),verdict='RETURNED_AND_STACK_FREED' if phase==3 else 'WAIT_DID_NOT_RETURN_WITHIN_CAPTURE' if phase==1 else 'INCOMPLETE')
if timer:
 r.update(port=hex(u(found+44)),ioRequest=hex(u(found+48)),openDeviceError=u(found+52))
 if phase==1:
  io=u(found+48);r.update(ioError=rd(io+31,1).hex(),ioCommand=int.from_bytes(rd(io+28,2),'big'),ioFlags=rd(io+30,1).hex(),ioMessageType=rd(io+8,1).hex(),taskSignalWait=hex(u(task+22)),taskSignalReceived=hex(u(task+26)))
if timer and r['openDeviceError'] != 0: r['verdict']='UNAVAILABLE_DEVICE_OPEN'
a.output.write_text(json.dumps(r,indent=2)+'\n');print(json.dumps(r))
