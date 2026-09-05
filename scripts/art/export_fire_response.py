"""Export the review GLBs as native Timberborn content, using Mechanistry's exporter.
Requires ~/repos/timbermesh at the revision recorded in integration.json.
"""
import bpy, sys, json, math, zlib, struct
from pathlib import Path
from mathutils import Vector
REPO=Path(__file__).resolve().parents[2]
SOURCE=REPO/'art/fire-response'
OUT=SOURCE/'game-mod'
PLUGIN=Path.home()/'repos/timbermesh/src/timbermesh_blender_plugin'
sys.path.insert(0,str(PLUGIN))
from timbermesh_exporter import Exporter, ExportSettings
import model_pb2

# Exact names observed in the shipped example blend and official ShantySpeaker mesh.
FOLK={'wood':'BaseWood_Brown.Folktails','plank':'BaseWood_LightBrown.Folktails',
'pale':'BaseWood_LightBrown.Folktails','edge':'BaseWood_Brown.Folktails','thatch':'ThatchedRoof.Folktails',
'rope':'BaseWood_Brown.Folktails','brass':'BaseMetal.Folktails','metal':'BaseMetal.Folktails',
'cloth':'BaseWood_Brown.Folktails','stone':'Dirt','earth':'Dirt'}
IRON={key:'BaseWood_DarkBrown.IronTeeth' for key in ('wood','plank','darkwood','edge','canvas','cloth','rope','earth','stone')}
IRON.update({key:'BaseMetal.IronTeeth' for key in ('metal','iron','brass','glass')})
IRON.update({key:'PaintedMetal.IronTeeth' for key in ('red','rust','greenroof')})
ASSETS=[('FireBell_Folktails',(2,2,3),'Fire Bell'),('WardenStation_Ironteeth',(3,3,3),'Warden Station'),
('BrigadeBucket',(1,1,1),'Brigade Bucket'),('WardenSprayer',(1,1,1),'Warden Sprayer'),
('WardenHelmet',(1,1,1),'Warden Helmet'),('WardenCoat',(1,1,1),'Warden Coat'),
('SmokeFan',(2,2,2),'Smoke Fan'),('FireBerm',(1,1,1),'Fire Berm')]
(OUT/'Buildings/WildfirePreview').mkdir(parents=True,exist_ok=True)
(OUT/'TemplateCollections').mkdir(exist_ok=True)
(OUT/'Localizations').mkdir(exist_ok=True)
manifest={'Name':'Wildfire Asset Preview','Version':'0.1.0','Id':'JasonKleinberg.Wildfire.AssetPreview','MinimumGameVersion':'1.0.7.0','Description':'Art review: placeable building models and static gear displays. No firefighting behavior. Use a disposable review save.','RequiredMods':[]}
(OUT/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
localization=['ID,Text,Comment'];paths=[];report=[]
for name,size,label in ASSETS:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(SOURCE/(name+'.glb')))
    meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
    mapping=FOLK if name in ('FireBell_Folktails','BrigadeBucket') else IRON
    for obj in meshes:
        for slot in obj.material_slots:
            source=slot.material.name
            native=mapping.get(source)
            if native is None: raise ValueError(f'Unmapped material {name}: {source}')
            mat=bpy.data.materials.get(native) or bpy.data.materials.new(native)
            slot.material=mat
    bpy.context.view_layer.update()
    points=[o.matrix_world@Vector(c) for o in meshes for c in o.bound_box]
    low=Vector(tuple(min(p[i] for p in points) for i in range(3)))
    high=Vector(tuple(max(p[i] for p in points) for i in range(3)))
    # Official exporter converts Blender (x,y,z) to Unity (-x,z,-y).
    shift=Vector((-size[0]/2,-size[1]/2,-low.z))
    for o in bpy.context.scene.objects:
        if o.parent is None:o.location+=shift
    bpy.context.view_layer.update()
    collection=bpy.context.scene.collection
    file=OUT/'Buildings/WildfirePreview'/f'{name}.timbermesh'
    Exporter.export_collection(collection,str(file),ExportSettings(bpy.context,True,False,False))
    model=model_pb2.Model();model.ParseFromString(zlib.decompress(file.read_bytes()))
    triangle_count=sum(len(mesh.indices)//3 for node in model.nodes for mesh in node.meshes)
    assert triangle_count>0 and all(mesh.material in set(mapping.values()) for node in model.nodes for mesh in node.meshes)
    height=high.z-low.z
    bp={'TemplateSpec':{'TemplateName':'WildfirePreview.'+name},
        'BuildingSpec':{'BuildingCost':[],'ScienceCost':0,'PlaceFinished':True},
        'BuildingModelSpec':{'FinishedModelName':'#Finished','UnfinishedModelName':'','ConstructionModeModel':'Finished'},
        'BlockObjectSpec':{'Size':dict(zip(('X','Y','Z'),size)),
          'Blocks':[{'MatterBelow':'GroundOrStackable' if z==0 else 'Any','Occupations':'All','Stackable':'None'} for z in range(size[2]) for y in range(size[1]) for x in range(size[0])],
          'Entrance':{'HasEntrance':False},'BaseZ':0,'Flippable':False},
        'PlaceableBlockObjectSpec':{'ToolGroupId':'Decoration','ToolOrder':200+len(report),'ToolShape':'Square','Layout':'Single','DevModeTool':False},
        'LabeledEntitySpec':{'DisplayNameLocKey':'WildfirePreview.'+name,'DescriptionLocKey':'WildfirePreview.Description','Icon':'Sprites/StatusIcons/BuildingNeedsWater'},
        'Children':{'#Finished':{'TimbermeshSpec':{'Model':'Buildings/WildfirePreview/'+name},
        'CollidersSpec':{'BoxColliders':[{'Center':{'X':size[0]/2,'Y':height/2,'Z':size[1]/2},'Size':{'X':high.x-low.x,'Y':height,'Z':high.y-low.y}}]}}}}
    path='Buildings/WildfirePreview/'+name+'.blueprint'
    (OUT/(path+'.json')).write_text(json.dumps(bp,indent=2)+'\n');paths.append(path)
    localization.append('WildfirePreview.'+name+','+label+' [Art Preview],')
    report.append({'asset':name,'triangles':triangle_count,'nodes':len(model.nodes),'materials':sorted({m.material for n in model.nodes for m in n.meshes}),'footprint':size,'height':height})
# Native models are referenced, not copied, for adjacent scale/style comparison.
for name,size,native,label in [('NativeLodge',(2,2,1),'Buildings/Housing/Lodge/Lodge.Folktails.Model','Native Folktails Lodge'),('NativeBarrack',(3,2,2),'Buildings/Housing/Barrack/Barrack.IronTeeth.Model','Native Iron Teeth Barrack')]:
    bp=json.loads((OUT/'Buildings/WildfirePreview/FireBerm.blueprint.json').read_text())
    bp['TemplateSpec']['TemplateName']='WildfirePreview.'+name
    bp['BlockObjectSpec']['Size']=dict(zip(('X','Y','Z'),size))
    bp['BlockObjectSpec']['Blocks']=[{'MatterBelow':'GroundOrStackable' if z==0 else 'Any','Occupations':'All','Stackable':'None'} for z in range(size[2]) for y in range(size[1]) for x in range(size[0])]
    bp['LabeledEntitySpec']['DisplayNameLocKey']='WildfirePreview.'+name
    bp['Children']['#Finished']={'TimbermeshSpec':{'Model':native},'CollidersSpec':{'BoxColliders':[{'Center':{'X':size[0]/2,'Y':size[2]/2,'Z':size[1]/2},'Size':{'X':size[0],'Y':size[2],'Z':size[1]}}]}}
    path='Buildings/WildfirePreview/'+name+'.blueprint'
    (OUT/(path+'.json')).write_text(json.dumps(bp,indent=2)+'\n');paths.append(path)
    localization.append('WildfirePreview.'+name+','+label+' [Reference],')
localization.append('WildfirePreview.Description,Static art preview. No firefighting behavior. Gear is displayed at ground level for scale review.,')
(OUT/'Localizations/enUS.csv').write_text('\n'.join(localization)+'\n')
for faction in ('Folktails','IronTeeth'):
    (OUT/'TemplateCollections'/f'TemplateCollection.Buildings.{faction}.blueprint.json').write_text(json.dumps({'TemplateCollectionSpec':{'CollectionId':'Buildings.'+faction,'Blueprints#append':paths}},indent=2)+'\n')
(SOURCE/'integration.json').write_text(json.dumps(report,indent=2)+'\n')
print('WILDFIRE_TIMBERMESH_EXPORT_OK',len(report))

# The preview intentionally renders both factions in one save. Load opposite-faction
# atlas materials as well, including the native comparison models' materials.
game=Path.home()/'Library/Application Support/Steam/steamapps/common/Timberborn/Timberborn.app/Contents/Resources/Data/StreamingAssets/Modding/Blueprints'
(OUT/'MaterialCollections').mkdir(exist_ok=True)
for faction,other in [('Folktails','IronTeeth'),('IronTeeth','Folktails')]:
    collection=json.loads((game/'MaterialCollections'/f'MaterialCollection.{other}.blueprint.json').read_text())
    paths=[p for p in collection['MaterialCollectionSpec']['Materials'] if p.startswith('Materials/UberAtlas/')]
    (OUT/'MaterialCollections'/f'MaterialCollection.{faction}.blueprint.json').write_text(json.dumps({'MaterialCollectionSpec':{'CollectionId':faction,'Materials#append':paths}},indent=2)+'\n')
