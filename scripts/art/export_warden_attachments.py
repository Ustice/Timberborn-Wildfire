"""Official Blender export of split Warden source parts; existing art is untouched.

/Applications/Blender.app/Contents/MacOS/Blender --background --python scripts/art/export_warden_attachments.py
Requires the same installed official timbermesh exporter as export_fire_response.py.
"""
import json
from pathlib import Path
import sys
import zlib
import bpy

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[2]
PLUGIN = Path.home() / 'repos/timbermesh/src/timbermesh_blender_plugin'
sys.path.insert(0, str(PLUGIN))
sys.path.insert(0, str(Path(__file__).resolve().parent))
from timbermesh_exporter import Exporter, ExportSettings
from native_materials import IRON, assign_native_materials
import model_pb2

mapping = {key: ('BaseMetal.IronTeeth' if value == 'BaseMetal.Folktails' else value)
           for key, value in IRON.items()}
output = ROOT / 'src/Wildfire.Timberborn/Data/Equipment/FireResponse'
output.mkdir(parents=True, exist_ok=True)
report = []
for name in ('WardenTank', 'WardenWand', 'WardenHose'):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(ROOT / 'art/fire-response/attachments' / (name + '.glb')))
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH']
    for obj in meshes:
        assign_native_materials(obj, mapping)
    bpy.context.view_layer.update()
    target = output / (name + '.IronTeeth.timbermesh')
    # Keep source origin; attachment wrappers, not source geometry, will own fitting.
    Exporter.export_collection(bpy.context.scene.collection, str(target),
        ExportSettings(bpy.context, True, False, False))
    model = model_pb2.Model()
    model.ParseFromString(zlib.decompress(target.read_bytes()))
    materials = sorted({mesh.material for node in model.nodes for mesh in node.meshes})
    assert materials and set(materials) <= set(mapping.values())
    assert all(not node.vertexAnimations and not node.nodeAnimations for node in model.nodes)
    triangles = sum(len(mesh.indices) // 3 for node in model.nodes for mesh in node.meshes)
    report.append({'model': name, 'triangles': triangles, 'materials': materials})
assert [part['triangles'] for part in report] == [3868, 88, 252]
print('WILDFIRE_WARDEN_NATIVE_EXPORT ' + json.dumps(report))
