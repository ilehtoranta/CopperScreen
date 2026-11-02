"""Read the native MUIMenu command witness from a completed RAM capture."""
import argparse
import json
from pathlib import Path
from capture_memory import banks, exec_resource_nodes

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('capture', type=Path)
parser.add_argument('--output', type=Path, required=True)
args = parser.parse_args()
memory = banks(args.capture)

def read(address, length):
    for base, data, _ in memory:
        if base <= address and address + length <= base + len(data):
            return data[address-base:address-base+length]
    return bytes(length)

def u32(address):
    return int.from_bytes(read(address, 4), 'big')

nodes = []
for node in exec_resource_nodes(memory):
    if read(node+16,4) == bytes.fromhex('4d4e5531') and read(node + 8, 1) == b'\x08' and read(u32(node + 10), 16) == b'MUI_MenuWitness\0':
        nodes.append(node)
result = dict(protocol='mui-menu-native-launch-witness-v1', verifierProtocol='exec-registered-resource-witness-v1', capture=str(args.capture),
              resources=[dict(address=hex(node), launches=u32(node+20), task=hex(u32(node+24))) for node in nodes],
              verdict='PASS_COMMAND_EXECUTED_ONCE' if len(nodes) == 1 and u32(nodes[0]+20) == 1
              else 'COMMAND_NOT_PROVEN')
args.output.write_text(json.dumps(result, indent=2) + '\n')
print(json.dumps(result))
