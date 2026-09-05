"""Native atlas assignments and deterministic component UVs for art review.

Regions were inspected in the installed 1.1 atlases. No game textures are copied
into the mod. Coordinates below use Blender's bottom-left UV origin.
"""
import bpy

FOLK = {
    'wood': 'BaseWood_Brown.Folktails', 'plank': 'BaseWood_LightBrown.Folktails',
    'pale': 'BaseWood_LightBrown.Folktails', 'edge': 'BaseWood_Brown.Folktails',
    'thatch': 'ThatchedRoof.Folktails', 'rope': 'Paper.IronTeeth',
    'brass': 'BaseMetal.Folktails', 'metal': 'BaseMetal.Folktails',
    'cloth': 'Paper.IronTeeth', 'stone': 'Plaster_White.IronTeeth', 'earth': 'DirtCommon',
}
IRON = {key: 'BaseWood_DarkBrown.IronTeeth' for key in ('wood', 'plank', 'darkwood', 'edge')}
IRON.update({key: 'BaseMetal.IronTeeth' for key in ('metal', 'iron', 'glass')})
IRON.update({key: 'Paper.IronTeeth' for key in ('rope',)})
IRON.update(earth='DirtCommon', stone='Plaster_White.IronTeeth', greenroof='RoofPlanks.IronTeeth',
            canvas='Details.IronTeeth', cloth='Details.IronTeeth', uniform='Details.IronTeeth',
            red='Plaster_Orange.IronTeeth', rust='Plaster_Orange.IronTeeth', brass='BaseMetal.Folktails')


def assign_native_materials(obj, mapping):
    mesh = obj.data
    uv = mesh.uv_layers.active or mesh.uv_layers.new(name='NativeAtlas')
    low = [min(v.co[i] for v in mesh.vertices) for i in range(3)]
    span = [max(v.co[i] for v in mesh.vertices) - low[i] for i in range(3)]
    sources = [slot.material.name for slot in obj.material_slots]
    for face in mesh.polygons:
        source = sources[face.material_index]
        native = mapping[source]  # Missing authoring materials must fail the export.
        normal_axis = max(range(3), key=lambda i: abs(face.normal[i]))
        axes = sorted((i for i in range(3) if i != normal_axis), key=lambda i: span[i])
        across, along = axes
        # Project per component, not per triangle: adjacent triangles share UVs.
        for index in face.loop_indices:
            co = mesh.vertices[mesh.loops[index].vertex_index].co
            a = (co[across] - low[across]) / max(span[across], 1e-6)
            b = (co[along] - low[along]) / max(span[along], 1e-6)
            if native.startswith('BaseWood'):
                # Interior of one long plank, clear of pale gutters/end-grain motifs.
                u = .529 + a * .018
                v = .56 + b * min(.39, span[along] * .20)
                if span[across] > .35:
                    # Broad panels use the fine-grain region, not a stretched plank.
                    u = .04 + a * .39
            elif native.startswith('BaseMetal'):
                # Flat sheet interior: exclude embossed circles, strips and borders.
                u, v = .06 + a * .36, .57 + b * .36
            elif native.startswith('Details'):
                if source == 'uniform':
                    # Plain navy fabric beside the faction banner emblem.
                    u, v = .018 + a * .05, .59 + b * .33
                else:
                    # Sack cloth interior, excluding ties, folds and outline.
                    u, v = .57 + a * .12, .56 + b * .18
            elif native == 'Plaster_Orange.IronTeeth':
                # Fine worn finish, avoiding large plaster pockmarks.
                u, v = .12 + a * .10, .75 + b * .10
            elif native.startswith('Paper'):
                # Plain canvas-like paper interior; avoids illustrated sheet seams.
                u, v = .58 + a * .32, .08 + b * .32
            elif native.startswith('RoofPlanks'):
                # One shingle course per hip panel; keep raised battens inside
                # a green plank so atlas crossbars cannot run up their sides.
                if obj.name.startswith('Roof sheathing'):
                    u, v = .03 + b * .94, .82 + a * .15
                else:
                    u, v = .33 + a * .025, .82 + b * .15
            elif native == 'DirtCommon':
                u, v = .05 + a * .90, .05 + b * .90
            else:
                # Full-field thatch and plaster, inset from the atlas boundary.
                u, v = .05 + a * .90, .05 + b * .90
            assert 0 <= u <= 1 and 0 <= v <= 1, (obj.name, native, u, v)
            uv.data[index].uv = (u, v)
    for slot, source in zip(obj.material_slots, sources):
        native = mapping[source]
        slot.material = bpy.data.materials.get(native) or bpy.data.materials.new(native)
