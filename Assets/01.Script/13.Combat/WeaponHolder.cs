using UnityEngine;

/// <summary>
/// 환도를 허리 칼집(hipHolder)과 오른손 소켓(handSocket) 사이에서 옮긴다.
/// 에디터에서 프리팹 인스턴스(Hwando_Sheathed) 밖으로 자식을 옮길 수 없어 런타임에 재부모한다.
/// 이름으로 자동 탐색: 검 "Hwando_Sword", 칼집 홀더 "Hwando_Sheathed", 손 소켓 "Socket_Hwando_Hand_R".
///
/// 뽑기/넣기 애니메이션(ARPGSamurai rig_Equip 0.82 s / rig_Unequip 0.65 s)은 상체 레이어(UpperBody, 상체 마스크)에서 재생해
/// 걷거나 뛰면서도 할 수 있다. 오른손이 칼자루에 가장 가까운 순간(측정: 뽑기 0.30 s, 넣기 0.37 s)에 검을 옮겨 붙인다.
/// 애니메이터 float 파라미터 "Drawn"(0 = 칼 넣은 이동, 1 = 칼 든 이동)을 부드럽게 바꾼다.
/// 2026-10-05: 뽑기·넣기를 직접 만든 클립(Haesin_Draw / Haesin_Sheathe, 각 1.0 s — Editor/SwordDrawClipTool)으로 바꿈(사용자 지시: 허리 곧게,
/// 오른쪽으로 뽑아 앞으로 가져와 두 손). 붙이는 시각 = 뽑기 0.26 s · 넣기 0.70 s. 팔이 짧아 칼집이 그대로면 손목이 꺾여 못 잡으므로
/// 동작 중 칼집 홀더(hipHolder)를 turnLocalPos/Euler 만큼 돌렸다 되돌림(왼손이 칼집을 돌려 미는 것처럼 — 시각표는 SwordDrawClipTool.TurnWeight 와 같음).
/// </summary>
public class WeaponHolder : MonoBehaviour
{
    public Transform sword;
    public Transform hipHolder;
    public Transform handSocket;
    [Tooltip("시작할 때 뽑아 든 상태로.")] public bool drawnAtStart = true;
    [Tooltip("칼집 안에서의 검 로컬 회전(원본 프리팹 값).")] public Vector3 sheathedLocalEuler = new Vector3(270f, 0f, 0f);
    [Tooltip("칼을 넣으면 숨길 렌더러 — 모델에 이미 칼이 붙어 있는 일본 장수(Shogun_Sword)는 환도 칼집을 끄고 검은 뽑았을 때만 보이게(2026-10-06).")] public Renderer[] hideWhenSheathed = new Renderer[0];

    [Header("뽑기/넣기 애니메이션")]
    public Animator animator;
    [Tooltip("뽑기·넣기를 재생하는 애니메이터 레이어.")] public string actionLayer = "UpperBody";
    public string drawState = "Equip";
    public string sheatheState = "Unequip";
    [Tooltip("뽑기 동작 길이(초).")] public float drawDuration = 1.0f;
    [Tooltip("뽑기 중 검을 손으로 옮기는 시각(초).")] public float drawAttachTime = 0.26f;
    [Tooltip("넣기 동작 길이(초).")] public float sheatheDuration = 1.0f;
    [Tooltip("넣기 중 검을 칼집으로 옮기는 시각(초).")] public float sheatheAttachTime = 0.70f;
    [Tooltip("이동 블렌드 파라미터 이름(0 = 칼 넣음, 1 = 칼 듦).")] public string drawnParam = "Drawn";
    [Tooltip("Drawn 파라미터·레이어 가중치 변화 속도(1/초).")] public float blendSpeed = 6f;

    [Header("칼집 돌리기 (뽑기·넣기 중)")]
    [Tooltip("동작 중 칼집 홀더를 돌림(클립을 만들 때 쓴 값과 같아야 손이 손잡이에 맞음).")] public bool turnSheath = true;
    [Tooltip("다 돌렸을 때 홀더 로컬 위치 더하기.")] public Vector3 turnLocalPos = Vector3.zero;
    [Tooltip("다 돌렸을 때 홀더 로컬 회전(원래 회전 앞에 곱함).")] public Vector3 turnLocalEuler = new Vector3(32.13f, 246.02f, 331.64f);
    [Tooltip("뽑기: 돌리기 시작·끝, 되돌리기 시작·끝(초).")] public Vector4 drawTurnTimes = new Vector4(0.04f, 0.22f, 0.60f, 0.85f);
    [Tooltip("넣기: 돌리기 시작·끝, 되돌리기 시작·끝(초).")] public Vector4 sheatheTurnTimes = new Vector4(0.30f, 0.46f, 0.74f, 0.92f);

    public bool IsDrawn { get; private set; }
    /// <summary>뽑기/넣기 동작 중 검이 손↔칼집으로 옮겨지는 순간(true = 뽑음). 즉시 Draw()/Sheathe() 에서는 부르지 않는다.</summary>
    public System.Action<bool> OnSwordMoved;
    /// <summary>뽑거나 넣는 중.</summary>
    public bool IsBusy { get { return _action != 0; } }

    int _layer = -1; int _action; float _actionTime; bool _attached; float _layerW; float _drawnValue;
    Vector3 _holderPos0; Quaternion _holderRot0; bool _haveHolder;
    static readonly int EmptyHash = Animator.StringToHash("Empty");

    void Awake()
    {
        if (sword == null || hipHolder == null || handSocket == null)
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (sword == null && t.name == "Hwando_Sword") sword = t;
                else if (hipHolder == null && t.name == "Hwando_Sheathed") hipHolder = t;
                else if (handSocket == null && t.name == "Socket_Hwando_Hand_R") handSocket = t;
            }
        if (animator == null) animator = GetComponent<Animator>();
        if (animator != null && !string.IsNullOrEmpty(actionLayer)) _layer = animator.GetLayerIndex(actionLayer);
        if (hipHolder != null) { _holderPos0 = hipHolder.localPosition; _holderRot0 = hipHolder.localRotation; _haveHolder = true; }
        if (drawnAtStart) Draw(); else Sheathe();
        _drawnValue = IsDrawn ? 1f : 0f;
        SetParam();
    }

    /// <summary>즉시 손에 쥔다(애니메이션 없음).</summary>
    public void Draw()
    {
        if (sword == null || handSocket == null) return;
        sword.SetParent(handSocket, false);
        sword.localPosition = Vector3.zero; sword.localRotation = Quaternion.identity; sword.localScale = Vector3.one;
        IsDrawn = true;
        SetHidden(false);
    }

    /// <summary>즉시 칼집에 넣는다(애니메이션 없음).</summary>
    public void Sheathe()
    {
        if (sword == null || hipHolder == null) return;
        sword.SetParent(hipHolder, false);
        sword.localPosition = Vector3.zero; sword.localRotation = Quaternion.Euler(sheathedLocalEuler); sword.localScale = Vector3.one;
        IsDrawn = false;
        SetHidden(true);
    }

    /// <summary>뽑기 동작 시작. 이미 들었거나 동작 중이면 false.</summary>
    public bool BeginDraw()
    {
        if (IsDrawn || IsBusy) return false;
        StartAction(1, drawState); return true;
    }

    /// <summary>넣기 동작 시작.</summary>
    public bool BeginSheathe()
    {
        if (!IsDrawn || IsBusy) return false;
        StartAction(-1, sheatheState); return true;
    }

    public bool Toggle() { return IsDrawn ? BeginSheathe() : BeginDraw(); }

    void StartAction(int dir, string state)
    {
        _action = dir; _actionTime = 0f; _attached = false;
        if (animator != null && _layer > 0) animator.CrossFadeInFixedTime(state, 0.1f, _layer, 0f);
        else { if (dir > 0) Draw(); else Sheathe(); _action = 0; }
    }

    void Update()
    {
        if (_action != 0)
        {
            _actionTime += Time.deltaTime;
            float attach = _action > 0 ? drawAttachTime : sheatheAttachTime;
            float dur = _action > 0 ? drawDuration : sheatheDuration;
            if (!_attached && _actionTime >= attach)
            {
                _attached = true;
                bool drawing = _action > 0;
                if (drawing) Draw(); else Sheathe();
                if (OnSwordMoved != null) OnSwordMoved(drawing); // 효과음 등(애니메이션으로 뽑고 넣을 때만)
            }
            if (_actionTime >= dur)
            {
                _action = 0;
                if (animator != null && _layer > 0) animator.CrossFadeInFixedTime(EmptyHash, 0.1f, _layer, 0f);
            }
        }
        // 칼집 돌리기
        if (_haveHolder && turnSheath)
        {
            float w = 0f;
            if (_action != 0) { Vector4 tt = _action > 0 ? drawTurnTimes : sheatheTurnTimes; w = Smooth(tt.x, tt.y, _actionTime) * (1f - Smooth(tt.z, tt.w, _actionTime)); }
            hipHolder.localPosition = _holderPos0 + turnLocalPos * w;
            hipHolder.localRotation = Quaternion.Slerp(_holderRot0, Quaternion.Euler(turnLocalEuler) * _holderRot0, w);
        }
        // 상체 레이어: 동작 중에만 1 (끝나면 부드럽게 0)
        float wantW = _action != 0 ? 1f : 0f;
        _layerW = Mathf.MoveTowards(_layerW, wantW, blendSpeed * 1.5f * Time.deltaTime);
        if (animator != null && _layer > 0) animator.SetLayerWeight(_layer, _layerW);
        // 이동 블렌드: 검이 옮겨진 뒤 바뀐다
        _drawnValue = Mathf.MoveTowards(_drawnValue, IsDrawn ? 1f : 0f, blendSpeed * Time.deltaTime);
        SetParam();
    }

    void SetHidden(bool hidden)
    {
        if (hideWhenSheathed == null) return;
        for (int i = 0; i < hideWhenSheathed.Length; i++) if (hideWhenSheathed[i] != null) hideWhenSheathed[i].enabled = !hidden;
    }

    static float Smooth(float a, float b, float t) { float k = Mathf.Clamp01((t - a) / (b - a)); return k * k * (3f - 2f * k); }

    void SetParam()
    {
        if (animator != null && !string.IsNullOrEmpty(drawnParam)) animator.SetFloat(drawnParam, _drawnValue);
    }
}
