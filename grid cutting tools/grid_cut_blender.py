import bpy ,bmesh
import copy
import mathutils
import math
    
def select(ob :bpy.types.Object):
    bpy.ops.object.select_all(action='DESELECT') # Deselect all objects
    bpy.context.view_layer.objects.active = ob   # Make the cube the active object 
    ob.select_set(True)

def perform_cuts(target,_range, dir):
    for i in _range:
        ret = bmesh.ops.bisect_plane(target, geom=target.verts[:]+target.edges[:]+target.faces[:], plane_co=(dir[0]*i,dir[1]*i,dir[2]*i), plane_no=dir)
        bmesh.ops.split_edges(target, edges=[e for e in ret['geom_cut'] if isinstance(e, bmesh.types.BMEdge)])

def cut(_range):
    bpy.ops.object.mode_set(mode='EDIT')
    bm = bmesh.from_edit_mesh(bpy.context.object.data)

    edges = []
    
    perform_cuts(bm,_range,(1,0,0))
    perform_cuts(bm,_range,(0,1,0))
    perform_cuts(bm,_range,(0,0,1))

    bmesh.update_edit_mesh(bpy.context.object.data)

    bpy.ops.mesh.separate(type='LOOSE')
    bpy.ops.object.mode_set(mode='OBJECT')

def floor(vec):
    return mathutils.Vector((math.floor(vec[0]),math.floor(vec[1]),math.floor(vec[2])))

def get_center(bbox):
    center = mathutils.Vector((0,0,0))
    for c in bbox:
        center += mathutils.Vector(c)
    center = center*0.125
    return center

def get_index(target):
    return floor(get_center(target.bound_box))

def fix_pivots():
    targets = bpy.context.selected_objects

    for target in targets:
        select(target)
        index = get_index(target)
        target.location -= index
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        target.location = index
    
    bpy.context.selected_objects = targets

def grid_cut(_range):
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    cut(_range)
    fix_pivots()

grid_cut(range(0,10,1))