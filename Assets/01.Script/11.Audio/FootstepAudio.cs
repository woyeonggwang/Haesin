using UnityEngine;

/// <summary>
/// 발소리. 애니메이션 발이 바닥에 닿는 순간(들렸던 발바닥이 바닥 가까이 내려옴)마다 한 번 낸다 — 걷기·달리기·전투 발놀림 모두 같은 규칙.
/// 들림 높이는 FootIK 가 있으면 애니메이션(IK 전) 발 높이를 쓴다 — 계단에서는 발 아래 바닥이 한 단씩 바뀌어
/// 레이로 잰 높이만으로는 '들림'이 안 잡혀 발소리가 빠졌다(측정: 계단에서 1~2 초 간격).
/// 바닥 종류는 발 아래로 쏜 레이가 맞은 곳으로 고른다:
///   터레인 → 그 지점에서 가장 진한 터레인 레이어 이름(grassKeywords / sandKeywords / 그 밖 = 돌),
///   그 밖의 콜라이더 → 이름(자신 + 최상위)에 woodKeywords 가 있으면 나무, stoneKeywords 가 있으면 돌, 둘 다 없으면 나무(배 갑판 등).
/// PlayerCharacter 가 있으면 OnFoot 상태에서만. 음원: Kenney Impact Sounds (CC0).
/// </summary>
[RequireComponent(typeof(Animator))]
public class FootstepAudio : MonoBehaviour
{
    public enum Surface { Wood, Grass, Sand, Stone }

    [Header("음원 (각 5개)")]
    public AudioClip[] wood;
    public AudioClip[] grass;
    [Tooltip("모래·자갈. Kenney 의 snow(사박거림)를 씀.")] public AudioClip[] sand;
    public AudioClip[] stone;

    [Header("크기")]
    [Range(0f, 1f)] public float volume = 0.1f;
    [Tooltip("달리기(속도 3.3 m/s 이상)일 때 배율.")] public float runVolumeScale = 1.3f;
    public Vector2 pitchRange = new Vector2(0.92f, 1.08f);
    [Tooltip("3D 소리 최소 거리(m). 카메라가 3.2 m 뒤라 그 안은 같은 크기.")] public float minDistance = 4f;
    public float maxDistance = 30f;

    [Header("발 닿음 판정")]
    [Tooltip("발바닥이 바닥에서 이만큼 들려야 '들림'(m).")] public float liftHeight = 0.05f;
    [Tooltip("들렸던 발바닥이 이 높이 아래로 내려오면 '닿음' → 소리(m).")] public float contactHeight = 0.025f;
    [Tooltip("같은 발 소리 최소 간격(초).")] public float minInterval = 0.18f;
    [Tooltip("캐릭터가 이보다 느리면 소리 안 냄(m/s) — 제자리 자세 변화 무시.")] public float minSpeed = 0.4f;
    public LayerMask groundMask = ~((1 << 2) | (1 << 4) | (1 << 5) | (1 << 8));

    [Header("바닥 종류 규칙")]
    public string[] grassKeywords = { "grass", "forrest" };
    public string[] sandKeywords = { "sand", "gravel" };
    [Tooltip("돌보다 먼저 검사(CaveStair 는 이름에 cave 가 있어도 나무).")] public string[] woodKeywords = { "stair", "bridge", "deck", "wood", "dock" };
    public string[] stoneKeywords = { "rock", "cave", "stone", "cliff" };

    [Header("상태 (읽기 전용)")]
    public Surface lastSurface;
    public int stepCount;

    Animator _anim; PlayerCharacter _pc; FootIK _ik;
    Transform _footL, _footR;
    bool _liftedL, _liftedR;
    float _lastL, _lastR;
    Vector3 _prevPos; float _speed;
    AudioSource _src;
    int _lastClip = -1;

    void Awake()
    {
        _anim = GetComponent<Animator>();
        _pc = GetComponent<PlayerCharacter>();
        _ik = GetComponent<FootIK>();
        if (_anim != null && _anim.isHuman)
        {
            _footL = _anim.GetBoneTransform(HumanBodyBones.LeftFoot);
            _footR = _anim.GetBoneTransform(HumanBodyBones.RightFoot);
        }
        var go = new GameObject("FootstepAudioSource");
        go.transform.SetParent(transform, false);
        _src = go.AddComponent<AudioSource>();
        _src.playOnAwake = false;
        _src.spatialBlend = 1f;
        _src.rolloffMode = AudioRolloffMode.Logarithmic;
        _src.minDistance = minDistance;
        _src.maxDistance = maxDistance;
        _src.dopplerLevel = 0f;
        _prevPos = transform.position;
    }

    void LateUpdate() // FootIK 가 적용된 뒤의 발 위치로 판정
    {
        float dt = Mathf.Max(1e-4f, Time.deltaTime);
        Vector3 p = transform.position;
        Vector3 v = p - _prevPos; v.y = 0f;
        _prevPos = p;
        _speed = Mathf.Lerp(_speed, v.magnitude / dt, 1f - Mathf.Exp(-10f * dt));

        if (_footL == null || _footR == null) return;
        if (_pc != null && _pc.state != PlayerCharacter.State.OnFoot) { _liftedL = _liftedR = false; return; }

        bool useIk = _ik != null && _ik.enabled;
        Check(_footL, _anim.leftFeetBottomHeight, useIk, useIk ? _ik.animLiftLeft : 0f, ref _liftedL, ref _lastL);
        Check(_footR, _anim.rightFeetBottomHeight, useIk, useIk ? _ik.animLiftRight : 0f, ref _liftedR, ref _lastR);
    }

    void Check(Transform foot, float bottom, bool useAnimLift, float animLift, ref bool lifted, ref float last)
    {
        RaycastHit hit;
        Vector3 o = foot.position + Vector3.up * 0.3f;
        if (!Physics.Raycast(o, Vector3.down, out hit, 0.3f + bottom + 0.4f, groundMask, QueryTriggerInteraction.Ignore)) { lifted = true; return; }
        float h = useAnimLift ? animLift : foot.position.y - bottom - hit.point.y;
        if (h > liftHeight) { lifted = true; return; }
        if (lifted && h < contactHeight)
        {
            lifted = false;
            if (Time.time - last >= minInterval && _speed >= minSpeed)
            {
                last = Time.time;
                Play(SurfaceOf(hit), foot.position);
            }
        }
    }

    Surface SurfaceOf(RaycastHit hit)
    {
        var tc = hit.collider as TerrainCollider;
        if (tc != null)
        {
            var t = tc.GetComponent<Terrain>();
            string layer = t != null ? DominantLayer(t, hit.point) : "";
            if (Has(layer, grassKeywords)) return Surface.Grass;
            if (Has(layer, sandKeywords)) return Surface.Sand;
            return Surface.Stone;
        }
        string n = hit.collider.name + " " + hit.collider.transform.root.name;
        if (Has(n, woodKeywords)) return Surface.Wood;
        return Has(n, stoneKeywords) ? Surface.Stone : Surface.Wood;
    }

    static bool Has(string s, string[] keys)
    {
        if (string.IsNullOrEmpty(s) || keys == null) return false;
        s = s.ToLowerInvariant();
        foreach (var k in keys) if (!string.IsNullOrEmpty(k) && s.Contains(k.ToLowerInvariant())) return true;
        return false;
    }

    static string DominantLayer(Terrain t, Vector3 world)
    {
        var td = t.terrainData;
        if (td.alphamapLayers == 0) return "";
        Vector3 local = world - t.transform.position;
        int x = Mathf.Clamp(Mathf.FloorToInt(local.x / td.size.x * td.alphamapWidth), 0, td.alphamapWidth - 1);
        int z = Mathf.Clamp(Mathf.FloorToInt(local.z / td.size.z * td.alphamapHeight), 0, td.alphamapHeight - 1);
        float[,,] a = td.GetAlphamaps(x, z, 1, 1);
        int best = 0; float bw = -1f;
        for (int i = 0; i < td.alphamapLayers; i++) if (a[0, 0, i] > bw) { bw = a[0, 0, i]; best = i; }
        var layers = td.terrainLayers;
        return (layers != null && best < layers.Length && layers[best] != null) ? layers[best].name : "";
    }

    void Play(Surface s, Vector3 at)
    {
        AudioClip[] set = s == Surface.Grass ? grass : s == Surface.Sand ? sand : s == Surface.Stone ? stone : wood;
        if (set == null || set.Length == 0) return;
        int i = Random.Range(0, set.Length);
        if (set.Length > 1 && i == _lastClip) i = (i + 1) % set.Length; // 같은 소리 연속 방지
        _lastClip = i;
        if (set[i] == null) return;
        lastSurface = s; stepCount++;
        _src.transform.position = at;
        _src.pitch = Random.Range(pitchRange.x, pitchRange.y);
        float vol = volume * (_speed > 3.3f ? runVolumeScale : 1f);
        _src.PlayOneShot(set[i], vol);
    }
}
