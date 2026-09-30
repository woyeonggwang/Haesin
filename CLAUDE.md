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

- HDRP 애셋 GPU Resident Drawer: 꺼짐(mode 0, 2026-09-26 확인). Static Batching 켜짐.
- Asset Store 다운로드 캐시: `D:\UnityAssetCache`(사용자가 변경, 2026-09-26 — C: 가 가득 찼었음. E: 는 이동형 SSD 라 안 씀). Asset Pipeline Refresh Import Mode = OutOfProcessPerQueue.
- git: 브랜치 `main`. Git LFS 3.7.0 은 설치돼 있으나 저장소는 안 씀(`.gitattributes` 없음). 큰 에셋은 `.gitignore` 로 로컬에만 둠 — TextureHaven·ObjectiveEnvironment_Assets·`SampleScene/*.bytes`·**`Models/Gyeongbokgung/`(약 4만 파일·27 GB, 2026-09-30 추가)**·확인용 캡처(`Assets/Screenshots/` — 이미 커밋된 것은 계속 추적, `/Captures/`, `/Claude outputs/`)·파이썬 캐시(`__pycache__`, `*.pyc`). 그래서 다른 곳에서 받으면 궁 프리팹이 없음(씬 `GyeongBok` 아래 Missing) — 궁 폴더는 따로 백업해야 함. 궁 작업 코드(`Editor/Palace*.cs`, `98.Blender/`)는 무시하지 않음.
- `Assets/Settings` 의 URP 설정 4개(`ForwardRenderer`, `Global Volume Profile`, `URP-Performant-Renderer`, `UniversalRP-HighQuality`)는 같은 GUID 로 궁 폴더(`GeunjeongjeonComplex/Settings`, `GyeonghoeruComplex/Gyeonghoeru/Settings`) 안에 있어 git 에서는 삭제로 보임. Quality·Graphics 설정은 이 넷을 참조 안 함(2026-09-30 확인).

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
  04.Faction/             ShipFaction (+ enum Faction), ReputationSystem, FactionTerritory
  05.Damage/              ShipHealth, ShipBurnVisual, PlayerHealthUI, DamageFXDistanceCull, BallisticAim
  06.Merchant/            MerchantAI
  07.Environment/         TorchFlicker, RespawnPoint, BaseSafeZone, HarborMooring
  08.Character/           PlayerCharacter, LandingPoint, OnFootController, BoardingSystem, FootIK, PlayerOrbitCamera(+ICharacterMover),
                          CharacterOutfit, SpringBoneSystem, SkirtLegFollow, EyeBlink, FacialExpression,
                          CharacterTestController·TestCircleWalker(CharacterTest 전용)
  09.Crew/                ShipCrew, CrewMember
  10.UI/                  MapSystem(+MapSystem.Atlas.cs), UIKit, GameMenuPause, ShowcaseViewer, ShipMenu, InventoryMenu, MenuArt, ResourceBar, UITooltip, ReputationGaugeUI
  11.Audio/               ShipAudio, SeaAmbientAudio, OneShotAudio, BulletAudio, SwordAudio, FootstepAudio
  12.Economy/             PlayerResources, ShipUpgrades
  13.Combat/              AttackData, CombatStats, SwordCombatant, PlayerCombatController, EnemySwordAI,
                          CombatHUD, WeaponHolder, HwandoGripIK, CombatAutoTest(테스트 전용)
  Editor/                 ARPGFabImportPostprocessor, BlinkBlendShapeNormals, IslandTerrainGenerator,
                          PalaceBakeTool, PalaceBakedImportPostprocessor, PalaceLodImport, PalaceGJImport,
                          PalaceOptimizeTool, PalaceRoofLodTool, PalaceShellTool(+ HaesinTriVis.shader — 보이는 삼각형 표시),
                          PalaceMeshCompact, PalaceBlenderRoundTrip,
                          PalaceOrigExport(데모 씬 원본을 부분별 OBJ + tex_map.txt 로 내보냄 — 에셋·씬은 안 바꿈),
                          PalaceBinExport(데모 씬을 고유 메시 + 배치 목록 이진 파일로 내보냄 — 에셋·씬은 안 바꿈),
                          PalaceCardTool(단순화판에 남은 문·문 장식·쇠문·홍살을 판으로 — 대상 내보내기·에셋 만들기·프리팹 교체·확인)(경복궁 §4)
                          PalaceLodBinImport(밖에서 만든 메시 묶음 → 메시 에셋·부분별 LODGroup 프리팹·여는 문짝 판·씬 배치 — 흥례문 단순화판)(경복궁 §4)
  98.Blender/             궁궐 단순화·판 굽기에 쓴 외부 처리 스크립트(파이썬·C, 사용자 지정 폴더 — Unity 에서 컴파일 안 됨, C 소스는 `.c.txt`): Simplify/(원형 유지 감면), Cards/(판 굽기), Bake/(아틀라스·지붕 껍데기 굽기), LOD/(강녕전·사정전 LOD 보조), Heungnyemun/(흥례문 보임 검사·LOD·판)
02.Resources/00.Component/
  NewShip/               anteck, panok, seki 프리팹
  02.Water/              ShipRipple.prefab
  ShipCloth/             깃발(Cloth) 포함 배 모델 프리팹
  06.Character/Haesin/   캐릭터 FBX·머티리얼·텍스처·애니메이션(§5)
  07.Animation/          ARPGSamurai(Unity 판 rig_*), ARPG_Samurai_Fab(Fab 판 FBX 339개)
02.Resources/01.Asset/   외부 에셋(종류별)
  Animations/  Quaternius_UAL(CC0), Kevin Iglesias(Human Melee Animations FREE)
  Models/      JoseonTongsinsaseon, SwampBridge4k, Free_Rocks, Gyeongbokgung(단지 12개 + _Shared + _Baked, §4)
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
- `enum Faction { Pirate(플레이어), Navy(관군 판옥선 + 상선), Japanese(왜군 세키부네/안택선) }`. **적 판정은 `ShipFaction.IsHostileTo` 한 곳**: 관군 ↔ 왜군은 늘 적, 플레이어 ↔ 두 진영은 우호도(`ReputationSystem.PlayerHostileWith`)로 정함. 표적 판정은 `IsTargetableBy(공격자, 대상)` = IsTargetable + 다른 진영 영해(FactionTerritory outer) 안의 배는 표적 아님.
- **우호도 `ReputationSystem`**(씬 루트 `ReputationSystem`, 예전 BountySystem 대체 — 스크립트 삭제됨): 게이지 하나 standing −100(관군 쪽) … 0 중립 … +100(일본 쪽). 관군 배(상선 포함)를 맞히면 +2, 일본 배를 맞히면 −2, 격침 한 방이면 군선 ±15 / 상선 ±10 추가(ShipHealth.TakeDamage 에서 attacker = Pirate 일 때). standing ≥ +25 → 관군 적대, ≤ −25 → 일본군 적대, 그 사이 중립(둘 다 공격 안 함). 초당 0.1 씩 0 으로 돌아옴. 시작 0. (사용자 지정: 한쪽이 오르면 다른 쪽이 내려가는 게이지 하나)
- `ReputationGaugeUI`: 체력바 바로 아래(bottom 22, 폭 560) 게이지 — 왼쪽 파랑 = 관군 쪽 끝(일본군 적대 구간), 가운데 회색 중립, 오른쪽 빨강 = 일본 쪽 끝(관군 적대 구간), 흰 표시 = 현재 값, 아래 글자로 상태.
- **영역 `FactionTerritory`**(각 섬 `Territory` 자식, 생성기가 만듦): inner(= 1.35 × 섬 반폭 + 60) 안에는 돌아다니는 배 스폰·진입 없음(정박선만), outer(= inner + 700) 안에는 그 섬 진영 배만 스폰(육지 쪽으로 갈수록 확률 ↓, smoothstep), 다른 진영 배는 들어오지 않고(순찰·항해 목적지를 밀어냄) 그 안의 배를 표적으로 삼지 않음(안전지대). 조선 섬 = Navy, 일본섬 = Japanese.
- 활성 배는 `ShipFaction.All` 에 자기 등록. `FindNearestHostile(self, range)`, `Of(GameObject)`(부모까지 탐색 — 포탄은 자식 콜라이더에 맞음), `IsTargetable()`(완파·침몰 중·안전지대 안이면 표적 아님).
- 태그(Player/Enemy) 판정은 `ShipFaction` 없는 대상용 폴백으로만 남음(`Bullet.ShouldExplode`, `EnemyShipBase.Retarget`).

### 플레이어 배 (`panok`, 태그 Player)
- `MoveControl`: 힘 기반(AddForce/AddTorque). 축 분담 — 물리 = XZ 위치 + Y 회전, `ShipBuoyancy` = Y 높이 + 피치/롤. Rigidbody 저항 0(**물 저항은 MoveControl.FixedUpdate 가 직접 계산** — 끄면 배가 떠내려감), Interpolate, ContinuousDynamic. 코드 기본 mass 1000·maxSpeed 10·boost ×1.6, 방향타 0.4~4 m/s. 씬 템플릿의 속도·가속은 코드 기본의 1.5 배.
  - **정박**: `SetDocked(bool)` — 입력 무시 + 정박 위치·방향으로 끌어당김(anchorStiffness 0.6 / anchorDamping 1.5 / anchorYawStiffness 0.8). 하차 중 사용. `Halt()` 는 속도·currentSpeed·boost 모두 0.
- `CannonControl`: 우클릭 줌(FOV 60→28) + 좌클릭 발사, 쿨다운 4 s. 좌/우현 = 선체 right 축과 조준 방향 내적(deadzone 0.08). 포탄에 진영·강화 배율을 찍음.
- `CameraFollow`: `panok/CameraPivot` 에 부착, 마우스 오빗(pitch −30~60), 우클릭 시 레이캐스트로 조준점. 침몰 중에만 카메라 y ≥ 해수면 +3 m. **Main Camera 는 CameraPivot 의 자식**(로컬 (0, 0.3, −0.471), 회전 (18.3, 0, 0)), far clip 4000, near 0.3, 안티에일리어싱 = TAA(품질 Medium — 먼 기와 반짝임 때문에 켬, 예전 None).
- `Bullet`: 플레이어 포탄만 발사 직후 위로 730 힘 보정. 같은 진영·침몰 중인 배엔 안 터짐. y < −20 이면 파괴. 바위·지형에 맞으면 터지지 않고 굴러감.
- `BallisticAim`: 적 포격 예측 사격(비행시간 × 표적 수평 속도 3회 반복, leadFactor 1, maxLeadTime 6 s). 탄도 공식 `v² = g·d² / (2(d·tanθ − Δh)cos²θ)`, 실패 시 직사.
- `OarAnimator`: `OarGroup/Left|Right` 자식 노의 로컬 X 만 회전(우현 노는 Y=180). 멈춰 있어도 idle 0.25 회/s 로 저음, **정박 중엔 1.5 s 에 걸쳐 멈추고 앞 자세(좌 −25° / 우 25°)**, 서지 없음.

### AI
- `EnemyShipBase`(왜군)와 `NavyShipBase`(관군·상선)는 **의도적으로 분리된 복제본**(힘 기반 항해 + 순찰 + 0.5 s 재표적). 순찰 = 전방 편향 0.45 + 앵커 드리프트 1.5 m/s.
- `EnemyAI`(안택선): Patrol/Chase/Broadside. 씬 값 det 100 / lose 130 / atk 70 / brk 85(`OnValidate` 가 lose ≥ det×1.2, brk ≥ atk×1.2 강제). 추격 리드 ≤ 2.5 s.
- `SekiScoutAI`(세키부네, 비무장): 발견 시 350 m 안 EnemyAI 에 25 s 경보, 55 m 거리 유지.
- `NavyPanokAI`: EnemyAI 뼈대 + 접현 회피 28 m + 300 m 내 아군 소집. 추격 90 m 이상이면 부스트(×1.6).
- 포격(`EnemyCannonControl`/`NavyCannonControl`): Broadside + 사거리(90/95) + 현측 55° 안일 때 일제사. 관군 최대 8문, 재장전 8 s.
- `MerchantAI`(Navy, 비무장): Sail/Follow/Flee. 격침(약탈)은 우호도로 처리.
- `EnemyFleetSpawner.PickFleetCenter(spread, 진영)`: BaseSafeZone + FactionTerritory.SpawnWeight 로 12번 뽑아 실패하면 그번엔 스폰 안 함. 배 한 척씩도 PushOut.
- `EnemyFleetSpawner`: 씬의 원본 배(anteck/seki/panok_navy/merchant)가 템플릿 — Start 에서 끄고 복제 스폰. 플레이어 기준 250~500 m, 거리 분포 `u^(1/1.8)`, 900 m 밖 정리, 8 s 간격.
- **베이스 안전지대** `BaseSafeZone`(PlayerRespawnPoint 에 부착, **반경 350** — 섬·동굴의 가장 먼 끝이 리스폰 지점에서 약 255 m): 스폰 위치에서 제외, AI 목적지를 경계 밖으로 밀어냄, 안에 있는 배는 표적 아님. 스포너는 함대 중심을 반경 + 흩어짐 + `safeZoneMargin`(60) 밖에서 뽑고, 배 한 척씩도 경계 + 60 밖으로 밀어냄.

### 물 (HDRP Water)
- `WaterHeightUtil.Sample` = `ProjectPointOnWaterSurface` 래퍼(캐시 없음).
- `ShipBuoyancy`(order 100): 4점 샘플, 비-kinematic 이면 Physics 모드(스프링 20 / 감쇠 8 / 최대 30, 중력 끔).
- `ShipWakeFoam`(WaterFoamGenerator), `ShipBowWave`(WaterDecal + `Water Decal Sample` 셰이더 — **키워드** `_TYPE_BOW_WAVE` 등으로 분기).
- `ShipDecalLOD` + `WaterDecalLODManager`: HDRP 워터 데칼 **씬당 96개 상한** → 카메라 가까운 8척만 켬(150 m + 15 m 히스테리시스).
- `WaterRippleSpawner`(풀: 충돌 8 / 항적 10 / 물보라 8), `ShipImpactSensor`(충돌 6프레임 전 속도로 세기 계산).
- `WaterShoreMask`(Ocean): 육지 가까운 바다의 파도를 줄이는 `waterMask`(R 너울 / G agitation / B 리플 — CPU 시뮬레이션에도 반영)를 굽는다. 육지 = 위에서 쏜 레이가 처음 맞는 리지드바디 없는 콜라이더가 y −0.5 이상인 곳(동굴 지붕 밑 물도 포함). 중심 (−588.5, 1478), 7146 m(base + 새 섬 전부), 1024²(칸 약 7 m), 육지 옆 R 0.1 / G 0.2 / B 0.6 → 200 m 에 걸쳐 1. 결과 `02.Resources/00.Component/02.Water/WaterShoreMask.png`. **터레인·마을을 바꾸면 컴포넌트 컨텍스트 메뉴 "해안 마스크 굽기"로 다시 굽는다.** 폭풍우(바람 140) 측정: 먼바다 −21.6~+18.4 m, 선착장 주변 −1.3~+0.2 m, 동굴 안 −3.5~+1.6 m.

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
- `MapSystem`: M 전체 지도(해도 `Resources/MapAtlas/OceanChart.png`) + 미니맵(같은 해도를 `ZoomUV` 로 300 px 만 확대, 관군·일본 배를 모두 표시 — 적대면 크게, 중립이면 0.75 배).
- 함선 메뉴 `ShipMenu`(Y/ESC): 좌측 이미지 타일 5개(`Resources/ShipMenuArt/`), 가운데 전체 화면 3D 배(`ShowcaseViewer` fullScreen + 배경 쿼드), 우측 상세·강화·깃발 UI, `ResourceBar`.
- 인벤토리 `InventoryMenu`(I/ESC): 좌측 캐릭터 3D 뷰어(전용 조명 켬), 우측 탭 5개 + 빈 슬롯 20칸(아이템 시스템 없음).
- `ShowcaseViewer`: 레이어 Showcase 전용 카메라 + RT, 스테이지 (20000|20300, 600, 20000). 대상은 **MeshFilter/MeshRenderer 만 계층째 복사**, SkinnedMeshRenderer 는 `BakeMesh(mesh, false)` 후 노드 월드 스케일 1. 고정 노출 13.5, 물·구름·안개 끔. `stageLights`(키 30000 / 보조 10000 / 역광 15000 lux 스포트, 카메라 기준 고정). 드래그는 마우스 아래 최상위 UI 가 뷰어일 때만.
- `UIKit`: 색은 `SolidSprite(color)`(픽셀에 `.linear` 로 구움) + Image.color 흰색. EventSystem 없으면 생성(InputSystemUIInputModule).
- `MenuArt`(`Resources/MenuCommonArt/` 버튼 9-slice: Normal border 44 / ppu 배수 4, Selected 80 / 6), `ResourceBar`(아이콘 + 숫자), `UITooltip`.

## 4. 월드 (SampleScene)

### 씬 루트
`panok`(플레이어 배) · `anteck`/`seki`/`panok_navy`/`merchant`(스포너 템플릿) · `EnemyFleetSpawner` · `WeatherSystem`(+WeatherTestKeys, LightningStorm, SeaAmbientAudio) · `WaterDecalLOD` · `ReputationSystem`(+ReputationGaugeUI) · `WaterFX` · `Ocean` · `BaseGroup`(Base 터레인, Cave, Light, 선착장) · `Sky and Fog Global Volume` · `CaveInteriorVolume` · `CaveReflectionProbe` · `Adaptive Probe Volume` · `PlayerRespawnPoint` · `LandingPoint_Dock` · `Boarding` · `MapSystem` · `GameData` · `ShipMenu` · `InventoryMenu` · 비활성: `Sphere`(포탄 원본), `Explosion_v1`, `BuoyancyObjects`.

### 터레인 (`BaseGroup/Base`, `Assets/New Terrain.asset`)
- 500×500 m, 높이 스케일 600 m, 원점 (−250, −60, −250). 실제 높이 −60 ~ +19 m, 해수면 y 0. 나무·디테일 없음.
- 텍스처 8레이어(TextureHaven 4K, `01.Asset/TerrainLayers/`): 해저 coral_gravel(tile 23)+rock_ground_02(31), 모래 레이어 `Sand_aerial_sand`(**텍스처는 Poly Haven park_sand 2K, tile 4** — 원래 aerial_sand 는 항공사진이라 바퀴 자국 줄무늬가 보여 교체, 레이어 이름은 그대로), 풀 aerial_grass_rock(19)+forrest_ground_01(27), 바위 rock_ground(15)+rocks_ground_05(21), 절벽 aerial_rocks_01(21). 높이·경사 규칙(높이 < −2.5 해저, ~+3.5 모래, 경사 <22° 풀, 22~42° 바위, >42° 또는 >15 m 절벽) + Perlin 으로 자동 페인트, 구역마다 2종을 저주파 노이즈로 섞음.
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
- 선착장 `BaseGroup/swamp-bridge-4k-ready_2016`(x −142~−121, z −6.6~−0.3), 판자 윗면 y 4.745. 메시 7개에 MeshCollider. **텍스처 교체(2026-09-26)**: 머티리얼 9개(`SwampBridge4k/Materials`, 동굴 계단도 같은 DeckBoards 3개를 씀)의 BaseColorMap = `SwampBridge4k/Retex/*_Retex.jpg`(Poly Haven `wood_planks` CC0 를 원래 아틀라스의 판자 배치 위에 입힘, 원래 밝기 30 % 유지, 기둥은 결 90° 회전), BaseColor 흰색, Smoothness 0.18. 예전: 회색 아틀라스 × 갈색·붉은 틴트라 어두웠음.
- 하차 지점 `LandingPoint_Dock` (−125, 6.5, −3.4), dockRange 35 — 아래로 레이를 쏴 판자 위에 세움.
- 리스폰 `PlayerRespawnPoint` (−110, 0.3, −4) + `BaseSafeZone` 반경 350.

### 새 섬 (`Islands`, 생성기 `Editor/IslandTerrainGenerator.cs`, 메뉴 HaeSin/섬 터레인 생성)
- 섬마다 Terrain 1개. 위치 = `IslandPlan` 아래 계획 큐브(비활성)의 XZ 중심 — 큐브를 옮기고 메뉴를 다시 실행하면 그 자리에 같은 모양으로 다시 만듦(TerrainData 덮어씀, 시드 고정). 타일 = 육지 + 사방 200 m(해저가 가장자리에서 −45 m), 높이 범위 −60~+240, 헤이트맵 1025, 알파맵 1024, 레이어·머티리얼·설정은 Base 터레인과 같음. TerrainData = `02.Resources/00.Component/08.Environment/Islands/Island_*.asset`.
- 섬(사용자 배치, 계획 크기 / 모양은 둥근 사각이라 실제 육지는 약 80 %):
  - `Joseon_Large` 1000 (−2828, 4051): 남쪽 평야 약 8 m, 가운데 남쪽 궁터 10 m 평탄, 북쪽 산줄기(최고 약 150 m), 동남 구릉, 남쪽 만.
  - `Joseon_Mid` 800 (1185, 2728): 해안 평지 4~9 m + 서쪽 구릉(약 75 m, 아랫자락 4 m 계단), 남쪽 만. (용도는 사용자 미지정 — 임의로 해안 마을형)
  - `Joseon_Small_A` 600 (−1780, 176): 북서쪽 뾰족한 산 약 150 m + 서쪽 어깨 능선, 남동 중턱 계단식 산간 마을(5 m 단).
  - `Joseon_Small_B` 600 (−962, 1619): 두 봉우리(약 115 / 95 m) 사이 골짜기 마을(바닥 10~18 m, 4 m 단), 남쪽 만.
  - `Japan_Castle` **1100** (1751, −1195, 계획 큐브 `Plan_Japan_1100`): 800 은 실제 땅이 약 0.5 km² 라 좁다는 지적으로 키우고 만을 줄임(땅 약 1.0 km²). 성하 마을 평지 4~7 m + 가운데 성 언덕(혼마루 42 m 반경 55 / 니노마루 26 m 반경 105), 북서쪽(base 쪽) 작은 만.
- **섬 선착장**(생성기가 만듦, 섬마다 `Dock`): 섬 중심에서 `dockAngle` 방향 해안선을 찾아 8 m 안쪽을 발치로, 발치 주변을 1.8 m 로 평탄화 + 육지 쪽으로 경사 약 9°·폭 12 m 길을 둑에 깎음(만 안쪽 해안이 가팔라서). Base 선착장을 복제(바다 쪽 경사로 Ramp_Upper 제거, 로컬 +x = 바다 쪽), `LandingPoint_<섬>`(dockRange 35), `HarborMooring`(정박선: 조선 = panok_navy [+ 큰·중간섬 merchant], 일본 = anteck + seki; 첫째 = 선착장 왼쪽 12 m 나란히, 오른쪽은 플레이어 배 자리, 둘째 = 앞바다 50 m). 정박선 = 원본 복제 + AI·이동·포격·노 끔, kinematic, 부력 Transform 모드 — 플레이어가 때리면 우호도는 움직이지만 반격 안 함.
- 측정: 섬 선착장에서 하차 → 발치 1.8 m → 길로 섬 위까지 걸어 올라감(작은섬 A 확인). 길 최대 경사 13~25°.
- **섬 생성기 메뉴는 비활성화**(사용자 지시 — `[MenuItem]` 주석 처리). 섬 지형은 손으로 다듬는 중이라 다시 돌리면 덮어써짐.
- **일본섬 터레인은 사용자가 직접 조각함**(성 자리 계단식 고원). 섬 중심 기준(1751, −1195) 로컬 좌표로 Claude 가 직각으로 정리(2026-09-26): 혼마루 고원 F x 79~176 · z −55~53 · 154.66 m, 아래 단 x 42~231 · z −130~−55(+ x 42~79 · z −55~−47.5, 동쪽 귀 x 176~213 · z −55~−34) · 89.80 m, 북쪽 고원 129.75 m(서쪽 모서리 x = 64 직선, z 53~125). 사각형 밖 18 m 안의 비탈은 20 m 바깥 높이까지 깎아 벽을 세움(벽 = 헤이트맵 한 칸 1.46 m). F 북쪽 파인 곳(x 114~141, z 5.5~53)은 북쪽 129.75 → 남쪽 154.66 한 방향 경사(약 28°).
- 일본섬 텍스처는 현재 높이로 다시 칠함: 바위 레이어(5·6) 안 씀 — 경사 26~36° 부터 절벽(풀+바위) 레이어, 95~135 m 위 절벽 45 % 섞음. 예전 성 언덕 경사 고리(바위 무늬)가 평지에 남아 있던 것 제거.
- **일본섬 구역(사용자 지정 영역도, 2026-09-26)**: 서·남 저지대 = 일반민가, 동쪽 저지대 = 억새밭+논밭, 성과 신사 사이 고원(129.75 m) = 기와민가(부유층), 동쪽 184 m 둔덕(E, 로컬 x 146~214 · z 196~292) = 성 진입 검문소, 서쪽 184 m 고원(W, x 12~94 · z 186~264) = 성(`CastlePlan` 을 사용자가 옮겨 둠, 회전 180°), 154.66 m 고원 = 신사.
- `JapanTownPlan`(씬 루트, 임시 큐브, 콜라이더 없음, 색 머티리얼 `Islands/PlanMaterials/Plan_*.mat`): 일반민가 약 290채(본채 7~10.5 × 10~15 × 5~7 m + 25 % 는 옆에 작은 별채, 25 m 격자를 노이즈로 크게 휘고(±16 m) 흔들고(±4.5 m) 방향장(±35°)·빈 터 노이즈로 들쭉날쭉 — 사용자가 "너무 균일"하다고 해서 바꿈; 4·5칸마다 굽은 골목, 각도 110°~−55°·높이 3~12 m·해안 18 m 여유·도로 10 m 여유·서로 겹치지 않게), 상인구역 22채(9×14×7 m, 선착장에서 섬 안쪽 길 양옆 14 m), 절(목탑) 자리 판 70×90 m(로컬 −230, −215), 기와민가 12채(14~19 × 12~17 × 7~9 m, ±20° 흔듦, 고원 129.75 m 위), 신사 본전 14×10×10 m(신사 고원 남쪽 끝 가운데 x 127.5 · z −43), 검문소↔성 다리 `CastleBridge_검문소-성`(Base 선착장 데크 3개 이어 붙임, 경사로·기둥 제거, 로컬 x 89~151 · z 225.3, 판자 184.4 m).
- 성 진입로(사용자 그림 → 씬뷰 카메라 레이로 옮김, 가파른 곳은 사용자 요청으로 지그재그로 바꿈): 동쪽 저지대 (433, −35) → 북으로 (406, 86) → **동쪽 비탈 지그재그 4구간**(z 108~208 사이 남북 왕복, 구간마다 등고선을 따라 14° 로 오름, 22.5 m → 122 m, 모서리 둥글림) → 고원 (262, 140) → (291, 179) → 북으로 x ≈ 290 → (233, 338). 길이 약 860 m. 도로 높이를 정해 폭 8 m 는 그 높이로, 11 m 까지 흐리게 깎고 메움(최대 변화 약 15 m), 도로 면 경사 최대 15°(지형 샘플로는 21° 가 몇 곳). 폭 7 m 로 칠함(`Road_dirt_gravel`, 일본섬 레이어 칸 6). 예전 검문소 경사로 갈래 길은 뺌(그 경사로는 사용자가 만든 약 46° 비탈).
- `CastlePlan`(씬 루트, 임시 크기 확인용 큐브, 콜라이더 있음, 바닥 = 섬 중심 높이 52.9 m 한 면): 천수각 = 히메지 대천수 기준 — 돌 기단 27×25×15 m, 1~3층 23.5 m 각 × 16 m(1층 554 m²), 4~6층 15.5 m 각 × 15.5 m(4층 240 m²) → 전체 46.5 m(히메지 46.4 m). 혼마루 흙담 80×80 m·높이 3 m, 모서리 망루 12×10×12 m ×2, 어전 32×16×10 / 22×14×8 m. 기단 15 m·건물 31.5 m 로 나눈 값과 망루·어전·흙담 크기는 확인 안 된 어림값.
- 텍스처 규칙: 해저 < −2.5, 모래 < ~3 m, 경사 20~28°부터 바위, 38~48°부터 절벽, 90~130 m 위 바위 섞임.

### 경복궁 에셋 (`02.Resources/01.Asset/Models/Gyeongbokgung/`, 국가유산청 KHS, 무료, 원래 URP)
- 패키지 11개(2026-09-26): Gwanghwamun Complex, Vol1 근정전, Vol2 사정전, Vol3 강녕전, Vol4 교태전, 경회루, 건춘문, 흥례문, 신무문, 영추문, 동궁. 단지 폴더 = DongGungComplex, GangnyeongieonComplex(패키지 철자 그대로), GeonchunmunComplex, GeunjeongjeonComplex, GwanghwamunComplex, GyeonghoeruComplex, GyotaejeonComplex, HeungnyemunComplex, SajeongjeonComplex, SinmumunComplex, YeongchumunComplex. 폴더 전체 약 20.9 GB(원본 + `_Baked` 6.3 GB).
- **Gwanghwamun Complex 패키지에는 광화문 문루가 없음**(모델 코드 CGCS·GSB·HSM·SMJC·YSM — 문루와 흥례문 사이 마당의 부속 건물). 문루·월대·해태상은 별도 패키지 "KHS_Korean Gwanghwamun and Woldae&Haetae"(12번째) → `GwanghwamunGateComplex/`(사용자가 `Assets/Gwanghwamun_Woldae Haetae` 로 임포트한 것을 GUID 유지한 채 옮김). 이 패키지는 언리얼 셰이더가 아니라 Standard/Autodesk Interactive 머티리얼(`_SpecGlossMap` = 거칠기 R 텍스처) → HDRP/Lit 로 변환(매끄러움 = 1 − R 평균(선형), 최대 0.6). 기존 텍스처와 같은 파일은 1장(`T_GHM_T0301A_N` = `T_KHL_Roofornament01c_NM`) — 경회루 것으로 연결하고 휴지통. `M_GHM_Auspiciousanimal` 은 머티리얼 변형(Variant)이라 셰이더를 못 바꿔 `parent = null` 로 떼고 변환. 데모 씬 `Scenes/Demo.unity`(문루 + 월대 1개 메시 + 해태 2 + 길·잔디·바닥), `Overview.unity`(부재 나열). 데모 삼각형(측정, 2026-09-29): 문루 `SM_GHM_Gwanghwamun` 819,591(서브메시 30, 양옆 궁장 포함 폭 158.5 m) / 월대 `SM_GHM_Woldae` 157,008(서브메시 10) / 해태 A·B 96,537·100,734 / 잔디·길·바닥 1,514. `PalaceOrigExport.Export` 로 문루·월대·바닥(해태 제외, 월대는 이후 작업에서 뺌)을 `D:\Blender\Enviroment\PalaceJoseon\광화문\Original\` 에 내보냄.
- 가져오기: 에디터 임포트 대신 .unitypackage 를 직접 풀어 같은 내용(MD5) 파일은 하나만 남기고 참조 GUID 를 바꿔 연결 — GUID 19,433개 중 2,497개 합침. 공용 텍스처 1,014장 = `_Shared/Textures`, 설정이 같은 머티리얼 689개를 대표 346개로 합쳐 `_Shared/Materials`. 작업 파일(스캔·변환표·로그) = `D:\UnityAssetCache\_extract\`.
- 머티리얼: 원래는 언리얼에서 옮긴 URP 전용 셰이더(`Unreal/MI_*`). 슬롯 뜻은 셰이더의 `Material_Texture2D_N("라벨")` 과 hlsl `CalcPixelMaterialInputs`, UV 배율·노멀 평탄화·틴트·금속/거칠기/AO 배율은 hlsl 에 박힌 PreshaderBuffer 값에서 읽어 **HDRP/Lit 로 변환**(키워드는 리플렉션으로 `HDShaderUtils.ResetMaterialKeywords`). 결과(측정, `_Baked` 제외): HDRP/Lit 2,406개 중 UV 뒤집힘 0, UV 배율 1 이 아닌 것 22, 노멀맵 2,155, 마스크맵 0, 평균 매끄러움 0.357(0.6 초과 3개). 그 밖 셰이더 1개(Unlit/Transparent Cutout). 데칼(`_DC`, 현판 글씨 등)은 따로 처리 안 함.
- 이 에셋 규칙(근정전 첫 변환 때 확인): UV 는 뒤집지 않는다(원래 셰이더는 (u, 1−v) 로 읽지만 HDRP 에서 뒤집으면 아틀라스 소품·일월오봉도가 깨짐). 거칠기 텍스처는 sRGB 로 들어와 있어 감마로 되돌려야 함. 노멀맵 초록 채널 방향은 확인 필요.
- 데모 씬(`<단지>/Scene(s)/Demo_*.unity`)마다 원점이 따로라 궁 전체가 한자리에 조립돼 있지 않음 → 섬에 배치할 때 단지별로 맞춰야 함.
- 원본 부하(데모 씬 11개 합계, 측정): 렌더러 48,843 / 삼각형 약 1억 3,860만. 큰 것: 사정전(`Demo.unity`) 2,908만, 동궁 2,399만, 근정전 2,171만(렌더러 10,818), 강녕전 2,072만, 흥례문 1,986만, 교태전(`Demo_Gyeongbokgung.unity`) 1,158만.

#### 단순화 결과 (`_Baked/<단지>/`, 2026-09-26 다시 만듦)
- 사용자 지정: 플레이어가 드나드는 **광화문(문루·월대·해태)·광화문 부속단지·흥례문·근정전·경회루·강녕전·사정전은 행각·담장까지 단지 전체를 원본으로**(가까이 원본 / 멀리 구운 것). 사정전은 실내 소품(책·책장·함·촛대·등·탁자·어좌·일월오봉도·병풍·문방구·향로 등, `SajeongjeonPropKeys`)만 뺌. 교태전·동궁·건춘문·영추문·신무문은 구운 겉모습만. **모든 궐문 문짝은 원본을 따로 떼어 열 수 있게**.
- 구운 메시: 단지 데모 씬에서 **밖에서 보이는 렌더러만** 골라(눈높이 격자 + 먼 시점 레이, 역방향 검사) 건물 코드(`SM_<코드>`)별 덩어리로 묶고 **지붕 / 목부 / 창호 / 석재** 4종 아틀라스로 구워 합친 메시. 덩어리마다 FBX + `<덩어리>_<roof|wood|win|stone>_BC/_N.png` + `MAT_<덩어리>_<종류>.mat`(HDRP/Lit, 양면, 매끄러움 지붕 0.3 / 석재 0.15 / 그 밖 0.25) + `<덩어리>_Baked.prefab`. **문짝·데모 바닥·(사정전) 실내 소품은 굽지 않음.** 원본 단지(멀리서만 보임)는 아틀라스 최대 2048, 구운 것만 쓰는 단지는 4096(흥례문 첫 덩어리·광화문 부속단지 일부는 4096 으로 구워짐).
- 원본: `<단지>_Original.prefab` = 데모 씬 렌더러 전체(문짝·데모 바닥·메시 없는 오브젝트·사정전 소품 제외, 조명 없음, 콜라이더 켬). 광화문 문루는 한 메시라 문짝 서브메시를 비운 `SM_GHM_Gwanghwamun_NoDoors.asset` 을 씀.
- 문짝: `<단지>_Doors.prefab` = 문짝마다 `DoorHinge_<번호>_<메시>`(피벗 = 경첩 쪽 아래 모서리, **Y 축으로 돌리면 열림**) 아래 원본 문짝(MeshCollider 켬) + 화면 높이 1 % 에서 끄는 LODGroup. 경첩 쪽 = 옆에 짝 문짝이 있으면 그 반대쪽, 없으면 폭 축 − 쪽(확인 필요). 문짝 = 메시 이름 앞부분 `GateLeafPrefixes`(근정문·행각 문·흥례문·사정문·향오문·양의문·건춘문·영추문·신무문·협생문·용성문·경회루 DoorE/F/H/I — 경회루는 크기로 고름, 어느 문인지 확인 필요), 행각 문(`_HG_Door`)은 폭 1.1 m 이상만(방문 제외). 광화문 문루 문짝(재질 `M_GHM_Gate02A`, 홍예 3곳 × 2짝)은 섬(연결 삼각형) → 문판(폭 3 m·높이 4 m 넘는 섬) → 문판 둘레 안 조각 → 가운데 x 로 잘라 좌·우 6짝(`SM_GHM_Gwanghwamun_DoorLeaf0~5.asset`). 광화문 가운데 문짝을 돌려 열리는 것 캡처로 확인.
- 조립: `<단지>_Complex.prefab` = `LOD`(LODGroup: LOD0 = 원본 전체, **화면 높이 1.0 까지**(단지 크기 기준이라 단지 중심에서 약 크기×0.87 m 안 — 근정전 123 m, 흥례문 113 m, 사정전 94 m, 강녕전 130 m, 경회루 200 m) / LOD1 = 구운 덩어리 전체 + 원본 물, 0.005 까지) + 문짝. 원본의 작은 부재(바운드 대각선 1.5 m 미만, 약 1만 5천 개)는 그림자 끔. 구운 것만 쓰는 단지는 구운 덩어리 + 문짝. 광화문 문루는 부분별로 `GwanghwamunGate_Gate`(문루 + 문짝) / `_Woldae` / `_Haetae` 프리팹(각각 원본/구운 것 LOD).
- 결과(2026-09-27 최적화 뒤 측정, 프리팹 기준. 괄호 = 최적화 전. 구운 것 = 지붕 껍데기 포함, 구운 것만 쓰는 단지는 + 먼 지붕 껍데기):

| 단지 | 원본(LOD0) 렌더러 / 삼각형 | 구운 것 렌더러 / 삼각형 | 문짝 |
|---|---|---|---|
| 광화문 문루·월대·해태 | 4 / 114만 | 6 / 11만 | 6 |
| 광화문 부속단지 | 843 / 95만 (862 / 162만) | 14 / 20만 | 8 |
| 흥례문 | 5,200 / 932만 (5,579 / 1,949만) | 38 / 190만 | 27 |
| 근정전 | 10,706 / 1,554만 (10,786 / 2,114만) | 49 / 229만 | 32 |
| 경회루 | 1,460 / 305만 (1,572 / 333만) | 7 / 36만 | 6 (2층 문 4짝 뺌) |
| 강녕전 | 5,319 / 616만 (7,108 / 2,051만) | 35 / 126만 | 16 |
| 사정전 | 4,416 / 717만 (6,046 / 2,629만) | 50 / 167만 | 14 |
| 교태전 | — | 24 / 98만 + 2.1만 | 18 |
| 동궁 | — | 49 / 340만 + 6.2만 | 4 |
| 건춘문 | — | 12 / 32만 + 0.9만 | 2 |
| 영추문 | — | 8 / 51만 + 1.1만 | 2 |
| 신무문 | — | 10 / 19만 + 0.6만 | 4 |

  텍스처 용량은 최적화 뒤 다시 재지 않음(확인 필요).
  최적화 전 값: 씬 `GyeongBok` 전체 LOD0 원본 삼각형 약 9,355만, 근정전 마당 한가운데에서 그려지는 삼각형 약 2,874만(계산, 시야 컬링 전) — 최적화 뒤 다시 계산 안 함. 텍스처 가져오기는 최대 4096, 스트리밍 밉맵(`PalaceBakedImportPostprocessor`).
- 에디터 도구 `Editor/PalaceBakeTool.cs`(정적 메서드, MCP 에서 호출): `OpenComplex`/`CloseComplex`(데모 씬을 y +3000 에 추가로 열고 조명·볼륨 끔, 저장 안 하고 닫음), `RunVisibility`, `ListChunks`/`ExportChunk`(굽기용 OBJ, 이미 `chunks.tsv` 에 있는 덩어리는 SKIP; 데모 바닥·물·문짝·사정전 소품 제외; `MatCategory` 재질은 서브메시별 종류 힌트), `CodeOf`(HAETAE/WOLDAE 분리), `SaveOriginalComplexPrefab`, `SaveDoorsPrefab`(둘 다 10분 안에 만든 프리팹이 있으면 RECENT 로 건너뜀 — 다시 만들려면 지우고 실행), `ImportBakedStep(단지, 초)`, `CleanDefaultCube`, `AssembleComplex2(단지, 원본여부, 1.0)`. 옛 `SaveOriginalPrefab`/`AssembleComplex`(코드별 LOD)는 안 씀. 작업 파일 = `D:\UnityAssetCache\_bake\<단지>\`(예전 결과는 `out_prev_*`).
- 다시 구울 때 순서: 데모 씬 열기·보임 검사 → 원본/문짝 프리팹 → `chunks.tsv` 비우고 `out` 을 옮긴 뒤 내보내기 → 굽기 → `_Original`·`_Doors`·`_Complex`·`DoorLeaf`·`_NoDoors` 를 뺀 옛 파일 휴지통 → `ImportBakedStep` → `AssembleComplex2`. `_Complex.prefab` 은 지우지 말고 덮어쓸 것(씬 인스턴스 연결 유지). 같은 경로에 다시 만든 `_Baked.prefab` 을 참조하던 씬 인스턴스도 끊기지 않았음(관찰).
- **최적화(2026-09-27, 에디터 도구 `Editor/PalaceOptimizeTool.cs`)**:
  - 창호지: 원본 단지(`FullOriginalComplexes`)에서 속이 비쳐 보이는 창·문 틀을 6 cm 격자 레이로 찾아 불투명 창호지 판(`MAT_Hanji_Window.mat`, 한지 텍스처, 매끄러움 0.15, 양면) + 얇은 BoxCollider 로 막음 → 원본 프리팹 마지막 자식 `WindowPaper`(합친 메시 `<단지>_WindowPaper.asset`). 흥례문 201 / 강녕전 113 / 사정전 43 / 경회루 8장. 경복궁 창호지 머티리얼 34개 매끄러움 0.08(0.36~0.5 는 하늘이 비쳐 속이 보이는 것처럼 보였음).
  - 실내 컬링(`CullBegin`/`CullStep`/`CullApply`/`CullEnd`): 원본을 빈 추가 씬 y +5000 에 놓고 0.75 m 격자로 걸어 갈 수 있는 땅(계단 1.2 m) + 먼 시점에서 보이는 것 + 표면에서 눈높이·바깥으로 빠져나가는 레이로 **밖에서 절대 안 보이는 렌더러를 지움**. 내부를 남기는 곳 = 근정전(GJJ)·경회루(사용자 지정 `Enterable`). 지운 것 흥례문 380 / 근정전 81 / 강녕전 1,789 / 사정전 1,631 / 경회루 20 렌더러(목록 `D:\UnityAssetCache\_bake\<단지>_Culled.txt`).
  - 창살 카드: 창·문 창살 메시(`IsCardMesh`, 233종)를 **앞뒤 판 2장(삼각형 4개)** 에 창살·창호지를 구운 텍스처로 바꿈(400 px/m, 최대 2048, 뚫린 테두리는 알파 컷 0.5). `_Baked/_Cards/`, `ExportCardsStep` → 굽기 → `ImportCardsStep` → `ApplyCards(단지)`. 멀리서 창살이 지글거리던 것 때문(사용자 지시). 가까이 모습은 사용자 확인 필요.
  - 먼 지붕 껍데기(`ImportRoofEnvStep`): 덩어리 지붕을 두께를 준 뒤 0.3 m 복셀로 다시 만든 매끈한 껍데기(삼각형 수천 개)에 기와를 구움(`<덩어리>_roofenv.fbx/_BC/_N`, `MAT_<덩어리>_roofenv` 양면). 원본 단지는 LOD1 의 `<덩어리>_roof` 메시를 껍데기로 바꿈(가까이는 원본 기와 그대로 — 사용자 선택 "먼 거리만 매끈하게"). 구운 것만 쓰는 단지는 `_Baked` 프리팹 안에 LODGroup(가까이 구운 지붕 / 화면 높이 0.5 아래 껍데기). 껍데기↔원본 적중률 `cov_env < 0.85` 또는 `cov_high < 0.8` 이면 껍데기를 안 쓰고 구운 지붕으로 되돌림(장식만 있는 작은 덩어리 등).
  - 지붕 분류: 이름에 rafter/rooffloor/roofinner/roofwall 이 든 것은 목부(예전엔 "roof" 가 들어가 지붕 아틀라스에 섞여 기와가 깨졌음). 전 단지 다시 구움 → `RefreshBakedStep(단지)`(out 의 FBX·PNG 를 덮어쓰고 프리팹 메시·머티리얼 다시 연결, "COPIED" 가 나오면 한 번 더 부름).
  - 경회루 2층 개방: 2층 문짝(`SM_KHL_Door*`, Doorframe 제외)을 원본·구운 것·문짝 프리팹에서 뺌(`IsExcludedProp`) — 실제 경회루처럼 2층이 트임(사용자 지시, 캡처 확인).
  - 빈 머티리얼 슬롯(마젠타) 채움: `SM_HNM_Roof_B05.prefab`, `SM_SJJ_DoorB.prefab`.
  - 순서(원본·구운 것이 바뀌면): `RefreshBakedStep` → `ImportRoofEnvStep` → `AssembleComplex2(단지, true, 1.0f)`(LOD 목록 다시 만듦, 프리팹 덮어써 GUID 유지). 원본을 다시 만들면 컬링·창호지·카드를 다시 해야 함.
- **지붕마다 LOD(2026-09-27, `Editor/PalaceRoofLodTool.cs` `Build(단지)`, 사정전만 적용)**: 예전엔 단지 전체가 LODGroup 하나(거리 = 단지 중심)라 단지 안에 서 있으면 30~70 m 떨어진 기와도 원본이라 지글거렸음(측정: 근정전 123 m·강녕전 130 m·경회루 200 m 안이면 전부 원본). 이제 지붕을 16 m 칸으로 나눠 칸마다 LODGroup(크기 20 m 고정·칸 가운데 기준): LOD0 원본 기와(칸 가운데서 약 25 m 안) / LOD1 매끈한 껍데기 + 노멀맵 / LOD2 평면(윗면만, 노멀맵 없음, 150 m 밖). 20 m 넘는 긴 지붕판만 칸마다 잘라 새 메시로(원본에선 지움), 작은 기와 조각은 같은 메시를 쓰는 오브젝트로 옮김(원본에선 끔). 결과 `<단지>_RoofLOD.prefab` + `RoofLOD/<단지>_RoofLOD_Meshes.asset`, `AssembleComplex2` 가 단지 LOD 밖에 넣음. 구운 덩어리의 `_roof` 는 끔. 껍데기 적중률 나쁜 덩어리(cov_env < 0.85 또는 cov_high < 0.8)는 제외. 지붕 굽기 결과에 `<덩어리>_roofflat.fbx` 추가, 영제교(`SM_HNM_Bridge`)가 "ridge" 로 지붕에 잡히던 것 제외.
- 방 속 비우기(`Editor/PalaceShellTool.cs`)는 사정전 원본에만 적용돼 있고 더 쓰지 않음 — 아래 단순화판으로 대체.
- **단순화판(2026-09-27, 강녕전·사정전)**: 단지 전체를 외부에서 단순화한 메시로 바꾼 `<단지>_Simplified.prefab` 을 씬 `GyeongBok/<단지>` 아래에 넣고 기존 `<단지>_Complex` 인스턴스는 **끔**(지우지 않음, 씬 저장은 사용자). 도구 `Editor/PalaceLodImport.cs`:
  - `ExportCodes(단지, 코드[])`: 원본 프리팹 + `<단지>_RoofLOD` 의 LOD0 기와(위치로 건물 배정)를 코드별 OBJ 로(`D:\UnityAssetCache\_edit\<단지>_<코드>.obj`). 강녕전은 `PalaceBlenderRoundTrip.ExportForEdit` 로 내보냄.
  - `LoadObj`: 외부에서 줄인 OBJ(Unity x = −OBJ x, 면 순서 뒤집음) → 메시 하나(재질마다 서브메시). 재질 이름 끝 `_1S` = 한쪽 면(`ShellMat`), `.001` 꼬리는 뗌. 결과 `_Baked/<단지>/Simplified/<이름>.asset` + `<이름>_LOD.prefab`.
  - `BuildBuilding`: LOD0 = 단순화 메시 + **지붕 받침판**(구운 `<덩어리>_roofenv` 윗면을 법선 반대로 0.25 m 내린 것 — 기와 틈으로 속이 비치지 않게, 그림자 끔, 머티리얼 = 어두운 단색 `MAT_RoofPlate_Black`) / LOD1 = 예전 구운 덩어리(지붕이 꺼진 덩어리는 roofenv/roofflat 을 대신 넣음). 전환 화면 높이 0.25(행각 0.6), 끝 0.003, CrossFade.
  - 본채 3단계(`BuildMainHall` 강녕전 / `BuildMainHallMulti` 사정전): LOD0 단순화본+받침판(0.35) / LOD1 구운 몸체 + 구운 지붕 윗면(평면)(0.1) / LOD2 줄인 구운 몸체 + 검은 판(`_Shared/Materials/MAT_RoofPlate_Black.mat`) + 흰 양성 선(0.004 — 산에서 보는 거리). 흰 양성 = 강녕전 `MI_GNJ_Concrete01`, 사정전 `M_SJJ_RoofWallA/B`.
  - `AssembleSimplified`: 원본에서 `SM_` 코드가 없는 렌더러·지정 접두어(강녕전 `SM_GNJ_SoilFloor`, 사정전 `SM_Door`)는 원본 그대로 `Ground` 로 + `<단지>_Doors` + 건물 LOD 프리팹들. `<단지>_RoofLOD` 는 넣지 않음(기와가 단순화본에 있음).
  - 삼각형(LOD0, 측정): 강녕전 본채 177,696 / HOM 43,392 / EJD 76,109 / YGD 66,461 / GSJ 133,308 / YSJ 132,504 / 행각 230,760(원본 단지 약 615만). 사정전(2026-09-27 다시 만듦, 데모 씬 원본 기준 — 원본 프리팹은 예전 컬링·속 비우기로 합각 벽 등이 빠져 있어 안 씀) 본채 151,431 / SJM 131,796 / MCJ 156,090 / CCJ 150,089 / 행각 156,542(데모 씬 원본 약 2,908만, 건물마다 목표 15만 — 사용자 지정). 사정전 단순화본: 창·문은 모두 판(앞·뒤 사각형 2장)에 창살+흰 창호지를 구운 것(투명 없음, 머티리얼 `SJCARD_<메시>_<GUID6>`, 텍스처·머티리얼 `_Baked/Sajeongjeon/Simplified/Cards/` 84종), 실내 집기 없음, 문고리·문 쇠장식 없음. 여는 문짝 14짝(사정문 10·행각 4)은 `Sajeongjeon_DoorsCard.prefab`(= `Sajeongjeon_Doors` 의 문짝 메시를 앞·뒤 판 2장으로 바꾼 사본, 경첩 피벗·충돌체 유지, 원본 약 11.9만 → 56 삼각형, 판 메시 `Simplified/Cards/Sajeongjeon_DoorCards.asset`)을 쓰고, 단순화본에서는 같은 문짝을 뺌. `AssembleSimplified` 는 `<단지>_DoorsCard.prefab` 이 있으면 그것을 씀. 사정전 조립은 keepMeshPrefixes 를 빈 배열로 부름(문 경첩 쇠 `SM_Door*` 빼기). 가까이서 본 모습·실제 프레임은 사용자 확인 필요.
  - 외부 단순화본 원본 파일: `D:\Blender\Enviroment\PalaceJoseon\강녕전_LOD\`, `…\사정전\LOD\`(OBJ). 다시 넣으려면 OBJ 를 바꾸고 `Build*` → `AssembleSimplified` 를 다시 부름(`LodDir` 를 단지 폴더로 지정).
- **근정전 단순화판(2026-09-28, 도구 `Editor/PalaceGJImport.cs`)**: `Geunjeongjeon_Simplified.prefab` 을 씬 `GyeongBok/근정전_Geunjeongjeon` 아래(기존 `Geunjeongjeon_Complex` 와 같은 자리 (0,0,42))에 넣고 `Geunjeongjeon_Complex` 는 끔(씬 저장은 사용자). OBJ = `D:\Blender\Enviroment\PalaceJoseon\근정전\LOD\GJ_*.obj`(단지 프리팹 루트 좌표, `LoadObj` 규칙).
  - 구성(프리팹 `_Baked/Geunjeongjeon/Simplified/*_LOD.prefab`, LOD0 삼각형 측정): `GJ_GJM_LOD` 근정문 243,947(LOD1 = 구운 GJM) / `GJ_GJJ_LOD` 전각 `GJ_Hall` 293,014 + 월대 `GJ_Woldae` 70,843 — **렌더러를 나눠 둠**(내부에 들어갔을 때 외부만 저품질로 바꿀 예정, 사용자 지정), LOD1 = 구운 GJJaaa~GJJbbb 8덩어리 / `GJ_YML_LOD` 양옆 누각 147,592(LOD1 = 구운 YML) / `GJ_RR_LOD` 바닥 19,434(LOD 하나) / `GJ_HG_Segments_LOD` 행각 12토막 / `GJ_Interior_LOD` 내부 구조 151,784 + 집기 236,512 / `Geunjeongjeon_DoorsCard`. 받침판(`BlackRoof`)은 GJM·GJJ(bba 제외 7덩어리)·YML 에 넣음. 전환: 건물 LOD0→LOD1 화면 높이 0.25(GJJ 크기 61.6 → 약 213 m, YML 130 → 약 450 m), 근정문만 0.123(크기 28.3 → 약 200 m — 0.25 면 약 98 m 라 마당 안쪽에서 바뀌었음), 끝 0.003.
  - **행각 12토막**: 예전엔 행각 전체(약 117×139 m)가 LODGroup 하나라 한쪽에만 가까이 가도 전부 원본으로 그렸음 → 토막(`HG_Seg01~12`, 근정문 쪽 변 4 + 동·서 변 각 4, 25~35 m)마다 LODGroup(`BuildHGSegmentsBlack`, 토막 판정 `HGSegOf`), 전환 = **토막마다 LOD0 은 약 200 m, LOD1 은 약 500 m 까지**(화면 높이 = 크기 / (거리 × 2·tan30°), 0.18~0.30 / 0.074~0.12, 끝 0.005 — 사용자 지시 "근정문에서도 똑바로, 걸어가며 바뀌면 이상함": 마당 어디서든(근정문 포함, 가장 먼 토막 약 125 m) 행각은 전부 LOD0. 예전 0.6 / 0.2 는 약 60 m 에서 바뀌었음. Main Camera FOV 60, lodBias 1). LOD0 = 외부에서 줄인 행각 + 받침판(0.25 m, 그림자 끔) / **LOD1·LOD2 = 줄인 행각에서 기와(`MI_GJJ_HG_Roof01`)를 빼고 검은 지붕판**(구운 `GJJ_HGa/HGb_roofenv` 윗면을 토막별로 잘라 0.02 m 내림, `BlackRoof`) — 사정전·강녕전처럼 멀리서 기와를 단색 면으로(사용자 지시; 예전엔 먼 LOD 도 줄인 기와라 울퉁불퉁·가시처럼 보였음). 삼각형 합계 LOD0 317,949 + 받침판 약 2.4만 / LOD1 163,951 / LOD2 111,639(검은 판 포함, 토막당 1.2~2.7천). 외부에서 줄일 때 LOD1 은 서까래 절반·공포류 0.35 배, **기둥(재질 `MI_GJJ_HG_Dongjaju01` — 이 에셋 행각 기둥 재질 이름이 동자주)·보·주춧돌·벽·창호는 안 줄임**(줄이면 기둥이 사라지고 벽에 흰 삼각 무늬). 긴 면(용마루 등)이 토막 경계를 넘어 LODGroup 크기가 42~68 m.
  - 내부: `GJ_Interior_LOD` 는 LOD 하나, 화면 높이 0.4 아래에서 꺼짐(약 30 m 크기라 대략 65 m 밖) — 들어갔을 때만 켜고 외부를 저품질로 바꾸는 스크립트는 아직 없음.
  - 창·문: 전부 판(카드, 불투명, 머티리얼 `GJCARD_<메시>` 39종, `Simplified/Cards/`, 스무스니스 0.12, 텍스처 2048). 여는 문짝 30짝(근정문 12 + 행각 18)은 `Geunjeongjeon_DoorsCard.prefab`(= `_Doors` 의 문짝 메시를 앞뒤 판으로, 경첩 피벗·충돌체 유지, 판 메시 `Simplified/Cards/Geunjeongjeon_DoorCards.asset`) — 문짝 원본 559,574 → 1,656 삼각형(근정문 문장식 `SM_GJM_DoorOrnament03A` 2개는 원본 그대로). 단순화본에서는 같은 문짝 판을 뺌.
  - 합계(LOD0, 내부 제외, 행각 전부 LOD0 기준) 약 109만(데모 씬 원본 약 2,171만). 내부 켜면 약 148만. 캡처(마당·위·근정문 앞·내부·행각)로 배치·재질 확인, 실제 프레임·LOD 전환 거리는 사용자 확인 필요.
- **경회루 단순화판(2026-09-28)**: `_Baked/Gyeonghoeru/Simplified/Gyeonghoeru_Simplified.prefab` 을 씬 `GyeongBok/경회루_Gyeonghoeru` 아래(기존 `Gyeonghoeru_Complex` 와 같은 자리 (0,0,−3))에 넣고 `Gyeonghoeru_Complex` 는 끔(씬 저장은 사용자). `경회루_Gyeonghoeru` 홀더는 켜 둔다(사용자 지시 — Claude 가 끄지 말 것). OBJ = `D:\Blender\Enviroment\PalaceJoseon\경회루\LOD\KHL_*.obj`(단지 프리팹 루트 좌표, `LoadObj` 규칙), 메시 = `Simplified/KHL_Simplified_Meshes.asset`.
  - 구성: `KHL_LOD`(LODGroup, 크기 132 m 수동·기준점 = KHL_Main 중심, LOD0 → LOD1 화면 높이 0.25(약 457 m), 끝 0.003) — LOD0 = `KHL_Main` + `KHL_Ground`(원본 바닥·잔디·물 72, MeshCollider) / LOD1 = 예전 구운 `KHLa`·`KHLb` + `KHL_Water`(바닥 메시에서 물 서브메시만; 구운 KHLa_stone 에 바닥이 들어 있어 바닥 전체를 LOD1 에 넣으면 겹침).
  - **2026-09-29 다시 올림**: `KHL_Main` = 325,684 삼각형, 서브메시 74, MeshCollider(메시 에셋을 같은 자리에 덮어써 GUID 유지). 연못 담장·담장 문을 뺐고 문짝 `KHL_GateDoors` 도 프리팹에서 지움(§8). 머티리얼 null 0.
  - `KHL_Main` 에 든 것: 원본(데모 씬)에서 밖에서 안 보이는 면을 지우고 줄인 메시 + 판(카드)들(재질 `Simplified/Cards/MI_KHL_*`): 2층 안쪽 방 문 `InnerDoor_w071/w076/w089`, 안쪽 창 `InnerWin_w405/w435/w506`, 2층 바깥 둘레 낙양각 `Frame_*`(알파 클립 0.5), 2층 바깥 둘레 문 위 창살 자리 `WhiteBoard`(단색), 계단 난간 `RailCard_<N|P>_<SlopeIn|SlopeOut|TopIn|TopOut|TopEnd>` 10종(알파 클립 — 난간 틀·기둥은 원본 입체 메시, 사이 무늬만 판), 2층 바깥 난간 옆면 `PerimCard_PX/NX/PY/NY`(알파 클립), 1층 천장 평면 `Ceiling1F_Flat`(알파 클립), 2층 천장 평면 `Ceil2Card_Ring/Inner`(불투명, 노멀맵) + 기와 밑 받침판(재질 `_Shared/Materials/MAT_RoofPlate_Black`, 위를 향한 한쪽 면만 — 기와 틈으로 하늘·바닥이 비치던 것 막음). 2층 바깥 둘레 문짝·들쇠는 없음(사용자 지시). 텍스처 70장 `Simplified/Cards/*.png`(노멀맵 `_N`). 안 쓰게 된 `MI_KHL_GateCard_01~06` 머티리얼·텍스처는 폴더에 남아 있음.
  - 캡처(위·1층 천장 밑·계단 난간·2층 바깥 난간·안쪽 창)로 담장 없음·천장 평면·난간 채움 확인. 연못 자리를 터레인(약 y 10)이 덮어 물(월드 y 약 9.0)·석축 아랫부분이 안 보임 — Complex 때도 같은 자리라 원래 있던 문제로 보임(확인 필요). 실제 프레임·LOD 전환 거리는 사용자 확인 필요.
- **해태상·월대는 고종 대 조성**: 해태상 1870년(고종 7) 월대 앞 설치, 월대 1866년(고종 3) 축조(《경복궁영건일기》). 월대는 2007년 발굴에서 고종 이전 것으로 추정되는 잡석 유구가 나왔으나 세종 대 것인지는 규명 안 됨(출처: 위키백과 「광화문」, 경향신문 2022-09-20 이기환 기사). 광화문 자체는 1395년 건립·1592년 임진왜란 소실이라 인종·명종대에 있음. → 해태상·월대는 배치에서 **별도 오브젝트**.
- 근정전 단지 모델 코드(사용자 확인): GJJ = 근정전 본전(월대 포함), GJJ_HG = 행각, GJM = 근정문, YML = 행각 양옆 누각, RR = 바닥. 데모 씬 `GeunjeongjeonComplex/Scenes/Demo_Geunjeongjeon.unity` 삼각형: GJJ 1,544만 / GJJ_HG 363만 / GJM 222만 / YML 40만 / RR 2만.
- **동궁 단순화판(2026-09-29 다시 올림)**: `_Baked/Donggung/Simplified/Donggung_Simplified.prefab`(같은 경로에 덮어써 GUID 유지)을 씬 `GyeongBok/동궁_Donggung` 아래에 두고 `Donggung_Complex` 는 끔, 홀더 `동궁_Donggung` 은 켬(사용자 지시 — 끄지 말 것). 인스턴스 로컬 (30.31, 0, 51.60) 회전 (0, 270, 0)(사용자 지시로 270° 돌림), 월드 바운드 중심 (−2640.4, 17.0, 4233.7) 크기 (84, 11, 167). 씬 저장은 사용자. OBJ = `D:\Blender\Enviroment\PalaceJoseon\동궁\LOD\DG_Bld_* / DG_HG_* / DG_Wall_* / DG_DoorWindowCards / DG_GateDoors.obj`(단지 프리팹 루트 좌표, `LoadObj` 규칙).
  - 구성: 건물별 45개(`Buildings/DG_Bld_01~10` 전각·대문, `DG_HG_01~26` 행각 날개, `DG_Wall_01~09` 담장 구간) + `DG_DoorWindowCards`(문·창 판) + `DG_GateDoors`(담장 대문 문짝 6곳, 연한 갈색 `Simplified/Cards/MI_DG_GateDoorWood` = RedWood 판문 무늬를 틀 나무색으로 바꾼 `T_DG_GateDoorWood_BC.png`). 오브젝트마다 LODGroup 하나: 건물 45개는 LOD0 → **LOD1(단순화) 을 건물 LODGroup 중심에서 약 50 m**(화면 높이 = 크기 / (50 × 1.1547), 최대 0.95 — 큰 행각 `DG_HG_14/15/17/26` 은 그래서 55~118 m), 0.003 에서 끔. 문·창 판과 대문 문짝은 LOD0 만. 삼각형 합계 4,189,471(측정) — 외부에서 건물별로 모양 유지 한도(표면 차 1.5 cm)까지 줄인 것, 기와 약 35%·목부 10~70%, 벽·돌은 평면 합치기만. LOD1(`<이름>_LOD1`) = 기와(`MI_DG_Giwa`) 면을 빼고 그 자리에 지붕 윗면을 따라 만든 판(0.3 m 격자, 3×3 평균, 기와 꼭대기에서 3 cm 아래)을 넣은 것 — 판 재질 `Simplified/Cards/MI_DG_RoofTop`(기와를 위에서 찍은 그림 `DG_RoofTop_BC.png`, 원본 8192×4096 → 가져오기 4096×2048, 위에서 투영한 UV). 전각 지붕 재질(`Roof01A` 등)·용마루·장식은 LOD1 에도 그대로. LOD1 합계 1,911,954 삼각형(측정). 메시 = `Simplified/Donggung_Buildings_Meshes.asset`(578 MB, LOD0+LOD1, `PalaceMeshCompact`). 예전 `Donggung_Simplified_Meshes.asset`(구역 LOD·안쪽 판·받침판)은 휴지통. 충돌체 없음. 머티리얼 null 0.
  - 내부 진입은 없지만 건물 안쪽을 지우지 않은 판(예전 "안쪽 지우기 + 둘레 판"은 벽이 사라져 사용자가 되돌림).
  - 문·창: 원래 1,824개 자리 전부 앞·뒤 판 3,582장(`MI_DGCard_<종류>`, 84종). 26종(`Door03A/B`, `Door04A~H`, `Door07A~F`, `Door08A~E`, `HG_Door03H/I`, `Window01A/C/D`)은 판 모양을 문짝 크기에서 새로 잡아 앞·뒤 그림이나 좌우가 바뀌었을 수 있음(확인 필요). 데모에서 머티리얼 빈 슬롯이던 면(대문 건물 2곳, 830 삼각형)은 뺌.
  - Unity 캡처(조감·전각 앞)로 배치·문 채움 확인. 가까이 모습·실제 프레임은 사용자 확인 필요.
- 동궁 데모 씬 `DongGungComplex/Demo_DongGung.unity`: 렌더러 8,337(계층 없이 루트 한 곳), 메시 코드는 전부 `DG`(`SM_DG_HG_*` 행각 3,191개 1,113만 삼각형 / 그 밖 약 1,286만 / 바닥 `DG_Ground_Geo` 48), 합계 23,992,236 삼각형. 범위 x −134~30 · z −16~65 m(데모 좌표). 본채 지붕 `SM_DG_Roof01A`(−118, 9)·`Roof02A`(−118, 49)·`Roof03A`(9, 7), 문 지붕 `SM_MGJ_RoofR`(−21·−40, 48). 일부 렌더러(`SM_DG_Door08A` 24곳, `SM_DG_Giwa03A-b` 10곳)는 서브메시 수보다 머티리얼이 적음(빈 슬롯).
- **교태전 단순화판(2026-09-29)**: `_Baked/Gyotaejeon/Simplified/Gyotaejeon_Simplified.prefab` 을 씬 `GyeongBok/교태전_Gyotaejeon` 아래 기존 `Gyotaejeon_Complex` 와 같은 자리 (−2, 0, −111)에 넣고 `Gyotaejeon_Complex` 는 끔. 홀더 `교태전_Gyotaejeon` 은 켜져 있음(2026-09-30 확인 — 예전엔 꺼져 있었음). 씬 저장은 사용자. OBJ = `D:\Blender\Enviroment\PalaceJoseon\교태전\LOD\GTJ_*.obj`(단지 프리팹 루트 좌표, `LoadObj` 규칙).
  - 구성: `Buildings/GTJ_GTJ·GSG·HHG·WGH·YEM·GSJ`(코드별 한 오브젝트) + `GTJ_DoorWindowCards`(문·창 판, 판 재질 `Simplified/Cards/MI_GTJCard_<종류>` 161종 = 동궁 `MI_DGCard_DoorB` 를 복제해 텍스처 `GTJCard_SM_<종류>_BC.png` 로 바꾸고 노멀맵 뺌, 알파 클립 0.5) + `GTJ_ClosedDoorPanels`(재질 `GTJ_ClosedPanel_*` 2종) + `GTJ_Chimney0/1`. 오브젝트마다 LODGroup(LOD0 만, 0.003 에서 끔). 삼각형 합계 1,110,676(측정). 메시 = `Simplified/Gyotaejeon_Simplified_Meshes.asset`(110 MB). 머티리얼 null 0. 충돌체 없음.
  - 건물은 모양 유지 한도(표면 차 1.5 cm, 벽·돌은 평면 합치기만)까지 줄인 것. 문 앞에 매달린 초록 판자·쇠사슬(재질 `MI_GSG_GreenWood`·`MI_GSG_GreenWood_Steel`)은 뺌(사용자 지시).
  - 행각 통로 협문(초록 판문 판 `MI_GTJCard_GTJ_HG_Door05A_007`/`05B_008`, 통로 2곳 × 양 끝 = 4곳, 한 곳에 4짝)은 원래 데모에서 벽 쪽으로 열려 있던 것을 **닫힌 모습으로 옮김**(사용자 지시): 문짝 폭을 문틀 사이(2.74 m)에 맞춰 4등분 + 양옆 3 cm 겹침, 짝마다 앞뒤로 1~3 cm 어긋나게 둠(판 가장자리 투명 부분으로 틈이 비치던 것 막음). 메시 에셋 안 `GTJ_DoorWindowCards` 를 같은 자리에서 바꿔 프리팹은 그대로. Unity 캡처(밖·통로 안 양쪽)로 틈 없음 확인.
  - Unity 캡처(조감·교태전 앞)로 배치 확인. 가까이 모습·LOD 없음(먼 거리 단순화 없음)·실제 프레임은 사용자 확인 필요.
- 교태전 데모 씬 `GyotaejeonComplex/Scenes/Demo_Gyeongbokgung.unity`(측정, 2026-09-29): 메시 코드 GTJ 2,643 렌더러 623만 / HHG 789·169만 / GSG 654·162만 / WGH 527·142만 / YEM 218·62만 / GSJ 3·84 삼각형. 문·창(메시 이름에 Door·Window, Frame 제외) 166종 968개 = 647만 삼각형(전체의 절반 이상). `PalaceOrigExport.Export` 로 코드별 + `DoorsWindows` OBJ 를 `D:\Blender\Enviroment\PalaceJoseon\교태전\Original\` 에 내보냄(문·창 OBJ 967 MB, MCP 시간 초과가 나도 끝까지 써짐).
- **광화문 문루 단순화판(2026-09-30)**: `_Baked/GwanghwamunGate/Simplified/GwanghwamunGate_Simplified.prefab` 을 씬 `GyeongBok/GwangHwamun/광화문_Gwanghwamun` 아래 기존 `GwanghwamunGate_Gate` 와 같은 자리 (0,0,0)에 넣고 `GwanghwamunGate_Gate` 는 끔(씬 저장은 사용자). 월대·해태 홀더는 손대지 않음. 새 문루+궁장 월드 바운드 = 옛 문루와 같음(측정: 중심 (−2745.0, 21.3, 3887.4), 크기 (158.5, 21.4, 16.4)). OBJ = `D:\Blender\Enviroment\PalaceJoseon\광화문\LOD\GHM_*.obj` + `pivots.txt`(문짝 경첩 위치, Unity 좌표)(문루 프리팹 루트 좌표, `LoadObj` 규칙).
  - `GHM_Core_LOD`(문루 가운데 |x| ≤ 23.5 m, LODGroup 크기 49 m): LOD0 = `GHM_Core` 351,081(서브메시 31, MeshCollider), 60 m 까지 / LOD1 = 같은 메시에 머티리얼 앞 28개만(기와 `M_GHM_Top01A/Top02A`·잡상 `M_GHM_T0301A` 빠짐) + 지붕 껍데기 `GHM_Core_RoofShell`(`MAT_GHM_roofenv`, 8,470) + 껍데기 위로 올린 잡상 `GHM_Core_Jabsang_OnShell`(5,342) = 247,490, 150 m 까지 / LOD2 = 앞 23개(기와·단청·장식 빠짐) + 줄인 단청·장식 `GHM_Core_LOD2Detail`(앞 4 서브메시) + 원본 창 `GHM_Core_LOD2Win`(6,045) + 잡상 + 껍데기 = 85,624, 0.003 에서 끔.
  - 궁장 `Walls/GHM_WallA1~A3·B1~B3_LOD`(한쪽 3토막, 토막당 14,394~23,667 — 거의 전부 기와, A1 은 23,661 중 23,610): LOD0 원본 / LOD1 기와(마지막 서브메시 `M_GHM_Top01A`) 빼고 껍데기 토막(702~1,287). 기준점·크기 = 그 토막의 기와 부분, 60 m 에서 바뀜.
  - 문짝 `Doors/DoorHinge_GHM_GateLeaf0~5`(정문 6짝, 2,267~2,350) + `DoorHinge_GHM_SmallGate_W_S/W_N/E_S/E_N`(작은 문 4짝, 789~877): 피벗 = 경첩, Y 축으로 돌리면 열림, MeshCollider, 화면 높이 0.002 에서 끔.
  - 전환: 화면 높이 = 크기 / (거리 × 2·tan30°), CrossFade. 렌더러 머티리얼 수가 서브메시 수보다 적으면 남은 서브메시는 안 그려짐(LOD1 에서 껍데기를 끄고 캡처해 기와가 없는 것 확인) — 한 메시를 LOD 마다 머티리얼 개수만 달리해 씀.
  - 메시 에셋(`Simplified/`): `GHM_Core.asset`(**서브메시 27 = Win01A, 28 = T0301A 로 바꿔 둠 — OBJ 재질 순서와 다름**), `GHM_WallA1.asset`(궁장 6개), `GHM_Core_RoofShell.asset`(껍데기 7개 + 잡상), `GHM_Core_LOD2Detail.asset`(줄인 단청·장식 5 서브메시 — 뾰족한 삼각형이 있던 창 서브메시는 뺌 — + 원본 창), `GHM_GateLeaf0.asset`(문짝 10개). 판 텍스처·재질 `Simplified/Cards/`(`MI_GHM_WinCard`, `MI_GHM_CeilCard`, `MI_GHM_2F_Gongpo`).
  - Unity 캡처(45 m 에서 LOD0/1/2 강제, 지붕 모서리 가까이, 100·250 m)로 배치·전환·잡상 확인, 콘솔 오류·경고 0. 멀리서 기와 지글거림이 줄었는지(움직이며 봐야 함)·실제 프레임은 사용자 확인 필요.
  - 문짝 10짝은 2026-09-30 판(상자)으로 바꿈 — 아래 "남은 문·문 장식 판 교체".
- **남은 문·문 장식 판 교체(2026-09-30, `Editor/PalaceCardTool.cs`)**: 단순화판(광화문 문루·근정전·강녕전·교태전·동궁)에 메시로 남아 있던 여닫는 문짝·문 장식·쇠문·홍살·작은 창을 판(카드, 이미지를 입힌 평면)으로 바꿈(사용자 지시: 모든 창살·문을 판에 이미지만). 여닫는 문짝(광화문 정문 6짝·작은 문 4짝, 강녕전 향오문 `SM_HOM_Door01A~03A` 8짝, 행각 문 `SM_GNJ_HG_Door02A_024` 8짝) = 상자 판(앞·뒤·옆 6면, 12 삼각형, 경첩 피벗 그대로, MeshCollider 도 상자), 나머지 = 앞·뒤 판 2장(맞은 곳만 불투명).
  - 강녕전 `Gangnyeongjeon_Doors` 는 원본 문짝 메시(향오문 13,560~16,236·행각 문 11,106 삼각형)를 쓰고 있었음.
  - 삼각형(측정, 오브젝트 합): 광화문 문짝 17,123 → 120 / 근정전 문 장식(`GJ_Hall`·`GJ_RR` 의 `MI_GJJ_DoorOrnament04` 187조각, 근정문 `SM_GJM_DoorOrnament03A` 2개) 14,987 → 756 / 강녕전(문짝 16 + 응지당 쇠문·홍살 + 행각 문틀 `Door03` 56조각 + 연길당 쇠문) 214,454 → 456 / 교태전 함흥각 작은 창 948 → 4 / 동궁(`DG_HG_26` 판문·쇠문, `DG_HG_01/08` 쇠문, `DG_GateDoors` 대문 6곳, LOD0 + LOD1) 9,186 → 64. 합계 256,698 → 1,400.
  - 판으로 안 바꾼 것(확인): 광화문 `GHM_Core` 의 `Win01A`(단청 보)·`Gate01A/03A/04A`(석재), 경회루 `KHL_Main` 의 `Windowframe`(틀), 교태전 `DoorFrame`·`Wallframe`(나무 틀), 교태전 문고리(`Doorhandle`, 입체 고리), 동궁 `DG_Bld_06`·`DG_HG_24`·`DG_HG_17`·`DG_HG_19` 의 `MI_DG_Door01A`(문짝이 아니라 3 cm 막대·꼭지 — 판으로 바꿨다가 되돌림, 원래 재질 = `_Shared/Materials/MI_DG_Door01A.mat`).
  - 새 에셋: `_Baked/<단지>/Simplified/Cards/<메시>_card.png`(아틀라스, Clamp, 알파 클립이면 밉맵 coverage 유지 0.5, 가져오면 가까운 2의 거듭제곱 크기) + `CARD_<메시>.mat`(HDRP/Lit, 한쪽 면, 매끄러움 0.12, 알파 클립 0.5) + `Cards/Meshes/<원래 메시>_Card.asset`(원래 메시에서 대상 서브메시를 빼고 판을 마지막 서브메시로, 안 쓰는 정점 뺌, `PalaceMeshCompact`). 메시 에셋 합계 209 MB(근정전 66.6·강녕전 46.2·교태전 14.6·동궁 81.7 — 큰 건물 메시가 통째로 새로 생김). 원래 메시·FBX 는 그대로, 프리팹(`GwanghwamunGate_Simplified`, `Geunjeongjeon_DoorsCard`, `GJ_GJJ_LOD`, `GJ_RR_LOD`, `Gangnyeongjeon_Doors`, `GNJC_EJD/GNJ_HG/YGD_LOD`, `Gyotaejeon_Simplified`, `Donggung_Simplified`)에서 메시·재질·MeshCollider 만 바꿈 — 되돌리려면 프리팹만.
  - 판 데이터(작업 파일) = `D:\UnityAssetCache\_cards\unity\`(`ucards2.bin` 판 정점 + 새 서브메시 목록, `ucards.tsv`, `targets.tsv` = `ExportTargets` 결과, `Cards/`). 다시 적용: `PrepareCards(폴더, 20, "다시 만들 항목,…")` 를 DONE 까지(메시는 같은 에셋에 덮어써 프리팹 연결 유지) → `ApplyToPrefabs(폴더)` → `Verify(폴더)`(41 오브젝트 ok, 2026-09-30).
  - Unity 캡처(광화문 정문 앞·가까이, 향오문, 강녕전 행각 안팎(원본과 비교), 근정전 문 장식, 동궁 대문, 응지당)로 배치·재질 확인, 콘솔 오류·경고 0. 가까이 모습(판이라 옆에서 보면 납작함, 문 장식 판이 문 앞 몇 cm 에 뜸)·실제 프레임은 사용자 확인 필요.
- **흥례문·건춘문·영추문·신무문·광화문 부속단지 원형 유지 감면(2026-09-30 — 흥례문만 Unity 에 올림, 아래 "흥례문 단순화판")**: 데모 씬을 `PalaceBakeTool.OpenComplex` 로 열고 `PalaceBinExport.Export(폴더)` 로 내보낸 고유 메시를 외부에서 줄임(씬·에셋은 안 바꿈). 폴더 = `D:\Blender\Enviroment\PalaceJoseon\FourDoor\Source\<Heungnyemun|Geonchunmun|Yeongchumun|Sinmumun>\`, 부속단지 `…\광화문\Source_Complex\`. 폴더마다 내보낸 원본(`meshes.bin`·`instances.tsv`·`materials.tsv`) + `reduced.bin`(줄인 것 — 형식·메시 순서가 `meshes.bin` 과 같음, 배치·재질은 원래 `instances.tsv`·`materials.tsv` 를 그대로 씀) + `report.tsv`(메시별 높이·허용 오차·삼각형 수).
  - `PalaceBinExport`: `__VIS_HOLDER`(OpenComplex 가 만든 데모 루트) 아래 켜진 MeshRenderer 를 고유 메시(`meshes.bin`: 매직 `0x31584250`, 메시마다 이름·위치·법선·UV0·서브메시 인덱스, 리틀 엔디언) + 배치(`instances.tsv`: 메시 번호, 이름, 루트 기준 4×4 행렬 16개(행 우선), 서브메시별 재질 이름 `|` 구분) + 재질(`materials.tsv`: 셰이더, BaseColor·Normal·Mask 경로, 색, 타일링, 알파 클립, 매끄러움, 노멀 세기, 양면)로 씀. 렌더러마다 정점을 다시 쓰는 OBJ 대신 같은 메시는 한 번만. Unity 좌표 그대로. `meshes.bin` 이 있으면 SKIP.
  - 데모 씬(측정, 켜진 렌더러 / 고유 메시 / 재질 / 삼각형): 흥례문 5,606 / 250 / 65 / 19,860,120 · 건춘문 1,131 / 143 / 62 / 2,025,047 · 영추문 1,625 / 152 / 66 / 3,689,997 · 신무문 623 / 130 / 60 / 945,100 · 광화문 부속단지 871 / 101 / 33 / 1,677,176. 흥례문 메시는 삼각형마다 정점이 거의 따로(고유 메시 389만 삼각형·974만 정점, 250개 중 129개가 각진 음영), 나머지 단지는 정점/삼각형 0.9~1.16.
  - 삼각형(배치 합, 데모 바닥 제외, 측정): 흥례문 19,860,080 → 6,377,320(32.1 %) / 건춘문 2,024,847 → 1,031,566(50.9 %) / 영추문 3,689,797 → 1,817,679(49.3 %) / 신무문 944,900 → 553,322(58.6 %) / 광화문 부속단지 1,676,976 → 829,068(49.4 %).
  - 방법: 정점을 새로 만들지 않고 남길 정점만 고르는 줄이기라 UV·법선은 원래 값, 재질(서브메시) 경계·UV 이음매는 안 움직임. 허용 오차 상한 = 그 메시 배치의 최저점이 바닥(전 배치 최저점의 5 백분위)에서 2.5 m 미만이면 4 mm, 5 m 미만 8 mm, 그 위 15 mm. 후보 2·4·8·15 mm 를 작은 것부터 원본과 14방향 평행 광선(축 6 + 대각 8)으로 비교해 맞음/안 맞음·깊이·텍스처 색이 어긋난 광선이 0.2 % 이하면 통과, 통과한 것 중 삼각형이 5 % 이상 더 줄 때만 큰 쪽을 고름. 2 mm 도 안 되면 1 mm, 그것도 안 되면 원본(단지마다 0~1개). 12 삼각형 이하·데모 바닥은 그대로.
  - `reduced.bin` 메시 이름 끝 `#F` = 각진 음영 메시(삼각형 90 % 이상이 정점 법선 = 면 법선) — 법선을 0 으로 저장했으므로 가져올 때 삼각형마다 정점을 나눠 면 법선을 넣어야 함. 그 밖 메시는 (위치, UV, 법선)이 같은 정점을 합친 것이라 정점 법선 그대로.
  - 줄인 뒤 큰 것(개수 × 삼각형): 흥례문 `SM_HNM_Windowframe_A` 87 × 15,270(원래 90,940) = 132.8만, `SM_HNM_Roof_B03` 34 × 23,234 = 79.0만, `SM_HNM_Roof_A01` 1 × 423,669(원래 1,310,940). 건춘문·영추문·신무문 지붕 `SM_GCM/YCM/SMM_RoofA`(37,458)는 거의 안 줄었음(37,423). Unity 에서 본 모습은 아직 확인 안 함.
  - **판(카드) 데이터(2026-09-30)**: 창살·문·홍살·문 천장을 판으로 바꾼 결과를 같은 폴더에 둠 — `cards.bin`(바뀐 메시, `meshes.bin` 형식·Unity 좌표; 이름 끝 `#CARD` = 통째 판, `#SPLIT` = 틀(남긴 서브메시) + 판(마지막 서브메시), `#F` = 각진 음영), `cards.tsv`(메시 번호, 종류 full/split, 모양 box = 여닫는 문짝 6면 / flat = 앞·뒤 / down·up = 천장 한 장, 모드, 재질 이름 `CARD_<HNM|GCM|YCM|SMM|GHC>_<번호>`, 텍스처, 알파 클립, 남긴 서브메시), `removed.tsv`(문짝 판에 구워 넣어 지울 쇠붙이 배치 줄 — 건춘문·영추문 `DoorLock` 각 1), `Cards/*.png`(97장). `reduced.bin` 대신 이 메시를 쓰는 것. 삼각형(배치 합): 흥례문 6,377,320 → 4,580,276 / 건춘문 1,031,566 → 716,329 / 영추문 1,817,679 → 1,225,956 / 신무문 553,322 → 501,147 / 부속단지 829,068 → 533,809. 천장 판 = 흥례문 `PatternCeilingA/B`·`Celling_A~G`, 건춘문·영추문·신무문 `Banja`, 신무문 `GateCeiling`. 석문 안 연등천장(서까래)은 판으로 안 바꿈. 흥례문 `Windowframe_A/B`(벽판과 창살이 한 메시)는 통째 판. Unity 에서 본 모습은 아직 확인 안 함.
- **흥례문 단순화판(2026-09-30)**: `_Baked/Heungnyemun/Simplified/Heungnyemun_Simplified.prefab` 을 씬 `GyeongBok/흥례문_Heungnyemun` 아래 기존 `Heungnyemun_Complex` 와 같은 자리 (−3, 0, 0)에 넣고 `Heungnyemun_Complex` 는 끔. 홀더 `흥례문_Heungnyemun` 은 켜져 있음. 씬 저장은 사용자. 원본 = 위 원형 유지 감면 메시 + 판(카드) 데이터(`cards.bin`).
  - 계단 뺌(사용자 지시): 문루 1층 서쪽 칸 나무 계단 `SM_HNM_Stair_A` + 발판 돌 `SM_HNM_StoneStair_A` + 2층 계단 구멍 난간 `SM_HNM_Handrail_C`·`SM_HNM_RailPillar_A`. 바닥 막기: 1층 천장 판(`SM_HNM_PatternCeilingA` 판, 텍스처 `HNM_149_SM_HNM_PatternCeilingA_card.png`)의 계단 구멍(0.93 × 2.07 m)과 둘레 틀을 같은 무늬 자리(가로 주기 192.5 px × 4 = 770 px 옆)에서 복사해 채움. 문 앞 돌계단(`Stair_B`·`StoneStair_B`)은 그대로.
  - 밖에서 안 보이는 것 지움(사용자 지시 — 문루 2층 안 공포 안쪽 단청·2층 바닥·천장·지붕 속·기둥 속 등): 장면을 0.1 m 복셀로 막고 위·옆 경계에서 채운 '바깥 공기'를 구한 뒤, 삼각형 표면 점(1,757만 점)에서 64방향 평행 광선 — 빠져나가거나 맞기 전 광선 위 공기가 바깥 공기면 보임(두 면 재질이라 양면 검사, 판은 앞면만). 2단계로 지붕 위 13방향 훑기(기와 사이 머리카락 틈으로 보이는 아래 부재) + 걸을 수 있는 곳 눈높이 1,262곳에서 사방 광선으로 놓친 것 되살림. 삼각형 4,564,419 → 2,894,238(63.4 % 남김, 땅 포함).
  - 받침판(LOD0): 지운 지붕 속이 기와 틈으로 비치지 않게 기와 골 바닥보다 3 cm 아래에 검은 면(`MAT_RoofPlate_Black`) — 기와 바로 아래 0.6 m 안에 남은 부재가 없는 곳만, 테두리에서 0.75 m 안쪽, 옆·아래에서 바깥 공기가 보이는 삼각형은 뺌(두 지붕이 만나는 틈). 부분마다 24~1,764 삼각형.
  - 부분(LODGroup 13개, 데모 좌표로 삼각형 무게중심을 상자에 넣음 — 긴 지붕판은 토막 경계에서 삼각형 단위로 갈림): `HNM_GATE`(문루, x −15.3~14.3 · z −9~9), 행각 9토막 `HNM_HG_SW1·SW2·W1·W2·W3·SE1·SE2·E1·E2`(크기 23.7~30.4 m), `HNM_YHM`(유화문), `HNM_GBC`(기별청), `HNM_GROUND`(땅·길·금천·영제교·석수·난간·쇠창살 — 이름으로). 여는 문짝 27짝은 `Heungnyemun_DoorsCard.prefab`(= `Heungnyemun_Doors` 의 문짝 메시를 상자 판으로, 경첩 피벗·충돌체 유지)으로 빼서 넣음.
  - LOD: LOD0 = 위 보이는 것 + 받침판(약 200 m 까지) / LOD1 = 기와(`MI_HNM_RoofTile`)·막새(`MI_HNM_YeonDan_*`)를 빼고 검은 판(기와 윗면 −3 cm, 0.25 m 격자에서 층을 나눠 만들고 가장자리는 반으로 나눠 찾음) + 나머지 줄임(잔부재 6 cm / 구조재 3 cm, UV 이음매 넘어 접기 허용)(약 500 m 까지) / LOD2 = 같은 판 + 줄임 20 cm / 10 cm + 작은 떨어진 조각 없앰. 끝 화면 높이 0.004. 전환 화면 높이 = 부분 크기 / (거리 × 1.1547), CrossFade. 용마루(`RoofLine`)·양성(`RoofWall`)·잡상(`RoofOrnament`)은 LOD1·2 에도 남김(줄임).
  - 삼각형(측정, LOD0 / LOD1 / LOD2): 문루 804,783 / 123,035 / 49,292 · 행각 토막 143,690~198,493 / 23,062~29,732 / 11,983~14,838(9토막 합 1,454,891 / 231,309 / 117,895 — 이름 순 SW1 147,984 / 24,164 / 13,014, SW2 198,493 / 28,830 / 14,838, W1 150,728 / 23,419 / 11,983, W2 144,050 / 25,278 / 12,809, W3 176,101 / 29,732 / 14,269, SE1 146,456 / 23,062 / 12,108, SE2 197,096 / 28,132 / 14,125, E1 150,293 / 23,562 / 12,106, E2 143,690 / 25,130 / 12,643) · 유화문 410,187 / 62,347 / 23,177 · 기별청 59,714 / 6,261 / 3,177 · 땅·영제교 167,645 / 11,403 / 3,317. 합계 2,897,220 / 434,355 / 196,858 + 문짝 판 324. 원래 데모 씬 19,860,120.
  - 충돌체: 부분마다 LOD0 에 MeshCollider(13) + 문짝 27. 메시 에셋 39개 479 MB(텍스트 저장), 판 텍스처·재질 `Simplified/Cards/`(30종 27.8 MB), 문짝 판 메시 `Simplified/DoorCards/`.
  - 데이터·다시 가져오기: `D:\Blender\Enviroment\PalaceJoseon\FourDoor\Source\Heungnyemun\LOD\`(`meshes.bin` 'HNMS' 형식 = 매직 0x534D4E48, 메시마다 이름·위치·법선·UV·서브메시마다 재질 이름 + 인덱스, Unity 좌표 = 단지 프리팹 루트 좌표 / `doors.bin` 문짝 판(문짝 메시 자기 좌표) / `layout.txt` 부분·LOD·전환 / `cards.tsv` + `Cards\` / `stats.tsv`). 순서: `PalaceLodBinImport.CloseHeavyScene()`(SampleScene 이 저장된 상태일 때만 빈 씬으로) → `ImportMeshes(폴더, "…/Simplified", 12, "", "meshes.bin", "Meshes", 3)` 를 DONE 까지 → `BuildDoorsCard(폴더, …/Simplified, …/Heungnyemun_Doors.prefab, "Heungnyemun_DoorsCard", …/Heungnyemun_Original.prefab)` → `BuildPrefab(폴더, …/Simplified, …/Heungnyemun_Original.prefab)` → `OpenScene("Assets/00.Scenes/SampleScene.unity")`(1분 넘게 걸려 MCP 시간 초과가 나도 열림) → `Place("GyeongBok/흥례문_Heungnyemun", "Heungnyemun_Complex", 프리팹 경로, "Heungnyemun_Simplified")`. 메시 에셋 정점 수는 `PalaceMeshCompact` 가 같은 정점을 합쳐 파일보다 적음(인덱스 수는 같음).
  - 확인: 바깥 18시점(마당 눈높이·문 앞·멀리 위)에서 원본(계단만 뺀 것)과 LOD0 을 그려 비교 — 다른 화소 0.07 % 이하(공포 사이 몇 화소). Unity 캡처(문 앞, 마당 쪽 서쪽 칸, 계단 자리 천장, LOD1·2 강제)로 계단 없음·천장 막힘·검은 판 확인, 콘솔 오류 0. 가까이 모습·실제 프레임·LOD 전환 거리는 사용자 확인 필요.
- 단지별 방향(데모 좌표 기준, 확인한 것): 근정전·사정전·강녕전·교태전·신무문 = +z 북쪽. 흥례문 = +z 남쪽(문이 +z 끝, 영제교 마당이 −z) → 180°. 광화문 문루 데모 = 월대·해태가 +z → 180°. 광화문 단지(부속 건물)·영추문은 긴 축이 데모 z·x 로 놓여 90° 돌렸으나 **어느 쪽이 맞는지는 확인 필요**. 동궁은 270° 돌림(사용자 지시, 2026-09-29), 경회루·건춘문은 회전 없이 둠(확인 필요).

#### 궁터 배치 (SampleScene 루트 `GyeongBok`, 2026-09-26)
- `GyeongBok` = (−2745, 10, 3830)(광화문 문 가운데, 조선 큰섬 남쪽 만 위 평지). 자식 = 건물별 이름 오브젝트(피벗 = 그 건물의 대표 지점, 사용자가 옮겨 맞춤) 아래 `_Baked` 프리팹 인스턴스: `광화문_Gwanghwamun`, `월대_Woldae(1866)`, `해태상_Haetae(1870)`, `광화문단지_부속건물_GwanghwamunComplex`, `흥례문_Heungnyemun`, `근정전_Geunjeongjeon`, `사정전_Sajeongjeon`, `강녕전_Gangnyeongjeon`, `교태전_Gyotaejeon`, `신무문_Sinmumun`, `경회루_Gyeonghoeru`, `동궁_Donggung`, `건춘문_Geonchunmun`, `영추문_Yeongchumun`. 이름은 C# `\uXXXX` 로 넣음(MCP 한글 깨짐 방지).
- 배치는 관람길 지도처럼 중심축(광화문→흥례문→근정전→사정전→강녕전→교태전→신무문)을 남북으로 붙여 쌓고, 경회루는 서쪽, 동궁은 동남쪽, 건춘문 동쪽, 영추문 서쪽. 거리는 단지 바운드를 맞붙인 어림값(실측 아님). 전체 남북 약 810 m(해태 z 3768 ~ 신무문 4581) × 동서 약 640 m. 높이는 모두 y 10(궁터 평지).
- 이후 사용자가 지형(궁터 북쪽 산을 평지로)과 배치를 손봄 — 위 좌표는 처음 배치 값. 현재 구조: `GyeongBok/GwangHwamun` 그룹 아래 광화문·월대·해태·부속단지(사용자가 만듦). 2026-09-26 확인: 광화문·월대·해태 홀더의 자식 위치가 (0,0,0) 이라 문루가 월대에서 약 24 m 북쪽으로 떨어지고 부속단지와 겹침(프리팹 원점 = 데모 원점이라 자식 위치가 −대표점이어야 붙음: 문루 (0,0,29.6) / 월대 (0,0,−0.1) / 해태 (0,0,−32)) — 사용자 확인 필요.

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

### 경복궁 에셋
| 증상 | 원인 | 해결 |
|---|---|---|
| 보임 검사에서 모든 렌더러가 가려짐으로 나옴 | KHS 프리팹 부모에 큰 콜라이더가 붙어 있음 | 렌더러 없는 오브젝트의 콜라이더를 끄고 렌더러마다 MeshCollider |
| 흥례문 지붕이 붉고 얼룩지게 구워짐 | 기와·서까래가 한 덩어리로 많이 줄어 낮은 면이 두 층 사이에 놓임 | 지붕 전용 투사 거리·최소 비율을 키워 다시 구움 |
| 문 단지에 흰·파란 판, 경회루 물이 하얗게 구워짐 | 데모 씬 바닥 `Plane`·물 메시까지 덩어리에 들어감, 물 머티리얼 변환 값 | `Plane*`·`*Water*` 제외, `MI_KHL_Water` = 색 (0.06, 0.11, 0.10)·매끄러움 0.93, 물은 원본을 LOD1 에 넣음 |
| OBJ 로 내보낸 텍스처가 안 붙음 | MTL 의 절대 경로 앞에 OBJ 폴더를 붙이고 `norm` 을 무시하는 임포터 | 받는 쪽에서 MTL 을 직접 읽어 머티리얼을 만듦 |
| AssetPostprocessor 를 추가하자 프로젝트 전체 텍스처·FBX 가 다시 임포트(30분 넘게) | 후처리기 추가 = 임포트 버전 변경 | 새 후처리기는 한가할 때 추가 |
| 모든 구운 단지 원점에 흰 2 m 상자 | 굽기 쪽 빈 장면의 기본 큐브가 같이 구워져 덩어리마다 목부 메시에 붙음 | 굽기 전 장면 오브젝트 삭제 + 기존 것은 `CleanDefaultCube`(큐브 삼각형 뺀 `<메시>_clean.asset` 로 교체, 87개) |
| 광화문 문루가 한 장 아틀라스로 흐리게 구워짐 | 문루가 메시 1개(서브메시 30)라 종류 분류가 오브젝트 이름 기준으로 전부 목부 | 서브메시마다 오브젝트를 나누고 재질별 종류 힌트(창호 힌트는 상자 대체 키워드 "window" 를 피해 "hyeonpan") |
| 머티리얼 셰이더가 안 바뀜(`Unknown shader`) | 머티리얼 변형(Variant)은 부모 셰이더를 따름 | `material.parent = null` 후 변환 |
| 긴 `ExportChunk` 가 여러 번 실행돼 `chunks.tsv` 에 같은 줄이 쌓임 | MCP 시간 초과 재시도 | `chunks.tsv` 에 있는 덩어리는 SKIP, 호출마다 남은 번호부터 몇 개씩 |
| OBJ 가 수백 MB(광화문 문루 700 MB) | 서브메시마다 vt 를 다시 씀 | (UV 배율, 오프셋) 별로 한 번만 |
| 강녕전 문짝이 450짝 | 행각 방문(`_HG_Door`, 폭 0.6~0.9 m)까지 궐문 문짝으로 잡음 | 행각 문은 폭 1.1 m 이상만 |
| 광화문 문짝을 가운데로 가르자 두 짝이 같은 크기 | 두 짝이 한 판(큰 삼각형 몇 개)이라 삼각형 중심으로 못 나눔 | 섬으로 나눠 문판을 찾고 가운데 x 평면으로 삼각형을 잘라 나눔 |
| 흥례문 원본에 메시 없는 오브젝트 | 데모 씬의 `SM_SkySphere`(메시 참조 없음) | 메시 없는 렌더러는 원본에서 뺌 |
| 궁 근처에서 에디터가 매우 느림 | LOD0 전환이 화면 높이 0.25(단지 크기의 약 3.5배 거리, 근정전 약 490 m)라 궁 어디서든 7개 단지 원본 전부(약 9천만 삼각형·3만 렌더러, 전부 그림자)를 그림 | LOD0 를 화면 높이 1.0 으로, 작은 부재 그림자 끔 — 체감은 사용자 확인 필요 |
| 큰 단지 `ImportBakedStep` 뒤 Unity 가 몇 분 응답 없음 | 4096 PNG 수십 장 임포트 | 기다렸다가 다시 호출(이미 된 것은 건너뜀) |
| 기와 지붕이 멀리서 흰 무늬로 반짝임 | 기와 한 장 한 장이 살아 있는 메시 + AA 없음 | 먼 거리만 매끈한 지붕 껍데기 + Main Camera TAA |
| 구운 지붕 아틀라스의 기와가 깨짐 | 서까래·지붕 안쪽(rafter/rooffloor/roofinner/roofwall)까지 이름에 roof 가 있어 지붕으로 분류 | 그 이름은 목부로 분류하고 다시 구움 |
| 단순화판에서 합각(지붕 옆 삼각형 벽)이 뚫려 속이 보임 | 예전 실내 컬링(`CullApply`)이 합각 벽(`SM_GSJ_Gable01`, `SM_MCJ/CCJ_PatternWall*` 등)을 안 보인다고 원본 프리팹에서 지웠음 — 원본 프리팹에서 내보내면 처음부터 없음 | 데모 씬을 열어(`OpenComplex`) 그 렌더러를 다시 내보내 채움. 지운 목록은 `D:\UnityAssetCache\_bake\<단지>_Culled.txt` |
| 합친 메시인데 지붕 안에 들어가면 속이 다 있음 | "합침" = 아틀라스로 구워 한 메시로 묶은 것일 뿐 겉만 남기는 게 아님(원본 단지는 가까이서 원본 그대로) | 밖에서 안 보이는 렌더러를 레이로 찾아 지움(실내 컬링) |
| 창 너머로 실내가 보임 | 창호지가 없거나 매끄러움 0.36~0.5 라 하늘이 비쳐 뚫린 것처럼 보임 | 창호지 판을 덧대고 매끄러움 0.08 |
| 창호지 판이 문 없는 통로를 막음 | 창 후보에 doorframe 까지 들어감 | doorframe·changbang 등 제외 |
| 실내 컬링이 보이는 부재를 지움 | 걸을 수 있는 땅을 바깥 테두리에서만 채움, 행각 기단(+1.01 m)을 못 올라감, 작은 공포 부재를 레이가 놓침 | 하늘이 트인 칸도 시작점, 계단 1.2 m, 가까운 눈 위치 12·35 m 에서 역방향 검사, 문 너머 깊은 곳은 바깥으로 빠져나가는 레이로 검사 |
| 지붕 껍데기 위치가 엉뚱함 | 합친 오브젝트의 변환을 정점에 적용 안 함 | 정점에 월드 변환 적용 |
| 지붕 껍데기에 구멍(얇은 한 겹 지붕) | 두께 없는 면은 복셀로 다시 만들면 사라짐 | 두께를 준 뒤 다시 만듦(경회루 적중률 0.40 → 0.92) |
| 지붕 껍데기 대부분이 불량으로 판정 | 적중률 기준 0.9 가 너무 빡빡함(0.86~0.9 인 멀쩡한 것이 많음) | `cov_env < 0.85` 또는 `cov_high < 0.8` 만 불량 |
| 해태·월대 구운 것에 기본 HDRP 머티리얼 | 한 오브젝트짜리 FBX 는 루트 이름이 `X_Baked` 라 이름으로 못 찾음 | 메시 이름으로 찾음(`RefreshBakedStep`) |
| 단순화 단지 기둥 줄에 흰 창호지 띠가 서 있음 | `AssembleSimplified` 의 Ground(원본 프리팹의 `SM_` 없는 렌더러)에 예전 방 속 비우기·창호지 작업의 한지 막 `ShellSkin`·`WindowPaper` 가 딸려 들어옴 | 조립 때 두 이름은 뺌 |
| 단순화판 지붕 모서리가 분홍색 | 받침판이 쓰는 `MAT_<덩어리>_roofenv.mat` 이 없는 덩어리(사정전 SJJab·SMALL)가 있었음 — 기와를 많이 줄이자 받침판이 드러남 | roofenv 텍스처로 머티리얼을 만듦. LOD2 몸체 재질 `ATLAS_<덩어리>_<종류>` 는 `FindMat` 이 `MAT_<덩어리>_<종류>` 로 찾음 |
| 멀리서 궁 뒤쪽에 분홍 띠(2026-09-28) | 강녕전 `GNJ_LOD0_LOD` 의 LOD2 몸체 `GNJ_LOD2_Body` 머티리얼 3개가 null — `FindMat` 에 `ATLAS_`→`MAT_` 규칙을 넣기 전에 만든 프리팹이라 그대로 남아 있었음. 사정전 `SJJ_LOD2_RoofPlate` 는 서브메시 4개에 머티리얼 1개 | GNJ = `MAT_GNJa_stone/win/wood` 로, SJJ 판 = `BlackRoof` 4개로 프리팹에서 바로 채움. 씬 `GyeongBok` 전체 렌더러(꺼진 것 포함) 검사 결과 null·서브메시 부족 0 |
| 단순화판 기와 위에 초록 얼룩(강녕전 본채 지붕 끝) | 받침판에 구운 roofenv 텍스처를 썼는데 지붕 끝에 처마 단청(초록)이 구워져 있어 기와 틈으로 비침 | 받침판 머티리얼을 어두운 단색(`BlackRoof`)으로(강녕전·사정전 프리팹 전부 교체) |
| 단지 안에 서 있으면 30~70 m 떨어진 기와도 지글거림 | LOD 가 단지 하나에 하나(거리는 단지 중심에서) — 단지 안은 늘 원본 | 지붕마다(16 m 칸) LOD |
| 방 속 비우기 뒤 사정전 지붕에 구멍 | 방 위 기와 중 "아래를 보는 면"을 지우고 먼 시점 그림(256 px)으로 윗면 여부를 판정 — 이 에셋 기와판은 법선이 아래인 채 양면으로 그려지는 것이 많고, 그림이 기와 조각을 놓침 | 데모 씬에서 기와를 되돌림. 판정은 삼각형에서 위로 레이(하늘로 빠지면 남김)로 바꿈 |
| 위로 쏜 레이가 모든 기와에서 제자리에 걸림 | 원본 프리팹에 렌더러 없는 충돌 메시(`BP_*`)가 기와와 겹쳐 있음 | 레이 계산 전에 렌더러 없는 콜라이더를 끔 |
| 새로 만든 메시 에셋이 수 GB(사정전 속 비운 메시 1.86 GB, 지붕 칸 800 MB) | 삼각형마다 정점을 따로 넣음(용접 안 함) + 프로젝트 직렬화가 ForceText 라 메시가 글자로 저장 | 원래 정점 번호로 합치고 `PalaceMeshCompact`(법선·접선 16비트, 2번 UV 뺌), 작은 기와는 메시 복사 대신 같은 메시를 쓰는 오브젝트로 — 지붕 칸 정점 765만 → 48만. 사정전 속 비운 메시는 아직 안 줄임 |
| Unity 가 꺼짐(`MemoryManager::OutOfMemoryError`, Unity 14.8 GB) | 큰 단지 속 비우기 계산 + SampleScene(궁 전체) 동시 로드. 2026-09-30 흥례문 메시 저장(`AssetDatabase.CreateAsset`) 중에도 같은 일(그래픽 메모리 14.5 GB, `System out of memory`) | 작업 중엔 SampleScene 을 닫고(`PalaceLodBinImport.CloseHeavyScene` — 닫으면 약 9 GB → 2 GB), 메시를 줄여서 몇 개씩 저장 |
| 문짝 판 30짝 중 1짝이 안 바뀜(근정전 `BuildDoorsCard`) | `Bounds.Expand(float)` 는 **크기**를 늘림(한쪽은 절반) — 0.1 이면 한쪽 5 cm 라 문짝 두께 끝에 놓인 판이 밖으로 나감 | `Expand(0.4f)` |
| 근정문 옆 계단 밑 쪽문 문짝에 흰 삼각형 빈칸(2026-09-28) | 문짝 판(`SM_GJM_Door02A_Card`)을 원래 문짝의 바운드로 만들었는데, 이 문짝은 빗장이 옆 문짝 쪽으로 약 0.22 m 튀어나와 판 폭이 0.74 m 로 잡힘 — 빗장 밖 부분은 구울 때 맞은 게 없어 배경색(밝은 회색)으로 남음 | `Geunjeongjeon_DoorCards.asset` 에서 02A 판 폭을 문짝 몸통(0.52 m, UV v 0.299~1)으로 줄이고 02A·02B 위아래 배경 띠(각 약 4 cm, UV u 2.3 %)도 잘라냄. 근정전 다른 문짝 판 그림은 배경 5 % 이하(측정) |
| 메시 에셋을 만들었는데 이름으로 첫 메시를 못 찾음 | `AssetDatabase.CreateAsset` 로 만든 첫 메시(주 에셋)의 이름은 파일 이름으로 바뀜(`KHL_Main` → `KHL_Simplified_Meshes`) | 첫 메시는 파일 이름으로 찾음 |
| 굽기 여러 개를 동시에 돌리면 멈춤(rc 3221225477 등) | PC 메모리 약 85 % 사용 중(Unity 약 12 GB) + GPU 메모리 부족 | 동시에 2개까지, 큰 덩어리(교태전 GTJ)는 CPU 로 구움 |
| LODGroup 기준점·크기를 넣었는데 저장된 값이 원래대로 | `LODGroup.SetLODs` 가 경계를 다시 계산함 | `SetLODs` 를 부른 **뒤에** `localReferencePoint`·`size` 를 넣음 |
| 광화문 멀리용 단청·장식 메시의 경계가 문루 앞 27 m 까지 늘어남 | 외부에서 줄인 창(Win01A) 메시에 정점이 30 m 튄 뾰족한 삼각형 13개 | 원래 표면에서 먼 삼각형을 거리로 찾아 확인하고, 그 재질은 원본 창을 씀 |
| 지붕 껍데기 LOD 에서 광화문 잡상이 안 보임 | 껍데기(roofenv)가 원래 기와 면보다 위에 있음(측정: 중앙값 0.29 m, 5~95 % 0.10~0.59 m) — 잡상(높이 약 0.3 m)이 묻힘 | 잡상 섬 88개를 껍데기 윗면까지 0.07~0.24 m 올린 사본을 LOD1·2 에 씀 |
| 판 문짝 두 짝이 맞닿는 곳에 흰 줄(빈틈) | 쇠장식·고리가 문짝 밖으로 튀어나와 경계 상자가 문짝보다 커서 판 가장자리에 투명 띠가 생기고, 밉맵에서 알파가 번져 넓어짐 | 가장자리 띠(칸·줄의 불투명 비율 8 % 미만, 최대 8 cm)는 잘라 판을 줄이고, 2.5 cm 이하 빈 곳은 가장자리 색으로 채움 |
| 강녕전 행각 바깥 창의 창살이 흰 판으로 덮임 | 틀만 있는 메시(`Door03`)를 판으로 구우며 틀 안 빈 곳을 창호지로 채움 — 단순화판은 창살 판이 이미 뒤에 따로 있음 | 단순화판에서는 맞은 곳만 불투명으로 굽기 |
| 동궁 문 재질(`MI_DG_Door01A`) 조각을 판으로 바꾸니 납작함 | 그 건물들에서 그 재질은 문짝이 아니라 3 cm 막대·꼭지에 쓰임(조각 3×3×56 cm) | 판으로 바꾸기 전에 조각 크기를 보고 뺌 |
| 여러 조각을 모은 판 아틀라스가 4096×20157 | 선반 배치 폭을 한 가지로만 정함 | 폭 후보 중 넓이가 가장 작은 것, 4096 을 넘으면 넓이 기준으로 줄임(강녕전 행각 문 56조각 0.49 배) |
| 향오문 문짝 하나가 판으로 안 바뀜 | 돌쩌귀·고리 때문에 경계 상자 두께 0.48 m 라 '입체 덩어리'로 판정 | 여닫는 문짝(상자 판)은 두께 검사 안 함 |
| 밖에서 보이는 것만 남기자 기와 사이 머리카락 틈으로 하늘·땅이 비침(흥례문) | 기와판 사이 틈(수 mm)은 표면에서 쏜 64방향 광선에 거의 안 걸려, 틈 아래 지붕 속 부재가 '안 보임'으로 지워짐 | 지붕 위 13방향 평행 광선으로 틈 아래 부재를 되살리고, 지운 지붕 속 위에는 기와 골 바닥 3 cm 아래 검은 받침판 |
| 받침판이 두 지붕이 만나는 곳에서 검은 조각으로 보임 | 받침판을 기와 장 테두리 가까이까지 깔면 문루 아래 지붕 끝과 행각 지붕 사이 틈으로 옆에서 보임 | 옆·아래 방향 광선에 바깥 공기가 보이는 받침판 삼각형은 뺌 |
| 먼 LOD 에서 공포가 거의 안 줄어듦(허용 오차 20 cm 에도 문루 12.7만) | 공포 부재 면마다 UV 가 따로라 이음매에서 접기가 막힘(위치만 같으면 합쳐 봐도 오히려 14.7만으로 늘어남) | meshoptimizer `SimplifyPermissive`(이음매 넘어 접기 허용, UV 가중 낮춤) — 문루 LOD2 4.9만 |
| 장치로 보낸 PNG 의 md5 가 원본과 다름 | 전송 과정에서 PNG 에 `caBX` 덩어리(5,758 바이트)가 붙음 — 픽셀(IHDR·IDAT)은 같음 | PNG 는 IHDR·IDAT 만 해시해 비교 |

## 7. Unity MCP 로 작업할 때

- `execute_code` 는 CodeDom(C# 6): `UnityEngine.Object.` 로 한정, 문자열 보간·`out var` 불가. `LightUnit` 은 `UnityEngine.Rendering.LightUnit`, 세기는 `HDAdditionalLightData.SetIntensity(v, LightUnit.X)`. Volume 파라미터 값은 리플렉션으로 `value` 읽기. `HDMaterial` 접근 불가 → `SetFloat` + `EnableKeyword`.
- **같은 코드가 여러 번 실행될 수 있음** → 만들기 코드는 이름으로 이미 있는지 확인(멱등). 응답 대기 약 30 s 를 넘기면 클라이언트가 같은 호출을 다시 보냄 → 오래 걸리는 일은 시간 제한을 두고 나눠 호출(예: `ImportBakedStep`).
- MCP 에서 건 `EditorApplication.delayCall` 이 실행되지 않았음(원인 확인 필요). `File.Delete`·단수형 `AssetDatabase.MoveAssetToTrash`·`while(true)` 는 안전 검사에 막힘 → 복수형 `MoveAssetsToTrash`.
- 한글을 보내면 일부 음절이 깨지는 일이 있었음(원인 확인 필요) → 저장 후 U+FFFD·문구를 확인.
- 스크립트 폴더 전체를 한 번에 읽으면 출력 한도 초과 — 나눠 읽기.
- 캡처: `manage_camera` 위치 지정 캡처엔 HDRP 물이 안 그려짐(예전 기록 — 2026-09-26 y 3000 캡처에서는 바다가 보였음, 확인 필요). Play 캡처는 Main Camera 를 옮긴 뒤 다음 호출에서(모션 블러), PlayerOrbitCamera 는 꺼 둘 것. 편집 모드 파티클은 `ps.Simulate(...)` 로 강제 시뮬. 편집 모드 `SampleAnimation` 직후 스키닝은 한 번 늦음.
- 에디터 자세 샘플링은 `AnimationMode.Start/SampleAnimationClip/Stop`(씬 dirty 안 됨) 또는 `AnimationClip.SampleAnimation`(PlayableGraph 는 휴머노이드 자세가 깨짐).
- 일시정지 상태로 Play 를 끄면 다음 Play 가 멈춘 채 시작 → 끄기 전 `isPaused = false`.
- 자동 시험(`CombatAutoTest`)을 쓸 땐 PlayerCombatController 를 끈다(매 프레임 막기 입력을 덮어씀).

## 8. 확정된 설계 결정

- 진영 3개, 적 판정은 `ShipFaction` 한 곳. 포탄은 `ownerFaction` 을 반드시 가짐.
- 배 이동은 전부 힘 기반, 축 분담(높이·기울기 = ShipBuoyancy, Y 회전·XZ = 이동 스크립트).
- `EnemyShipBase` / `NavyShipBase` / `MoveControl` 은 공용 베이스로 합치지 않음.
- 씬의 원본 배 오브젝트가 스포너 템플릿(프리팹 참조 아님).
- 날씨 프리셋은 `WeatherSystem` 인스펙터 인라인 리스트.
- 현상금 대신 우호도 게이지 하나(관군 ↔ 일본). 중립이면 두 진영 모두 플레이어를 공격하지 않음. 섬 근처(영해)는 그 섬 진영 배만, 육지 가까이는 정박선만.
- 근접전은 세키로식(튕겨내기 + 체간 + 결정타), **배에서 내린 상태에서만**. 공중제비·구르기류 동작은 넣지 않음(치마 속 노출). 결정타 쌍 애니메이션은 쓰지 않음.
- 두 번째 콤보는 별도 키 없이 좌클릭 콤보에 자동으로 섞음. 강베기는 좌클릭 꾹. 칼은 자동으로 뽑고 넣음.
- 걷는 캐릭터 카메라는 배 카메라와 같은 조작(마우스만 움직이면 돎).
- 하차 중 배는 정박(`SetDocked`) — 조종 스크립트를 끄지 않음.
- 조선 큰섬의 궁(경복궁)은 **인종·명종 시대(1544~1567)** 기준 — 고종 중건(1865~) 이후 새로 생긴 건물(집옥재·건청궁·향원정·자경전 등)은 넣지 않음(사용자 지시). 근정전 일곽을 포함해 궁 전체 에셋을 먼저 들여온 뒤 최적화함.
- 경복궁: **광화문(문루·월대·해태)·광화문 부속단지·흥례문·근정전·경회루·강녕전·사정전은 단지 전체(행각·담장 포함) 원본** — 플레이어가 언제든 가는 곳(사용자 지시, 2026-09-26; 처음엔 근정전·경회루·강녕전 건물만이었음). 사정전 실내 소품은 넣지 않음. 나머지(교태전·동궁·건춘문·영추문·신무문)는 겉모습만(구운 것). 멀리서는 모두 구운 것(LOD, 사용자 선택). **모든 궐문 문짝은 분리**(열 수 있게). 같은 텍스처·머티리얼은 `_Shared` 한 곳으로 합침.
- 경복궁 최적화(사용자 지시, 2026-09-27): 실내를 남기는 건 **근정전·경회루만**(나머지는 밖에서 안 보이는 것 지움). 모든 창은 불투명 창호지, **창살은 메시 대신 구운 판(카드)**. 지붕은 **먼 거리만 매끈한 껍데기**, 가까이는 원본 기와. **경회루 2층은 문 없이 트임**. 행각 끝에서 기와가 잘린 모양은 원본 데모 씬도 같아 그대로 둠(원복).
- 동궁은 내부 진입 없음. 안쪽을 지우고 판으로 가리는 방식은 벽이 사라져 사용자가 취소 — 건물별로 나눠 모양을 유지하는 만큼만 줄임(사용자 지시, 2026-09-29). 경회루·동궁 홀더는 켜 둔다(사용자 지시).
- 경회루 연못 담장·담장 문(3곳)은 넣지 않음 — 고종 대 건립이라(사용자 확인·지시, 2026-09-28). 연못 다리는 남김. Unity `Gyeonghoeru_Simplified` 에서도 뺌(2026-09-29).
- 광화문 해태상(1870)·월대(1866)는 인종·명종대에 없던 것이라 별도 오브젝트로 분리(사용자 지시: 없었다면 분리). **해태상·월대 모두 고증에 안 맞아 단순화판에서 뺌 — 광화문은 문루(양옆 궁장 포함)만 단순화**(사용자 지시, 2026-09-29). 궁 배치는 건물별 이름 오브젝트 + 부모 `GyeongBok`, 정확한 위치는 사용자가 맞춤. 진입 막기(콜라이더)는 다 만든 뒤 사용자가 함.

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
- [ ] 새 섬: 안전지대·적 스폰 규칙을 섬별로 정해야 함(지금은 base 만). 섬을 옮기거나 새로 만들면 `WaterShoreMask` 범위·다시 굽기. 섬 지형은 첫 생성본 — 마을·궁·성 배치하며 다듬기. APV·리플렉션 프로브 범위 밖(최종 베이크 때 확인 필요).
- [ ] 경복궁: 광화문 문루·월대 사이 틈(§4 궁터 배치) 확인. 광화문 부속단지·영추문·동궁·경회루·건춘문 방향 확인. 흥례문 바닥판 확인. 진입 못 하는 건물 막기(콜라이더 — 사용자가 나중에). 문 여는 스크립트 없음(피벗만 있음), 문짝 경첩 쪽·여는 방향 확인. 원본 단지 실내 조명 없음(데모 조명 안 가져옴). 원본 LOD0 가 무거움(단지 하나 2천만 삼각형·1만 렌더러대) — 성능 실측 필요, 더 줄이려면 GPU Resident Drawer(같은 메시 반복이 많음)·작은 부재 거리 컬링 검토. 데칼(`_DC`, 현판 글씨) 미처리.
- [ ] 광화문과 다른 궐문 잇기(궁장): 에셋 "KCISA - Korean Traditional Spline Builder"(1.8 GB, 수원 화성·화성행궁 기반)를 받아 둠(2026-09-30, 아직 안 가져옴). 에셋 의존성 `com.unity.splines@2.5.2` — 프로젝트엔 Splines 없음(manifest·packages-lock 확인), Unity 문서상 6000.3 용 출시 버전은 2.9.1. 스토어 표기 렌더 파이프라인 호환은 Built-in 만(URP·HDRP 아님) → 가져오면 HDRP 머티리얼 변환 필요. 화성(1794~1796) 성벽은 여장이 있는 성곽이라 궁장과 모양이 다름 — 쓸 부품은 확인 필요.
- [ ] 경복궁 정리: 원본 단지 폴더의 안 쓰는 텍스처(R·MT·AO 등)·FBX 추출 머티리얼·데모 씬·URP 설정 애셋 정리, 옛 파일(`*_clean.asset`, 예전 `_roof` 텍스처, `out_prev*`) 정리, 먼 거리용 LOD2/HLOD, 텍스처 메모리·스트리밍 실측 — 아직 안 함.
- [ ] 근정전 단순화판: 내부에 들어가면 내부를 켜고 외부(전각·월대)를 LOD1 로 바꾸는 스크립트 없음(사용자 계획). 행각 토막 LOD 전환 거리·실제 프레임 확인 필요. 근정문 계단 밑 창호 판 두 장(Door02A/B 옆)이 같은 면에 겹쳐 흰 줄이 보일 수 있음(확인 필요). 행각 옆 마당에 터레인 풀이 바닥 위로 올라온 곳이 있음(캡처로 봄).
- [ ] 동궁 단순화판: 충돌체 없음, LOD1 전환(약 50 m) 모습·전각 지붕은 LOD1 에서도 안 줄어듦, 새로 잡은 문 판 26종 확인 필요. 위치 — 실제 배치도에서는 근정전 동쪽 행각 바로 옆인데 지금은 근정전 동쪽 담에서 약 78 m 떨어져 있음(옮길지 사용자 결정).
- [ ] 경회루 단순화판: 연못 자리 터레인을 파야 물·석축이 보임(확인 필요). 1·2층 천장·난간 판의 가까이 모습은 사용자 확인 필요.
- [ ] 광화문 문루 단순화판: 멀리서 기와 지글거림(움직이며)·LOD 전환(60 / 150 m)·실제 프레임 사용자 확인 필요. 월대·해태는 단순화판에서 뺐지만 씬의 옛 `GwanghwamunGate_Woldae`·`_Haetae` 는 켜진 채(끌지는 사용자 결정).
- [ ] 경복궁 단순화판: 강녕전·사정전·근정전·경회루·동궁·교태전·광화문 문루·흥례문 적용 — 나머지(광화문 부속단지·건춘문·영추문·신무문)는 줄인 메시(`reduced.bin`)와 판 데이터(`cards.bin`, §4)만 있고 Unity 에 안 올림(사용자 지시 대기). 흥례문 방식(보임 검사 → 부분 나누기 → LOD → `PalaceLodBinImport`, §4)을 그대로 쓸 수 있음. 교태전 단순화판은 먼 거리 LOD 없음. 두 단지 모두 가까이 모습(기와 틈·받침판 가장자리·한쪽 면 처리로 방 안이 보이는지)·LOD 전환 거리·실제 프레임 사용자 확인 필요. 옛 방식 산출물(사정전 `_Shell.asset` 1.86 GB, `<단지>_RoofLOD`) 정리 여부 결정.
- [ ] 흥례문 단순화판(§4, 2026-09-30): 가까이 모습·실제 프레임·LOD 전환 거리(약 200 / 500 m)·검은 판 경계 사용자 확인 필요. LOD0 메시가 각진 음영이라 정점이 많음(LOD0 합 5,425,475 정점, 파일 기준) — 줄일지 결정. 충돌체는 LOD0 전체 메시(부분 13개)라 무거울 수 있음 — 진입 막기와 함께 사용자가 정리. 문루 서쪽 칸 계단 자리 옆 행각 사이로 하늘이 보이는 틈은 원래 모델에도 있음(계단이 가리고 있었음). 씬에서 끈 `Heungnyemun_Complex` 정리 여부 결정.
- [ ] 문·문 장식 판 교체(§4, 2026-09-30): 가까이 모습(옆에서 보면 납작함, 문 장식 판이 문 앞에 뜸)·실제 프레임 사용자 확인 필요. 광화문 가운데 문짝(`GHM_GateLeaf2/3`) 판 아래쪽에 몇 mm 투명 줄(원래 형상의 틈)이 있어 비쳐 보이는지 확인 필요. 새 메시 에셋 209 MB(원래 메시는 원래 에셋·FBX 안에 그대로 남아 있음 — 정리 여부 결정).
- [ ] 경복궁 최적화 뒤: 창살 카드 가까이 모습·지붕 껍데기 전환 거리·실내 컬링 누락(몇 픽셀짜리 틈) 사용자 확인 필요. 사정전 행각 끝 잘린 기와 막기(원하면). 문 여는 스크립트 없음. 실제 프레임 성능 측정 안 함. PC 메모리 사용률 높음(약 85 %).
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
