"""Prepare unchanged small MUI distributions on pinned private A/B images."""
import argparse, json, shutil, sys
from pathlib import Path
import lhafile
from amitools.fs.ADFSVolume import ADFSVolume
from amitools.fs.blkdev.BlkDevFactory import BlkDevFactory
from prepare_mui_calc import put
from refresh_utility_gui import inventory, sha
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('output',type=Path)
parser.add_argument('--library',type=Path)
parser.add_argument('--utilities',nargs='+')
parser.add_argument('--menu-witness',action='store_true')
parser.add_argument('--icon-write-fixture',action='store_true')
parser.add_argument('--join-write-fixture',action='store_true')
parser.add_argument('--fixture-setup-only',action='store_true')
parser.add_argument('--respect-icon-stack',action='store_true',help='Set the CLI default to the unchanged executable icon StackSize before launching it')
args=parser.parse_args()
if args.fixture_setup_only and not (args.icon_write_fixture or args.join_write_fixture):
    parser.error('--fixture-setup-only requires a file fixture')
if args.fixture_setup_only and args.respect_icon_stack:
    parser.error('Icon stack applies to an actual utility launch, not setup-only probes')
root=args.output; root.mkdir(parents=True,exist_ok=True)
library=None
if args.library:
    library=args.library.read_bytes()
    receipt=json.loads(Path(str(args.library)+'.json').read_text())
    assert receipt['Cpu']=='68000' and receipt['Name']=='muimaster.library'
    assert sha(library)==receipt['Sha256'].lower()

manifest=json.loads(Path('scripts/qualification/amiga-utilities-v1.json').read_text())
tools=[('mui-mem-11','MUImem.lha','MUImem/MUImem'),('mui-join-10','MUIJoin10.lha','MUIJoin/MUIJoin'),('mui-menu-100','MUIMenu.lha','MUIMenu/MUIMenu'),('mui-icon-update-12','MUI_IconUpd12.lha','MUI_IconUpdate')]
bases=[('copperstart','mui-calc-copperos-ffs-v50-backfill.hdf','4473c347e20345e0f13171876b0147bbd63c4cb00cd79a5e19a21ab63b084695'),('kickstart31','mui-calc-copperos-ofs-v48.hdf','e1733f3cf0922dc1d1f10f9b10de37e16b2f486d108811a1d7e3d0a03a10e799')]
for id,archive,exe in tools:
    if args.utilities and id not in args.utilities: continue
    app=next(a for a in manifest['applications'] if a['id']==id)
    src=Path('media/utilities/originals')/archive
    asset=next(a for a in app['assets'] if a['file'].endswith(archive))
    assert sha(src.read_bytes())==asset['sha256']
    files={}
    for entry in lhafile.Lhafile(str(src)).infolist():
        files['Utilities/'+entry.filename.replace('\\','/')]=lhafile.Lhafile(str(src)).read(entry.filename)
    launch_stack=None
    if args.respect_icon_stack:
        icon=files['Utilities/'+exe+'.info']
        assert len(icon)>=78 and icon[:2]==b'\xe3\x10' and icon[48]==3
        stack_bytes=int.from_bytes(icon[74:78],'big')
        assert 4096<=stack_bytes<=1024*1024 and stack_bytes%4==0
        launch_stack=dict(protocol='original-diskobject-stack-v1',bytes=stack_bytes,
                          originalIconSha256=sha(icon),guestPreference='public cli_DefaultStack, in longwords')
    if library is not None: files['Libs/muimaster.library']=library
    for rom,base,expected in bases:
        base=Path('artifacts/utility-mui-2026-10-31')/base
        assert sha(base.read_bytes())==expected
        out=root/(id+'-'+rom+'.hdf')
        if out.exists(): raise FileExistsError(out)
        writes=dict(files)
        prefix='C:Assign ENV: RAM:\nC:Assign T: RAM:\n' if rom=='kickstart31' else ''
        if id=='mui-mem-11' and rom=='copperstart': prefix+='C:CopperFfp\n'
        writes['S/Startup-Sequence']=(prefix+'C:CopperGui\nSYS:Utilities/'+exe+'\n').encode('ascii')
        if id=='mui-icon-update-12' and args.icon_write_fixture:
            from icon_update_fixture import files as icon_files, COPY_STARTUP
            writes.update(icon_files())
            writes['S/Startup-Sequence']=(prefix+'C:CopperGui\n').encode('ascii')+COPY_STARTUP+('SYS:Utilities/'+exe+'\nC:IconAfter\n').encode('ascii')
            if args.fixture_setup_only:
                from native_fixture_copy import wait_command
                writes['C/FixtureWait']=wait_command()
                writes['S/Startup-Sequence']=(prefix+'C:CopperGui\n').encode('ascii')+COPY_STARTUP+b'C:FixtureWait\n'
        if id=='mui-join-10' and args.join_write_fixture:
            from mui_join_fixture import files as join_files, COPY_STARTUP
            writes.update(join_files())
            writes['S/Startup-Sequence']=(prefix+'C:CopperGui\n').encode('ascii')+COPY_STARTUP+('SYS:Utilities/'+exe+'\nC:JoinAfter\n').encode('ascii')
            if args.fixture_setup_only:
                from native_fixture_copy import wait_command
                writes['C/FixtureWait']=wait_command()
                writes['S/Startup-Sequence']=(prefix+'C:CopperGui\n').encode('ascii')+COPY_STARTUP+b'C:FixtureWait\n'
        if id=='mui-menu-100':
            writes['S/MUIMenu.data']=b'MUIMenu Data File\nITEM\nTest Utility\nC:Echo\nMUI-MENU-FIXTURE\n0\nEND MUIMenu\n'
            if args.menu_witness:
                from menu_launch_witness import command
                writes['C/MenuWitness']=command()
                writes['S/MUIMenu.data']=b'MUIMenu Data File\nITEM\nTest Utility\nC:MenuWitness\nWITNESS\n0\nEND MUIMenu\n'
        if launch_stack is not None:
            from native_stack_preference import command as stack_command
            writes['C/UtilityStack']=stack_command(launch_stack['bytes'])
            launch=('SYS:Utilities/'+exe+'\n').encode('ascii')
            assert writes['S/Startup-Sequence'].count(launch)==1
            writes['S/Startup-Sequence']=writes['S/Startup-Sequence'].replace(launch,b'C:UtilityStack\n'+launch)
        shutil.copyfile(base,out)
        dev=BlkDevFactory().open(str(out)); vol=ADFSVolume(dev); vol.open()
        try:
            before=inventory(vol)
            for path,data in writes.items(): put(vol,path,data)
            after=inventory(vol)
            expectedfiles={k.casefold():v for k,v in before.items()}
            expectedfiles.update({k.casefold():sha(v) for k,v in writes.items()})
            assert {k.casefold():v for k,v in after.items()}==expectedfiles
        finally: vol.close(); dev.close()
        receipt=dict(protocol='small-mui-private-images-v2' if launch_stack else 'small-mui-private-images-v1',utility=id,rom=rom,launchStack=launch_stack,archiveSha256=sha(src.read_bytes()),baseSha256=expected,sha256=sha(out.read_bytes()),startup=writes['S/Startup-Sequence'].decode(),installedFiles=[dict(path=k,bytes=len(v),sha256=sha(v)) for k,v in writes.items()],allGuestFiles=after)
        Path(str(out)+'.json').write_text(json.dumps(receipt,indent=2)+'\n')
        print(out,receipt['sha256'])
