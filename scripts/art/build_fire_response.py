"""Build original, editable low-poly Wildfire fire-response assets with Blender."""
import bpy
import math
import json
from pathlib import Path
from mathutils import Vector

OUT = Path(__file__).resolve().parents[2] / 'art/fire-response'
OUT.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
COLORS = {'wood': (.32,.16,.065,1), 'plank': (.60,.36,.14,1),
          'edge': (.18,.09,.035,1), 'metal': (.18,.23,.25,1),
          'brass': (.65,.39,.105,1), 'cloth': (.40,.48,.26,1),
          'red': (.55,.12,.055,1), 'water': (.12,.43,.52,1),
          'earth': (.38,.30,.19,1), 'stone': (.38,.40,.36,1)}
MATS = {}
for name, color in COLORS.items():
    m = bpy.data.materials.new(name)
    m.diffuse_color = color
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = color
    bsdf.inputs['Roughness'].default_value = .7
    bsdf.inputs['Metallic'].default_value = .55 if name in ('metal','brass') else 0
    MATS[name] = m

root = None

def finish(obj, name, mat):
    obj.name = name
    obj.data.materials.append(MATS[mat])
    obj.parent = root
    return obj

def box(name, pos, size, mat='wood'):
    bpy.ops.mesh.primitive_cube_add(size=1, location=pos)
    obj = bpy.context.object
    obj.dimensions = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return finish(obj, name, mat)

def cylinder(name, pos, radius, depth, mat='metal', radius2=None):
    bpy.ops.mesh.primitive_cone_add(vertices=12, radius1=radius,
        radius2=radius if radius2 is None else radius2, depth=depth, location=pos)
    return finish(bpy.context.object, name, mat)

def beam(name, a, b, width, mat='wood'):
    a,b = Vector(a),Vector(b)
    obj = box(name, (a+b)/2, (width,width,(b-a).length),mat)
    obj.rotation_euler = (b-a).to_track_quat('Z','Y').to_euler()
    return obj

def tube(name, points, radius, mat='metal'):
    # Mesh rings along a polyline; no live curves or external textures in exports.
    verts, faces = [], []
    for i,p in enumerate(points):
        direction = Vector(points[min(i+1,len(points)-1)])-Vector(points[max(0,i-1)])
        q = direction.to_track_quat('Z','Y')
        for j in range(8):
            v = q @ Vector((radius*math.cos(j*math.tau/8),radius*math.sin(j*math.tau/8),0))
            verts.append(Vector(p)+v)
    for i in range(len(points)-1):
        for j in range(8):
            a=i*8+j; b=i*8+(j+1)%8
            faces.append((a,b,b+8,a+8))
    faces += [tuple(reversed(range(8))),tuple(range(len(verts)-8,len(verts)))]
    mesh=bpy.data.meshes.new(name); mesh.from_pydata(verts,[],faces); mesh.update()
    obj=bpy.data.objects.new(name,mesh); bpy.context.collection.objects.link(obj)
    return finish(obj,name,mat)

def ring(name, z, radius, width, mat='metal', center=(0,0)):
    pts=[(center[0]+radius*math.cos(i*math.tau/24),center[1]+radius*math.sin(i*math.tau/24),z) for i in range(25)]
    return tube(name,pts,width,mat)

def roof(width, depth, z, mat='plank'):
    for side in (-1,1):
        for i in range(7):
            x=side*(i+.5)*width/14
            obj=box('Roof shingle',(x,0,z+.40-abs(x)*.65),(width/14+.025,depth,.10),mat)
            obj.rotation_euler[1]=side*math.atan(.65)
    beam('Ridge', (0,-depth/2,z+.44),(0,depth/2,z+.44),.13,'edge')

def bucket():
    # Open stave vessel with a solid bottom, metal hoops, and a separate bail.
    for i in range(12):
        a=i*math.tau/12
        obj=box('Bucket stave',(.17*math.cos(a),.17*math.sin(a),.18),(.09,.035,.32),'plank')
        obj.rotation_euler[2]=a+math.pi/2
    cylinder('Bucket bottom',(0,0,.035),.185,.045,'wood')
    for z in (.07,.28): ring('Bucket hoop',z,.19,.018)
    tube('Carry bail',[(.20*math.cos(a),0,.32+.20*math.sin(a)) for a in [i*math.pi/16 for i in range(17)]],.016)

def bell():
    for x in (-.65,.65):
        for y in (-.45,.45): box('Foot',(x,y,.12),(.35,.35,.24),'stone')
        beam('Upright',(x,0,.1),(x,0,1.9),.19)
        beam('Brace',(x,0,1.20),(x*.35,0,1.85),.11,'plank')
    beam('Bell crossbar',(-.76,0,1.8),(.76,0,1.8),.20)
    cylinder('Bell crown',(0,0,1.52),.17,.3,'brass',.11)
    # Open flared bell profile.
    profiles=[(.13,1.66),(.16,1.49),(.20,1.34),(.32,1.22),(.32,1.17),(.28,1.17),(.17,1.34),(.12,1.49)]
    verts=[(r*math.cos(i*math.tau/24),r*math.sin(i*math.tau/24),z) for r,z in profiles for i in range(24)]
    faces=[(k*24+i,k*24+(i+1)%24,(k+1)*24+(i+1)%24,(k+1)*24+i) for k in range(len(profiles)-1) for i in range(24)]
    mesh=bpy.data.meshes.new('Bell'); mesh.from_pydata(verts,[],faces); mesh.update()
    obj=bpy.data.objects.new('Flared bell',mesh); bpy.context.collection.objects.link(obj); finish(obj,'Flared bell','brass')
    cylinder('Clapper',(0,0,1.25),.045,.32,'metal')
    tube('Pull rope',[(.08,.1,1.6),(.34,.12,1.4),(.45,.12,.45)],.02,'cloth')
    roof(1.75,1.2,1.93)
    box('Bucket shelf',(0,.40,.45),(1.4,.35,.12),'plank')

def station():
    box('Foundation',(0,0,.10),(2.8,2.0,.20),'stone')
    for x in (-1.2,1.2):
        for y in (-.8,.8): box('Steel post',(x,y,.95),(.14,.14,1.7),'metal')
    for z in (.5,.85,1.2,1.55):
        box('Back siding',(0,.85,z),(2.45,.10,.29),'wood')
        box('Side siding',(-1.25,0,z),(.10,1.6,.29),'wood')
    roof(2.85,2.05,1.8,'metal')
    box('Door lintel',(-.3,-.82,1.53),(1.5,.15,.16),'metal')
    box('Work bench',(.40,.38,.7),(1.3,.60,.12),'plank')
    for x in (-.1,.9): box('Bench leg',(x,.38,.4),(.09,.45,.6),'metal')
    cylinder('Water reservoir',(.84,-.12,.83),.33,1.25,'metal')
    for z in (.3,1.3): ring('Reservoir band',z,.34,.035,'brass',(.84,-.12))
    cylinder('Tank cap',(.84,-.12,1.49),.15,.08,'brass')
    tube('Outlet',[(.84,-.46,.44),(.84,-.65,.44),(.55,-.65,.44)],.05,'brass')
    box('Warden sign',(-.30,-.9,1.65),(.7,.08,.32),'red')
    for x in (-.46,-.14): box('Sign stripe',(x,-.95,1.65),(.06,.02,.23),'brass')

def sprayer():
    cylinder('Backpack tank',(0,0,.43),.23,.72,'metal')
    for z in (.13,.70): ring('Tank binding',z,.235,.028,'brass')
    cylinder('Filler cap',(0,0,.84),.09,.10,'brass')
    for x in (-.15,.15):
        tube('Shoulder strap',[(x,-.18,.68),(x,-.36,.75),(x,-.41,.30),(x,-.20,.16)],.036,'cloth')
    tube('Flexible hose',[(.2,0,.22),(.38,.05,.1),(.53,-.1,.1),(.57,-.33,.32),(.48,-.42,.52)],.024,'edge')
    beam('Spray wand',(.48,-.42,.45),(.48,-.8,.64),.06,'brass')
    beam('Wand grip',(.48,-.45,.49),(.48,-.48,.35),.07,'wood')

def helmet():
    # Ear clearances are visual proposals pending native character fitting.
    # Annular brim and open hemisphere: a head can enter from below.
    verts=[]
    for z in (.028,.072):
        for radius in (.205,.30):
            verts += [(radius*math.cos(i*math.tau/16),radius*math.sin(i*math.tau/16),z) for i in range(16)]
    faces=[]
    for i in range(16):
        j=(i+1)%16
        faces += [(i,j,16+j,16+i),(32+i,48+i,48+j,32+j),
                  (16+i,16+j,48+j,48+i),(i,32+i,32+j,j)]
    mesh=bpy.data.meshes.new('Open helmet brim');mesh.from_pydata(verts,[],faces);mesh.update()
    obj=bpy.data.objects.new('Helmet brim',mesh);bpy.context.collection.objects.link(obj);finish(obj,'Helmet brim','brass')
    bpy.ops.mesh.primitive_uv_sphere_add(segments=16,ring_count=8,radius=1,location=(0,0,.07))
    obj=bpy.context.object
    import bmesh
    bm=bmesh.new();bm.from_mesh(obj.data)
    bmesh.ops.delete(bm,geom=[v for v in bm.verts if v.co.z < -1e-5],context='VERTS')
    bm.to_mesh(obj.data);bm.free()
    obj.scale=(.235,.25,.16);finish(obj,'Helmet crown','metal')
    shell=obj.modifiers.new('Crown thickness','SOLIDIFY');shell.thickness=.055
    box('Crest',(0,0,.22),(.055,.35,.05),'brass')
    box('Front badge',(0,-.246,.13),(.10,.025,.10),'red')

def coat():
    # Rounded, open garment with a flared hem, waist, shoulders and neck opening.
    # Side faces stop below the shoulder yoke to leave real armholes.
    levels=((0,.32,.215),(.26,.285,.21),(.43,.27,.19),(.55,.275,.175),(.62,.13,.12))
    verts=[]
    for z,w,d in levels:
        verts += [(x*w,y*d,z) for x,y in ((-.72,-1),(.72,-1),(1,-.6),(1,.6),(.72,1),(-.72,1),(-1,.6),(-1,-.6))]
    faces=[]
    for k in range(len(levels)-1):
        for i in range(8):
            if k>=2 and i in (2,6): continue
            faces.append((k*8+i,k*8+(i+1)%8,(k+1)*8+(i+1)%8,(k+1)*8+i))
    mesh=bpy.data.meshes.new('Tailored coat shell');mesh.from_pydata(verts,[],faces);mesh.update()
    obj=bpy.data.objects.new('Protective coat',mesh);bpy.context.collection.objects.link(obj);finish(obj,'Protective coat','uniform')
    mod=obj.modifiers.new('Fabric thickness','SOLIDIFY');mod.thickness=.012
    tube('Front placket',[(0,-.225,0),(0,-.22,.26),(0,-.20,.43),(0,-.185,.54)],.018,'canvas')
    for z,y in ((.13,-.232),(.27,-.224),(.40,-.207),(.51,-.195)):
        box('Fastener',(0,y,z),(.065,.022,.022),'metal')
    for x in (-.15,.15):
        box('Utility pocket',(x,-.218,.18),(.13,.028,.12),'canvas')
        box('Pocket flap',(x,-.238,.245),(.15,.023,.035),'canvas')
    collar=[(x*.14,y*.13,.625) for x,y in ((-.72,-1),(.72,-1),(1,-.6),(1,.6),(.72,1),(-.72,1),(-1,.6),(-1,-.6),(-.72,-1))]
    tube('Raised collar',collar,.024,'canvas')
    for side in (-1,1):
        tube('Armhole binding',[(side*.27,-.114,.43),(side*.275,-.105,.55),(side*.13,-.072,.62),(side*.13,.072,.62),(side*.275,.105,.55),(side*.27,.114,.43)],.012,'canvas')
    for z in (.035,.06):
        tube('Hem stitching',[(-.23,-.218,z),(0,-.223,z),(.23,-.218,z)],.006,'rope')

def fan():
    box('Fan base',(0,0,.10),(1.4,1.1,.2),'stone')
    for x in (-.52,.52): beam('Fan support',(x,0,.15),(x,0,1.3),.12,'metal')
    # Rotor faces the front (-Y), separate blades can be animated.
    for i in range(12):
        a=i*math.tau/12;b=(i+1)*math.tau/12
        beam('Cage rim',(.62*math.cos(a),0,1.1+.62*math.sin(a)),(.62*math.cos(b),0,1.1+.62*math.sin(b)),.075,'metal')
    for i in range(4):
        a=i*math.tau/4+.3
        obj=box('Rotor blade',(.28*math.cos(a),0,1.1+.28*math.sin(a)),(.50,.075,.17),'plank'); obj.rotation_euler[1]=-a
    obj=cylinder('Axle',(0,0,1.1),.12,.45,'brass');obj.rotation_euler[0]=math.pi/2
    for x in (-.35,0,.35): beam('Front guard',(x,-.17,.65),(x,-.17,1.55),.025,'metal')

def berm():
    verts=[(-.5,-.45,0),(.5,-.45,0),(.5,.45,0),(-.5,.45,0),(-.5,-.17,.65),(.5,-.17,.65),(.5,.17,.65),(-.5,.17,.65)]
    faces=[(0,3,2,1),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7),(4,5,6,7)]
    mesh=bpy.data.meshes.new('Berm');mesh.from_pydata(verts,[],faces);mesh.update()
    obj=bpy.data.objects.new('Packed earth',mesh);bpy.context.collection.objects.link(obj);finish(obj,'Packed earth','earth')
    for x in (-.37,0,.37): box('Stone facing',(x,-.35,.15),(.31,.20,.26),'stone')

ASSETS=[('FireBell_Folktails',bell),('WardenStation_Ironteeth',station),('BrigadeBucket',bucket),('WardenSprayer',sprayer),('WardenHelmet',helmet),('WardenCoat',coat),('SmokeFan',fan),('FireBerm',berm)]
exec(compile((Path(__file__).with_name('refine_fire_response.py')).read_text(), 'refine_fire_response.py', 'exec'))
manifest=[]
for name,build in ASSETS:
    root=bpy.data.objects.new(name,None);bpy.context.collection.objects.link(root)
    build()
    refine(name)
    bpy.ops.object.select_all(action='DESELECT')
    root.select_set(True)
    for obj in root.children: obj.select_set(True)
    bpy.context.view_layer.objects.active=root
    bpy.ops.export_scene.gltf(filepath=str(OUT/(name+'.glb')),use_selection=True,export_format='GLB',export_apply=True)
    manifest.append({'name':name,'file':name+'.glb','mesh_objects':len(root.children),'triangles':sum(sum(len(p.vertices)-2 for p in obj.data.polygons) for obj in root.children),'status':'faction-style-draft'})

# Presentation scene; exported assets above retain their own local origin.
for i,(name,_) in enumerate(ASSETS):
    obj=bpy.data.objects[name]
    obj.location=((i%4)*3.7,(i//4)*3.7,0)
    if i in (2,3,4,5): obj.scale=(2,2,2)
    bpy.ops.object.text_add(location=(obj.location.x-1.3,obj.location.y-1.30,.02))
    label=bpy.context.object;label.name='Label '+name;label.data.body=name.replace('_','\n');label.data.size=.18;label.data.materials.append(MATS['metal'])
root=None
box('Display ground',(5.2,2,-.16),(17,10,.20),'stone')
bpy.ops.object.camera_add(location=(16,-18,19))
cam=bpy.context.object;cam.rotation_euler=(Vector((5.3,1.7,.5))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=18
scene=bpy.context.scene;scene.camera=cam
scene.render.engine='CYCLES';scene.cycles.samples=32
scene.world.color=(.35,.35,.35)
for pos,power,size in [((1,-5,11),2300,8),((10,6,10),1800,7)]:
    bpy.ops.object.light_add(type='AREA',location=pos);light=bpy.context.object;light.data.energy=power;light.data.shape='DISK';light.data.size=size
    light.rotation_euler=(Vector((5,2,0))-light.location).to_track_quat('-Z','Y').to_euler()
scene.render.resolution_x=1800;scene.render.resolution_y=1100;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.render.filepath=str(OUT/'overview.png')
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'fire-response.blend'))
(OUT/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
bpy.ops.render.render(write_still=True)
