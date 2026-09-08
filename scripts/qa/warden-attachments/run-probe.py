"""Run the production Warden provider source in an isolated licensed Unity editor.

No game launch or deployment. Uses installed game assemblies only as test inputs;
none are copied into the repository or player package. The shared build lock
serializes this probe with bundle/deploy operations. See the adjacent README.
"""
import argparse
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[3]
HOME = Path.home()
NATIVE = ('Timberborn.AssetSystem', 'Timberborn.Timbermesh', 'Bindito.Core',
    'Timberborn.TimbermeshDTO', 'Timberborn.BlueprintSystem', 'Timberborn.Common',
    'Timberborn.BlueprintPrefabSystem', 'protobuf-net', 'protobuf-net.Core',
    'Timberborn.SerializationSystem', 'Timberborn.SingletonSystem',
    'Timberborn.FeatureToggleSystem', 'Bindito.Unity', 'Newtonsoft.Json',
    'Timberborn.CommandLine', 'System.Collections.Immutable',
    'System.Runtime.CompilerServices.Unsafe')


def run(command, log, **kwargs):
    with log.open('w') as output:
        subprocess.run(command, stdout=output, stderr=subprocess.STDOUT, check=True, **kwargs)


def prepare(output, managed):
    project = output / 'UnityProject'
    source = ROOT / 'src/Wildfire.Unity/UnityBatchmodeProject'
    for folder in ('Packages', 'ProjectSettings'):
        shutil.copytree(source / folder, project / folder)
    plugins = project / 'Assets/Plugins'
    plugins.mkdir(parents=True)
    for name in NATIVE:
        shutil.copy2(managed / (name + '.dll'), plugins)
    editor = project / 'Assets/Editor'
    editor.mkdir()
    shutil.copy2(Path(__file__).with_name('WardenAttachmentProbe.cs'), editor)

    # Compile the actual provider and configurator, not a Unity-compatible rewrite.
    compile_dir = output / 'Compile'
    compile_dir.mkdir()
    csproj = ET.Element('Project', Sdk='Microsoft.NET.Sdk')
    props = ET.SubElement(csproj, 'PropertyGroup')
    for key, value in {'TargetFramework': 'netstandard2.1', 'LangVersion': '10.0',
            'Nullable': 'enable', 'ImplicitUsings': 'enable',
            'AssemblyName': 'Wildfire.WardenAttachmentPrototype'}.items():
        ET.SubElement(props, key).text = value
    items = ET.SubElement(csproj, 'ItemGroup')
    for name in ('Timberborn.AssetSystem', 'Timberborn.Timbermesh', 'Bindito.Core', 'UnityEngine.CoreModule'):
        reference = ET.SubElement(items, 'Reference', Include=name)
        ET.SubElement(reference, 'HintPath').text = str(managed / (name + '.dll'))
        ET.SubElement(reference, 'Private').text = 'false'
    for name in ('WardenAttachmentAssetProvider.cs', 'WardenAttachmentAssetConfigurator.cs'):
        ET.SubElement(items, 'Compile', Include=str(ROOT / 'src/Wildfire.Timberborn/FireResponse/Presentation' / name))
    path = compile_dir / 'Provider.csproj'
    ET.ElementTree(csproj).write(path, encoding='unicode')
    run(['dotnet', 'build', str(path), '--configuration', 'Release', '-v', 'quiet'], output / 'compile.log')
    shutil.copy2(compile_dir / 'bin/Release/netstandard2.1/Wildfire.WardenAttachmentPrototype.dll', plugins)
    return project


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--managed', type=Path, default=HOME / 'Library/Application Support/Steam/steamapps/common/Timberborn/Timberborn.app/Contents/Resources/Data/Managed')
    parser.add_argument('--unity', type=Path, default=Path(os.environ.get('WILDFIRE_UNITY_EXECUTABLE', '/Applications/Unity/Hub/Editor/6000.3.6f1/Unity.app/Contents/MacOS/Unity')))
    args = parser.parse_args()
    for file in [args.unity, *(args.managed / (name + '.dll') for name in NATIVE)]:
        if not file.is_file():
            parser.error('Missing installed dependency: ' + str(file))
    lock = HOME / 'Library/Application Support/Timberborn/WildfireQA/locks/build-deploy.lock'
    lock.parent.mkdir(parents=True, exist_ok=True)
    # Never steal or resolve another controller's lock automatically.
    lock.mkdir()
    output = Path(tempfile.mkdtemp(prefix='wildfire-warden-attachment-probe-'))
    try:
        (lock / 'lock.json').write_text(json.dumps({'pid': os.getpid(), 'task': 'Warden attachment Unity probe', 'output': str(output)}) + '\n')
        print('Warden attachment probe evidence: ' + str(output), flush=True)
        project = prepare(output, args.managed)
        env = dict(os.environ, WILDFIRE_GEAR_SOURCE_ROOT=str(ROOT))
        run([str(args.unity), '-batchmode', '-projectPath', str(project),
            '-executeMethod', 'WardenAttachmentProbe.Run', '-logFile', str(output / 'unity.log')],
            output / 'process.log', env=env, timeout=180)
        if 'WILDFIRE_WARDEN_ATTACHMENT_PROBE_PASS' not in (output / 'unity.log').read_text():
            raise RuntimeError('Unity exited without a passing probe marker; inspect ' + str(output))
        print('PASS: native wrappers, importer geometry, cache reset; native atlases and character fit remain untested.')
    finally:
        shutil.rmtree(lock)


if __name__ == '__main__':
    main()
