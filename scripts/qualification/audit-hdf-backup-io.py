"""Independently audit installed storage failures, interruptions and native readback."""
from pathlib import Path
import argparse, hashlib, json, zipfile
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('evidence',type=Path)
parser.add_argument('--inputs',type=Path,required=True)
parser.add_argument('--package-layout',type=Path,required=True)
args=parser.parse_args(); out=args.evidence

def read(path): return json.loads(path.read_text(encoding='utf-8-sig'))
def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest().upper()
r=read(out/'installed-hdf-backup-io-record.json')
assert r['Status']=='PASS' and r['InstalledBackupIoAcceptance'] and r['InstalledMsixAcceptance']
assert r['OwnedRegistrationRemoved'] and r['PrivateDataRetained']
assert r['DiskFullIsDiagnosticFaultInjection'] and r['ActualWindowsAccessDenied'] and r['ExpectedForcedTerminations']==4
phases=['io-failures','io-export-partial','io-export-flushed','io-restore-partial','io-restore-flushed','io-recovery']
assert [p['phase'] for p in r['Phases']]==phases
assert len({p['HostProcess']['Id'] for p in r['Phases']})==6
first=r['Phases'][0]; baseline=first['protectedFilesBefore']
assert first['passed'] and first['postIoFullRomReadback'] and first['allIoFailuresPassed']
assert first['protectedFilesAfter']==baseline
assert [c['name'] for c in first['ioCases']]==['export-disk-full','restore-disk-full','export-access-denied','restore-access-denied','restore-settings-unwritable']
for c in first['ioCases']:
    assert all(c[k] for k in ['savedFilesUnchanged','draftUnchanged','committedUnchanged','backupUnchanged','noPublishedOrPartialFiles'])
    assert c['hresult']==('80070070' if c['failure']=='full' else '80070005')
    assert c['hitBytes']>=262144 if c['failure']=='full' else c['hitBytes']==0
    assert (out/'io-failures'/('io-failures-'+c['name']+'.png')).stat().st_size>0
    assert not any((args.inputs/c['name']).iterdir())
private=out/'package-private-data/backup-io-default'
for name,value in baseline.items(): assert sha(private/name)==value
backup=args.inputs/'existing-backup.copperhdf'
assert sha(backup)==first['backupHashBefore']==r['InputBackupSha256']['existing-backup.copperhdf']
with zipfile.ZipFile(backup) as source: disk=source.read('disks/unit-0.hdf')
assert hashlib.sha256(disk).hexdigest().upper()==r['FixtureSha256']==baseline['disk.hdf']
assert read(private/'last-machine.json')['HardDrives'][0]['Path']==str(Path(first['userDataDirectory'])/'disk.hdf')
expectedRestores=set()
for p in r['Phases']:
    assert not p['elevated'] and not p['hostDosCalls'] and p['windowOpened']
    assert p['packageFullName']==r['FirstInstalledPackage']['FullName']
    for binary in p['binaries']: assert sha(args.package_layout/Path(binary['path']).name)==binary['sha256']
    if p['phase'].startswith(('io-export-','io-restore-')):
        assert p['ForcedTermination'] and not p['passed']
        b=p['InterruptedAt']; assert b['protectedFiles']==baseline and b['backupHash']==first['backupHashBefore']
        path=Path(b['staged'])
        if b['operation']=='restore':
            relative=path.relative_to(p['userDataDirectory']); path=private/relative; expectedRestores.add(path.name)
        else:
            assert path.parent==args.inputs.resolve()/p['phase'] and not (path.parent/'new.copperhdf').exists()
        assert path.exists()
        for artifact in p['UnpublishedArtifacts']:
            artifactPath=Path(artifact['Path'])
            if b['operation']=='restore': artifactPath=private/artifactPath.relative_to(p['userDataDirectory'])
            assert artifactPath.stat().st_size==artifact['Bytes'] and sha(artifactPath)==artifact['Sha256']
        if b['operation']=='export':
            assert path.name.startswith('.hdf-backup-') and path.suffix=='.tmp'
            if p['phase'].endswith('flushed'):
                with zipfile.ZipFile(path) as complete:
                    assert complete.testzip() is None and complete.read('disks/unit-0.hdf')==disk
            else: assert 0<path.stat().st_size<backup.stat().st_size
        else:
            assert path.name.startswith('.restore-')
            image=path/'unit-0.hdf'
            assert image.read_bytes()==(disk if p['phase'].endswith('flushed') else disk[:262144])
    else:
        assert p['passed'] and p['HostProcess']['ExitCode']==0 and p['savedSha256AfterWindowClose']==r['FixtureSha256']

for p in r['Phases']:
    payload = bytearray((i*37+11)&255 for i in range(2049)); payload[509:541] = bytes(255-i for i in range(32))
    assert [o['snapshot'] for o in p['observations']] == (['-live','-after-io'] if p['phase'] == 'io-failures' else ['-live'])
    for obs in p['observations']:
        stem = out/p['phase']/(p['phase']+obs['snapshot'])
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

assert {p.name for p in (private/'HardDiskRestores').iterdir()}==expectedRestores
result={'protocol':'copperscreen-hdf-backup-io-audit-v1','passed':True,'gracefulFailures':5,'forcedTerminations':4,'installedProcesses':6,'fullRomBoots':7,
        'diskFullFaultInjection':True,'actualWindowsAccessDenied':True,'existingDisksSettingsBackupUnchanged':True,'gracefulFailureStagingCleaned':True,
        'interruptedArtifactsRemainUnpublished':True,'interruptedArtifactsRetained':True,'freshFullRomBothPartitionsReadback':True}
(out/'completion-audit.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
print(json.dumps(result,indent=2))
