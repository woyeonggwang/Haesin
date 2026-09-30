import sys, time; sys.path.insert(0,'.')
from scene import *
from load import load
from parts import *
t0=time.time()
inst, mats, red = load()
names = [red[i['k']]['name'].split('#')[0] for i in inst]
keep = load_vis(inst, red, names, 'work')
parts = collect(inst, mats, red, keep)
print('collected %.0fs'%(time.time()-t0), {p: sum(len(c[3]) for c in parts[p]) for p in parts})
def scene_from(parts, with_doors=True, only=None):
    sc = Scene(); sc.cardtex = card_info(red)
    sc.cardtex['CARD_HNM_149'] = ('/home/claude/palace_simplify/hnm/work/cards/HNM_149_SM_HNM_PatternCeilingA_card.png', True)
    for p, chunks in parts.items():
        if only and p not in only: continue
        for (W, N, UV, tt, mn, flat, ii, s) in chunks:
            if p == 'GROUND' and names[ii] == 'SM_HNM_Ground': continue
            sc.add(W, UV, [tt], [mn], ii)
    if with_doors:
        for ii, i in enumerate(inst):
            if DOOR_RE.match(names[ii]):
                d = red[i['k']]; W = xform(i['M'], d['pos'].astype(np.float64))
                sc.add(W, d['uv'], d['subs'], instance_mats(i, d), ii)
    return sc.finish()
if __name__ == "__main__":
    O='/tmp/claude-0/-home-claude/d3523ed0-164d-5ce5-81f3-4f3efb3b93be/scratchpad/hnm/'
    sc = scene_from(parts)
    rm = RMesh(sc.P, sc.T, sc.U, sc.TM)
    print('scene tris', len(sc.T))
    views = {'c_ext_s': ((0,6,22),(0,8,0),60), 'c_ext_n': ((0,6,-22),(0,8,0),60), 'c_bay_w': ((-7.5,1.7,-10),(-7,4.5,-2),75),
             'c_up': ((-4.5,1.7,-3.0),(-4.3,8,-0.9),80), 'c_top_gate': ((-20,30,25),(0,8,0),50), 'c_hg_in': ((-58,1.7,-20),(-58,4,-40),80),
             'c_roof_hg': ((-30,16,12),(-45,6,-5),60), 'c_passage_up': ((0,1.7,-2),(0,7,1),85)}
    for k,(cam,look,fov) in views.items():
        render(sc, cam, look, O+k+'.png', fov, 800, 520, rm=rm)
    print('done %.0fs'%(time.time()-t0))
