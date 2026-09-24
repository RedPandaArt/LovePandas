# Сборка панд и первых предметов одежды в Blender по концептам docs/concepts/characters/chosen/.
# Запуск: blender -b --python Art/build_panda.py -- <out_dir> [preview_dir]
# Результат: panda_m.fbx, panda_f.fbx (общий скелет) и items/*.fbx (жёсткие предметы, origin = точка сокета).
#
# Устройство: модель из гладких частей, каждая часть жёстко привязана к своей кости (как игрушка),
# хвост — цепочка из 4 костей с плавными весами. Окрас — цвета вершин (атрибут "Col"),
# альфа вершины < 1 = свечение (шейдер LovePandas/Toon).
# Оси Blender: вперёд −Y, вверх Z, лапы на z=0, рост без шляпы ≈ 1 м.

import bpy, bmesh, math, os, sys
from mathutils import Vector, Matrix

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = argv[0] if argv else "D:/LovePandas/Assets/LovePandas/Resources/Models"
PREVIEW = argv[1] if len(argv) > 1 else None

# ---------- Палитра (из концептов) ----------
FUR = (0.80, 0.33, 0.12)
WHITE = (0.98, 0.95, 0.90)
DARK = (0.16, 0.09, 0.07)
EAR_DARK = (0.30, 0.16, 0.10)
EYE = (0.20, 0.11, 0.05)
NOSE = (0.06, 0.04, 0.04)
CREAM = (0.97, 0.88, 0.72)
BLUSH = (0.98, 0.55, 0.58)
TEAL = (0.10, 0.52, 0.48)
STRAW = (0.66, 0.55, 0.40)
WOOD = (0.42, 0.27, 0.15)
LANTERN_RED = (0.72, 0.10, 0.08)
GLOW = (1.00, 0.80, 0.45)
PINK = (0.98, 0.62, 0.72)
IVORY = (0.98, 0.94, 0.84)

# ---------- Сцена ----------
def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)

def lin(c):
    """Палитра задана в sRGB (как видно на картинке), атрибут цвета хранит линейные значения."""
    f = lambda x: x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4
    return (f(c[0]), f(c[1]), f(c[2]))

def paint(obj, color, glow=False):
    me = obj.data
    attr = me.color_attributes.get("Col") or me.color_attributes.new("Col", 'FLOAT_COLOR', 'CORNER')
    a = 0.0 if glow else 1.0
    c = lin(color)
    for d in attr.data:
        d.color = (c[0], c[1], c[2], a)
    me.color_attributes.active_color = attr

def finish(obj, name, color, loc=None, rot=None, scale=None, glow=False, smooth=True):
    obj.name = name
    if loc: obj.location = loc
    if rot: obj.rotation_euler = [math.radians(r) for r in rot]
    if scale: obj.scale = scale
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    obj.select_set(False)
    if smooth:
        for p in obj.data.polygons: p.use_smooth = True
    paint(obj, color, glow)
    return obj

def sphere(name, color, loc, r, scale=(1, 1, 1), rot=None, seg=24, glow=False):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=seg // 2, radius=r)
    return finish(bpy.context.object, name, color, loc, rot, scale, glow)

def cone(name, color, loc, r1, r2, depth, rot=None, scale=(1, 1, 1), verts=24, glow=False):
    bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r1, radius2=r2, depth=depth)
    return finish(bpy.context.object, name, color, loc, rot, scale, glow)

def cylinder(name, color, loc, r, depth, rot=None, scale=(1, 1, 1), verts=16, glow=False):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=depth)
    return finish(bpy.context.object, name, color, loc, rot, scale, glow)

def torus(name, color, loc, R, r, rot=None, scale=(1, 1, 1)):
    bpy.ops.mesh.primitive_torus_add(major_radius=R, minor_radius=r, major_segments=32, minor_segments=10)
    return finish(bpy.context.object, name, color, loc, rot, scale)

def capsule(name, color, a, b, r):
    """Капсула от точки a до точки b."""
    a, b = Vector(a), Vector(b)
    d = b - a
    bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=10, radius=r)
    o = bpy.context.object
    # растянуть сферу вдоль оси: верх и низ полусфер разнести на длину отрезка
    for v in o.data.vertices:
        v.co.z += (d.length / 2) * (1 if v.co.z > 0 else -1)
    o.rotation_mode = 'QUATERNION'
    o.rotation_quaternion = Vector((0, 0, 1)).rotation_difference(d)
    o.location = (a + b) / 2
    return finish(o, name, color)

def smooth_path(points, radii, count):
    """Сплайн Катмулла–Рома через точки: гладкий хвост вместо ломаной."""
    P = [Vector(p) for p in points]
    P = [P[0] * 2 - P[1]] + P + [P[-1] * 2 - P[-2]]
    R = [radii[0]] + list(radii) + [radii[-1]]
    out_p, out_r = [], []
    segs = len(points) - 1
    for k in range(count):
        u = k / (count - 1) * segs
        i = min(int(u), segs - 1)
        t = u - i
        p0, p1, p2, p3 = P[i], P[i + 1], P[i + 2], P[i + 3]
        t2, t3 = t * t, t * t * t
        out_p.append(0.5 * (2 * p1 + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3))
        out_r.append(R[i + 1] + (R[i + 2] - R[i + 1]) * (t * t * (3 - 2 * t)))
    return out_p, out_r

def tube(name, points, radii, colors_fn, ring_segments=16):
    """Трубка по точкам с переменным радиусом; colors_fn(t) → цвет по длине (0..1)."""
    bm = bmesh.new()
    pts = [Vector(p) for p in points]
    rings = []
    n = len(pts)
    for i, p in enumerate(pts):
        t = pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)]
        t.normalize()
        side = t.cross(Vector((0, 0, 1)))
        if side.length < 1e-3: side = t.cross(Vector((1, 0, 0)))
        side.normalize()
        up = side.cross(t)
        ring = []
        for k in range(ring_segments):
            ang = 2 * math.pi * k / ring_segments
            ring.append(bm.verts.new(p + (side * math.cos(ang) + up * math.sin(ang)) * radii[i]))
        rings.append(ring)
    for i in range(n - 1):
        for k in range(ring_segments):
            k2 = (k + 1) % ring_segments
            bm.faces.new((rings[i][k], rings[i][k2], rings[i + 1][k2], rings[i + 1][k]))
    tip = bm.verts.new(pts[-1] + (pts[-1] - pts[-2]).normalized() * radii[-1] * 0.8)
    for k in range(ring_segments):
        bm.faces.new((rings[-1][k], rings[-1][(k + 1) % ring_segments], tip))
    base = bm.verts.new(pts[0])
    for k in range(ring_segments):
        bm.faces.new((rings[0][(k + 1) % ring_segments], rings[0][k], base))
    # нормали наружу: иначе шейдер рисует изнанку, а обводка закрывает хвост тёмной заливкой
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    # параметр t по длине — по индексу кольца вершины (посчитать до освобождения bmesh)
    bm.verts.index_update()
    ring_of = {}
    for i, ring in enumerate(rings):
        for v in ring: ring_of[v.index] = i
    ring_of[tip.index] = n - 1
    ring_of[base.index] = 0
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(o)
    for p in me.polygons: p.use_smooth = True
    attr = me.color_attributes.new("Col", 'FLOAT_COLOR', 'CORNER')
    # цвет по грани, а не по вершине — полосы получаются чёткими, без размытия между кольцами
    for poly in me.polygons:
        t = sum(ring_of[v] for v in poly.vertices) / len(poly.vertices) / (n - 1)
        c = lin(colors_fn(t))
        for li in poly.loop_indices:
            attr.data[li].color = (c[0], c[1], c[2], 1.0)
    return o

# ---------- Скелет ----------
TAIL_PTS = [(0.10, 0.16, 0.16), (0.24, 0.26, 0.20), (0.34, 0.34, 0.33), (0.37, 0.36, 0.50), (0.32, 0.32, 0.64), (0.24, 0.28, 0.72)]
TAIL_R = [0.06, 0.11, 0.14, 0.14, 0.12, 0.07]

BONES = [
    # имя, голова, хвост, родитель
    ("Root", (0, 0, 0), (0, 0, 0.1), None),
    ("Hips", (0, 0, 0.12), (0, 0, 0.34), "Root"),
    ("Spine", (0, 0, 0.34), (0, 0, 0.5), "Hips"),
    ("Head", (0, 0, 0.5), (0, 0, 0.92), "Spine"),
    ("Ear_L", (0.2, 0, 0.85), (0.32, 0, 0.96), "Head"),
    ("Ear_R", (-0.2, 0, 0.85), (-0.32, 0, 0.96), "Head"),
    ("Arm_L", (0.16, -0.04, 0.44), (0.21, -0.1, 0.28), "Spine"),
    ("Arm_R", (-0.16, -0.04, 0.44), (-0.21, -0.1, 0.28), "Spine"),
    ("Leg_L", (0.09, 0, 0.16), (0.09, -0.02, 0.0), "Hips"),
    ("Leg_R", (-0.09, 0, 0.16), (-0.09, -0.02, 0.0), "Hips"),
    ("Tail_1", TAIL_PTS[0], TAIL_PTS[2], "Hips"),
    ("Tail_2", TAIL_PTS[2], TAIL_PTS[3], "Tail_1"),
    ("Tail_3", TAIL_PTS[3], TAIL_PTS[4], "Tail_2"),
    ("Tail_4", TAIL_PTS[4], TAIL_PTS[5], "Tail_3"),
    # Сокеты одежды — точки крепления предметов (в Unity ищутся по имени).
    ("Socket_Head", (0, 0.0, 0.97), (0, 0.0, 1.07), "Head"),
    ("Socket_Face", (0, -0.27, 0.76), (0, -0.37, 0.76), "Head"),
    ("Socket_Neck", (0, -0.02, 0.5), (0, -0.12, 0.5), "Spine"),
    ("Socket_Back", (0, 0.1, 0.46), (0, 0.2, 0.46), "Spine"),
    ("Socket_Hand_R", (-0.21, -0.1, 0.28), (-0.21, -0.1, 0.18), "Arm_R"),
    ("Socket_Tail", TAIL_PTS[4], TAIL_PTS[5], "Tail_3"),
]

def build_armature():
    arm = bpy.data.armatures.new("PandaRig")
    obj = bpy.data.objects.new("PandaRig", arm)
    bpy.context.collection.objects.link(obj)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode='EDIT')
    for name, h, t, parent in BONES:
        b = arm.edit_bones.new(name)
        b.head, b.tail = h, t
        b.roll = 0
        if parent: b.parent = arm.edit_bones[parent]
        b.use_deform = not name.startswith("Socket")
    bpy.ops.object.mode_set(mode='OBJECT')
    return obj

# ---------- Тело ----------
def build_body(girl):
    parts = []  # (объект, кость или None для хвоста)
    P = lambda o, bone: parts.append((o, bone))

    # ноги и туловище
    P(capsule("LegL", DARK, (0.09, -0.01, 0.05), (0.09, 0, 0.16), 0.075), "Leg_L")
    P(capsule("LegR", DARK, (-0.09, -0.01, 0.05), (-0.09, 0, 0.16), 0.075), "Leg_R")
    P(sphere("FootL", DARK, (0.09, -0.05, 0.035), 0.07, (1, 1.3, 0.6)), "Leg_L")
    P(sphere("FootR", DARK, (-0.09, -0.05, 0.035), 0.07, (1, 1.3, 0.6)), "Leg_R")
    P(sphere("Body", DARK, (0, 0.01, 0.32), 0.2, (1.0, 0.9, 1.15)), "Hips")
    P(sphere("Chest", DARK, (0, -0.01, 0.46), 0.15, (1.05, 0.9, 0.8)), "Spine")
    P(capsule("ArmL", DARK, (0.16, -0.04, 0.43), (0.205, -0.1, 0.29), 0.055), "Arm_L")
    P(capsule("ArmR", DARK, (-0.16, -0.04, 0.43), (-0.205, -0.1, 0.29), 0.055), "Arm_R")

    # голова
    P(sphere("Head", FUR, (0, 0, 0.73), 0.26, (1.16, 1.0, 0.95), seg=32), "Head")
    P(sphere("CheekL", WHITE, (0.21, -0.1, 0.65), 0.1, (1.0, 0.85, 0.8)), "Head")
    P(sphere("CheekR", WHITE, (-0.21, -0.1, 0.65), 0.1, (1.0, 0.85, 0.8)), "Head")
    P(sphere("Muzzle", WHITE, (0, -0.2, 0.64), 0.095, (1.2, 0.85, 0.75)), "Head")
    P(sphere("Nose", NOSE, (0, -0.29, 0.675), 0.03, (1.3, 0.8, 0.9)), "Head")
    P(sphere("BrowL", WHITE, (0.1, -0.21, 0.865), 0.045, (1.35, 0.55, 0.6), rot=(0, -12, 0)), "Head")
    P(sphere("BrowR", WHITE, (-0.1, -0.21, 0.865), 0.045, (1.35, 0.55, 0.6), rot=(0, 12, 0)), "Head")
    eye_r = 0.07 if girl else 0.064
    for sx, side in ((1, "L"), (-1, "R")):
        # глаз: тёмная радужка, янтарный ободок, крупный блик
        P(sphere("EyeRim" + side, (0.55, 0.32, 0.1), (0.1 * sx, -0.212, 0.76), eye_r, (1, 0.45, 1.12)), "Head")
        P(sphere("Eye" + side, (0.05, 0.03, 0.02), (0.1 * sx, -0.222, 0.762), eye_r * 0.78, (1, 0.45, 1.12)), "Head")
        P(sphere("EyeShine" + side, WHITE, (0.082 * sx, -0.25, 0.79), 0.02, (1, 0.5, 1)), "Head")
        P(sphere("EyeShine2" + side, WHITE, (0.115 * sx, -0.248, 0.735), 0.009, (1, 0.5, 1)), "Head")
        # уши торчат вбок из-под полей шляпы: рыжие, изнанка белая
        P(cone("Ear" + side, FUR, (0.25 * sx, 0.0, 0.9), 0.1, 0.02, 0.18, rot=(0, 52 * sx, 0), scale=(1, 0.55, 1)), "Ear_" + side)
        P(cone("EarIn" + side, WHITE, (0.248 * sx, -0.03, 0.897), 0.075, 0.01, 0.14, rot=(0, 52 * sx, 0), scale=(1, 0.35, 1)), "Ear_" + side)
        if girl:
            # ресницы — тёмные клинышки у внешнего края глаза, румянец на щеках
            P(cone("Lash" + side, DARK, (0.15 * sx, -0.215, 0.805), 0.012, 0.002, 0.05, rot=(0, -50 * sx, 0)), "Head")
            P(sphere("Blush" + side, BLUSH, (0.2 * sx, -0.19, 0.69), 0.04, (1.2, 0.35, 0.7)), "Head")

    # хвост: кольца рыжий/кремовый
    pts, radii = smooth_path(TAIL_PTS, TAIL_R, 48)
    tail = tube("Tail", pts, radii, lambda t: CREAM if int(t * 7) % 2 == 1 and t < 0.93 else FUR, ring_segments=20)
    P(tail, None)
    return parts

def skin(parts, rig, name):
    """Склеить части в один меш и назначить веса: части — целиком своей кости, хвост — по цепочке."""
    tail_bones = ["Tail_1", "Tail_2", "Tail_3", "Tail_4"]
    seg_pts = [Vector(TAIL_PTS[i]) for i in (0, 2, 3, 4, 5)]
    for obj, bone in parts:
        if bone:
            obj.vertex_groups.new(name=bone).add([v.index for v in obj.data.vertices], 1.0, 'REPLACE')
        else:
            groups = {b: obj.vertex_groups.new(name=b) for b in tail_bones}
            for v in obj.data.vertices:
                # ближайший сегмент цепочки + плавный переход к соседнему
                best, bi, bt = 1e9, 0, 0.0
                for i in range(4):
                    a, b = seg_pts[i], seg_pts[i + 1]
                    ab = b - a
                    t = max(0.0, min(1.0, (v.co - a).dot(ab) / ab.length_squared))
                    d = (a + ab * t - v.co).length
                    if d < best: best, bi, bt = d, i, t
                w_next = max(0.0, (bt - 0.6) / 0.4) * 0.5 if bi < 3 else 0.0
                groups[tail_bones[bi]].add([v.index], 1.0 - w_next, 'REPLACE')
                if w_next > 0: groups[tail_bones[bi + 1]].add([v.index], w_next, 'REPLACE')
    bpy.ops.object.select_all(action='DESELECT')
    for obj, _ in parts: obj.select_set(True)
    bpy.context.view_layer.objects.active = parts[0][0]
    bpy.ops.object.join()
    body = bpy.context.object
    body.name = name
    body.parent = rig
    mod = body.modifiers.new("Armature", 'ARMATURE')
    mod.object = rig
    return body

# ---------- Предметы ----------
# Каждый предмет строится в координатах персонажа, затем origin переносится в точку сокета.
SOCKETS = {name: Vector(h) for name, h, t, p in BONES if name.startswith("Socket")}

def join_item(objs, name, socket):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    if len(objs) > 1: bpy.ops.object.join()
    item = bpy.context.object
    item.name = name
    bpy.context.scene.cursor.location = SOCKETS[socket]
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    item.location = (0, 0, 0)
    return item

def item_hat_wanderer():
    o = [cylinder("Brim", STRAW, (0, 0, 0.99), 0.4, 0.025, verts=40),
         cone("Crown", STRAW, (0, 0.01, 1.16), 0.2, 0.015, 0.34, rot=(-6, 0, 0), verts=32),
         cylinder("Band", TEAL, (0, 0, 1.045), 0.185, 0.05, verts=32)]
    return join_item(o, "hat_wanderer", "Socket_Head")

def item_hat_witch():
    o = [cylinder("Brim", IVORY, (0, 0, 0.99), 0.36, 0.022, verts=40),
         cone("CrownLow", IVORY, (0, 0.0, 1.12), 0.19, 0.1, 0.24, verts=32),
         cone("CrownTip", IVORY, (0.05, 0.03, 1.33), 0.1, 0.012, 0.24, rot=(-10, 22, 0), verts=24),
         cylinder("Band", PINK, (0, 0, 1.03), 0.185, 0.045, verts=32),
         sphere("Flower", PINK, (0.12, -0.14, 1.05), 0.04, (1, 0.6, 1)),
         sphere("FlowerCore", (1.0, 0.85, 0.4), (0.12, -0.165, 1.05), 0.015)]
    return join_item(o, "hat_witch", "Socket_Head")

def jagged_cape(name, color, top_r, bottom_r, z_top, z_bottom, teeth, front_open=True):
    """Плащ-конус с рваным краем «листьями». Открыт спереди, чтобы не закрывать живот."""
    bm = bmesh.new()
    seg = teeth * 2
    start, end = (math.radians(35), math.radians(325)) if front_open else (0, 2 * math.pi)
    top, bottom = [], []
    for k in range(seg + 1):
        a = start + (end - start) * k / seg
        # угол 0 = назад (+Y), обходим через бока
        dx, dy = math.sin(a), math.cos(a)
        top.append(bm.verts.new((dx * top_r, dy * top_r, z_top)))
        drop = 0.06 if k % 2 == 0 else 0.0  # зубцы
        bottom.append(bm.verts.new((dx * bottom_r, dy * bottom_r, z_bottom - drop)))
    for k in range(seg):
        bm.faces.new((top[k], top[k + 1], bottom[k + 1], bottom[k]))
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(o)
    o.modifiers.new("Solid", 'SOLIDIFY').thickness = 0.015
    bpy.context.view_layer.objects.active = o
    o.select_set(True)
    bpy.ops.object.modifier_apply(modifier="Solid")
    o.select_set(False)
    for p in me.polygons: p.use_smooth = True
    paint(o, color)
    return o

def item_scarf_teal():
    o = [torus("Wrap", TEAL, (0, -0.01, 0.51), 0.15, 0.045, scale=(1.05, 0.95, 1)),
         jagged_cape("Cape", TEAL, 0.17, 0.25, 0.5, 0.38, 7),
         capsule("Tail", TEAL, (0.07, -0.15, 0.49), (0.1, -0.18, 0.38), 0.03)]
    return join_item(o, "scarf_teal", "Socket_Neck")

def item_cape_cream():
    o = [torus("Collar", IVORY, (0, -0.01, 0.51), 0.15, 0.035, scale=(1.05, 0.95, 1)),
         jagged_cape("Cape", IVORY, 0.17, 0.26, 0.5, 0.37, 6)]
    return join_item(o, "cape_cream", "Socket_Neck")

def item_bow_pink():
    o = [sphere("Knot", PINK, (0, -0.15, 0.5), 0.03, (1, 0.8, 1)),
         sphere("LoopL", PINK, (0.06, -0.15, 0.51), 0.05, (1.2, 0.4, 0.8), rot=(0, 20, 0)),
         sphere("LoopR", PINK, (-0.06, -0.15, 0.51), 0.05, (1.2, 0.4, 0.8), rot=(0, -20, 0)),
         capsule("RibbonL", PINK, (0.01, -0.16, 0.49), (0.04, -0.17, 0.4), 0.014),
         capsule("RibbonR", PINK, (-0.01, -0.16, 0.49), (-0.04, -0.17, 0.4), 0.014)]
    return join_item(o, "bow_pink", "Socket_Neck")

def item_staff_lantern():
    h = SOCKETS["Socket_Hand_R"]
    o = [capsule("Stick", WOOD, (h.x - 0.02, h.y - 0.02, 0.02), (h.x + 0.03, h.y - 0.02, 0.78), 0.018),
         capsule("Twig", WOOD, (h.x + 0.03, h.y - 0.02, 0.72), (h.x - 0.06, h.y - 0.03, 0.8), 0.01),
         cylinder("LampTop", LANTERN_RED, (h.x - 0.08, h.y - 0.03, 0.7), 0.035, 0.02),
         cylinder("LampBottom", LANTERN_RED, (h.x - 0.08, h.y - 0.03, 0.6), 0.04, 0.02),
         cylinder("LampGlass", GLOW, (h.x - 0.08, h.y - 0.03, 0.65), 0.03, 0.08, glow=True),
         cone("LampRoof", LANTERN_RED, (h.x - 0.08, h.y - 0.03, 0.725), 0.04, 0.01, 0.035)]
    return join_item(o, "staff_lantern", "Socket_Hand_R")

def item_wand_star():
    h = SOCKETS["Socket_Hand_R"]
    tip = (h.x - 0.05, h.y - 0.08, h.z + 0.24)
    o = [capsule("Stick", WOOD, (h.x + 0.01, h.y + 0.01, h.z - 0.03), tip, 0.011)]
    # звёздочка: 5 лучей
    for k in range(5):
        a = math.radians(90 + 72 * k)
        o.append(capsule("Ray%d" % k, GLOW, tip, (tip[0] + math.cos(a) * 0.05, tip[1] - 0.005, tip[2] + math.sin(a) * 0.05), 0.013))
        paint(o[-1], GLOW, glow=True)
    return join_item(o, "wand_star", "Socket_Hand_R")

ITEMS = [item_hat_wanderer, item_hat_witch, item_scarf_teal, item_cape_cream, item_bow_pink, item_staff_lantern, item_wand_star]

# ---------- Экспорт ----------
def export_fbx(path, objs):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, apply_scale_options='FBX_SCALE_ALL',
                             axis_forward='-Z', axis_up='Y', bake_space_transform=False,
                             add_leaf_bones=False, primary_bone_axis='Y', secondary_bone_axis='X',
                             mesh_smooth_type='FACE', colors_type='LINEAR', bake_anim=False,
                             object_types={'ARMATURE', 'MESH'})

def preview(path, objs):
    """Быстрый рендер Workbench с вертекс-цветами — чтобы проверить форму без Unity."""
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.display.shading.light = 'STUDIO'
    scene.display.shading.color_type = 'VERTEX'
    scene.display.shading.show_object_outline = True
    scene.render.resolution_x, scene.render.resolution_y = 600, 700
    scene.render.film_transparent = False
    world = bpy.data.worlds.new("W"); scene.world = world
    world.color = (0.2, 0.35, 0.35)
    cam_data = bpy.data.cameras.new("Cam")
    cam = bpy.data.objects.new("Cam", cam_data)
    bpy.context.collection.objects.link(cam)
    scene.camera = cam
    cam.location = (0.9, -2.2, 0.85)
    cam.rotation_euler = (math.radians(86), 0, math.radians(22))
    cam_data.lens = 50
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)

os.makedirs(os.path.join(OUT, "Items"), exist_ok=True)
if PREVIEW: os.makedirs(PREVIEW, exist_ok=True)

for girl, name, outfit in ((False, "panda_m", [item_hat_wanderer, item_scarf_teal, item_staff_lantern]),
                           (True, "panda_f", [item_hat_witch, item_cape_cream, item_bow_pink, item_wand_star])):
    reset()
    rig = build_armature()
    body = skin(build_body(girl), rig, name + "_body")
    export_fbx(os.path.join(OUT, name + ".fbx"), [rig, body])
    if PREVIEW:
        # надеть наряд: предмет стоит origin-ом в точке своего сокета
        for w in [f() for f in outfit]:
            sock = {"hat_wanderer": "Socket_Head", "hat_witch": "Socket_Head", "scarf_teal": "Socket_Neck",
                    "cape_cream": "Socket_Neck", "bow_pink": "Socket_Neck", "staff_lantern": "Socket_Hand_R",
                    "wand_star": "Socket_Hand_R"}[w.name]
            w.location = SOCKETS[sock]
        preview(os.path.join(PREVIEW, name + ".png"), [])

for make in ITEMS:
    reset()
    item = make()
    export_fbx(os.path.join(OUT, "Items", item.name + ".fbx"), [item])

print("LOVEPANDAS_BUILD_OK")
