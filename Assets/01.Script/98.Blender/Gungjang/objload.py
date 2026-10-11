import numpy as np
def load_obj(path):
    V=[]; VT=[]; VN=[]; faces=[]; mat=None
    for line in open(path, encoding='utf-8', errors='replace'):
        if line.startswith('v '):
            p=line.split(); V.append((float(p[1]),float(p[2]),float(p[3])))
        elif line.startswith('vt '):
            p=line.split(); VT.append((float(p[1]),float(p[2])))
        elif line.startswith('vn '):
            p=line.split(); VN.append((float(p[1]),float(p[2]),float(p[3])))
        elif line.startswith('usemtl'):
            mat=line.split(None,1)[1].strip()
        elif line.startswith('f '):
            idx=[]
            for tok in line.split()[1:]:
                a=tok.split('/')
                vi=int(a[0]); ti=int(a[1]) if len(a)>1 and a[1] else 0; ni=int(a[2]) if len(a)>2 and a[2] else 0
                idx.append((vi,ti,ni))
            for k in range(1,len(idx)-1):
                faces.append((mat, idx[0], idx[k], idx[k+1]))
    return np.array(V), np.array(VT), np.array(VN), faces
