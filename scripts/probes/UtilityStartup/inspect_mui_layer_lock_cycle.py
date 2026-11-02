"""Check the two-task paint/refresh lock cycle in a completed native capture.

Uses the bounded CopperStart bridge and Layers wait-record diagnostic layouts.
This is an ownership observation, not a stack unwinder or timing measurement.
"""
import argparse
import json
from pathlib import Path
from capture_memory import banks

p = argparse.ArgumentParser(description=__doc__)
p.add_argument('capture', type=Path)
p.add_argument('--output', type=Path, required=True)
a = p.parse_args()
j = json.loads(a.capture.read_text())
memory = banks(a.capture.with_suffix('.fastram'))

def read(address, length):
    for base, data, _ in memory:
        if base <= address and address + length <= base + len(data):
            return data[address-base:address-base+length]
    raise ValueError(f'Unmapped diagnostic address {address:#x}')

def u32(address): return int.from_bytes(read(address, 4), 'big')
def i16(address): return int.from_bytes(read(address, 2), 'big', signed=True)
def text(address): return read(address, 80).split(b'\0')[0].decode('latin1')

exec_base = u32(4)
application = u32(exec_base+276)
refresh = None
task = u32(exec_base+420)
for _ in range(128):
    if not task or not u32(task): break
    if u32(task+10) and text(u32(task+10)) == 'Intuition refresh': refresh = task
    task = u32(task)
windows = [o['NativeWindow'] for o in j['state']['Objects'] if o['NativeWindow']]
if refresh is None or len(windows) != 1:
    raise ValueError('Expected one live MUI window and the native refresh task')
layer = u32(windows[0]+124)
info = u32(layer+68)
app_bridge = int(j['address'][4], 16)
refresh_bridge = u32(refresh+54)+66
app_wait = u32(app_bridge+32)
refresh_wait = u32(refresh_bridge+32)

def wait(record):
    return dict(address=hex(record), task=hex(u32(record+4)),
                token=hex(u32(record+8)), signal=u32(record+12),
                waitObject=hex(u32(record+16)))

def semaphore(address):
    return dict(address=hex(address), owner=hex(u32(address+40)),
                nestCount=i16(address+14), queueCount=i16(address+44))

cycle = (u32(app_wait+4) == application and u32(app_wait+16) == info and
         u32(refresh_wait+4) == refresh and u32(refresh_wait+16) == layer and
         u32(layer+72+40) == application and u32(info+24+40) == refresh and
         i16(layer+72+14) >= 0 and i16(info+24+14) >= 0)
result = dict(protocol='mui-native-paint-refresh-lock-cycle-v1', capture=str(a.capture),
              application=hex(application), refreshTask=hex(refresh),
              window=hex(windows[0]), layer=hex(layer), layerInfo=hex(info),
              applicationWait=wait(app_wait), refreshWait=wait(refresh_wait),
              layerSemaphore=semaphore(layer+72), layerInfoSemaphore=semaphore(info+24),
              verdict='CONFIRMED_TWO_TASK_LOCK_CYCLE' if cycle else 'CYCLE_NOT_ESTABLISHED')
a.output.write_text(json.dumps(result, indent=2)+'\n')
print(json.dumps(result))
