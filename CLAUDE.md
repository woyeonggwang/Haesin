# CLAUDE.md — 해신(HaeSin)

조선을 배경으로 한 오픈월드 해상 전투 게임(어쌔신크리드 오디세이의 해전과 유사). Unity 단일 프로젝트.
이 파일에는 **현재 상태**와 **기억해야 할 시행착오**만 적는다. 작업 과정·검증 로그·캡처 목록은 적지 않는다(자세한 과거 기록은 git 이력에 있음).
추측은 적지 않고, 확인되지 않은 것은 "확인 필요"로 표시한다.

## 0. 작업 규칙 (사용자 지시)

- **기록**: 이 파일 하나만 고친다. 세션마다 별도 md 문서를 만들지 않는다. 끝난 일은 결과만 남기고 과정은 지운다. 프로젝트(claude.ai) 문서 `claude/CLAUDE.md` 는 이 파일과 같게 맞춘다.
- **이 파일에는 Blender 관련 내용을 적지 않는다.**
- **라이트맵/APV 베이크는 최종 단계에 한 번만** 굽는다. 중간에 굽지 않는다.
- 근접전(칼싸움)은 배에서 내린 상태(OnFoot)에서만. 배 위에서는 배 조작만.
- 해 본 것만 "됐다"고 말하고, 안 해 본 것은 해 봐야 안다고 한다.

## 1. 환경 / 버전 고정

| 항목 | 값 |
|---|---|
| 프로젝트 경로 | `D:\Unity\Haesin` (git 저장소, 사용자가 커밋·푸시) |
| Unity | **6000.3.17f1** (Unity 6) |
| 렌더 파이프라인 | **HDRP 17.3.0**. URP 17.3.0 패키지도 설치돼 있으나 미사용 |
| 주요 패키지 | Input System 1.19.0, Cinemachine 2.10.7, VFX Graph 17.3.0, AI Navigation 2.0.12, Timeline 1.8.12, uGUI 2.0.0, Visual Scripting 1.9.11, Test Framework 1.6.0, **Magica Cloth 2** |
| MCP | `com.coplaydev.unity-mcp` (git main) |
| 빌드 씬 목록 | **비어 있음** |
| 씬 | `Assets/00.Scenes/SampleScene.unity`(메인 게임), `CharacterTest.unity`(캐릭터·칼싸움 시험), `VFX.unity` |
| 입력 | 배 이동/부스트 = Input System(`Assets/InputActions.inputactions`, PlayerInput Send Messages: `OnMove`, `OnBoost`). 나머지(카메라·포격·걷기·전투·메뉴·테스트 키)는 레거시 `Input`. Active Input Handling = Both |
| HDRP Water | Ocean = `WaterSurface`(OceanSeaLake), `scriptInteractions = true` 필수, decalRegionSize 200×200. 워터 마스크는 **텍스처 방식**(전역 설정 Water Decal Mask and Current = 꺼짐 — 켜면 `WaterShoreMask` 가 무시됨) |
| 태그 / 레이어 | 태그 Player(플레이어 배 `panok`), Enemy, Navy. 레이어 추가: `Showcase`(8, 메뉴 3D 뷰어 전용), `StepSurface`(9, 계단 디딤판 — 발 IK 전용, CharacterController 는 통과) |
| 라이선스 | ARPG Samurai(Fab Standard License — 등급 Personal/Professional 은 Fab Support 문의로만 확인 가능). 효과음은 CC0(각 폴더 LICENSE 동봉) |

### 코드 규칙
- Unity 6 API(`linearVelocity`, `FindFirstObjectByType` 등). 구 API 금지.
- 스크립트는 `Assets/01.Script/<NN.카테고리>/`. **파일 이름 = 클래스 이름**(한 파일에 MonoBehaviour 여러 개 넣으면 씬에 저장되지 않아 플레이 정지 시 컴포넌트가 사라짐).
- 주석·툴팁은 한국어. 설계 이유("예전엔 X 였는데 Y 때문에 Z")를 주석에 남기는 것이 관례.
- UTF-8 저장(BOM 유무 혼재 — 통일 여부 미정).
- Volume 프로파일은 런타임 사본(`volume.profile`)만 건드린다. 에셋에 컴포넌트를 추가할 땐 `profile.Add<T>()` 뒤 반드시 `AssetDatabase.AddObjectToAsset(comp, profile)` + `SaveAssets()`(안 하면 도메인 리로드 때 사라짐).
- **플레이 중에는 스크립트를 저장하지 않는다**(도메인 리로드로 비직렬화 필드가 null — RainMaker NRE, 메뉴 복구 목록 소실 경험).
- 스크립트를 delete→create 하지 않는다(.meta GUID 가 바뀌어 씬 컴포넌트가 Missing). 수정은 기존 파일을 고친다.
- 씬 컴포넌트의 직렬화된 값이 스크립트 기본값보다 우선한다 — 기본값만 바꾸면 기존 씬엔 반영 안 됨.

## 2. 폴더 구조 (Assets/)

```
00.Scenes/            SampleScene, CharacterTest, VFX
01.Script/
  00.Player/00.Control/   CameraFollow, CannonControl, MoveControl, PlayerRespawn
  00.Player/01.Component/ Bullet, ExploseEfx
  01.Enemy/               EnemyShipBase, EnemyAI, EnemyCannonControl, SekiScoutAI, EnemyFleetSpawner
  02.Navy/                NavyShipBase, NavyPanokAI, NavyCannonControl
  02.Water/               ShipBuoyancy, WaterHeightUtil, ShipWakeFoam, ShipBowWave, ShipDecalLOD,
                          WaterDecalLODManager, WaterRipple, WaterRippleSpawner, ShipImpactSensor, OarAnimator, WaterShoreMask
  03.Weather/             WeatherSystem, WeatherPreset, WeatherTestKeys, FlagWind, LightningStorm
  04.Faction/             ShipFaction (+ enum Faction)
  05.Damage/              ShipHealth, ShipBurnVisual, PlayerHealthUI, DamageFXDistanceCull, BallisticAim
  06.Merchant/            MerchantAI, BountySystem
  07.Environment/         TorchFlicker, RespawnPoint, BaseSafeZone
  08.Character/           PlayerCharacter, LandingPoint, OnFootController, BoardingSystem, FootIK, PlayerOrbitCamera(+ICharacterMover),
                          CharacterOutfit, SpringBoneSystem, SkirtLegFollow, EyeBlink, FacialExpression,
                          CharacterTestController·TestCircleWalker(CharacterTest 전용)
  09.Crew/                ShipCrew, CrewMember
  10.UI/                  MapSystem(+MapSystem.Atlas.cs), UIKit, GameMenuPause, ShowcaseViewer, ShipMenu, InventoryMenu, MenuArt, ResourceBar, UITooltip
  11.Audio/               ShipAudio, SeaAmbientAudio, OneShotAudio, BulletAudio, SwordAudio, FootstepAudio
  12.Economy/             PlayerResources, ShipUpgrades
  13.Combat/              AttackData, CombatStats, SwordCombatant, PlayerCombatController, EnemySwordAI,
                          CombatHUD, WeaponHolder, HwandoGripIK, CombatAutoTest(테스트 전용)
  Editor/                 ARPGFabImportPostprocessor, BlinkBlendShapeNormals
02.Resources/00.Component/
  NewShip/               anteck, panok, seki 프리팹
  02.Water/              ShipRipple.prefab
  ShipCloth/             깃발(Cloth) 포함 배 모델 프리팹
  06.Character/Haesin/   캐릭터 FBX·머티리얼·텍스처·애니메이션(§5)
  07.Animation/          ARPGSamurai(Unity 판 rig_*), ARPG_Samurai_Fab(Fab 판 FBX 339개)
02.Resources/01.Asset/   외부 에셋(종류별)
  Animations/  Quaternius_UAL(CC0), Kevin Iglesias(Human Melee Animations FREE)
  Models/      JoseonTongsinsaseon, SwampBridge4k, Free_Rocks
  VFX/         RainMaker, LightningBolt, Hovl Studio, CamelotVFX_ Fire&Smoke(출처 확인 필요)
  Textures/    TextureHaven, ObjectiveEnvironment_Assets
  Audio/(Sea, Cannon, Sword, Footstep), Materials/, TerrainLayers/
Resources/            ShipMenuArt/, MenuCommonArt/, MapAtlas/ (런타임 Resources.Load)
StreamingAssets/Flags/  깃발 이미지(png/jpg) 넣는 곳
MagicaCloth2/         Magica Cloth 2
InputActions.cs       자동 생성(수정 금지)
Shaders/              Unreal 이식 셰이더. `Haesin/ShipBurn` 셰이더 파일 위치는 확인 필요
Samples/              HDRP Water Samples(패키지 샘플 — 그대로 둠)
```

## 3. 게임 시스템 (현재 상태)

### 진영 (`ShipFaction`)
- `enum Faction { Pirate(플레이어), Navy(관군 판옥선 + 상선), Japanese(왜군 세키부네/안택선) }`. **적 판정은 `ShipFaction.IsHostileTo`("나와 다른 진영이면 적") 한 곳**. 관군은 플레이어·왜군 모두의 적.
- 활성 배는 `ShipFaction.All` 에 자기 등록. `FindNearestHostile(self, range)`, `Of(GameObject)`(부모까지 탐색 — 포탄은 자식 콜라이더에 맞음), `IsTargetable()`(완파·침몰 중·안전지대 안이면 표적 아님).
- 태그(Player/Enemy) 판정은 `ShipFaction` 없는 대상용 폴백으로만 남음(`Bullet.ShouldExplode`, `EnemyShipBase.Retarget`).

### 플레이어 배 (`panok`, 태그 Player)
- `MoveControl`: 힘 기반(AddForce/AddTorque). 축 분담 — 물리 = XZ 위치 + Y 회전, `ShipBuoyancy` = Y 높이 + 피치/롤. Rigidbody 저항 0(**물 저항은 MoveControl.FixedUpdate 가 직접 계산** — 끄면 배가 떠내려감), Interpolate, ContinuousDynamic. 코드 기본 mass 1000·maxSpeed 10·boost ×1.6, 방향타 0.4~4 m/s. 씬 템플릿의 속도·가속은 코드 기본의 1.5 배.
  - **정박**: `SetDocked(bool)` — 입력 무시 + 정박 위치·방향으로 끌어당김(anchorStiffness 0.6 / anchorDamping 1.5 / anchorYawStiffness 0.8). 하차 중 사용. `Halt()` 는 속도·currentSpeed·boost 모두 0.
- `CannonControl`: 우클릭 줌(FOV 60→28) + 좌클릭 발사, 쿨다운 4 s. 좌/우현 = 선체 right 축과 조준 방향 내적(deadzone 0.08). 포탄에 진영·강화 배율을 찍음.
- `CameraFollow`: `panok/CameraPivot` 에 부착, 마우스 오빗(pitch −30~60), 우클릭 시 레이캐스트로 조준점. 침몰 중에만 카메라 y ≥ 해수면 +3 m. **Main Camera 는 CameraPivot 의 자식**(로컬 (0, 0.3, −0.471), 회전 (18.3, 0, 0)), far clip 4000, near 0.3.
- `Bullet`: 플레이어 포탄만 발사 직후 위로 730 힘 보정. 같은 진영·침몰 중인 배엔 안 터짐. y < −20 이면 파괴. 바위·지형에 맞으면 터지지 않고 굴러감.
- `BallisticAim`: 적 포격 예측 사격(비행시간 × 표적 수평 속도 3회 반복, leadFactor 1, maxLeadTime 6 s). 탄도 공식 `v² = g·d² / (2(d·tanθ − Δh)cos²θ)`, 실패 시 직사.
- `OarAnimator`: `OarGroup/Left|Right` 자식 노의 로컬 X 만 회전(우현 노는 Y=180). 멈춰 있어도 idle 0.25 회/s 로 저음, **정박 중엔 1.5 s 에 걸쳐 멈추고 앞 자세(좌 −25° / 우 25°)**, 서지 없음.

### AI
- `EnemyShipBase`(왜군)와 `NavyShipBase`(관군·상선)는 **의도적으로 분리된 복제본**(힘 기반 항해 + 순찰 + 0.5 s 재표적). 순찰 = 전방 편향 0.45 + 앵커 드리프트 1.5 m/s.
- `EnemyAI`(안택선): Patrol/Chase/Broadside. 씬 값 det 100 / lose 130 / atk 70 / brk 85(`OnValidate` 가 lose ≥ det×1.2, brk ≥ atk×1.2 강제). 추격 리드 ≤ 2.5 s.
- `SekiScoutAI`(세키부네, 비무장): 발견 시 350 m 안 EnemyAI 에 25 s 경보, 55 m 거리 유지.
- `NavyPanokAI`: EnemyAI 뼈대 + 접현 회피 28 m + 300 m 내 아군 소집. 추격 90 m 이상이면 부스트(×1.6).
- 포격(`EnemyCannonControl`/`NavyCannonControl`): Broadside + 사거리(90/95) + 현측 55° 안일 때 일제사. 관군 최대 8문, 재장전 8 s.
- `MerchantAI`(Navy, 비무장): Sail/Follow/Flee. 격침 시 `BountySystem.ReportPlunder`.
- `BountySystem`: 플레이어가 상선을 약탈할 때만 현상금 +1(120 s 후 개별 만료) → `EnemyFleetSpawner.maxNavyFleets` 증가(base 3, hardMax 8).
- `EnemyFleetSpawner`: 씬의 원본 배(anteck/seki/panok_navy/merchant)가 템플릿 — Start 에서 끄고 복제 스폰. 플레이어 기준 250~500 m, 거리 분포 `u^(1/1.8)`, 900 m 밖 정리, 8 s 간격.
- **베이스 안전지대** `BaseSafeZone`(PlayerRespawnPoint 에 부착, **반경 350** — 섬·동굴의 가장 먼 끝이 리스폰 지점에서 약 255 m): 스폰 위치에서 제외, AI 목적지를 경계 밖으로 밀어냄, 안에 있는 배는 표적 아님. 스포너는 함대 중심을 반경 + 흩어짐 + `safeZoneMargin`(60) 밖에서 뽑고, 배 한 척씩도 경계 + 60 밖으로 밀어냄.

### 물 (HDRP Water)
- `WaterHeightUtil.Sample` = `ProjectPointOnWaterSurface` 래퍼(캐시 없음).
- `ShipBuoyancy`(order 100): 4점 샘플, 비-kinematic 이면 Physics 모드(스프링 20 / 감쇠 8 / 최대 30, 중력 끔).
- `ShipWakeFoam`(WaterFoamGenerator), `ShipBowWave`(WaterDecal + `Water Decal Sample` 셰이더 — **키워드** `_TYPE_BOW_WAVE` 등으로 분기).
- `ShipDecalLOD` + `WaterDecalLODManager`: HDRP 워터 데칼 **씬당 96개 상한** → 카메라 가까운 8척만 켬(150 m + 15 m 히스테리시스).
- `WaterRippleSpawner`(풀: 충돌 8 / 항적 10 / 물보라 8), `ShipImpactSensor`(충돌 6프레임 전 속도로 세기 계산).
- `WaterShoreMask`(Ocean): 육지 가까운 바다의 파도를 줄이는 `waterMask`(R 너울 / G agitation / B 리플 — CPU 시뮬레이션에도 반영)를 굽는다. 육지 = 위에서 쏜 레이가 처음 맞는 리지드바디 없는 콜라이더가 y −0.5 이상인 곳(동굴 지붕 밑 물도 포함). 중심 (0, 0), 1000 m, 1024², 육지 옆 R 0.1 / G 0.2 / B 0.6 → 200 m 에 걸쳐 1. 결과 `02.Resources/00.Component/02.Water/WaterShoreMask.png`. **터레인·마을을 바꾸면 컴포넌트 컨텍스트 메뉴 "해안 마스크 굽기"로 다시 굽는다.** 폭풍우(바람 140) 측정: 먼바다 −21.6~+18.4 m, 선착장 주변 −1.3~+0.2 m, 동굴 안 −3.5~+1.6 m.

### 날씨 (`WeatherSystem`)
- 인스펙터 인라인 프리셋 리스트(SO 아님)를 가중치로 뽑아 25 s SmoothStep 블렌드. WaterSurface·CloudLayer(**cloudCoverage 는 R/G/B/A 채널을 순차로 켬**)·VolumetricClouds·하늘 노출·Fog(meanFreePath/tint 만)·태양 Lux·RainMaker 를 구동.
- 프리셋 바람: 맑음 65, 나머지는 `65 + (w−35)·0.5`. 비 0.6 / 폭풍우 1.
- 비 가림 `UpdateRainCover()`: 0.25 s 마다 배 주변 3×3 점(간격 40 m, y 35)에서 위로 600 m 레이 → 막힌 비율 `rainCovered01` 만큼 비를 줄임(동굴 안에서 비 안 옴).
- `FlagWind`(worldAccelerationScale 0.3, damping 0.1), `LightningStorm`(바람 ≥ 90), `WeatherTestKeys`(`]`/`[`/1~9/`\`/0).
- `LightningStorm` 섬광은 그림자 없는 방향광 → **카메라 위가 막힌 비율(`shelter01`, 중심 + 둘레 4점에서 위로 레이 400 m)만큼 줄인다**(동굴 안에서 안 번쩍임).

### 피해·침몰·리스폰
- `ShipHealth`: 손상 단계 0~3(0.75/0.5/0.25), 적 150 m 밖 10 s 후 초당 5 회복. 침몰 = 대부분 컴포넌트 off·kinematic·콜라이더 off·기울며 하강, `DamageFX` 파티클 방출 정지. `OnSunk`, `Revive()`, `EndSinkAndRestore()`.
- **플레이어 배**: maxHealth 300, diesAtZeroHealth true, sinkOnDeath true, destroyAfterSink false, sinkDuration 14 s, sinkDepth 30 m.
- `PlayerRespawn`: `OnSunk` → `Revive()` → 그 다음 Rigidbody 위치·회전 대입(순서 중요: kinematic 을 먼저 풀어야 함). `fadeDelay` 0(화면 전환 연출 자리).
- 리스폰 지점 `PlayerRespawnPoint` (−110, 0.3, −4), 뱃머리 +z (수심 약 −20 m).
- `ShipBurnVisual`: `Haesin/ShipBurn` 셰이더로 교체. 제외: 이름에 flag/flg, `crew_`/`char_` 로 시작, 투명 머티리얼. 불 파티클은 `DamageFX` 아래 미리 넣어 두고 켜기만 함. 불 머티리얼 `Oil_Fire_v1~3` 은 `_EnableFogOnTransparent = 1`.
- `DamageFXDistanceCull`: 카메라 320 m 밖이면 불·연기 렌더러 끔(280 m 에서 다시 켬).
- `PlayerHealthUI`: 하단 체력바(코드 생성).

### 선원 (`ShipCrew`, `CrewMember` — 임시 큐브)
- 배 자식 `Crew` 아래: panok 10, panok_navy 10, anteck 12, seki 6(상선 없음). 머티리얼 `Crew_Pirate/Navy/Japanese`.
- 배 루트 스케일 66.79 → `Crew`·`CharacterSeat` 컨테이너 localScale = 1/lossyScale 로 두어 **그 아래는 미터 단위**.
- 갑판 높이는 메시 삼각형(xz 무게중심 포함 판정)으로 구한 정적 값 — 배·위치를 바꾸면 다시 맞춰야 함.
- `AddRecruit()`: recruitSpots 순서로 복제, maxCrew 16.

### 자원·강화 (씬 루트 `GameData`)
- `PlayerResources` 목재/철/금화 초기 200/120/1000.
- `ShipUpgrades`: 함포(피해 ×(1 + 0.15·lvl))·화살·선원 체력(값만 저장 — 시스템 없음) 최대 10, 깃발 색·이미지(MaterialPropertyBlock, 텍스처 이름 `Flag_*`, `StreamingAssets/Flags/`).

### 소리 (`11.Audio/`, 음원 모두 CC0)
- `SeaAmbientAudio`(WeatherSystem 오브젝트): 2D 0.2, 동굴 가림 시 ×0.25, 0 에서 페이드인.
- `ShipAudio`(panok): 선체 물살(0.8→12 m/s, 최대 0.55) + 노 소리(젓기 속도 기준, 최대 0.35).
- `BulletAudio`(포탄 템플릿 `Sphere`): 발사 0.5(max 900 m) / 비행 휘파람(합성 음원, 속도 3 m/s 아래 0) / 명중 0.5(max 600 m). `OneShotAudio` = 감쇠 지정 가능한 원샷.
- `SwordAudio`(캐릭터): 휘두르기 0.1(windup − 0.28 s 에 재생), 막기·튕겨내기 부딪힘 3종 0.6, 뽑기·넣기 각 3종 0.1(넣기 소리는 CC0 조각으로 합성). 들어 보고 고른 것 아님.
- `FootstepAudio`(캐릭터, OnFoot 에서만): 발 닿음 = `FootIK.animLiftLeft/Right`(IK 전 애니메이션 발바닥 높이)가 0.05 m 위로 들렸다가 0.025 m 아래로 내려올 때, 캐릭터 속도 0.4 m/s 이상, 같은 발 0.18 s 간격. 바닥 = 발 아래 레이: 터레인은 가장 진한 레이어 이름(grass/forrest → 풀, sand/gravel → 모래, 그 밖 → 돌), 그 밖 콜라이더는 이름(자신+최상위)에 stair/bridge/deck/wood/dock → 나무, rock/cave/stone/cliff → 돌, 없으면 나무. 음원 Kenney Impact Sounds(CC0) wood/grass/snow(모래용)/concrete(돌용) 각 5개, 모노, 볼륨 0.1(사용자 지정, 달리기 ×1.3), 3D min 4 / max 30 m, 피치 0.92~1.08. 측정: 걷기 0.41 s 간격(선착장·계단 모두). 들어 보고 고른 것 아님.

### UI
- 공통 일시정지 `GameMenuPause`(static): timeScale 0, 물 timeMultiplier 0, 소리 정지, `disableWhileOpen`(CameraFollow, CannonControl, WeatherTestKeys, PlayerOrbitCamera, PlayerCombatController) 끔. 한 번에 하나. `MapSystem.disableWhileOpen` 은 씬 직렬화 값(같은 목록으로 맞춰 둠).
- `MapSystem`: M 전체 지도(해도 `Resources/MapAtlas/OceanChart.png`) + 미니맵(같은 해도를 `ZoomUV` 로 300 px 만 확대, 적대 진영만 표시).
- 함선 메뉴 `ShipMenu`(Y/ESC): 좌측 이미지 타일 5개(`Resources/ShipMenuArt/`), 가운데 전체 화면 3D 배(`ShowcaseViewer` fullScreen + 배경 쿼드), 우측 상세·강화·깃발 UI, `ResourceBar`.
- 인벤토리 `InventoryMenu`(I/ESC): 좌측 캐릭터 3D 뷰어(전용 조명 켬), 우측 탭 5개 + 빈 슬롯 20칸(아이템 시스템 없음).
- `ShowcaseViewer`: 레이어 Showcase 전용 카메라 + RT, 스테이지 (20000|20300, 600, 20000). 대상은 **MeshFilter/MeshRenderer 만 계층째 복사**, SkinnedMeshRenderer 는 `BakeMesh(mesh, false)` 후 노드 월드 스케일 1. 고정 노출 13.5, 물·구름·안개 끔. `stageLights`(키 30000 / 보조 10000 / 역광 15000 lux 스포트, 카메라 기준 고정). 드래그는 마우스 아래 최상위 UI 가 뷰어일 때만.
- `UIKit`: 색은 `SolidSprite(color)`(픽셀에 `.linear` 로 구움) + Image.color 흰색. EventSystem 없으면 생성(InputSystemUIInputModule).
- `MenuArt`(`Resources/MenuCommonArt/` 버튼 9-slice: Normal border 44 / ppu 배수 4, Selected 80 / 6), `ResourceBar`(아이콘 + 숫자), `UITooltip`.

## 4. 월드 (SampleScene)

### 씬 루트
`panok`(플레이어 배) · `anteck`/`seki`/`panok_navy`/`merchant`(스포너 템플릿) · `EnemyFleetSpawner` · `WeatherSystem`(+WeatherTestKeys, LightningStorm, SeaAmbientAudio) · `WaterDecalLOD` · `BountySystem` · `WaterFX` · `Ocean` · `BaseGroup`(Base 터레인, Cave, Light, 선착장) · `Sky and Fog Global Volume` · `CaveInteriorVolume` · `CaveReflectionProbe` · `Adaptive Probe Volume` · `PlayerRespawnPoint` · `LandingPoint_Dock` · `Boarding` · `MapSystem` · `GameData` · `ShipMenu` · `InventoryMenu` · 비활성: `Sphere`(포탄 원본), `Explosion_v1`, `BuoyancyObjects`.

### 터레인 (`BaseGroup/Base`, `Assets/New Terrain.asset`)
- 500×500 m, 높이 스케일 600 m, 원점 (−250, −60, −250). 실제 높이 −60 ~ +19 m, 해수면 y 0. 나무·디테일 없음.
- 텍스처 8레이어(TextureHaven 4K, `01.Asset/TerrainLayers/`): 해저 coral_gravel(tile 23)+rock_ground_02(31), 모래 aerial_sand(17), 풀 aerial_grass_rock(19)+forrest_ground_01(27), 바위 rock_ground(15)+rocks_ground_05(21), 절벽 aerial_rocks_01(21). 높이·경사 규칙(높이 < −2.5 해저, ~+3.5 모래, 경사 <22° 풀, 22~42° 바위, >42° 또는 >15 m 절벽) + Perlin 으로 자동 페인트, 구역마다 2종을 저주파 노이즈로 섞음.
- **HDRP TerrainLit 레이어는 8개까지**(9번째부터 검게 렌더됨).
- 터레인은 헤이트맵이라 오버행·동굴 불가 → 동굴은 바위 메시로 덮음, 구멍은 Terrain Holes.

### 동굴 (`BaseGroup/Cave`)
- 바위 91개로 섬을 덮는 돔: Free_Rocks rock1~4(메시 Cube_002/003/004/006) 복제, 삼각형 합계 27,952, 머티리얼 1개(`rocks`, HDRP/Lit), 전부 Static(Batching·Occluder·Lightmap)·MeshCollider·그림자 On. 바운드 (−330, −85, −228) ~ (42, 272, 98), 중심 (−143.9, 93.6, −65.3). 입구는 +z 쪽.
- 조명: APV 베이크(`Assets/Settings/HaesinLighting.lighting`, Progressive GPU), 바위는 ContributeGI + ReceiveGI=LightProbes. 글로벌 `SkyAndFog Profile` 에 HDShadowSettings(maxShadowDistance 700, 캐스케이드 4) — HDRP 기본 150 m 면 돔 그림자가 잘림. 태양 그림자 4096 + Contact Shadows.
- `CaveInteriorVolume`(박스 트리거, 중심 = 동굴 중심, 크기 (382, 367, 336), priority 10, blend 70, `Assets/Settings/CaveInterior Profile.asset`): Exposure Automatic limitMin 11.0 / limitMax 14.5, IndirectLightingController 0.15/0.15, Fog **SkyColor 모드 유지** + tint (0.20, 0.19, 0.19), albedo (0.35, 0.30, 0.26), mfp 600, SSAO 2.5/4. (ConstantColor 검정 안개는 볼륨 경계에서 검은 벽처럼 보여 안 씀.)
- `CaveReflectionProbe`(box projection, 같은 바운드, Custom 베이크 `Assets/00.Scenes/SampleScene/CaveReflectionProbe.exr`) — 동굴 안 바다가 하늘을 반사해 반짝이던 것 해결.
- **바위를 옮기면 APV 와 리플렉션 프로브를 다시 구워야 함**(최종 단계에서 한 번).

### 횃불 (`BaseGroup/Light/Torch` × 8, 섬 능선을 따라 호)
- 각 Torch 아래: `Pole`(Cylinder 0.25×4 m, `MI_Wood.mat`, **그림자 끔**), `TorchLight`(Point, 색 (1.0, 0.55, 0.22), **3,500,000 lm**, range 90, Soft shadow, shapeRadius 0.15, affectsVolumetric 끔, 기둥 꼭대기 +0.5 m, `TorchFlicker` = 세기 ±18%·위치 ±6 cm), `Flame`(`Fire_v1.prefab` 스케일 0.18, 파티클 scalingMode Hierarchy, `glow_particles`·프리팹 Light 비활성, **파티클 렌더러 그림자 전부 끔**).
- 제대로 된 횃불 메시는 없음(프리미티브 기둥).

### 선착장 · 리스폰 · 안전지대
- 선착장 `BaseGroup/swamp-bridge-4k-ready_2016`(x −142~−121, z −6.6~−0.3), 판자 윗면 y 4.745. 메시 7개에 MeshCollider.
- 하차 지점 `LandingPoint_Dock` (−125, 6.5, −3.4), dockRange 35 — 아래로 레이를 쏴 판자 위에 세움.
- 리스폰 `PlayerRespawnPoint` (−110, 0.3, −4) + `BaseSafeZone` 반경 350.

### 섬 배치 계획 (`IslandPlan`, 임시)
- 계획(사용자): base 동남쪽에 일본 마을 중간섬 700×700, 서북쪽에 조선 큰섬 1000×1000 1개 + 작은섬 400×400 2개. 크기 확인용 큐브 4개(높이 35 m, 윗면 해발 15 m, **콜라이더 없음**)를 `IslandPlan` 아래에 두고 `MapSystem.mapGeometryRoots` 에 등록 — 지도는 Play 시작 때 구우므로 옮긴 뒤 Play 하면 반영. 위치는 사용자가 조정 중.

### 원거리 표시
- Main Camera far clip 4000, 글로벌 Fog `maximumHeight` 350(돔 전체가 높이 안개 안). 4000 m 에서 투과율 1 % 대라 자연스럽게 사라짐.

## 5. 플레이어 캐릭터 (Unity)

### 에셋 (`Assets/02.Resources/00.Component/06.Character/Haesin/`)
- FBX: `Haesin_Base`(몸·머리·눈알·핫팬츠 — **Humanoid**, 아바타 유효), `Haesin_Outfit_Hanbok`(Hanbok_Top·Hanbok_Skirt), `Haesin_Shoes_Boots`, `Haesin_Hair_Ponytail`(Generic, Hair_Root·Hair_01~05), `Hwando_Sheathed`(환도·칼집).
- `Haesin_Base`: Body 정점 35,108, 서브메시 9(마지막 = LashLower), 블렌드셰이프 11(`Blink_L/R`, `Blink_L_mid/R_mid`, `MouthOpen`, `Smile_L/R`, `Frown_L/R`, `Pucker`, `Press`), 본 78(Mixamo 52 + 치마 24 + `LeftEye/RightEye`). **Humanoid 에 LeftEye/RightEye 를 매핑하면 아바타가 깨짐** → 매핑 안 함, 시선은 눈 Transform 을 스크립트로 돌리면 됨.
- 임포트: Blend Shape Normals = **None** + `Editor/BlinkBlendShapeNormals.cs`(Haesin_Base 만: 움직인 정점 + 1링에만 법선 차를 넣음). Calculate 면 표정 키를 켤 때 얼굴 전체 음영이 깨짐.
- 머티리얼은 FBX 에 텍스처가 안 들어가므로 HDRP/Lit 머티리얼을 만들고 `ModelImporter.AddRemap` 으로 연결: `M_Haesin_Head`(`Haesin_Head_BaseColor_Baked_v4`, 노멀 `Haesin_Head_Normal_NeckFlat_v4`), `M_Haesin_EyeArea`(`Haesin_EyeArea_BaseColor_Baked_liner2`, 노멀 없음), `M_Haesin_Eye_L/R`(`_i90` 텍스처, Smoothness 0.88), `M_Haesin_Lash`·`M_Haesin_LashLower`(알파 클리핑 0.35, 양면), `M_Haesin_MouthArea`, `M_Haesin_Teeth`(0.55), `M_Haesin_Tongue`(0.5), `M_Haesin_Shorts`((0.145, 0.15, 0.25), 0.2, 양면). 몸 피부 텍스처는 `Haesin_Body_BaseColor_Baked`.
- Unity 에서 캐릭터 오른쪽 = +X(캐릭터가 +Z 를 볼 때).

### 씬 속 캐릭터
- `CharacterTest`: `Haesin_Player`(Haesin_Base.fbx 프리팹 인스턴스), `Haesin_Enemy`(**프리팹 인스턴스 아님** — FBX 를 바꿀 때마다 머티리얼 배열·본 배열·LeftEye/RightEye 위치를 이름으로 직접 맞춰야 함, 현재 비활성으로 저장됨), `CombatHUD`, `Ref_ARPG_Samurai`(비교용), `Ground`.
- `SampleScene`: `panok/CharacterSeat/Haesin_Player`(CharacterTest 의 Player 를 복제 — 공유 프리팹 아님, 한쪽을 고치면 다른 쪽도 고쳐야 함). 자리 = 판옥선 누각 바닥(발 y 6.63), 뱃머리 방향. CharacterTestController·PlayerOrbitCamera 는 뺌.
- Body 렌더러를 스크립트로 바꿀 땐 루트 이름(Haesin_Player/Haesin_Enemy)으로 거를 것(`Ref_ARPG_Samurai` 에도 "Body" 가 있음).

### 옷 · 흔들림
- `CharacterOutfit`: 슬롯 Clothes/Shoes/Hair, 본 이름으로 재연결, `hideBodyParts`(한복 = `Body_UnderTop` 숨김). `Underwear_UnderSkirt`·`Black_Underwear` 는 남색 핫팬츠로 보이게 둠(치마 트임 사이로 보여도 옷의 일부).
- **Magica Cloth 2**
  - 치마 `Cloth_Skirt`: BoneCloth, SequentialLoopMesh, 8가닥(Skirt_F/FL/L/BL/B/BR/R/FR_01, 끝에 잎 본 `_04`), gravity 5, damping 0.1, radius 0.02, normalAxis Forward, angleRestoration 0.35/0.7, angleLimit 60°, tether 0.3, **maxDistance 0.3 + 깊이 곡선 (0,0)(0.33,0.15)(0.66,0.5)(1,1)**, backstop 10/0.02, inertia world 0.35 / local 1 / depth 0.7 / speedLimit 5, 콜라이더 Edge 4개(양 허벅지·정강이).
  - 소매 `Cloth_Sleeves`: MeshCloth(Hanbok_Top, 페인트맵 `Cloth/Hanbok_Top_SleevePaint.png`, `Haesin_Outfit_Hanbok.fbx` isReadable), Line, normalAxis Up, maxDistance 0.12, backstop 10/0.01, 콜라이더 Point 5개.
  - MagicaCapsuleCollider(Y, 시작R/끝R/길이): UpLeg 0.09/0.08/0.48, Leg 0.07/0.05/0.36, Spine1 0.12/0.12/0.20, Arm 0.04/0.03/0.17, ForeArm 0.03/0.03/0.22.
  - 코드로 `CurveSerializeData` 필드는 `.value` 에 대입.
- `SkirtLegFollow`: LateUpdate 에서 치마 뿌리 본을 허벅지 흔들림만큼 돌림(Magica 는 그 뒤에 계산). weight F 0.5, FL/FR 0.9, L/R 0.9, BL/BR 0.3, B 0.2, globalWeight 1, `clearance` 0.12 m(자락이 허벅지 축 안쪽이면 더 벌림), `untrap`(자락이 다리 뒤에 끼면 0.35 s 동안 치마 충돌 끔).
- `SpringBoneSystem`(자체 Verlet): 이제 포니테일만.

### 환도 · 손
- 손 소켓 `Socket_Hwando_Hand_R`(RightHand 자식): localPosition (−0.0232, 0.0760, −0.0449), localEuler (358.7, 184.1, 180.0). 이 캐릭터 RightHand 로컬 +Z 는 몸 뒤쪽이라 소켓 회전으로 맞춤(애니메이션으로는 안 고쳐짐). 환도 메시: 피벗 = 코등이, +Z 칼날(끝 0.689 m), 등 +Y / 날 −Y.
- `WeaponHolder`: 허리 칼집 ↔ 손 소켓 재부모(런타임 — 프리팹 인스턴스 밖으로 자식을 옮기면 저장 안 됨). 뽑기 0.30 s / 넣기 0.37 s 에 옮김, `Drawn` 파라미터 블렌드, `OnSwordMoved` 이벤트. Player 는 칼 넣고 시작.
- `HwandoGripIK`: 전투 상태에서 애니메이션 왼손목이 손잡이 축 0.20 m 안이면 IK 100 %(0.30 m 밖 0 %), 목표 = 손잡이 위 + 축에서 0.078 m. 컨트롤러 Base IK Pass 켬 + 레이어 `LeftGrip`(Kevin Iglesias 왼손가락 마스크, `Human@ObjectGripHands01`).
- 체형(측정): 해신 팔(위팔+아래팔) 0.386 m, 어깨 폭 0.236 m 로 팩 캐릭터보다 짧음 — **판정 거리·접점은 팩 값이 아니라 해신 몸으로 잰다**.

### 애니메이터 (`Haesin_TestLocomotion`)
- `Locomotion` = 블렌드 트리 `Loco_Root`(float `Drawn`): 칼 넣음 `rig_Idle_no_weapon` 0 / `rig_Walk_no_weapon_IPC` 2.69 / `rig_Sprint_Unequip_IPC` 4.06, 칼 듦 `rig_Idle1` 0 / `rig_Walk_Forward_IPC` 2.69 / `rig_Run_Forward_IPC` 3.30 / `rig_Sprint_IPC` 4.06 (Unity 판 rig_ 클립). 임계값 = 클립 몸 이동 속도.
- 전투 상태는 **Fab 판 FBX 클립**: Attack1~4 = Attack_Combo1~4, AttackB1~B5 = Attack_Combo_B1~B5, Thrust = Attack_Sprint, Sweep → Sweep_Swing = Attack_Heavy1_Start → Attack_Heavy1, Heavy = Attack_HeavyB, Heavy2 = Attack_Heavy2, SprintAttack = Attack_Sprint_Unequip, EvadeAttack = Attack_Evade (공격 상태 속도 0.8), Block/Parry/BlockHit(1~3)/Hit(1~3, Hit_Heavy = Hit8) = Guard/Parry/Guard_Hit1~3/Hit1~3, DodgeBack = Dodge, Death/Death_2 = Death2/Death, Broken → BrokenHold(속도 0.067) → BrokenRecover = Guard_Break. Dodge(옆·앞 회피)·Jump 는 아직 임시 머슬 클립.
- 레이어 `UpperBody`(Kevin Iglesias 상체 마스크, Equip/Unequip — 걸으며 뽑고 넣음), `LeftGrip`.
- **쓰지 않는 클립**(치마 속이 보임·뒤집힘): Evade 8방향, Hit_knockdown 계열, 뒤로 넘어지는 Death, Airborne 계열.
- Fab 임포트(`07.Animation/ARPG_Samurai_Fab/`, 339개): 아바타 = `ARPG_Samurai_Anim_UE_A_Bindpose.FBX`(CreateFromThisModel, Chest = spine_02, UpperChest = spine_03 수동), 나머지 CopyFromOther. `Editor/ARPGFabImportPostprocessor.cs` — 이름에서 접두어 제거, 루프 규칙(`_IPC`/`_Rootmotion` 끝, Idle·Guard 등), **루트 회전·높이는 포즈에 굽고 XZ 만 루트 모션**(안 그러면 몸 비틀기가 루트 회전으로 빠져 사라짐). 확장자가 대문자 `.FBX` 라 경로 비교는 대소문자 무시.
- 타이밍 규칙: windup = (손 속도 최고점 − 0.06) / 0.8, 총 길이 = 클립 길이 / 0.8, active 0.1. chainAt = 다음 클립 첫 자세와 가장 비슷한 시각 / 0.8.

### 전투 (`13.Combat/`, 세키로식)
- 규칙: 체력 100 / 체간 100. 막기 +10, 튕겨내기(막기 누른 지 0.2 s 안) 성공 시 상대 +25·자신 +5. 마지막 체간 피해 3 s 뒤 초당 20 회복(체력 낮으면 최대 50 % 감소). 체간이 다 차면 3 s 무방비 → 맞으면 결정타 즉사. 회피 무적 0.3 s. 위험 공격(찌르기·베어 넘기기)은 막아도 피해 — 찌르기는 옆 회피, 베어 넘기기는 점프. 피격 경직 0.35 s(강공격 0.6~0.7).
- `SwordCombatant`(공용 상태기계 Idle/Attack/Block/Dodge/Jump/HitStun/Broken/Dead): 판정은 콜라이더 없이 거리·각도(active 시작 순간). 예비 동작 중 상대 추적은 windup 55 % 까지만(그 뒤 고정 → 옆 회피가 통함). 루트 모션은 Attack·HitStun·Dead·Broken·DodgeBack 에서만 XZ 적용, 상대와 0.9 m 이내로 못 파고듦. 이동 대상은 부모의 `ICharacterMover.MotionRoot`(OnFootRoot 면 CharacterController.Move). 콤보는 기본(4타)/B(5타) 자동 섞기(같은 콤보 최대 2번 연속).
- 공격 값(windup / active / recovery s, range m / arc °): 내려베기 0.24/0.1/0.685, 1.25/60 · 가로베기 0.40/0.1/0.7125, 1.25/60 · 찔러베기 0.26/0.1/0.7275, 1.3/45(chain 0.4625) · 마무리 베기 0.4875/·/0.85, 1.4/70, 피해 24·체간 32 · 찌르기(위험) 0.425/0.1/0.70, 1.35/25 · 베어 넘기기(위험) 1.2625/0.1/0.575, 1.3/100 · 강베기 0.2375/·/0.975, 2.0, 30·45 · 강베기 2 0.4625/·/0.85, 1.5 · 달려 베기 0.3875/·/0.7375, 1.8, 22·28 · 회피 베기 0.275/·/0.85, 1.4, 20·26 · B 콤보 비껌/돌려/올려/훑어/내리꽂기 windup 0.30/0.30/0.275/0.30/0.325, 피해 14/14/16/16/24. chainAt 기본 콤보 0.375 → 0.60 → 0.4625. range = 칼끝 도달(해신 측정) + 상대 반경 0.35.
- 조작(`PlayerCombatController`): 좌클릭 짧게 = 콤보(뗄 때 나감), **좌클릭 0.35 s 꾹 = 강베기**(후딜 중 다시 꾹 = 강베기 2), Shift 달리며 좌클릭 = 달려 베기, 회피 직후 좌클릭 = 회피 베기, 우클릭 = 튕겨내기·누른 채 막기, Space = 이동키와 회피 / 없으면 점프. 칼 자동 뽑기(살아 있는 적 6 m 안, 또는 클릭)·자동 넣기(5 s 무입력·비전투). E·F·Q 는 비어 있음(메인 씬 E = 승·하차). 테스트 키: T 부활, 1·2·3 옷·신발·머리, R 위치 초기화, Esc 커서.
- `EnemySwordAI`: 1.7 m 까지 접근 → 1.6~2.6 s 뒤 [콤보 60 % | 찌르기 20 % | 베어 넘기기 20 %], B 콤보 0.4, 강베기 0.12(+강베기 2 0.5), 플레이어 공격에 막기 35 %·튕겨내기 15 %, 위험 공격엔 50 % 회피. 이동 속도 2.69. transform 을 직접 옮김(중력·지형 없음). 현재 매우 공격적.
- `CombatHUD`(OnGUI 임시), `CombatAutoTest`(MCP 검증용 자동 조작 — PlayerCombatController 를 끄고 써야 함).

### 표정
- `EyeBlink`: 2.5~5.5 s 무작위, 15 % 두 번, 감기 0.07 + 유지 0.03 + 뜨기 0.12 s. 반쯤 감김 보정 키 `_mid` 가중치 4w(1−w). `baseClosed` = 기본 감김.
- `FacialExpression`: 프리셋 Neutral/Smile/SmileLeft/SmileRight/Frown/Pucker/Press/MouthOpen/OpenSmile/Surprised/Custom, 말하기(Perlin 입 벌림). 캐릭터 왼쪽 = +x 키.

### 승·하차 (`08.Character/`, SampleScene)
- `BoardingSystem`(`Boarding`): 탑승 중 배가 LandingPoint 의 dockRange 안 & **E 3초** → 하차: `OnFootRoot`(자동 생성, CharacterController r 0.32 h 1.75 + `OnFootController`) 를 판자 위에 세우고 외형을 그 아래로 옮긴 **다음** 켬, `PlayerCharacter.SetOnFoot()`, CannonControl·CameraFollow 끔, **MoveControl 은 끄지 않고 `SetDocked(true)`**, 카메라 부모·로컬 자세 저장. 캐릭터가 배 중심 25 m 안 & E 3초 → 승선(카메라 로컬 자세 복구, SetDocked(false)).
- `PlayerCharacter`: standOffset 0(피벗 = 발), `onFootOnly`(= PlayerCombatController) 는 배 위에서 끄고 칼을 즉시 넣음.
- `OnFootController`: 걷기 2.69 / 달리기 4.06 / 가속 8, 중력, `PlayerOrbitCamera`(거리 3.2, 높이 1.1, 감도 5, pitch −15~60, 마우스만 움직이면 돎, 휠 줌, SphereCast). Animator 가 없으면 Update 에서 다시 찾음. `walkThroughLayers`(StepSurface)는 CharacterController.excludeLayers 로 통과.
- `FootIK`(Haesin_Player, 컴포넌트 순서 HwandoGripIK 보다 위): OnFoot 에서만(사망·점프·공중 제외). 발목·발끝 아래로 구(r 0.04)를 쏴 높은 쪽 바닥 높이 − 피벗 높이만큼 발을 옮기고, 낮은 발 쪽만큼 골반(bodyPosition)을 옮김. 계단 `BaseGroup/CaveStair` = MeshCollider + StepSurface 레이어 — 몸은 디딤판을 통과해 밑 터레인 경사를 걷고 발만 디딤판에 놓임. 측정(오르기): 발바닥–디딤판 최저 −3.4 cm(1프레임), 발가락 뼈가 디딤판 아래로 들어간 프레임 0.6 %(최저 −5.8 cm). 칼 든 채 계단에서 왼손 그립 유지 확인.

## 6. 기억해야 할 시행착오 (증상 → 원인 → 해결)

### 배·물·날씨
| 증상 | 원인 | 해결 |
|---|---|---|
| 충돌해도 배가 안 밀림 | transform 직접 이동 → Rigidbody 속도 0 | 힘 기반 이동, 부력도 힘으로 |
| 배가 180° 돌면 좌/우현 포격이 뒤집힘 | 카메라 `rotation.y`(쿼터니언 성분)로 판단 | 선체 right 축·조준 방향 내적 |
| 하차 중 배가 떠내려가고 노를 계속 저음 | MoveControl 을 끄면 물 저항 계산도 멈춤(Rigidbody 저항 0), OarAnimator 가 멈춘 currentSpeed 를 읽음, PlayerInput(Send Messages)은 꺼진 컴포넌트에도 OnMove 를 보냄 | 끄지 않고 `SetDocked` |
| BowWave 데칼 타입이 안 바뀜 | `WaterDeformer` deprecated | `WaterDecal` + `Water Decal Sample` 셰이더, 키워드로 분기 |
| 함대가 커지면 항적이 사라짐 | 워터 데칼 96개/씬 상한 | 가까운 8척만 켬 |
| 충돌 파문 세기 0 | 충돌 후 상대속도 0 | 6프레임 전 속도 |
| 비가 한쪽으로 통째로 날아감 | RainMaker 가 WindZone 을 무작위로 바꿈 | EnableWind 끔, 고정 기울기 |
| 비가 아예 안 나옴 | velocityOverLifetime x/y/z 커브 모드 불일치 | 같은 모드로 대입 |
| 깃발이 폴대에 감김 | Cloth worldAccelerationScale | 0.3 / damping 0.1 |
| 번개가 안 그려짐 | Instantiate 직후 Start 미실행 | 한 프레임 뒤 `Trigger()` |
| 동굴 안에서 번개처럼 번쩍임 | 섬광 라이트가 그림자 없는 방향광 | 카메라 위 가림 비율만큼 섬광을 줄임 |
| 폭풍우 때 파도가 선착장 위로 올라옴 | 먼바다 너울 ±20 m(바람 140) | `WaterShoreMask` 로 육지 근처 파도 감쇠 |
| 구름이 안 늘어남 | CloudLayer opacity 는 전체 페이드 | 채널 R/G/B/A 순차 |
| 스폰이 코앞처럼 느껴짐 | 거리 균등 분포 | `u^(1/bias)` |
| 베이스(섬) 바로 옆에 적 함대가 뜨고 좌초 | 안전지대 반경 200 < 섬 끝(255 m), 스포너가 함대 중심만 검사(흩어진 배는 안쪽에 생길 수 있음) | 반경 350, 배마다 검사 |
| 플레이 중 스크립트 수정 후 RainMaker NRE 매 프레임 | 도메인 리로드로 비직렬화 필드 null, Start 재실행 안 됨 | `BaseRainScript.Update` 앞 null 가드(`[HaeSin 수정]`) + 플레이 중 저장 금지 |

### 피해·불·연출
| 증상 | 원인 | 해결 |
|---|---|---|
| 불 파티클이 엉뚱한 곳에서 뿜음 | World 시뮬 파티클 런타임 생성 | 미리 넣어 두고 켜기만 |
| Burn 머티리얼 뒷면 컬링 | `new Material()` 은 HDRP 검증 미통과 | `_CullMode=0`, `_DoubleSidedEnable=1` |
| 피격 시 배 밑 캡슐이 드러남 | 투명 머티리얼까지 Burn 으로 교체 | 투명 머티리얼 제외 |
| 침몰 중 연기가 옆으로 뻗음 | DamageFX velocityOverLifetime 이 Local +z | space = World, y 로 옮김 |
| 먼 적함 불이 그대로 보임 | 불 머티리얼 `_EnableFogOnTransparent = 0` + 큰 빌보드 | 안개 켬 + 320 m 밖 렌더러 끔 |
| 먼 바다에서 동굴이 갑자기 나타남 | far clip 1000, Fog maximumHeight 96.7 | far 4000, maximumHeight 350 |

### 조명·동굴·횃불
| 증상 | 원인 | 해결 |
|---|---|---|
| 동굴 안이 밝음 | 그림자 거리 150 m 기본값, 하늘 앰비언트, 자동 노출, 하늘색 안개, 바다 하늘 반사 | HDShadowSettings 700, APV, 로컬 볼륨, 리플렉션 프로브 |
| 동굴 볼륨 프로파일이 비워짐 | `Add<T>()` 만 하면 서브에셋으로 저장 안 됨 | `AddObjectToAsset` + `SaveAssets` |
| 바위를 옮긴 뒤 벽이 회색으로 밝음 | 옛 APV 데이터 | 재베이크(최종 단계) |
| 횃불 근처가 흰 공처럼 날아감 | Fire 프리팹 `glow_particles`(가산 흰색) | 비활성 |
| 불꽃이 너무 높이 치솟음 | 파티클 속도는 월드 단위 | scalingMode Hierarchy |
| 횃불 밑에 검은 원 | 기둥 그림자가 확대됨 | 기둥 그림자 끔 |
| Point Light range 를 늘려도 빛이 안 퍼짐 | HDRP 는 역제곱 감쇠 — range 는 상한일 뿐, 도달 거리는 세기(lm)·노출(EV) | 세기를 올리고(반경 3배 = 세기 9배) 노출 하한 조정 |
| 횃불 빛이 반원으로 잘림 | 불꽃 파티클 렌더러 Cast Shadows On | 파티클 그림자 끔 |

### UI·메뉴
| 증상 | 원인 | 해결 |
|---|---|---|
| Image 색이 뿌옇게/다르게 나옴 | HDRP Linear 에서 `Image.color` 정점색 문제(원인 확인 필요) | 색을 스프라이트에 구움(`UIKit.SolidSprite`) |
| 메뉴 버튼이 안 눌림 | 씬에 EventSystem 없음 | UIKit 이 생성 |
| 뷰어에 바다가 가득 | HDRP 물은 farClip 무관 | 컬링 레이어 + 프레임 설정으로 물 등 끔 |
| 뷰어 배가 하얗게 탐 | timeScale 0 이라 자동 노출이 적응 못 함 | 전용 볼륨 고정 노출 |
| 뷰어 깃발이 수백 m | Cloth BakeMesh 결과는 월드 크기 | 노드 월드 스케일 1 |
| 뷰어 캐릭터 머리·손이 옷 속에 묻힘 | `BakeMesh(useScale: true)` 는 렌더러 스케일을 뺀 정점(Unity 6 측정) | `BakeMesh(false)` |
| 뷰어 캐릭터가 거의 검게 나옴 | 햇빛(날씨에 따라 변함)만 받음 | 전용 스포트 3개 |
| HDRP 카메라 RT 알파가 항상 1 | (측정) | 배경을 RT 안 쿼드로 그림, Unlit 밝기 ×1.7 |
| 바다 소리가 처음에 크다가 줄어듦 / 스크립트 기본값을 바꿔도 안 바뀜 | 시작 볼륨 최대 + 씬 직렬화 값이 우선 | 0 에서 페이드인, 씬 값 수정 |

### 캐릭터·전투·승하차
| 증상 | 원인 | 해결 |
|---|---|---|
| 칼날이 손에서 뒤로 뻗음 | RightHand 로컬 +Z 가 몸 뒤쪽 | 소켓 회전으로 맞춤 |
| Fab 클립을 씌우면 몸 비틀기가 사라짐 | 루트 회전을 포즈에 안 구움 + OnAnimatorMove 는 위치만 적용 | lockRootRotation·높이 굽기, XZ 만 루트 모션 |
| 루트 모션 사망 때 두 캐릭터가 겹침 | 거리 제한을 Attack 에만 | 모든 루트 모션 상태에 적용 |
| 콤보 3타 입력 무시 | 다음 타 입력을 늦게 받음 | 입력 버퍼(windup × 0.5 부터) |
| 치마 앞자락이 다리 뒤에 낌 | 빠른 동작에서 다리 콜라이더가 자락을 지나쳐 몸 쪽에서 밀어냄, maxDistance 가 전 구간 같아 뿌리 따라가기가 안 먹음 | maxDistance 깊이 곡선 + clearance + untrap |
| 대기에서 다리가 치마 앞으로 나옴 | 치마 뿌리 본은 골반만 따라감 | `SkirtLegFollow` |
| 스프링 치마 값을 바꿔도 날림이 그대로 | 날림의 주원인은 관성이 아니라 다리 콜라이더 | Magica 로 교체 |
| 하차 후 모델 Animator 에 Speed 가 안 감 | OnFootRoot 를 켠 뒤 외형을 옮김(OnEnable 에서 Animator 탐색) | 옮긴 뒤 켜기 + Update 재탐색 |
| 계단에서 발이 디딤판을 뚫음 | 몸(CharacterController)은 디딤판 밑 터레인 경사를 걷고 발 IK 없음 | `FootIK` + 디딤판 StepSurface 레이어(몸은 통과) |
| 다시 탔을 때 카메라가 기울어 물속을 비춤 | PlayerOrbitCamera 가 CameraPivot 자식인 Main Camera 의 월드 자세를 덮어씀 | 하차 때 로컬 자세 저장, 승선 때 복구 |
| 계단에서 발소리가 1~2 초씩 빠짐 | 레이로 잰 발 높이는 발 아래 디딤판이 바뀌어 '들림'이 안 잡힘 | FootIK 의 애니메이션(IK 전) 발 높이로 판정 |
| 표정 키를 켜면 얼굴 음영이 전부 바뀜 | Blend Shape Normals = Calculate | None + `BlinkBlendShapeNormals` |
| 블렌드셰이프 법선 변화가 대부분 0 | `Vector3.normalized` 는 길이 1e-5 미만이면 0 | 직접 길이로 나눔 |
| 편집 모드에서 SetBlendShapeWeight 가 캡처에 안 나옴 | 같은 호출 안 렌더는 스키닝 미갱신 | Play 에서, 다음 호출에 렌더 |

## 7. Unity MCP 로 작업할 때

- `execute_code` 는 CodeDom(C# 6): `UnityEngine.Object.` 로 한정, 문자열 보간·`out var` 불가. `LightUnit` 은 `UnityEngine.Rendering.LightUnit`, 세기는 `HDAdditionalLightData.SetIntensity(v, LightUnit.X)`. Volume 파라미터 값은 리플렉션으로 `value` 읽기. `HDMaterial` 접근 불가 → `SetFloat` + `EnableKeyword`.
- **같은 코드가 여러 번 실행될 수 있음** → 만들기 코드는 이름으로 이미 있는지 확인(멱등).
- 한글을 보내면 일부 음절이 깨지는 일이 있었음(원인 확인 필요) → 저장 후 U+FFFD·문구를 확인.
- 스크립트 폴더 전체를 한 번에 읽으면 출력 한도 초과 — 나눠 읽기.
- 캡처: `manage_camera` 위치 지정 캡처엔 HDRP 물이 안 그려짐. Play 캡처는 Main Camera 를 옮긴 뒤 다음 호출에서(모션 블러), PlayerOrbitCamera 는 꺼 둘 것. 편집 모드 파티클은 `ps.Simulate(...)` 로 강제 시뮬. 편집 모드 `SampleAnimation` 직후 스키닝은 한 번 늦음.
- 에디터 자세 샘플링은 `AnimationMode.Start/SampleAnimationClip/Stop`(씬 dirty 안 됨) 또는 `AnimationClip.SampleAnimation`(PlayableGraph 는 휴머노이드 자세가 깨짐).
- 일시정지 상태로 Play 를 끄면 다음 Play 가 멈춘 채 시작 → 끄기 전 `isPaused = false`.
- 자동 시험(`CombatAutoTest`)을 쓸 땐 PlayerCombatController 를 끈다(매 프레임 막기 입력을 덮어씀).

## 8. 확정된 설계 결정

- 진영 3개, 적 판정은 `ShipFaction` 한 곳. 포탄은 `ownerFaction` 을 반드시 가짐.
- 배 이동은 전부 힘 기반, 축 분담(높이·기울기 = ShipBuoyancy, Y 회전·XZ = 이동 스크립트).
- `EnemyShipBase` / `NavyShipBase` / `MoveControl` 은 공용 베이스로 합치지 않음.
- 씬의 원본 배 오브젝트가 스포너 템플릿(프리팹 참조 아님).
- 날씨 프리셋은 `WeatherSystem` 인스펙터 인라인 리스트.
- 현상금은 플레이어의 상선 약탈에만, 관군 함대 수 상한으로 이어짐.
- 근접전은 세키로식(튕겨내기 + 체간 + 결정타), **배에서 내린 상태에서만**. 공중제비·구르기류 동작은 넣지 않음(치마 속 노출). 결정타 쌍 애니메이션은 쓰지 않음.
- 두 번째 콤보는 별도 키 없이 좌클릭 콤보에 자동으로 섞음. 강베기는 좌클릭 꾹. 칼은 자동으로 뽑고 넣음.
- 걷는 캐릭터 카메라는 배 카메라와 같은 조작(마우스만 움직이면 돎).
- 하차 중 배는 정박(`SetDocked`) — 조종 스크립트를 끄지 않음.

## 9. 미해결 과제 / 확인 필요

### 프로젝트
- [ ] Build Settings 에 씬 없음.
- [ ] `CameraFollow.cs`·`CannonControl.cs` 옛 주석이 깨져 있음(U+FFFD) — 다시 써야 함.
- [ ] 스크립트 BOM 유무 혼재 — 통일 여부 결정. URP 패키지 제거 여부 결정.
- [ ] `WaterRipple`/`ShipRipple.prefab` 은 아직 deprecated `WaterDeformer`.
- [ ] `WaterHeightUtil.Sample` 캐시 없음(배당 FixedUpdate 4회) — 배가 많아지면 성능 확인. `SekiScoutAI.SendAlert`·`NavyPanokAI.CallNearbyAllies` 는 전수 탐색.
- [ ] `WaterHeightUtil.Sample` 은 `startPositionWS` 가 (0, 0, 0) — 원점에서 먼 곳에서도 8회 반복으로 수렴하는지 확인 필요.
- [ ] `Haesin/ShipBurn` 셰이더 파일 위치, `.gitignore` 확인 필요. `CamelotVFX_ Fire&Smoke` 출처 확인 필요. SampleScene 에 해석 안 되는 GUID 4개(무엇인지 확인 필요).
- [ ] 콘솔 `Leak Detected : Persistent allocates 127 individual allocations` 경고 1건(Play 여러 번 후) — 출처 확인 필요.

### 월드
- [ ] 새 섬을 만들면 `WaterShoreMask` 범위(현재 중심 (0,0) 1000 m)를 넓혀 다시 구워야 함, 안전지대·스폰 규칙도 섬별로 정해야 함.
- [ ] 터레인 브러시 다듬기, 선착장 주변 정리, 나무·풀 없음. 제대로 된 횃불 메시 없음, 횃불 불티가 많음(모닥불용 파티클).
- [ ] APV·리플렉션 프로브 최종 베이크(바위 위치 확정 후).

### 배·전투(해상)
- [ ] 리스폰 화면 전환(`PlayerRespawn.fadeDelay` 자리), 리스폰 시 자원 손실.
- [ ] 안전지대 경계에서 AI 움직임, 관군 소집 지점이 경계 안일 때 — 플레이 확인 필요.
- [ ] 적 배 소리(ShipAudio 미부착), 침몰·착수 효과음 없음. 볼륨 밸런스 플레이 확인.
- [ ] 선원 = 임시 큐브(모델·애니메이션·역할 없음), 일부 위치가 난간·벽에 걸칠 수 있음(안택선 양현).
- [ ] 화살·선원 체력 강화는 값만 저장. 자원 획득 경로·저장/불러오기 없음. 깃발 이미지 실제 파일로 미시험. 인벤토리 아이템 시스템 없음.
- [ ] 예측 사격 난이도 조정, 미니맵 밖 적 방향 표시.
- [ ] 함선 메뉴 강화/영입 버튼은 단색(공용 아트 미적용).

### 캐릭터·전투(육상)
- [ ] 메인 씬 하차 후 칼싸움: CombatHUD·칼싸움 적(EnemySwordAI) 없음, EnemySwordAI 는 지형·중력 미지원.
- [ ] CharacterTest 와 메인 씬 캐릭터를 공유 프리팹으로 만들지 결정.
- [ ] 옆·앞 회피·점프는 임시 동작(팩에 공중제비 아닌 회피 없음). 락온 UI·카메라 없음. 적 AI 난이도 조정, 여러 적.
- [ ] 이동 블렌드 트리를 Fab 클립으로 바꿀지. 걷기 2.69 m/s 가 빠른지.
- [ ] 배가 빠르게 움직일 때 치마·포니테일 날림, 옷 전환(1번 키) 시 소매 MeshCloth 가 깨지는지 — 확인 필요.
- [ ] 적 예비 동작이 짧음(내려베기 0.24 s) — 사람이 튕겨낼 수 있는지 손맛 확인.
- [ ] 효과음·인벤토리 조명 밝기는 수치로만 고름 — 사용자 확인.
- [ ] 발 IK: 계단 내려가기·전투 중 동작은 수치 확인 안 함 — 사용자 플레이 확인 필요. CharacterTest 의 캐릭터에는 FootIK 없음.
- [ ] 실제 키보드·마우스(E 3초, WASD, 좌클릭 꾹, 메뉴 Y/I/M/ESC, 드래그) — 대부분 메서드 호출로만 검증, 사용자 플레이 확인 필요.
