"""Faction-specific art pass; runs in the generator's modeling namespace."""
import random
rng = random.Random(104)

def palette(name, color):
    m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
    m.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=(*color,1)
    m.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value=.8
    MATS[name]=m

for n,c in {'pale':(.58,.43,.24),'darkwood':(.14,.095,.052),'greenroof':(.17,.24,.16),
            'thatch':(.48,.39,.16),'iron':(.105,.13,.12),'rust':(.38,.12,.045),
            'uniform':(.075,.105,.16),'canvas':(.29,.27,.18),'rope':(.40,.30,.14),'glass':(.12,.23,.24)}.items(): palette(n,c)

# Original grain textures, embedded in GLBs; the native game textures stay untouched.
for name in ('pale','darkwood','wood','plank','edge','thatch','greenroof'):
    m=MATS[name];base=m.diffuse_color[:3];size=128
    img=bpy.data.images.new('Wildfire_'+name+'_grain',width=size,height=size)
    pixels=[]
    for y in range(size):
        for x in range(size):
            grain=math.sin(x*.72+math.sin(y*.075)*1.7)*.065+math.sin(x*2.1+y*.035)*.035+rng.uniform(-.035,.035)
            pixels.extend([max(0,min(1,c*(1+grain*1.4))) for c in base]+[1])
    img.pixels=pixels;img.pack()
    tex=m.node_tree.nodes.new('ShaderNodeTexImage');tex.image=img
    m.node_tree.links.new(tex.outputs['Color'],m.node_tree.nodes.get('Principled BSDF').inputs['Base Color'])


def recolor(obj,mat):
    obj.data.materials.clear();obj.data.materials.append(MATS[mat])

def bolts(pos, along='x', count=3, spacing=.12):
    for i in range(count):
        p=list(pos);p[0 if along=='x' else 2]+=(i-(count-1)/2)*spacing
        o=cylinder('Iron rivet',p,.018,.02,'metal');o.rotation_euler[0]=math.pi/2

def wheel(pos,r=.12):
    pts=[(pos[0]+r*math.cos(i*math.tau/16),pos[1],pos[2]+r*math.sin(i*math.tau/16)) for i in range(17)]
    tube('Valve handwheel',pts,.022,'rust')
    for a in (0,math.pi/2): beam('Valve spoke',(pos[0]-r*math.cos(a),pos[1],pos[2]-r*math.sin(a)),(pos[0]+r*math.cos(a),pos[1],pos[2]+r*math.sin(a)),.025,'iron')

def refine(name):
    objs=list(root.children)
    folk=name in ('FireBell_Folktails','BrigadeBucket')
    for obj in objs:
        mat=obj.data.materials[0].name
        if mat in ('wood','plank'): recolor(obj,'pale' if folk else 'darkwood')
        if mat=='cloth' and not folk: recolor(obj,'canvas')
    if name=='FireBell_Folktails':
        for obj in objs:
            if obj.name.startswith('Roof shingle'): recolor(obj,'thatch')
        for x in (-.65,.65):
            for z in (1.72,1.78,1.84):
                tube('Crossbar rope lashing',[(x-.12,-.12,z),(x+.12,-.12,z),(x+.12,.12,z),(x-.12,.12,z),(x-.12,-.12,z)],.016,'rope')
        for i in range(10): box('Deck plank',(-.81+i*.18,0,.22),(.17,1.10,.09),'pale')
        for side in (-1,1):
            for j in range(27):
                y=-.59+j*.045
                beam('Thatched reed course',(side*.04,y,2.38),(side*.90,y,1.80+rng.uniform(-.025,.025)),.034,'thatch')
        for x in (-.38,.03,.44):
            for z in (.58,.75): beam('Shelf bucket rack',(x,.50,.48),(x,.50,z),.025,'wood')
        # A working bell reads from the path through the rope and striker wheel.
        o=cylinder('Bell suspension axle',(0,0,1.72),.045,.56,'metal');o.rotation_euler[1]=math.pi/2
    elif name=='WardenStation_Ironteeth':
        for obj in objs:
            if obj.name.startswith('Steel post'): recolor(obj,'darkwood')
        for obj in objs:
            if obj.name.startswith(('Warden sign','Sign stripe')): obj.location.z-=.25
        for obj in objs:
            if obj.name.startswith(('Roof shingle','Ridge')): bpy.data.objects.remove(obj,do_unlink=True)
        # Truncated hipped roof: a different silhouette from the Folktails canopy.
        bottom=[(-1.43,-1.04,1.65),(1.43,-1.04,1.65),(1.43,1.04,1.65),(-1.43,1.04,1.65)]
        top=[(-.95,-.58,2.13),(.95,-.58,2.13),(.95,.58,2.13),(-.95,.58,2.13)]
        for i in range(4):
            a,b=Vector(bottom[i]),Vector(bottom[(i+1)%4]);c,d=Vector(top[i]),Vector(top[(i+1)%4])
            mesh=bpy.data.meshes.new('Roof panel');mesh.from_pydata([a,b,d,c],[],[(0,1,2,3)]);mesh.update()
            panel=bpy.data.objects.new('Roof sheathing',mesh);bpy.context.collection.objects.link(panel);finish(panel,'Roof sheathing','greenroof')
            for j in range(12):
                t=(j+.5)/12
                beam('Green roof batten',a.lerp(b,t),c.lerp(d,t),.105,'greenroof')
            beam('Roof iron edging',a,b,.09,'iron');beam('Roof upper rim',c,d,.09,'iron');beam('Hip reinforcement',a,c,.08,'iron')
        box('Roof cap',(0,0,2.12),(1.95,1.20,.10),'darkwood')
        for x in (-.65,-.39,-.13,.13,.39,.65): box('Roof vent slat',(x,0,2.24),(.12,.75,.13),'iron')
        for x in (-1.2,1.2):
            box('Corner iron strap',(x,-.88,1.04),(.20,.04,1.15),'iron')
            bolts((x,-.91,1.05),'z',5,.22)
            beam('Front diagonal',(x,-.8,1.15),(x*.58,-.8,1.62),.105,'darkwood')
        for i in range(11): box('Front platform board',(-1.25+i*.25,-.20,.235),(.23,1.65,.065),'darkwood')
        box('Entry step',(-.35,-1.14,.1),(1.1,.35,.16),'darkwood')
        wheel((.53,-.72,.47))
        for x in (-.8,-.50,-.2):
            beam('Equipment peg',(x,.72,1.15),(x,.54,1.15),.045,'metal')
        for z in (.5,.86,1.2): bolts((-1.30,-.70,z),'x',1)
    elif name=='BrigadeBucket':
        for obj in objs:
            if obj.name.startswith('Bucket stave'): obj.rotation_euler[1]=rng.uniform(-.025,.025)
        for x in (-.195,.195):
            o=cylinder('Bail pivot',(x,0,.32),.028,.032,'metal');o.rotation_euler[1]=math.pi/2
        beam('Wood carry grip',(-.065,0,.51),(.065,0,.51),.04,'pale')
    elif name=='WardenSprayer':
        for obj in objs:
            if obj.name.startswith('Backpack tank'): recolor(obj,'rust')
        box('Backplate',(0,-.195,.4),(.35,.08,.61),'darkwood')
        for x in (-.14,.14):
            for z in (.18,.67): bolts((x,-.247,z),count=1)
        tube('Pump riser',[(.23,.09,.18),(.29,.09,.18),(.29,.09,.69)],.037,'iron')
        beam('Pump lever',(.29,.09,.69),(.29,-.12,.76),.045,'darkwood')
        wheel((.29,-.02,.24),.055)
        o=cylinder('Pressure dial',(.06,-.235,.62),.057,.035,'metal');o.rotation_euler[0]=math.pi/2
        beam('Dial needle',(.06,-.26,.62),(.083,-.26,.65),.008,'red')
        for x in (-.15,.15): box('Strap buckle',(x,-.40,.40),(.10,.045,.08),'iron')
    elif name=='WardenHelmet':
        for obj in objs:
            if obj.name.startswith('Helmet crown'): recolor(obj,'rust')
        for i in range(10):
            a=i*math.tau/10
            cylinder('Helmet rivet',(.265*math.cos(a),.265*math.sin(a),.079),.012,.014,'metal')
        tube('Chin strap',[(-.24,0,.06),(-.16,-.02,-.12),(0,-.03,-.17),(.16,-.02,-.12),(.24,0,.06)],.016,'canvas')
    elif name=='SmokeFan':
        for obj in objs:
            if obj.name.startswith('Rotor blade'): recolor(obj,'greenroof')
        box('Motor housing',(0,.29,1.10),(.38,.36,.38),'rust')
        for z in (.97,1.04,1.11,1.18): box('Motor cooling fin',(0,.29,z),(.45,.38,.022),'iron')
        for x in (-.52,.52):
            bolts((x,-.085,.46),'z',3,.14)
            beam('Frame diagonal',(x,.0,.3),(x,.4,.9),.07,'darkwood')
        box('Drive pedestal',(0,.35,.45),(.35,.35,.55),'darkwood')
    elif name=='FireBerm':
        for obj in objs:
            if obj.name.startswith('Stone facing'): obj.rotation_euler[1]=rng.uniform(-.10,.10)
        for i in range(16):
            x=rng.uniform(-.43,.43);y=rng.uniform(-.12,.12)
            o=box('Packed aggregate',(x,y,.65),(.065,.045,.025),'stone');o.rotation_euler[2]=rng.random()*3
    for obj in root.children:
        if obj.type=='MESH' and len(obj.data.polygons)>0:
            bevel=obj.modifiers.new('Soft worked edges','BEVEL');bevel.width=.007 if folk else .004;bevel.segments=1
            # Generate deterministic UVs for every component, including custom meshes.
            bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
            bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(island_margin=.02);bpy.ops.object.mode_set(mode='OBJECT')
