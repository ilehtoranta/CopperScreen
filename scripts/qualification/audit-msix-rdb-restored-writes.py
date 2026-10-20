"""Independently inspect full images, re-exported profiles and raw native receipts."""
from pathlib import Path
import argparse, hashlib, json, struct, zipfile

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('evidence', type=Path)
parser.add_argument('--inputs', type=Path, required=True)
parser.add_argument('--package-layout', type=Path, required=True)
args = parser.parse_args()
out = args.evidence
def read(p): return json.loads(p.read_text(encoding='utf-8-sig'))
def digest(data): return hashlib.sha256(data).hexdigest().upper()
def sha(p): return digest(p.read_bytes())
def bundle(path):
    with zipfile.ZipFile(path) as archive:
        assert sorted(archive.namelist()) == ['disks/unit-0.hdf', 'manifest.json', 'profile.json']
        disk, profile = archive.read('disks/unit-0.hdf'), archive.read('profile.json')
        manifest = json.loads(archive.read('manifest.json'))
        assert manifest['Version'] == 1 and manifest['ProfileSha256'] == digest(profile)
        assert manifest['Images'] == [{'Unit': 0, 'Bytes': len(disk), 'Sha256': digest(disk)}]
        return disk, json.loads(profile)
expected = bytearray((i*37+11)&255 for i in range(2049))
expected[509:541] = bytes(255-i for i in range(32))

def inspect(before, after):
    assert len(before) == len(after) == 3768320
    assert before[:131072] == after[:131072] and before[3735552:] == after[3735552:]
    results = []
    for part in range(2):
        start = (4 + part*55)*32768
        old, new = before[start:start+1802240], after[start:start+1802240]
        def word(data, block, at): return struct.unpack_from('>I', data, block*512+at)[0]
        def find(data, directory, name):
            for slot in range(72):
                block = word(data, directory, 24+slot*4)
                while block:
                    at = block*512
                    if data[at+433:at+433+data[at+432]].decode('ascii') == name: return block
                    block = word(data, block, 496)
            return 0
        def blocks(data, header):
            assert word(data, header, 504) == 0
            return [header]+[word(data, header, 308-i*4) for i in range(word(data, header, 8))]
        def contents(data, header):
            return b''.join(data[b*512:(b+1)*512] for b in blocks(data, header)[1:])[:word(data, header, 324)]
        directory = find(new, 1760, 'Data'); assert directory == find(old, 1760, 'Data')
        assert find(old, directory, 'After') == 0
        header = find(new, directory, 'After'); assert header != 0
        written = blocks(new, header); assert len(written) == len(set(written)) == 6
        assert contents(new, header) == expected
        saved = find(old, directory, 'Saved'); assert saved == find(new, directory, 'Saved')
        assert contents(old, saved) == contents(new, saved) == expected
        assert all(old[b*512:(b+1)*512] == new[b*512:(b+1)*512] for b in blocks(old, saved))
        bitmap = word(new, 1760, 316); assert bitmap == word(old, 1760, 316)
        def free(data, block): return bool(word(data, bitmap, 4+(block-2)//32*4) & (1 << ((block-2)%32)))
        assert sum(free(old,b) for b in range(2,3520))-sum(free(new,b) for b in range(2,3520)) == 6
        assert all(free(old,b) and not free(new,b) for b in written)
        for b in [1760, bitmap, directory, header]:
            assert sum(struct.unpack_from('>128I', new, b*512)) & 0xFFFFFFFF == 0
        allowed = set(written+[1760,bitmap,directory])
        for b in range(3520):
            if b not in allowed: assert old[b*512:(b+1)*512] == new[b*512:(b+1)*512], (part,b)
        results.append({'partition':part, 'newFile':'Data/After', 'payloadBytes':2049, 'newlyAllocatedBlocks':written, 'originalSavedAndUnrelatedBlocksUnchanged':True})
    return results

r = read(out/'installed-rdb-restored-writes-record.json')
assert r['Status'] == 'PASS' and r['InstalledRestoredWritesAcceptance'] and r['InstalledMsixAcceptance']
assert r['OwnedRegistrationRemoved'] and r['PrivateDataRetained']
assert r['SettingsExports'] == 2 and r['SettingsRestores'] == 4 and len(r['Phases']) == 6
layouts, snapshots, pids = [], [], []
for layout, prefix in [('default',''), ('priority','priority-')]:
    inputDisk, inputProfile = bundle(args.inputs/(layout+'.copperhdf'))
    assert sha(args.inputs/(layout+'.copperhdf')) == r['InputBackupSha256'][layout+'.copperhdf']
    exported, exportedProfile = bundle(out/'reexport'/(layout+'.copperhdf'))
    partitions = inspect(inputDisk, exported)
    write = next(p for p in r['Phases'] if p['phase'] == prefix+'restore-write')
    restart = next(p for p in r['Phases'] if p['phase'] == prefix+'restart')
    fresh = next(p for p in r['Phases'] if p['phase'] == prefix+'fresh-restore')
    assert write['restoredImagePath'] == restart['restoredImagePath'] != fresh['restoredImagePath']
    assert (out/'reexport'/(prefix+'restore-write-restored-before-boot.hdf')).read_bytes() == inputDisk
    assert (out/'reexport'/(prefix+'fresh-restore-restored-before-boot.hdf')).read_bytes() == exported
    assert write['backupSha256'] == sha(out/'reexport'/(layout+'.copperhdf'))
    assert inputProfile['HardDrives'][0]['Path'] != exportedProfile['HardDrives'][0]['Path'] == write['restoredImagePath']
    inputProfile['HardDrives'][0]['Path'] = exportedProfile['HardDrives'][0]['Path']
    assert inputProfile == exportedProfile and not exportedProfile['HardDrives'][0]['ReadOnly']
    for phase in [write,restart,fresh]:
        assert phase['passed'] and phase['completedWindowSession'] and phase['windowOpened']
        assert not phase['elevated'] and not phase['hostDosCalls'] and phase['HostProcess']['ExitCode'] == 0
        assert phase['scenario'] == 'restored-writes' and phase['guestFlushBeforeHostSave'] and not phase['hostSaveAdvancedCycles']
        assert phase['packageFullName'] == r['FirstInstalledPackage']['FullName']
        pids.append(phase['HostProcess']['Id'])
        for binary in phase['binaries']: assert sha(args.package_layout/Path(binary['path']).name) == binary['sha256']
        assert phase['liveSha256'] == phase['savedSha256AfterWindowClose'] == digest(exported)
        expectedSnapshots = ['-live','-reset'] if phase['phase'] == 'restore-write' else ['-live']
        assert [o['snapshot'] for o in phase['observations']] == expectedSnapshots
        for obs in phase['observations']:
            stem = out/'reexport'/(phase['phase']+obs['snapshot'])
            chip, slow = stem.with_suffix('.chipram').read_bytes(), stem.with_suffix('.slowram').read_bytes()
            def mem(address, length=4):
                if 0 <= address and address+length <= len(chip): return chip[address:address+length]
                assert 0xC00000 <= address and address-0xC00000+length <= len(slow)
                return slow[address-0xC00000:address-0xC00000+length]
            def word(address): return int.from_bytes(mem(address),'big')
            branch = 1 if phase is write and obs['snapshot'] == '-live' else 2
            assert word(0xB80) == 0x52444231 and word(0xB88) == (2 if layout == 'default' else 1)
            assert obs['phase'] == branch and word(0xB8C) != word(0xB90)
            requests = []
            for part in range(2):
                assert word(0xB98+part*4) == branch and mem(word(0xBA0+part*4),2049) == expected
                port = word(0xBC0+part*8); assert port == word(0xB8C+part*4) != 0 and word(0xBC4+part*8) != 0
                request = word(word(port-92+88)+4);requests.append(request)
                assert word(request+28) & 0xFFFF00FF == 4<<16 and word(request+32) == 0
            assert requests[0] != requests[1] and word(word(0x840)+20) == 0
            assert stem.with_suffix('.hdf').read_bytes() == exported and stem.with_suffix('.png').stat().st_size > 0
            snapshots.append({'layout':layout,'phase':phase['phase'],'snapshot':obs['snapshot'],'nativeBranch':branch,'bothCompletedFlushes':True})
    layouts.append({'layout':layout,'inputHdfSha256':digest(inputDisk),'writtenHdfSha256':digest(exported),'reexportSha256':sha(out/'reexport'/(layout+'.copperhdf')),'partitions':partitions})
assert len(snapshots) == 7
result = {'protocol':'copperscreen-restored-rdb-writes-audit-v1','passed':True,'installedProcesses':6,'fullRomBoots':7,'layouts':layouts,'rawNativeObservations':snapshots,'processIds':pids}
(out/'completion-audit.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'passed':True,'installedProcesses':6,'fullRomBoots':7,'bothRestoredPartitionsWritten':True,'bothPriorities':True,'reexportsAndFreshRestores':2},indent=2))
