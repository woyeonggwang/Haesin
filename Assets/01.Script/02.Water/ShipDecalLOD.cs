using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 배 한 척의 물 데칼(항적 거품 + 뱃머리 파도) 스위치.
///
/// HDRP 의 워터 데칼은 한 장면에 96개까지만 그려진다. 배 한 척이 2개를 쓰므로
/// 함대가 커지면 금세 상한에 닿고, 상한을 넘으면 어느 배의 데칼이 사라질지
/// 예측할 수 없다. 게다가 수평선 너머의 배가 만드는 항적은 화면에 보이지도 않는다.
///
/// 그래서 이 컴포넌트는 배마다 붙어 자기 데칼을 켜고 끌 수 있게만 해두고,
/// 실제로 "누구를 켤지"는 WaterDecalLODManager 가 카메라 거리 순으로 정한다.
///
/// 끄는 방식은 데칼을 들고 있는 자식(WakeFoam / BowWave)을 비활성화하는 것이다.
/// HDRP 는 컴포넌트가 비활성일 때 목록에서 빼므로 상한 계산에서도 빠진다.
/// ShipWakeFoam / ShipBowWave 본체는 건드리지 않아서, 다시 켜면 그대로 이어진다.
/// </summary>
[DisallowMultipleComponent]
public class ShipDecalLOD : MonoBehaviour
{
    [Tooltip("항적 거품 오브젝트. 비우면 자식에서 WakeFoam 을 찾는다.")]
    public Transform wakeFoam;
    [Tooltip("뱃머리 파도 오브젝트. 비우면 자식에서 BowWave 를 찾는다.")]
    public Transform bowWave;

    [Header("상태 (읽기 전용)")]
    [Tooltip("지금 이 배의 데칼이 켜져 있는지.")]
    public bool decalsOn = true;
    [Tooltip("카메라와의 거리(m).")]
    public float cameraDistance;

    /// <summary>씬에서 활성 상태인 모든 배의 데칼 스위치.</summary>
    public static readonly List<ShipDecalLOD> All = new List<ShipDecalLOD>();

    private bool _resolved;

    void OnEnable()
    {
        Resolve();
        if (!All.Contains(this)) All.Add(this);
    }

    void OnDisable()
    {
        All.Remove(this);
    }

    void Resolve()
    {
        if (_resolved) return;
        if (wakeFoam == null) wakeFoam = transform.Find("WakeFoam");
        if (bowWave == null) bowWave = transform.Find("BowWave");
        _resolved = wakeFoam != null || bowWave != null;
    }

    /// <summary>이 배의 데칼을 켜거나 끈다.</summary>
    public void SetDecalsActive(bool on)
    {
        Resolve();
        if (decalsOn == on && _resolved) return;
        decalsOn = on;

        if (wakeFoam != null && wakeFoam.gameObject.activeSelf != on)
            wakeFoam.gameObject.SetActive(on);
        if (bowWave != null && bowWave.gameObject.activeSelf != on)
            bowWave.gameObject.SetActive(on);
    }
}
