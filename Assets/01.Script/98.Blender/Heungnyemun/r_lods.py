import sys, time; sys.path.insert(0,'.')
from scene import *
from load import load
from export import read_meshes
O='/tmp/claude-0/-home-claude/d3523ed0-164d-5ce5-81f3-4f3efb3b93be/scratchpad/hnm/'
inst, mats, red = load()
ms = read_meshes('work/out/meshes.bin')
doors = read_meshes('work/out/doors.bin')
def scene_lod(lod, doors_on=True):
    sc = Scene(); sc.cardtex = card_info(red)
    sc.cardtex['CARD_HNM_149'] = ('/home/claude/palace_simplify/hnm/work/cards/HNM_149_SM_HNM_PatternCeilingA_card.png', True)
    for name, m in ms:
        if not name.endswith('LOD%d' % lod): continue
        sc.add(m['pos'], m['uv'], m['subs'], m['mats'], -1)
    if doors_on:
        dm = dict(doors)
        names = [red[i['k']]['name'].split('#')[0] for i in inst]
        for ii, i in enumerate(inst):
            if names[ii] in dm:
                m = dm[names[ii]]; W = (np.concatenate([m['pos'], np.ones((len(m['pos']),1))],1) @ i['M'].T)[:, :3]
                subs = m['subs']
                if np.linalg.det(i['M'][:3,:3]) < 0: subs = [np.asarray(s).reshape(-1,3)[:, [0,2,1]].ravel() for s in subs]
                sc.add(W, m['uv'], subs, m['mats'], ii)
    return sc.finish()
if __name__ == '__main__':
    views = [(a.split(':')[0], [float(x) for x in a.split(':')[1].split(',')], [float(x) for x in a.split(':')[2].split(',')], float(a.split(':')[3])) for a in sys.argv[2:]]
    for lod in [int(x) for x in sys.argv[1].split(',')]:
        sc = scene_lod(lod); rm = RMesh(sc.P, sc.T, sc.U, sc.TM)
        for (k, cam, look, fov) in views:
            render(sc, cam, look, O + 'L%d_%s.png' % (lod, k), fov, 800, 500, rm=rm)
        rm.free(); print('lod', lod, 'tris', len(sc.T))
