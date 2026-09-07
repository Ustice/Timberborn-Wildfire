"""Partition the original review GLB without reauthoring or converting its geometry.

Run with Python 3. Outputs are source art, not loaded by the player mod. Buffer
views are copied byte-for-byte; only resource indices and scene membership change.
"""
import copy
import hashlib
import json
from pathlib import Path
import struct

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'art/fire-response/WardenSprayer.glb'
OUTPUT = ROOT / 'art/fire-response/attachments'
GROUPS = {
    'WardenTank': {'Backpack tank', 'Backplate', 'Dial needle', 'Filler cap',
        'Iron rivet.013', 'Iron rivet.014', 'Iron rivet.015', 'Iron rivet.016',
        'Pressure dial', 'Pump lever', 'Pump riser', 'Shoulder strap',
        'Shoulder strap.001', 'Strap buckle', 'Strap buckle.001', 'Tank binding',
        'Tank binding.001', 'Valve handwheel.001', 'Valve spoke.002', 'Valve spoke.003'},
    'WardenWand': {'Spray wand', 'Wand grip'},
    'WardenHose': {'Flexible hose'},
}


def read_glb(path):
    data = path.read_bytes()
    magic, version, size = struct.unpack_from('<4sII', data)
    assert magic == b'glTF' and version == 2 and size == len(data)
    json_size, kind = struct.unpack_from('<II', data, 12)
    assert kind == 0x4E4F534A
    document = json.loads(data[20:20 + json_size])
    binary_size, kind = struct.unpack_from('<II', data, 20 + json_size)
    assert kind == 0x004E4942
    binary = data[28 + json_size:]
    assert len(binary) == binary_size
    return document, binary


def texture_slots(material):
    for key in ('normalTexture', 'occlusionTexture', 'emissiveTexture'):
        if key in material:
            yield material[key]
    pbr = material.get('pbrMetallicRoughness', {})
    for key in ('baseColorTexture', 'metallicRoughnessTexture'):
        if key in pbr:
            yield pbr[key]


def subset(source, binary, name, names):
    result = copy.deepcopy(source)
    nodes = [copy.deepcopy(n) for n in source['nodes'] if n['name'] in names]
    assert {n['name'] for n in nodes} == names
    assert all('mesh' in n and 'children' not in n for n in nodes)
    # The original scene is one identity parent plus independent mesh nodes.
    parent = copy.deepcopy(source['nodes'][source['scenes'][source['scene']]['nodes'][0]])
    assert set(parent) == {'name', 'children'}
    parent.update(name=name, children=list(range(len(nodes))))
    result['nodes'] = nodes + [parent]
    result['scenes'] = [{'name': name, 'nodes': [len(nodes)]}]
    result['scene'] = 0

    def select(table, ids):
        ids = sorted(set(ids))
        result[table] = [copy.deepcopy(source[table][i]) for i in ids]
        return {old: new for new, old in enumerate(ids)}

    mesh_map = select('meshes', (n['mesh'] for n in nodes))
    for node in nodes:
        node['mesh'] = mesh_map[node['mesh']]
    primitives = [p for m in result['meshes'] for p in m['primitives']]
    assert all(set(p) <= {'attributes', 'indices', 'material', 'mode'} for p in primitives)
    assert all(p.get('mode', 4) == 4 for p in primitives)
    accessor_map = select('accessors', [i for p in primitives for i in [p['indices'], *p['attributes'].values()]])
    material_map = select('materials', (p['material'] for p in primitives))
    for primitive in primitives:
        primitive['indices'] = accessor_map[primitive['indices']]
        primitive['attributes'] = {k: accessor_map[v] for k, v in primitive['attributes'].items()}
        primitive['material'] = material_map[primitive['material']]
    slots = [slot for material in result['materials'] for slot in texture_slots(material)]
    texture_map = select('textures', (slot['index'] for slot in slots))
    for slot in slots:
        slot['index'] = texture_map[slot['index']]
    image_map = select('images', (t['source'] for t in result['textures']))
    sampler_map = select('samplers', (t['sampler'] for t in result['textures'] if 'sampler' in t))
    for texture in result['textures']:
        texture['source'] = image_map[texture['source']]
        if 'sampler' in texture:
            texture['sampler'] = sampler_map[texture['sampler']]
    assert all('sparse' not in a for a in result['accessors'])
    resources = result['accessors'] + result['images']
    view_map = select('bufferViews', (r['bufferView'] for r in resources))
    for resource in resources:
        resource['bufferView'] = view_map[resource['bufferView']]
    packed = bytearray()
    for view in result['bufferViews']:
        assert view['buffer'] == 0
        original = binary[view.get('byteOffset', 0):view.get('byteOffset', 0) + view['byteLength']]
        assert len(original) == view['byteLength']
        packed.extend(b'\0' * (-len(packed) % 4))
        view['byteOffset'] = len(packed)
        packed.extend(original)
        assert packed[view['byteOffset']:view['byteOffset'] + view['byteLength']] == original
    result['buffers'] = [{'byteLength': len(packed)}]
    return result, bytes(packed)


def write_glb(path, document, binary):
    # Empty optional tables are omitted for glTF conformance.
    document = {k: v for k, v in document.items() if not isinstance(v, list) or v}
    encoded = json.dumps(document, separators=(',', ':')).encode()
    encoded += b' ' * (-len(encoded) % 4)
    binary += b'\0' * (-len(binary) % 4)
    content = (struct.pack('<4sII', b'glTF', 2, 28 + len(encoded) + len(binary))
        + struct.pack('<II', len(encoded), 0x4E4F534A) + encoded
        + struct.pack('<II', len(binary), 0x004E4942) + binary)
    path.write_bytes(content)
    return hashlib.sha256(content).hexdigest()


def triangles(document):
    return sum(document['accessors'][p['indices']]['count'] // 3
        for mesh in document['meshes'] for p in mesh['primitives'])


def main():
    source, binary = read_glb(SOURCE)
    assert not any(k in source for k in ('animations', 'skins', 'extensionsUsed', 'extensionsRequired'))
    all_names = [name for names in GROUPS.values() for name in names]
    assert len(all_names) == len(set(all_names)), 'Groups overlap'
    assert set(all_names) == {n['name'] for n in source['nodes'] if 'mesh' in n}, 'Source membership changed'
    OUTPUT.mkdir(exist_ok=True)
    report = {'source': str(SOURCE.relative_to(ROOT)),
        'sourceSha256': hashlib.sha256(SOURCE.read_bytes()).hexdigest(),
        'coordinateSystem': 'Original glTF Y-up; original node transforms retained; attachment fit not established',
        'parts': []}
    for name, names in GROUPS.items():
        document, packed = subset(source, binary, name, names)
        path = OUTPUT / (name + '.glb')
        digest = write_glb(path, document, packed)
        reopened, _ = read_glb(path)
        assert triangles(reopened) == triangles(document)
        report['parts'].append({'file': path.name, 'sha256': digest,
            'sourceObjects': sorted(names), 'triangles': triangles(document)})
    assert sum(p['triangles'] for p in report['parts']) == triangles(source)
    (OUTPUT / 'provenance.json').write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps(report, indent=2))


if __name__ == '__main__':
    main()
