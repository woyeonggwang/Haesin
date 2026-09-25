using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 캐릭터의 옷 / 신발 / 머리카락을 "스킨처럼" 갈아입히는 컴포넌트.
///
/// 동작 방식 (본 이름 재연결 = stitching):
///  - 옷 FBX 는 몸과 같은 뼈대(Haesin_Armature, 76본: 기본 52 + 치마 24)로 스키닝되어 있다.
///  - 옷 프리팹을 복제한 뒤 SkinnedMeshRenderer.bones 를 캐릭터 뼈대의 "같은 이름" 본으로 바꿔 끼우고,
///    옷 프리팹에 딸려 온 뼈대 사본은 버린다. → 애니메이션은 캐릭터 뼈대 하나만 움직이면 된다.
///  - 캐릭터 뼈대에 없는 본(예: 머리카락 Hair_Armature/Hair_Root~05)은 "가장 가까운 이름이 같은 조상 본"
///    아래로 통째로 옮겨 붙인다(graft). 머리카락은 Blender 에서 Head 본 자식이므로 Head 밑에 붙는다.
///    예전에는 머리카락 본을 몸 뼈대에 합쳤으나, 머리스타일마다 본 구성이 달라 교체형에 맞지 않아 분리함.
///
/// 몸 부위 숨기기 (옷 속으로 피부가 비치는 문제 방지):
///  - 몸은 모듈로 나뉜다: Body(기본) / Body_Foot_L·R(맨발) / Body_UnderTop(한복 상의가 딱 붙어 덮는 몸통·어깨).
///  - 아이템마다 hideBodyParts 에 적힌 모듈을 입어 있는 동안 끈다. (신발 → 맨발, 한복 → Body_UnderTop)
///    가려진 피부는 그리지 않으므로 팔을 흔들 때 등·겨드랑이 피부가 옷을 뚫고 나오는 일이 없다.
///  - 다른 옷(예: 짧은 소매)은 가리는 범위가 다르므로 그 옷에 맞는 모듈을 Blender 에서 따로 떼서 넣고
///    그 이름을 hideBodyParts 에 적는다.
///
/// 에디터 미리 입히기:
///  - 인스펙터 우클릭(⋮) 메뉴 "시작 장비 미리 입히기" 로 씬에 입혀 두고 저장할 수 있다.
///    입힌 상태는 equippedRecords 에 저장되며, 플레이 때는 이미 입은 슬롯을 다시 입히지 않는다.
/// </summary>
public class CharacterOutfit : MonoBehaviour
{
    public enum Slot { Clothes, Shoes, Hair }

    [System.Serializable]
    public class StartItem
    {
        public Slot slot;
        public GameObject prefab;
        [Tooltip("이 아이템을 입으면 끌 몸 모듈 이름 (캐릭터 바로 아래 자식). 예: 신발 → Body_Foot_L, Body_Foot_R / 한복 → Body_UnderTop")]
        public string[] hideBodyParts;
    }

    /// <summary>입힌 상태 기록. 씬에 저장되므로 에디터에서 입혀 둔 옷도 플레이 때 그대로 쓴다.</summary>
    [System.Serializable]
    public class EquippedRecord
    {
        public Slot slot;
        public GameObject source;
        public List<GameObject> renderers = new List<GameObject>();
        public List<GameObject> graftedRoots = new List<GameObject>();
        public List<string> graftedNames = new List<string>();
        public List<string> hiddenBodyParts = new List<string>();
    }

    [Tooltip("캐릭터 뼈대 루트(Hips 의 부모). 비우면 자식 중 'Hips' 를 찾아 그 부모를 쓴다")]
    public Transform skeletonRoot;

    [Tooltip("시작할 때 입힐 아이템 (슬롯마다 하나)")]
    public List<StartItem> startItems = new List<StartItem>();

    [SerializeField] List<EquippedRecord> equippedRecords = new List<EquippedRecord>();

    readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();

    void Awake()
    {
        // 지워진 기록 정리
        equippedRecords.RemoveAll(r => r == null || r.renderers.TrueForAll(g => g == null));
        BuildBoneMap();
        foreach (var it in startItems)
        {
            if (it == null || it.prefab == null) continue;
            if (FindRecord(it.slot) != null) continue;   // 에디터에서 미리 입혀 둠
            Equip(it.slot, it.prefab);
        }
        RefreshBodyParts();
    }

    void BuildBoneMap()
    {
        bones.Clear();
        if (skeletonRoot == null)
        {
            var hips = FindDeep(transform, "Hips");
            skeletonRoot = hips != null ? hips.parent : transform;
        }
        foreach (var t in skeletonRoot.GetComponentsInChildren<Transform>(true))
        {
            // 이름이 겹치면 먼저 찾은 것을 쓴다 (FBX 뼈대 이름은 모두 고유함)
            if (!bones.ContainsKey(t.name)) bones.Add(t.name, t);
        }
    }

    EquippedRecord FindRecord(Slot slot)
    {
        foreach (var r in equippedRecords) if (r != null && r.slot == slot) return r;
        return null;
    }

    /// <summary>startItems 에 적힌 이 프리팹의 숨길 몸 모듈 목록.</summary>
    string[] LookupHide(GameObject prefab)
    {
        foreach (var it in startItems)
            if (it != null && it.prefab == prefab && it.hideBodyParts != null) return it.hideBodyParts;
        return new string[0];
    }

    /// <summary>슬롯에 아이템을 입힌다. 숨길 몸 모듈은 startItems 에 같은 프리팹이 있으면 그 설정을 쓴다.</summary>
    public void Equip(Slot slot, GameObject prefab)
    {
        Equip(slot, prefab, LookupHide(prefab));
    }

    /// <summary>슬롯에 아이템을 입힌다. 같은 슬롯에 입고 있던 것은 먼저 벗긴다.</summary>
    public void Equip(Slot slot, GameObject prefab, string[] hideBodyParts)
    {
        if (prefab == null) return;
        if (bones.Count == 0) BuildBoneMap();
        Unequip(slot);

        var e = new EquippedRecord { slot = slot, source = prefab };
        if (hideBodyParts != null) e.hiddenBodyParts.AddRange(hideBodyParts);
        var inst = Instantiate(prefab, transform.position, transform.rotation);

        foreach (var smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            var src = smr.bones;
            var dst = new Transform[src.Length];
            for (int i = 0; i < src.Length; i++) dst[i] = Resolve(src[i], e);
            var root = Resolve(smr.rootBone, e);

            smr.transform.SetParent(transform, false);
            smr.bones = dst;
            smr.rootBone = root;
            e.renderers.Add(smr.gameObject);
        }

        // 뼈대 사본·빈 노드는 버린다 (렌더러와 옮겨 붙인 본은 이미 캐릭터 쪽으로 이동함)
        SafeDestroy(inst);

        equippedRecords.Add(e);
        RefreshBodyParts();
        NotifyChanged();
    }

    /// <summary>슬롯의 아이템을 벗긴다.</summary>
    public void Unequip(Slot slot)
    {
        var e = FindRecord(slot);
        if (e == null) return;
        // Destroy 는 프레임 끝에 일어나므로, 같은 프레임에 다시 입힐 때 옛 본이 이름 검색에 걸리지 않도록
        // 먼저 캐릭터에서 떼어 내고 끈 다음 지운다 (SpringBoneSystem 이 옛 Hair_* 를 잡는 문제 방지).
        foreach (var go in e.renderers) DetachAndDestroy(go);
        foreach (var go in e.graftedRoots) DetachAndDestroy(go);
        foreach (var n in e.graftedNames) bones.Remove(n);
        equippedRecords.Remove(e);
        RefreshBodyParts();
        NotifyChanged();
    }

    public bool IsEquipped(Slot slot) { return FindRecord(slot) != null; }

    /// <summary>토글용: 입고 있으면 벗기고, 안 입었으면 startItems 의 같은 슬롯 아이템을 입힌다.</summary>
    public void Toggle(Slot slot)
    {
        if (IsEquipped(slot)) { Unequip(slot); return; }
        foreach (var it in startItems)
            if (it != null && it.slot == slot && it.prefab != null) { Equip(slot, it.prefab, it.hideBodyParts); return; }
    }

    /// <summary>
    /// 몸 모듈 표시 갱신: 입고 있는 아이템 중 하나라도 숨기라고 한 모듈은 끄고, 나머지는 켠다.
    /// (어느 아이템에도 이름이 없는 렌더러는 건드리지 않는다)
    /// </summary>
    public void RefreshBodyParts()
    {
        var known = new HashSet<string>();
        var hidden = new HashSet<string>();
        foreach (var it in startItems)
            if (it != null && it.hideBodyParts != null) foreach (var n in it.hideBodyParts) if (!string.IsNullOrEmpty(n)) known.Add(n);
        foreach (var r in equippedRecords)
        {
            if (r == null) continue;
            // startItems 에서 나중에 숨길 목록을 고쳐도 바로 반영되도록, 기록의 목록에 startItems 설정을 합친다
            if (r.hiddenBodyParts == null) r.hiddenBodyParts = new List<string>();
            foreach (var n in LookupHide(r.source)) if (!r.hiddenBodyParts.Contains(n)) r.hiddenBodyParts.Add(n);
            foreach (var n in r.hiddenBodyParts) if (!string.IsNullOrEmpty(n)) { known.Add(n); hidden.Add(n); }
        }
        foreach (Transform c in transform)
        {
            if (!known.Contains(c.name)) continue;
            var rd = c.GetComponent<Renderer>();
            if (rd != null) rd.enabled = !hidden.Contains(c.name);
        }
    }

    void NotifyChanged()
    {
        // 흔들림 본(SpringBoneSystem)이 새 본을 다시 잡도록 알림 (플레이 중에만)
        if (Application.isPlaying) SendMessage("OnOutfitChanged", SendMessageOptions.DontRequireReceiver);
    }

    static void SafeDestroy(GameObject go)
    {
        if (go == null) return;
        if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
    }

    static void DetachAndDestroy(GameObject go)
    {
        if (go == null) return;
        go.transform.SetParent(null, false);
        go.SetActive(false);
        SafeDestroy(go);
    }

    [ContextMenu("시작 장비 미리 입히기 (에디터)")]
    public void EquipStartItemsNow()
    {
        BuildBoneMap();
        foreach (var it in startItems)
            if (it != null && it.prefab != null) Equip(it.slot, it.prefab, it.hideBodyParts);
        MarkDirtyInEditor();
    }

    [ContextMenu("모두 벗기기")]
    public void UnequipAll()
    {
        BuildBoneMap();
        foreach (Slot s in System.Enum.GetValues(typeof(Slot))) Unequip(s);
        MarkDirtyInEditor();
    }

    [ContextMenu("몸 모듈 표시 갱신")]
    public void RefreshBodyPartsMenu()
    {
        RefreshBodyParts();
        MarkDirtyInEditor();
    }

    void MarkDirtyInEditor()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif
    }

    /// <summary>
    /// 옷 쪽 본을 캐릭터 본으로 바꾼다. 캐릭터에 없는 본이면 조상 쪽으로 올라가며
    /// 이름이 같은 본을 찾고, 그 아래로 빠진 부분(가지)을 통째로 옮겨 붙인다.
    /// </summary>
    Transform Resolve(Transform src, EquippedRecord e)
    {
        if (src == null) return null;
        Transform found;
        if (bones.TryGetValue(src.name, out found)) return found;

        // src 에서 위로 올라가며 캐릭터에 있는 조상을 찾는다
        Transform branch = src;
        Transform anchor = null;
        while (branch.parent != null)
        {
            if (bones.TryGetValue(branch.parent.name, out anchor)) break;
            branch = branch.parent;
            anchor = null;
        }
        if (anchor == null)
        {
            Debug.LogWarning("[CharacterOutfit] 본 '" + src.name + "' 의 조상을 캐릭터 뼈대에서 찾지 못함", this);
            return null;
        }

        // 바인드 자세 기준 로컬 변환이 그대로 유지되도록 worldPositionStays=false 로 옮긴다
        branch.SetParent(anchor, false);
        e.graftedRoots.Add(branch.gameObject);
        foreach (var t in branch.GetComponentsInChildren<Transform>(true))
        {
            if (!bones.ContainsKey(t.name)) { bones.Add(t.name, t); e.graftedNames.Add(t.name); }
        }
        return bones.TryGetValue(src.name, out found) ? found : null;
    }

    static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform c in t)
        {
            var r = FindDeep(c, name);
            if (r != null) return r;
        }
        return null;
    }
}
