using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어 함선 강화 상태. 씬의 GameData 오브젝트에 하나.
///   - cannonLevel  : 함포 공격력 (CannonControl.StampFaction 이 포탄 damage 에 CannonDamageMultiplier 를 곱한다)
///   - arrowLevel   : 선원 화살 공격력 (화살 시스템은 아직 없음 — 값만 보관)
///   - crewHealthLevel : 선원 체력 (적 화살·총알 피격 시스템은 아직 없음 — 값만 보관)
///   - flagColor    : 깃발 색. 플레이어 배의 깃발 렌더러(텍스처 이름 Flag_*, 깃대 제외)에 MaterialPropertyBlock 으로 _BaseColor 를 넣는다
///                    (머티리얼은 관군 판옥선과 공유되므로 머티리얼 자체는 건드리지 않는다).
///   - flagImagePath: StreamingAssets/Flags/ 안의 PNG 파일 이름. 있으면 깃발 텍스처를 그 이미지로 바꾼다(추후 사용자 이미지 기능).
/// 비용은 Cost* 함수가 레벨에 따라 계산한다.
/// </summary>
public class ShipUpgrades : MonoBehaviour
{
    public static ShipUpgrades Instance { get; private set; }

    [Header("레벨 (0 = 기본)")]
    public int cannonLevel = 0;
    public int arrowLevel = 0;
    public int crewHealthLevel = 0;
    public int maxLevel = 10;

    [Header("외관")]
    public Color flagColor = Color.white;
    [Tooltip("StreamingAssets/Flags/ 안의 이미지 파일 이름(비우면 원래 텍스처).")]
    public string flagImagePath = "";

    [Header("효과 계수")]
    [Tooltip("함포 레벨당 공격력 증가 비율.")] public float cannonDamagePerLevel = 0.15f;
    [Tooltip("화살 레벨당 공격력 증가 비율.")] public float arrowDamagePerLevel = 0.15f;
    [Tooltip("선원 기본 체력.")] public float crewBaseHealth = 100f;
    [Tooltip("선원 체력 레벨당 증가 비율.")] public float crewHealthPerLevel = 0.2f;

    public System.Action OnChanged;

    public float CannonDamageMultiplier { get { return 1f + cannonDamagePerLevel * cannonLevel; } }
    public float ArrowDamageMultiplier { get { return 1f + arrowDamagePerLevel * arrowLevel; } }
    public float CrewHealth { get { return crewBaseHealth * (1f + crewHealthPerLevel * crewHealthLevel); } }

    // ---- 비용 (목재, 철, 금화) ----
    public Vector3Int CostCannon(int level) { return new Vector3Int(0, 20 + 10 * level, 100 + 50 * level); }
    public Vector3Int CostArrow(int level) { return new Vector3Int(15 + 5 * level, 5 + 5 * level, 60 + 30 * level); }
    public Vector3Int CostCrewHealth(int level) { return new Vector3Int(10 + 5 * level, 0, 80 + 40 * level); }
    public Vector3Int CostRecruit(int currentCrew) { return new Vector3Int(5, 0, 50 + 10 * Mathf.Max(0, currentCrew - 10)); }

    void Awake() { Instance = this; }
    void OnDestroy() { if (Instance == this) Instance = null; }
    void Start() { ApplyFlag(); }

    bool Spend(Vector3Int c)
    {
        var r = PlayerResources.Instance;
        return r != null && r.TrySpend(c.x, c.y, c.z);
    }

    public bool UpgradeCannon() { if (cannonLevel >= maxLevel || !Spend(CostCannon(cannonLevel))) return false; cannonLevel++; Changed(); return true; }
    public bool UpgradeArrow() { if (arrowLevel >= maxLevel || !Spend(CostArrow(arrowLevel))) return false; arrowLevel++; Changed(); return true; }
    public bool UpgradeCrewHealth() { if (crewHealthLevel >= maxLevel || !Spend(CostCrewHealth(crewHealthLevel))) return false; crewHealthLevel++; Changed(); return true; }

    /// <summary>선원 영입: 플레이어 배 ShipCrew 에 큐브 한 명 추가.</summary>
    public bool Recruit()
    {
        ShipCrew crew = PlayerCrew();
        if (crew == null || !crew.HasFreeSlot) return false;
        if (!Spend(CostRecruit(crew.Count))) return false;
        crew.AddRecruit();
        Changed();
        return true;
    }

    public void SetFlagColor(Color c) { flagColor = c; ApplyFlag(); Changed(); }
    public void SetFlagImage(string fileName) { flagImagePath = fileName ?? ""; ApplyFlag(); Changed(); }

    void Changed() { if (OnChanged != null) OnChanged(); }

    public static GameObject PlayerShip()
    {
        var go = GameObject.FindGameObjectWithTag("Player");
        return go;
    }
    public static ShipCrew PlayerCrew() { var s = PlayerShip(); return s != null ? s.GetComponent<ShipCrew>() : null; }

    // ---- 깃발 ----
    static readonly int IdBaseColor = Shader.PropertyToID("_BaseColor");
    static readonly int IdBaseMap = Shader.PropertyToID("_BaseColorMap");
    Texture2D _flagTex;
    string _loadedFlagPath;

    /// <summary>깃발 렌더러 = 머티리얼의 베이스 텍스처 이름이 Flag_ 로 시작하고 Flag_pole 이 아닌 것.</summary>
    public static List<Renderer> FlagRenderers(GameObject ship)
    {
        var list = new List<Renderer>();
        if (ship == null) return list;
        foreach (var r in ship.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials; bool isFlag = false;
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i]; if (m == null || m.mainTexture == null) continue;
                string tn = m.mainTexture.name;
                if (tn.StartsWith("Flag_") && !tn.StartsWith("Flag_pole")) { isFlag = true; break; }
            }
            if (isFlag) list.Add(r);
        }
        return list;
    }

    public void ApplyFlag()
    {
        ApplyFlagTo(PlayerShip());
    }

    /// <summary>ship(플레이어 배 또는 전시용 복제본)의 깃발에 현재 색·이미지를 입힌다.</summary>
    public void ApplyFlagTo(GameObject ship)
    {
        if (ship == null) return;
        Texture2D tex = LoadFlagTexture();
        var block = new MaterialPropertyBlock();
        foreach (var r in FlagRenderers(ship))
        {
            r.GetPropertyBlock(block);
            block.SetColor(IdBaseColor, flagColor);
            if (tex != null) block.SetTexture(IdBaseMap, tex);
            else block.Clear(); // 이미지가 없으면 텍스처 덮어쓰기 없이 색만
            if (tex == null) block.SetColor(IdBaseColor, flagColor);
            r.SetPropertyBlock(block);
        }
    }

    Texture2D LoadFlagTexture()
    {
        if (string.IsNullOrEmpty(flagImagePath)) { _flagTex = null; _loadedFlagPath = null; return null; }
        if (_flagTex != null && _loadedFlagPath == flagImagePath) return _flagTex;
        string path = System.IO.Path.Combine(Application.streamingAssetsPath, "Flags", flagImagePath);
        if (!System.IO.File.Exists(path)) { Debug.LogWarning("[ShipUpgrades] 깃발 이미지가 없습니다: " + path); _flagTex = null; return null; }
        var t = new Texture2D(2, 2, TextureFormat.RGBA32, true);
        if (!t.LoadImage(System.IO.File.ReadAllBytes(path))) { Debug.LogWarning("[ShipUpgrades] 깃발 이미지를 읽지 못했습니다: " + path); return null; }
        t.wrapMode = TextureWrapMode.Clamp;
        _flagTex = t; _loadedFlagPath = flagImagePath;
        return t;
    }

    /// <summary>StreamingAssets/Flags 안의 png/jpg 파일 이름 목록.</summary>
    public static List<string> AvailableFlagImages()
    {
        var list = new List<string>();
        string dir = System.IO.Path.Combine(Application.streamingAssetsPath, "Flags");
        if (!System.IO.Directory.Exists(dir)) return list;
        foreach (var f in System.IO.Directory.GetFiles(dir))
        {
            string ext = System.IO.Path.GetExtension(f).ToLower();
            if (ext == ".png" || ext == ".jpg" || ext == ".jpeg") list.Add(System.IO.Path.GetFileName(f));
        }
        return list;
    }
}
