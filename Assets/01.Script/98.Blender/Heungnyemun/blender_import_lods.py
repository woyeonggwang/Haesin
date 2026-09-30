"""Blender 안에서 실행: 흥례문 단순화판 메시 묶음(meshes.bin, 'HNMS')을 컬렉션으로 가져온다.

사용법(블렌더 Python 콘솔 또는 MCP):
  import sys; sys.path.append(r'<이 파일 폴더>'); import blender_import_lods as bil
  bil.run(r'D:\\Blender\\Enviroment\\PalaceJoseon\\FourDoor\\Source\\Heungnyemun\\LOD\\meshes.bin', lods=(0, 1, 2), parent='Heungnyemun')
- 메시 이름 HNM_<부분>_LOD<n> → 컬렉션 Heungnyemun_LOD<n> (이미 있으면 같은 이름 메시를 새로 만들어 바꿔 끼움)
- 좌표: Unity (x, y, z) → Blender (−x, −z, y), 삼각형 감김 (0, 2, 1). 법선은 파일 값을 사용자 법선으로.
- 재질은 이름으로 찾고(MI_HNM_*, CARD_HNM_*), 없으면 MAT_RoofPlate_Black 은 검은 재질로 새로 만든다.
- LOD1·LOD2 컬렉션은 보기에서 뺀다(exclude) — 켜서 보면 됨.
"""
import struct
import numpy as np
import bpy


def read_meshes(path):
    buf = open(path, 'rb').read(); off = 0

    def i32():
        nonlocal off
        v = struct.unpack_from('<i', buf, off)[0]; off += 4; return v
    assert i32() == 0x534D4E48
    n = i32(); out = []
    for _ in range(n):
        ln = i32(); name = buf[off:off + ln].decode('utf-8'); off += ln
        nv = i32(); ns = i32()
        pos = np.frombuffer(buf, '<f4', nv * 3, off).reshape(-1, 3); off += nv * 12
        nrm = np.frombuffer(buf, '<f4', nv * 3, off).reshape(-1, 3); off += nv * 12
        uv = np.frombuffer(buf, '<f4', nv * 2, off).reshape(-1, 2); off += nv * 8
        subs = []; mats = []
        for _ in range(ns):
            lm = i32(); mats.append(buf[off:off + lm].decode('utf-8')); off += lm
            ni = i32(); subs.append(np.frombuffer(buf, '<i4', ni, off)); off += ni * 4
        out.append((name, dict(pos=pos, nrm=nrm, uv=uv, subs=subs, mats=mats)))
    return out


def black_material():
    m = bpy.data.materials.get('MAT_RoofPlate_Black')
    if m is None:
        m = bpy.data.materials.new('MAT_RoofPlate_Black'); m.use_nodes = True
        bs = m.node_tree.nodes.get('Principled BSDF')
        bs.inputs['Base Color'].default_value = (0.02, 0.02, 0.02, 1); bs.inputs['Roughness'].default_value = 0.8
        m.diffuse_color = (0.02, 0.02, 0.02, 1)
    return m


def build(name, d):
    P = d['pos'].astype(np.float64); N = d['nrm'].astype(np.float64)
    B = np.stack([-P[:, 0], -P[:, 2], P[:, 1]], 1).astype(np.float32)
    NB = np.stack([-N[:, 0], -N[:, 2], N[:, 1]], 1).astype(np.float32)
    T = []; M = []
    for k, s in enumerate(d['subs']):
        t = np.asarray(s, np.int64).reshape(-1, 3)[:, [0, 2, 1]]
        T.append(t); M.append(np.full(len(t), k, np.int32))
    T = np.concatenate(T); M = np.concatenate(M)
    me = bpy.data.meshes.new(name)
    me.vertices.add(len(B)); me.vertices.foreach_set('co', B.ravel())
    me.loops.add(len(T) * 3); me.loops.foreach_set('vertex_index', T.astype(np.int32).ravel())
    me.polygons.add(len(T)); me.polygons.foreach_set('loop_start', (np.arange(len(T)) * 3).astype(np.int32))
    me.polygons.foreach_set('material_index', M)
    uvl = me.uv_layers.new(name='UVMap'); uvl.data.foreach_set('uv', np.asarray(d['uv'], np.float32)[T.ravel()].ravel())
    missing = []
    for mn in d['mats']:
        m = bpy.data.materials.get(mn)
        if m is None and mn == 'MAT_RoofPlate_Black':
            m = black_material()
        if m is None:
            missing.append(mn); m = bpy.data.materials.new(mn)
        me.materials.append(m)
    me.update(calc_edges=True)
    me.validate(clean_customdata=False)
    me.polygons.foreach_set('use_smooth', np.ones(len(T), bool))
    me.normals_split_custom_set_from_vertices([tuple(v) for v in NB])
    me.update()
    return me, missing


def run(path, lods=(0, 1, 2), parent='Heungnyemun', only=None):
    data = read_meshes(path)
    par = bpy.data.objects.get(parent) if parent else None
    log = []; allmiss = set()
    for name, d in data:
        lod = int(name[-1])
        if lod not in lods or (only and name not in only):
            continue
        cn = 'Heungnyemun_LOD%d' % lod
        col = bpy.data.collections.get(cn)
        if col is None:
            col = bpy.data.collections.new(cn); bpy.context.scene.collection.children.link(col)
        me, miss = build(name + '__new', d); allmiss |= set(miss)
        ob = bpy.data.objects.get(name)
        if ob is None:
            ob = bpy.data.objects.new(name, me); col.objects.link(ob)
            if par is not None:
                ob.parent = par; ob.matrix_parent_inverse = par.matrix_world.inverted()
        else:
            old = ob.data; ob.data = me
            if old.users == 0:
                bpy.data.meshes.remove(old)
        me.name = name
        log.append('%s tris=%d' % (name, len(me.polygons)))
    for lod in lods:
        lc = bpy.context.view_layer.layer_collection.children.get('Heungnyemun_LOD%d' % lod)
        if lc is not None:
            lc.exclude = lod != 0
    if allmiss:
        log.append('MISSING MATERIALS ' + ','.join(sorted(allmiss)))
    return log
