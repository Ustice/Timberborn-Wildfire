"""Run the actual compiled native simulator's complete snapshot lifecycle in licensed Unity; no game or deployment."""
import argparse,hashlib,json,os,shutil,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
USER_HOME=Path.home()

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output',type=Path,required=True)
    args=parser.parse_args()
    output=args.output.resolve();output.mkdir(parents=True,exist_ok=False)
    processes=subprocess.check_output(['ps','-axo','pid=,comm='],text=True)
    for line in processes.splitlines():
        if any(name in line for name in ['/Unity.app/Contents/MacOS/Unity','/Timberborn.app/Contents/MacOS/Timberborn','/Blender.app/Contents/MacOS/Blender']):
            raise RuntimeError('Existing engine process: '+line)
    lock=USER_HOME/'Library/Application Support/Timberborn/WildfireQA/locks/build-deploy.lock'
    lock.mkdir()
    (lock/'lock.json').write_text(json.dumps({'pid':os.getpid(),'controller':'/root/baseline_audit','task':'native complete snapshot probe','output':str(output)}))
    try:
        with (output/'build.log').open('w') as log:
            subprocess.run(['dotnet','build','src/Wildfire.Timberborn/Wildfire.Timberborn.csproj','--configuration','Release'],cwd=ROOT,stdout=log,stderr=subprocess.STDOUT,check=True)
        project=output/'UnityProject'
        for folder in ['Packages','ProjectSettings']:
            shutil.copytree(ROOT/'src/Wildfire.Unity/UnityBatchmodeProject'/folder,project/folder)
        plugins=project/'Assets/Plugins';plugins.mkdir(parents=True)
        # Only Core and the JSON serializer enter Unity's eager plugin import graph.
        # The actual native DLL is loaded lazily by the probe; no incompatible game-engine DLLs are imported.
        managed=USER_HOME/'Library/Application Support/Steam/steamapps/common/Timberborn/Timberborn.app/Contents/Resources/Data/Managed'
        shutil.copy2(managed/'Newtonsoft.Json.dll',plugins)
        shutil.copy2(ROOT/'src/Wildfire.Core/bin/Release/netstandard2.1/Wildfire.Core.dll',plugins)
        native=output/'Wildfire.Timberborn.dll'
        shutil.copy2(ROOT/'src/Wildfire.Timberborn/bin/Release/netstandard2.1/Wildfire.Timberborn.dll',native)
        (output/'assembly-sha256.json').write_text(json.dumps({str(file.relative_to(output)):hashlib.sha256(file.read_bytes()).hexdigest()
            for file in [native,*plugins.glob('*.dll')]},indent=2))
        editor=project/'Assets/Editor';editor.mkdir()
        shutil.copy2(Path(__file__).with_name('MaterialSnapshotProbe.cs'),editor)
        shutil.copy2(ROOT/'src/Wildfire.Unity/FireSim.compute',project/'Assets/FireSim.compute')
        (output/'revision.txt').write_text(subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True))
        (output/'source-status.txt').write_text(subprocess.check_output(['git','status','--short'],cwd=ROOT,text=True))
        (output/'probe-shader-sha256.json').write_text(json.dumps({str(file.relative_to(project)):hashlib.sha256(file.read_bytes()).hexdigest()
            for file in [editor/'MaterialSnapshotProbe.cs',project/'Assets/FireSim.compute']},indent=2))
        unity='/Applications/Unity/Hub/Editor/6000.3.6f1/Unity.app/Contents/MacOS/Unity'
        cmd=[unity,'-disable-assembly-updater','-batchmode','-quit','-projectPath',str(project),'-executeMethod','MaterialSnapshotProbe.Run','-logFile',str(output/'unity.log')]
        with (output/'stdout.log').open('w') as log:
            subprocess.run(cmd,env=dict(os.environ,WILDFIRE_SNAPSHOT_PROBE_OUTPUT=str(output),WILDFIRE_SNAPSHOT_PROBE_NATIVE=str(native)),stdout=log,stderr=subprocess.STDOUT,timeout=300,check=True)
        for marker in ['WILDFIRE_MATERIAL_SNAPSHOT_PROBE_PASS','WILDFIRE_GPU_EXHAUSTION_RESTORE_PASS','WILDFIRE_FIRST_SLOT_ACTIVATION_PASS']:
            if marker not in (output/'unity.log').read_text(): raise RuntimeError('Missing actual probe pass marker: '+marker)
        print('PASS '+str(output))
    finally: shutil.rmtree(lock)

if __name__=='__main__':main()
