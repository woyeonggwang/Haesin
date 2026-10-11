using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 퀘스트 대화 상대(2026-10-07). 걸어서(하차) talkRange 안에 들어가면 화면에 "F 대화" 안내 → F 로 대화(QuestUI).
/// 지금 퀘스트의 Talk 목표 상대면 그 대사, 아니면 idleLines 중 하나.
/// 모델 = 자식(시민 모델 등), Animator 는 Villager 컨트롤러의 Idle(Speed 0). 대화하는 동안 플레이어 쪽으로 몸을 돌림.
/// 키를 E 대신 F 로 한 까닭: E(3초)는 승·하차·궁문·나룻배가 쓰고, 숙부는 선착장 가까이 서 있음.
/// </summary>
public class QuestNPC : MonoBehaviour
{
    public string npcId = "";
    public string displayName = "";
    [Tooltip("대화할 수 있는 거리(m).")] public float talkRange = 3.5f;
    [Tooltip("퀘스트 상대가 아닐 때 하는 말.")] public DialogueLine[] idleLines = new DialogueLine[0];

    public static readonly List<QuestNPC> All = new List<QuestNPC>();

    Quaternion _baseRot; bool _facing; Transform _faceTarget;

    void Awake() { _baseRot = transform.rotation; }
    void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    void OnDisable() { All.Remove(this); }

    public static QuestNPC Find(string id)
    {
        for (int i = 0; i < All.Count; i++) if (All[i] != null && All[i].npcId == id) return All[i];
        return null;
    }

    /// <summary>대화 동안 이 대상을 바라봄(null 이면 원래 방향으로).</summary>
    public void FaceTowards(Transform t) { _faceTarget = t; _facing = t != null; }

    void Update()
    {
        Quaternion want = _baseRot;
        if (_facing && _faceTarget != null)
        {
            Vector3 d = _faceTarget.position - transform.position; d.y = 0f;
            if (d.sqrMagnitude > 0.01f) want = Quaternion.LookRotation(d.normalized, Vector3.up);
        }
        transform.rotation = Quaternion.RotateTowards(transform.rotation, want, 240f * Time.deltaTime);
    }
}
