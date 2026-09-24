# 3D-интерфейс инвентаря: подвесная деревянная доска с гнёздами под вещи и медальонами категорий.
# Запуск: blender -b --python Art/build_ui.py -- <out_dir> [preview.png]
# Результат: board.fbx (доска + пустышки Slot_0..5, Tab_0..3, Close) и ring.fbx (кольцо подсветки).
# Пустышки — точки, куда Unity ставит вещи, медальоны и крестик. Оси как у панд: вперёд −Y, вверх Z.

import bpy, bmesh, math, os, sys
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = argv[0] if argv else "D:/LovePandas/Assets/LovePandas/Resources/Models/UI"
PREVIEW = argv[1] if len(argv) > 1 else None

WOOD = (0.78, 0.55, 0.33)
WOOD_DARK = (0.52, 0.33, 0.19)
BARK = (0.36, 0.23, 0.14)
SLOT = (0.45, 0.28, 0.16)
ROPE = (0.80, 0.68, 0.46)
MOSS = (0.36, 0.55, 0.22)
LEAF = (0.30, 0.62, 0.40)
NAIL = (0.25, 0.25, 0.28)
DISC = (0.86, 0.66, 0.42)

W, H, D = 1.0, 2.4, 0.08   # доска
TOP = H / 2

def lin(c):
    f = lambda x: x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4
    return (f(c[0]), f(c[1]), f(c[2]))

def paint(o, color, glow=False):
    attr = o.data.color_attributes.get("Col") or o.data.color_attributes.new("Col", 'FLOAT_COLOR', 'CORNER')
    c = lin(color)
    for d in attr.data: d.color = (c[0], c[1], c[2], 0.0 if glow else 1.0)

def done(o, name, color, loc=None, rot=None, scale=None, smooth=True, glow=False):
    o.name = name
    if loc: o.location = loc
    if rot: o.rotation_euler = [math.radians(r) for r in rot]
    if scale: o.scale = scale
    bpy.context.view_layer.objects.active = o
    o.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    o.select_set(False)
    for p in o.data.polygons: p.use_smooth = smooth
    paint(o, color, glow)
    return o

def box(name, color, loc, size, bevel=0.0, seg=3):
    bpy.ops.mesh.primitive_cube_add(size=1)
    o = bpy.context.object
    o.scale = size
    bpy.ops.object.transform_apply(scale=True)
    if bevel > 0:
        m = o.modifiers.new("B", 'BEVEL'); m.width = bevel; m.segments = seg
        bpy.ops.object.modifier_apply(modifier="B")
    return done(o, name, color, loc, smooth=bevel > 0)

def cyl(name, color, loc, r, depth, rot=None, verts=24, scale=None, smooth=True):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=depth)
    o = done(bpy.context.object, name, color, loc, rot, scale, smooth=smooth)
    if smooth:
        # плоские торцы: иначе сглаживание делает из диска купол
        for p in o.data.polygons:
            if len(p.vertices) > 4: p.use_smooth = False
    return o

def sph(name, color, loc, r, scale=(1, 1, 1), rot=None, seg=16):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=seg // 2, radius=r)
    return done(bpy.context.object, name, color, loc, rot, scale)

def torus(name, color, loc, R, r, rot=None, glow=False):
    bpy.ops.mesh.primitive_torus_add(major_radius=R, minor_radius=r, major_segments=40, minor_segments=10)
    return done(bpy.context.object, name, color, loc, rot, glow=glow)

def empty(name, loc):
    e = bpy.data.objects.new(name, None)
    e.location = loc
    bpy.context.collection.objects.link(e)
    return e

def join(objs, name):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    o = bpy.context.object
    o.name = name
    return o

def export(path, objs):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, apply_scale_options='FBX_SCALE_ALL',
                             axis_forward='-Z', axis_up='Y', add_leaf_bones=False, mesh_smooth_type='FACE',
                             colors_type='LINEAR', bake_anim=False, object_types={'MESH', 'EMPTY'})

# ---------- Доска ----------
bpy.ops.wm.read_factory_settings(use_empty=True)
parts = []
front = -D / 2  # лицевая плоскость доски (y)

parts.append(box("Board", WOOD, (0, 0, 0), (W, D, H), bevel=0.035))
# три вертикальные доски: тёмные швы
for x in (-W / 6, W / 6):
    parts.append(box("Seam", WOOD_DARK, (x, front - 0.002, 0), (0.014, 0.01, H - 0.08)))
# шапка под медальоны — выступающая тёмная планка
parts.append(box("Header", WOOD_DARK, (0, front - 0.02, TOP - 0.32), (W - 0.12, 0.04, 0.46), bevel=0.02))
# рамка из брёвнышек
parts.append(cyl("LogTop", BARK, (0, 0, TOP + 0.04), 0.055, W + 0.28, rot=(0, 90, 0), verts=12))
parts.append(cyl("LogBottom", BARK, (0, -0.01, -TOP - 0.03), 0.05, W + 0.16, rot=(0, 90, 0), verts=12))
for x in (-W / 2 - 0.02, W / 2 + 0.02):
    parts.append(cyl("LogSide", BARK, (x, -0.01, 0), 0.045, H + 0.02, verts=12))
# верёвки вверх от концов верхнего бревна — на них доска спускается
for x in (-W / 2 + 0.05, W / 2 - 0.05):
    parts.append(cyl("Rope", ROPE, (x, 0, TOP + 1.6), 0.018, 3.1, verts=8))
    parts.append(torus("Knot", ROPE, (x, 0, TOP + 0.04), 0.07, 0.02, rot=(0, 90, 0)))
# гвозди
for x, z in ((-W / 2 + 0.1, TOP - 0.1), (W / 2 - 0.1, TOP - 0.1), (-W / 2 + 0.1, -TOP + 0.1), (W / 2 - 0.1, -TOP + 0.1)):
    parts.append(sph("Nail", NAIL, (x, front - 0.01, z), 0.022, (1, 0.5, 1)))
# мох и листья
for (x, z, r) in ((-W / 2, TOP - 0.05, 0.12), (W / 2 - 0.05, -TOP + 0.08, 0.1), (-W / 2 + 0.05, -0.4, 0.07)):
    parts.append(sph("Moss", MOSS, (x, front - 0.01, z), r, (1.3, 0.5, 0.8)))
for k, (x, rz) in enumerate(((-0.45, 30), (-0.3, -20), (0.35, 25), (0.52, -35))):
    parts.append(sph("Leaf%d" % k, LEAF, (x, -0.04, TOP + 0.1), 0.09, (1.6, 0.25, 0.6), rot=(0, rz, 0)))

# гнёзда под вещи: 2 колонки × 3 ряда
slots = []
for row, z in enumerate((0.3, -0.26, -0.82)):
    for col, x in enumerate((-0.23, 0.23)):
        parts.append(cyl("SlotRim", WOOD_DARK, (x, front - 0.012, z), 0.205, 0.03, rot=(90, 0, 0), verts=40))
        parts.append(cyl("Slot", SLOT, (x, front - 0.02, z), 0.18, 0.02, rot=(90, 0, 0), verts=40))
        slots.append(empty("Slot_%d" % (row * 2 + col), (x, front - 0.1, z)))

# медальоны категорий в шапке
tabs = []
for k, x in enumerate((-0.33, -0.11, 0.11, 0.33)):
    parts.append(cyl("Tab", DISC, (x, front - 0.05, TOP - 0.32), 0.095, 0.035, rot=(90, 0, 0), verts=32))
    tabs.append(empty("Tab_%d" % k, (x, front - 0.1, TOP - 0.32)))

close = empty("Close", (W / 2 - 0.02, front - 0.08, TOP + 0.02))
board = join(parts, "Board")
for e in slots + tabs + [close]: e.parent = board
os.makedirs(OUT, exist_ok=True)
export(os.path.join(OUT, "board.fbx"), [board] + slots + tabs + [close])

if PREVIEW:
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.display.shading.light = 'STUDIO'
    scene.display.shading.color_type = 'VERTEX'
    scene.display.shading.show_object_outline = True
    scene.render.resolution_x, scene.render.resolution_y = 500, 900
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    bpy.context.collection.objects.link(cam)
    scene.camera = cam
    cam.location = (0.6, -4.2, 0.3)
    cam.rotation_euler = (math.radians(90), 0, math.radians(8))
    scene.render.filepath = PREVIEW
    bpy.ops.render.render(write_still=True)

# ---------- Платформа под пандой ----------
# Каменный диск: верх — текстура плит из фона (UV-проекция сверху), бок — тёмный камень, по краю мох и камешки.
STONE_SIDE = (0.42, 0.38, 0.34)
bpy.ops.wm.read_factory_settings(use_empty=True)
R_PLAT, H_PLAT = 1.35, 0.22
bpy.ops.mesh.primitive_cylinder_add(vertices=64, radius=R_PLAT, depth=H_PLAT, location=(0, 0, -H_PLAT / 2))
plat = bpy.context.object
m = plat.modifiers.new("B", 'BEVEL'); m.width = 0.05; m.segments = 3; m.limit_method = 'ANGLE'
bpy.ops.object.modifier_apply(modifier="B")
me = plat.data
uv = me.uv_layers.new(name="UV")
col = me.color_attributes.new("Col", 'FLOAT_COLOR', 'CORNER')
side = lin(STONE_SIDE)
for poly in me.polygons:
    top = poly.normal.z > 0.7
    poly.use_smooth = not top and poly.normal.z > -0.7
    for li in poly.loop_indices:
        co = me.vertices[me.loops[li].vertex_index].co
        uv.data[li].uv = (co.x / (2 * R_PLAT) + 0.5, co.y / (2 * R_PLAT) + 0.5)
        col.data[li].color = (1, 1, 1, 1) if top else (side[0], side[1], side[2], 1)
plat.name = "Platform"
extra = []
for k in range(14):
    a = 2 * math.pi * k / 14 + 0.3
    r = R_PLAT - 0.02
    if k % 3 == 0:
        extra.append(sph("Moss", MOSS, (math.cos(a) * r, math.sin(a) * r, -0.02), 0.12, (1.4, 0.9, 0.45)))
    elif k % 3 == 1:
        extra.append(sph("Pebble", STONE_SIDE, (math.cos(a) * (r + 0.08), math.sin(a) * (r + 0.08), -0.16), 0.07, (1.2, 1, 0.7)))
# Мох и камешки — отдельным мешем: у них нет текстуры, Unity даёт им простой материал.
decor = join(extra, "PlatformDecor")
env_dir = os.path.join(os.path.dirname(OUT.rstrip("/\\")), "Env")
os.makedirs(env_dir, exist_ok=True)
export(os.path.join(env_dir, "platform.fbx"), [plat, decor])

# ---------- Кольцо подсветки (цвет задаёт Unity) ----------
bpy.ops.wm.read_factory_settings(use_empty=True)
ring = torus("Ring", (1, 1, 1), (0, 0, 0), 0.2, 0.018, rot=(90, 0, 0), glow=True)
export(os.path.join(OUT, "ring.fbx"), [ring])
print("LOVEPANDAS_UI_OK")
