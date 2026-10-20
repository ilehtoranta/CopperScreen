from pathlib import Path
import json,hashlib,argparse
parser=argparse.ArgumentParser(description="Audit retained installed RDB backup/restore evidence without running the emulator.")
parser.add_argument("evidence",type=Path)
parser.add_argument("--package-layout",type=Path,required=True)
args=parser.parse_args()
out=args.evidence.resolve()
def read(p): return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest().upper()
r=read(out/'installed-rdb-reinstall-record.json')
assert r['Status']=='PASS' and r['InstalledMsixAcceptance'] and r['InstalledReinstallAcceptance']
assert r['OwnedRegistrationRemoved'] and r['PrivateDataRetained']
before=r['BeforeUninstallFiles']
assert len(before)==8 and before==r['BackupFiles']==r['RestoredFilesBeforeActivation']==r['AfterRestoredReadbackFiles']==r['AfterAllReadbacksFiles']
for relative,value in before.items():
    assert sha(out/'external-backup'/relative)==value
    assert sha(out/'package-private-data'/relative)==value
assert read(out/'external-backup/backup-hashes.json')==before
for key in ['FirstRegistrationRemoved','PrivateProofDataRemovedByWindows','ExternalBackupSurvivedUninstall','FreshPrivateStateBeforeRestore']:
    assert r[key],key
base=r['FirstInstalledPackage']; reinstall=r['ReinstalledPackage']
assert base==reinstall
assert not base['IsDevelopmentMode'] and base['Status']=='Ok'
assert len(r['Phases'])==15
expected=bytearray((i*37+11)&255 for i in range(2049));expected[509:541]=bytes(255-i for i in range(32))
counts={}; observations=[]; ids=[]
for phase in r['Phases']:
    stage=phase['ReinstallStage'];name=phase['phase'];priority=name.startswith('priority-');protected=name=='protected'
    counts[stage]=counts.get(stage,0)+1; ids.append(phase['HostProcess']['Id'])
    install=base
    assert phase['passed'] and phase['windowOpened'] and phase['completedWindowSession'] and not phase['elevated'] and not phase['hostDosCalls']
    assert phase['HostProcess']['ExitCode']==0 and phase['packageFullName']==install['FullName']
    if stage=='reinstalled-writes': assert phase['scenario']=='post-restore' and 'post-restore-' in phase['userDataDirectory']
    binaryRoot=args.package_layout
    for binary in phase['binaries']: assert sha(binaryRoot/Path(binary['path']).name)==binary['sha256']
    saved='425D4D57271143F1B438014003E25C07BE0B95A2B9F8611C7F295C9B93F286DE' if protected else '10755D5522B1FF637DD937C6EA3E5D4B10B6A157DAB43B4A23190D59B1085617' if priority else r['FixtureSha256']
    assert phase['liveSha256']==phase['savedSha256AfterWindowClose']==saved and not phase['hostSaveAdvancedCycles']
    if not protected: assert phase['guestFlushBeforeHostSave']
    assert [obs['snapshot'] for obs in phase['observations']]==(['-live','-reset'] if name=='write' else ['-live'])
    for obs in phase['observations']:
        stem=out/stage/(name+obs['snapshot']);chip=stem.with_suffix('.chipram').read_bytes();slow=stem.with_suffix('.slowram').read_bytes()
        def mem(a,n=4):
            if 0<=a and a+n<=len(chip): return chip[a:a+n]
            assert 0xC00000<=a and a-0xC00000+n<=len(slow),(a,n)
            return slow[a-0xC00000:a-0xC00000+n]
        def word(a): return int.from_bytes(mem(a),'big')
        assert sha(stem.with_suffix('.hdf'))==saved and stem.with_suffix('.png').stat().st_size>0
        if protected: assert word(0xB80)==0x52444641 and word(0xB84)==214
        else:
            expectedPhase=1 if name.endswith('write') and obs['snapshot']=='-live' else 2
            assert obs['phase']==expectedPhase
            assert word(0xB80)==0x52444231 and word(0xB88)==(1 if priority else 2)
            assert word(0xB8C)!=0 and word(0xB8C)!=word(0xB90)
            requests=[]
            for part in range(2):
                assert word(0xB98+part*4)==expectedPhase
                assert mem(word(0xBA0+part*4),2049)==expected
                port=word(0xBC0+part*8);assert port==word(0xB8C+part*4) and word(0xBC4+part*8)!=0
                request=word(word(port-92+88)+4);requests.append(request)
                assert word(request+28)&0xFFFF00FF==4<<16 and word(request+32)==0
            assert requests[0]!=requests[1]
            assert word(word(0x840)+20)==0  # No DF0 bootstrap.
        observations.append({'stage':stage,'phase':name,'snapshot':obs['snapshot'],'rawReceiptVerified':True,'sha256':saved})
assert counts=={'before-uninstall':4,'restored-readback':4,'reinstalled-writes':7} and len(observations)==17
for directory in (out/'package-private-data').iterdir():
    if not directory.is_dir(): continue
    for name in ['profile.json','last-machine.json']:
        profile=read(directory/name);drives=profile['HardDrives']
        assert len(drives)==1 and drives[0]['Mode'].lower()=='rdb' and not drives[0].get('Partition')
        assert drives[0]['ReadOnly']==(directory.name.endswith('fresh') or directory.name.endswith('protected'))
assert len([d for d in (out/'package-private-data').iterdir() if d.is_dir()])==9
result={'protocol':'copperscreen-installed-rdb-reinstall-audit-v1','passed':True,'fullRomPhases':15,'resetRomBoots':17,'stageCounts':counts,'processIds':ids,'allEightPersistedFilesUnchanged':True,'bothBootSelectionsBeforeAndAfterRestore':True,'newWritesInReinstalledApp':True,'freshProtectedReadbacks':4,'rawObservations':observations}
(out/'completion-audit.json').write_text(json.dumps(result,indent=2)+'\n')
print(json.dumps({'passed':True,'phases':15,'boots':17,'persistedFiles':8,'retainedGroups':9},indent=2))
