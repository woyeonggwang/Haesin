"""Blender 안에서 실행: 판(카드) 데이터를 블렌더 파일의 줄인 메시에 반영한다.

입력 폴더(단지마다, 예: D:\\Blender\\Enviroment\\PalaceJoseon\\FourDoor\\Source\\Heungnyemun):
  cards.bin    바뀐 메시(meshes.bin 형식, Unity 좌표). 이름 = <메시>#CARD(통째 판) / <메시>#SPLIT(틀 + 판) (+ '#F' = 각진 음영)
  cards.tsv    k, name, kind(full/split), style, mode, material(CARD_<PRE>_<k>), texture, clip, keep_subs, info
  removed.tsv  문짝 판에 구워 넣어 지울 쇠붙이 배치(오브젝트 이름) — 비어 있을 수 있음
  Cards/*.png  판 텍스처

블렌더 파일 규칙(FourDoor_Origin.blend, GwanghwamunComplex_Origin.blend):
  줄인 메시 = <PRE>_R_<k:03d>_<메시 이름>, 판으로 바꾼 메시 = <PRE>_C_<k:03d>_<메시 이름>
  (PRE = HNM 흥례문 / GCM 건춘문 / YCM 영추문 / SMM 신무문 / GHC 광화문 부속단지)
  좌표: Unity (x, y, z) → Blender (−x, −z, y), 삼각형 감김 (0, 2, 1)

사용법(블렌더 Python 콘솔 또는 MCP):
  import sys; sys.path.append(r'<이 파일이 있는 폴더>')
  import blender_apply_cards as bac
  bac.apply(r'D:\\Blender\\Enviroment\\PalaceJoseon\\FourDoor\\Source\\Heungnyemun', 'HNM')
이미 판으로 바꾼 메시(<PRE>_C_...)도 다시 만든다(PNG 를 다시 구운 뒤 다시 적용할 때). 저장은 따로 한다.
"""
import os, struct
import numpy as np
import bpy


def read_bin(path):
    """meshes.bin 형식(매직 0x31584250, 리틀 엔디언) 읽기."""
    buf = open(path, 'rb').read()
    off = 0

    def i32():
        nonlocal off
        v = struct.unpack_from('<i', buf, off)[0]; off += 4; return v
    magic = i32(); n = i32(); out = []
    for _ in range(n):
        ln = i32(); name = buf[off:off + ln].decode('utf-8'); off += ln
        nv = i32(); ns = i32()
        pos = np.frombuffer(buf, '<f4', nv * 3, off).reshape(-1, 3); off += nv * 12
        nrm = np.frombuffer(buf, '<f4', nv * 3, off).reshape(-1, 3); off += nv * 12
        uv = np.frombuffer(buf, '<f4', nv * 2, off).reshape(-1, 2); off += nv * 8
        subs = []
        for _ in range(ns):
            ni = i32(); subs.append(np.frombuffer(buf, '<i4', ni, off).copy()); off += ni * 4
        out.append(dict(name=name, pos=pos, nrm=nrm, uv=uv, subs=subs))
    return out


def read_tsv(path):
    rows = []
    if not os.path.isfile(path):
        return rows
    lines = open(path, encoding='utf-8').read().splitlines()
    if not lines:
        return rows
    head = lines[0].split('\t')
    for l in lines[1:]:
        if l.strip():
            rows.append(dict(zip(head, l.split('\t'))))
    return rows


def card_material(name, img_abs, clip):
    """판 머티리얼: 이미지 색 + 알파 0.5 컷(점묘), 한쪽 면, 거칠기 0.88. 이미지 경로는 블렌더 파일 기준 상대 경로."""
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new('ShaderNodeOutputMaterial'); out.location = (400, 0)
    bs = nt.nodes.new('ShaderNodeBsdfPrincipled'); bs.location = (100, 0)
    bs.inputs['Roughness'].default_value = 0.88
    tex = nt.nodes.new('ShaderNodeTexImage'); tex.location = (-400, 0)
    img = None
    for im in bpy.data.images:
        if im.filepath and os.path.normcase(bpy.path.abspath(im.filepath)) == os.path.normcase(img_abs):
            img = im; break
    if img is None:
        img = bpy.data.images.load(img_abs)
    else:
        img.reload()          # PNG 를 바꿨으면 다시 읽어야 보인다
    try:
        img.filepath = bpy.path.relpath(img_abs)
    except ValueError:
        pass                  # 드라이브가 다르면 절대 경로 그대로
    tex.image = img
    nt.links.new(tex.outputs['Color'], bs.inputs['Base Color'])
    mth = nt.nodes.new('ShaderNodeMath'); mth.location = (-150, -200)
    mth.operation = 'GREATER_THAN'; mth.inputs[1].default_value = 0.5
    nt.links.new(tex.outputs['Alpha'], mth.inputs[0])
    nt.links.new(mth.outputs[0], bs.inputs['Alpha'])
    nt.links.new(bs.outputs['BSDF'], out.inputs['Surface'])
    try:
        m.surface_render_method = 'DITHERED'     # Blender 4.2+
    except Exception:
        pass
    try:
        m.blend_method = 'HASHED'
    except Exception:
        pass
    m.use_backface_culling = True
    return m


def build_mesh(name, d, flat, mats):
    """cards.bin 메시 하나 → 블렌더 메시(서브메시 = 머티리얼 칸)."""
    P = np.asarray(d['pos'], np.float64)
    B = np.stack([-P[:, 0], -P[:, 2], P[:, 1]], 1)
    tris = []; mid = []
    for k, s in enumerate(d['subs']):
        t = np.asarray(s, np.int64).reshape(-1, 3)[:, [0, 2, 1]]
        tris.append(t); mid.append(np.full(len(t), k, np.int32))
    T = np.concatenate(tris) if tris else np.zeros((0, 3), np.int64)
    M = np.concatenate(mid) if mid else np.zeros(0, np.int32)
    me = bpy.data.meshes.new(name)
    me.vertices.add(len(B)); me.vertices.foreach_set('co', B.astype(np.float32).ravel())
    me.loops.add(len(T) * 3); me.loops.foreach_set('vertex_index', T.astype(np.int32).ravel())
    me.polygons.add(len(T)); me.polygons.foreach_set('loop_start', (np.arange(len(T)) * 3).astype(np.int32))
    me.polygons.foreach_set('material_index', M)
    uvl = me.uv_layers.new(name='UVMap')
    uvl.data.foreach_set('uv', np.asarray(d['uv'], np.float32)[T.ravel()].ravel())
    for m in mats:
        me.materials.append(m)
    me.update(calc_edges=True)
    me.validate(clean_customdata=False)
    if flat:
        me.polygons.foreach_set('use_smooth', np.zeros(len(T), bool))
    else:
        me.polygons.foreach_set('use_smooth', np.ones(len(T), bool))
        N = np.asarray(d['nrm'], np.float64)
        NB = np.stack([-N[:, 0], -N[:, 2], N[:, 1]], 1)
        l = np.linalg.norm(NB, axis=1); l[l < 1e-12] = 1
        NB = (NB / l[:, None]).astype(np.float32)
        me.normals_split_custom_set_from_vertices([tuple(v) for v in NB])
    me.update()
    return me


def apply(src_dir, pre, only=None):
    """src_dir 의 cards.* 를 <pre>_R_/<pre>_C_ 메시에 반영. only = 메시 이름 목록(없으면 전부). 돌려주는 값 = 로그 줄 목록."""
    rows = read_tsv(os.path.join(src_dir, 'cards.tsv'))
    data = read_bin(os.path.join(src_dir, 'cards.bin'))
    if len(rows) != len(data):
        raise RuntimeError('cards.tsv %d 줄 / cards.bin %d 메시가 다름' % (len(rows), len(data)))
    log = []
    for r, d in zip(rows, data):
        k = int(r['k']); name = r['name']
        if only and name not in only:
            continue
        rn = '%s_R_%03d_%s' % (pre, k, name); cn = '%s_C_%03d_%s' % (pre, k, name)
        cur = bpy.data.meshes.get(cn) or bpy.data.meshes.get(rn)
        if cur is None:
            log.append('MISS ' + rn); continue
        flat = d['name'].endswith('#F') or r['kind'] == 'full'
        cmat = card_material(r['material'], os.path.join(src_dir, 'Cards', r['texture']), r['clip'] == '1')
        if r['kind'] == 'split':
            if cur.name == cn:
                kept = list(cur.materials)[:-1]
            else:
                keep = [int(x) for x in r['keep_subs'].split(',') if x != '']
                kept = [cur.materials[i] for i in keep]
            mats = kept + [cmat]
        else:
            mats = [cmat]
        if len(mats) != len(d['subs']):
            log.append('SUBS %s mats=%d subs=%d' % (name, len(mats), len(d['subs']))); continue
        new = build_mesh(cn + '__new', d, flat, mats)
        users = [o for o in bpy.data.objects if o.type == 'MESH' and o.data == cur]
        for o in users:
            o.data = new
        bpy.data.meshes.remove(cur)
        new.name = cn
        log.append('OK %s objs=%d tris=%d' % (cn, len(users), len(new.polygons)))
    # removed.tsv(머리줄 없음) = instances.tsv 줄 번호, 메시 번호, 이름 → 그 배치 위치의 오브젝트를 지운다
    rp = os.path.join(src_dir, 'removed.tsv')
    rem = [l.split('\t') for l in open(rp, encoding='utf-8').read().splitlines() if l.strip()] if os.path.isfile(rp) else []
    if rem:
        inst = open(os.path.join(src_dir, 'instances.tsv'), encoding='utf-8').read().splitlines()
        for ai, k, name in rem:
            c = inst[int(ai)].split('\t')
            m = [float(x) for x in c[2:18]]
            want = np.array([-m[3], -m[11], m[7]])            # Unity 이동 (m03, m13, m23) → Blender (−x, −z, y)
            k = int(k); hit = None
            for mn in ('%s_R_%03d_%s' % (pre, k, name), '%s_C_%03d_%s' % (pre, k, name)):
                me = bpy.data.meshes.get(mn)
                if me is None:
                    continue
                for o in bpy.data.objects:
                    if o.type == 'MESH' and o.data == me and np.linalg.norm(np.array(o.matrix_world.translation) - want) < 1e-3:
                        hit = o; break
                if hit:
                    break
            if hit is None:
                log.append('DEL-MISS %s %s' % (ai, name)); continue
            log.append('DEL ' + hit.name)
            bpy.data.objects.remove(hit, do_unlink=True)
    return log
