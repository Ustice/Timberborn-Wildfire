"""Run the actual compiled native simulator's complete snapshot lifecycle in licensed Unity; no game or deployment."""
import argparse,json,os,shutil,subprocess,tempfile,datetime
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
HOME=Path.home()

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output',type=Path,required=True)
    args=parser.parse_args()
    output=args.output.resolve();output.mkdir(parents=True,exist_ok=False)
    processes=subprocess.check_output(['ps','-axo','pid=,comm='],text=True)
    for line in processes.splitlines():
        if any(name in line for name in ['/Unity.app/Contents/MacOS/Unity','/Timberborn.app/Contents/MacOS/Timberborn','/Blender.app/Contents/MacOS/Blender']):
            raise RuntimeError('Existing engine process: '+line)
    lock=HOME/'Library/Application Support/Timberborn/WildfireQA/locks/build-deploy.lock'
    lock.mkdir()
    (lock/'lock.json').write_text(json.dumps({'pid':os.getpid(),'controller':'/root/baseline_audit','task':'native complete snapshot probe','output':str(output)}))
    try:
        with (output/'build.log').open('w') as log:
            subprocess.run(['dotnet','build','src/Wildfire.Timberborn/Wildfire.Timberborn.csproj','--configuration','Release'],cwd=ROOT,stdout=log,stderr=subprocess.STDOUT,check=True)
        project=output/'UnityProject'
        for folder in ['Packages','ProjectSettings']:
            shutil.copytree(ROOT/'src/Wildfire.Unity/UnityBatchmodeProject'/folder,project/folder)
        plugins=project/'Assets/Plugins';plugins.mkdir(parents=True)
        managed=HOME/'Library/Application Support/Steam/steamapps/common/Timberborn/Timberborn.app/Contents/Resources/Data/Managed'
        # Test-only installed dependencies remain under /tmp; no shipping/source assets are copied back.
        selected=[]
        for dll in managed.glob('*.dll'):
            if dll.name.startswith(('Timberborn.','Bindito.')) or dll.name in ['Newtonsoft.Json.dll','System.Collections.Immutable.dll','System.Runtime.CompilerServices.Unsafe.dll','protobuf-net.dll','protobuf-net.Core.dll']:
                shutil.copy2(dll,plugins/dll.name);selected.append(dll.name)
        for name in ['Core','Timberborn']:
            shutil.copy2(ROOT/f'src/Wildfire.{name}/bin/Release/netstandard2.1/Wildfire.{name}.dll',plugins)
        editor=project/'Assets/Editor';editor.mkdir()
        shutil.copy2(Path(__file__).with_name('MaterialSnapshotProbe.cs'),editor)
        shutil.copy2(ROOT/'src/Wildfire.Unity/FireSim.compute',project/'Assets/FireSim.compute')
        (output/'installed-test-assemblies.json').write_text(json.dumps(sorted(selected),indent=2))
        (output/'revision.txt').write_text(subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True))
        unity='/Applications/Unity/Hub/Editor/6000.3.6f1/Unity.app/Contents/MacOS/Unity'
        cmd=[unity,'-batchmode','-quit','-projectPath',str(project),'-executeMethod','MaterialSnapshotProbe.Run','-logFile',str(output/'unity.log')]
        with (output/'stdout.log').open('w') as log:
            subprocess.run(cmd,env=dict(os.environ,WILDFIRE_SNAPSHOT_PROBE_OUTPUT=str(output)),stdout=log,stderr=subprocess.STDOUT,timeout=300,check=True)
        if 'WILDFIRE_MATERIAL_SNAPSHOT_PROBE_PASS' not in (output/'unity.log').read_text(): raise RuntimeError('Missing actual probe pass marker')
        print('PASS '+str(output))
    finally: shutil.rmtree(lock)

if __name__=='__main__':main()
