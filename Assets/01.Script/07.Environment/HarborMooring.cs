using UnityEngine;

/// <summary>
/// 선착장 정박선. 시작할 때 씬의 원본 배(스포너 템플릿: panok_navy, anteck, seki, merchant)를 복제해
/// 정해진 자리에 세워 둔다. AI·이동·포격·노 젓기를 끄고 Rigidbody 는 kinematic, 부력은 Transform 모드(제자리에서 파도만 탐).
/// 진영(ShipFaction)·체력(ShipHealth)은 그대로라 플레이어가 공격할 수 있고, 공격하면 우호도가 움직인다(반격은 하지 않음).
/// 섬 가까이(FactionTerritory inner)에는 돌아다니는 배가 없으므로 섬에 다가가면 이 배들만 보인다.
/// </summary>
public class HarborMooring : MonoBehaviour
{
    [System.Serializable]
    public struct Spot
    {
        [Tooltip("씬의 원본 배 이름(panok_navy / anteck / seki / merchant).")] public string templateName;
        [Tooltip("이 오브젝트 기준 위치(m, 로컬).")] public Vector3 localPosition;
        [Tooltip("이 오브젝트 기준 방향(°, 로컬 Y).")] public float localYaw;
    }

    public Spot[] spots;
    [Tooltip("생성된 정박선(읽기 전용).")] public GameObject[] spawned;

    void Start()
    {
        if (spots == null) return;
        spawned = new GameObject[spots.Length];
        for (int i = 0; i < spots.Length; i++)
        {
            var tpl = FindTemplate(spots[i].templateName);
            if (tpl == null) { Debug.LogWarning("[HarborMooring] 원본 배 없음: " + spots[i].templateName); continue; }
            Vector3 p = transform.TransformPoint(spots[i].localPosition); p.y = 0f;
            Quaternion r = Quaternion.Euler(0f, transform.eulerAngles.y + spots[i].localYaw, 0f);

            // 원본이 아직 켜져 있으면(스포너 Start 전) 복제본의 AI 가 깨어나지 않게 잠깐 끈 채로 복제
            bool was = tpl.activeSelf;
            tpl.SetActive(false);
            var go = Instantiate(tpl, p, r);
            tpl.SetActive(was);
            go.name = "Moored_" + spots[i].templateName;
            MakeMoored(go);
            go.SetActive(true);
            spawned[i] = go;
        }
    }

    static GameObject FindTemplate(string n)
    {
        if (string.IsNullOrEmpty(n)) return null;
        foreach (var sf in Resources.FindObjectsOfTypeAll<ShipFaction>())
        {
            if (sf == null || sf.gameObject.scene.name == null || !sf.gameObject.scene.IsValid()) continue; // 에셋 제외
            if (sf.transform.parent == null && sf.gameObject.name == n) return sf.gameObject;
        }
        return null;
    }

    static void MakeMoored(GameObject go)
    {
        foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb == null) continue;
            if (mb is EnemyShipBase || mb is NavyShipBase || mb is OarAnimator || mb.GetType().Name.Contains("CannonControl"))
                mb.enabled = false;
        }
        var rb = go.GetComponent<Rigidbody>();
        if (rb != null) { rb.isKinematic = true; rb.interpolation = RigidbodyInterpolation.None; }
        var b = go.GetComponent<ShipBuoyancy>();
        if (b != null) b.mode = ShipBuoyancy.Mode.Transform;
    }

    void OnDrawGizmosSelected()
    {
        if (spots == null) return;
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.9f);
        foreach (var s in spots)
        {
            Vector3 p = transform.TransformPoint(s.localPosition); p.y = 0f;
            Gizmos.DrawWireSphere(p, 4f);
            Gizmos.DrawLine(p, p + Quaternion.Euler(0f, transform.eulerAngles.y + s.localYaw, 0f) * Vector3.forward * 15f);
        }
    }
}
