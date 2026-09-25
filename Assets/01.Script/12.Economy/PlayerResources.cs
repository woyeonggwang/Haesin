using UnityEngine;

/// <summary>
/// 플레이어 자원(목재·철·금화). 씬의 GameData 오브젝트에 하나. 함선 메뉴(Y)·인벤토리(I) 상단에 표시된다.
/// 추후 상선 약탈·리스폰 손실 등이 이 값을 건드린다. 저장/불러오기는 아직 없다.
/// </summary>
public class PlayerResources : MonoBehaviour
{
    public static PlayerResources Instance { get; private set; }

    [Tooltip("목재")] public int wood = 200;
    [Tooltip("철")] public int iron = 120;
    [Tooltip("금화")] public int gold = 1000;

    /// <summary>자원이 바뀔 때.</summary>
    public System.Action OnChanged;

    void Awake() { Instance = this; }
    void OnDestroy() { if (Instance == this) Instance = null; }

    public bool CanAfford(int w, int i, int g) { return wood >= w && iron >= i && gold >= g; }

    /// <summary>모자라면 아무것도 빼지 않고 false.</summary>
    public bool TrySpend(int w, int i, int g)
    {
        if (!CanAfford(w, i, g)) return false;
        wood -= w; iron -= i; gold -= g;
        if (OnChanged != null) OnChanged();
        return true;
    }

    public void Add(int w, int i, int g)
    {
        wood = Mathf.Max(0, wood + w); iron = Mathf.Max(0, iron + i); gold = Mathf.Max(0, gold + g);
        if (OnChanged != null) OnChanged();
    }
}
