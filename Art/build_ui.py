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

# Платформу-диск под пандой пробовали и убрали: поверх нарисованного пола получалось «два пола».

# ---------- Доска заказов ----------
# Большой щит на весь экран: доски, толстая рамка из брёвен, табличка-заголовок, две колонки листков
# («Для тебя» — задания партнёра, «Твои заказы» — свои), внизу табличка «+ Новый заказ».
# Пустышки: Title, Header_L/R, Note_L0..3 / Note_R0..3 (центр листка), New (табличка), Close.
bpy.ops.wm.read_factory_settings(use_empty=True)
QW, QH, QD = 1.44, 2.96, 0.08
QTOP, qfront = QH / 2, -QD / 2
q = []
q.append(box("QBoard", WOOD, (0, 0, 0), (QW, QD, QH), bevel=0.035))
for x in (-QW / 4, 0, QW / 4):
    q.append(box("QSeam", WOOD_DARK, (x, qfront - 0.002, 0), (0.014, 0.01, QH - 0.1)))
q.append(cyl("QLogTop", BARK, (0, 0, QTOP + 0.05), 0.07, QW + 0.34, rot=(0, 90, 0), verts=12))
q.append(cyl("QLogBottom", BARK, (0, -0.01, -QTOP - 0.04), 0.065, QW + 0.24, rot=(0, 90, 0), verts=12))
for x in (-QW / 2 - 0.03, QW / 2 + 0.03):
    q.append(cyl("QLogSide", BARK, (x, -0.01, 0), 0.06, QH + 0.04, verts=12))
for x in (-QW / 2 + 0.08, QW / 2 - 0.08):
    q.append(cyl("QRope", ROPE, (x, 0, QTOP + 1.7), 0.02, 3.2, verts=8))
    q.append(torus("QKnot", ROPE, (x, 0, QTOP + 0.05), 0.085, 0.024, rot=(0, 90, 0)))
# табличка-заголовок и таблички колонок
q.append(box("TitlePlaque", WOOD_DARK, (0, qfront - 0.03, QTOP - 0.2), (0.9, 0.05, 0.24), bevel=0.03))
for x in (-QW / 4, QW / 4):
    q.append(box("HeaderPlaque", DISC, (x, qfront - 0.02, QTOP - 0.47), (0.6, 0.035, 0.13), bevel=0.02))
# вертикальная перегородка между колонками — тонкое брёвнышко
q.append(cyl("QDivider", BARK, (0, qfront - 0.02, -0.18), 0.022, QH - 0.9, verts=10))
# табличка «Новый заказ» внизу
q.append(box("NewPlaque", DISC, (0, qfront - 0.03, -QTOP + 0.2), (0.62, 0.05, 0.2), bevel=0.03))
q.append(box("PlusH", WOOD_DARK, (-0.21, qfront - 0.06, -QTOP + 0.2), (0.1, 0.02, 0.026)))
q.append(box("PlusV", WOOD_DARK, (-0.21, qfront - 0.06, -QTOP + 0.2), (0.026, 0.02, 0.1)))
for x, z in ((-QW / 2 + 0.1, QTOP - 0.1), (QW / 2 - 0.1, QTOP - 0.1), (-QW / 2 + 0.1, -QTOP + 0.1), (QW / 2 - 0.1, -QTOP + 0.1)):
    q.append(sph("QNail", NAIL, (x, qfront - 0.01, z), 0.024, (1, 0.5, 1)))
for (x, z, r) in ((-QW / 2, QTOP - 0.05, 0.14), (QW / 2 - 0.05, -QTOP + 0.1, 0.12), (QW / 2, 0.6, 0.08)):
    q.append(sph("QMoss", MOSS, (x, qfront - 0.01, z), r, (1.3, 0.5, 0.8)))
for k, (x, rz) in enumerate(((-0.62, 30), (-0.45, -20), (0.5, 25), (0.68, -35))):
    q.append(sph("QLeaf%d" % k, LEAF, (x, -0.05, QTOP + 0.12), 0.1, (1.6, 0.25, 0.6), rot=(0, rz, 0)))

qe = [empty("Title", (0, qfront - 0.08, QTOP - 0.2)), empty("Close", (QW / 2 - 0.02, qfront - 0.1, QTOP + 0.02)),
      empty("New", (0.04, qfront - 0.08, -QTOP + 0.2))]
for side, x in (("L", -QW / 4), ("R", QW / 4)):
    qe.append(empty("Header_" + side, (x, qfront - 0.06, QTOP - 0.47)))
    for k in range(4):
        qe.append(empty("Note_%s%d" % (side, k), (x, qfront - 0.05, QTOP - 0.83 - k * 0.5)))
qboard = join(q, "QuestBoard")
for e in qe: e.parent = qboard
export(os.path.join(OUT, "quest_board.fbx"), [qboard] + qe)

# ---------- Листок-заказ ----------
# Пергамент 0.62×0.46, чуть изогнут (края отходят от доски), с UV под текстуру; гвоздик с красной шляпкой сверху.
bpy.ops.wm.read_factory_settings(use_empty=True)
PW, PH = 0.62, 0.46
bm = bmesh.new()
nx, nz = 12, 9
grid = []
for j in range(nz + 1):
    row = []
    for i in range(nx + 1):
        u, v = i / nx, j / nz
        x, z = (u - 0.5) * PW, (v - 0.5) * PH
        # изгиб: нижние углы и край отходят от доски (к камере = −Y)
        y = -(0.035 * (abs(u - 0.5) * 2) ** 2 + 0.025 * (1 - v) ** 2)
        row.append(bm.verts.new((x, y, z)))
    grid.append(row)
for j in range(nz):
    for i in range(nx):
        bm.faces.new((grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]))
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
me = bpy.data.meshes.new("Paper")
bm.to_mesh(me)
bm.free()
paper = bpy.data.objects.new("Paper", me)
bpy.context.collection.objects.link(paper)
uvl = me.uv_layers.new(name="UV")
for loop in me.loops:
    co = me.vertices[loop.vertex_index].co
    uvl.data[loop.index].uv = (co.x / PW + 0.5, co.z / PH + 0.5)
# нормали к камере (−Y): иначе лицевая сторона смотрит в доску
if me.polygons[0].normal.y > 0:
    for p in me.polygons: p.flip()
for p in me.polygons: p.use_smooth = True
paint(paper, (1, 1, 1))
pin = join([sph("PinHead", (0.85, 0.18, 0.16), (0, -0.05, PH / 2 - 0.05), 0.03),
            cyl("PinStem", NAIL, (0, -0.02, PH / 2 - 0.05), 0.006, 0.06, rot=(90, 0, 0), verts=8)], "Pin")
export(os.path.join(OUT, "paper.fbx"), [paper, pin])

# ---------- Дощечка-модалка «Новый заказ» ----------
# Пустышки: Title (надпись), Field (поле текста), Price (цена), Submit (кнопка), Close.
bpy.ops.wm.read_factory_settings(use_empty=True)
MW, MH = 1.24, 0.96
m = [box("MBoard", WOOD, (0, 0, 0), (MW, 0.07, MH), bevel=0.04)]
for x in (-MW / 6, MW / 6):
    m.append(box("MSeam", WOOD_DARK, (x, -0.037, 0), (0.012, 0.01, MH - 0.08)))
m.append(cyl("MLogTop", BARK, (0, 0, MH / 2 + 0.04), 0.05, MW + 0.2, rot=(0, 90, 0), verts=12))
for x in (-MW / 2 + 0.06, MW / 2 - 0.06):
    m.append(cyl("MRope", ROPE, (x, 0, MH / 2 + 1.6), 0.016, 3.1, verts=8))
m.append(sph("MMoss", MOSS, (MW / 2 - 0.04, -0.04, -MH / 2 + 0.06), 0.09, (1.3, 0.5, 0.8)))
mb = join(m, "ModalBoard")
me_ = [empty("Title", (0, -0.1, MH / 2 - 0.13)), empty("Field", (0, -0.1, 0.02)),
       empty("Price", (-0.3, -0.1, -MH / 2 + 0.17)), empty("Submit", (0.25, -0.1, -MH / 2 + 0.17)),
       empty("Close", (MW / 2 - 0.02, -0.1, MH / 2 + 0.02))]
for e in me_: e.parent = mb
export(os.path.join(OUT, "modal_board.fbx"), [mb] + me_)

# ---------- HUD: деревянные медальоны-кнопки с 3D-иконками ----------
# Каждый файл — медальон + иконка перед ним, центр в нуле, диаметр медальона 1.
# hud_inventory — сундучок, hud_orders — свиток с печатью, hud_visit — домик с сердечком.
GOLD_ = (1.0, 0.8, 0.3)
IRON = (0.3, 0.3, 0.34)
CREAM_ = (0.97, 0.9, 0.74)
RED_ = (0.82, 0.16, 0.18)
PINK_ = (0.98, 0.42, 0.55)
WALL = (0.98, 0.9, 0.78)
ROOF = (0.86, 0.36, 0.2)

def medallion():
    parts = [cyl("Disc", DISC, (0, 0.04, 0), 0.5, 0.1, rot=(90, 0, 0), verts=48),
             torus("Rim", WOOD_DARK, (0, -0.01, 0), 0.5, 0.05, rot=(90, 0, 0)),
             sph("Moss", MOSS, (-0.34, -0.03, -0.36), 0.1, (1.3, 0.5, 0.8))]
    return parts

def heart(name, loc, size, color, glow=False):
    """Объёмное сердце: две сферы и конус остриём вниз (в плоскости XZ, лицом к −Y)."""
    x, y, z = loc
    s = size
    # треугольная призма (цилиндр из 3 граней, первая вершина по +Y): X−90 ставит её остриём вниз в плоскости XZ
    parts = [sph(name + "L", color, (x - 0.26 * s, y, z + 0.12 * s), 0.3 * s, (1, 0.7, 1)),
             sph(name + "R", color, (x + 0.26 * s, y, z + 0.12 * s), 0.3 * s, (1, 0.7, 1)),
             cyl(name + "Tip", color, (x, y, z - 0.12 * s), 0.52 * s, 0.4 * s, rot=(-90, 0, 0), verts=3,
                 scale=(1.0, 1.1, 1))]
    for o in parts: paint(o, color, glow)
    return parts

def hud_inventory():
    p = []
    p.append(box("ChestBody", WOOD, (0, -0.22, -0.08), (0.52, 0.3, 0.3), bevel=0.03))
    p.append(cyl("ChestLid", WOOD_DARK, (0, -0.22, 0.07), 0.15, 0.52, rot=(0, 90, 0), verts=24, scale=(1, 1, 1)))
    for x in (-0.17, 0.17):
        p.append(box("Band", IRON, (x, -0.22, -0.02), (0.05, 0.32, 0.46), bevel=0.01))
    p.append(box("Lock", GOLD_, (0, -0.385, -0.03), (0.09, 0.03, 0.1), bevel=0.01))
    return p

def hud_orders():
    p = []
    p.append(box("Sheet", CREAM_, (0, -0.2, -0.02), (0.42, 0.02, 0.5), bevel=0.01))
    for z in (0.25, -0.29):
        p.append(cyl("Roll", (0.9, 0.8, 0.62), (0, -0.21, z), 0.055, 0.5, rot=(0, 90, 0), verts=16))
    for k, z in enumerate((0.1, 0.0, -0.1)):
        p.append(box("Line%d" % k, (0.55, 0.42, 0.3), (-0.02, -0.215, z), (0.26 - k * 0.04, 0.005, 0.018)))
    p.append(cyl("Seal", RED_, (0.1, -0.23, -0.19), 0.075, 0.03, rot=(90, 0, 0), verts=20))
    return p

def hud_visit():
    p = []
    p.append(box("Walls", WALL, (0, -0.2, -0.12), (0.42, 0.3, 0.3), bevel=0.02))
    # крыша — треугольная призма
    # крыша: призма коньком вверх (первая вершина цилиндра по +Y, X90 переводит её в +Z)
    p.append(cyl("Roof", ROOF, (0, -0.2, 0.06), 0.3, 0.36, rot=(90, 0, 0), verts=3, scale=(1.35, 0.8, 1)))
    p.append(box("Door", WOOD_DARK, (0.07, -0.355, -0.17), (0.1, 0.01, 0.17)))
    p.append(box("Window", (0.55, 0.8, 0.95), (-0.1, -0.355, -0.08), (0.08, 0.01, 0.08)))
    p += heart("Heart", (0.18, -0.3, 0.3), 0.32, PINK_, glow=True)
    return p

for fn, name in ((hud_inventory, "hud_inventory"), (hud_orders, "hud_orders"), (hud_visit, "hud_visit")):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    # два объекта: неподвижная плашка-медальон и иконка на ней (в Unity шевелится только иконка)
    plaque = join(medallion(), "Plaque")
    icon = join(fn(), "Icon")
    export(os.path.join(OUT, name + ".fbx"), [plaque, icon])
    if PREVIEW:
        scene = bpy.context.scene
        scene.render.engine = 'BLENDER_WORKBENCH'
        scene.display.shading.light = 'STUDIO'
        scene.display.shading.color_type = 'VERTEX'
        scene.display.shading.show_object_outline = True
        scene.render.resolution_x = scene.render.resolution_y = 300
        cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
        bpy.context.collection.objects.link(cam)
        scene.camera = cam
        cam.location = (0.35, -3.2, 0.2)
        cam.rotation_euler = (math.radians(87), 0, math.radians(6))
        scene.render.filepath = os.path.join(os.path.dirname(PREVIEW), name + ".png")
        bpy.ops.render.render(write_still=True)

# ---------- Кольцо подсветки (цвет задаёт Unity) ----------
bpy.ops.wm.read_factory_settings(use_empty=True)
ring = torus("Ring", (1, 1, 1), (0, 0, 0), 0.2, 0.018, rot=(90, 0, 0), glow=True)
export(os.path.join(OUT, "ring.fbx"), [ring])
print("LOVEPANDAS_UI_OK")
