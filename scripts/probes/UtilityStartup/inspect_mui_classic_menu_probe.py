"""Verify completed native NewMenu conversion, getters and balanced disposal."""
import argparse
import hashlib
import json
from pathlib import Path
import struct
from capture_memory import banks


def inspect(path):
    matches = []
    for base, data, bank_path in banks(path):
        start = 0
        while True:
            name = data.find(b'MuiClassicMenuProbe\0', start)
            if name < 0: break
            start = name + 1; record = name - 480
            if record < 0 or data[record + 8] != 8 or struct.unpack_from('>I', data, record + 10)[0] != base + name:
                continue
            fields = struct.unpack_from('>28I', data, record + 16)
            if fields[0] not in (0x4d4d4331, 0x4d4d4332, 0x4d4d4333, 0x4d4d4334, 0x4d4d4335, 0x4d4d4336, 0x4d4d4337, 0x4d4d4338, 0x4d4d4339, 0x4d4d4341) or not fields[1]:
                raise ValueError(f'{path}: incomplete probe: {fields}')
            if not fields[2] or fields[2] != fields[3]:
                raise ValueError(f'{path}: unbalanced repeated disposal: {fields[2:4]}')
            cycles = []
            for i in range(2):
                cls, app, status, strip, count_status, count, alias_status, alias = fields[4+i*8:12+i*8]
                if not cls or not app or status != 1 or not strip or count_status != 1 or count != 1 or alias_status != 1 or alias != strip:
                    raise ValueError(f'{path}: failed application/menu cycle {i+1}: {fields}')
                cycles.append(dict(applicationClass=cls, application=app, menustrip=strip, titleCount=count, alias=alias))
                if fields[0] in (0x4d4d4332, 0x4d4d4333, 0x4d4d4334, 0x4d4d4335, 0x4d4d4336, 0x4d4d4337, 0x4d4d4338, 0x4d4d4339, 0x4d4d4341):
                    if not fields[20+i*2] or not fields[21+i*2] or fields[24+i] != 3 or fields[26+i] != 1:
                        raise ValueError(f'{path}: failed Window ownership cycle {i+1}: {fields[20:]}')
                    cycles[-1].update(windows=fields[20+i*2:22+i*2], applicationChildCount=fields[24+i])
                if fields[0] in (0x4d4d4333, 0x4d4d4334, 0x4d4d4335, 0x4d4d4336, 0x4d4d4337, 0x4d4d4338, 0x4d4d4339, 0x4d4d4341):
                    opened = struct.unpack_from('>10I', data, record + 360 + i*48)
                    if (opened[1] != 1 or opened[2] != 1 or opened[3] != 1 or not opened[4]
                            or opened[6] != 1 or opened[7] != 0 or opened[8] != 1 or opened[9] != 0):
                        raise ValueError(f'{path}: failed native Window open/close cycle {i+1}: {opened}')
                    cycles[-1].update(nativeWindow=opened[4], closedWindow=opened[9], openClose='PASS')
                if fields[0] in (0x4d4d4334, 0x4d4d4335, 0x4d4d4336, 0x4d4d4337, 0x4d4d4338, 0x4d4d4339, 0x4d4d4341):
                    roots = struct.unpack_from('>2I', data, record + 448 + i*8)
                    if not all(roots): raise ValueError(f'{path}: missing Text roots: {roots}')
                    if fields[0] in (0x4d4d4339, 0x4d4d4341):
                        cycles[-1].update(rootObjects=roots, rootTypes=['Text.mui', 'List.mui'], visualVerification='REQUIRED_SEPARATELY')
                    else:
                        cycles[-1].update(textRoots=roots, visualVerification='REQUIRED_SEPARATELY')
                if fields[0] in (0x4d4d4335, 0x4d4d4336, 0x4d4d4337, 0x4d4d4338, 0x4d4d4339, 0x4d4d4341):
                    registered = struct.unpack_from('>I', data, record + 620 + i*4)[0]
                    callbacks = struct.unpack_from('>I', data, record + 628 + i*4)[0]
                    handled = struct.unpack_from('>4I', data, record + 640 + i*16)
                    if handled != (1, 1, 1, 1):
                        raise ValueError(f'{path}: unhandled pointer sequence: {handled}')
                    if registered != 1 or callbacks != i+1:
                        raise ValueError(f'{path}: duplicate/missing/cancelled-release hook: {registered}, {callbacks}')
                    cycles[-1].update(pointerNotify='PASS', callbackCount=callbacks, pointerEventResults=handled)
                if fields[0] in (0x4d4d4337, 0x4d4d4338, 0x4d4d4339, 0x4d4d4341):
                    setter, getter, pointer, word = struct.unpack_from('>4I', data, record + 700 + i*16)
                    if getter != 1 or not pointer or word != 0x3131:
                        raise ValueError(f'{path}: failed live Text setter/getter: {setter}, {getter}, {pointer}, {word}')
                    cycles[-1].update(textUpdate='PASS', updatedText='11', visualVerification='REQUIRED_SEPARATELY')
                if fields[0] in (0x4d4d4338, 0x4d4d4339, 0x4d4d4341):
                    status, background = struct.unpack_from('>2I', data, record + 736 + i*8)
                    if status != 1 or background != 4:
                        raise ValueError(f'{path}: changed/missing indexed Background: {status}, {background}')
                    cycles[-1].update(backgroundValue=background, indexedBackgroundPixels='REQUIRED_SEPARATELY')
                if fields[0] in (0x4d4d4339, 0x4d4d4341):
                    native_list_window = struct.unpack_from('>I', data, record + 752 + i*4)[0]
                    entries = struct.unpack_from('>I', data, record + 760 + i*4)[0]
                    closed_list_window = struct.unpack_from('>I', data, record + 616)[0]
                    if not native_list_window or entries != 2 or closed_list_window != 0:
                        raise ValueError(f'{path}: failed native List row lifecycle: {native_list_window}, {entries}, {closed_list_window}')
                    cycles[-1].update(listEntries=entries, listWindow=native_list_window, listPixels='REQUIRED_SEPARATELY')
                if fields[0] == 0x4d4d4336:
                    width, height = struct.unpack_from('>2I', data, record + 680 + i*8)
                    if width != 320 or not 200 <= height <= 256:
                        raise ValueError(f'{path}: uncommitted/invalid automatic resize: {width}x{height}')
                    cycles[-1].update(automaticResize='PASS', nativeWidth=width, nativeHeight=height)
            # The factory must not rewrite the caller-owned classic vector or tags.
            if (struct.unpack_from('>H', data, record+256)[0] != 0x0100
                    or struct.unpack_from('>H', data, record+276)[0] != 0x0200
                    or struct.unpack_from('>I', data, record+292)[0] != 17
                    or struct.unpack_from('>H', data, record+296)[0] != 0x0200
                    or struct.unpack_from('>I', data, record+298)[0] != 0xffffffff
                    or struct.unpack_from('>H', data, record+316)[0] != 0x0200
                    or struct.unpack_from('>H', data, record+326)[0] != 0x0010
                    or struct.unpack_from('>I', data, record+332)[0] != 33
                    or struct.unpack_from('>I', data, record+128)[0] != 0x80420e1f
                    or struct.unpack_from('>I', data, record+132)[0] != base+record+256):
                raise ValueError(f'{path}: caller-owned input was changed')
            matches.append(dict(result='PASS', protocol={0x4d4d4331:'MMC1',0x4d4d4332:'MMC2',0x4d4d4333:'MMC3',0x4d4d4334:'MMC4',0x4d4d4335:'MMC5',0x4d4d4336:'MMC6',0x4d4d4337:'MMC7',0x4d4d4338:'MMC8',0x4d4d4339:'MMC9',0x4d4d4341:'MMC10'}[fields[0]], resourceAddress=base+record, resourceBank=bank_path.name,
                                availableAfterCycles=fields[2:4], cycles=cycles))
    if len(matches) != 1: raise ValueError(f'{path}: expected one native menu probe')
    return dict(capture=str(path), sha256=hashlib.sha256(path.read_bytes()).hexdigest(), **matches[0])


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__); p.add_argument('capture', type=Path, nargs='+')
    print(json.dumps([inspect(path) for path in p.parse_args().capture], indent=2))
