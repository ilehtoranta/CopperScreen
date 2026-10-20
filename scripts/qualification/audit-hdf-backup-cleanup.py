"""Audit installed startup cleanup, preserved objects and native partition readback."""
from pathlib import Path
import argparse, hashlib, json, zipfile

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('evidence', type=Path)
parser.add_argument('--inputs', type=Path, required=True)
parser.add_argument('--package-layout', type=Path, required=True)
args = parser.parse_args()
out = args.evidence

def read(path): return json.loads(path.read_text(encoding='utf-8-sig'))
def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest().upper()

r = read(out/'installed-hdf-backup-cleanup-record.json')
assert r['Status'] == 'PASS' and r['InstalledBackupCleanupAcceptance'] and r['InstalledMsixAcceptance']
assert r['OwnedRegistrationRemoved'] and r['PrivateDataRetained']
assert r['AutomaticStartupCleanups'] == r['ExpectedForcedTerminations'] == 4
assert r['FullRomBoots'] == r['FullRomPhases'] == 5
phases = ['io-export-partial', 'io-export-flushed', 'io-restore-partial', 'io-restore-flushed', 'io-recovery']
assert [p['phase'] for p in r['Phases']] == phases
assert len({p['HostProcess']['Id'] for p in r['Phases']}) == 5
first = r['Phases'][0]
baseline = first['protectedFilesBefore']
private = out/'package-private-data/backup-cleanup-default'
for name, value in baseline.items(): assert sha(private/name) == value
backup = args.inputs/'existing-backup.copperhdf'
assert sha(backup) == first['backupHashBefore'] == r['InputBackupSha256']['existing-backup.copperhdf']
with zipfile.ZipFile(backup) as source: disk = source.read('disks/unit-0.hdf')
assert hashlib.sha256(disk).hexdigest().upper() == r['FixtureSha256'] == baseline['disk.hdf']
assert read(private/'last-machine.json')['HardDrives'][0]['Path'] == str(Path(first['userDataDirectory'])/'disk.hdf')

def retained(path):
    path = Path(path)
    try: return private/path.relative_to(first['userDataDirectory'])
    except ValueError: return path

for path, expected in r['PreservationSentinels'].items(): assert sha(retained(path)) == expected
assert len(r['PreservationSentinels']) == 4
assert not list((private/'HardDiskBackupOperations').iterdir())
expected_dirs = {retained(p).parent.name for p in r['PreservationSentinels'] if 'HardDiskRestores' in p}
assert {p.name for p in (private/'HardDiskRestores').iterdir()} == expected_dirs

for index, p in enumerate(r['Phases']):
    assert not p['elevated'] and not p['hostDosCalls'] and p['windowOpened']
    assert p['packageFullName'] == r['FirstInstalledPackage']['FullName']
    assert p['startupBackupCleanup'] == {'Cleaned': int(index > 0), 'Deferred': 0}
    for binary in p['binaries']: assert sha(args.package_layout/Path(binary['path']).name) == binary['sha256']
    if index < 4:
        assert p['ForcedTermination'] and not p['passed']
        b = p['InterruptedAt']
        assert b['protectedFiles'] == baseline and b['backupHash'] == first['backupHashBefore']
        path = Path(b['staged'])
        assert not retained(path).exists() and not retained(p['OwnershipJournal']).exists()
        copies = out/p['phase']/'abandoned-files'
        assert {a.name for a in copies.iterdir()} == {Path(a['Path']).name for a in p['UnpublishedArtifacts']}
        for artifact in p['UnpublishedArtifacts']:
            copy = copies/Path(artifact['Path']).name
            assert copy.stat().st_size == artifact['Bytes'] and sha(copy) == artifact['Sha256']
        journal = out/p['phase']/'abandoned-records'/Path(p['OwnershipJournal']).name
        assert sha(journal) == p['OwnershipJournalSha256']
        raw = journal.read_bytes()
        assert raw.endswith(b'\n')
        records = [json.loads(line) for line in raw.splitlines()]
        assert records[0] == {'Version': 1, 'Kind': b['operation'], 'Path': b['staged']}
        entries = {entry['Name']: entry['Identity'] for entry in records[1:]}
        assert len(entries) == len(records)-1 and '' in entries
        assert all(e['Volume'] and (e['Low'] or e['High']) for e in entries.values())
        operation_id = journal.stem
        assert len(operation_id) == 32 and all(c in '0123456789abcdef' for c in operation_id)
        if b['operation'] == 'export':
            assert path.name == '.hdf-backup-'+operation_id+'.tmp'
            assert path.parent == args.inputs.resolve()/p['phase'] and not (path.parent/'new.copperhdf').exists()
            assert set(entries) == {''} and not entries['']['Directory']
            copy = copies/path.name
            if p['phase'].endswith('flushed'):
                with zipfile.ZipFile(copy) as complete:
                    assert complete.testzip() is None and complete.read('disks/unit-0.hdf') == disk
            else: assert 0 < copy.stat().st_size < backup.stat().st_size
        else:
            assert path.name == '.restore-'+operation_id and entries['']['Directory']
            assert set(entries) == {'', 'profile.json', 'unit-0.hdf'}
            assert not entries['profile.json']['Directory'] and not entries['unit-0.hdf']['Directory']
            assert (copies/'unit-0.hdf').read_bytes() == (disk if p['phase'].endswith('flushed') else disk[:262144])
    else:
        assert p['passed'] and p['HostProcess']['ExitCode'] == 0
        assert p['savedSha256AfterWindowClose'] == r['FixtureSha256']

    # Recheck raw guest RAM and whole media, independently of app booleans.
    payload = bytearray((i*37+11)&255 for i in range(2049))
    payload[509:541] = bytes(255-i for i in range(32))
    assert [o['snapshot'] for o in p['observations']] == ['-live']
    stem = out/p['phase']/(p['phase']+'-live')
    chip, slow = stem.with_suffix('.chipram').read_bytes(), stem.with_suffix('.slowram').read_bytes()
    def mem(address, length=4):
        if 0 <= address and address+length <= len(chip): return chip[address:address+length]
        assert 0xC00000 <= address and address-0xC00000+length <= len(slow)
        return slow[address-0xC00000:address-0xC00000+length]
    def word(address): return int.from_bytes(mem(address), 'big')
    assert word(0xB80) == 0x52444231 and word(0xB88) == 2 and word(0xB8C) != word(0xB90)
    requests = []
    for part in range(2):
        assert word(0xB98+part*4) == 2 and mem(word(0xBA0+part*4), 2049) == payload
        port = word(0xBC0+part*8)
        assert port == word(0xB8C+part*4) != 0 and word(0xBC4+part*8) != 0
        request = word(word(port-92+88)+4)
        requests.append(request)
        assert word(request+28) & 0xFFFF00FF == 4 << 16 and word(request+32) == 0
    assert requests[0] != requests[1] and word(word(0x840)+20) == 0
    assert stem.with_suffix('.hdf').read_bytes() == disk

result = {'protocol': 'copperscreen-hdf-backup-cleanup-audit-v1', 'passed': True,
          'forcedTerminations': 4, 'automaticStartupCleanups': 4, 'installedProcesses': 5, 'fullRomBoots': 5,
          'existingDisksSettingsBackupUnchanged': True, 'untrackedAndPublishedObjectsPreserved': True,
          'interruptedArtifactsAndOwnershipRecordsRemoved': True, 'preCleanupCopiesIndependentlyAudited': True,
          'freshFullRomBothPartitionsReadback': True}
(out/'completion-audit.json').write_text(json.dumps(result, indent=2)+'\n', encoding='utf-8')
print(json.dumps(result, indent=2))
