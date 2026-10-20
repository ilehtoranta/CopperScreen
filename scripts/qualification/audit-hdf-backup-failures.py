"""Audit failed app operations, byte preservation and raw post-failure ROM readback."""
from pathlib import Path
import argparse, hashlib, json, zipfile
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('evidence', type=Path)
parser.add_argument('--inputs', type=Path, required=True)
parser.add_argument('--package-layout', type=Path, required=True)
args = parser.parse_args(); out = args.evidence
def read(p): return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest().upper()
r = read(out/'installed-hdf-backup-failures-record.json')
assert r['Status'] == 'PASS' and r['InstalledBackupFailureAcceptance'] and r['InstalledMsixAcceptance']
assert r['OwnedRegistrationRemoved'] and r['PrivateDataRetained'] and len(r['Phases']) == 1
p = r['Phases'][0]
assert p['passed'] and p['postFailureFullRomReadback'] and p['allFailureCasesPassed']
assert not p['elevated'] and not p['hostDosCalls'] and p['HostProcess']['ExitCode'] == 0
assert p['packageFullName'] == r['FirstInstalledPackage']['FullName'] and not r['FirstInstalledPackage']['IsDevelopmentMode']
for binary in p['binaries']: assert sha(args.package_layout/Path(binary['path']).name) == binary['sha256']
expectedCases = ['paused-export','paused-restore','running-export','running-restore','corrupt-image','corrupt-profile','truncated-archive','truncated-image','existing-backup']
assert [c['name'] for c in p['failureCases']] == expectedCases
for c in p['failureCases']:
    assert c['error'] and all(c[k] for k in ['savedFilesUnchanged','inputsUnchanged','draftUnchanged','committedUnchanged','runtimeUnchanged','noPublishedRestoreOrStaging'])
    assert c['active'] == (c['name'].startswith('paused-') or c['name'].startswith('running-'))
    assert c['paused'] == c['name'].startswith('paused-')
    assert (out/'failures'/('failures-'+c['name']+'.png')).stat().st_size > 0
assert p['filesBeforeFailures'] == p['filesAfterFailures']
assert p['inputHashesBeforeFailures'] == p['inputHashesAfterFailures']
before = out/'failures/failures-before-data'; after = out/'failures/failures-after-data'
private = out/'package-private-data/backup-failures-default'
for directory in [before,after,private]:
    files = {str(f.relative_to(directory)).replace('/', '\\'):sha(f) for f in directory.rglob('*')
             if f.is_file() and f.relative_to(directory).parts[0].lower() != 'logs'}
    assert files == p['filesBeforeFailures']
assert not (private/'HardDiskRestores').exists() or not any((private/'HardDiskRestores').iterdir())
for name, value in p['inputHashesBeforeFailures'].items(): assert sha(args.inputs/name) == value
source = args.inputs/'existing-backup.copperhdf'
with zipfile.ZipFile(source) as archive:
    disk = archive.read('disks/unit-0.hdf')
    assert hashlib.sha256(disk).hexdigest().upper() == r['FixtureSha256']
    for case, entryName in [('corrupt-image','disks/unit-0.hdf'),('corrupt-profile','profile.json'),('truncated-image','disks/unit-0.hdf')]:
        with zipfile.ZipFile(args.inputs/(case+'.copperhdf')) as invalid:
            assert invalid.testzip() is None  # Valid ZIP CRCs; failure must reach backup checks.
            assert invalid.read('manifest.json') == archive.read('manifest.json')
            changed = invalid.read(entryName); original = archive.read(entryName)
            if case == 'truncated-image': assert changed == original[:len(original)//2]
            else: assert len(changed) == len(original) and sum(a != b for a,b in zip(changed,original)) == 1
assert (args.inputs/'truncated-archive.copperhdf').read_bytes() == source.read_bytes()[:-32]
payload = bytearray((i*37+11)&255 for i in range(2049)); payload[509:541] = bytes(255-i for i in range(32))
assert [o['snapshot'] for o in p['observations']] == ['-live','-after-failures']
for obs in p['observations']:
    stem = out/'failures'/('failures'+obs['snapshot'])
    chip, slow = stem.with_suffix('.chipram').read_bytes(), stem.with_suffix('.slowram').read_bytes()
    def mem(address,length=4):
        if 0 <= address and address+length <= len(chip): return chip[address:address+length]
        assert 0xC00000 <= address and address-0xC00000+length <= len(slow)
        return slow[address-0xC00000:address-0xC00000+length]
    def word(address): return int.from_bytes(mem(address),'big')
    assert word(0xB80) == 0x52444231 and word(0xB88) == 2 and word(0xB8C) != word(0xB90)
    requests = []
    for part in range(2):
        assert word(0xB98+part*4) == 2 and mem(word(0xBA0+part*4),2049) == payload
        port = word(0xBC0+part*8); assert port == word(0xB8C+part*4) != 0 and word(0xBC4+part*8) != 0
        request = word(word(port-92+88)+4); requests.append(request)
        assert word(request+28) & 0xFFFF00FF == 4<<16 and word(request+32) == 0
    assert requests[0] != requests[1] and word(word(0x840)+20) == 0
    assert stem.with_suffix('.hdf').read_bytes() == disk
assert p['liveSha256'] == p['savedSha256AfterWindowClose'] == r['FixtureSha256']
result = {'protocol':'copperscreen-hdf-backup-failures-audit-v1','passed':True,'installedFailureCases':9,'fullRomBoots':2,
          'cases':expectedCases,'existingDisksAndSettingsByteIdentical':True,'unsavedAndCommittedSettingsPreserved':True,
          'activeSessionRetained':True,'noPublishedRestoreOrStaging':True,'existingBackupUnchanged':True,'postFailureBothPartitionsReadBack':True}
(out/'completion-audit.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
print(json.dumps(result,indent=2))
