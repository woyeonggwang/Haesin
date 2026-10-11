using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 선원 사격 연출과 명중 처리를 한곳에서 맡는다(씬에 하나, `CrewCombatFX`).
/// - 화살: 풀에서 꺼낸 화살 메시가 포물선으로 날아가고, 명중이면 도착 순간 표적 선원이 쓰러진다. 빗나가면 물에 떨어질 때까지 날아감.
/// - 조총: 총구 불꽃·연기(파티클 시스템 하나씩 같이 씀 — Emit), 명중이면 탄 비행시간(거리/350 m/s) 뒤 쓰러짐. 탄은 그리지 않음.
/// 소리: 조총 = 대포 소리(CC0)를 높은 피치로, 활 = 칼 휘두르는 소리(CC0) — 들어 보고 고른 것 아님.
/// 박히는 소리(2026-10-06, 사용자 지시 "탄 박히는 소리·화살 박히는 소리 0.2"): 화살은 날아가 도착하는 순간(명중이면 표적, 빗나가면 겨눈 자리 근처),
/// 조총 탄은 비행시간(거리/350 s) 뒤 명중·빗나감 자리에서 — `Audio/Impact/` 의 합성 음원(기존 CC0 조각 + 합성).
/// 이 컴포넌트가 씬에 없으면 연출 없이 판정만 한다.
/// </summary>
public class CrewProjectiles : MonoBehaviour
{
    [Header("화살")]
    public Mesh arrowMesh;
    public Material[] arrowMaterials;
    [Tooltip("화살 속도(m/s).")] public float arrowSpeed = 55f;
    [Tooltip("동시에 날 수 있는 화살 수.")] public int maxArrows = 80;

    [Header("조총 연기·불꽃")]
    [Tooltip("연기 재질(한 장짜리 연기 그림, 정점 색·투명 섞기 셰이더 — Hovl HDRP_Blend_CG + Smoke20).")] public Material smokeMaterial;
    [Tooltip("불꽃 재질(8×8 시트).")] public Material flashMaterial;

    [Header("소리")]
    public AudioClip musketClip;
    public float musketVolume = 0.2f, musketPitch = 2.0f, musketMaxDistance = 260f;
    public AudioClip[] bowClips;
    public float bowVolume = 0.2f, bowPitch = 1.5f, bowMaxDistance = 45f;
    [Tooltip("화살이 박히는 소리(여럿이면 무작위).")] public AudioClip[] arrowImpactClips;
    public float arrowImpactVolume = 0.2f, arrowImpactMaxDistance = 45f;
    [Tooltip("조총 탄이 박히는 소리(여럿이면 무작위).")] public AudioClip[] bulletImpactClips;
    public float bulletImpactVolume = 0.2f, bulletImpactMaxDistance = 60f;

    static CrewProjectiles _inst;
    static bool _searched;

    /// <summary>시험용 세기(쏜 수·맞힌 수). Play 를 시작할 때 0.</summary>
    public static int musketShots, musketHits, arrowShots, arrowHits;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { _inst = null; _searched = false; musketShots = musketHits = arrowShots = arrowHits = 0; }

    class Flight
    {
        public Transform t;
        public Vector3 from, v0;
        public float time, duration;
        public CrewMember target;
        public bool hit;
    }
    readonly List<Flight> _flights = new List<Flight>();
    readonly Stack<Transform> _arrowPool = new Stack<Transform>();
    int _arrowCount;

    struct PendingKill { public CrewMember m; public float at; }
    readonly List<PendingKill> _pending = new List<PendingKill>();
    struct PendingSound { public Vector3 pos; public float at; }
    readonly List<PendingSound> _pendingBulletSounds = new List<PendingSound>();

    ParticleSystem _smoke, _flash;

    static CrewProjectiles Instance
    {
        get
        {
            if (_inst == null && !_searched)
            {
                _searched = true;
                _inst = FindFirstObjectByType<CrewProjectiles>();
                if (_inst == null)
                {
                    var go = new GameObject("CrewCombatFX (자동)");
                    _inst = go.AddComponent<CrewProjectiles>();
                }
            }
            return _inst;
        }
    }

    void Awake()
    {
        if (_inst != null && _inst != this) { Destroy(this); return; }
        _inst = this;
        _searched = true;
        _smoke = MakeSystem("MusketSmoke", smokeMaterial, false);
        _flash = MakeSystem("MusketFlash", flashMaterial, true);
    }

    void OnDestroy() { if (_inst == this) { _inst = null; _searched = false; } }

    // ---------- 바깥에서 부름 ----------

    /// <summary>화살을 쏜다. target 이 null 이 아니면 명중(도착 때 쓰러짐), null 이면 aim 근처로 빗나감.</summary>
    public static void Arrow(Vector3 from, Vector3 aim, CrewMember target, ShipCrew shooter)
    {
        var inst = Instance;
        if (inst == null) { if (target != null) target.Kill(); return; }
        inst.LaunchArrow(from, aim, target);
    }

    /// <summary>조총을 쏜다. target 이 null 이 아니면 명중.</summary>
    public static void Musket(Vector3 from, Vector3 aim, CrewMember target, ShipCrew shooter)
    {
        var inst = Instance;
        if (inst == null) { if (target != null) target.Kill(); return; }
        inst.FireMusket(from, aim, target);
    }

    // ---------- 화살 ----------

    void LaunchArrow(Vector3 from, Vector3 aim, CrewMember target)
    {
        bool hit = target != null;
        arrowShots++; if (hit) arrowHits++;
        if (!hit)
        {
            // 빗나감: 표적 둘레 1.5~5 m, 조금 낮게
            Vector2 r = Random.insideUnitCircle.normalized * Random.Range(1.5f, 5f);
            aim += new Vector3(r.x, Random.Range(-1.5f, 0.5f), r.y);
        }
        float dist = Vector3.Distance(from, aim);
        float T = Mathf.Max(0.15f, dist / Mathf.Max(5f, arrowSpeed));
        Vector3 g = Physics.gravity;
        Vector3 v0 = (aim - from - 0.5f * g * T * T) / T;

        Transform a = GetArrow();
        if (a == null)
        {
            if (hit) AddPending(target, T);
        }
        else
        {
            a.position = from;
            a.rotation = Quaternion.LookRotation(v0.normalized);
            a.gameObject.SetActive(true);
            _flights.Add(new Flight { t = a, from = from, v0 = v0, time = 0f, duration = T, target = target, hit = hit });
        }
        if (bowClips != null && bowClips.Length > 0)
            OneShotAudio.Play(bowClips[Random.Range(0, bowClips.Length)], from, bowVolume, 2f, bowMaxDistance, bowPitch * Random.Range(0.92f, 1.08f));
    }

    Transform GetArrow()
    {
        if (arrowMesh == null) return null;
        if (_arrowPool.Count > 0) return _arrowPool.Pop();
        if (_arrowCount >= maxArrows) return null;
        _arrowCount++;
        var go = new GameObject("FlyingArrow");
        go.transform.SetParent(transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = arrowMesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterials = arrowMaterials;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        go.SetActive(false);
        return go.transform;
    }

    // ---------- 조총 ----------

    void FireMusket(Vector3 from, Vector3 aim, CrewMember target)
    {
        Vector3 dir = (aim - from);
        float dist = dir.magnitude;
        dir = dist > 1e-3f ? dir / dist : Vector3.forward;
        musketShots++; if (target != null) musketHits++;
        if (target != null) AddPending(target, dist / 350f);
        else aim += new Vector3(Random.Range(-3f, 3f), Random.Range(-1.5f, 0.3f), Random.Range(-3f, 3f)); // 빗나간 탄이 박히는 자리
        _pendingBulletSounds.Add(new PendingSound { pos = aim, at = Time.time + dist / 350f });
        Puff(from, dir);
        if (musketClip != null)
            OneShotAudio.Play(musketClip, from, musketVolume, 5f, musketMaxDistance, musketPitch * Random.Range(0.92f, 1.08f));
    }

    void Puff(Vector3 pos, Vector3 dir)
    {
        if (_smoke != null)
        {
            var ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < 4; i++)
            {
                ep.position = pos + dir * (0.15f + 0.25f * i) + Random.insideUnitSphere * 0.08f;
                ep.velocity = dir * Random.Range(1.5f, 4f) + Vector3.up * Random.Range(0.2f, 0.6f) + Random.insideUnitSphere * 0.3f;
                ep.startSize = Random.Range(0.45f, 0.75f);
                ep.startLifetime = Random.Range(2.2f, 3.2f);
                ep.rotation = Random.Range(0f, 360f);
                _smoke.Emit(ep, 1);
            }
        }
        if (_flash != null)
        {
            var ep = new ParticleSystem.EmitParams();
            ep.position = pos + dir * 0.12f;
            ep.velocity = dir * 2f;
            ep.startSize = Random.Range(0.35f, 0.5f);
            ep.startLifetime = 0.09f;
            ep.rotation = Random.Range(0f, 360f);
            _flash.Emit(ep, 1);
        }
    }

    ParticleSystem MakeSystem(string name, Material mat, bool flash)
    {
        if (mat == null) return null;
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;          // 늘 재생 중(방출은 끔) — Emit 으로만 넣는다
        main.playOnAwake = true;
        main.duration = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = flash ? 100 : 600;
        main.startColor = flash ? new Color(1f, 0.85f, 0.6f, 1f) : new Color(0.82f, 0.8f, 0.76f, 0.75f);
        main.gravityModifier = flash ? 0f : -0.01f;
        var em = ps.emission; em.enabled = false;
        var sh = ps.shape; sh.enabled = false;

        // 불꽃만 8×8 시트(무작위 한 칸). 연기는 한 장짜리 그림.
        // (처음엔 연기도 Camelot 시트 재질(HDRP/Unlit)을 썼는데 정점 색을 안 받고 섞기가 One/Zero 라 검은 네모로 보였다 — 2026-10-04)
        var sheet = ps.textureSheetAnimation;
        sheet.enabled = flash;
        if (flash)
        {
            sheet.numTilesX = 8; sheet.numTilesY = 8;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f, 0.99f);
        }

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, flash ? AnimationCurve.Linear(0f, 1f, 1f, 0.6f) : new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.3f, 1.6f), new Keyframe(1f, 3.2f)));

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                     flash ? new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
                           : new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.08f), new GradientAlphaKey(0.5f, 0.5f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var lim = ps.limitVelocityOverLifetime;
        lim.enabled = !flash;
        lim.drag = new ParticleSystem.MinMaxCurve(1.5f);
        lim.multiplyDragByParticleSize = false;

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        ps.Play();
        return ps;
    }

    // ---------- 공통 ----------

    void AddPending(CrewMember m, float delay)
    {
        _pending.Add(new PendingKill { m = m, at = Time.time + delay });
    }

    void Update()
    {
        float dt = Time.deltaTime;
        Vector3 g = Physics.gravity;
        for (int i = _flights.Count - 1; i >= 0; i--)
        {
            var f = _flights[i];
            if (f.t == null) { _flights.RemoveAt(i); continue; }
            f.time += dt;
            float t = f.time;
            Vector3 v = f.v0 + g * t;
            f.t.position = f.from + f.v0 * t + 0.5f * g * t * t;
            if (v.sqrMagnitude > 1e-4f) f.t.rotation = Quaternion.LookRotation(v.normalized);

            if (f.hit && t >= f.duration)
            {
                PlayImpact(arrowImpactClips, f.t.position, arrowImpactVolume, arrowImpactMaxDistance);
                if (f.target != null && f.target.alive) f.target.Kill();
                Recycle(f.t);
                _flights.RemoveAt(i);
                continue;
            }
            // 빗나간 화살: 겨눈 자리(갑판 근처)에 닿는 순간 박히는 소리 한 번
            if (!f.hit && t >= f.duration && t - dt < f.duration) PlayImpact(arrowImpactClips, f.t.position, arrowImpactVolume, arrowImpactMaxDistance);
            // 빗나간 화살은 물 밑으로 내려가거나 오래 날면 거둠
            if (!f.hit && (f.t.position.y < -1f || t > f.duration + 3f))
            {
                Recycle(f.t);
                _flights.RemoveAt(i);
            }
        }

        for (int i = _pending.Count - 1; i >= 0; i--)
        {
            if (Time.time < _pending[i].at) continue;
            var m = _pending[i].m;
            _pending.RemoveAt(i);
            if (m != null && m.alive) m.Kill();
        }

        for (int i = _pendingBulletSounds.Count - 1; i >= 0; i--)
        {
            if (Time.time < _pendingBulletSounds[i].at) continue;
            PlayImpact(bulletImpactClips, _pendingBulletSounds[i].pos, bulletImpactVolume, bulletImpactMaxDistance);
            _pendingBulletSounds.RemoveAt(i);
        }
    }

    void PlayImpact(AudioClip[] clips, Vector3 pos, float volume, float maxDistance)
    {
        if (clips == null || clips.Length == 0 || volume <= 0f) return;
        var c = clips[Random.Range(0, clips.Length)];
        if (c != null) OneShotAudio.Play(c, pos, volume, 2f, maxDistance, Random.Range(0.92f, 1.08f));
    }

    void Recycle(Transform a)
    {
        if (a == null) return;
        a.gameObject.SetActive(false);
        _arrowPool.Push(a);
    }
}
