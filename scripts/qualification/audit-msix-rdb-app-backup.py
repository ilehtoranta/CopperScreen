from pathlib import Path
import json,hashlib,argparse,zipfile
parser=argparse.ArgumentParser(description="Audit retained installed RDB backup/restore evidence without running the emulator.")
parser.add_argument("evidence",type=Path)
parser.add_argument("--package-layout",type=Path,required=True)
args=parser.parse_args()
out=args.evidence.resolve()
def read(p): return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest().upper()
r=read(out/'installed-rdb-app-backup-record.json')
assert r['Status']=='PASS' and r['InstalledMsixAcceptance'] and r['InstalledAppBackupAcceptance']
assert r['OwnedRegistrationRemoved'] and r['PrivateDataRetained']
for key in ['FirstRegistrationRemoved','PrivateProofDataRemovedByWindows','ExternalBackupsSurvivedUninstall','FreshPrivateStateBeforeRestore']:
    assert r[key],key
assert r['SettingsExports']==2 and r['SettingsRestores']==2
backupResults=[]
for group in ['default','priority']:
    backup=out/'export'/(group+'.copperhdf')
    assert sha(backup)==r['BeforeUninstallBackupFiles'][backup.name]
    before=out/'before-uninstall-private-data'/('app-backup-'+group)
    after=out/'package-private-data'/('app-backup-'+group)
    with zipfile.ZipFile(backup) as archive:
        assert sorted(archive.namelist())==['disks/unit-0.hdf','manifest.json','profile.json']
        manifest=json.loads(archive.read('manifest.json')); profileBytes=archive.read('profile.json');disk=archive.read('disks/unit-0.hdf')
        original=json.loads(profileBytes)
        assert manifest['Version']==1 and manifest['ProfileSha256']==hashlib.sha256(profileBytes).hexdigest().upper()
        sourceProfile=read(before/'profile.json')
        if 'Media' not in sourceProfile and 'Media' in original:
            # Normal Settings materializes the existing default protection flags
            # for empty floppy slots. No floppy path or payload is added.
            assert original['Media']=={'Drives':[{'Index':i,'WriteProtected':True} for i in range(4)]}
            sourceProfile['Media']=original['Media']
        assert original==sourceProfile
        expected=r['FixtureSha256'] if group=='default' else '10755D5522B1FF637DD937C6EA3E5D4B10B6A157DAB43B4A23190D59B1085617'
        assert manifest['Images']==[{'Unit':0,'Bytes':len(disk),'Sha256':expected}]
        assert hashlib.sha256(disk).hexdigest().upper()==expected and disk==(before/'disk.hdf').read_bytes()
        restored=read(after/'last-machine.json')
        restores=list((after/'HardDiskRestores').iterdir());assert len(restores)==1
        assert (restores[0]/'unit-0.hdf').read_bytes()==disk
        assert read(restores[0]/'profile.json')==restored
        restoredPath=restored['HardDrives'][0]['Path']
        assert restoredPath!=original['HardDrives'][0]['Path'] and 'HardDiskRestores' in restoredPath
        original['HardDrives'][0]['Path']=restoredPath
        assert original==restored and not restored['HardDrives'][0]['ReadOnly']
        assert not (after/'disk.hdf').exists() and not (after/'profile.json').exists()
        backupResults.append({'layout':group,'backupSha256':sha(backup),'hdfSha256':expected,'allConfigurationFieldsPreservedExceptRebasedDiskPath':True})
base=r['FirstInstalledPackage']; reinstall=r['ReinstalledPackage']
assert base==reinstall
assert not base['IsDevelopmentMode'] and base['Status']=='Ok'
assert len(r['Phases'])==8
expected=bytearray((i*37+11)&255 for i in range(2049));expected[509:541]=bytes(255-i for i in range(32))
counts={}; observations=[]; ids=[]
for phase in r['Phases']:
    stage=phase['ReinstallStage'];name=phase['phase'];priority=name.startswith('priority-');protected=name=='protected'
    counts[stage]=counts.get(stage,0)+1; ids.append(phase['HostProcess']['Id'])
    install=base
    assert phase['passed'] and phase['windowOpened'] and phase['completedWindowSession'] and not phase['elevated'] and not phase['hostDosCalls']
    assert phase['HostProcess']['ExitCode']==0 and phase['packageFullName']==install['FullName']
    assert phase['scenario']=='app-backup'
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
assert counts=={'export':4,'restore':4} and len(observations)==9
result={'protocol':'copperscreen-installed-rdb-app-backup-audit-v1','passed':True,'fullRomPhases':8,'resetRomBoots':9,'stageCounts':counts,'processIds':ids,'bothBootSelectionsBeforeAndAfterRestore':True,'backups':backupResults,'rawObservations':observations}
(out/'completion-audit.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'passed':True,'phases':8,'boots':9,'appSettingsExports':2,'appSettingsRestores':2,'completeHdfAndConfigurationMatches':2},indent=2))
