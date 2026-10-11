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
- **개발 중단(2026-10-11, 사용자 결정)**: "이것으로는 출시할 수 없고 만들고자 했던 것이 망가지는 기분"이라 개발을 멈추고 포트폴리오로 정리함. 자료 = `E:\PortPolio\unity\Haesin`(설명 텍스트 7개 + `screenshots\` 인게임 캡처 32장, 원본 1920×1080 은 `Captures/portfolio/`). 캡처 일부는 연출(적 배 옮김·자유 카메라·NPC 한 줄 세우기 등 — Play 중에만 바꿈, 씬 변경 없음).

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
  03.Weather/             WeatherSystem, WeatherPreset, WeatherTestKeys, FlagWind, LightningStorm, DayNightCycle(밤낮 — §3 밤낮)
  04.Faction/             ShipFaction (+ enum Faction — 왜구 Wokou 포함), ReputationSystem(현상금 — 이름은 예전 우호도 그대로), FactionTerritory
  05.Damage/              ShipHealth, ShipBurnVisual, PlayerHealthUI, DamageFXDistanceCull, BallisticAim
  06.Merchant/            MerchantAI
  07.Environment/         TorchFlicker, RespawnPoint, BaseSafeZone, HarborMooring(섬 선착장 앞바다 배 — 도는 원 찾기·복제), HarborPatrol(그 배가 원을 천천히 돎 — §4 섬 선착장), PalaceWallPath(궁장 깔기·광화문 날개 끝 자르기 — §4 궁장), PalaceGate(궁 문 E 3초 열고 닫기 — §4 문 열기), FerryCrossing(호수 나룻배 — E 3초로 타고 40초 건너기, 타는 동안 측면 고정 카메라 — §4 호수·나룻배), PalaceVista(궁궐을 평소엔 끄고 마을 시점 그림 한 장으로, 나룻배로 육조거리에 갈 때만 켬 — §4 궁궐 원경 그림)
  08.Character/           PlayerCharacter, LandingPoint, OnFootController, BoardingSystem, FootIK, PlayerOrbitCamera(+ICharacterMover), BowString(활시위·화살 표시 — §5 조선 수군 모델),
                          CharacterOutfit, SpringBoneSystem, SkirtLegFollow, EyeBlink, FacialExpression,
                          CharacterTestController·TestCircleWalker(CharacterTest 전용)
  09.Crew/                ShipCrew(선원 목록 + 표적 잡기), CrewMember(선원 한 명 — 사격·쓰러짐), CrewProjectiles(화살·조총 연출과 명중 처리, 씬 `CrewCombatFX`)(§3 선원)
  10.UI/                  MapSystem(+MapSystem.Atlas.cs), UIKit, GameMenuPause, ShowcaseViewer, ShipMenu, InventoryMenu, MenuArt, ResourceBar, UITooltip, ReputationGaugeUI(현상금 투구 아이콘 — 이름은 예전 게이지 그대로)
  11.Audio/               ShipAudio, SeaAmbientAudio, OneShotAudio, BulletAudio, SwordAudio, FootstepAudio
  12.Economy/             PlayerResources, ShipUpgrades
  14.NPC/                 VillagePopulation(마을마다 가까이 가면 사람 생성·멀어지면 끔), Villager(NavMesh 로 돌아다님, 플레이어 칼에 쓰러짐)(§4 마을 사람)
  15.Soldier/             SoldierAI(육상 병사 한 명 — 경비·순찰, 먼저 공격받으면 덤빔), SoldierGroup(무리 — 보이기·알리기·되살리기), PlayerLandDeath(걷다 쓰러지면 배에서 되살림·체력 회복), LandCombatHUD(메인 씬 칼싸움 HUD)(§4 병사)
  16.Quest/               QuestData(퀘스트 에셋 SO + 대사·목표·동작 타입), QuestManager(진행), QuestUI(추적·알림·자막·대화창·화면 어둡게), QuestNPC(F 대화 상대), QuestMarker(도착 장소), QuestTag(퀘스트가 켜고 끄는 오브젝트 표), QuestWalker(미행 대상 걷기)(§3 퀘스트)
  13.Combat/              AttackData, CombatStats, SwordCombatant, PlayerCombatController, EnemySwordAI,
                          CombatHUD, WeaponHolder, HwandoGripIK, CombatAutoTest(테스트 전용)
  Editor/                 ARPGFabImportPostprocessor, BlinkBlendShapeNormals, IslandTerrainGenerator(+ `GenerateOne(key)` — 섬 하나만, 왜구섬 §4),
                          DayNightSetup(밤낮 준비 `Build(forceStars)` — 별 큐브맵·SkyAndFog Profile 에 PBS·DayNightCycle·달, §3 밤낮),
                          QuestAct1Builder(1막 퀘스트 에셋 `Build()`), QuestStoryBuilder(2막~에필로그 퀘스트 에셋 `Build()` — 1막도 다시 만들고 1-4 → 2-1 을 이음), QuestSceneSetup(2막~에필로그 마커·NPC·건물·불·병사 씬 배치 `Build()`) — 셋 다 다시 부르면 덮어씀(§3 퀘스트),
                          SoldierPrefabTool(육상 병사 프리팹·관군 재질 만들기 — §4 병사),
                          SwordDrawClipTool(해신 칼 뽑기·넣기 클립 생성 — §5 환도),
                          PalaceBakeTool, PalaceBakedImportPostprocessor, PalaceLodImport, PalaceGJImport,
                          PalaceOptimizeTool, PalaceRoofLodTool, PalaceShellTool(+ HaesinTriVis.shader — 보이는 삼각형 표시),
                          PalaceMeshCompact, PalaceBlenderRoundTrip,
                          PalaceOrigExport(데모 씬 원본을 부분별 OBJ + tex_map.txt 로 내보냄 — 에셋·씬은 안 바꿈),
                          PalaceBinExport(데모 씬을 고유 메시 + 배치 목록 이진 파일로 내보냄 — 에셋·씬은 안 바꿈),
                          PalaceCardTool(단순화판에 남은 문·문 장식·쇠문·홍살을 판으로 — 대상 내보내기·에셋 만들기·프리팹 교체·확인)(경복궁 §4)
                          PalaceLodBinImport(밖에서 만든 메시 묶음 → 메시 에셋·부분별 LODGroup 프리팹·여는 문짝 판·씬 배치 — 흥례문·건춘문·영추문·신무문 단순화판, `COPYFAR` = 다른 프리팹의 구운 덩어리 먼 단계 복사, `COPYFARBOX` = 그것을 상자 안 삼각형만 잘라 새 메시로)(경복궁 §4)
                          PalaceImpostorTool(지붕 모양 상자 임포스터 — 페이지 그림 가져오기 설정·재질·단지 프리팹 `Setup`, 묶음 프리팹 `BuildKit`, 빈터 배치 `PlaceFill`(배율·거울 열, 땅 NaN = 발밑 터레인))(경복궁 §4 임포스터)
                          PerfProbe(플레이 중 성능 재기 — 프로파일러 카운터(CPU·GPU 프레임 시간, 삼각형, 드로우콜, 그림자 물체, 그래픽 메모리)를 모아 `Begin`·`Mark`·`Report(n)`·`End`, `AddMarkers("카테고리:이름,…")` 로 프로파일러 표시 시간도(Begin 뒤에), MCP 에서 부름)
                          NpcModelSetupTool(퀘스트 NPC 모델 가져오기 — `Setup(이름)` 텍스처·HDRP 재질 2개(Skin 한 면·Cloth 양면)·Humanoid·AddRemap, `MakePrefab(이름)` LODGroup 프리팹 변형, `Capture(이름)` 기본 자세·걷기 LOD0/1/2 BakeMesh 그림 → `Captures/npc/<이름>_lods.png`)(§5 퀘스트 NPC 모델)
                          PalaceYukjoTool(육조거리 — 행랑 조각 프리팹 `BuildRowModules`, ㄱ자 모서리·관아 둘레 껍데기 `BuildCornerRing`, 관아 안쪽 `BuildRest`; 옛 `BuildGwana*` 는 안 씀)(§4 육조거리)
                          SkirtMeshClothTool(해신 치마를 Magica MeshCloth 로 — 고정/움직임 그림 `MakePaintMap`, 설정·엉덩이·발 충돌체 `Apply`, 메뉴 HaeSin/치마 MeshCloth 적용)(§5 옷·흔들림)
                          MeshNormalFixTool(정점 법선 다시 계산 — 같은 자리 정점끼리 각도 안 면만 넓이 평균(자동 스무딩) → 새 메시 `NormalFix/<이름>_N.asset`, 프리팹 참조 교체 `ApplyToPrefab`, 어긋남 비율 `BadPercent`)(§4 광화문 문루 단순화판)
                          PalaceVistaTool(궁궐 원경 그림 — 마을 눈 위치에서 궁만 HDRP AOV 로 기본색·노멀을 찍고 판 메시를 만듦 `Capture`, AOV 값 확인 `ProbeAOV`)(§4 궁궐 원경 그림)
  98.Blender/             궁궐 단순화·판 굽기에 쓴 외부 처리 스크립트(파이썬·C, 사용자 지정 폴더 — Unity 에서 컴파일 안 됨, C 소스는 `.c.txt`): Simplify/(원형 유지 감면), Cards/(판 굽기), Bake/(아틀라스·지붕 껍데기 굽기), LOD/(강녕전·사정전 LOD 보조), Heungnyemun/(흥례문 보임 검사·LOD·판), Gates/(건춘문·영추문·신무문 LOD0·문짝 판 묶기 — Heungnyemun/ 의 load·export·scene 을 씀), Gungjang/(궁장 반복 부품 만들기·확인 그림), Impostor/(지붕 모양 상자 임포스터 — `impcx.py` 단지 통째 굽기(`hullgen.py` 껍데기 + `bake.py` 함수), `fillplan.py` 빈터 묶음 배치 계획, `render_imp.py`·`render_shape.py` 비교 그림, `hull.py` 는 첫 한 채 시험용 — Simplify/·Heungnyemun/ 을 씀; `jp_lod.py` 일본 마을 집 7종 LOD1·LOD2 만들기(§4 일본 마을), `jp_cmp_lod.py` LOD0/1/2 비교 그림, `jc_lod.py`·`jc_cmp_lod.py` 일본성 LOD, `jc_lod2.py` 천수·망루 LOD2 = LOD1 더 줄이기(§4 일본성), `shrine_ishi.py`·`shrine_ishi_bake.py` 신사 돌 축대(§4 일본섬 지형)), Ganuidae/(간의대 모델 `build_ganuidae.py` → FBX, 돌난간 판 그림 `railtex.py`(+`railsrc.py`·`railrender.py`, Impostor/ 를 씀), 기구 `build_instruments.py` → FBX, 저삼각형 도형·지붕·FBX 내보내기 도우미 `lowpoly.py`, 정자·기구·연잎 그림 `maketex.py`), Chwirojeong/(취로정 연못·정자 `build_chwirojeong.py` → FBX — Ganuidae/lowpoly.py 를 씀), Yukjo/(육조거리 길 곧게 `roads.py`), Impostor/yj_*(육조거리 — `yj_build.py` 관아 임포스터·행랑 껍데기 굽기, `yj_row.py` 행랑 조각, `yj_corner.py` ㄱ자 모서리, `yj_plan3.py` 배치·관아 둘레 껍데기, `yjr_view.py`·`yjc_view.py`·`yj_view.py`·`yj_cmp.py`·`view_src.py` 확인 그림; `yj_plan.py`·`yj_plan2.py` 는 옛 배치)
02.Resources/00.Component/
  NewShip/               anteck, panok, seki 프리팹. `Ship/fbx_file_render/WokouMaterial/` = 왜구 배 돛·깃발 재질(원래 돛·깃발 재질 복사 + 어두운 BaseColor)
  02.Water/              ShipRipple.prefab
  ShipCloth/             깃발(Cloth) 포함 배 모델 프리팹
  06.Character/Haesin/   캐릭터 FBX·머티리얼·텍스처·애니메이션(§5)
  06.Character/Shogun/   일본 장군 NPC 모델(Tripo) — Shogun.fbx(LOD 3단계 한 뼈대) · Shogun.prefab · 텍스처 · Materials/(§5 일본 장군 모델)
  06.Character/JPSoldier/ 일본 병사(아시가루) NPC 모델(Tripo) — JPSoldier.fbx · JPSoldier.prefab · 텍스처·마스크 · Materials/ · Animations/(조총 사격)(§5 일본 병사 모델)
  06.Character/GwanGun/   관군(빨간 옷) 텍스처 T_GwanGun_LOD0~2_BaseColor.jpg(수군 텍스처의 파랑 → 진홍) · Materials/M_GwanGun_LOD0~2(§4 병사)
  06.Character/Soldiers/  육상 병사 프리팹 GwanGun_Soldier · JPSoldier_Sword · Shogun_Sword · Wokou_Boss · Wokou_Sword(왜구 병사 모델) · PalaceGuard_Sword · Kurose_Armor_Sword(SoldierPrefabTool 이 만듦, §4 병사·왜구섬·§3 퀘스트)
  06.Character/WokouBoss/ 왜구 두목 — 일본 장수 텍스처의 붉은 칠을 검게 바꾼 `T_WokouBoss_LOD0~2_BaseColor.jpg` · Materials/`M_WokouBoss_LOD0~2`(§4 왜구섬)
  06.Character/JoseonNavy/ 조선 수군 병사 NPC 모델(Tripo) — JoseonNavy.fbx · JoseonNavy.prefab · 텍스처 · Materials/ · Animations/(활 쏘기)(§5 조선 수군 모델)
  06.Character/Citizen/   시민 NPC 모델(Tripo) — HanbokMale/·HanbokFemale/·KimonoMale/·KimonoFemale/ 마다 <이름>.fbx · <이름>.prefab · 텍스처 · Materials/(§5 시민 모델)
  06.Character/(공용 컨트롤러) JPSoldier/Animations/`Crew_Matchlock.controller`, JoseonNavy/Animations/`Crew_Bow.controller`(선원 — 해적도 씀), Citizen/Animations/`Villager.controller`(마을 사람)
  06.Character/Pirate/    판옥선 해적(선원) NPC 모델(Tripo) — Pirate.fbx · Pirate.prefab · 텍스처 · Materials/ · Animations/(활 쏘기 컨트롤러)(§5 해적 모델)
  06.Character/OldMan/    나룻배 사공 노인 — HanbokOldMan.fbx(LOD 3 + 노, 노 젓기·대기 클립) · HanbokOldMan.prefab · Textures/ · Materials/ · Animations/OldMan_Ferry.controller(§5 사공 노인 모델)
  06.Character/HanyangAlly/ 한양 조력자 NPC(홍단령·사모) — HanyangAlly.fbx(LOD 3, 한 뼈대) · HanyangAlly.prefab · Textures/(4096 아틀라스 3장, 세 LOD 가 같이 씀) · Materials/(§5 한양 조력자 모델)
  06.Character/<이름>/  퀘스트 NPC(Claude 가 만든 모델, 한양 조력자와 같은 구성: <이름>.fbx · <이름>.prefab · Textures/ · Materials/) — JoMyeongGu(현감 조명구), YiDam_Dopo(이담 도포·갓), YiDam_King(이담 곤룡포·익선관), GwonSiheon(병조참판 권시헌), HanYungyeom(한윤겸), HayashiSoemon(지주 하야시), KuroseMasatoki·KuroseMasatoki_Armor(영주 구로세 가미시모·갑주), JpRetainer(배신한 가신), WokouSoldier(왜구 병사), PalaceGuard(궁궐 호위), RebelSoldier(반란파), Eunuch(내관), GwanaClerk(관아 하급 관리), MaskedMan(정체 모를 조선인), CourtLady(궁녀), JpRegent(섭정 가로), HongDaebi(홍 대비)(§5 퀘스트 NPC 모델)
  06.Character/Weapons/   Matchlock/(조총 Matchlock.fbx + Materials/), Bow/(활·화살·시위 토막 Bow.fbx + Materials/)(§5 무기)
  07.Animation/          ARPGSamurai(Unity 판 rig_*), ARPG_Samurai_Fab(Fab 판 FBX 339개)
  08.Environment/JapanCastle/  일본성(§4 일본성): Meshes/JapanCastle.fbx · Textures/ · Materials/ · Prefabs/JapanCastle.prefab · LOD/(LOD 메시 `Meshes/`, 임포스터 페이지 2장 + 재질 `IMP_JPCastle_P0·P1`)
  08.Environment/JapanTown/    일본 마을 건물 11종(§4 일본 마을): Meshes/JapanTown_Buildings.fbx · Textures/ · Materials/ · Prefabs/JP_*.prefab · LOD/(LOD1·LOD2 메시 `Meshes/`, 임포스터 페이지 `IMP_JPTown_P0_BC/NOS.png` + 재질 `IMP_JPTown_P0`)
  08.Environment/Sky/     `T_StarsCubemap.asset`(별 큐브맵 512² RGBA32 — DayNightSetup 이 만듦, PBS spaceEmissionTexture)
  08.Environment/NavMesh/ 마을 사람 NavMesh 데이터(`NavMesh_KoreanVillage.asset`·`NavMesh_JapanTown.asset`)(§4 마을 사람), 병사(`NavMesh_Yukjo`·`NavMesh_JapanCastle`), 왜구섬(`NavMesh_WokouDen`), 근정전 마당 궁궐 호위(`NavMesh_Geunjeongjeon`)
  09.Quest/Act1·Act2·Act3·Act4·Epilogue/ 퀘스트 에셋 `Q_<번호>_<이름>.asset` — 1막 6개 · 2막 10개(2-8A·2-8B 분기 포함) · 3막 6개 · 4막 7개(4-5A·4-5B) · 에필로그 2개(§3 퀘스트)
  08.Environment/Ferry/  나룻배(§4 호수·나룻배): Meshes/Ferryboat.fbx(LOD 3) · Textures/ · Materials/ · Prefabs/Ferryboat.prefab(사공·승객 자리 포함)
  08.Environment/PalaceVista/  궁궐 원경 그림(§4 궁궐 원경 그림): T_PalaceVista_BaseColor/Normal.png · PalaceVista_Card.asset · M_PalaceVista.mat
  08.Environment/Hanok/  기와집 일자·ㄴ자·ㄷ자 + 기와 담장 조각 + 초가 일자·ㄴ자(§4 기와 마을·초가 마을): Meshes/(FBX) · Textures/(기와·초가 아틀라스 각 2장 + 담장 벽 2장) · Materials/ · Prefabs/
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
- `enum Faction { Pirate(플레이어), Navy(관군 판옥선 + 상선), Japanese(왜군 세키부네/안택선), Wokou(왜구 — 2026-10-07 끝에 추가, 직렬화 값 3) }`. **적 판정은 `ShipFaction.IsHostileTo` 한 곳**: 관군 ↔ 왜군은 늘 적, **왜구는 모든 진영과 늘 적**, 플레이어 ↔ 관군·일본군은 현상금(`ReputationSystem.PlayerHostileWith`)으로 정함. 표적 판정은 `IsTargetableBy(공격자, 대상)` = IsTargetable + 다른 진영 영해(FactionTerritory outer) 안의 배는 표적 아님.
- **현상금 `ReputationSystem`**(씬 루트 `ReputationSystem` — 2026-10-07 사용자 지시 "어쌔신크리드 오디세이처럼 관군 현상금 아이콘, 일본 현상금 아이콘으로 기존 게이지를 교체". 예전 우호도 게이지 standing −100~+100 하나를 바꿈, 클래스·파일 이름은 씬 참조 때문에 그대로, 씬의 옛 `standing` 값은 안 씀): 진영마다 점수 `navyBounty`·`japanBounty`(0~100). **단계(아이콘 수) = `levelThresholds` 25·40·55·70·85 를 넘은 개수(0~5), 1단계부터 그 진영이 플레이어를 공격**. 관군 배(상선 포함)를 맞히면 관군 +2·일본 −2, 격침 한 방이면 군선 ±15 / 상선 ±10 추가(그 반대도 같음 — 예전 "한쪽이 오르면 다른 쪽이 내려감"을 살림, `otherSideRelief` 1). 0단계일 때만 초당 0.1 씩 0 으로 돌아오고, **1단계 이상(적대)이면 그대로 머묾**(2026-10-06 사용자 지시 그대로). 왜구를 쳐도 현상금은 안 움직임(`wokouSunkByPlayer` 만 셈). 퀘스트용 `SetLevel(진영, 단계)`·`SetBounty`·`AddBounty`, 알림용 `OnLevelChanged(진영, 예전, 새)`. 단계 수·문턱·증감량은 Claude 가 정함(문턱 25·증감량은 예전 값) — 손맛 사용자 확인 필요. 예전 우호도 API `SetStanding` 은 없어짐(시험 코드에서 쓰면 컴파일 오류).
- `ReputationGaugeUI`(같은 오브젝트): 체력바 아래(bottom 4) 왼쪽 "관군" 투구 5개 · 가운데 상태 글자(중립 / ◀ 관군 적대 / 일본 적대 ▶ / 둘 다 적대) · 오른쪽 "일본" 가부토 5개. 단계만큼 진한 색(관군 파랑 · 일본 빨강), 빈 칸은 밝은 회색 반투명(처음 짙은 회색은 검은 테두리와 겹쳐 바다 위에서 안 보였음 — 캡처로 확인 후 바꿈), 아이콘 줄 아래 가는 막대 = 다음 단계까지. 단계가 오르면 그 아이콘이 잠깐 커짐. 아이콘 그림은 코드로 그림(조선 투구 = 반타원 머리·챙·꼭지·드림, 일본 = 둥근 머리·차양·목가리개·V자 뿔, 64² 안티에일리어싱, 에셋 없음).
- **영역 `FactionTerritory`**(각 섬 `Territory` 자식, 생성기가 만듦): inner(= 1.35 × 섬 반폭 + 60) 안에는 돌아다니는 배 스폰·진입 없음(선착장 배만), outer(= inner + 700, 왜구섬은 + 400) 안에는 그 섬 진영 배만 스폰(육지 쪽으로 갈수록 확률 ↓, smoothstep), 다른 진영의 배는 들어오지 않고(순찰·항해 목적지를 밀어냄) 그 안의 배를 표적으로 삼지 않음(안전지대). 조선 섬 = Navy, 일본섬 = Japanese, 왜구섬 = Wokou. **퀘스트가 띄운 배는 예외**(2026-10-09, `ShipFaction.ignoreTerritory` — 스폰 금지·밀어내기(EnemyShipBase·NavyShipBase 의 PushOut·InsideBlocked)·보호(IsTargetableBy) 모두 건너뜀; 퀘스트가 남의 영해 안(구로세 항구·왜구섬 앞)에서 싸우게).
- 활성 배는 `ShipFaction.All` 에 자기 등록. `FindNearestHostile(self, range)`, `Of(GameObject)`(부모까지 탐색 — 포탄은 자식 콜라이더에 맞음), `IsTargetable()`(완파·침몰 중·안전지대 안이면 표적 아님).
- 태그(Player/Enemy) 판정은 `ShipFaction` 없는 대상용 폴백으로만 남음(`Bullet.ShouldExplode`, `EnemyShipBase.Retarget`).

### 플레이어 배 (`panok`, 태그 Player)
- **속도·선회(2026-10-06, 사용자 지시 "너무 느림·무거움 — 1.7 배를 기본, Space(부스트)로 서서히 4 배, 판옥선은 가만히 있어도 회전(고증)")**: 씬 값 maxSpeed 15 → 25.5 → **17.85**, 후진 6 → 10.2 → **7.14**(2026-10-09 사용자 "배 속도 전체적으로 70 % 로" — 플레이어 배만, AI 배는 그대로), `boostMultiplier` 1.6 → 4 → 2(2026-10-07 "부스터 속도 절반으로") → **1.7**(2026-10-07 사용자 지정 — 최고 약 30 m/s), `boostRiseTime` 2 → **6 s**(부스트 키 = Space, Input Actions `Boost`), 가속 9 그대로. 새 `constantAcceleration`(미는 동안 물 저항을 상쇄 — 예전엔 가속 9 / 저항 0.6 → 이론상 최고 15 m/s 에 점근해서 무겁게 느껴졌음), 새 `pivotTurnAuthority` 1(멈춰서도 방향타 100 %). 확인(Play, maxSpeed 25.5·부스트 4 배일 때 — 17.85 로는 아직 Play 확인 안 함): 4.3 s 에 24.9 m/s, 부스트 7.9 s 에 100 m/s(롤 10° 까지, 뒤집히지 않음), 멈춘 채 3.6 s 에 85° 돎(25 °/s). 100 m/s 에서 섬·다른 배와 부딪힐 때·물결 효과는 확인 안 함.
- `MoveControl`: 힘 기반(AddForce/AddTorque). 축 분담 — 물리 = XZ 위치 + Y 회전, `ShipBuoyancy` = Y 높이 + 피치/롤. Rigidbody 저항 0(**물 저항은 MoveControl.FixedUpdate 가 직접 계산** — 끄면 배가 떠내려감), Interpolate, ContinuousDynamic. 코드 기본 mass 1000·maxSpeed 10·boost ×1.6, 방향타 0.4~4 m/s. 씬 템플릿의 속도·가속은 코드 기본의 1.5 배.
  - **정박**: `SetDocked(bool)` — 입력 무시 + 정박 위치·방향으로 끌어당김(anchorStiffness 0.6 / anchorDamping 1.5 / anchorYawStiffness 0.8). 하차 중 사용. `Halt()` 는 속도·currentSpeed·boost 모두 0.
- `CannonControl`: 우클릭 줌(FOV 60→28) + 좌클릭 발사, 쿨다운 4 s. **일제사를 포문마다 차례로 `volleyDuration` 1 s 에 걸쳐**(간격 = 1/(문 수 − 1) × 0.8~1.2, 한쪽 14 문이면 약 0.077 s — 2026-10-06 사용자 "한 번에 발사되어 시끄러움, 일제사 전체 약 1 초" — 쏘는 순간의 카메라 방향을 씀). 좌/우현 = 선체 right 축과 조준 방향 내적(deadzone 0.08). 포탄에 진영·강화 배율을 찍음.
- **들이받기 `RamDamage`(panok, 2026-10-07 사용자 지시 "부스터 상태로 배를 박으면 배 데미지")**: 부스트 게이지(`MoveControl.boostLevel`) ≥ 0.2 이고 상대 속도 ≥ 5 m/s 로 다른 배(ShipHealth)에 부딪히면 피해 = 1.2 × 상대 속도 × 부스트 게이지, 같은 배는 1.5 s 에 한 번, 공격자 진영 = 플레이어(현상금 반영). 수치는 Claude 가 정함. 확인(Play): 관군 판옥선에 부딪혀 7.06 피해(HP 100 → 92.94).
- `CameraFollow`: `panok/CameraPivot` 에 부착, 마우스 오빗(pitch −30~60), 우클릭 시 레이캐스트로 조준점. 침몰 중에만 카메라 y ≥ 해수면 +3 m. **Main Camera 는 CameraPivot 의 자식**(로컬 (0, 0.3, −0.471), 회전 (18.3, 0, 0)), far clip 4000, near 0.3, 안티에일리어싱 = TAA(품질 Medium — 먼 기와 반짝임 때문에 켬, 예전 None).
- `Bullet`: 플레이어 포탄만 발사 직후 위로 730 힘 보정. 같은 진영·침몰 중인 배엔 안 터짐. y < −20 이면 파괴. 바위·지형에 맞으면 터지지 않고 굴러감.
- `BallisticAim`: 적 포격 예측 사격(비행시간 × 표적 수평 속도 3회 반복, leadFactor 1, maxLeadTime 6 s). 탄도 공식 `v² = g·d² / (2(d·tanθ − Δh)cos²θ)`, 실패 시 직사.
- `OarAnimator`: `OarGroup/Left|Right` 자식 노의 로컬 X 만 회전(우현 노는 Y=180). 멈춰 있어도 idle 0.25 회/s 로 저음, **정박 중엔 1.5 s 에 걸쳐 멈추고 앞 자세(좌 −25° / 우 25°)**, 서지 없음.

### AI
- `EnemyShipBase`(왜군)와 `NavyShipBase`(관군·상선)는 **의도적으로 분리된 복제본**(힘 기반 항해 + 순찰 + 0.5 s 재표적). 순찰 = 전방 편향 0.45 + 앵커 드리프트 1.5 m/s.
- `EnemyAI`(안택선): Patrol/Chase/Broadside. 씬 값 det 100 / lose 130 / atk 70 / brk 85(`OnValidate` 가 lose ≥ det×1.2, brk ≥ atk×1.2 강제). 추격 리드 ≤ 2.5 s.
- `SekiScoutAI`(세키부네, 비무장): 발견 시 350 m 안 EnemyAI 에 25 s 경보, 55 m 거리 유지.
- `NavyPanokAI`: EnemyAI 뼈대 + 접현 회피 28 m + 300 m 내 아군 소집. 추격 90 m 이상이면 부스트(×1.6).
- **교전 자세(2026-10-06, 사용자 지적 "멈춘 배 주위를 돌며 멀어졌다 가까이 와서야 공격 — 근접하면 측면을 보이며 공격")**: `EnemyAI`·`NavyPanokAI` 의 Broadside 가 표적 둘레를 돌지 않음 — `standoffDistance`(0 = attackRange × 0.75 = 52.5 m) ± `holdBand` 12 m 안이면 **추진 없이 현측(좌·우 중 덜 도는 쪽)만 표적에 맞춤**(적 배는 원래 속도와 상관없이 돎), 밖이면 표적 쪽(멀 때)/바깥쪽(가까울 때)으로 최대 35° 기울여 broadsideSpeed 로 움직임. 관군 keepAwayDistance 28 은 그대로. 확인(Play): 멈춘 플레이어 동쪽 65 m 에 관군 판옥선 → Broadside, 현측 각 90°, 63.8 m 에서 정지(0.02 m/s), 일제사 2 번·플레이어 배 체력 290 → 284.
- 포격(`EnemyCannonControl`/`NavyCannonControl`): Broadside + 사거리(90/95) + 현측 55° 안일 때 일제사. 관군 최대 8문, 재장전 8 s. AI 가 꺼진 배는 같은 배의 `HarborPatrol.Target` 을 쏨(선착장 배, §4).
- `MerchantAI`(Navy, 비무장): Sail/Follow/Flee. 격침(약탈)은 현상금으로 처리. **퀘스트 호위 목적지 모드**(2026-10-09): `hasDestination` 이면 도망·대열 없이 `destination` 으로 `destinationSpeed`(퀘스트 4.5 m/s) 항해, `arriveRadius` 안이면 멈춤(`Arrived`).
- `EnemyFleetSpawner.PickFleetCenter(spread, 진영)`: BaseSafeZone + FactionTerritory.SpawnWeight 로 12번 뽑아 실패하면 그번엔 스폰 안 함. 배 한 척씩도 PushOut.
- `EnemyFleetSpawner`: 씬의 원본 배(anteck/seki/panok_navy/merchant/**anteck_wokou/seki_wokou**)가 템플릿 — Start 에서 끄고 복제 스폰. 플레이어 기준 250~500 m, 거리 분포 `u^(1/1.8)`, 900 m 밖 정리, 8 s 간격. 왜구 함대(2026-10-07) = 세키부네 1~2 + 안택선 0~1, 동시에 1 함대(`maxWokouFleets`). 퀘스트용 `SpawnForQuest(원본, 수, 거리, 흩어짐)` — 플레이어 둘레 링에 띄우고 스포너 정리 목록에는 안 넣음(퀘스트가 1000 m 밖이면 지움). 2026-10-09 넓힘: `SpawnForQuest(원본, 수, 거리, 흩어짐, ignoreTerritory, around 마커, factionOverride)` — 마커 둘레에도 띄우고, ignoreTerritory 면 영해 규칙 무시(예전엔 섬 영해 안에서 부르면 영해 가장자리 약 1.5 km 밖에 생겼음), factionOverride 로 진영을 바꿔 띄움(상선 원본 → 일본 수송선). 육지(터레인 −2 m 위)에는 안 띄움(`IsLand`).
- 왜구 배 원본 `anteck_wokou`·`seki_wokou`(씬 루트, 2026-10-07): 씬의 anteck·seki 를 복제(프리팹 아님)해 `ShipFaction` = Wokou, 돛(`sail_JP_color` 텍스처 재질) BaseColor (0.26, 0.23, 0.22), 깃발(`Flag_J*`) (0.10, 0.09, 0.09) — 재질 `WokouMaterial/Wokou_*`. AI·선원(일본 병사 조총)·대포는 원본 그대로. 위치 (75, 0, 137)·(30, 0, 91)(물 위, Play 에서는 꺼짐).
- `SekiScoutAI.SendAlert` 는 **같은 진영 EnemyAI 만** 부름(2026-10-07 — 왜구 세키부네가 일본군 안택선을 부르지 않게).
- **베이스 안전지대** `BaseSafeZone`(PlayerRespawnPoint 에 부착, **반경 350** — 섬·동굴의 가장 먼 끝이 리스폰 지점에서 약 255 m): 스폰 위치에서 제외, AI 목적지를 경계 밖으로 밀어냄, 안에 있는 배는 표적 아님. 스포너는 함대 중심을 반경 + 흩어짐 + `safeZoneMargin`(60) 밖에서 뽑고, 배 한 척씩도 경계 + 60 밖으로 밀어냄.

### 물 (HDRP Water)
- `WaterHeightUtil.Sample` = `ProjectPointOnWaterSurface` 래퍼(캐시 없음).
- `ShipBuoyancy`(order 100): 4점 샘플, 비-kinematic 이면 Physics 모드(스프링 20 / 감쇠 8 / 최대 30, 중력 끔).
- `ShipWakeFoam`(WaterFoamGenerator), `ShipBowWave`(WaterDecal + `Water Decal Sample` 셰이더 — **키워드** `_TYPE_BOW_WAVE` 등으로 분기).
- `ShipDecalLOD` + `WaterDecalLODManager`: HDRP 워터 데칼 **씬당 96개 상한** → 카메라 가까운 8척만 켬(150 m + 15 m 히스테리시스).
- `WaterRippleSpawner`(풀: 충돌 8 / 항적 10 / 물보라 8), `ShipImpactSensor`(충돌 6프레임 전 속도로 세기 계산).
- `WaterShoreMask`(Ocean): 육지 가까운 바다의 파도를 줄이는 `waterMask`(R 너울 / G agitation / B 리플 — CPU 시뮬레이션에도 반영)를 굽는다. 육지 = 위에서 쏜 레이가 처음 맞는 리지드바디 없는 콜라이더가 y −0.5 이상인 곳(동굴 지붕 밑 물도 포함). 중심 (−513.5, 1215), 7500 m(base + 새 섬 전부 + 왜구섬 — 2026-10-07 왜구섬을 넣으려 넓힘, 예전 중심 (−513.5, 1403)·7296 m), 1024²(칸 약 7.3 m), 육지 옆 R 0.1 / G 0.2 / B 0.6 → 200 m 에 걸쳐 1. 결과 `02.Resources/00.Component/02.Water/WaterShoreMask.png`. **터레인·마을을 바꾸면 컴포넌트 컨텍스트 메뉴 "해안 마스크 굽기"로 다시 굽는다.** 잔잔한 물 구역 `calmZones`(다각형 — 안쪽으로 feather 80 m 에 걸쳐 R 0 / G 0.1 / B 0.5 로 낮춤, 높이지는 않음): 지금 `육조거리 남쪽 호수` 하나(§4 호수·나룻배). 2026-10-07 다시 구움(1586 ms). 폭풍우(바람 140) 측정: 먼바다 −21.6~+18.4 m, 선착장 주변 −1.3~+0.2 m, 동굴 안 −3.5~+1.6 m.

### 날씨 (`WeatherSystem`)
- 인스펙터 인라인 프리셋 리스트(SO 아님)를 가중치로 뽑아 25 s SmoothStep 블렌드. WaterSurface·CloudLayer(**cloudCoverage 는 R/G/B/A 채널을 순차로 켬**)·VolumetricClouds·하늘 노출·Fog(meanFreePath/tint 만)·태양 Lux·RainMaker 를 구동. **하늘 볼륨은 `WeatherSystem.FindSkyVolume()`(VisualEnvironment 가 든 전역 볼륨, `Showcase*` 제외)** — 2026-10-09 전에는 첫 전역 볼륨을 잡아 인벤토리 뷰어 볼륨 `ShowcaseStage_InventoryMenu` 를 잡았음(Play 에서 확인 — 날씨의 구름·안개·하늘 밝기가 실제 하늘에 안 들어갔을 것), 씬 값 `skyVolume` = `Sky and Fog Global Volume`, `sun` = `Directional Light` 로 박아 둠. PBS 일 때 하늘 밝기는 `skyExposure − pbSkyReferenceExposure(14)` 보정값, 해 Lux = 프리셋 Lux × `DayNightCycle.SunScale`.
- 프리셋 바람: 맑음 65, 나머지는 `65 + (w−35)·0.5`. 비 0.6 / 폭풍우 1.
- 비(RainMaker)는 배(`rainFollowTarget` = panok, 씬 값)를 따라가고, **하차 중(`BoardingSystem.IsOnFoot`)이면 캐릭터 `OnFootRoot` 를 따라감**(2026-10-04). RainMaker 안개 입자 `RainMistParticleSystem` 은 끔(`rainDisableMist` — 사용자 지시 "비현실적", 생성 직후 참조를 비워 RainScript 가 건너뜀, 에셋은 안 바꿈).
- 비 가림 `UpdateRainCover()`: 0.25 s 마다 따라가는 대상 주변 3×3 점에서 위로 600 m 레이 → 막힌 비율 `rainCovered01` 만큼 비를 줄임(동굴 안에서 비 안 옴). 배 = 간격 40 m · 시작 y 35, 하차 = 간격 3 m · 시작 = 발 + 2.2 m(땅이 35 m 넘는 곳이 있어서).
- `FlagWind`(worldAccelerationScale 0.3, damping 0.1), `LightningStorm`(바람 ≥ 90), `WeatherTestKeys`(`]`/`[`/1~9/`\`/0).
- `LightningStorm` 섬광은 그림자 없는 방향광 → **카메라 위가 막힌 비율(`shelter01`, 중심 + 둘레 4점에서 위로 레이 400 m)만큼 줄인다**(동굴 안에서 안 번쩍임).

### 밤낮 (`DayNightCycle`, 씬 루트 `DayNightCycle` + 자식 `Moon`, 2026-10-09 — 사용자 지시 "밤낮 시스템 넣어주세요, 낮인데 해가 없더라고요 해도 보이게")
- 해가 안 보이던 까닭: 하늘이 GradientSky(색 그라데이션)라 해 원반을 그리지 않음 → `Assets/Settings/SkyAndFog Profile.asset` 에 **Physically Based Sky**(EarthAdvanced 기본값, exposure 0, updateMode Realtime 1 s, 별 = `T_StarsCubemap` spaceEmission 1) 를 넣고 Visual Environment 하늘 종류를 PBS(4)로 바꿈. GradientSky 덮어쓰기는 남겨 둠(안 쓰임). 해 원반 = `Directional Light` HDAdditionalLightData angularDiameter 0.53·interactsWithSky.
- 시각 `timeOfDay`(0~24, 시작 9 시): 06~18 시 = `dayMinutes` 16 분, 18~06 시 = `nightMinutes` 6 분(실제 시간). 해: 6 시 동(+X) → 12 시 남(−Z 쪽 하늘) 높이 `maxSunElevation` 60° → 18 시 서. 노을·하늘색은 PBS 가 해 높이로 계산.
- 해 밝기 `SunScale` = smoothstep(높이 −2° → 12°), 높이 −4° 아래면 해 Light 끔. 달 = 해 반대 방향 방향광(`moonLux` 2.5 × smoothstep(−높이), 색 (0.72, 0.80, 1), 원반 0.55°, Soft 그림자), 해 높이 2° 아래에서 켬 — 방향광 그림자는 늘 하나.
- 밤 밝기(전역 볼륨 런타임 사본에 Exposure·IndirectLightingController 덮어쓰기): 자동 노출 하한 `limitMin` = 낮 −1 ↔ 밤 `nightExposureMin` 1(daylight = smoothstep(해 높이 −8° → 8°)), 상한 14. **간접 확산·반사 프로브 배율 = 낮 1 ↔ 밤 0.002(로그 보간)** — APV·동굴 리플렉션 프로브는 낮 해로 구운 값이라 0.12 로 두었을 때 밤에 동굴 바위가 하얗게 빛났음(캡처). 동굴 볼륨(CaveInteriorVolume)도 같은 배율 × 씬 값, 노출 하한 밤 3.
- 시험 키 `=`(skipKey) = 1 시간 건너뜀(None 이면 끔). `ClockText()` = "오후 3:20" 글(아직 화면에 안 씀).
- 준비 도구 `Editor/DayNightSetup.Build(forceStars)` — 별 큐브맵(없을 때)·PBS·해 원반 값·DayNightCycle·Moon 을 만들고 참조를 채움(다시 불러도 됨).
- 확인(Play, 2026-10-09, 카메라를 해·달 쪽으로 돌려 캡처): 오전 9:30 해 원반·푸른 하늘·구름, 오후 5:53 노을(해가 수평선에 붉게, 바다에 해 길), 오후 11:00 달·별·달빛 구름, 섬 쪽 밤 장면(동굴 입구 횃불만 밝음, 비 오는 날), 정오 해 높이 60°·67,000 lux. 콘솔 오류·경고 0. **해 뜨는 무렵(5~6 시)·흐린 날 밤·실제 시간 흐름(22 분 하루)은 캡처하지 않음.**

### 피해·침몰·리스폰
- `ShipHealth`: 손상 단계 0~3(0.75/0.5/0.25), 적 150 m 밖 10 s 후 초당 5 회복. 침몰 = 대부분 컴포넌트 off·kinematic·콜라이더 off·기울며 하강, `DamageFX` 파티클 방출 정지. `OnSunk`, `Revive()`, `EndSinkAndRestore()`.
- **플레이어 배**: maxHealth 300, diesAtZeroHealth true, sinkOnDeath true, destroyAfterSink false, sinkDuration 14 s, sinkDepth 30 m.
- `PlayerRespawn`: `OnSunk` → `Revive()` → 그 다음 Rigidbody 위치·회전 대입(순서 중요: kinematic 을 먼저 풀어야 함). `fadeDelay` 0(화면 전환 연출 자리).
- 리스폰 지점 `PlayerRespawnPoint` (−110, 0.3, −4), 뱃머리 +z (수심 약 −20 m).
- `ShipBurnVisual`: `Haesin/ShipBurn` 셰이더로 교체. 제외: 이름에 flag/flg, `crew_`/`char_` 로 시작, 투명 머티리얼. 불 파티클은 `DamageFX` 아래 미리 넣어 두고 켜기만 함. 불 머티리얼 `Oil_Fire_v1~3` 은 `_EnableFogOnTransparent = 1`.
- **타는 세기 `ShipBurnVisual.intensity` = 0.6**(2026-10-04, 사용자 지시 "불타는 효과가 과함 — 최대를 60 % 정도로, 그 밑도 비율대로", 배 5척 씬 값): 표면 그을음·불씨 = 손상도 × 0.6(완파 직전도 예전 손상 60 % 때 모습 — 예전엔 0.9 에서 배 전체가 불씨로 덮임), 불 파티클(`DamageFX/Fire_1~3`, 손상 단계 수만큼 켜는 것 그대로)은 크기·방출량을 각각 √0.6 ≈ 0.77 배(런타임에만 곱함, 원본 값은 그대로 — 예: 안택선 크기 8~10 → 6.2~7.7 m, 300 → 232 개/s). 이미 곱한 배율을 직렬화 필드에 남겨 스포너·정박선 복제본이 두 번 줄이지 않음(측정: 복제본 = 원본 = 7.75 / 232). 확인: 같은 자리·같은 카메라로 손상 90 / 55 / 30 % 안택선 전후 캡처(Play), 콘솔 오류·경고 0. 모습은 사용자 확인 필요.
- `DamageFXDistanceCull`: 카메라 320 m 밖이면 불·연기 렌더러 끔(280 m 에서 다시 켬).
- `PlayerHealthUI`: 하단 체력바(코드 생성).

### 선원 (`ShipCrew`, `CrewMember`, `CrewProjectiles` — 2026-10-04 큐브를 병사 모델로 바꿈)
- 배 자식 `Crew` 아래 `Crew_N`(CrewMember, 배율 1, 원점 = 발) + 자식 `Model`(프리팹 인스턴스): panok 10 = 해적(`Pirate`, 활), panok_navy 10 = 조선 수군(`JoseonNavy`, 활), anteck 12·seki 6 = 일본 병사(`JPSoldier`, 조총). 상선 없음. 자리 = 예전 큐브 자리(발 = 큐브 중심 − 0.8 m), 몸 방향 = 좌·우현 쪽 선원은 바깥으로 70°, 가운데는 앞/뒤.
- 배 루트 스케일 66.79 → `Crew`·`CharacterSeat` 컨테이너 localScale = 1/lossyScale 로 두어 **그 아래는 미터 단위**. 갑판 높이는 메시 삼각형으로 구한 정적 값(배 충돌체는 거친 상자라 못 씀) — 배·위치를 바꾸면 다시 맞춰야 함.
- 애니메이터: 조총 `Crew_Matchlock`(Idle = Quaternius `Rig|Idle_Loop` · Fire = `MatchlockFire` · Death = Quaternius `Rig|Death01`), 활 `Crew_Bow`(Idle · Shoot = `BowShoot` · Death, float `BowString`). 상태 사이 넘김은 스크립트 `CrossFadeInFixedTime`.
- **전투(사용자 지시 "배 말고 선원을 죽이게, 선원이 다 죽으면 대포가 발사되지 않게")**: `ShipCrew` 가 0.5 s 마다 **두 배 충돌체 가장자리 사이 빈틈이 `engageGap` 30 m 안**인 가장 가까운 적대 배(현상금·영해 규칙 그대로 — `IsHostileTo`·`IsTargetableBy`, 중심 거리 90 m 안만 계산)를 표적으로 잡고(2026-10-05 사용자 지정 10 m → 같은 날 30 m — 예전엔 배 중심끼리 110 m), 선원이 그 배의 살아 있는 선원(무작위)을 쏜다. 한 번 = 사격 클립 한 바퀴(조총 2 s 중 1.0 s, 활 3 s 중 1.68 s 에 발사) → 대기(조총 5~9 s, 활 1.5~4 s) → … 처음 장전은 0.5~4 s 흩음. 쏘는 동안 몸을 표적 쪽으로 돌림(초당 240°). 명중률 = 쏘는 선원 ↔ 맞는 선원 거리 20 m → `combatRange` 110 m 에 조총 0.22 → 0.07, 활 0.12 → 0.04(씬 값). 맞으면 그 선원이 쓰러짐(`Death`, 쓰러지는 2.6 s 동안 `AlwaysAnimate` → 끝나면 Animator 끔). **배 체력은 선원 사격으로 안 깎이고 현상금도 안 움직임.** 선원이 모두 쓰러진 배는 대포를 못 쏨: `EnemyCannonControl`·`NavyCannonControl` 은 일제사 전에 `ShipCrew.CanFireCannons`, 플레이어 `CannonControl` 은 좌클릭 때 막고 화면에 "선원이 모두 쓰러져 대포를 쏠 수 없습니다"(OnGUI 2 s, 임시).
- 빈틈 계산 `ShipCrew.GapTo`: 두 배의 트리거 아닌 Box·Capsule(·볼록 Mesh) 충돌체 쌍마다 `Collider.ClosestPoint` 를 번갈아 5번 → 가장 짧은 거리(겹치면 0). 배 충돌체(측정): 판옥선 = Box + Capsule 4, 안택선·세키부네 = Box + Capsule. 충돌체 폭은 판옥선 약 12.6 m · 안택선 약 15 m 라 나란히 붙어도 중심끼리 약 14 m. 확인(Play): 관군 판옥선·안택선을 나란히 세워 중심 22 m(빈틈 9.30 m) → 서로 표적·사격(25 s 에 10→7 / 12→11), 23.5 m(10.80 m)·24.5 m(11.80 m) → 표적 없음(이 시험은 기준 10 m 때 — 30 m 로 바꾼 뒤 Play 시험은 안 함, 판정 코드는 같음).
- **멀리서 끄기(2026-10-05, 사용자 지시 "90 m 에서 선원 오브젝트를 아예 꺼버림")**: `ShipCrew.Update` 가 매 프레임 `Camera.main` ↔ 배 원점 거리를 재 `crewHideDistance` 90 m 를 넘으면 선원 오브젝트(`Crew_N`)를 `SetActive(false)`, 85 m(`crewHideHysteresis` 5) 안으로 들어오면 다시 켬(`crewShown`). 꺼진 선원은 쏘지 않음(가까운 배 선원은 꺼진 선원을 계속 쏠 수 있음). 꺼진 채 맞거나(`Kill`) 되살아나면(`Revive`) 상태만 바꾸고 다시 켜진 뒤 첫 Update 에서 자세를 맞춤 — 쓰러진 선원은 `Death` 끝 자세(잠깐 돌린 뒤 Animator 끔), 산 선원은 Idle(무작위 시점). 꺼진 Animator 에 Play 를 부르지 않으려고. 확인(Play): 시작 때 배 39척 중 38척 꺼짐(선원 336명 중 10명 켜짐 = 카메라 53 m 플레이어 배), 263 m 로 멀어지자 시험 배 둘 꺼짐, 그때 `Kill` 한 선원이 다시 켜진 뒤 골반 높이 0.12 m(누움)·Animator 꺼짐, 산 선원 0.85 m, 콘솔 오류·경고 0. 씬에 저장된 4척(`anteck`·`panok_navy`·`seki`·`panok`) `engageGap` 30 · `crewHideDistance` 90 · `crewHideHysteresis` 5.
- 보이는 거리(2026-10-05, 사용자 지적 "90 m 부터 보인다는데 체감 10 m"): 켜고 끄는 판정은 맞음(측정: 카메라 25·45·60 m 에서 선원 12/12 켜짐, LOD 는 약 150 m 까지 그림 — 시민·병사 모델 LODGroup 크기 1.85, 끄는 화면 높이 0.0107, lodBias 1, FOV 60). 안 보이는 까닭은 **배 난간(현장)에 가려서**: 안택선 선원 발은 배 원점보다 4.3 m 위(판옥선 3.5 m)인데 둘레 벽이 그보다 높아, 물 위 4 m 높이 25 m 에서는 머리만 보이고 14 m 높이 60 m 에서는 하나도 안 보임(Game 뷰 캡처). 판옥선은 45 m · 높이 10 m 에서 선원이 보임. 고치는 방법은 사용자 결정 대기(§9).
- 무기 자세: 대기 = carry(조총은 오른손에 세워 듦 — 소켓 회전 (57.5, 291.9, 333.1), 활은 왼손 옆에 세움 — (330.4, 298.0, 188.5)), 사격 = aim(프리팹 소켓 값) — 0.3 s 에 걸쳐 섞음. 활 선원은 대기 중 `BowString.allowHold = false`(대기 클립엔 BowString 커브가 없어서).
- 연출 `CrewProjectiles`(씬 루트 `CrewCombatFX`): 화살 = `Bow.fbx` 의 Arrow 메시를 풀(최대 80)에서 꺼내 55 m/s 포물선, 명중이면 도착 순간 쓰러짐, 빗나가면 표적 둘레 1.5~5 m 로. 조총 = 총구 불꽃(`Oil_Fire_v1` 시트 무작위 한 칸, 0.09 s) + 연기 4덩이(재질 `Weapons/Matchlock/Materials/M_MusketSmoke` = Hovl `HDRP_Blend_CG` + `Smoke20`, 2.2~3.2 s), 명중이면 거리/350 s 뒤 쓰러짐(탄은 안 그림). 파티클 시스템 두 개를 같이 씀(Emit). 소리: 조총 = `cannon_fire_thimras_cc0` 피치 2.0·0.2, 활 = `sword_swing_1~3` 피치 1.5·0.2, 탄·화살 박힘 각 0.2(2026-10-06, §3 소리) — 들어 보고 고른 것 아님.
- 되살리기: 플레이어 리스폰(`PlayerRespawn`) 때 `ShipCrew.ReviveAll()`. 선착장 배(`HarborMooring`)는 `combatEnabled = false`(선원은 반격 안 함 — 맞으면 쓰러지기는 함, 대포는 쏨). `ShipBurnVisual` 은 CrewMember 아래 렌더러·SkinnedMeshRenderer 를 태우지 않음(예전엔 재질 이름 `crew_`/`char_` 만 뺌).
- `AddRecruit()`: recruitSpots 순서로 첫 선원을 복제(되살린 상태로), maxCrew 16.
- 확인(Play, 2026-10-04): 관군 판옥선 ↔ 안택선을 65 m 떨어뜨려 세움 → 37 s 에 10→7 / 12→7, 쓰러진 선원 골반 높이 0.11 m(누움), 일본 병사 겨눔·관군 만작 캡처, 선원 전멸한 플레이어 배 `CrewAllowsFire` = false, 침몰 → 리스폰 뒤 선원 10/10·대기 상태, 콘솔 오류·경고 0.

### 자원·강화 (씬 루트 `GameData`)
- `PlayerResources` 목재/철/금화 초기 200/120/1000.
- `ShipUpgrades`: 함포(피해 ×(1 + 0.15·lvl))·화살·선원 체력(값만 저장 — 시스템 없음) 최대 10, 깃발 색·이미지(MaterialPropertyBlock, 텍스처 이름 `Flag_*`, `StreamingAssets/Flags/`).

### 소리 (`11.Audio/`, 음원 모두 CC0)
- `SeaAmbientAudio`(WeatherSystem 오브젝트): 2D 0.2, 동굴 가림 시 ×0.25, 0 에서 페이드인.
- `ShipAudio`(panok): 선체 물살(0.8→12 m/s, 최대 0.55) + 노 소리. **노 소리(2026-10-06, 사용자 지시 "0.5, 나무 삐걱임, 랜덤으로 3 개는 들리게", 이어서 "피치 0.5 까지")**: 예전 물소리 루프(`rowing_quickly_s1513_cc0` 0.35 → 잠깐 키운 `rowing_quickly_loud.ogg`)는 안 씀. **OarAnimator 위상이 0 을 지날 때(젓기 시작)마다** `oarDriveClips` = `Audio/Oar/oar_stroke_1~6.wav` 중 `oarVoices` 3 개(서로 다른 것)를 좌현·우현(배 원점 ± 옆 5 m)·가운데 AudioSource 에서 첫 소리는 바로, 나머지는 0.05~0.35 s 늦게(×0.75~0.95) 틂, **피치 `oarPitch` 0.1**(사용자 "피치 최소로" — 0 이면 소리가 멈추고 음수는 거꾸로 재생이라 0.1 로 둠, 2026-10-07) × 0.94~1.06. 볼륨 `oarVolume` **0.25**(2026-10-07 사용자 지정, 0.5 → 0.2 → 0.25) × (제자리 0.6 ~ 최고 속도 1), `oarMinDistance` 60 m(배 카메라–배 원점 약 53 m). 되돌릴 때 소리(`oarRecoverClips`)는 비움. 음원 = **Kenney RPG Audio(CC0) `creak1~3`(피치 0.75~0.9, 0.55) + 원래 `rowing_quickly_s1513_cc0` 녹음에서 젓는 소리 봉우리 12 곳을 0.9 s 씩 잘라 낸 것**을 겹침(약 1 s). 정박(젓기 0)·침몰 중엔 안 남. 제자리에서는 배가 앞뒤로 아주 조금씩 밀려 위상이 거꾸로 갔다 왔다 해서 거의 안 남(관찰). 확인(Play): 0.6 으로 저어 24.6 m/s 에서 젓기 소리 고름(리플렉션), 콘솔 오류 0. 처음 합성으로 만든 삐걱임(`oar_drive_*`·`oar_recover_*`, 파일 남음 — 안 씀)은 "불쾌한 끄윽 소리, 노 소리가 아님"이라는 지적으로 버림.
- `BulletAudio`(포탄 템플릿 `Sphere`): 발사 0.5(max 900 m) / 비행 휘파람(합성 음원, 속도 3 m/s 아래 0) **0.15**(2026-10-06 사용자 "휘이잉 소리 너무 큼" — 예전 0.5) / 명중(터지는 소리 `cannon_hit_thimras_cc0`) **0.25**(2026-10-07 사용자 "터지는 소리 너무 커서 0.1" → 2026-10-09 "폭발 사운드 0.25", 예전 0.5, max 600 m). `OneShotAudio` = 감쇠 지정 가능한 원샷.
- `SwordAudio`(캐릭터): 휘두르기 0.1(windup − 0.28 s 에 재생), 막기·튕겨내기 부딪힘 3종 0.6, **베이는 소리 `fleshClips`(`Sword/flesh_hit_1~3.wav`) 0.3**(2026-10-06 사용자 지정 — 합성으로 두 번 만들었으나 "이불 터는 소리" 지적 → **Kenney RPG Audio(CC0) `knifeSlice`·`knifeSlice2`(셋째는 피치 1.12) 위에 `chop` 을 베는 소리 최고점에 겹친 것**, 0.36~0.47 s). 이 사람이 이 사람이 "피격"·"위험 공격 피격"·"결정타" 를 받을 때, 플레이어가 마을 사람을 벨 때 `PlayFleshAt`; 메인 씬 플레이어·병사 프리팹에 넣음, CharacterTest 씬 두 캐릭터에는 아직 안 넣음), 뽑기·넣기 각 3종 0.1(넣기 소리는 CC0 조각으로 합성). 들어 보고 고른 것 아님.
- **선원 사격 소리(2026-10-06, 사용자 지정 각 0.2)**: `CrewProjectiles`(CrewCombatFX) 조총 0.2(예전 0.35)·화살 쏘기 0.2(예전 0.12)·**탄 박힘** `bulletImpactClips`(`Impact/bullet_impact_1~3.wav`) 0.2 — 비행시간(거리/350 s) 뒤 명중 자리 또는 빗나간 자리(겨눈 곳 ±3 m)·**화살 박힘** `arrowImpactClips`(`Impact/arrow_impact_1~3.wav`) 0.2 — 화살이 도착하는 순간(명중·빗나감 모두 한 번).
- **Kenney RPG Audio(CC0)**(2026-10-06, 사용자 허락으로 Unity 에서 kenney.nl 의 `kenney_rpg-audio.zip`(964,837 B)을 `Captures/kenney/` 에 받음 — 소리 50개: knifeSlice·chop·creak1~3·doorOpen·footstep 등, 라이선스 사본 `Audio/Oar/LICENSE_Kenney_RPG_Audio.txt`). 클라우드에서 zip 을 풀어 필요한 것만 섞어 넣음(CodeDom 에서 `System.IO.Compression` 은 참조가 없어 못 씀).
- 새 음원(2026-10-06 — `Audio/Impact/`(탄·화살 박힘, 합성), `Sea/rowing_quickly_loud.ogg`(안 씀)): 클라우드 작업 공간에서 음원 사이트(kenney.nl·bigsoundbank·opengameart·freesound)·GitHub 검색이 프록시 정책으로 막혀 받지 못함 → **프로젝트에 이미 있는 CC0 조각(Kenney 발소리 wood/snow, StarNinjas 칼 휘두르기)을 피치 바꿔 겹치고 합성음(감쇠 사인 쿵·대역 잡음)을 더해 만듦**(모노, 44.1 kHz, 0.33~0.5 s, 가져오기 Force To Mono). 들어 보고 고른 것 아님 — 사용자 확인 필요.
- **전투 북 `WarDrum`(씬 루트 `WarDrum`, 2026-10-07 사용자 지시 "적이 근처에 있으면 북소리, 가까울수록 빠르게")**: 2D. 배를 탔을 때 = 가장 가까운 적대 배(`ShipFaction.FindNearestHostile`, 300 m 안), 걸을 때 = `SoldierAI.Hostiles` 중 가장 가까운 병사(40 m 안). 간격 1.6 s(멀 때) → 0.35 s(배 40 m / 병사 4 m), 볼륨 0.08 → 0.3, 4 박마다 강박. 음원 `Audio/Drum/war_drum_1~3.wav`·`war_drum_accent_1~2.wav`(합성 — 들어 보고 고른 것 아님). 확인(Play): 적 152 m → 0.89 s, 27 m → 0.35 s.
- `FootstepAudio`(캐릭터, OnFoot 에서만): 발 닿음 = `FootIK.animLiftLeft/Right`(IK 전 애니메이션 발바닥 높이)가 0.05 m 위로 들렸다가 0.025 m 아래로 내려올 때, 캐릭터 속도 0.4 m/s 이상, 같은 발 0.18 s 간격. 바닥 = 발 아래 레이: 터레인은 가장 진한 레이어 이름(grass/forrest → 풀, sand/gravel → 모래, 그 밖 → 돌), 그 밖 콜라이더는 이름(자신+최상위)에 stair/bridge/deck/wood/dock → 나무, rock/cave/stone/cliff → 돌, 없으면 나무. 음원 Kenney Impact Sounds(CC0) wood/grass/snow(모래용)/concrete(돌용) 각 5개, 모노, 볼륨 0.1(사용자 지정, 달리기 ×1.3), 3D min 4 / max 30 m, 피치 0.92~1.08. 측정: 걷기 0.41 s 간격(선착장·계단 모두). 들어 보고 고른 것 아님.

### UI
- 공통 일시정지 `GameMenuPause`(static): timeScale 0, 물 timeMultiplier 0, 소리 정지, `disableWhileOpen`(CameraFollow, CannonControl, WeatherTestKeys, PlayerOrbitCamera, PlayerCombatController) 끔. 한 번에 하나. `MapSystem.disableWhileOpen` 은 씬 직렬화 값(같은 목록으로 맞춰 둠).
- `MapSystem`: M 전체 지도(해도 `Resources/MapAtlas/OceanChart.png`) + 미니맵(같은 해도를 `ZoomUV` 로 300 px 만 확대, 관군·일본·왜구(보라 `enemyWokou`) 배를 모두 표시 — 적대면 크게, 중립이면 0.75 배). 퀘스트 목표 자리 `MapSystem.questTarget`(static, QuestManager 가 정함)을 금색 점으로 — 미니맵 밖이면 가장자리에 붙여 방향만, 큰 지도에는 이름 글자와 함께.
- 함선 메뉴 `ShipMenu`(Y/ESC): 좌측 이미지 타일 5개(`Resources/ShipMenuArt/`), 가운데 전체 화면 3D 배(`ShowcaseViewer` fullScreen + 배경 쿼드), 우측 상세·강화·깃발 UI, `ResourceBar`.
- 인벤토리 `InventoryMenu`(I/ESC): 좌측 캐릭터 3D 뷰어(전용 조명 켬), 우측 탭 5개 + 빈 슬롯 20칸(아이템 시스템 없음).
- `ShowcaseViewer`: 레이어 Showcase 전용 카메라 + RT, 스테이지 (20000|20300, 600, 20000). 대상은 **MeshFilter/MeshRenderer 만 계층째 복사**, SkinnedMeshRenderer 는 `BakeMesh(mesh, false)` 후 노드 월드 스케일 1. 고정 노출 13.5, 물·구름·안개 끔. `stageLights`(키 30000 / 보조 10000 / 역광 15000 lux 스포트, 카메라 기준 고정). 드래그는 마우스 아래 최상위 UI 가 뷰어일 때만.
- `UIKit`: 색은 `SolidSprite(color)`(픽셀에 `.linear` 로 구움) + Image.color 흰색. EventSystem 없으면 생성(InputSystemUIInputModule).
- `MenuArt`(`Resources/MenuCommonArt/` 버튼 9-slice: Normal border 44 / ppu 배수 4, Selected 80 / 6), `ResourceBar`(아이콘 + 숫자), `UITooltip`.

### 퀘스트 (`16.Quest/`, 씬 루트 `QuestSystem` — 2026-10-07 1막, 2026-10-09 2막~에필로그)
- 스토리 원본 = 사용자 스프레드시트 `해신_스토리_구조.xlsx`(퀘스트 흐름·분기·인물·묘호, claude.ai 채팅에서 만듦 — 프로젝트 폴더에는 없음). 대사는 줄거리를 Claude 가 씀.
- `QuestData`(ScriptableObject, 에셋 하나 = 퀘스트 하나): questId·act·title·giver·summary, `onStart` 동작 → `introDialogue`(자막) → `objectives`(순서대로 하나씩, 목표마다 `onBegin`·`onEnd` 동작) → `onComplete` 동작 → `outroDialogue` → `next`(깃발 `branchFlag` 이 켜져 있으면 `branchNext`).
  - 목표 `QuestObjectiveType`: **Talk**(QuestNPC.npcId = targetId 에게 걸어서 F, 대사·선택지 `choices` → `choiceFlags` 깃발) / **SinkShips**(faction 배를 플레이어가 count 척 — `ShipHealth.OnAnyDied` 중 lastAttackerFaction = Pirate; `spawnTemplates` 가 있으면 살아 있는 띄운 배가 keepAlive 보다 적을 때 2 s 마다 더 띄움, `marker` 가 있으면 플레이어 대신 그 마커 둘레, `ignoreTerritory` 기본 켬) / **Reach**(QuestMarker 반경 안 — 배든 걷는 캐릭터든 수평 거리) / **BountyAtLeast·AtMost**(현상금 단계) / **Escort**(2026-10-09: `escortTemplate` 원본(퀘스트는 `merchant`)을 `marker` 자리에 `escortFaction` 진영으로 띄워 MerchantAI 목적지 모드로 targetId 마커까지 보냄, 8 s 뒤부터 호위 배 둘레 spawnDistance 에 습격 배를 count 척까지 keepAlive 척씩, 호위 배가 가라앉으면 "다시" 알림 뒤 6 s 에 목표를 처음부터) / **Follow**(2026-10-09: QuestTag(targetId) 의 QuestWalker — 플레이어가 followStartRange 30 m 안에 오면 출발, radius 45 m 보다 멀면 "놓쳤습니다", minDistance 5 m 보다 가까우면 "들켰습니다" → 처음 자리에서 다시, 끝에 닿으면 `dialogue` 를 자막으로) / **Defeat**(2026-10-09: QuestTag(targetId) 의 SoldierAI 가 모두 쓰러짐, 그 병사는 questNoRespawn).
  - 동작 `QuestActionType`: Message(알림) / SetBountyLevel(text "atleast" 면 낮을 때만 올림) / SpawnShips(원본·수·거리, `marker` 둘레, `ignoreTerritory`, `factionOverride`) / SetFlag / GiveGold / **SetTagActive**(QuestTag 켜기·끄기 — 병사면 questHidden 도) / **TeleportPlayer**(걷는 플레이어를 마커 자리·방향으로, 화면 0.5 s 어둡게 → 옮김 → 0.7 s 밝게; 궁궐 구역(PalaceVista zoneMin~Max) 안이면 궁을 먼저 켬; 배에 타고 있으면 안 함) / **SoldierAggro**(QuestTag 병사의 aggroOnSightRange = distance, value 1 이면 바로 덤빔) / **Subtitles**(lines 자막).
- `QuestManager`: 한 번에 퀘스트 하나. `autoStart` 면 시작 4 s 뒤 `startQuest`(P-1), 퀘스트 끝나고 4 s 뒤 다음. Start 에서 `QuestTag.startHidden` 인 것을 모두 끔. 격침 목표가 찬 프레임에는 끝내지 않고 다음 Update 에서 끝냄(§6 퀘스트). 대화 = 걸어서 `talkRange`(기본 3.5 m) 안 가장 가까운 QuestNPC 에 **F**(E 는 승·하차 등 3초 누르기와 겹쳐서 Claude 가 정함), 대화 중 F = 다음 줄, 4 m 넘게 멀어지거나 배에 타면 대화를 접음. 지도 목표 = 대화 상대·도착 마커·띄운 배·호위 배·미행 대상·제압 대상. 목표가 끝나 남은 퀘스트 배는 1000 m 밖이면 지움(끝난 호위 배 이름 `Quest_Escort_Done`). **시험용 F8 = 지금 목표 바로 끝냄**(`debugCompleteKey`). **저장/불러오기 없음** — Play 마다 처음부터.
- `QuestUI`(같은 오브젝트, UIKit, 캔버스 `QuestHUD` 정렬 110): 오른쪽 위 추적(막·제목, ◆ 목표 + 격침 n/m·현상금 단계·호위 배 체력 %·미행 거리·남은 제압 수, 목표까지 거리), 위 가운데 알림, 아래 가운데 자막(3.5 s + 글자당 0.04 s), 대화창(980 × 150, "F 다음 ▶"), 선택지 버튼(마우스, 그동안 커서 보임), "F 대화 — 이름 ◆" 안내, 맨 위 검은 화면(`FadeTo` — 장면 옮김). 전체화면 메뉴·지도가 열리면 숨김.
- `QuestNPC`(npcId, displayName, talkRange, idleLines) — 대화 중 플레이어 쪽으로 돎. `QuestMarker`(markerId, displayName, radius — 회전은 TeleportPlayer 방향). `QuestTag`(id, startHidden — 같은 id 여럿 가능, 꺼진 것도 찾음). `QuestWalker`(NavMeshAgent + Villager 컨트롤러 Speed, points 를 차례로 1.1 m/s, 짝수 점에서 2.5 s 멈춤).
- **데이터**: 1막 `Editor/QuestAct1Builder.Build()` → `09.Quest/Act1/`(P-1 왜구 습격 → P-2 관군의 오인 추격 → 1-1 숙부와의 재회 → 1-2 해월도 현감 → 1-3 왜구 토벌 → 1-4 구로세지마 잠입 준비). 2막~에필로그 `Editor/QuestStoryBuilder.Build()`(1막도 다시 만들고 1-4 → 2-1 을 이음) → `Act2·Act3·Act4·Epilogue/`:
  - 2막 구로세지마: 2-1 잠입(상인 거리 도착 → 하야시) → 2-2 지주의 부탁(왜구 3척, 금화 200) → 2-3 의심받는 손님(성에서 영주 → 항구 앞바다 → **수송선 호위** 구로세_호위출발 → 구로세_북서포구, 습격 세키 3척 → 항구의 영주, 금화 200) → 2-4 불타는 어촌(상인 거리 불 켬, 아군 안택선 2척, 항구 앞 왜구 4척 → 침몰선 살펴보기(조선식 화포 자막) → 영주) → 2-5 은을 받는 가신(영주 → **가신 미행**(성 오름길 아래 (1614, −1089) → 사찰 앞, 끝에 정체 모를 조선인과의 대화 자막) → 영주) → 2-6 왜구의 소굴(영주 → 아군 안택선 2척 + 왜구섬 앞바다 왜구 4척 → 상륙 → **두목 제압**(두목 숨기고 소굴에 영주 등장) → 영주) → 2-7 왜구의 배후(해월도 관아 인장 문서) → 2-8 암살 밀서(전령, **선택지 ①**: kill_lord → 2-8A 영주 암살(성의 갑주 영주 병사 제압, 일본 현상금 5단계 → 해월도 현감 보고, 금화 500) / 아니면 2-8B 명령 불복(관군 현상금 2단계, 숙부댁 하인)).
  - 3막 망운도: 3-1 유배 죄인 감시(망운도 도착 → 관아 관리) → 3-2 약초(한윤겸 → 골짜기 약초 자리 → 한윤겸) → 3-3 옛 동료(해적선(왜구 배) 2척) → 3-4 제삿날(중섬 선착장 도착 → 관군 현상금 1단계 이상·판옥선 2척 → 망운도로 돌아가기 → 제사 대화) → 3-5 무력 시험(왜구 3척) → 3-6 봉인된 서신(끝에 관군 현상금 0).
  - 4막 한양: 4-1 한양 입성(한양 선착장 → 나룻배로 육조거리 권시헌 — "현왕이 네 아버지를 죽였다") → 4-2 입궐(권시헌 → 광화문 앞 도착 → 근정전 마당으로 옮김 → 내관 → 홍 대비) → 4-3 경회루 회합(경회루 동쪽으로 옮김 → 권시헌·반란파) → 4-4 거사(대비전 불, 근정전 마당으로 옮김, **궁궐 호위 4명 제압** — 보자마자 덤빔 45 m) → 4-5 강녕전의 대면(강녕전 마당으로 옮김 → 등 돌린 왕, **선택지 ②**: kill_king → 4-5A 시역(관군 현상금 5단계, 육조거리 관군 보자마자 덤빔 40 m, 광화문 앞으로 옮김 → 한양 선착장으로 달아나기) / 아니면 4-5B 왕의 고백(대비의 독살 → 왕이 스스로 목숨을 끊음, 광화문 앞으로)).
  - 에필로그: E-1 새 왕(자막 — 종친 옹립, 묘호 민종 → 한양 선착장) → E-2 생모의 유서(망운도 한윤겸 — 숙의 정씨 유서, 권시헌의 거짓말 → "해신 — 끝").
- 확인(Play, 2026-10-07 1막 / 2026-10-09 새 목표·동작 — **모두 메서드 호출·순간 이동으로**): 1막 P-1~1-4 전체 흐름(2026-10-07). 2026-10-09: 호위(수송선이 북서쪽으로 4.5 m/s, 습격 세키 2척 260~290 m 에 생김, 목적지 가까이 옮기자 목표 완료·항구 영주 켜짐·성 영주 꺼짐, 호위 배를 가라앉히자 "다시 준비 중" → 6 s 뒤 새 호위 배), 미행(30 m 안에서 출발 1.1 m/s·Speed 1.0, 80 m 밖으로 가자 처음 자리로, 마지막 구간에서 도착 → 다음 목표), 2-6 아군 안택선 276·287 m·왜구 129·158 m 가 왜구섬 앞바다 마커 둘레에 생김, 두목 결정타 → 제압 완료(두목 숨김, 소굴 영주 켜짐), 2-8 선택지 0 → 2-8A(성 영주 꺼짐·갑주 영주 aggro 8), 4-2 광화문 앞 도착 → 근정전 마당(GJ_RR 10.95)으로 옮김·궁 켜짐·궁 NPC 4명, 4-4 호위 4명 Hostile → 결정타 → 4-5 강녕전 마당(왕까지 5.6 m, "F 대화 — 현왕 이담") → 선택지 1 → 4-5B → E-1, 4-5A 관군 5단계·육조거리 병사 6명 덤빔. 콘솔 오류·경고 0. **실제 키보드·마우스 플레이·2막~에필로그 전체 연속 진행은 안 해 봄.**

## 4. 월드 (SampleScene)

### 씬 루트
`panok`(플레이어 배) · `anteck`/`seki`/`panok_navy`/`merchant`/`anteck_wokou`/`seki_wokou`(스포너 템플릿) · `QuestSystem`(§3 퀘스트) · `해월도_Haewoldo`(§4 해월도) · `왜구소굴_WokouDen`(§4 왜구섬) · `구로세지마_Kurose`·`망운도_Mangundo`·`한양_Hanyang`·`궁궐호위_PalaceGuards`(§4 퀘스트 배치 2막~4막) · `EnemyFleetSpawner` · `WeatherSystem`(+WeatherTestKeys, LightningStorm, SeaAmbientAudio) · `WaterDecalLOD` · `ReputationSystem`(+ReputationGaugeUI) · `WaterFX` · `Ocean` · `BaseGroup`(Base 터레인, Cave, Light, 선착장) · `Sky and Fog Global Volume` · `CaveInteriorVolume` · `CaveReflectionProbe` · `Adaptive Probe Volume` · `PlayerRespawnPoint` · `LandingPoint_Dock` · `Boarding` · `MapSystem` · `GameData` · `ShipMenu` · `InventoryMenu` · `나룻배_Ferry`(§4 호수·나룻배) · `궁궐원경_PalaceVista`(§4 궁궐 원경 그림) · 비활성: `Sphere`(포탄 원본), `Explosion_v1`, `BuoyancyObjects`.

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
- **섬 선착장 땅 높이(2026-10-07 사용자 지시 "모든 지역 선착장 땅 높이 맞춰주기")**: `Editor/DockRampTool.cs` `ApplyAll(9, 3.6, 4, 40, 2)` — 부모 `Dock` 이 있는 LandingPoint 6곳(Wokou_Den·Joseon_Small_A/B·Joseon_Large·Joseon_Mid·Japan_Castle; 베이스 `LandingPoint_Dock` 은 부모가 없어 제외)에서 판자 육지 끝 높이를 재고, 판자 폭 따라 터레인을 [판자윗면 − 0.08 ∓ 거리 × tan 9°] 사이로 눌러 경사로를 만듦(가장자리 섞임). 백업 `Captures/terrain_backup/<터레인>_dock_20261007_213907.bin`. 바뀐 터레인 위 NavMesh `한옥마을_사람`·`일본마을_사람` 다시 구움(GUID 유지 방법 §7).
- **하차 시 배 밀착(2026-10-07 사용자 지시 "선착장으로 내리면 배가 선착장에 밀착되게 하여 다시 탈 수 있게", 이어서 "밀착되는 부분 배의 측면으로")**: `BoardingSystem.PlaceShipAtBerth` — `LandingPoint.berthAtPierHead`(기본 true)면 **T 자**: 배를 판자와 직각으로(뱃머리는 지금 가까운 쪽), 배 옆구리 가운데를 판자 바다 끝(Dock 로컬 x `pierSeaEndX` 11.5)에서 `berthGap` 0.6 + 배 반폭 떨어진 곳(z = 판자 가운데)에 둠. 배 밑 7 × 3 점 터레인이 `maxSeabedY` −2.5 보다 높으면 바다 쪽으로 1 m 씩. false 면 판자 옆에 나란히(예전 방식). 부모 없는 LandingPoint 는 안 옮김. 그 뒤 `SetDocked(true)`. **다시 타기 = 배 중심 `boardRange`(씬 12) 안 또는 선체(볼록 충돌체)에서 `boardHullRange` 9 m 안**. 확인(Play, 6곳): 배 Dock 로컬 (18.4, ·, 0.6), 판자 끝 ↔ 선체 박스 충돌체 1.2~1.65 m, 하차 지점(로컬 x 6)에서 선체까지 7.04 m, 9 s 뒤에도 그대로, CanAct true, Board() 됨, 캡처로 옆구리가 판자 끝에 붙은 것 확인.
- 리스폰 `PlayerRespawnPoint` (−110, 0.3, −4) + `BaseSafeZone` 반경 350.

### 새 섬 (`Islands`, 생성기 `Editor/IslandTerrainGenerator.cs`, 메뉴 HaeSin/섬 터레인 생성)
- 섬마다 Terrain 1개. 위치 = `IslandPlan` 아래 계획 큐브(비활성)의 XZ 중심 — 큐브를 옮기고 메뉴를 다시 실행하면 그 자리에 같은 모양으로 다시 만듦(TerrainData 덮어씀, 시드 고정). 타일 = 육지 + 사방 200 m(해저가 가장자리에서 −45 m), 높이 범위 −60~+240, 헤이트맵 1025, 알파맵 1024, 레이어·머티리얼·설정은 Base 터레인과 같음. TerrainData = `02.Resources/00.Component/08.Environment/Islands/Island_*.asset`.
- 섬(사용자 배치, 계획 크기 / 모양은 둥근 사각이라 실제 육지는 약 80 %):
  - `Joseon_Large` 1000 (−2828, 4051): 남쪽 평야 약 8 m, 가운데 남쪽 궁터 10 m 평탄, 북쪽 산줄기(최고 약 150 m), 동남 구릉, 남쪽 만.
  - `Joseon_Mid` 800 (1185, 2728): 해안 평지 4~9 m + 서쪽 구릉(약 75 m, 아랫자락 4 m 계단), 남쪽 만. (용도는 사용자 미지정 — 임의로 해안 마을형)
  - `Joseon_Small_A` 600 (−1780, 176): 북서쪽 뾰족한 산 약 150 m + 서쪽 어깨 능선, 남동 중턱 계단식 산간 마을(5 m 단).
  - `Joseon_Small_B` 600 (−962, 1619): 두 봉우리(약 115 / 95 m) 사이 골짜기 마을(바닥 10~18 m, 4 m 단), 남쪽 만.
  - `Wokou_Den` 400 (149, −2120, 계획 큐브 `Plan_Wokou_400`, 2026-10-07 — §4 왜구섬): 일본섬 남서쪽 무인도, `GenerateOne("Wokou_Den")` 로 이 섬만 만듦.
  - `Japan_Castle` **1100** (1751, −1195, 계획 큐브 `Plan_Japan_1100`): 800 은 실제 땅이 약 0.5 km² 라 좁다는 지적으로 키우고 만을 줄임(땅 약 1.0 km²). 성하 마을 평지 4~7 m + 가운데 성 언덕, 북서쪽(base 쪽) 작은 만 — 처음 생성본. 지금 모양은 아래 "일본섬 지형"(사용자 조각 → 2026-10-04 동·남쪽을 바다로, 높이 0.5 배).
- **섬 선착장**(생성기가 만듦, 섬마다 `Dock`): 섬 중심에서 `dockAngle` 방향 해안선을 찾아 8 m 안쪽을 발치로, 발치 주변을 1.8 m 로 평탄화 + 육지 쪽으로 경사 약 9°·폭 12 m 길을 둑에 깎음(만 안쪽 해안이 가팔라서). Base 선착장을 복제(바다 쪽 경사로 Ramp_Upper 제거, 로컬 +x = 바다 쪽), `LandingPoint_<섬>`(dockRange 35), `HarborMooring`(선착장 배: 조선 = panok_navy [+ 큰·중간섬 merchant], 일본 = anteck + seki).
- **선착장 배는 앞바다를 돎**(2026-10-05, 사용자 지시 "멈춰 있고 내리는 걸 방해 — 근처를 돌게, 부딪히면 밀려나게"; 예전엔 선착장 바로 옆에 kinematic 으로 세워 둠): 플레이 시작에 `HarborMooring.FindCircle` 이 선착장 로컬(+x = 바다 쪽) 거리 80~260 m · 옆(로컬 z, +z 쪽 먼저 — 예전 정박선 자리, 플레이어 배 자리는 −z) 가운데에 반지름 45 → 35 m 원을 찾음: 원 둘레 ± 14 m 36방향이 모두 깊은 물(위에서 쏜 레이가 리지드바디 없는 충돌체를 y −5 위에서 맞지 않음), 하차 지점(LandingPoint)에서 60 m 밖, 선착장에서 곧게 나가는 뱃길(로컬 z 0)에서 25 m 밖(없으면 뱃길 위 원, 그것도 없으면 예전 정박 + 경고). 결과(측정): 다섯 곳 모두 반지름 45, 가운데 로컬 (80, ±85)(작은섬 B 만 (100, 70)), 원↔하차 지점 67.7~72.2 m, 원↔뱃길 25~40 m. 배는 원 위에 고르게 놓이고 `HarborPatrol`(07.Environment)이 반시계로 2.5 m/s 로 돎 — EnemyShipBase 와 같은 힘 기반(Rigidbody kinematic 아님, 저항 직접 계산, 옆미끄러짐 억제 `lateralGrip` 1.2 — AI 배 3~3.5 보다 작아 더 잘 밀림), 원 위 30 m 앞 점을 따라가 밀려나도 돌아옴, 앞(±40°) 45 m 안에 다른 배가 있으면 추진을 멈춤, 침몰 중·완파면 아무것도 안 함. 부력 Physics 모드, 노 젓기 켬. AI(이름이 `AI` 로 끝나는 것 포함)는 끄고, 선원은 `combatEnabled = false`.
- **선착장 배 포격(2026-10-05, 사용자 지시 "선착장 근처 배가 대포를 안 쏨")**: `HarborPatrol` 이 0.5 s 마다 110 m(`engageRange`) 안 가장 가까운 적대 배를 `Target` 에 둠(`ShipFaction.FindNearestHostile` — 현상금·영해 규칙 그대로, 0단계면 안 쏨) → 그 배의 `EnemyCannonControl`·`NavyCannonControl` 이 AI 가 꺼져 있으면 `HarborPatrol.Target` 을 표적으로, 대포 사거리(왜군 90 · 관군 95 m)·현측 55° 안일 때 일제사(원을 계속 돌며 현이 맞을 때). 세키부네·상선은 대포 없음. 확인(Play): standing +80(관군 적대) 큰섬 선착장 판옥선 약 20 s 에 3번 일제사(플레이어 배 300 → 267), standing −80(일본 적대) 일본섬 안택선 2번(267 → 252). 원을 못 찾아 정박하는 배는 예전처럼 포격도 끔.
- 확인(Play, 2026-10-05, 포격 넣기 전): 8척 모두 50 s 뒤 2.50 m/s 로 원(실제로는 안쪽 약 6 m — 앞 점을 따라가 현이 짧아짐) 위를 돎, 진행 방향 오차 3°. 밀기: 플레이어 배(속도 약 5 m/s)로 큰섬 선착장 판옥선 옆구리를 받자 그 배가 옆으로 5.3 m/s 로 밀려 원 안쪽 23.5 m 까지 갔다가 원으로 돌아옴(옆으로 기울기 7.5°). 돌아다니는 관군 판옥선(NavyPanokAI, `lateralGrip` 3.5)도 옆구리를 받으면 약 4 s(게임 시간)에 7.6 m 밀림 — 따로 밀기 코드는 없음(원래 힘 기반 물리). 콘솔 오류·경고 0. 씬에 저장된 `HarborMooring` 5곳 값(radii 45·35, 하차 지점 60, 뱃길 25)은 Claude 가 넣음 — 씬 저장은 사용자.
- 측정: 섬 선착장에서 하차 → 발치 1.8 m → 길로 섬 위까지 걸어 올라감(작은섬 A 확인). 길 최대 경사 13~25°.
- **섬 생성기 메뉴는 비활성화**(사용자 지시 — `[MenuItem]` 주석 처리). 섬 지형은 손으로 다듬는 중이라 다시 돌리면 덮어써짐.
- **일본섬 지형(2026-10-04 다시 만듦, 사용자 지시 "구조를 바꿔서라도 최적화")**: 사용자가 씬 뷰 그림에 빨간색으로 칠한 동·남쪽(동쪽 저지대·성 길·검문소 둔덕·기와민가 고원·신사 고원·남쪽 큰길)을 바다로 가라앉힘(사용자 캡처를 같은 씬 뷰 카메라 렌더와 맞춰(배율 1, 위치 차 63·8 px) 칸마다 화면 위치로 판정, 경계는 6 칸 흐림 + 저주파 굴곡). 해저 = −(1.5 + 남는 땅에서 거리 × 0.35), 최대 −40. 남는 땅 가장자리는 해안 비탈(20 m 안 0.21 기울기, 그 뒤 1.2). 높이 5 m 위는 **0.5 배**(5 + (h − 5) × 0.5 — 사용자 선택 "성 고원 약 95 m"). 땅 약 0.64 → 0.25 km²(서쪽 저지대 + 가운데 언덕). 성 고원(82 × 82 m 정사각형, 월드 x 1762.7~1844.8 · z −1010.4~−928.4)은 **94.71 m** 평면, 둘레는 서쪽 67.4 m 단으로 떨어지는 벽. 가라앉은 곳은 해저 칠(레이어 0·1), 새 바닷가 0~3 m 는 모래(레이어 2). 되돌리기: `Captures/terrain_backup/Island_Japan_Castle_full_before_RESHAPE.f32`(헤이트맵) · `…_alpha_full_L8_before_RESHAPE.f32`(알파맵 1024² × 8 float32 [z, x, 레이어]). 계산 스크립트 = Claude 작업 폴더(옮기지 않음), 결과 높이 = `Captures/jptown/hm_reshape.f32`.
- **신사 고원(2026-10-04 다시 만듦, 사용자 지시 "신사 있는 곳을 올려 주고 길은 입구 앞을 지나가게")**: 서쪽 비탈 월드 x 1668~1707 · z −1102~−1062 를 **44.0 m 평면**으로 올림(예전 38.0 m 터, 자연 땅보다 서쪽 끝에서 약 11 m 높음). 둘레(2026-10-04 고침, 사용자 지시 "돌 축대 만드세요") = **돌 축대**(아래 `JP_Shrine_Ishigaki`): 축대 면 = 고원 가장자리에서 74.5° 로 기울어 내려감(성 석축 서쪽 면에서 잰 기울기), 높이 최대 12 m, 그 아래는 45° 흙 비탈로 자연 땅까지(높이 거리 = 직사각형에서 체비쇼프 거리 — 모서리 각짐). 축대 뒤 땅은 축대 면 − 0.5 m(가려짐). 깎는 쪽(동쪽 언덕)은 50° 비탈. 축대 높이(측정): 서쪽 약 12.3 m, 남쪽 6~12 m, 북쪽 0~11 m, 동쪽 0~5 m. 고원 위 풀 칠(레이어 3·4), 바뀐 땅 중 35° 넘는 곳은 절벽 칠(7). 본전 (1687.5, 43.95, −1086) **회전 0(북쪽 = 길 쪽을 봄)**, 도리이 (1688, 41.72, −1046.5) 회전 0 = 길 남쪽 가장자리, 거기서 고원 북쪽 끝까지 폭 5 m 흙 경사로(17.6 m, 2.5 m 오름, 8°, 둘레 섞음 0.8 m) — 북쪽 축대는 x 1685~1691 에서 끊기고 끝을 막음.
- 길(폭 6 m, `Road_dirt_gravel` 칠): **마을 → 성 오름길 한 줄**(2026-10-04) = 큰길 동쪽 뒷길 (1614, −1089) → 북동으로 비탈을 비스듬히 오름 → 신사 고원 북쪽(도리이 앞, 길 높이 41.5 m)을 지남 → (1712, −1030) → 예전 성 오름길 (1702, −985) 에서 이어짐 → 성문 (1762.7, −969.4, 94.71). 새로 깐 구간 167 m, 최대 25°(땅 높이 5 m 흐림 + 끝점 고정 + 앞뒤 경사 제한, 길 밖 max(5 m, 높이 차) 에 걸쳐 섞음). (1702, −985) → 성문 구간(성문 앞 약 26 m 흙 경사로 포함)은 예전 그대로. 예전 참배길(큰길 → 신사 터)·신사 터를 지나던 오름길은 지움(뒷길 동쪽 x > 1598 은 자연 땅으로 되돌림, 길 칠은 이웃 칠로). 집 발밑(9 m 안)은 안 바꿈(측정 0). 캐릭터 slopeLimit 50° 안. 계단·돌 경사로 모델은 없음.
- 되돌리기: `Captures/terrain_backup/Island_Japan_Castle_full_before_SHRINE2.f32`(헤이트맵 1025² float) · `…_alpha_full_L8_before_SHRINE2.f32`(알파맵 1024² × 8 [z, x, 레이어]). 바꾼 칸 = 헤이트맵 i 408~507 · j 557~672, 알파맵 i 416~508 · j 570~673. 계산 스크립트 = Claude 작업 폴더(`shrine3.py`(땅·축대 높이 `wall_G`)·`shrine3_paint.py`, 옮기지 않음). 축대 뒤 바꾼 칸 = 헤이트맵 i 408~507 · j 557~672, 알파맵 i 416~508 · j 559~673. 바닷가는 안 바뀜(`WaterShoreMask` 다시 굽지 않음).
- **신사 돌 축대 `JP_Shrine_Ishigaki`**(2026-10-04, `일본마을_JapanTown/Shrine` 아래, 프리팹 `08.Environment/JapanTown/ShrineWall/JP_Shrine_Ishigaki.prefab`, 위치 (1687.5, 44, −1082)): 성 석축과 같은 방식 — LOD0 `SHRINE_WALL_LOD0` = 보로노이 칸 돌 1,208개(약 1.2 m, 돌마다 앞면·모따기·옆면, 앞으로 9~15 cm 튀어나옴, 재질 성의 `Ishigaki_Natural_Stone_1~7` 무작위) + 돌 뒤 줄눈 면(`Ishigaki_Shadowed_Stone_Joints`, 땅속 0.6 m 까지) + 윗면 갓돌(`Ishigaki_Worn_Step_And_Coping_Stone`, 가장자리에서 안쪽 1.6 m, 위 44.12, 1.1~1.5 m 마다 끊음) = 34,284 삼각형 / LOD1 `SHRINE_WALL_LOD1` = 줄눈 면 + 갓돌(2,068 삼각형)에 LOD0 를 구운 그림(2048 페이지 `ShrineWall/IMP_JPShrineWall_P0_BC/NOS.png`, 재질 `IMP_JPShrineWall_P0` = 성 임포스터 재질 복사, 벽 약 39 px/m, 광선 맞음 100 %). 전환 60 m, 2,500 m 에서 끔(CrossFade). 충돌체 = LOD1 메시(`Collider` 자식). 재질은 성 재질(`JapanCastle/Materials/`)을 그대로 씀(삼면 투영). 다시 만들기: `shrine3.py` → `98.Blender/Impostor/shrine_ishi.py`(돌·껍데기) → `shrine_ishi_bake.py`(굽기 1분, 결과 `Captures/jptown/shrine_wall/out/`) → `PalaceLodBinImport.ImportMeshes(out, "…/JapanTown/ShrineWall", 60, "", "JPShrineWall.bin", "Meshes")` → `RecalculateTangents`. 캡처(마을 쪽 아래, 남서 모서리, 길·도리이 쪽, 위)로 확인. 가까이 모습·실제 프레임 사용자 확인 필요.
- `JapanTownPlan`(씬 루트, 임시 큐브): 지금 켜진 것은 절(목탑) 자리 판 `TemplePlot_목탑사찰자리`(사용자가 서쪽 땅 (1400~1470, −1104~−1014)으로 옮김)뿐. 일반·기와민가·상인구역·신사 상자, 검문소↔성 다리 `CastleBridge_검문소-성` 은 끔.
- `CastlePlan`(씬 루트, 임시 큐브, 꺼짐): 히메지 대천수 기준 크기 확인용(돌 기단 27×25×15 m, 전체 46.5 m — 기단·건물 나눔과 망루·어전·흙담 크기는 어림값).
- **일본성 `일본성_JapanCastle`(씬 루트)**: 위치 (1803.735, **94.7075**, −969.414) = 성 고원 윗면 가운데, **회전 180°**(2026-10-04 — 성문이 서쪽 = 마을·신사 쪽을 보게, 사용자 선택). 자식 `JapanCastle`(프리팹 `08.Environment/JapanCastle/Prefabs/JapanCastle.prefab`, FBX `Meshes/JapanCastle.fbx` — 외부에서 만든 모델) = 혼마루 석축 둘레(높이 10.5 m)·성벽 위 통로·성문(문짝 2, 원점 = 경첩)·천수(돌 기단 + 5겹 지붕, 안 1~5층 바닥·벽·기둥·계단)·망루 2(2층)·바깥 돌계단. 렌더러 99, 삼각형 699,964(측정). 성문 문턱 = 고원 서쪽 끝(x 약 1763~1770, z −972~−966)에 성 오름길 끝이 닿음(캡처 확인).
  - **모델 고침(2026-10-04, 사용자 지시 — 원본 모델 파일을 고쳐 FBX 를 다시 가져옴, 바뀌지 않은 메시 88개는 이전 FBX 와 정점 합이 같음 확인)**: ① 성벽 북서 모서리(서쪽·북쪽 흰 담이 0.73 m 비어 있던 곳)에 흰 벽·기와 덮개·모서리 마루·통로 갓돌을 채움(`Perimeter_North_Walkway_And_White_Parapet` 안). ② 망루 계단 `JC_Yagura_NE/SW_Stairs_25_Proper_Risers` 를 마당 쪽으로 2 m 빼고 계단 위에 참(약 3 × 2 m, 높이 5.04 — 성벽 위 통로 5.08~5.11)을 붙임 → 계단 위에서 동쪽(NE)·서쪽(SW) 성벽 통로로 걸어 들어감. ③ 천수 안 계단 4개(`JC_Keep_Interior_Stair_*`)를 위 끝은 두고 남쪽으로 늘림(1→2층 6.4 m·41°, 2→5층 5.3 m·42°, 예전 3.7 m·57°), 위층 바닥·아래층 천장 구멍을 남쪽으로 0.7 m 넓힘(구멍 x 28.25~29.75·y −28.3~−23.9, 모델 좌표 — 천장·바닥·`JC_Tenshu_F2~F5_Walls`·`JC_Keep_F1` 의 해당 면 자름, 구멍 둘레 막음 면 추가), 계단 발치를 막던 난간(2~4층, 계단 앞 1 m 높이 가로대)·4층 기둥·4층 들보 가운데·4층 계단 앞 작은 방 문틀과 벽을 지움, 2층 궤짝을 0.3 m 옮김, 5층 난간을 새 구멍 가장자리로 0.5 m 옮김.
  - 걷기 시험(2026-10-04, CharacterController r 0.32 · h 1.75 · stepOffset 0.4 · slopeLimit 50 — 해신 OnFootRoot 와 같은 값, 편집 모드에서 `Move` 를 1/60 s 씩): 천수 1층 → 5층·5층 → 1층 모두 걸어서(2.69 m/s)·달려서(4.06 m/s) 통과, 망루 NE·SW 마당 → 계단 → 참 → 성벽 통로 통과. 실제 캐릭터(애니메이션·발 IK)로는 안 해 봄 — 사용자 확인 필요. 망루 문(계단 위, 문짝 면 y +31.14)은 막혀 있어 망루 안으로는 못 들어감(원래 모델 그대로).
  - 재질(`Materials/`, HDRP/Lit 25개): 석축·지붕·회벽 등 15개는 원본 모델 재질의 색 띠 두 색 사이를 잡음으로 섞은 반복 텍스처(석재 4 m·1024, 그 밖 2 m·512, 노멀 포함)를 **삼면 투영(월드 좌표)** 으로 씀 — 원본은 잡음 셰이더라 그대로 옮길 수 없어 색·거칠기만 맞춘 근사. `MI_JC_Plaster/DarkWood/WoodDoor` 는 UV0 텍스처, 나머지 7개는 단색. FBX 가져오기 = 재질 설명 + 이름으로 `AddRemap`.
  - 충돌체: 지붕·서까래·박공·샤치호코·용마루·처마 받침 빼고 MeshCollider 71개. Static(Batching·Occluder·Occludee·ContributeGI·ReflectionProbe), ReceiveGI LightProbes.
  - **LOD(2026-10-04)**: 프리팹 루트 아래 부분 6개(원래 자식 99개를 이름으로 옮김 — `TENSHU` = `JC_Tenshu_*` + `JC_Keep_F1`, `YAGURA_NE`·`YAGURA_SW` = `JC_Yagura_*`, `WALL` = 석축·둘레 껍데기·`Perimeter_*`, `INTERIOR` = `JC_Interior_*`·`JC_Keep_Interior_*`, `GATE` = `West_Gate_*`), 부분마다 LODGroup(CrossFade, 전환 폭 0.1). 충돌체는 원래 자식 그대로(LOD 와 상관없이 켜짐).
    - TENSHU·YAGURA: LOD0 원본 / LOD1 `<부분>_LOD1` = 서까래·처마 받침(`Exposed_Rafters`·`Roof_Brackets`)과 기와(`Tenshu_Charcoal_Verdigris_Kawara`·`Tenshu_Raised_Kawara_Ribs`)를 빼고 나머지 줄임(천수 5 cm·망루 3 cm) + 기와 윗면 판(0.25 m 격자, 위·아래 두 장 — 아래 장에 서까래가 그림으로 들어감) / LOD2 `<부분>_LOD2` = **LOD1 을 더 줄인 것**(2026-10-04 — 나머지 천수 25 cm·망루 15 cm, 기와 판 15 / 10 cm, 둘 다 이음매 넘어 접기 + 작은 조각 없앰, 재질은 LOD1 과 같은 12개; 예전 지붕 모양 상자는 천수 1층 처마·벽이 빠져 멀리서 1층이 비어 보였음). 전환 = **천수 약 50 / 220 m**(2026-10-04, 예전 80 m — 원본 흰 서까래가 중간 거리에서 지글거려 당김), 망루 약 45 / 130 m.
    - WALL: 석축 돌 `Ishigaki_Continuous_Connected_Stone_Facades` 는 따로 떨어진 돌 3,477개(돌마다 32 삼각형)라 줄여지지 않아, 뒤에 있는 280 삼각형 둘레 껍데기 `Castle_Integrated_Ishigaki_Keep_Yagura_Wall_Ring` 에 돌 그림을 구워 씀 — LOD1 = 껍데기(그림) + 성벽 위 통로 바닥·흰 담 원본 / LOD2 = 껍데기 + 흰 담 줄임(15 cm, 통로 바닥 뺌). 전환 약 100 / 260 m.
    - 끄는 거리: 천수·석축 4,000 m, 망루 3,000 m, `INTERIOR` 45 m(실내 가운데에서 — 밖에서는 안 그림), `GATE` 800 m.
    - 그림: 4096 페이지 2장(`JapanCastle/LOD/IMP_JPCastle_P0·P1_BC/NOS.png`, 재질 `IMP_JPCastle_P0·P1`), 광선 맞음 96.8·94.3 %. 원래 재질 대부분이 삼면 투영이라 굽기 장면에서만 그 삼각형 UV 를 면 방향 투영 × 배율(석재 0.25·나머지 0.5)로 바꿔 그림을 읽음. 메시 `JapanCastle/LOD/Meshes/`(탄젠트는 가져온 뒤 계산).
    - 삼각형(LOD0 / LOD1 / LOD2, 측정): 천수 336,546 / 74,037 / 22,196 · 망루 각 81,362 / 약 14,800 / 6,511(NE)·6,575(SW) · 석축 143,364 / 26,680 / 4,408 · 실내 53,878(45 m 밖 0) · 성문 3,452.
    - 다시 만들기: 프리팹을 빈 추가 씬 `__VIS_HOLDER` 아래 원점에 놓고 `PalaceBinExport.Export("Captures/jptown/castle_lod/src")` → 텍스처 `tex/index.tsv` 와 함께 `98.Blender/Impostor/jc_lod.py`(약 10분, 결과 `Captures/jptown/castle_lod/out/`) → `PalaceLodBinImport.ImportMeshes(out, "…/JapanCastle/LOD", 60, "", "JPCastle_LOD.bin", "Meshes")` → 페이지 PNG·재질(집과 같은 설정) → LOD 메시 `RecalculateTangents` → 부분 오브젝트에 `_LOD1`·`_LOD2` 자식과 LODGroup(같은 이름이면 지우고 다시). 비교 그림 `jc_cmp_lod.py`. 그 뒤 LOD2: `98.Blender/Impostor/jc_lod2.py`(`JPCastle_LOD.bin` 의 `_LOD1` → `JPCastle_LOD2.bin`, 1분 안) → `ImportMeshes(out, …, 60, "TENSHU_LOD2,YAGURA_NE_LOD2,YAGURA_SW_LOD2", "JPCastle_LOD2.bin", "Meshes")`(같은 에셋에 덮어씀) → `RecalculateTangents` → 프리팹 `_LOD2` 렌더러 재질 = `_LOD1` 렌더러 재질.
    - 모델 고친 뒤(2026-10-04) 전부 다시 구움(`src2` → `out2`, 페이지 2장 광선 맞음 96.8·94.2 %), 삼각형 LOD1/LOD2: 천수 74,085 / 22,233 · 망루 14,784·14,915 / 6,521·6,578 · 석축 26,728 / 4,460. 재질 순서가 바뀌어 `out2/mats.json` 의 이름 순서대로 다시 넣음.
    - 확인(2026-10-04, Play 중 캡처): 천수 131 m 에서 새 LOD2 는 LOD1 과 거의 같음(1층 처마·벽 있음, 맨 위 지붕에 세로 줄무늬가 조금), 250 m(마을 쪽)에서도 같음. 성 안마당 56 m 에서 LOD0 는 처마 밑 흰 서까래가 빗살처럼 촘촘하고 LOD1 은 처마 밑이 어두운 판 — 50 m 에서 바뀌는 게 보일 수 있음. 131 m 에서 카메라를 조금씩 옮긴 8장 사이 차이(천수 부분 평균): LOD0 1.17 / LOD1 0.99, 화면 안 세밀함(이웃 화소 차) 19.5 / 14.0. 머티리얼 null 0, 콘솔 오류·경고 0. 사용자가 본 지글거림이 이것인지·전환 모습·가까이 모습은 사용자 확인 필요.
- **일본 마을 `일본마을_JapanTown`(씬 루트)**: 건물 프리팹(`08.Environment/JapanTown/Prefabs/`, FBX `Meshes/JapanTown_Buildings.fbx` — 외부에서 만든 모델, 앞 = 로컬 +Z, 원점 = 기단 바닥 가운데). 그룹 3개(2026-10-04 지형을 다시 만들며 `WealthyHouses` 그룹은 지움 — 사용자 지시 "귀족저택구역을 없애고"):
  - `CommonHouses` 129채(`Minka_###` — 번호는 처음 350채 배치 그대로, 바다에 잠긴·땅이 바뀐 212채와 참배길에 걸린 4채 등 221채를 지움): 서쪽 저지대의 길 따라 붙여 세운 거리(사용자 지시 "건물이 촘촘하게 이어져서 거리를 만들고"). 큰길(폭 12 m) = 상인 거리 끝 (1531, −975) → (1623.6, −1189.1) 방향(남쪽 끝은 바다), 큰길 양쪽 줄 + 뒷길(큰길 가운데에서 ±35 m, 폭 6 m) + 골목(폭 5 m, 약 70 m 마다). 집 = 지붕 앞끝이 길 가장자리에 한 줄, 앞이 길 쪽, 이웃 지붕 사이 0.3 m, 배율 1, 마치야 위주. 높이 = 기단 범위 터레인 최저 − 0.05 m(첫 배치 때 발밑이 기운 자리는 땅을 고름).
  - `MerchantDistrict` 22채: 선착장(1410, −854) 앞 상인 거리 양쪽, Minka_06(배율 0.92) 15 · Minka_02 7, 앞이 길 쪽.
  - `Shrine`: 본전 + 도리이(위 신사 터).
  - 길 칠(`Road_dirt_gravel`, 레이어 6): 상인 거리((1418, −862) → 큰길 시작, 폭 12 m)·큰길·집이 선 뒷길·골목·참배길·성 오름길.
  - 모델 수정(2026-10-04, 외부에서 고친 것을 FBX 로 다시 가져옴 — 지붕 등 그 밖 메시는 이전과 정점·법선이 같음 확인): 집 10종 모두 지붕과 벽 사이 틈을 막는 `<집>_Roof_Wall_Seals` 추가(1,928~9,824 삼각형), `Minka_02_Machiya`·`Minka_06_Merchant_With_Kura` 에 2층 바닥 틈 `_Floor_Seam_Closure`(48), `Noble_02` 에 `_Walkway_Join_Seal`(108), 박공 벽 `_Walls` 바뀜. 새 자식은 프리팹에 MeshRenderer + MeshCollider 로 넣음. 이전 FBX 백업 `Captures/jptown/lod/backup/JapanTown_Buildings_before_GAPFIX.fbx`.
  - **LOD(2026-10-04, 집 7종 = Minka_01~06·신사 본전)**: 프리팹 루트에 LODGroup(CrossFade, 전환 폭 0.1) — LOD0 = 원래 자식 렌더러 전부 / LOD1 `<집>_LOD1` = 원본에서 지붕 기와·초가(지붕 메시의 `JP_Kawara`·`JP_Kawara_Ridge`·`JP_Thatched_Straw`)를 빼고 나머지를 재질별로 줄인 것(3 cm, 작은 떨어진 조각 없앰) + 기와 윗면을 따라 만든 판(0.2 m 격자, 기와 꼭대기 − 2 cm, 위·아래 두 장 — 아래 장은 1 cm 내림)에 기와·처마 밑을 구운 그림(재질 `IMP_JPTown_P0`, 탄젠트는 가져온 뒤 계산) / LOD2 `<집>_LOD2` = 지붕 모양 상자(임포스터, 재질 `IMP_JPTown_P0` 하나). 7종이 4096 페이지 하나를 같이 씀(지붕 윗면 약 68 px/m, 벽 약 43 px/m, 광선 맞음 92.9 %). 전환 = LOD0 → LOD1 약 35 m, LOD1 → LOD2 약 90 m, 2,500 m 에서 끔(화면 높이 = 크기 / (거리 × 1.1547)). 도리이는 LOD0 하나, 1,500 m 에서 끔. `Noble_01~03` 프리팹(지금 씬에 없음)은 LOD 안 함. LOD1·LOD2 는 충돌체 없음(충돌은 LOD0 자식의 MeshCollider — LOD 와 상관없이 늘 켜짐).
  - 삼각형(LOD0 / LOD1 / LOD2, 측정): Minka_01 26,824 / 7,830 / 690 · 02 39,424 / 17,426 / 1,564 · 03 24,004 / 8,664 / 1,094 · 04 29,464 / 11,736 / 1,128 · 05 16,620 / 6,232 / 1,100 · 06 28,840 / 11,586 / 1,316 · 신사 16,736 / 7,446 / 1,042. LOD1 재질 수 7~11(LOD0 렌더러 6~8개 · 서브메시 14 안팎).
  - LOD 다시 만들기: 집 프리팹을 빈 추가 씬의 `__VIS_HOLDER` 아래 x = 60·i 에 놓고(렌더러 이름 앞에 `<집>|`) `PalaceBinExport.Export("Captures/jptown/lod/src")` → 텍스처 `tex/index.tsv` 와 함께 `98.Blender/Impostor/jp_lod.py`(결과 `JPTown_LOD.bin`·`P0_BC.png`·`P0_NOS.png`, 약 4분) → `Captures/jptown/lod/out/` → `PalaceLodBinImport.ImportMeshes(out, "…/JapanTown/LOD", 60, "", "JPTown_LOD.bin", "Meshes")` → 페이지 PNG 를 `LOD/IMP_JPTown_P0_*.png` 로(BC sRGB / NOS 선형, 4096, Clamp, 스트리밍 밉맵) → LOD1 메시 `RecalculateTangents` → 프리팹에 `_LOD1`·`_LOD2` 자식과 LODGroup(같은 이름이면 지우고 다시).
  - 확인: 같은 시점에서 LOD 0/1/2 강제 캡처(큰길 눈높이, 마을 위 45 m) — LOD1 은 LOD0 와 거의 같음, LOD2 는 가까이 강제하면 2층 집 몸통·지붕이 뭉개짐(자동이면 90 m 밖). 콘솔 오류·경고 0. 가까이 모습·전환 모습 사용자 확인 필요.
  - 삼각형(프리팹 합, LOD0 기준) 마을 약 419만(틈 막기 전 측정 4,192,968 — 틈 막기로 집마다 늘어남, 합계 다시 안 잼) + 성 699,964.
  - 배치 자료: `Captures/jptown/town_y.tsv`(처음 350채 — 지금은 `drop_houses.txt` 에 적힌 221채를 뺀 것), `paint.tsv`·`road_paint2.json`(길 칠).
  - 확인: 캡처(씬 뷰와 같은 시점, 남서쪽 위, 참배길, 신사 터, 성 오름길, 성문 앞), 콘솔 오류·경고 0, `WaterShoreMask` 다시 구움(2026-10-04). 씬 저장은 사용자. 가까이 모습·실제 프레임 사용자 확인 필요.
- 텍스처 규칙: 해저 < −2.5, 모래 < ~3 m, 경사 20~28°부터 바위, 38~48°부터 절벽, 90~130 m 위 바위 섞임.

### 경복궁 에셋 (`02.Resources/01.Asset/Models/Gyeongbokgung/`, 국가유산청 KHS, 무료, 원래 URP)
- **원본 정리(2026-10-11, 사용자 지시 "경복궁 원본 에셋 안 쓸 테니 삭제")**: SampleScene 에서 꺼 둔 원본 단지 인스턴스 11개(`Heungnyemun`·`Geunjeongjeon`·`Sajeongjeon`·`Gangnyeongjeon`·`Gyotaejeon`·`Sinmumun`·`Gyeonghoeru`·`Donggung`·`Geonchunmun`·`Yeongchumun` `_Complex`, `GwanghwamunGate_Gate`)를 씬에서 지우고 씬 저장. 이어서 게임 씬 3개·Resources·StreamingAssets·Settings·렌더 파이프라인 에셋이 참조하지 않는 에셋을 Unity 휴지통 이동으로 지움(프로젝트 전체 14,291개·17.77 GB — 경복궁 원본 메시·소품·데모 씬 11개, 원본 단지용 `_Baked/<단지>`·`_Baked/_Cards`, TextureHaven·ObjectiveEnvironment 미사용 텍스처, MagicaCloth2 예제, 통신사선·Free_Rocks·VFX 데모 등). 남긴 것: 켜진 오브젝트가 쓰는 것 — **광화문 부속단지 `Gwanghwamun_Complex` 는 여전히 원본(`Gwanghwamun_Original`)을 씀**, 궐내각사 가림 `SMJC_LongHall_Orig`, 단순화판이 쓰는 원본 텍스처·재질(`_Shared` 등), 씬에 꺼 둔 `Donggung_Simplified`·`Gyotaejeon_Simplified`·월대·해태. 일부러 남긴 것: `02.Resources/00.Component`(NPC 등 — 사용자 지시 "npc 는 놔두세요"), 애니메이션 라이브러리(`01.Asset/Animations`·`07.Animation`), `_Baked` 중 단지가 아닌 폴더(Impostor·Gungjang·Ganuidae·Chwirojeong 등). 경복궁 폴더 약 26 → 16.18 GB. **원본 데모 씬·원본 메시가 없어져, 이 절의 원본 기반 "다시 굽기·다시 만들기" 절차(PalaceBakeTool·PalaceOptimizeTool·PalaceRoofLodTool·PalaceCardTool·PalaceOrigExport·PalaceBinExport 로 데모 씬 내보내기 등)는 더 쓸 수 없음.** 삭제 뒤 확인: Missing 프리팹 0, 콘솔 확인은 아래 §9.
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
  - **계단 위 트기(2026-10-04)**: 양쪽 계단(P x −2890.65~−2888.65, N x −2861.35~−2859.35, 1층 13.4 → 2층 바닥 18.56) 위 z 4287.8~4292.5 · y 17.3~19.7 의 2층 바닥·보·1층 천장 판 삼각형을 잘라냄(경계 걸친 삼각형은 잘라 다시 나눔, 계단·난간·기둥 재질은 안 건드림). 2층 계단 구멍 남쪽 끝 난간(`RailCard_*_TopEnd` + 같은 자리 `MI_KHL_Stair` 난간틀 152 삼각형)은 계단을 가로막아 1.6 m 남쪽(새 구멍 끝 z 약 4287.8)으로 옮김. `KHL_Main` 메시를 같은 에셋에 덮어씀. CharacterController 걷기로 두 계단 모두 2층(18.58)까지 오르내림 확인. 그 뒤 고침(사용자 지적 "난간 뚫려 있고 계단에 구멍"): 구멍 양옆 난간 가로대 3줄을 새 구멍 끝까지 늘림(정점 z → 4287.90), 난간 판 `RailCard_*_TopIn/TopOut` 은 z 4287.90~4289.38 에 앞뒤 판을 더함(u 를 이어 매김, 텍스처 Repeat), 계단 맨 위(z 4292.36~4292.75)·구멍 옆 틈(폭 0.1~0.15 m)에 2층 바닥 판(`MI_KHL_Floor01A`, 위·아래 면) + 계단 끝 앞면. 다시 걷기 시험 통과.
  - `KHL_Main` 에 든 것: 원본(데모 씬)에서 밖에서 안 보이는 면을 지우고 줄인 메시 + 판(카드)들(재질 `Simplified/Cards/MI_KHL_*`): 2층 안쪽 방 문 `InnerDoor_w071/w076/w089`, 안쪽 창 `InnerWin_w405/w435/w506`, 2층 바깥 둘레 낙양각 `Frame_*`(알파 클립 0.5), 2층 바깥 둘레 문 위 창살 자리 `WhiteBoard`(단색), 계단 난간 `RailCard_<N|P>_<SlopeIn|SlopeOut|TopIn|TopOut|TopEnd>` 10종(알파 클립 — 난간 틀·기둥은 원본 입체 메시, 사이 무늬만 판), 2층 바깥 난간 옆면 `PerimCard_PX/NX/PY/NY`(알파 클립), 1층 천장 평면 `Ceiling1F_Flat`(알파 클립), 2층 천장 평면 `Ceil2Card_Ring/Inner`(불투명, 노멀맵) + 기와 밑 받침판(재질 `_Shared/Materials/MAT_RoofPlate_Black`, 위를 향한 한쪽 면만 — 기와 틈으로 하늘·바닥이 비치던 것 막음). 2층 바깥 둘레 문짝·들쇠는 없음(사용자 지시). 텍스처 70장 `Simplified/Cards/*.png`(노멀맵 `_N`). 안 쓰게 된 `MI_KHL_GateCard_01~06` 머티리얼·텍스처는 폴더에 남아 있음.
  - 캡처(위·1층 천장 밑·계단 난간·2층 바깥 난간·안쪽 창)로 담장 없음·천장 평면·난간 채움 확인. 연못 자리를 터레인(약 y 10)이 덮어 물(월드 y 약 9.0)·석축 아랫부분이 안 보임 — Complex 때도 같은 자리라 원래 있던 문제로 보임(확인 필요). 실제 프레임·LOD 전환 거리는 사용자 확인 필요.
- **해태상·월대는 고종 대 조성**: 해태상 1870년(고종 7) 월대 앞 설치, 월대 1866년(고종 3) 축조(《경복궁영건일기》). 월대는 2007년 발굴에서 고종 이전 것으로 추정되는 잡석 유구가 나왔으나 세종 대 것인지는 규명 안 됨(출처: 위키백과 「광화문」, 경향신문 2022-09-20 이기환 기사). 광화문 자체는 1395년 건립·1592년 임진왜란 소실이라 인종·명종대에 있음. → 해태상·월대는 배치에서 **별도 오브젝트**.
- 근정전 단지 모델 코드(사용자 확인): GJJ = 근정전 본전(월대 포함), GJJ_HG = 행각, GJM = 근정문, YML = 행각 양옆 누각, RR = 바닥. 데모 씬 `GeunjeongjeonComplex/Scenes/Demo_Geunjeongjeon.unity` 삼각형: GJJ 1,544만 / GJJ_HG 363만 / GJM 222만 / YML 40만 / RR 2만.
- **동궁 단순화판(2026-09-29 다시 올림 — 2026-09-30 부터 씬에서 끔, 임포스터로 바꿈: 아래 §4 임포스터)**: `_Baked/Donggung/Simplified/Donggung_Simplified.prefab`(같은 경로에 덮어써 GUID 유지)을 씬 `GyeongBok/동궁_Donggung` 아래에 두고 `Donggung_Complex` 는 끔, 홀더 `동궁_Donggung` 은 켬(사용자 지시 — 끄지 말 것). 인스턴스 로컬 (30.31, 0, 51.60) 회전 (0, 270, 0)(사용자 지시로 270° 돌림), 월드 바운드 중심 (−2640.4, 17.0, 4233.7) 크기 (84, 11, 167). 씬 저장은 사용자. OBJ = `D:\Blender\Enviroment\PalaceJoseon\동궁\LOD\DG_Bld_* / DG_HG_* / DG_Wall_* / DG_DoorWindowCards / DG_GateDoors.obj`(단지 프리팹 루트 좌표, `LoadObj` 규칙).
  - 구성: 건물별 45개(`Buildings/DG_Bld_01~10` 전각·대문, `DG_HG_01~26` 행각 날개, `DG_Wall_01~09` 담장 구간) + `DG_DoorWindowCards`(문·창 판) + `DG_GateDoors`(담장 대문 문짝 6곳, 연한 갈색 `Simplified/Cards/MI_DG_GateDoorWood` = RedWood 판문 무늬를 틀 나무색으로 바꾼 `T_DG_GateDoorWood_BC.png`). 오브젝트마다 LODGroup 하나: 건물 45개는 LOD0 → **LOD1(단순화) 을 건물 LODGroup 중심에서 약 50 m**(화면 높이 = 크기 / (50 × 1.1547), 최대 0.95 — 큰 행각 `DG_HG_14/15/17/26` 은 그래서 55~118 m), 0.003 에서 끔. 문·창 판과 대문 문짝은 LOD0 만. 삼각형 합계 4,189,471(측정) — 외부에서 건물별로 모양 유지 한도(표면 차 1.5 cm)까지 줄인 것, 기와 약 35%·목부 10~70%, 벽·돌은 평면 합치기만. LOD1(`<이름>_LOD1`) = 기와(`MI_DG_Giwa`) 면을 빼고 그 자리에 지붕 윗면을 따라 만든 판(0.3 m 격자, 3×3 평균, 기와 꼭대기에서 3 cm 아래)을 넣은 것 — 판 재질 `Simplified/Cards/MI_DG_RoofTop`(기와를 위에서 찍은 그림 `DG_RoofTop_BC.png`, 원본 8192×4096 → 가져오기 4096×2048, 위에서 투영한 UV). 전각 지붕 재질(`Roof01A` 등)·용마루·장식은 LOD1 에도 그대로. LOD1 합계 1,911,954 삼각형(측정). 메시 = `Simplified/Donggung_Buildings_Meshes.asset`(578 MB, LOD0+LOD1, `PalaceMeshCompact`). 예전 `Donggung_Simplified_Meshes.asset`(구역 LOD·안쪽 판·받침판)은 휴지통. 충돌체 없음. 머티리얼 null 0.
  - 내부 진입은 없지만 건물 안쪽을 지우지 않은 판(예전 "안쪽 지우기 + 둘레 판"은 벽이 사라져 사용자가 되돌림).
  - 문·창: 원래 1,824개 자리 전부 앞·뒤 판 3,582장(`MI_DGCard_<종류>`, 84종). 26종(`Door03A/B`, `Door04A~H`, `Door07A~F`, `Door08A~E`, `HG_Door03H/I`, `Window01A/C/D`)은 판 모양을 문짝 크기에서 새로 잡아 앞·뒤 그림이나 좌우가 바뀌었을 수 있음(확인 필요). 데모에서 머티리얼 빈 슬롯이던 면(대문 건물 2곳, 830 삼각형)은 뺌.
  - Unity 캡처(조감·전각 앞)로 배치·문 채움 확인. 가까이 모습·실제 프레임은 사용자 확인 필요.
- 동궁 데모 씬 `DongGungComplex/Demo_DongGung.unity`: 렌더러 8,337(계층 없이 루트 한 곳), 메시 코드는 전부 `DG`(`SM_DG_HG_*` 행각 3,191개 1,113만 삼각형 / 그 밖 약 1,286만 / 바닥 `DG_Ground_Geo` 48), 합계 23,992,236 삼각형. 범위 x −134~30 · z −16~65 m(데모 좌표). 본채 지붕 `SM_DG_Roof01A`(−118, 9)·`Roof02A`(−118, 49)·`Roof03A`(9, 7), 문 지붕 `SM_MGJ_RoofR`(−21·−40, 48). 일부 렌더러(`SM_DG_Door08A` 24곳, `SM_DG_Giwa03A-b` 10곳)는 서브메시 수보다 머티리얼이 적음(빈 슬롯).
- **교태전 단순화판(2026-09-29 — 2026-09-30 부터 씬에서 끔, 임포스터로 바꿈: 아래 §4 임포스터)**: `_Baked/Gyotaejeon/Simplified/Gyotaejeon_Simplified.prefab` 을 씬 `GyeongBok/교태전_Gyotaejeon` 아래 기존 `Gyotaejeon_Complex` 와 같은 자리 (−2, 0, −111)에 넣고 `Gyotaejeon_Complex` 는 끔. 홀더 `교태전_Gyotaejeon` 은 켜져 있음(2026-09-30 확인 — 예전엔 꺼져 있었음). 씬 저장은 사용자. OBJ = `D:\Blender\Enviroment\PalaceJoseon\교태전\LOD\GTJ_*.obj`(단지 프리팹 루트 좌표, `LoadObj` 규칙).
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
  - **법선 고침(2026-10-05, 사용자 지적 — 기단 옆면에 면 두 개가 겹친 듯한 대각선, 아래 돌 단에 이상한 그림자)**: 원인(측정) — 외부에서 줄인 메시가 다른 면에 붙어 있던 정점 법선을 그대로 가져와, 모서리의 38 %(GHM_Core)가 그 면 법선과 40° 넘게 어긋남(평평한 기단 앞면 정점 법선 (0.29, 0.30, −0.91)·(0.67, 0.67, −0.31) 등 → 삼각형마다 음영이 갈라짐, 돌 단 모서리 법선 (−0.63, 0.47, −0.62) 처럼 세 면 평균 → 둥근 음영). 겹친 면·UV 늘어남은 없었음(측정). 고침 = `MeshNormalFixTool`(50°): `GHM_Core` 38.2 → 0.5 %, `GHM_Core_LOD2Detail` 52.5 → 0.6 %, `GHM_Core_LOD2Win` 33.3 → 0.0 %, `GHM_Core_Jabsang_OnShell` 51.3 → 0.2 %, 날개 궁장 `GHM_WallA1~B3_Trim` 34~45 → 0.8~1.5 %(정점 393,036 → 463,224 등, 삼각형 수 그대로). 새 메시 = `Simplified/NormalFix/*_N.asset`·`_Baked/Gungjang/WingTrim/NormalFix/*_N.asset`, `GwanghwamunGate_Simplified.prefab` 의 MeshFilter·MeshCollider 26곳을 바꿈(원래 메시 에셋은 그대로 — 되돌리려면 프리팹 참조만). 궁장(§4 궁장) 부품 `Gungjang_Module_LOD0`·잘린 부품 12개도 같은 문제(43 %)라 **에셋 안에서 바로** 고침(→ 1.5 %, "궁장 다시 깔기" 를 해도 고친 부품에서 잘림). 확인: 같은 시점(사용자 화면·남서·위) 전후 렌더 — 대각선·둥근 띠 음영 사라짐, 지붕은 그대로, 콘솔 오류·경고 0. 다른 단순화판(흥례문·근정전 등)은 안 봄(같은 문제가 있을 수 있음 — 확인 필요). OBJ 에서 다시 가져오면 법선 고침을 다시 해야 함.
- **남은 문·문 장식 판 교체(2026-09-30, `Editor/PalaceCardTool.cs`)**: 단순화판(광화문 문루·근정전·강녕전·교태전·동궁)에 메시로 남아 있던 여닫는 문짝·문 장식·쇠문·홍살·작은 창을 판(카드, 이미지를 입힌 평면)으로 바꿈(사용자 지시: 모든 창살·문을 판에 이미지만). 여닫는 문짝(광화문 정문 6짝·작은 문 4짝, 강녕전 향오문 `SM_HOM_Door01A~03A` 8짝, 행각 문 `SM_GNJ_HG_Door02A_024` 8짝) = 상자 판(앞·뒤·옆 6면, 12 삼각형, 경첩 피벗 그대로, MeshCollider 도 상자), 나머지 = 앞·뒤 판 2장(맞은 곳만 불투명).
  - 강녕전 `Gangnyeongjeon_Doors` 는 원본 문짝 메시(향오문 13,560~16,236·행각 문 11,106 삼각형)를 쓰고 있었음.
  - 삼각형(측정, 오브젝트 합): 광화문 문짝 17,123 → 120 / 근정전 문 장식(`GJ_Hall`·`GJ_RR` 의 `MI_GJJ_DoorOrnament04` 187조각, 근정문 `SM_GJM_DoorOrnament03A` 2개) 14,987 → 756 / 강녕전(문짝 16 + 응지당 쇠문·홍살 + 행각 문틀 `Door03` 56조각 + 연길당 쇠문) 214,454 → 456 / 교태전 함흥각 작은 창 948 → 4 / 동궁(`DG_HG_26` 판문·쇠문, `DG_HG_01/08` 쇠문, `DG_GateDoors` 대문 6곳, LOD0 + LOD1) 9,186 → 64. 합계 256,698 → 1,400.
  - 판으로 안 바꾼 것(확인): 광화문 `GHM_Core` 의 `Win01A`(단청 보)·`Gate01A/03A/04A`(석재), 경회루 `KHL_Main` 의 `Windowframe`(틀), 교태전 `DoorFrame`·`Wallframe`(나무 틀), 교태전 문고리(`Doorhandle`, 입체 고리), 동궁 `DG_Bld_06`·`DG_HG_24`·`DG_HG_17`·`DG_HG_19` 의 `MI_DG_Door01A`(문짝이 아니라 3 cm 막대·꼭지 — 판으로 바꿨다가 되돌림, 원래 재질 = `_Shared/Materials/MI_DG_Door01A.mat`).
  - 새 에셋: `_Baked/<단지>/Simplified/Cards/<메시>_card.png`(아틀라스, Clamp, 알파 클립이면 밉맵 coverage 유지 0.5, 가져오면 가까운 2의 거듭제곱 크기) + `CARD_<메시>.mat`(HDRP/Lit, 한쪽 면, 매끄러움 0.12, 알파 클립 0.5) + `Cards/Meshes/<원래 메시>_Card.asset`(원래 메시에서 대상 서브메시를 빼고 판을 마지막 서브메시로, 안 쓰는 정점 뺌, `PalaceMeshCompact`). 메시 에셋 합계 209 MB(근정전 66.6·강녕전 46.2·교태전 14.6·동궁 81.7 — 큰 건물 메시가 통째로 새로 생김). 원래 메시·FBX 는 그대로, 프리팹(`GwanghwamunGate_Simplified`, `Geunjeongjeon_DoorsCard`, `GJ_GJJ_LOD`, `GJ_RR_LOD`, `Gangnyeongjeon_Doors`, `GNJC_EJD/GNJ_HG/YGD_LOD`, `Gyotaejeon_Simplified`, `Donggung_Simplified`)에서 메시·재질·MeshCollider 만 바꿈 — 되돌리려면 프리팹만.
  - 판 데이터(작업 파일) = `D:\UnityAssetCache\_cards\unity\`(`ucards2.bin` 판 정점 + 새 서브메시 목록, `ucards.tsv`, `targets.tsv` = `ExportTargets` 결과, `Cards/`). 다시 적용: `PrepareCards(폴더, 20, "다시 만들 항목,…")` 를 DONE 까지(메시는 같은 에셋에 덮어써 프리팹 연결 유지) → `ApplyToPrefabs(폴더)` → `Verify(폴더)`(41 오브젝트 ok, 2026-09-30).
  - Unity 캡처(광화문 정문 앞·가까이, 향오문, 강녕전 행각 안팎(원본과 비교), 근정전 문 장식, 동궁 대문, 응지당)로 배치·재질 확인, 콘솔 오류·경고 0. 가까이 모습(판이라 옆에서 보면 납작함, 문 장식 판이 문 앞 몇 cm 에 뜸)·실제 프레임은 사용자 확인 필요.
- **흥례문·건춘문·영추문·신무문·광화문 부속단지 원형 유지 감면(2026-09-30 — 흥례문·건춘문·영추문·신무문은 Unity 에 올림(아래 "흥례문 단순화판"·"건춘문·영추문·신무문 단순화판"), 부속단지는 아직)**: 데모 씬을 `PalaceBakeTool.OpenComplex` 로 열고 `PalaceBinExport.Export(폴더)` 로 내보낸 고유 메시를 외부에서 줄임(씬·에셋은 안 바꿈). 폴더 = `D:\Blender\Enviroment\PalaceJoseon\FourDoor\Source\<Heungnyemun|Geonchunmun|Yeongchumun|Sinmumun>\`, 부속단지 `…\광화문\Source_Complex\`. 폴더마다 내보낸 원본(`meshes.bin`·`instances.tsv`·`materials.tsv`) + `reduced.bin`(줄인 것 — 형식·메시 순서가 `meshes.bin` 과 같음, 배치·재질은 원래 `instances.tsv`·`materials.tsv` 를 그대로 씀) + `report.tsv`(메시별 높이·허용 오차·삼각형 수).
  - `PalaceBinExport`: `__VIS_HOLDER`(OpenComplex 가 만든 데모 루트) 아래 켜진 MeshRenderer 를 고유 메시(`meshes.bin`: 매직 `0x31584250`, 메시마다 이름·위치·법선·UV0·서브메시 인덱스, 리틀 엔디언) + 배치(`instances.tsv`: 메시 번호, 이름, 루트 기준 4×4 행렬 16개(행 우선), 서브메시별 재질 이름 `|` 구분) + 재질(`materials.tsv`: 셰이더, BaseColor·Normal·Mask 경로, 색, 타일링, 알파 클립, 매끄러움, 노멀 세기, 양면)로 씀. 렌더러마다 정점을 다시 쓰는 OBJ 대신 같은 메시는 한 번만. Unity 좌표 그대로. `meshes.bin` 이 있으면 SKIP.
  - 데모 씬(측정, 켜진 렌더러 / 고유 메시 / 재질 / 삼각형): 흥례문 5,606 / 250 / 65 / 19,860,120 · 건춘문 1,131 / 143 / 62 / 2,025,047 · 영추문 1,625 / 152 / 66 / 3,689,997 · 신무문 623 / 130 / 60 / 945,100 · 광화문 부속단지 871 / 101 / 33 / 1,677,176. 흥례문 메시는 삼각형마다 정점이 거의 따로(고유 메시 389만 삼각형·974만 정점, 250개 중 129개가 각진 음영), 나머지 단지는 정점/삼각형 0.9~1.16.
  - 삼각형(배치 합, 데모 바닥 제외, 측정): 흥례문 19,860,080 → 6,377,320(32.1 %) / 건춘문 2,024,847 → 1,031,566(50.9 %) / 영추문 3,689,797 → 1,817,679(49.3 %) / 신무문 944,900 → 553,322(58.6 %) / 광화문 부속단지 1,676,976 → 829,068(49.4 %).
  - 방법: 정점을 새로 만들지 않고 남길 정점만 고르는 줄이기라 UV·법선은 원래 값, 재질(서브메시) 경계·UV 이음매는 안 움직임. 허용 오차 상한 = 그 메시 배치의 최저점이 바닥(전 배치 최저점의 5 백분위)에서 2.5 m 미만이면 4 mm, 5 m 미만 8 mm, 그 위 15 mm. 후보 2·4·8·15 mm 를 작은 것부터 원본과 14방향 평행 광선(축 6 + 대각 8)으로 비교해 맞음/안 맞음·깊이·텍스처 색이 어긋난 광선이 0.2 % 이하면 통과, 통과한 것 중 삼각형이 5 % 이상 더 줄 때만 큰 쪽을 고름. 2 mm 도 안 되면 1 mm, 그것도 안 되면 원본(단지마다 0~1개). 12 삼각형 이하·데모 바닥은 그대로.
  - `reduced.bin` 메시 이름 끝 `#F` = 각진 음영 메시(삼각형 90 % 이상이 정점 법선 = 면 법선) — 법선을 0 으로 저장했으므로 가져올 때 삼각형마다 정점을 나눠 면 법선을 넣어야 함. 그 밖 메시는 (위치, UV, 법선)이 같은 정점을 합친 것이라 정점 법선 그대로.
  - 줄인 뒤 큰 것(개수 × 삼각형): 흥례문 `SM_HNM_Windowframe_A` 87 × 15,270(원래 90,940) = 132.8만, `SM_HNM_Roof_B03` 34 × 23,234 = 79.0만, `SM_HNM_Roof_A01` 1 × 423,669(원래 1,310,940). 건춘문·영추문·신무문 지붕 `SM_GCM/YCM/SMM_RoofA`(37,458)는 거의 안 줄었음(37,423). 부속단지는 Unity 에서 아직 안 봄.
  - **판(카드) 데이터(2026-09-30)**: 창살·문·홍살·문 천장을 판으로 바꾼 결과를 같은 폴더에 둠 — `cards.bin`(바뀐 메시, `meshes.bin` 형식·Unity 좌표; 이름 끝 `#CARD` = 통째 판, `#SPLIT` = 틀(남긴 서브메시) + 판(마지막 서브메시), `#F` = 각진 음영), `cards.tsv`(메시 번호, 종류 full/split, 모양 box = 여닫는 문짝 6면 / flat = 앞·뒤 / down·up = 천장 한 장, 모드, 재질 이름 `CARD_<HNM|GCM|YCM|SMM|GHC>_<번호>`, 텍스처, 알파 클립, 남긴 서브메시), `removed.tsv`(문짝 판에 구워 넣어 지울 쇠붙이 배치 줄 — 건춘문·영추문 `DoorLock` 각 1), `Cards/*.png`(97장). `reduced.bin` 대신 이 메시를 쓰는 것. 삼각형(배치 합): 흥례문 6,377,320 → 4,580,276 / 건춘문 1,031,566 → 716,329 / 영추문 1,817,679 → 1,225,956 / 신무문 553,322 → 501,147 / 부속단지 829,068 → 533,809. 천장 판 = 흥례문 `PatternCeilingA/B`·`Celling_A~G`, 건춘문·영추문·신무문 `Banja`, 신무문 `GateCeiling`. 석문 안 연등천장(서까래)은 판으로 안 바꿈. 흥례문 `Windowframe_A/B`(벽판과 창살이 한 메시)는 통째 판. 부속단지는 Unity 에서 아직 안 봄. `instances.tsv` 재질은 서브메시 수만큼만 — 렌더러에 남는 재질 슬롯은 빠짐(§6 영추문 문짝).
- **흥례문 단순화판(2026-09-30)**: `_Baked/Heungnyemun/Simplified/Heungnyemun_Simplified.prefab` 을 씬 `GyeongBok/흥례문_Heungnyemun` 아래 기존 `Heungnyemun_Complex` 와 같은 자리 (−3, 0, 0)에 넣고 `Heungnyemun_Complex` 는 끔. 홀더 `흥례문_Heungnyemun` 은 켜져 있음. 씬 저장은 사용자. 원본 = 위 원형 유지 감면 메시 + 판(카드) 데이터(`cards.bin`).
  - 계단 뺌(사용자 지시): 문루 1층 서쪽 칸 나무 계단 `SM_HNM_Stair_A` + 발판 돌 `SM_HNM_StoneStair_A` + 2층 계단 구멍 난간 `SM_HNM_Handrail_C`·`SM_HNM_RailPillar_A`. 바닥 막기: 1층 천장 판(`SM_HNM_PatternCeilingA` 판, 텍스처 `HNM_149_SM_HNM_PatternCeilingA_card.png`)의 계단 구멍(0.93 × 2.07 m)과 둘레 틀을 같은 무늬 자리(가로 주기 192.5 px × 4 = 770 px 옆)에서 복사해 채움. 문 앞 돌계단(`Stair_B`·`StoneStair_B`)은 그대로.
  - 밖에서 안 보이는 것 지움(사용자 지시 — 문루 2층 안 공포 안쪽 단청·2층 바닥·천장·지붕 속·기둥 속 등): 장면을 0.1 m 복셀로 막고 위·옆 경계에서 채운 '바깥 공기'를 구한 뒤, 삼각형 표면 점(1,757만 점)에서 64방향 평행 광선 — 빠져나가거나 맞기 전 광선 위 공기가 바깥 공기면 보임(두 면 재질이라 양면 검사, 판은 앞면만). 2단계로 지붕 위 13방향 훑기(기와 사이 머리카락 틈으로 보이는 아래 부재) + 걸을 수 있는 곳 눈높이 1,262곳에서 사방 광선으로 놓친 것 되살림. 삼각형 4,564,419 → 2,894,238(63.4 % 남김, 땅 포함).
  - 받침판(LOD0): 지운 지붕 속이 기와 틈으로 비치지 않게 기와 골 바닥보다 3 cm 아래에 검은 면(`MAT_RoofPlate_Black`) — 기와 바로 아래 0.6 m 안에 남은 부재가 없는 곳만, 테두리에서 0.75 m 안쪽, 옆·아래에서 바깥 공기가 보이는 삼각형은 뺌(두 지붕이 만나는 틈). 부분마다 24~1,764 삼각형.
  - 부분(LODGroup 13개, 데모 좌표로 삼각형 무게중심을 상자에 넣음 — 긴 지붕판은 토막 경계에서 삼각형 단위로 갈림): `HNM_GATE`(문루, x −15.3~14.3 · z −9~9), 행각 9토막 `HNM_HG_SW1·SW2·W1·W2·W3·SE1·SE2·E1·E2`(크기 23.7~30.4 m), `HNM_YHM`(유화문), `HNM_GBC`(기별청), `HNM_GROUND`(땅·길·금천·영제교·석수·난간·쇠창살 — 이름으로). 여는 문짝 27짝은 `Heungnyemun_DoorsCard.prefab`(= `Heungnyemun_Doors` 의 문짝 메시를 상자 판으로, 경첩 피벗·충돌체 유지)으로 빼서 넣음.
  - LOD: LOD0 = 위 보이는 것 + 받침판(부분 중심에서 약 60 m 까지) / LOD1 = 기와(`MI_HNM_RoofTile`)·막새(`MI_HNM_YeonDan_*`)를 빼고 지붕판(기와 윗면 −3 cm, 0.25 m 격자에서 층을 나눠 만들고 가장자리는 반으로 나눠 찾음) + 나머지 줄임(잔부재 6 cm / 구조재 3 cm, UV 이음매 넘어 접기 허용)(약 150 m 까지) / LOD2 = 같은 판 + 줄임 20 cm / 10 cm + 작은 떨어진 조각 없앰. 끝 화면 높이 0.004. 전환 화면 높이 = 부분 크기 / (거리 × 1.1547)(최대 LOD0 0.99 · LOD1 0.9 — 땅 `HNM_GROUND`(크기 130 m)는 그래서 약 114 / 150 m), CrossFade. 용마루(`RoofLine`)·양성(`RoofWall`)·잡상(`RoofOrnament`)은 LOD1·2 에도 남김(줄임).
  - LOD 거리(2026-10-01, 사용자 지시 "경복궁처럼" → 선택 "광화문처럼 60 / 150 m"): 처음엔 약 200 / 500 m 라 너무 멀리 가야 바뀌었음. 프리팹 LODGroup 값만 바꾸고 `layout.txt` 도 같은 값으로 고침(옛 값 `layout_200_500.txt`). LOD1·2 지붕판은 새 재질 `Simplified/MAT_RoofPlate_HNMFar.mat`(`MAT_RoofPlate_Black` 복사, 색 (0.23, 0.23, 0.24)·매끄러움 0.54 = 기와 재질과 같은 매끄러움) — 검은 판(0.035)이면 60 m 에서 지붕이 확 어두워짐(80 m 캡처 화면 평균 sRGB: LOD0 (61, 86, 99) / 검은 판 (46, 68, 79) / 새 재질 (60, 84, 96)). LOD0 받침판은 검은 판 그대로. **`BuildPrefab` 으로 다시 만들면 지붕판 재질이 검은 판으로 돌아감** — 다시 넣어야 함.
  - 삼각형(측정, LOD0 / LOD1 / LOD2): 문루 804,783 / 123,035 / 49,292 · 행각 토막 143,690~198,493 / 23,062~29,732 / 11,983~14,838(9토막 합 1,454,891 / 231,309 / 117,895 — 이름 순 SW1 147,984 / 24,164 / 13,014, SW2 198,493 / 28,830 / 14,838, W1 150,728 / 23,419 / 11,983, W2 144,050 / 25,278 / 12,809, W3 176,101 / 29,732 / 14,269, SE1 146,456 / 23,062 / 12,108, SE2 197,096 / 28,132 / 14,125, E1 150,293 / 23,562 / 12,106, E2 143,690 / 25,130 / 12,643) · 유화문 410,187 / 62,347 / 23,177 · 기별청 59,714 / 6,261 / 3,177 · 땅·영제교 167,645 / 11,403 / 3,317. 합계 2,897,220 / 434,355 / 196,858 + 문짝 판 324. 원래 데모 씬 19,860,120.
  - 충돌체: 부분마다 LOD0 에 MeshCollider(13) + 문짝 27. 메시 에셋 39개 479 MB(텍스트 저장), 판 텍스처·재질 `Simplified/Cards/`(30종 27.8 MB), 문짝 판 메시 `Simplified/DoorCards/`.
  - 데이터·다시 가져오기: `D:\Blender\Enviroment\PalaceJoseon\FourDoor\Source\Heungnyemun\LOD\`(`meshes.bin` 'HNMS' 형식 = 매직 0x534D4E48, 메시마다 이름·위치·법선·UV·서브메시마다 재질 이름 + 인덱스, Unity 좌표 = 단지 프리팹 루트 좌표 / `doors.bin` 문짝 판(문짝 메시 자기 좌표) / `layout.txt` 부분·LOD·전환 / `cards.tsv` + `Cards\` / `stats.tsv`). 순서: `PalaceLodBinImport.CloseHeavyScene()`(SampleScene 이 저장된 상태일 때만 빈 씬으로) → `ImportMeshes(폴더, "…/Simplified", 12, "", "meshes.bin", "Meshes", 3)` 를 DONE 까지 → `BuildDoorsCard(폴더, …/Simplified, …/Heungnyemun_Doors.prefab, "Heungnyemun_DoorsCard", …/Heungnyemun_Original.prefab)` → `BuildPrefab(폴더, …/Simplified, …/Heungnyemun_Original.prefab)` → `OpenScene("Assets/00.Scenes/SampleScene.unity")`(1분 넘게 걸려 MCP 시간 초과가 나도 열림) → `Place("GyeongBok/흥례문_Heungnyemun", "Heungnyemun_Complex", 프리팹 경로, "Heungnyemun_Simplified")`. 메시 에셋 정점 수는 `PalaceMeshCompact` 가 같은 정점을 합쳐 파일보다 적음(인덱스 수는 같음).
  - 확인: 바깥 18시점(마당 눈높이·문 앞·멀리 위)에서 원본(계단만 뺀 것)과 LOD0 을 그려 비교 — 다른 화소 0.07 % 이하(공포 사이 몇 화소). Unity 캡처(문 앞, 마당 쪽 서쪽 칸, 계단 자리 천장, LOD1·2 강제)로 계단 없음·천장 막힘·검은 판 확인, 콘솔 오류 0. 가까이 모습·실제 프레임·LOD 전환 거리는 사용자 확인 필요.
- **건춘문·영추문·신무문 단순화판(2026-09-30)**: 예전엔 세 문이 가까이서도 구운 덩어리만 씀(사용자 지적). 흥례문처럼 **가까이 = 원형 유지 감면 + 판(`cards.bin`), 멀리 = 예전 구운 덩어리**(사용자 선택). `_Baked/<단지>/Simplified/<단지>_Simplified.prefab` 을 홀더 아래 기존 `<단지>_Complex` 와 같은 자리(건춘문 (−26, 0, −12)·영추문 (54, 0, 9)·신무문 (−6, 0, −17), 회전 0)에 넣고 `<단지>_Complex` 는 끔. 홀더는 켜져 있음. 씬 저장은 사용자.
  - 부분 = 예전 구운 덩어리와 같은 묶음(메시 이름의 건물 코드 `SM_<코드>_…`, 덩어리 목록에 없는 작은 코드는 SMALL): 건춘문 GCM·GSB·SMALL·SMJC / 신무문 SMALL·SMJC·SMM(영추문은 자리별 — 아래). 부분마다 LODGroup(CrossFade): LOD0 = 줄인 원형 + 판(메시 하나가 30만 삼각형을 넘으면 긴 축으로 나눔, 메시마다 MeshCollider), 전환 화면 높이 = 부분 크기 / (200 × 1.1547)(부분 중심에서 약 200 m) / LOD1 = `<단지>_Complex` 안 `<덩어리>_Baked` 의 마지막 LOD 단계(석재·창호·목부 + 지붕 껍데기)를 복사한 렌더러, 0.002 에서 끔. 건춘문 SMALL 은 구운 것(24,778)이 줄인 것(10,276)보다 무거워 LOD0 하나만. 부분 크기 7.3~105.5 m.
  - 빼는 것: 데모 바닥(`Plane*`·skysphere), `removed.tsv` 쇠붙이(문짝 판에 구워 넣음), 여는 문짝(`SM_GCM/YCM/SMM_Door0*`, 이름에 frame·handle·lock 이 든 것 제외) → `<단지>_DoorsCard.prefab`(= `<단지>_Doors` 의 문짝 메시를 상자 판(12 삼각형)으로, 경첩 피벗·충돌체 유지).
  - 삼각형(측정, LOD0 / LOD1): 건춘문 716,305 / 191,213(+ SMALL 10,276 은 멀리서도 LOD0) · 영추문 1,225,932 / 305,012 · 신무문 501,099 / 162,770. 문짝 판 건춘문 2 · 영추문 2 · 신무문 4짝(24 / 24 / 48). 데모 원본 2,025,047 / 3,689,997 / 945,100. 머티리얼 null 0.
  - 에셋: `<단지>_Original.prefab`(데모 원본 렌더러 1,128 / 1,622 / 618 — 가져올 때 재질 이름 → 재질 찾기용), `Simplified/Meshes/`(5 / 6 / 4 파일, 73.0 / 126.8 / 54.6 MB), `Simplified/Cards/`(판 텍스처·`CARD_*` 재질 16 / 17 / 14종, 11.2 / 11.4 / 14.3 MB), `Simplified/DoorCards/`, 영추문만 `Simplified/Far/`(아래).
  - 데이터·다시 가져오기: `98.Blender/Gates/build_gates.py` → `D:\Blender\Enviroment\PalaceJoseon\FourDoor\Source\<단지>\LOD\`(`meshes.bin`·`doors.bin`·`layout.txt`·`cards.tsv`·`Cards\`·`stats.tsv`). 순서는 흥례문과 같음(`ImportMeshes` → `BuildDoorsCard(…, <단지>_Doors.prefab, "<단지>_DoorsCard", <단지>_Original.prefab)` → `BuildPrefab` → `Place("GyeongBok/<홀더>", "<단지>_Complex", …, "<단지>_Simplified")`). `layout.txt` 의 `COPYFAR <LOD> <프리팹> <오브젝트 경로>` = 그 프리팹 안 오브젝트의 LODGroup 마지막 단계 렌더러(LODGroup 이 없으면 전부)의 메시·재질·그림자 설정·상대 위치를 그 LOD 에 복사(`PalaceLodBinImport.CopyFar`).
  - 영추문 문짝 한 짝(`SM_YCM_Door02A`) 판이 검게 구워져 있던 것 → 건춘문 `SM_GCM_Door02A` 판 그림으로 바꿈(§6).
  - 영추문이 무거운 까닭(측정): 영추문 데모에는 문 말고도 궁 안쪽 문 남쪽에 건물 3채가 들어 있음 — T자(문에서 약 55 m) 232,797 · ㄱ자(약 105 m) 324,472 · ㄱ자(약 130 m, 옆 일영대 `SM_YCM_Ilyeongdae` 4,112 포함) 245,948 = LOD0 의 66 %. 문 + 북쪽 작은 건물은 422,715(건춘문 문 25 m 안 412,609, 신무문 전체 501,099 와 비슷). ㄱ자 두 채 지붕은 기와가 한 장씩 든 큰 메시 `SM_YCM_Giwa*`(원본 584,527 → 450,224, 77 % 남음 — 모양 유지 한도에서 거의 안 줄어듦). 에셋 코드(JS·SMJC·Giwa)만 있어 무슨 건물인지는 확인 필요.
  - **영추문 자리별 부분(2026-09-30, 사용자 선택)**: 예전엔 코드별(YCM·SMJC — 크기 155~163 m, 기준점이 문에서 남쪽 55~62 m)이라 문 앞에서도 세 채가 LOD0 → 데모 x(문→남쪽) 구간으로 4부분: `Yeongchumun_GATE`(문 + 북쪽 작은 건물, x < −23) / `BLD_T`(T자, −23~33) / `BLD_L1`(ㄱ자, 33~65) / `BLD_L2`(ㄱ자 + 일영대, 65~). 경계 = 배치 경계 상자 사이 빈틈 가운데(배치는 경계 상자 가운데 x 로 나눔). LOD0 → LOD1 = 문 약 200 m, 세 채 약 70 m(부분 중심에서, `build_gates.py` 의 `REGIONS`). 삼각형(LOD0 / LOD1): 문 422,715 / 92,895 · T자 232,797 / 102,206 · ㄱ자 324,472 / 65,065 · ㄱ자 245,948 / 44,846(합 1,225,932 / 305,012 — 예전과 같음). 문 안쪽에 서면 문·T자 LOD0 + ㄱ자 두 채 LOD1 = 765,423(예전 1,225,932). 먼 LOD = 구운 덩어리(`SMJC_Baked`·`YCM_Baked` 마지막 단계)에서 무게중심이 그 구간인 삼각형만 잘라 만든 새 메시 `Simplified/Far/<부분>_<렌더러>.asset`(30개 52.6 MB, 각진 음영이라 정점이 많음) — `layout.txt` 의 `COPYFARBOX <LOD> <프리팹> <오브젝트> <x최소> <x최대> <z최소> <z최대>`(`PalaceLodBinImport.CopyFarBox`). 예전 코드별 LOD0 메시 5개(126.8 MB)는 휴지통, 예전 데이터는 `…\Yeongchumun\LOD\*_bycode.*`. 캡처: 건물 30 m 앞에서 자동 = LOD0 강제(같은 그림), 약 100 m 에서는 ㄱ자 두 채가 구운 것(기와가 매끈하고 용마루 장식이 낮게 보임).
  - 확인: Unity 캡처(세 문 문짝 안팎, 현판·잡상·단청 가까이, 영추문 215 m 에서 자동(LOD1)과 LOD0 강제 비교)로 배치·전환 확인, 콘솔 오류·경고 0. 215 m 비교에서 LOD1(예전 구운 것)은 석축이 LOD0 보다 밝고 얼룩져 보임 — 200 m 에서 바뀌는 게 보일 수 있음. 가까이 모습·실제 프레임·전환은 사용자 확인 필요.
- 단지별 방향(데모 좌표 기준, 확인한 것): 근정전·사정전·강녕전·교태전·신무문 = +z 북쪽. 흥례문 = +z 남쪽(문이 +z 끝, 영제교 마당이 −z) → 180°. 광화문 문루 데모 = 월대·해태가 +z → 180°. 광화문 단지(부속 건물)·영추문은 긴 축이 데모 z·x 로 놓여 90° 돌렸으나 **어느 쪽이 맞는지는 확인 필요**. 동궁은 270° 돌림(사용자 지시, 2026-09-29), 경회루·건춘문은 회전 없이 둠(확인 필요).

#### 문 열기 (`GyeongBok/문열기_Gates`, 2026-10-04)
- `PalaceGate`(07.Environment): 배에서 내린 상태에서 문짝 가운데 range(m, 수평) 안에 서서 **E 3초** → 묶음 문짝 전부를 안쪽(`Inward` 자식 위치 쪽)으로 90° 2.5 s 에 열고, 다시 3초 → 닫음. 여러 묶음이 겹치면 가장 가까운 하나만, 승·하차 자리(`BoardingSystem.CanAct`)에서는 E 를 양보. 안내·진행 바 = OnGUI(임시). 시험용 `SetOpenImmediate(bool)`.
- 묶음(자식, 문짝 = `DoorHinge_*` 경첩 피벗): 광화문 6짝(홍예 3곳) · 흥례문 16짝(가운데·양옆 문 + 양옆 칸 작은 문, 2층 문 `026_Door_F` 뺌) · 근정문 16짝(가운데 3칸 + 양옆 쪽문 + 양옆 행각 문 월화문·일화문 `GJJ_HG_Door01A` 003·027·015·031) · 근정전 6짝(앞면 가운데 3칸의 가운데 두 짝씩 — Hall_16·21·24·22·15·14, 사용자 지시; 나머지 30짝은 떼어 둔 채 안 열림, 안쪽 = 문짝 36짝 가운데 평균). range 모두 6. 그 밖 행각 문은 안 넣음.
- 월화문·일화문 문짝은 데모에서 열린 채(행각 안쪽으로 90°)였음 → `Geunjeongjeon_DoorsCard.prefab` 에서 경첩을 ±90° 돌려 닫고, 두 짝이 문틀 사이(3.10·3.18 m)에 맞게 경첩 로컬 z 배율 0.92~0.96.
- 근정문 문짝(`Geunjeongjeon_DoorsCard.prefab` 의 GJM 12짝): 앞뒤 판 2장(0.48 m 떨어짐, 거울 배치 6짝은 감김이 뒤집혀 남쪽에서 뒤 그림·뒷면이 보였음) → 상자(두께 Door01 0.12 · Door02/03 0.08 m, 앞 그림 = 남쪽 면, 옆·위·아래 면은 가장자리 그림), 자식 변환은 0, 메시 `Simplified/Cards/Geunjeongjeon_DoorBoxes.asset`(첫 메시 이름 = 파일 이름, 000 과 024 는 같은 상자). 문 장식 판 2개(`DoorOrnament03A`)는 `DoorHinge_014` 아래로 옮겨 상자 면 밖 5 mm 에 둠(따로 있던 경첩 2개는 지움).
- 근정전 문짝 분리: `GJ_Hall` 메시(`GJ_GJJ_Card.asset`)의 문 서브메시 6개(`GJCARD_SM_GJJ_Door01A~F`)를 비우고(서브메시 수는 그대로) `GJ_GJJ_LOD/LOD0/GJ_HallDoors` 아래 `DoorHinge_Hall_00~35`(경첩 = 이어진 문짝 줄 가운데에서 먼 쪽 모서리, 아래 끝) + `Leaf`(앞뒤 판 + 옆면 4개, MeshRenderer·MeshCollider, 메시 `Simplified/Cards/Meshes/GJ_HallDoorLeaves.asset`) 로. 렌더러는 `GJ_GJJ_LOD` LOD0 에 넣음.
- **끼임 고침(2026-10-06, 사용자 지적 "붙어서 문을 열면 끼임")**: 문짝이 움직이는 동안 문짝 충돌체를 끄고, 다 움직인 뒤 걷는 캐릭터(CharacterController)가 문짝 충돌체와 겹치면 `Physics.ComputePenetration` 으로 겹친 만큼(+3 cm, 수평) 밀어낸 다음 켬. 확인(Play): 일화문 동쪽 문짝이 열린 자리에 서 있다가 열기 → 0.18 m 밀려남, 겹침 0.
- **일화문 문짝 비뚤어짐 고침(2026-10-06, 사용자 지적 "문이 딱 맞물리지 않음")**: `DoorHinge_015_SM_GJJ_HG_Door01A`(일화문 동쪽 짝) 문짝 상자 방향이 5.22° 틀어져 있었음(문짝 꼭짓점 XZ 주성분 각도로 잼, 나머지 근정문·흥례문·광화문·근정전 문짝 43 개는 0°) → 경첩 Y 회전 270 → 264.78(씬 값, 저장은 사용자). 두 짝 사이 7 cm 틈은 남음.
- 확인(편집 모드): 캡처(닫힘 남·북, 열림), CharacterController 걷기 — 닫히면 4곳 모두 막힘, 열면 광화문·흥례문·근정문(가운데·옆)·근정전 앞 모두 지나감. 월화문·일화문 닫힘/열림·근정전 6짝은 Play 중 캡처로 확인. 콘솔 오류·경고 0. 실제 E 키 입력·문 여는 모습은 사용자 확인 필요.

- **근정전 행각 창틀 뾰족 튀어나옴 고침(2026-10-06, 사용자 지적 "벽에 이상하게 튀어나온 것")**: 원인 = 외부에서 줄인 단순화판 행각 메시(`_Baked/Geunjeongjeon/Simplified/GJ_HG_Seg01~12.asset` 의 LOD0·1·2)에서 창틀(`MI_GJJ_HG_WindowFrame01`) 서브메시가 작은 창(판 `GJCARD_SM_GJJ_HG_Window01A`, 1.37 × 0.67 m) 둘레로 피라미드처럼 뭉쳐 벽에서 0.66 m 튀어나옴(원본 창틀 두께 0.09 m). 고침 = 창틀 서브메시에서 가장 가까운 창 판의 얇은 축(벽 법선)과 법선이 0.3~0.97 로 비스듬한 삼각형을 지움(벽과 나란하거나 수직인 면은 남김) — 36 개 메시, 예: Seg02 1,303 → 808 삼각형. MeshCollider 다시 연결. 캡처로 창 판(창살 그림)만 남은 것 확인. **이 에셋은 git 밖(`Models/Gyeongbokgung/` 무시)이라 되돌리려면 단순화판을 다시 가져와야 함.** 다른 궁 건물 창틀은 확인 안 함.

#### 궁터 배치 (SampleScene 루트 `GyeongBok`, 2026-09-26)
- `GyeongBok` = (−2745, 10, 3830)(광화문 문 가운데, 조선 큰섬 남쪽 만 위 평지). 자식 = 건물별 이름 오브젝트(피벗 = 그 건물의 대표 지점, 사용자가 옮겨 맞춤) 아래 `_Baked` 프리팹 인스턴스: `광화문_Gwanghwamun`, `월대_Woldae(1866)`, `해태상_Haetae(1870)`, `광화문단지_부속건물_GwanghwamunComplex`, `흥례문_Heungnyemun`, `근정전_Geunjeongjeon`, `사정전_Sajeongjeon`, `강녕전_Gangnyeongjeon`, `교태전_Gyotaejeon`, `신무문_Sinmumun`, `경회루_Gyeonghoeru`, `동궁_Donggung`, `건춘문_Geonchunmun`, `영추문_Yeongchumun`. 이름은 C# `\uXXXX` 로 넣음(MCP 한글 깨짐 방지).
- 배치는 관람길 지도처럼 중심축(광화문→흥례문→근정전→사정전→강녕전→교태전→신무문)을 남북으로 붙여 쌓고, 경회루는 서쪽, 동궁은 동남쪽, 건춘문 동쪽, 영추문 서쪽. 거리는 단지 바운드를 맞붙인 어림값(실측 아님). 전체 남북 약 810 m(해태 z 3768 ~ 신무문 4581) × 동서 약 640 m. 높이는 모두 y 10(궁터 평지).
- 이후 사용자가 지형(궁터 북쪽 산을 평지로)과 배치를 손봄 — 위 좌표는 처음 배치 값. 현재 구조: `GyeongBok/GwangHwamun` 그룹 아래 광화문·월대·해태·부속단지(사용자가 만듦). 2026-09-26 확인: 광화문·월대·해태 홀더의 자식 위치가 (0,0,0) 이라 문루가 월대에서 약 24 m 북쪽으로 떨어지고 부속단지와 겹침(프리팹 원점 = 데모 원점이라 자식 위치가 −대표점이어야 붙음: 문루 (0,0,29.6) / 월대 (0,0,−0.1) / 해태 (0,0,−32)) — 사용자 확인 필요.
- 2026-09-30 현재 홀더(로컬): 부속단지 `광화문단지_부속건물_GwanghwamunComplex` (15.4, −0.3, 83.6)(작업 도중 바뀐 값 — Claude 가 옮긴 것 아님), 영추문 (−294.2, −7.37, 330)·회전 93°, 신무문 (0, −7.38, 607.8)·회전 4.98°, 건춘문 (300, 0, 420)(아래 궁장 — 문 바닥을 땅에 맞춤), 경회루 (−133.9, 1.0, 440)(단순화판 로컬 (3.9, 1.59, 27.1) — 사용자가 옮김), 동궁 (99.3, 2.8, 397)(사용자가 사정전·강녕전 땅 높이에 맞춰 올림), 교태전 (24.8, −0.2, 478.7), 간의대 `간의대_Ganuidae` (−130, 2.341, 556)(아래 간의대), 취로정 `취로정_Chwirojeong` (0, 0, 546)(아래 취로정 연못).
- 땅 높이(측정, 2026-09-30): 강녕전 땅판(사정전까지 덮음, x −2810.8~−2678.9 · z 4195.9~4345.9) 윗면 11.75(북쪽 끝에서 그 북쪽 터레인과 1.74 m 턱), 교태전 땅판 11.55, 근정전 마당 `GJ_RR` 10.45·10.70·11.00, 경회루 땅판 12.34(연못 물 11.6, 땅판 북쪽 끝 z 4403.3 — 그 밖 터레인 10.0 과 2.3 m 턱), 동궁 건물 땅 11.72~12.04(남쪽 11.72, 북쪽 11.84~12.04), 그 밖 궁 안 터레인 10.0(정확히 9.996, 취로정 연못 안은 9.2 로 팜).

#### 동궁 자리 터레인 (2026-09-30)
- 사용자 지시: 동궁을 내리지 말고 터레인을 올림(동궁은 사정전·강녕전 땅 높이에 맞춰 사용자가 올린 것). `Island_Joseon_Large` 높이맵 칸 i 605~646 · j 704~783(42×80) 중 3,360점, 최고 12.01 m(+2.01).
- 방법: 동궁 임포스터 오브젝트 경계 상자를 모은 사각형(x −2682.3~−2598.4, z 4143.7~4310.2) 안 = 가장 가까운 오브젝트의 땅 − 0.03 m(땅 = 건물은 판 밑바닥 + 0.6, 담장은 + 0.05; 담장 2.6 m 안은 담장 땅), 밖은 smoothstep 으로 10 m 까지 내려감(서 3.5 m — 근정전 행각까지 3.7 m, 동 6 m — 빈터 묶음까지 7.2 m, 남 9 m — 11.7 m, 북 9 m). 기존 높이보다 낮추지는 않음.
- 확인(측정): 동궁 45 오브젝트 모두 발밑 터레인이 판 밑바닥보다 위(담장 1.7 cm 묻힘이 최소), 땅보다 0.35 m 넘게 묻힌 곳 없음. 빈터 묶음 자리는 그대로 10.0. 캡처(조감·남동 모서리·서쪽 위)로 뜬 곳 없음 확인.
- 되돌리기: 바꾸기 전 높이(float32, 행 = j) `D:\Unity\Haesin\Captures\terrain_backup\Island_Joseon_Large_i605_j704_w42_h80_before_DG.f32` — `TerrainData.SetHeights(605, 704, …)`. 에셋은 저장함(`SaveAssetIfDirty`), Undo 도 등록.
- 궁 안 다른 곳에 10.0 이 아닌 터레인(Claude 가 만든 것 아님 — 확인 필요): 근정전 서쪽 x −2813~−2791 · z 4067~4172 최고 11.28, 그 서쪽 x −2840~−2833 · z 4072~4082 최고 11.14(16점 둔덕).

#### 궁장 (궁궐 담, `GyeongBok/궁장_Gungjang`, 2026-09-30)
- 광화문 양옆 궁장 끝에서 네 모서리를 돌아 건춘문·신무문·영추문 궁장 토막까지 잇는 담(현재 경복궁 지도 모양 — §8). 사슬 4개: C1 광화문 동쪽 날개 끝 → 동남 모서리 (−2442.602, 3884.939) → 건춘문 남쪽 / C2 건춘문 북쪽 → 동북 (−2442.602, 4439.6) → 신무문 동쪽 / C3 신무문 서쪽 → 서북 (−3041.605, 4439.6) → 영추문 북쪽 / C4 광화문 서쪽 날개 끝 → 서남 (−3041.605, 3890.161) → 영추문 남쪽. 마디 = `궁장_Gungjang/Nodes`(월드 XZ 만 씀). 문 쪽 마디는 문 궁장 토막 기와 끝에서 문 쪽으로 0.3 m 들어감(건춘문 남 z 4232.984·북 4272.628, 신무문 동 x −2726.866·서 −2769.069, 영추문 북 z 4177.079·남 4137.435). 전체 2,026 m. 궁장 길의 땅은 모두 10.0(측정).
- 부품(`98.Blender/Gungjang/build_module.py` → HNMS → `PalaceLodBinImport.ImportMeshes` → `_Baked/Gungjang/Meshes/`): 광화문 동쪽 날개 `GHM_WallA3` 의 평평한 구간. 날개 좌표 X = 기준점(프리팹 (∓57.84, 1.49, …))에서 바깥으로 잰 거리, 기와 좁은 줄 가운데 c_k = 3.3456 + 0.26816 k. c_1 ~ c_60(15.821 m)을 평면으로 잘라 3번 이은 47.464 m. 몸통·기단은 새 사각형(u 를 6번·5번 반복 — 원래보다 몸통 무늬 4 % 좁게, 기단 0.4 %), 기단 아래 땅속 2줄(Y −2.5 까지), 용마루 긴 면은 u 를 다시 매김. 삼각형 LOD0 30,614 / LOD1 44(몸통 + 기와 윗면 단면을 밀어 낸 검은 판 `MAT_RoofPlate_Black` + 처마 밑면) / 마구리 21. 부품 바닥 = 월드 y 10.59(광화문 궁장 기단 바닥), 용마루 꼭대기 15.29.
- 배치 `PalaceWallPath`(컨텍스트 메뉴 "궁장 다시 깔기" 또는 `Rebuild()`): 마디 사이에 부품을 이어 깔고, 모서리는 두 방향의 이등분 면(마이터)으로 양쪽을 잘라 맞대고, 문 쪽 끝은 수직으로 잘라 마구리로 막음. 잘린 부품은 `_Baked/Gungjang/Clipped/`(다시 깔 때 옛 것은 휴지통). 부품 47개(잘린 것 12, 24 파일 23 MB) + 마구리 6. LOD: 부품마다 LODGroup(크기 = 부품 길이) LOD0 → LOD1 150 m, 5,000 m 에서 끔, 마구리는 1,500 m. 부품마다 BoxCollider(메시 Y −2.5 ~ 4.7, 두께 3.38). 모두 LOD0 이면 1,314,264 삼각형(LOD1 합 2,332).
- 광화문 날개 끝: 날개 X 19.4352(c_60)에서 잘라 바깥 끝 마감을 없앰 — `PalaceWallPath.TrimGwanghwamunWings(프리팹, "…/_Baked/Gungjang/WingTrim", 19.4352)`. 잘린 사본(A3 14,399 → 10,434, B3 14,394 → 10,431, A1·B1 +2(기단 긴 면), 지붕 껍데기 A3 694 → 566 / B3 699 → 580)을 `GwanghwamunGate_Simplified` 의 MeshFilter·MeshCollider 에 끼움(원래 메시는 `GHM_WallA1.asset`·`GHM_Core_RoofShell.asset` 에 그대로 — 되돌리려면 프리팹 참조만).
- 문 높이(사용자 선택): 영추문 홀더 로컬 y 0 → −7.37·회전 90 → 93°, 신무문 로컬 y −3.95 → −7.38·회전 0 → 4.98°. 두 문은 데모 바닥(약 7.37)만큼 땅 위로 떠 있었고, 데모에서 궁장 토막이 3°·5° 틀어져 있었음. 이제 세 문 모두 궁장 토막 안쪽 조각 바닥 ≈ 9.98(땅 10.0). 궁장 토막 선: 건춘문 x −2442.60, 영추문 x −3041.605, 신무문 z 4439.59~4439.65. 토막 바깥 조각 기와 꼭대기 15.91~16.52 — 새 궁장(15.29)보다 0.6~1.2 m 높아 문 쪽에서 층이 짐.
- 광화문 주변 땅: 문루 기단 판석(`M_GHM_Gst01A`, 홍예 바닥) 11.144, 데모 바닥은 날개 옆 11.13~11.18(측정) → 문루·날개 밑을 11.08(판석 6 cm 아래 — 같은 높이면 깜빡일 수 있어)로 돋우고 경사(남쪽 8 m, 날개 북쪽 2.5 m, 문루 북쪽 8 m, 날개 끝 6 m). `Island_Joseon_Large` 높이맵 칸 2.44 m, 627점, 최대 +1.08 m.
- 경복궁 폴더는 .gitignore 라 궁장 메시도 로컬에만 있음(스크립트 `PalaceWallPath.cs`·`98.Blender/Gungjang/` 은 git).
- 확인: 캡처(광화문 양쪽 이음, 동남 모서리, 건춘문 남쪽 안팎, 신무문 북쪽·서쪽, 영추문 서쪽, 부품 이음, 전체)로 이음매·모서리·문 연결 확인, 콘솔 오류·경고 0. 가까이 모습·LOD 전환(150 m 에서 검은 판)·실제 프레임은 사용자 확인 필요.

#### 지붕 모양 상자(임포스터) — 교태전·동궁 통째 + 빈터 채움 (2026-09-30)
- 사용자 선택: 빈터 건물은 모델 대신 그림을 입힌 상자(윗부분 = 지붕 모양)로. 영추문 ㄱ자 한 채로 시험한 뒤 **교태전·동궁은 통째로** 바꾸고(길이 닿지 않는 곳), **빈터는 서쪽 + 동쪽(A·B·B′)** 을 채움. 북쪽 띠 C 는 비워 둠.
- 껍데기(오브젝트마다, 단지 프리팹 루트 좌표): 지붕 판(위→아래 광선 윗면 + 아래→위 처마 밑면을 닫은 판) + 몸통(칸마다 바닥 높이 위 0.4~1.76 m 단면을 닫고, 지붕 덮인 구멍만 채움) + 기단(땅보다 8 cm 높은 곳 중 몸통 5 m 안 — 윗면 = 광선 높이라 계단은 비탈 판) + 툇간 기둥 상자. 0.25 m 격자 → 윤곽선에 붙이고 meshoptimizer 5 cm 로 줄임. 판 밑바닥 = 땅 − 0.6 m. 높이 3 m 미만이거나 지붕이 없으면 담장 방식(기와 머리 = 지붕). 3,000 삼각형 미만 오브젝트와 넓은 땅판(재질 삼각형 평균 50 m² 넘음)은 원본 그대로(Keep). 문·창 판(카드)은 굽기 장면에만 넣음(벽 그림으로 들어감).
- 그림: 면 방향 6가지(상자 투영) 조각을 여러 건물이 함께 쓰는 4096 페이지에 길로틴 방식으로 채움. 텍셀마다 껍데기 면에서 원본 쪽으로 2×2 광선(알파 클립 판은 뚫린 곳을 지나감) → 기본색 × (0.5 + 0.5·AO(8 광선, 1.2 m)) = `_BC`(DXT1), 물체 공간 노멀 반 크기 = `_NOS`(BC7, sRGB 끔). 마스크 없음(매끄러움 0.2). 재질 = HDRP/Lit, Normal Map Space = Object(키워드 `_NORMALMAP`) — 메시 탄젠트가 필요 없음.
- 밀도(px/m, 지붕 윗면 / 벽): 교태전 37 / 37(1 페이지), 동궁 33 / 20(먼 프로필 `IMP_PROFILE=far`, 2 페이지), 빈터용 영추문 남쪽 3채 32 / 32(2048 한 장). 광선 맞음 교태전 83.4 %, 동궁 91.9·77.2 %, 영추문 3채 89.3 %(나머지 = 가려진 곳, 가까운 색으로 채움).
- 삼각형(측정): 교태전 1,110,676 → 21,768(껍데기 5 오브젝트 18,953 + 원본 그대로 2,815: 땅판 `MI_GTJ_OutFloor01` 19·`GTJ_GSJ` 84·굴뚝 2 × 1,356), 동궁 4,189,471(LOD0)·1,911,954(LOD1) → 62,399(45 오브젝트). 그림 메모리(에디터 측정 — 에디터 값은 계산값의 2 배로 보임): 교태전 32 MB · 동궁 64 MB · 영추문 3채 8 MB. 옛 단순화판이 참조하던 텍스처는 교태전 508 장 1,859 MB · 동궁 282 장 867 MB(에디터 측정 — 다른 단지와 같이 쓰는 텍스처도 있어 실제로 줄어드는 양은 확인 필요).
- 씬: `GyeongBok/교태전_Gyotaejeon/Gyotaejeon_Impostor`·`GyeongBok/동궁_Donggung/Donggung_Impostor` 를 옛 `*_Simplified` 와 같은 로컬 자리에 두고 옛 것은 끔(홀더는 켬). 오브젝트마다 LODGroup 한 단계(화면 높이 0.002 에서 끔), 그림자 켬, 충돌체 없음.
- 빈터 채움 `GyeongBok/빈터채움_Fill`: **2026-09-30 크기 다양화로 다시 배치 — 묶음 43개, 303,491 삼각형(측정)**(처음 배치는 47개 321,830, 모두 같은 크기). 묶음 = 동궁 임포스터 오브젝트 몇 개(동궁 페이지를 그대로 써 그림 메모리가 안 늘어남) — `DG_S`(본채 Bld_06 ㅁ자) 9,452 · `DG_E`(Bld_05 + ㄱ자 행각) 4,814 · `DG_NE`(두 채) 4,513 · `DG_MID` 9,660 · `DG_WM`(긴 행각 둘) 15,326 · `DG_N`(HG_24, 북쪽 트임) 5,577 — 과 영추문 남쪽 `YCM_T` 3,028 · `YCM_L1` 1,144 · `YCM_L2` 1,150. 묶음마다 메시 하나로 합침(렌더러 1, 서브메시 = 페이지). 배치: 구역(궁장·이웃 단지에서 6~8 m 띄움) 안을 북쪽부터 동서 줄로 채움, 골목 6~11 m·줄 사이 7~12 m, 75 % 는 90° 회전(동궁 본채 정면 = 데모 +x 가 남쪽), 나머지 270°, 땅 = 월드 10(동궁 묶음 데모 땅 −0.9, 영추문 3채 7.37 을 여기에 맞춤). (처음 배치 개수: YCM_T 7, DG 묶음 6종 × 6, YCM_L1 3, YCM_L2 1.)
- 크기 다양화(사용자 지시 "반복을 줄이기 위해 몇 개의 크기를 각각 다르게", 2026-09-30): 배치마다 약 3/4 을 묶음 로컬 x(깊이) 0.85~1.2 · z(길이) 0.8~1.3 · y(높이) 0.9~1.12(넓이를 조금 따라감)로 늘이고 줄이고, 그중 약 40 % 는 x 를 뒤집음(거울 — 반복을 줄이려 Claude 가 더함). 계획 `fillplan.py 7`(씨앗 7) → `D:\Unity\Haesin\Captures\impostor\fillplan_v2.tsv`(열: 묶음·x·z·회전·배율 x·y·z; 처음 계획 `fillplan.tsv` 는 그대로 둠) → `PlaceFill("GyeongBok/빈터채움_Fill", …fillplan_v2.tsv, float.NaN)`. 결과 43개 중 원래 크기 13, 거울 13(측정). 개수: DG_N·YCM_T·DG_WM·DG_S·DG_E 각 6, DG_NE·DG_MID 각 5, YCM_L2 2, YCM_L1 1. 땅 = 발자국 안 5×5 점 터레인 가운데값(최저 + 0.5 m 를 넘지 않게) — 모두 9.996. 경계 상자 겹침 0, 묶음끼리 최소 7.04 m, 다른 단지·궁장과 최소 6.14 m. 거울·늘인 묶음의 빛·면 방향 캡처로 확인(뒤집힌 면 없음).
- 첫 시험(영추문 ㄱ자 한 채, 2048 그림 3장)의 에셋은 휴지통, 씬 `빈터_시험` 은 지움.
- **궐내각사 가림 `GyeongBok/궐내각사가림_Screen`(2026-10-04, 사용자 지시 "근정전에서 경회루로 가는 길목 궐내각사 이상하게 보이는 부분 안 보이게" — 광화문 부속단지 `SMJC_Baked` 의 긴 행각 한 채를 씀)**: `_Baked/Gwanghwamun/SMJC_Baked.prefab` 메시 4개에서 그 한 채(프리팹 로컬 x 36~48 · z 52~83)의 삼각형만 떼어 메시 하나(서브메시 = `MAT_SMJC_roofenv/stone/win/wood`, 54,891 삼각형, 길이 26.03 · 깊이 6.87 · 높이 5.53 m, 원점 = 바닥 가운데)로 → `_Baked/Gwanghwamun/Screen/SMJC_LongHall.asset`. 지금 모습(2026-10-04, 사용자 지시 "빨간색 두 곳을 채우고 나머지 가림막은 지우고 일렬로" → "빈 곳에 채우라고" → "원본 화질로"): 근정전 서쪽 행각(서쪽 면 x 약 −2802) 서쪽에 남북으로 `Hall_01~04` 4채를 빈틈없이 이어 놓음 — 사용자가 옮겨 둔 두 채의 양 끝(z 4097.27 · 4196.09) 사이를 4등분(한 채 24.70 m, 길이 배율 0.949, 회전 180°), x −2818.53~−2811.65 한 줄, 바닥 12.22. 각 채 = 프리팹 `Screen/SMJC_LongHall_Orig.prefab`: LOD0 = `Gwanghwamun_Original.prefab` 에서 그 한 채 렌더러 303개(248,456 삼각형, 원본 메시·재질 그대로 — 처음 쓴 구운 메시는 화질이 낮다는 지적) / LOD1 = 구운 `SMJC_LongHall.asset`, 전환 약 120 m, 0.01 에서 끔, BoxCollider. 땅: 그 자리 터레인(`Island_Joseon_Large`)은 z 4128 북쪽·x −2817 동쪽만 12.32 이고 나머지 10.0 이라 4채 바닥(x −2819.6~−2810.6 · z 4096.3~4197.1)을 12.32 로 올리고 둘레 5 m 섞음(올리기만, 헤이트맵 i 548~559 · j 686~735 중 193점, 최대 +2.32 m, Undo 등록, 파일 백업 없음) — 건물 발밑 12.30~12.32, 기단 8~10 cm 묻힘. Play 캡처로 이어진 줄·땅에 앉음 확인. 가까이 모습·프레임은 사용자 확인 필요.
- 확인(Unity 캡처, 같은 시점 비교): 교태전 조감(약 100 m)에서 교태전 자리 화면 차이 평균 2.8/255(20 넘게 다른 픽셀 3.7 %). 동궁 조감은 옛 LOD1(기와 윗면 판)보다 기와 결이 또렷함. 교태전 마당 30 m 에서는 현판 글씨가 흐리고 용마루 잡상·추녀 끝이 그림으로 납작하며 계단은 비탈 판에 그린 계단. 콘솔 오류·경고 0. 실제 프레임은 사용자 확인 필요.
- 다시 만들기: 입력 = 단순화판 LOD0 를 `PalaceBinExport` 로 내보낸 것(`D:\Unity\Haesin\Captures\impostor\src\<단지>\`) + 텍스처 캐시(`…\impostor\tex\index.tsv` + PNG), 빈터용 영추문 3채 = 영추문 데모 내보내기(`…\FourDoor\Source\Yeongchumun\`) + 512² npy 캐시(`D:\Unity\Haesin\Temp\PalaceSimplify\tex` — Unity Temp 라 지금 기기엔 없음, 없으면 materials.tsv 의 원본 텍스처를 1024 로 줄여 바로 읽음). `impcx.py <단지> <페이지 수>`(결과 `Captures\impostor\out\<단지>\`: `<단지>_Imp.bin`·`_Keep.bin`·`P<k>_BC/NOS.png`·`layout.json`·`bbox.json`) → 그림을 `_Baked/Impostor/<단지>/IMP_<단지>_P<k>_BC/NOS.png` 로, bin 을 `Captures\impostor\<단지>\` 로 → `PalaceImpostorTool.Setup(단지, bin 폴더, 옛 단순화판 프리팹, 페이지 수)`(메시 `…/<단지>/Meshes/`, 재질 `IMP_<단지>_P<k>`, 프리팹 `<단지>_Impostor.prefab` — 같은 경로에 덮어써 씬 연결 유지) → `PalaceLodBinImport.Place(홀더, "<단지>_Simplified", 프리팹, "<단지>_Impostor")`. 빈터: `fillplan.py`(→ `fillplan.tsv`) → `PalaceImpostorTool.BuildKit` 9개(`_Baked/Impostor/Kits/`) → `PlaceFill("GyeongBok/빈터채움_Fill", tsv, float.NaN)`(계획에 없는 옛 묶음은 지움; 땅에 숫자를 주면 그 높이).

#### 간의대 (簡儀臺, `GyeongBok/간의대_Ganuidae`, 2026-09-30)
- 사용자 지시: 경회루 북쪽에 간의대를 삼각형 최대한 적게 만들어 놓음. 조사(아래 §9 궁 안 빈터): 세종 때 경회루 북쪽 돌 대, 명종 원년(1546) 중수 기록이 있어 인종·명종대에 있었음.
- 모델 131 삼각형(측정): 석축 네 면 + 윗면, 남쪽 면에 붙인 곧은 돌계단 24단(챌면 0.2625 m · 디딤 0.34 m, 서→동으로 오름, 동쪽 끝 계단참 1.4 m) + 소맷돌(바깥 벽, 디딤 모서리선보다 0.55 m 높음), 윗면 돌난간 판 4장(높이 1 m, 계단참 쪽은 열림, 양면). 크기 = 높이 6.3 · 동서 9.7 · 남북 6.6 m(자료의 31·47·32 척 환산 — 어느 변이 동서인지·계단 자리·난간 모양은 기록에 없어 가정). 꼭대기 간의·정방안, 서쪽 규표·소각(혼의·혼상)은 따로 `Ganuidae_Instruments`(아래 기구).
- 재질(서브메시 순서): `MAT_GYD_Wall`(석축, `T_GCM_BrickStone01A`, 4.19 m 한 장) 21 · `MAT_GYD_Step`(`T_GCM_Stone03`, 0.94 m) 98 · `MAT_GYD_Floor`(윗면·계단참 전돌, `T_GCM_BrickStone`, 3.16 m) 4 · `MAT_GYD_Rail`(돌난간 판, 알파 클립 0.5, 양면) 8. 석재 세 재질은 `_Shared` 의 `MI_GCM_*` 를 복사해 타일 1 로(그림 한 장 크기 = 신무문 원본 메시에서 잰 값). 난간 그림 `GYD_Rail_BC.png`(512×256, 한 마디 1.833 m = 반 기둥 0.195 + 하엽동자 2개 칸 1.443 + 반 기둥 0.195, 높이 1 m) = 흥례문 데모 영제교 서쪽 돌난간을 옆에서 평행 광선으로 찍어 붙인 것(색만, 빛 안 구움). u 는 마디 수만큼 되풀이(Repeat U / Clamp V).
- 에셋 `_Baked/Ganuidae/`: `Ganuidae.fbx`(22 KB), 재질 4, `GYD_Rail_BC.png`, `Ganuidae.prefab`(LODGroup 한 단계, 화면 높이 0.004 에서 끔, 그림자 켬, 충돌체 없음). 씬: 홀더 월드 (−2875, 12.341, 4386)(경회루 누각 가운데 x, 경회루 땅판 위 — 연못 북쪽 끝 z 4365.2 와 땅판 북쪽 끝 4403.3 사이), 인스턴스 로컬 0. 월드 경계 x −2879.85~−2870.15 · y 11.94~19.64 · z 4381.10~4389.30. 씬 저장은 사용자.
- 다시 만들기: `98.Blender/Ganuidae/build_ganuidae.py`(치수는 파일 위쪽) → `_Baked/Ganuidae/Ganuidae.fbx` 덮어쓰기 → 프리팹 렌더러 재질은 서브메시 순서대로(이 폴더 FBX 는 재질 가져오기 None — `PalaceBakedImportPostprocessor`). 난간 그림: `railtex.py`(흥례문 데모 내보내기 + 512² npy 캐시).
- 확인: 캡처(남서 위·남동·연못 건너·윗면)로 계단·소맷돌·난간 확인, 콘솔 오류·경고 0. 가까이 모습(난간이 판이라 옆에서 납작)은 사용자 확인 필요.
- **기구(2026-09-30, `GyeongBok/간의대_Ganuidae/Ganuidae_Instruments`)**: 사용자 지시 "간의대 위에 간의, 남쪽 정방안, 서쪽 규표·혼의·혼상". 배치 근거 = 『세종실록』 세종 19년(1437) 4월 15일 「주야 측후기인 일성정시의가 이룩되다」(경회루 북쪽 돌 대(높이 31·길이 47·넓이 32척) 위에 간의, 정방안은 그 남쪽, 대 서쪽에 동표(8척 얼의 5배 = 40척)와 청석 규(면에 장·척·촌·분), 표 서쪽 작은 집에 혼의(동)·혼상(서), 혼상은 베에 칠, 둘레 10척 8촌 6분).
  - 자식(홀더 로컬, 삼각형 측정): `GYD_Ganui` 444 — 대 윗면(6.3) 위 받침틀 약 1.9 × 3.9 m(z −1.57~2.37), 극축(한양 위도 37.57°, 3 m), 백각환 r 0.95·적도환 r 0.84(극축에 수직, 남쪽 끝), 사유환 쌍고리 r 0.78 + 규형, 정극환 r 0.2, 북쪽 ㅅ자 받침(꼭대기 9.31) / `GYD_Jeongbangan` 10 — 1.2 × 1.2 m 돌판(z −2.9~−1.7) / `GYD_Gyupyo` 84 — x −13: 규 0.9 × 15.8 m·높이 0.45(z −5.3~10.5), 표 받침돌 1.3 m, 8각 구리 표, 규 윗면에서 40척(8.24 m)에 횡량 1.6 m / `GYD_Sogak` 308 — 가운데 (−24, −5.5), 기둥 3 × 2(칸 2.6·3.0 m), 기단 6.4 × 4.2, 팔작지붕(처마 내밈 1.0, 꼭대기 4.7), 동쪽 디딤돌 / `GYD_Honui` 388 — 소각 동쪽 칸, 지름 약 1.34 m, 지평환·자오환·적도환·황도환·사유환 + 극축 + 열십자 받침 / `GYD_Honsang` 352 — 서쪽 칸, 지름 0.71 m 구(12 × 8, 별그림) + 지평·자오 고리 + 받침. 합계 1,586.
  - 가정(기록에 없음 — 확인 필요): 간의 크기·짜임(원나라 곽수경 간의의 일반 구조를 줄임), 규 길이(동지 정오 그림자 약 14.9 m 가 들어가게), 1척 = 0.206 m(주척), 소각 크기·지붕, 혼의 고리 구성·크기, 받침 모양. 혼상 별 위치는 무작위 장식(실제 성도 아님), 28수 경계는 흔히 쓰는 옛 도수로 나눈 장식, 적도 빨강·황도 노랑.
  - 재질: `MAT_PAV_Palette`(`_Baked/Pavilion/`, 4×4 칸 단색 그림 + 마스크(R 금속·A 매끄러움) — 기둥 빨강·창방 초록·마루 흰색·지붕 밑면·나무·청동 등, 양면, 면마다 칸 가운데 한 점 UV), `MAT_PAV_Roof`(기와 그림 1.08 m 한 장 + 노멀), `MAT_PAV_Rail`(정자 난간, 알파), `MAT_GYD_Jeongbangan`·`MAT_GYD_Gyu`(규 윗면 눈금, 세로 2.06 m 되풀이)·`MAT_GYD_Honsang`(별그림), 기존 `MAT_GYD_Step`·`MAT_GYD_Floor`. 그림은 모두 코드로 만든 것(`98.Blender/Ganuidae/maketex.py` — 석재 바탕만 원본 `T_GCM_Stone03` 캐시).
  - 프리팹 `_Baked/Ganuidae/Ganuidae_Instruments.prefab`: 자식 6(메시 = `Ganuidae_Instruments.fbx`), 자식마다 LODGroup 한 단계(끄는 화면 높이 간의 0.008 · 정방안·혼의·혼상 0.01 · 규표·소각 0.004), 그림자 켬, 충돌체 없음. 씬 인스턴스 로컬 0. 월드 경계: 규표 x −2888.8~−2887.2 · z 4379.4~4396.5, 소각 x −2902.9~−2895.1 · z 4377.7~4383.3(경회루 땅판 위).
  - 다시 만들기: `98.Blender/Ganuidae/build_instruments.py`(치수는 함수 안)로 `Ganuidae_Instruments.fbx` 덮어쓰기(재질 순서 표 = `Captures/crj/*_materials.json`, 메시 이름이 같으면 프리팹 연결 유지).
  - 확인: 캡처(간의대 위, 소각 안, 규표 남동쪽), 콘솔 오류·경고 0. 가까이 모습·실제 프레임은 사용자 확인 필요.

#### 취로정 연못 (翠露亭, `GyeongBok/취로정_Chwirojeong`, 2026-09-30)
- 사용자 지시 "취로정 연못을 그 형태 맞춰서", 선택: 교태전 북쪽 가운데·줄여서 / 모서리 둥근 네모 + 북쪽 둑에 취로정 / 사방이 트인 작은 정자. 근거: 위키백과 「향원정」(세조 2년(1456) 후원에 취로정을 짓고 그 앞에 연못을 파 연꽃을 심었다는 『세조실록』 기록, 이 연못이 지금 향원지 자리로 여겨짐, 향원지 = 모서리를 둥글게 한 네모 연못 4,605 m²), 『세조실록』 세조 5년(1459) 4월 22일 「경회루에 나아가 관사하고, 취로정에 나아가 관가하다」. 세조 때 연못 모양·크기 기록은 확인 못 함.
- 홀더 로컬 (0, 0, 546) = 월드 (−2745, 10.0, 4376). 연못 52 × 48 m(월드 x −2771~−2719 · z 4352~4400, 모서리 반지름 7 m, 약 2,450 m² — 향원지의 약 53 %). 홀더 기준 높이: 둘레돌(너비 0.5) 윗면 +0.32, 안쪽 석축 +0.32~−0.72, 물 −0.40(월드 9.60, `MI_KHL_Water`), 잔디 둔덕(`MI_KHL_Ground`) 안쪽 +0.18 → 바깥 끝 +0.06(x ±33 · z −30.5~36, 모서리 10 m — 남쪽 끝은 강녕전 땅판 밑), 연잎 판 8장(알파, 물 위 1.2 cm). 정자(가운데 로컬 (0, 28) = 월드 z 4404, 신무문 안쪽 면까지 약 12.6 m): 돌 기단 4.4 × 4.8, 기둥 4(3.0 m 간격), 나무 마루 +1.15, 난간 동·남·서(북쪽 나무 계단 2단), 사모지붕(처마 내밈 1.1 m) + 절병통, 꼭대기 +5.5. 크기·짜임은 가정.
- 삼각형(측정): `Chwirojeong_Pond` 280(잔디·둘레돌·석축) · `_Water` 26 · `_Lotus` 16 · `_Pavilion` 286 = 608. 프리팹 `_Baked/Chwirojeong/Chwirojeong.prefab`(자식마다 LODGroup 한 단계: 연못·물 0.002, 연잎 0.006, 정자 0.004에서 끔), 물·연잎 그림자 끔. 연잎 그림 `CRJ_Lotus_BC`(`MAT_CRJ_Lotus`, 알파 클립), 정자는 `_Baked/Pavilion/` 재질.
- 충돌체: `Chwirojeong_Pond` 에만 MeshCollider(FBX 읽기 가능 켬) — 연못 자리 터레인을 파서, 없으면 둔덕 위에서 파인 곳으로 떨어짐. 물·연잎·정자는 없음(연못 안 바닥 = 터레인 9.2, 물 밑 0.4 m). 레이 확인: 둔덕 10.07~10.16, 둘레돌 10.32.
- 터레인(`Island_Joseon_Large`): 칸 i 566~599 · j 790~820 중 연못 윤곽에서 3.46 m(칸 대각선) 안 532점을 9.996 → 9.2 로. 칸 사이 보간으로 윤곽 밖 약 5.9 m(모서리 쪽 약 6.9 m)까지 낮아지는데 잔디 둔덕(윤곽에서 6.5 m 이상)이 덮음. 되돌리기: `Captures/terrain_backup/Island_Joseon_Large_i566_j790_w34_h31_before_CRJ.f32` → `SetHeights(566, 790, …)`. 에셋 저장, Undo 등록.
- 이 자리: 강녕전 땅판 북쪽 끝(z 4345.9, 11.74)과 연못 쪽 땅 사이 1.74 m 턱(원래 있던 것). 터레인은 바위·절벽 레이어로 칠해져 있어(원래) 잔디 둔덕 끝이 흙바닥과 뚜렷이 갈림.
- 다시 만들기: `98.Blender/Chwirojeong/build_chwirojeong.py`(치수는 파일 위쪽) → `_Baked/Chwirojeong/Chwirojeong.fbx` 덮어쓰기 → 씬 연못 MeshCollider 에 메시를 다시 넣을 것(§6). 윤곽·높이를 바꾸면 터레인을 백업에서 되돌린 뒤 다시 파야 함.
- 확인: 캡처(남쪽 위, 정자 가까이, 북쪽 약 340 m), 콘솔 오류·경고 0. 가까이 모습·실제 프레임은 사용자 확인 필요.

#### 육조거리 (광화문 앞, `GyeongBok/육조거리_Yukjo`, 2026-09-30)
- 사용자 지시: 광화문 앞 육조거리 — 관아는 강녕전·사정전·교태전·동궁 건물을 지붕 모양 상자(임포스터)로, 길에 닿는 벽·건물 겉은 깨진 곳 없이, 길에서 약 110 m(3~5줄) 안쪽까지(그 뒤 민가는 사용자가 만듦), 현판은 뺌(재질 이름 `Stela`), 건물은 섞어 씀. 길가는 "멀쩡한 벽 한 칸과 문을 떼어 일자로 이어 붙이기"(사용자가 준 서울역사박물관 육조거리 복원 모형 사진처럼), 모서리는 근정전·흥례문 행각 모서리처럼.
- 길(터레인 `Island_Joseon_Large`, `98.Blender/Yukjo/roads.py`): 사용자가 손으로 올린 길(13.52)·광화문 앞 띠(10.75)를 곧게 다시 만듦 — 남북 길 x −2762~−2728(광화문 축 x −2745, 폭 34) · z 3296~3872, 동서 길 z 3462~3496 · x −2920~−2560, 띠 x −2826~−2664 · z 3852~3861 높이 10.75. 길 바닥 = 손대기 전 땅(손댄 칸을 둘레 값으로 조화 보간해 추정) + 0.3(땅 11.0 에 가까울수록 0 — 광화문 앞 돋운 땅). 측정: 길 10.30, 남쪽 끝(z 3320) 10.07, 관아 마당 10.00. 길·관아 마당은 `Sand_aerial_sand`(칸 2)로 칠함.
- 터레인 백업(`Captures/terrain_backup/`): 높이 `Island_Joseon_Large_i444_j347_w280_h271_before_YJ.f32`(길), `…_i604_j439_w42_h155_before_YJback.f32`(동쪽 뒤채 자리), 칠 `…_alpha_i502_j355_w165_h247_L8_before_YJ.f32`(길)·`…_alpha_i547_j443_w70_h147_L8_before_YJyard.f32`(마당) — 칠은 8 레이어 float32.
- 홀더 `육조거리_Yukjo` 월드 (−2745, 10, 3680): `행랑_Haengrang`(조각 116), `관아_Gwana`(관아 안쪽 6), `뒤채_Back`(빈터 묶음 재사용 15 — 서쪽 x −2870~−2826, 동쪽 x −2665~−2618). 모두 땅 y 10(측정 9.98~10.00). 씬 저장은 사용자. (`관아_Gwana` 아래 `Gwana_SmallA`(프리팹 `08.Environment/Gwana/Prefabs/Gwana.prefab`, 충돌체 23)와 육조거리 홀더 경계가 x −842 · z 19 까지 늘어난 것은 Claude 가 만든 것 아님 — 확인 필요.)
- **충돌체(2026-10-05, 사용자 지적 "육조거리 벽이 뚫림")**: 묶음 프리팹(`_Baked/Impostor/Kits/`)에 MeshCollider 를 넣음 — `YJR_BAY`·`YJR_CORNER` 는 자식 `Collider` = LOD2(임포스터 껍데기, 684 / 2,063 삼각형), `YJR_GATE` 도 LOD2(2,452 — 문짝 자리까지 막힘), `YJR_RING_START/END`·`YJ_R_*` 6개·빈터 묶음 9개(`DG_*`·`YCM_*` — 궁 안 `빈터채움_Fill` 도 같은 프리팹이라 같이 막힘)는 자기 메시. 육조거리 오브젝트 160개 모두 충돌체 있음. 걷기 시험(Play, CharacterController 를 길 가운데에서 옆으로 3 m/s): 서쪽 줄은 x −2767.7(정면 처마 −2766 보다 1.7 m 안 = 벽), 동쪽 줄은 x −2722.2 에서 멈춤, 서쪽 문(z 3560)은 x −2770.8(문짝 자리)에서 멈춤.
- 행랑(길가 줄행랑, `yj_row.py`): 사정전 단순화판 남행각에서 뗀 조각(조각 좌표 x = 길 따라, z = 처마 앞 끝부터 안쪽, 깊이 9.88). `BAY` = 두 칸(원래 x 14.130~20.205, 6.075 m — 한 칸(2.89~3.17 m)은 기와 줄 간격 0.3565 m 의 정수배가 아니라 이으면 최대 18 cm 어긋남, 두 칸 = 17줄로 어긋남 1.5 cm), `GATE` = 사정문 + 양옆 행각(22.083 m, 현판 뺌, 문짝 10짝 다 있음). **길 쪽 지붕면(처마 ~ 용마루 선) 기와는 원본**(사용자 지시 "거리쪽 기와는 조금 살려줄수있나요" → 길 쪽 지붕면만): `PalaceYukjoTool.ExportOrigTiles` 가 `Sajeongjeon_RoofLOD.prefab` LOD0(데모 원본 기와)에서 남행각 `MI_SJJ_HG_Roof01`·사정문 `MI_SJM_Kiwa` 삼각형(무게중심 x −10~22, z < 9.5)을 `Captures/impostor/out/YJR/orig_tiles.bin` 으로 내보내고, `yj_row.py` 의 `orig_front` 가 단순화판 기와를 용마루 면(남행각 단지 z 7.49, 사정문 = 원본 기와 가장 높은 곳 z 7.70)에서 잘라 뒤쪽만 남기고 앞쪽은 원본으로 바꿈. 용마루·용머리·마루돌은 단순화판. 모서리·옆 행각 BAY 도 같은 조각이라 바깥 지붕면이 원본. 줄 = [모서리] BAY×5 GATE BAY×16 GATE BAY×15 GATE BAY×5 [모서리]. 서쪽 줄 정면 x −2766(회전 270, z 3502.8~3844.1), 동쪽 줄 정면 x −2724(회전 90, z 3843.2~3501.9), 문 시작 = 줄 좌표 45.67 / 164.96 / 278.16 m.
- ㄱ자 모서리 `YJR_CORNER`(`yj_corner.py`): BAY 두 개를 45° 대각선 면에서 잘라 맞댐(바깥 추녀·안쪽 골, 바깥 모서리 기둥 하나) + 추녀마루(BAY 용마루 한 토막을 대각선을 따라 기와 윗면 높이로 내린 것, 폭·높이 0.8 배, 아래 끝 막음). 줄 날개 = 이음(x 0)~바깥 처마 11.475 m, 옆 날개 = 앞 처마~이음 14.5 m. 모든 행각은 BAY 앞(돌벽·창)이 바깥, 뒤(문)가 마당 — 위에서 보아 반시계로 돌면 바깥이 오른쪽이라 모서리 하나를 돌려 네 모서리에 다 씀.
- 줄 끝 관아 둘(길 양쪽 남·북 끝)은 행각으로 둘러쌈: 길가 모서리 → 바깥 옆 행각 BAY×4(진짜 — 동서 길·광화문 앞에서 보임) → 뒤 바깥 모서리(진짜, 뒤 처마 = 줄 정면에서 50.3 m) → 뒤 행각 BAY×14 + 안쪽 모서리 + 안쪽 옆 행각(길 쪽 끝은 줄 용마루 면(줄 정면에서 4.97 m)에서 자름 — 길에서는 줄 지붕에 가려 안 보임) = 임포스터 껍데기 한 덩어리 `YJR_RING_START`/`YJR_RING_END`(LOD 하나). 안쪽 옆 행각 바깥 처마 = 줄 좌표 111.8 / 231.1 m. 가운데 관아 둘은 원래 행각 둘레(교태전·강녕전 틀, 줄 뒤 처마에서 자름)를 그대로 씀.
- 관아 안쪽 `YJ_R_*`(`BuildRest`, 틀 A = 사정전, B = 강녕전, C = 교태전 임포스터 `_Baked/Impostor/YJ_A·B·C/` 에서 오브젝트를 골라 합친 메시 하나): 서쪽 남→북 `YJ_R_AB_S`(B:GNJ·GSJ·YSJ) / `YJ_R_C`(C:GTJ·GSG·HHG·WGH·GSJ) / `YJ_R_AB2_N`(A:SJJ + B:EJD·YGD), 동쪽 북→남 `YJ_R_A_N`(A:SJJ·MCJ·CCJ) / `YJ_R_BA`(B:강녕전 행각 + A:SJJ·MCJ·CCJ + B:EJD·YGD) / `YJ_R_AB3_S`(A:SJJ + B:GSJ·YSJ). 명세는 `yj_plan2.py` 의 `REST`(줄 끝 넷은 안쪽 옆 행각 HG_E/HG_W 를 뺀 것 — Unity 에서 `BuildRest` 로 다시 만듦).
- LOD(행랑 조각·모서리, CrossFade): LOD0 원래 메시(조각 기준점에서 약 45 m) / LOD1 8 cm 줄임(자른 면 정점 고정 — 이웃 조각과 단계가 달라도 틈 없음, 약 130 m) / LOD2 임포스터 껍데기(1,200 m 에서 끔).
- 삼각형(측정, LOD0 / LOD1 / LOD2): BAY 13,200 / 7,374 / 684 · GATE 165,491 / 32,531 / 2,452 · 모서리 42,750 / 23,714 / 2,063 · 둘레 껍데기 14,525(START)·14,283(END) · 관아 안쪽 AB_S 11,296 · AB2_N 5,736 · A_N 6,848 · AB3_S 8,296 · C 16,727 · BA 27,464 · 뒤채 15개 93,713. 길 가운데 눈높이에서 그려지는 육조거리 삼각형(LOD 선택 계산): z 3480 약 80만 · 3560 128만 · 3675 131만 · 3790 125만 · 3850 82만(길 쪽 원본 기와 전: 63만~111만).
- 에셋: 메시 `_Baked/Yukjo/YJR/Meshes/`(BAY·GATE·CORNER·RING)·`_Baked/Yukjo/Rest/`(관아 안쪽 6), 프리팹 `_Baked/Impostor/Kits/YJR_BAY·GATE·CORNER·RING_START·RING_END`·`YJ_R_*`, 임포스터 `_Baked/Impostor/YJ_A·B·C/`. 옛 방식(단지 앞쪽을 통째로 원래 메시로 둔 `YJ_A`~`YJ_C2` 묶음·`YJR_END_W/E`·앞쪽 메시)은 휴지통.
- 다시 만들기(자료 `Captures/impostor/out/`): `yj_build.py <A|B|C> rowbake`(임포스터·행랑 껍데기, 페이지가 이미 있으면 `FORCE=1`) → Unity `PalaceImpostorTool.Setup("YJ_<t>", …/out/YJ_<t>, 단순화판 프리팹, 페이지 수 A 2 · B 1 · C 1)` → Unity `PalaceYukjoTool.ExportOrigTiles(…/YJR/orig_tiles.bin)` → `yj_row.py`(YJR/YJR_Modules.bin, BAY·GATE) → `yj_corner.py`(YJR_Corner.bin) → `yj_plan3.py`(YJR_Ring.bin, yj_row3.tsv) → Unity: `PalaceLodBinImport.ImportMeshes(dir, Root + "/YJR", 30, "", <bin>, "Meshes")` → `PalaceYukjoTool.BuildRowModules`·`BuildCornerRing(dir, 사정전 단순화판 프리팹)`·`BuildRest`(임포스터 페이지 배치가 바뀌면 모서리·둘레 껍데기·관아 안쪽도 다시) → `PalaceImpostorTool.PlaceFill("GyeongBok/육조거리_Yukjo/행랑_Haengrang", yj_row3.tsv, 10)`.
- 확인: 캡처(동서 길에서 남쪽 모서리 가까이·뒤 모서리, 광화문 앞에서 북쪽 모서리, 뒤 행각 옆, 안쪽 옆 행각이 줄에 붙는 곳 위·길, 남쪽 네거리에서 길 전체, 모서리 LOD1·LOD2 강제)로 이음매·모서리·길에서 안 보임 확인, 머티리얼 null 0, 콘솔 오류·경고 0. 가까이 모습·실제 프레임은 사용자 확인 필요.

### 호수 · 나룻배 (육조거리 남쪽, 2026-10-04 / 나룻배 타기 2026-10-05)
- 호수: 사용자가 육조거리 남쪽 끝과 마을(기와·초가 마을 띠) 사이 만을 파서 만든 것(터레인 `Island_Joseon_Large`, 대략 x −3450~−1950 · z 3195~3440, 깊이 −10~−38 m). 북쪽 물가가 육조거리 길(예전 남쪽 끝 z 3296)을 z 약 3400 까지 잘라 먹음. **서남쪽 입구(x 약 −3440~−3300, z 약 3255, 폭 약 140 m)로 바다와 이어져 있음**(측정) — 물은 HDRP 바다(`Ocean`) 그대로.
- 파도: `WaterShoreMask.calmZones` 의 `육조거리 남쪽 호수` 다각형(꼭짓점 (−3455, 3264)·(−3455, 3520)·(−1840, 3520)·(−1840, 3150)·(−3170, 3150)·(−3200, 3248)·(−3295, 3252) — 마지막 변이 입구를 가로지름)으로 너울을 없앰. 폭풍우(바람 140, 편집 모드 `ProjectPointOnWaterSurface`, 지점마다 12 m 간격 9점) 측정: 호수 가운데·육조거리 쪽 물가 −0.18~+0.16 m, 입구 −1.6~−0.5 m, 서쪽 먼바다 −11.2~−6.3 m.
- 나룻터 `나룻배_Ferry`(씬 루트, `FerryCrossing`): 선착장 2개 = `BaseGroup/swamp-bridge-4k-ready_2016` 복제(배율 2.78·2.78·3.75, 경사로 `Ramps` 꺼짐 — 원본과 같음, MeshCollider 7). **2026-10-05 물 가까이 내림**(사용자 지시 "나룻터가 상당히 높아서 낮추고 터레인도 경사줘서"): 판자 윗면 4.75 → **0.996**(물 위 약 1 m). `Dock_North_Yukjo` (−2745.6, 0.98, 3387.47) 회전 90 = 판자 z 3375.8~3396.5 / `Dock_South_Village` (−2744.36, 0.98, 3174.02) 회전 270 = 판자 z 3165.0~3185.7. 판자 양옆 난간 `TopRails`(x 약 −2747·−2743, 윗면 1.71~1.75) 사이를 걸음. 서는 자리 `Point_North` (−2745, 1.05, 3378.8) 북쪽 봄 / `Point_South` (−2745, 1.05, 3182.7) 남쪽 봄. 배 대는 자리 `Moor_North` (−2740.2, 0, 3376.8) / `Moor_South` (−2740.2, 0, 3184.7) = 선착장 동쪽 옆 물 쪽 끝(배 길이 약 6.1 m). 대는 자리 사이 곧은 길(x −2740.2 · z 3184.7~3376.8)과 카메라 길(x −2731.2) 밑 터레인은 모두 물속(−1.0 ~ −22.8 m, 측정).
- 경사로(터레인 `Island_Joseon_Large`, 낮추기만): 선착장 땅 쪽 끝에서 원래 땅까지 길(가운데 x −2745, 폭 6.4 m + 둘레 6 m 섞음) — 남쪽 z 3165(0.95) → 3128(원래 약 7.7, 약 10.4°), 북쪽 z 3396.5(0.95) → 3425(원래, 약 9.8°). 167점, 최대 3.25 m 낮춤. 남쪽 판자 끝 바로 앞이 원래 물밑 칸이라 터레인이 0.5 로 꺼져 판자와 0.5 m 턱(stepOffset 0.4 보다 큼)이 생겨, 판자 밑 헤이트맵 점(i 581~583 · j 308 → 1.0, j 309 → 0.93)만 고침(이 칸만 올림) — 측정: 땅 z 3165 0.969 → 판자 0.996. 남쪽 경사로는 모래(`Sand_aerial_sand`, 칸 2)로 칠함(z 3121~3168, 120칸 — 북쪽은 원래 육조거리 길 모래). 되돌리기: 높이 `Captures/terrain_backup/Island_Joseon_Large_i571_j285_w23_h138_before_FERRY.f32` → `SetHeights(571, 285, …)`, 칠 `…_alpha_i575_j287_w15_h27_L8_before_FERRY.f32`(float32 [z, x, 레이어]) → `SetAlphamaps(575, 287, …)`. Undo 등록, 에셋 저장. `한옥마을_사람` NavMesh 다시 구움(에셋 GUID 유지 — §7) → 마을 선착장 안쪽 → 남쪽 판자까지 길 이어짐(PathComplete).
- 걷기 시험(편집 모드, CharacterController r 0.32 · h 1.75 · stepOffset 0.4 · slopeLimit 50, 2.69 m/s): 남쪽 판자 ↔ 마을 땅(z 3120, 높이 7.8), 북쪽 판자 ↔ 육조거리 땅(z 3432, 7.3) 네 방향 모두 통과.
- 나룻배 `나룻배_Ferry/Ferryboat`(프리팹 `08.Environment/Ferry/Prefabs/Ferryboat.prefab` = `Meshes/Ferryboat.fbx` 의 프리팹 변형, FBX 재질 가져오기 None): 뱃머리 = 로컬 +X, 원점 = 배 바닥 가운데. LOD0/1/2 = 27,576 / 3,696 / 860 삼각형(LOD1·2 는 배 안쪽 구조를 뺀 것), 재질 `Materials/M_Ferryboat_LOD0~2`(HDRP/Lit, 매끄러움 0.22, 기본색 + 노멀 `Textures/T_Ferryboat_LOD#_BaseColor/_Normal.png`(노멀맵, 최대 2048)), LODGroup(이름 `_LOD#` 로 자동) 크기 6.12, 전환 화면 높이 0.26 / 0.0866 / 0.0035(약 20 / 61 m, 1.5 km 에서 끔), CrossFade. 자식 `Boatman`(사공 노인 프리팹, 로컬 (−2.58, 0.683, −0.30) 회전 (0, 90, 0) — 고물 쪽에 서서 뱃머리를 봄) · `PassengerSpot`(플레이어 발 자리, 로컬 (0.3, 0.108, 0) 회전 (0, 90, 0)). 충돌체 없음. 씬 = `Moor_South` 자리, 뱃머리 남쪽(선착장 땅 쪽), y −0.22.
- `FerryCrossing`(2026-10-05 다시 씀 — 예전: E 짧게 → 화면 어둡게 했다가 건너편으로 순간이동): 하차 중 서는 자리에서 8 m(수평)·높이 3 m 안이면 **E 3초** → 화면 0.35 s 어둡게 → 배를 출발 쪽 대는 자리에 건너편을 보게 놓고, 캐릭터(`OnFootRoot`)를 `PassengerSpot` 자식으로(OnFootController·CharacterController·PlayerOrbitCamera·`PlayerCharacter.onFootOnly`(칼싸움) 끔, Animator `Speed` 0) → 사공 `Rowing` true → 밝힘 → **40 s** 동안 건너편 대는 자리까지 곧게(출발·도착 5 s 가감속, 높이 = HDRP 물 높이 − 0.22, 물결 기울기 따라감, 도착 2.5 s 전 노 멈춤) → 어둡게 → 건너편 서는 자리에 내림(조작 복구, 카메라 yaw = 서는 자리 방향) → 0.25 s 뒤 밝힘. 타는 동안 카메라는 **배 동쪽(월드 +X, `sideCameraWorldSide`) 9 m · 물 위 2.6 m 에서 배 가운데를 봄 — 돌릴 수 없음**(`[DefaultExecutionOrder(1000)]` LateUpdate 로 덮어씀). 동쪽 고정 까닭: 선착장이 대는 자리 서쪽이라 서쪽 카메라는 출발·도착 때 난간에 가림(계산). 궁 쪽(A = 육조거리)으로 갈 때 출발하면서 `PalaceVista.Show()`, 마을 쪽으로 갈 때 `HideNow()`(§4 궁궐 원경 그림). 승·하차 자리(`BoardingSystem.CanAct`)에서는 양보, 일시정지 중엔 배도 멈춤. 안내·진행 바 OnGUI(임시). 시험용 `TestRide(toA)`, `MoveTo(서는 자리)`, `PlaceBoatAtDock(atA)`. **멀리서 끄기(2026-10-05, 사용자 지시 "마을 사람처럼")**: 타고 있지 않을 때 카메라에서 배까지 `hideDistance` 60 m 밖이면 배(사공 포함) 오브젝트를 끄고 55 m 안이면 켬, 타는 동안·출발 때는 늘 켬(사공 `SetBool` 은 켜져 있을 때만). 확인(Play): 하차 지점(256 m)에서 꺼짐 → 남쪽 서는 자리(6 m)에서 켜짐 → `TestRide(true)` 중 켜짐·`Rowing` true, 콘솔 오류·경고 0.
- 사공은 배 오른쪽(뱃머리를 볼 때)으로 저음 → 북쪽으로 갈 때는 노가 카메라 쪽, 남쪽으로 갈 때는 배 너머라 노가 거의 안 보임(캡처).
- **고침(2026-10-05, 사용자 지적)**: ① 배를 안 타고 건너간 쪽 나룻터에서 배가 없는데 E 로 갑자기 탐 → 하차 중 서는 자리 `summonDistance` 90 m(수평) 안에 오고 배가 반대쪽에 있으며 꺼져 있으면(카메라 60 m 밖이라 안 보임) 이쪽에 댐(`SummonBoat`). 확인: 북쪽 나룻터 약 50 m 로 옮기자 배가 북쪽에 와서 보임. ② 타면 포니테일이 뒤로 뻗어 뒤통수가 뾰족해 보임 → 타는 동안 `SpringBoneSystem.worldInfluence` 0(배 이동을 바람처럼 받지 않게), 태우고 내릴 때 `ResetTails()`. 확인: 타는 중 포니테일 끝이 아래에서 15°(캡처), 내린 뒤 0.35 로 돌아옴.
- 확인(Play, 2026-10-05, Game 뷰 캡처): 마을 섬 선착장에서 하차(`Disembark`) → `MoveTo(Point_South)` → `TestRide(true)` → 타는 동안 측면(사공 노 젓기·플레이어 서 있음, 궁은 화면 밖) → 북쪽 도착, 캐릭터 `Point_North` 에 서고 조작·칼싸움 컴포넌트 복구, 궁 켜짐(`Show` 0.20 s · 5 프레임 · 한 프레임 최대 4.3 ms)·그림 꺼짐 → `TestRide(false)` → 마을 쪽 도착, 궁 꺼짐(홀더 켜진 것 = 육조거리 하나)·그림 켜짐. 콘솔 오류·경고 0. 실제 E 키 3초 입력은 안 해 봄 — 사용자 확인 필요.

### 궁궐 원경 그림 (씬 루트 `궁궐원경_PalaceVista`, 2026-10-05)
- 사용자 지시: 마을에 있을 때는 광화문부터 뒤쪽 궁궐까지를 마을에서 보이는 모습의 이미지 한 장으로 대체하고, 육조거리로 가는 나룻배를 탈 때만 궁을 켬(궁은 옆·뒤에서는 볼 수 없어 늘 그림 — 사용자 답변).
- `PalaceVista`(07.Environment, `[DefaultExecutionOrder(-100)]`, static `Instance`): 플레이 시작(Awake)에 `GyeongBok` 직속 자식 중 켜진 것(`keepActive` = `육조거리_Yukjo` 만 남김 — 지금 17개)을 끄고 그림 판 `Card` 를 켬. `Show()` = 궁을 묶음(렌더러 150 개 이하가 되도록 쪼갠 하위 오브젝트 — 원래 꺼져 있던 것은 그대로, 지금 3,094 묶음)으로 나눠 프레임당 4 ms 안에서 켜고 다 켜면 그림을 끔(`lastShowSeconds`·`Frames`·`MaxFrameMs` 기록). `HideNow()` = 궁 홀더를 바로 끄고 그림을 켬. 편집 모드에서는 아무것도 안 바꿈 — 씬에는 궁 켜짐·`Card` 꺼짐으로 저장.
- **자동 켜고 끄기(2026-10-05, 사용자 지적 "나룻배를 안 타고 육조거리로 넘어가면 광화문이 그림인 채")**: 0.25 s 마다 카메라 위치가 구역(`zoneMin` (−3110, 3360) ~ `zoneMax` (−2380, 4510), XZ — 북쪽 나룻터·육조거리·궁 전체) 안이면 `Show()`, 구역에서 `hideMargin` 40 m 넘게 나가면 `HideNow()`. 나룻배를 타는 동안은 `FerryCrossing` 이 `autoSuspended` 를 켜 둠(호수 위 카메라는 구역 밖). 확인(Play): 마을 쪽에서 하차 → 광화문 앞(z 3830)으로 옮기자 궁 켜짐(0.44 s · 7 프레임 · 한 프레임 최대 4.2 ms)·그림 꺼짐(캡처), 나룻배로 마을 쪽 도착 → 궁 꺼짐·그림 켜짐. 구역 밖(바다 쪽 북·동·서)에서는 여전히 그림만 보임(그림은 남쪽에서만 보이는 판).
- 그림: 눈 (−2745, 9.4, 3128)(남쪽 선착장 경사로 위, 광화문 축)에서 궁 렌더러(3,735개, 육조거리 뺌)만 잠깐 레이어 30 으로 옮겨 HDRP AOV 로 기본색·월드 노멀을 찍음(2 배로 찍어 평균, AA 없음, 2.5 s) → `08.Environment/PalaceVista/T_PalaceVista_BaseColor.png`(sRGB, 알파 = 칸 안 궁 비율, BC7) · `T_PalaceVista_Normal.png`(선형, 월드 노멀 n·0.5+0.5), 4096×176. 판 `PalaceVista_Card.asset` = 그 눈에서 본 궁 시야(비대칭 절두체 l −0.404 · r 0.409 · b −0.0034 · t 0.0311)를 궁 맨 앞 깊이(751.1 m — 렌더러 경계 상자 기준)의 수직면에 펼친 사각형(폭 611 m · 높이 25.9 m, 가운데 (−2741.37, 19.80, 3879.13) = 광화문 앞면), 회전 0(그래서 물체 공간 노멀 = 월드 노멀). 재질 `M_PalaceVista`(= `IMP_Gyotaejeon_P0` 복사, HDRP/Lit, Normal Map Space = Object, 알파 잘라내기 0.5, 한쪽 면(뒤에서는 안 보임), 매끄러움 0.2) → 해 빛을 실시간으로 받음(궁 안 그림자·AO 는 없음). 그림자 안 드리움, 라이트 프로브 끔. 그 눈에서는 진짜 궁과 같은 자리·같은 모양.
- 마을 쪽 땅·물 높이에서 보이는 궁은 육조거리 행랑(진짜, 늘 켜짐) 사이 광화문·궁장 일부뿐(캡처). 호수 동·서쪽 물 높이에서는 지형에 가려 안 보임.
- 확인(편집 모드, 같은 시점 진짜 궁 ↔ 그림 렌더 비교, 1600×900): 눈 위치(FOV 14°) 화면 평균 차 1.97/255(30 넘게 다른 픽셀 0.65 %), 남쪽 선착장 끝(물 위 2.7 m)·육조거리 앞 호수(3 m)에서도 궁 자리 차이는 눈으로 안 보임(나머지 차이는 물결).
- 다시 찍기(궁을 바꾸면): `PalaceVistaTool.Capture(new Vector3(-2745, 9.4f, 3128), 4096, "육조거리_Yukjo", 1024)` → 텍스처·판 메시 덮어씀 → `Card` 위치 = EditorPrefs `PalaceVista_CardCenter`(판 크기가 바뀌면). 처음 한 번은 셰이더 컴파일로 MCP 시간 초과가 났으나 파일은 써짐.

### 기와 마을 (조선 큰섬 남쪽 띠, 씬 루트 `기와마을_GiwaVillage`, 2026-10-03 다시 배치)
- 사용자가 그린 영역(궁 남쪽 만 건너편 띠 땅, 대략 x −3196~−2152 · z 2969~3318 — 사용자 화면 캡처의 빨간 선을 씬 뷰 카메라 레이로 옮김)에 **기와집 8곳, 한 곳에 기와 건물 6~10채**(사용자 지시 — 두 채 묶음 40개는 단조로워 바꿈). 자식 `Estate_01_6bld` ~ `Estate_08_6bld`, 건물 63채(6·10·9·6·10·10·6·6).
- 회전은 0/90/180/270° 만(사용자 지시 "회전 되돌리고" — 집 전체를 비스듬히 돌리지 않음). 안채(ㄷ자 또는 ㄴ자)는 남쪽을 봄.
- 짜는 방법(`estate_plan.py`, 씨앗 11): 안채 앞 안마당에서 시작해 마당 변(서·동·남·북)에 마당을 보게 건물을 붙이고, 가끔 새 마당(사랑마당 등 — 북쪽에 주인 건물)이나 안채 뒤 건물(뒤뜰)을 더함. 마당 크기·건물 간격(마당 변 0.3~4.5 m, 새 마당 2~9 m, 뒤뜰 1.5~8 m)·변 안 위치는 무작위, 처마끼리 1.2 m 이상, 집 전체 95 m 안, 같은 구성 반복 없음. 한 집 크기 약 48~82 m.
- 자리 조건: 영역 안, 건물 밑 땅 높이 3 m 이상, 건물 밑 높이 차 1.0 m 이하, 집끼리 경계 상자 60 m 이상, 섬 선착장(−2944, 3037)에서 45 m 밖. 건물 높이 = 건물 밑 땅 최저점 − 0.1 m(건물마다 따로 — 뜬 곳 0 측정).
- 파일 `Captures/hanok_village/estate_plan.tsv`(집 번호·Estate·건물 종류·x·y·z·회전) + `estate_plan.py`·`estate_view.py`(평면 확인 그림). 바꾸기 전 배치(두 채 묶음, 사용자가 손본 33묶음 67채) = `backup_before_estates.tsv`(묶음·이름·프리팹·위치·회전).
- 프리팹 `02.Resources/00.Component/08.Environment/Hanok/Prefabs/Hanok_Giwa_Il_01`(1,544 삼각형) / `_Nieun_01`(1,968) / `_Digeut_01`(2,498): FBX(재질 가져오기 None) + 재질 `MI_Hanok_Giwa_Atlas`(HDRP/Lit, 아틀라스 2048×4096 기본색 + 노멀, 매끄러움 0.2) + MeshCollider + Static(Batching·Occluder·Occludee·ContributeGI·ReflectionProbe), ReceiveGI = LightProbes. 모델 로컬 = 앞마당(창·문 쪽) −z, 뒤(벽) +z, 원점 = 기단 바닥 가운데. ㄴ자는 뒤 날개(+z) + 옆 날개(+x 쪽이 −z 로 뻗음). LOD 없음.
- 확인: 임시 카메라 캡처(집 4곳)로 배치 확인, 경계 상자 겹침 0, 콘솔 오류·경고 0. 씬 저장은 사용자. 가까이 모습·실제 프레임은 사용자 확인 필요.
- **담장(2026-10-03, 사용자 지시 "둘레에 맞춰 담장, 문은 남쪽 하나")**: 집마다 `Estate_xx/Wall` 아래. 건물 처마 사각형을 3 m 넓혀 안채 기준 2.4 m 칸에 칠하고(이어지게 메움·구멍 메움) **줄마다·열마다 처음 칸부터 끝 칸까지 채우기를 바뀌지 않을 때까지 되풀이**(직교 볼록 — 건물 선을 따라 안으로 파인 홈·ㄱ자 들어감을 곧게 이음, 사용자 지시 2026-10-03; 남는 꺾임은 바깥으로 나가는 계단꼴뿐) + 대각선으로만 닿은 칸 채움, 그 바깥 테두리(직각 다각형)를 따라: 꼭짓점마다 ㄴ자 모서리(팔 1.2 m — 들어간 모서리도 같은 조각), 변에는 곧은 조각(2.4 m), 남쪽을 보는 변 중 안채 가운데 x 에 가깝고 안쪽 4.5 m 가 건물에 막히지 않은 곳에 대문 하나(곧은 조각 2칸 자리 4.8 m). 8집 합계 모서리 136 · 곧은 708 · 대문 8 · 마구리 29, 32,061 삼각형(측정). 건물과 경계 상자 겹침 0. (홈 메우기 전: 모서리 158 · 곧은 784 · 마구리 31.)
- 담장 높이: 조각마다 담 중심선 ±0.35 m 안 터레인 최저·최고(Unity `SampleHeight`, 0.25 m 간격)를 재어, 고리를 따라 이어진 조각을 묶음(묶음 안 최저~최고 0.6 m 이하)으로 같은 높이(묶음 최저 − 0.1 m). 묶음이 바뀌는 이음점에는 높은 쪽 끝에 마구리(`Hanok_Wall_End_01`, 낮은 쪽을 봄)를 붙여 단 사이 열린 단면을 막음. 높이가 묶음 안 최저보다 낮아 뜬 곳은 없음(계산상). 묻힘 최대 0.72 m(집 8의 조각 3개만 1.0 m 넘음, 최대 1.43 m — 조각 하나 안에서 땅이 1 m 넘게 기울어짐).
- 담장 조각 프리팹(`Hanok/Prefabs/`): `Hanok_Wall_Straight_01`(26 삼각형, 길이 Unity x) · `Hanok_Wall_Corner_01`(60, 회전 0 에서 팔이 −x·−z, 바깥이 +x+z) · `Hanok_Gate_Giwa_01`(654, 팔작 대문 + 양옆 담 토막, 폭 4.8 m, Unity x) · `Hanok_Wall_End_01`(9, x = 0 면, 면 방향 −x). 재질 2개 = 담 몸 `MI_Hanok_Wall_01`(막돌·회반죽 그림 1024², 2.4 m 한 장, 반복) + 지붕 `MI_Hanok_Giwa_Atlas`. 가져올 때 재질 이름으로 `AddRemap`(FBX 서브메시 순서가 조각마다 다름 — 곧은·모서리·마구리는 0 = 담 몸, 대문은 0 = 아틀라스). MeshCollider·Static·ReceiveGI LightProbes 는 집과 같음.
- 파일 `Captures/hanok_village/`: `wall_plan.py`(둘레·조각·대문, 인자 = 집 계획 tsv·여유 3.0) → `wall_plan.tsv` → Unity 에서 조각 발밑 높이를 재어 `wall_heights.tsv` → `wall_level.py`(높이 묶음·마구리) → `wall_plan_final.tsv`(집 번호·종류 W/C/G/E·x·y·z·회전) → 배치. `wall_view.py` = 평면 확인 그림.
- 확인: 캡처(위에서 2집, 대문 앞 눈높이 3곳, 마당 안, 모서리, 단 진 곳)로 이음·대문·단 확인, 콘솔 오류·경고 0. 씬 저장은 사용자.

### 초가 마을 (기와 마을 주변, 씬 루트 `초가마을_ChogaVillage`, 2026-10-03)
- 사용자가 씬 뷰 화면에 보라색으로 그린 영역 4곳(사용자 캡처를 같은 씬 뷰 카메라 렌더와 맞춰 보니 배율 1·위치만 다름 → 윤곽 픽셀마다 씬 뷰 카메라 레이를 `Island_Joseon_Large` 터레인 충돌체에 쏴 월드 다각형으로)에 **초가집 40곳, 한 곳에 초가 1~2채**(사용자 지시). 자식 `Home_01_2bld` … `Home_40_1bld`, 건물 60채(두 채 20곳 · 한 채 20곳, 일자 43 · ㄴ자 17), 105,798 삼각형(측정).
- 짜는 방법(`choga_plan.py`, 씨앗 6): 안채(일자 60 % / ㄴ자 40 %)는 앞 = 마당. 두 채면 일자 한 채를 마당 옆(마당을 보게, ㄱ자 배치) 또는 마당 건너편(안채를 마주봄, 마당 깊이 5~11 m)에. 집 전체는 0°(남향, 2/3) 또는 90/270° — 회전은 0/90/180/270 만. 이웃 집과 경계 상자 7~18 m(무작위), 기와집 담장 경계 상자에서 12 m, 선착장에서 45 m 밖, 건물 밑 땅 높이 3 m 이상·높이 차 1.0 m 이하. 두 채 집과 한 채 집이 반씩 되게 번갈아 놓음.
- **빈 곳 더 채움(2026-10-03, 사용자가 보라색으로 다시 그린 영역 4곳)**: 집 13곳(`Home_41` ~ `Home_53`, 건물 20채 · 두 채 7곳, 33,308 삼각형) 추가 — 같은 방법(씨앗 1)에 이웃 집과 4~10 m, 기와집 담장에서 8 m, 이미 놓인 초가 경계 상자(`choga_existing.tsv`)를 피함. 이번 캡처는 씬 뷰 렌더보다 1.01 배·위치 다름(맞춰서 옮김), 이동 기즈모 화살표가 보라색으로 잡혀 그 영역은 뺌. 합계 53곳 · 80채 · 139,106 삼각형(측정).
- 건물 높이 = 그 집 고른 땅 높이 − 0.1 m(아래 "땅 고르기"). 처음엔 건물 경계 상자 안 터레인 최저 − 0.1 m 였음. 초가끼리·기와 마을과 경계 상자 겹침 0.
- 파일 `Captures/hanok_village/`: `choga_px.tsv`(그린 영역 윤곽, 씬 뷰 픽셀) → Unity 레이 → `choga_world.tsv`(영역 번호·x·y·z) → `choga_plan.py`(인자: 영역 tsv · wall_plan_final.tsv · 씨앗 · [이미 놓인 초가 tsv · 출력 이름 · 집 번호 시작], 환경 변수 `M_LO`/`M_HI` 이웃 간격 · `GIWA_M` · `FAILS`) → `choga_plan.tsv`·`choga_plan2.tsv`(집 번호·영역·종류·x·y·z·회전, y 는 2 m 격자 높이라 Unity 에서 다시 잼).
- 프리팹 `Hanok/Prefabs/Hanok_Choga_Il_01`(1,612 삼각형) / `Hanok_Choga_Nieun_01`(2,146): FBX(재질 가져오기 None) + 재질 `MI_Hanok_Choga_Atlas`(HDRP/Lit, `MI_Hanok_Giwa_Atlas` 복사, 아틀라스 `Hanok_Choga_Atlas_BC/N` 2048×4096, 매끄러움 0.15) + MeshCollider + Static·ReceiveGI LightProbes(기와집과 같음). 모델 로컬 = 앞(마당) −z, 원점 = 기단 바닥 가운데, ㄴ자 날개 방향은 기와 ㄴ자와 같음. LOD 없음.
- **담장(2026-10-03, 사용자 지시 "똑같이")**: 집마다 `Home_xx/Wall` 아래. 기와 마을 담장과 같은 방법(2.4 m 칸 칠하기 → 직교 볼록 → 테두리에 모서리·곧은 조각)에 초가용으로 바꾼 것: 안채 앞으로 마당(5 m)을 칸 칠하기에 더하고, 대문은 안채 앞(마당) 쪽 변이 먼저(벌점 0, 옆 변 30, 뒤 변 60). **대문 안쪽 통로(폭 2.4 m · 깊이 7 m, 안채는 3.5 m 까지만)가 건물(처마 사각형 0.4 m 줄임)에 걸리면 막힘(벌점 +100)** — 사용자 지적 "문이 막혀 있다"(마당 건너편에 둔 아래채 뒷벽을 문이 마주봄)으로 바꿈: 집 9·23·27·33·41·45·53 은 앞 변이 막혀 옆 변(마당 옆)에 문. 집 사이가 좁아(4~18 m) 집마다 여러 안(여유 2.0/1.5/1.0/0.6/0.3 m · 마당 5/4/3/2.5/2/1.2 m · 칸 원점 0~2.1 m 밀기)을 차례로 해 보고, 이미 놓은 담·다른 집 건물·기와 마을(담·건물)과 0.3 m 안에 겹치지 않는 첫 안을 고름(이웃이 가까운 집부터; 그래도 겹친 두 집 52·53 은 두 집의 안 짝을 같이 골라 풂). 그래도 모든 변이 막힌 집은 **집 전체를 옮김**(사용자 지시 "건물째로 적절하게 떨어뜨려") — 가까운 자리부터(±12 m, 1 m 간격) 건물 밑 땅 3 m 이상·높이 차 1.0 m 이하, 다른 건물·기와 마을과 1.5 m, 담이 겹치지 않고 문이 막히지 않는 첫 자리: 집 39 를 동쪽 2 m(`choga_moved.tsv`). 53집 합계 모서리 308 · 곧은 1,678 · 사립문 53 · 마구리 0, 134,526 삼각형(조각 삼각형 × 개수). 담끼리·다른 집 건물·기와 마을 겹침 0, 막힌 문 0(통로 검사 `gate_check2.py`).
- **땅 고르기(2026-10-03, 사용자 지시 "터레인이 낮아지며 담장도 내려가는 곳은 터레인 높이를 맞추면서라도", 기와집은 그대로)**: 초가집마다 담 둘레(모서리 조각 꼭짓점 다각형) 안 + 둘레에서 3.9 m(담 바깥 면 0.4 m + 헤이트맵 칸 대각선 3.45 m — 담 밑 보간이 정확히 한 높이가 되게)까지의 헤이트맵 점을 한 높이 T 로, 그 밖은 max(6 m, 2.5 × 그 묶음 최대 높이 차) 에 걸쳐 원래 땅으로 섞음(smoothstep, 여러 묶음 가중 평균). T = 담 안 원래 땅 가운데값. 고른 땅이 서로 닿는 집은 같은 T(24 묶음). 기와 마을 담 경계 상자 + 3 m 안은 손대지 않음. 건물·담 = T − 0.1 m, 단이 없어 마구리 없음. 측정: 담 밑 뜬 곳 0(담 바닥이 땅보다 최소 7 cm 아래), 묻힘 최대 0.105 m. 바뀐 헤이트맵 점 5,080, 최대 돋움 4.22 m(집 32 — 바다 쪽 비탈) · 최대 깎음 2.28 m. 비탈이 30° 넘게 된 곳: 북쪽 바닷가 두 곳(x −2662 · z 3188 최대 53°, 원래 44° / x −2326 · z 3272 최대 46°, 원래 30°) — 집 담 둘레를 바닷가 비탈 위에 돋워서. 되돌리기: `Captures/terrain_backup/Island_Joseon_Large_i390_j220_w441_h161_before_CHOGA.f32` → `SetHeights(390, 220, …)`. 에셋 저장, Undo 등록. 땅이 바뀌었으니 `WaterShoreMask` 다시 굽기 필요(확인 필요 — 바닷가 돋운 곳).
- 초가 담 조각 프리팹(`Hanok/Prefabs/`): `Hanok_ChogaWall_Straight_01`(28 삼각형, 길이 2.4 m Unity x, 막돌·흙 몸 1.30 m + 짚 덮개 1.57 m, 두께 0.8 m 덮개 포함) · `Hanok_ChogaWall_Corner_01`(224, 둥근 모서리 반지름 0.6 m, 팔 −x·−z, 바깥 +x+z — 기와 모서리와 같은 방향) · `Hanok_Gate_Choga_01`(350, 사립문 + 양옆 담 토막, 폭 4.8 m, Unity x) · `Hanok_ChogaWall_End_01`(67, x = 0 면, 둥근 덮개 끝이 −x 로 0.15 m — 지금은 안 씀). 재질 = 담 몸 `MI_Hanok_ChogaWall_01`(흙·막돌 그림 1024², `MI_Hanok_Wall_01` 복사) + 덮개·사립문 `MI_Hanok_Choga_Atlas`, 가져올 때 재질 이름으로 `AddRemap`(`MI_Hanok_Choga_01` → `MI_Hanok_Choga_Atlas`, `MI_Hanok_ChogaWall_01` → 같은 이름). MeshCollider·Static·ReceiveGI LightProbes 는 집과 같음.
- 담장 파일(`Captures/hanok_village/`): Unity 에서 지금 건물 자리를 `choga_current.tsv` 로 받아(사용자가 옮긴 집이 있어 계획 파일과 최대 92 m 다름 — 계획 파일 대신 이것을 씀) → `choga_wall_plan.py`(인자: choga_current.tsv · 여유 2.0 · 1 · 출력 · 마당 5.0; 기와 `wall_plan_final.tsv`·`estate_plan_11.tsv` 를 피함; 옮긴 집은 `<출력>_moved.tsv`) → `choga_wall_plan.tsv` → Unity 에서 헤이트맵 칸 i 390~830 · j 220~380 을 `choga_hm.f32` 로 → `choga_flatten.py`(헤이트맵 · 담 계획 · 기와 담 · 출력 접두) → `cflat_hm_new.f32`·`cflat_level.tsv`(집 번호·T) → 담 y = T − 0.1, 마구리 뺌 → `choga_wall_final.tsv`. 겹침 검사 `choga_wall_check.py`. (옛 `choga_wall_level.py`·`choga_wall_heights.tsv` 높이 묶음 방식은 안 씀.)
- 확인: 캡처(문이 막혔던 집 9·23·39, 비탈 집 22·32·46 — 대문 쪽 위에서), 콘솔 오류·경고 0. 씬 저장은 사용자. 가까이 모습·실제 프레임은 사용자 확인 필요.

### 마을 사람 (씬 루트 `마을사람_Villagers`, 2026-10-04 — 사용자 지시 "각 마을 돌아다니는 사람, 섬에 가까이 가면 생기게, 육조거리 제외")
- 자식 둘(각각 `NavMeshSurface` + `VillagePopulation`): `한옥마을_사람`(기와·초가 마을, 중심 (−2695, 20, 3135), 사람 구역 990 × 290 m = x −3190~−2200 · z 2990~3280, 시민 `HanbokMale`·`HanbokFemale`) / `일본마을_사람`(일본 마을, 중심 (1572, 25, −1060), 285 × 370 m = x 1430~1715 · z −1245~−875 — 성 고원(x 1762~) 밖, 신사 고원은 안, `KimonoMale`·`KimonoFemale`). 육조거리(호수 북쪽 z 3400~)는 뺌 — 관복 입은 양반을 따로 넣을 계획(사용자).
- `VillagePopulation`: 카메라(`Camera.main`)가 사람 구역 상자에서 **450 m 안**이면 20명(사용자 선택 "마을당 약 20명")을 한 프레임에 2명씩 만들고, **600 m 밖**이면 모두 끔(풀에 남겨 두고 다시 오면 새 자리에서 켬). 자리 = 구역 안 무작위 점 → 땅(레이를 위에서 쏴 터레인) + 0.3 m → `NavMesh.SamplePosition`(3 m) → 높이 1.2 m 위 · 지붕 아님(2.5 m 안) · `anchor`(한옥 = 선착장 안쪽 (−2944, 5.43, 3055), 일본 = 상인 거리 (1480, 5.06, −925))까지 길이 이어지는 곳만(담 안 마당·지붕 위 NavMesh 조각을 거름). 사람 = 시민 프리팹 + 컨트롤러 `Citizen/Animations/Villager.controller`(블렌드 float `Speed` 0 = Quaternius `Rig|Idle_Loop`, 1 = `Rig|Walk_Loop`) + `NavMeshAgent`(반지름 0.3, 높이 1.7, 낮은 품질 회피) + `Villager`.
- **한 명씩 끄기(2026-10-05, 사용자 지시 60 m)**: `VillagePopulation.UpdateHidden` 이 매 프레임 쓰는 중인 사람(`Villager.inUse`)마다 `Camera.main` 거리를 재 `hideDistance` 60 m 밖이면 오브젝트를 끄고 55 m(`hideHysteresis` 5) 안이면 다시 켬(`hiddenCount`). 풀의 빈자리 판정은 `activeSelf` 대신 `inUse`(마을에서 멀어져 `Despawn` 하면 false). 꺼진 동안은 제자리에 멈춰 있고, 다시 켜지면 그 자리에서 NavMeshAgent 로 이어 걸음. 확인(Play): 마을 섬 선착장에서 하차 → 20명 모두 60 m 밖이라 꺼짐, 한 명 옆 5 m 로 옮기자 47 m 안 2명 켜져 걸음(속도 0.94~1.06 m/s, `Speed` 1), 거리 규칙 어긋난 사람 0, 콘솔 오류·경고 0.
- **죽이기(2026-10-05, 사용자 지시 "마을 NPC 죽일 수 있게")**: 플레이어 칼 공격의 판정 순간(`SwordCombatant.OnStrike` — 새로 넣은 static 이벤트, `ResolveHit` 맨 앞에서 부름, 표적이 아니어도)마다 `Villager.HandleStrike` 가 켜진 마을 사람 중 공격 거리(range + `hitRadius` 0.35 m)·각도(arc) 안·높이 차 2 m 안을 한 번에 쓰러뜨림(`Kill`): NavMeshAgent 끔, 공격자를 보게 돌린 뒤 `Villager.controller` 새 상태 `Death`(Quaternius `Rig|Death01`, 2.4 s, 루프 없음), 그동안 Animator `AlwaysAnimate`. 시체는 그 자리에 남고(60 m 밖에서 꺼졌다 켜져도 누운 자세 — 다시 켜진 뒤 첫 Update 에서 `Death` 끝 자세), 마을에서 600 m 밖으로 멀어져 풀로 돌아간 뒤 다시 쓰일 때 `Begin` 에서 되살아남. 플레이어 칼만(`PlayerCombatController.combatant`) — 적 칼잡이·포탄·화살로는 안 죽음. 칼을 자동으로 뽑지는 않음(자동 뽑기는 SwordCombatant 적 6 m 안일 때만 — 좌클릭하면 뽑음). 확인(Play): 큰섬 선착장에서 하차, 마을 사람 뒤 1.0 m 에서 B 콤보 1타(range 1.3, arc 60) → 쓰러짐(골반 높이 0.79 → 0.12 m), 끄고 켠 뒤에도 누움, `Begin` → 되살아나 걷기 트리, 1.65 m 에서는 안 맞음(사거리 밖). 콘솔 오류·경고 0.
- `Villager`: 사람마다 걷는 속도 0.8~1.15 m/s, 도착하면 2~10 s 서 있다가 다음 자리(지금 자리에서 20~90 m, 없으면 구역 아무 데나), 4 s 동안 거의 못 움직이면 다른 곳으로. 걷기 클립 속도(Quaternius `Walk_Loop` 를 시민 모델로 잰 발 속도 약 0.8 m/s)에 맞춰 Animator.speed = 실제 속도 / 0.8(0.7~1.6). 충돌체 없음(플레이어와 부딪히지 않고 지나감).
- NavMesh(`08.Environment/NavMesh/`): 두 `NavMeshSurface` 모두 에이전트 Humanoid(반지름 0.5, 높이 2, 경사 45°, 턱 0.75), 상자 안 물리 충돌체로 굽기(`CollectObjects.Volume`, 복셀 0.25 m — 한옥 1030 × 80 × 330 m 0.9 s · 일본 330 × 90 × 410 m 0.5 s), 자식 `물밑_NotWalkable`(NavMeshModifierVolume, 영역 Not Walkable, y −30.8~0.8)로 물 밑을 뺌(측정: NavMesh 최저 0.9 m). 땅 격자 검사(측정): 한옥 30 m 격자 땅 233점 모두 NavMesh 위, 그중 185점이 선착장까지 이어짐 / 일본 20 m 격자 272점 중 270점 NavMesh, 250점 이어짐, 도리이 앞까지 길 이어짐. **마을·지형을 바꾸면 NavMeshSurface 를 다시 굽기**(에셋 덮어쓰기).
- 애니메이션 가져오기: Quaternius `AnimationLibrary_Unity_Standard.fbx` 의 `Rig|Idle_Loop`·`Idle_Talking_Loop`·`Walk_Loop`·`Walk_Formal_Loop`(루프 켬)·`Death01`(루프 끔)만 루트 회전·높이·XZ 를 포즈에 굽게 바꿈(나머지 클립은 기본값 그대로).
- 확인(Play, 2026-10-04): 카메라가 한옥 마을 구역에서 약 430 m 일 때(플레이어 배 시작 자리) 20명 생김(19명 걷는 중, 평균 0.96 m/s), 배를 일본 마을 앞바다(1300, −760)로 옮기면 한옥 마을 0명·일본 마을 20명, 캡처로 발이 땅에 닿아 걷는 것 확인, 콘솔 오류·경고 0. 오래 돌아다니게 둔 모습·성능은 안 봄.

### 기와 재질 (2026-10-04)
- 사용자 지시 "모든 기와 메탈릭값 빼세요": 씬에서 쓰는 기와 재질은 원래 모두 금속성 0 이었음(측정 — 금속성이 있던 것은 배 `MI_Roof`(통신사선, 마스크 R 평균 0)뿐). 반짝임은 매끄러움 때문이라 이름에 rooftile·giwa·kiwa·kawara·top01a/02a·yeondan·roofenv·roofflat·roofplate·_roof 가 든 재질(장식·벽·나무·바닥·서까래·돌·쇠 등 제외) 188개 중 매끄러움 0.2 넘는 것을 0.15 로, 금속성 0, `MI_Roof` 는 `_MetallicRemapMax` 0. 바꾸기 전 값 = `Captures/tiles/tile_material_before.tsv`(재질·경로·매끄러움·금속성). 흥례문 먼 지붕판 `MAT_RoofPlate_HNMFar` 도 0.54 → 0.15(기와 `MI_HNM_RoofTile` 과 같이). 모습은 사용자 확인 필요.

### 병사 (씬 루트 `병사_Soldiers`, 2026-10-06 — 사용자 지시 "빨간 관군을 만들어 싸울 수 있게, 일본섬에 일본 장수·병사(조총 대신 칼)를 배치하고 싸우게")
- 사용자 선택: 관군 = **육조거리·광화문 경비**, 무기 **환도**, 일본군 = **성 + 마을 거리**, **일본 장수는 성 안에만**, 공격 조건 = **먼저 공격할 때만**(현상금 게이지 무시 — 그 병사를 치거나 근처에서 마을 사람을 쓰러뜨리면 주변 병사가 덤빔).
- 무리(`SoldierGroup`) 3개: `관군_육조거리`(12명 — 광화문 앞 4(z 3842~3846, 남쪽을 봄), 관아 문 6(`YJR_GATE` 앞 길 쪽 x −2760 / −2730), 길 순찰 2(반지름 70 m)) · `일본군_성`(7명 — 장수 `일본장수`(1795, 94.7, −985, 서쪽 = 성문 쪽을 봄) + 호위 2, 성문 밖 2(x 1758), 안마당 순찰 2(반지름 18 m)) · `일본군_마을`(6명 순찰, 반지름 45 m, 상인 거리·큰길 — 일본 마을 NavMesh `일본마을_사람` 을 씀). 자리는 위에서 쏜 레이로 땅 높이.
- NavMesh(새로 구움, `08.Environment/NavMesh/`): `관군_육조거리/NavMesh_육조거리`(중심 (−2745, 20, 3645), 120 × 40 × 450 m — z 3420~3870, 광화문 문 앞까지) → `NavMesh_Yukjo.asset`, `일본군_성/NavMesh_일본성`(중심 (1780, 110, −975), 180 × 70 × 130 m — 성문 밖 오름길 끝·안마당·천수 층) → `NavMesh_JapanCastle.asset`. 설정은 마을 것과 같음(Humanoid, Volume, 물리 충돌체, 복셀 0.25) + **층 마스크에서 Ignore Raycast(2) 뺌**(병사 캡슐이 구워지지 않게). 확인: 길 순찰 ↔ 광화문 앞, 성문 ↔ 안마당 경로 PathComplete. 지형·궁·성을 바꾸면 다시 굽기.
- 프리팹(`06.Character/Soldiers/`, `Editor/SoldierPrefabTool.BuildAll(해신 플레이어)`): 뿌리(층 Ignore Raycast, NavMeshAgent r 0.35·h 1.8, CapsuleCollider r 0.3·h 1.8, `SoldierAI`) → 자식 `Model` = FBX 인스턴스(관군 = `JoseonNavy.fbx` + 빨간 재질, 일본 병사 = `JPSoldier.fbx`(조총 없는 FBX 그대로), 장수 = `Shogun.fbx`), LODGroup 은 다른 NPC 와 같은 값, Animator = **해신 컨트롤러 `Haesin_TestLocomotion` 을 그대로**(Fab 휴머노이드 클립이라 다른 아바타에도 됨), `CombatStats`(체력·체간: 관군 80/100, 일본 병사 70/100, 장수 180/160), `SwordCombatant`(메인 씬 해신 값 복사 — 공격 데이터 같음, team 1, motionRoot = 뿌리), `WeaponHolder`(칼 넣고 시작), `HwandoGripIK`, `SwordAudio`(해신 값 복사).
  - 칼: 모두 **환도 모델(`Hwando_Sheathed`)** — 일본도 모델은 없음. 손 소켓·골반 소켓은 두 몸을 머슬 0 자세로 두고 해신 소켓이 손·골반에 대해 놓인 세계 관계를 NPC 뼈 로컬로 옮겨 계산(뼈 축이 달라도 맞음), 골반 쪽은 골반 폭 비율로 늘림(세 모델 모두 해신과 거의 같아 약 1 배). 왼쪽 허리(확인: 캐릭터 기준 왼다리 쪽).
  - **일본 장수 모델에는 칼(칼집)이 이미 메시에 붙어 있음**(캡처) → 환도 칼집 렌더러를 끄고 `WeaponHolder.hideWhenSheathed` 에 환도 검 렌더러 — 뽑았을 때만 보임(새 필드, 2026-10-06).
- `SoldierAI`: 평소(calm) — 경비는 제자리·정한 방향, 순찰은 제자리 둘레 반지름 안 NavMesh 점(경로 완전한 곳만)을 1.3~1.5 m/s 로 걷고 3~9 s 섬(걷기 클립 2.69 m/s 를 Animator.speed 로 늦춤, 뽑고 넣는 동안은 1 배). `SwordCombatant.calm` = true(새 필드) → 플레이어 자동 칼 뽑기·전투 판정에서 빠짐, 플레이어 표적은 덤비는 적을 먼저 고름(`PlayerCombatController.FindTarget`, 0.5 s 마다 다시 고름).
  - 덤비기: 이 병사가 공격을 받으면(판정 결과 피격·막기·회피·튕겨내기·위험 공격 피격·체간 붕괴) `SoldierGroup.Alert`(30 m 안 같은 무리), 동료가 쓰러지면 그 자리에서 Alert, 플레이어가 마을 사람을 쓰러뜨리면(`Villager.OnKilledByPlayer` — 새 static 이벤트) 40 m 안. 덤비면 칼을 뽑고(WeaponHolder 동작) 7 m 밖은 3.3 m/s, 안은 2.69 m/s 로 다가가 EnemySwordAI 와 같은 패턴(공격 간격 1.4~2.8 s + 0.8, 찌르기·베어 넘기기 각 12 %, 강베기 12 %, 막기 30 %·튕겨내기 10 %, 위험 공격 40 % 회피). **한 번에 2명만 붙어 싸움**(플레이어에 가장 가까운 덤비는 병사 2명), 나머지는 3.6 m 에서 둘러섬.
  - **보자마자 덤비기 `aggroOnSightRange`**(2026-10-07, 기본 0 = 끔): 0 보다 크면 평소 상태에서 15 프레임마다 걷는 플레이어(OnFootController 켜짐)가 그 거리 안이면 무리에 알리고 덤빔. 왜구 두목 10 m·선착장 경비 14 m 에만 씀(관군·일본군은 0).
  - **퀘스트 숨김 `questHidden`·되살리지 않음 `questNoRespawn`**(2026-10-09): questHidden 이면 무리(SoldierGroup)가 거리로 켜지 않고 알림에서도 빠짐(QuestTag.SetShown 이 정함), questNoRespawn 이면 쓰러져도 무리가 되살리지 않음(두목·영주·궁궐 호위).
  - 포기: 플레이어가 죽거나, 배에 타거나(OnFootController 꺼짐), 제자리에서 60 m 밖 → 칼 넣고 제자리로 돌아가 체력을 채우고 calm.
  - 이동: NavMeshAgent `updatePosition`·`updateRotation` 끔 → LateUpdate 에서 걸을 땐 에이전트 다음 위치를 뿌리에, 루트 모션(공격·피격) 뒤에는 거꾸로 `agent.nextPosition = 뿌리`(NavMesh 위로 잡힘), 높이는 아래로 쏜 레이(Ignore Raycast 층 제외, CharacterController 건너뜀). 쓰러지면 캡슐 끔·Animator AlwaysAnimate.
- `SoldierGroup`: 0.25 s 마다 카메라에서 160 m(+10 히스테리시스) 밖 병사는 오브젝트를 끔(덤비는 병사는 켬, Animator `keepAnimatorStateOnDisable` — 쓰러진 자세 유지), 카메라가 무리의 모든 제자리에서 300 m 밖이면 쓰러진·돌아가는 병사를 제자리에 되살림.
- 플레이어 쪽(메인 씬 `Haesin_Player`): `PlayerLandDeath` — 걷다 쓰러지면 4 s 뒤 되살려 배에 태움(`BoardingSystem.Board`), 덤비는 병사가 25 m 안에 없고 마지막 피해 뒤 8 s 지나면 체력 초당 5 회복(다른 회복 수단이 없어서). `LandCombatHUD`(`병사_Soldiers` 에 붙음, OnGUI 임시): 걷는 중 전투·다침·체간이 있을 때 왼쪽 아래 체력·체간, 위 가운데 덤비는 표적 이름·체력·체간, 위험 공격 경고, 판정 글자, 쓰러지면 "쓰러졌습니다 — 배에서 깨어납니다".
- 확인(Play, 2026-10-06, 기능은 메서드 호출로): 순찰 1.30 m/s·Speed 2.69·anim 0.48 배, 관아 문 경비를 치자 그 경비만 덤빔(30 m 밖 경비는 그대로) → 칼 뽑고 공격, 플레이어 체력 100 → 0 → 4 s 뒤 배에서 되살아남(체력 100), 경비는 칼 넣고 제자리(0.5 m 안)·체력 80; 광화문 앞 4명 한꺼번에 덤빔·2명만 붙고 2명은 3.9 m 에서 대기(캡처); 쓰러진 경비 캡슐 꺼짐, 일본섬으로 간 뒤 300 m 밖 되살아남(체력 80); 일본 장수를 치자 장수·호위 2 덤빔(성문·안마당 병사는 30 m 밖이라 그대로), 캡처로 장수·일본 병사가 환도를 두 손으로 듦; 일본 마을에서 마을 사람을 쓰러뜨리자 40 m 안 순찰 병사 1명 덤빔; HUD Game 뷰 캡처(표적 "일본 병사 70/70", 왼쪽 아래 체력); 콘솔 오류·경고 0. **실제 키보드·마우스로 싸운 것은 아님**, 병사끼리 붙으면 서로 겹쳐 보이는 장면이 있음(캡처).

### 왜구섬 (씬 `Islands/Island_Wokou_Den` + 루트 `왜구소굴_WokouDen`, 2026-10-07 — 사용자 선택 "일본섬 남서쪽 무인도", "선착장 + 기존 건물로 소굴", "왜구 장수는 일본 장수 갑주를 검은색으로")
- 터레인: 계획 큐브 `IslandPlan/Plan_Wokou_400` (149, −2.5, −2120) → `IslandTerrainGenerator.GenerateOne("Wokou_Den")`(Kind.Wokou, 계획 400 m, 시드 67, 선착장 방향 30°). 터레인 위치 (−251, −60, −2520), 크기 800, 최고 67.8 m, 땅 약 0.12 km². 남서쪽 산(약 58 m)·남동 곶·북동쪽 만 안 소굴 평지(약 6 m, 2 m 단). TerrainData `08.Environment/Islands/Island_Wokou_Den.asset`.
- 선착장 `Dock` (264.35, 0, −2053.4) rotY 330(생성기가 만듦 — 다른 섬과 같은 방식), 선착장 배 = anteck_wokou + seki_wokou(앞바다를 돎). 영역(`FactionTerritory`) Wokou inner 330 / outer 730(왜구는 outer = inner + 400).
- 소굴 건물(`왜구소굴_WokouDen/Buildings`, 일본 마을 프리팹 재사용): 두목집 `JP_Noble_01` (197.7, 6.4, −2109) rot 330, 숙소_1 Minka_04, 창고_1 Minka_01, 어막 Minka_03, 숙소_2 Minka_05, 창고_2 Minka_02. 건물 밑 터레인을 고름(8077 칸).
- 병사(`왜구소굴_WokouDen/왜구_소굴`, SoldierGroup factionName "왜구"): 왜구두목(`Wokou_Boss`, aggro 10 m) 1 · 호위 2 · 순찰 3(반경 22/22/18) · 선착장 경비 1(aggro 14 m). 모두 NavMesh 경로 완전(PathComplete) 확인. 2026-10-09: 선착장 경비 y 1.8 → 2.64(다른 세션이 선착장 땅을 올린 뒤 땅속에 묻혀 있었음)·`NavMesh_WokouDen` 다시 구움(GUID 유지), 왜구두목에 QuestTag `wokou_boss`(2-6 제압 대상).
- NavMesh `NavMesh_왜구섬`: 중심 (205, 0, −2090), center y 20, 크기 170 × 60 × 170, 물밑 `물밑_NotWalkable`(center y −15, 170 × 31.6) → `08.Environment/NavMesh/NavMesh_WokouDen.asset`.
- 두목 모델(`Editor/SoldierPrefabTool.BuildWokou`): `Wokou_Boss` = 일본 장군 Shogun.fbx + 검은 재질 `Soldiers/WokouBoss/M_WokouBoss_LOD0~2`(원래 텍스처에서 붉은 칠(색상 ±14°→28°, 채도 0.30~0.45)만 검게 → `T_WokouBoss_LOD{n}_BaseColor.jpg`, 매끄러움 0.45), 체력 220·체간 180, 칼은 모델에 붙은 것. `Wokou_Sword` = **왜구 병사 모델 `WokouSoldier`**(2026-10-09 `SoldierPrefabTool.BuildQuestSoldiers` 로 다시 만듦 — 같은 경로라 씬 배치 그대로, 70/100). 두목은 그대로 검은 갑주 장군.
- 왜구 배: 원본 `anteck_wokou` (75, 0, 137) · `seki_wokou` (30, 0, 91)(anteck·seki 복제, faction Wokou, 재질 `NewShip/Ship/fbx_file_render/WokouMaterial/Wokou_*` — 돛 (0.26, 0.23, 0.22), 깃발 (0.10, 0.09, 0.09)).
- 확인(Play, 2026-10-07, 순간 이동·호출로): 선착장 경비가 9 m 에서 덤비고 30 m 안 순찰병에게 알림, 선착장 배가 원을 돌며 순찰, 두목 검은 갑주 ↔ 원래 장군 비교 캡처, 소굴 전경 캡처. 콘솔 오류·경고 0.

### 해월도 (조선 작은섬 A `Joseon_Small_A`, 씬 루트 `해월도_Haewoldo`, 2026-10-07 — 1막 무대)
- 이름은 스토리 스프레드시트의 해월도를 작은섬 A 에 붙임(Claude 가 정함). 선착장 = 섬 LandingPoint (−1542.2, 6.5, −27.8).
- `관아_Gwana`: `Hanok_Giwa_Digeut_01` (−1632.3, 4.9, 49.4) rot 310.6.
- `NPC_숙부`(npcId `suk_bu`): **퀘스트 NPC 모델 `YiDam_Dopo`**(2026-10-09 바꿈, 예전 시민 HanbokMale) + `Villager.controller`(Idle), (−1562.3, 1.8, −4.0) yaw 130.6, idle 대사 2줄. `NPC_현감`(npcId `hyeongam`, 현감 조명구): **`JoMyeongGu`**(2026-10-09 바꿈), (−1619.4, 5.0, 38.4), idle 대사 2줄.
- 마을·사람 없음(관아 한 채 + NPC 둘).


### 퀘스트 배치 — 2막~4막 (`Editor/QuestSceneSetup.Build()`, 2026-10-09)
- 다시 부르면 같은 이름의 오브젝트를 지우고 표대로 다시 만듦(손으로 옮긴 자리는 사라짐). NPC = 뿌리(QuestNPC·QuestTag) + 자식 `Model`(퀘스트 NPC 모델 프리팹, Animator = `Citizen/Animations/Villager.controller`, 루트 모션 끔). 높이 = 대략 높이 + 6 m 에서 아래로 레이.
- 마커(`QuestSystem/Markers`, 1막 둘 + 16개): 구로세_상인거리 (1470, −905) r25 · 구로세_항구 (1250, −700) r160 · 구로세_호위출발 (1300, −760) r30 · 구로세_북서포구 (700, −300) r60 · 구로세_침몰선 (1180, −640) r60 · 구로세_성문(표시용) · 왜구섬_앞바다 (400, −1950) r220 · 왜구섬_선착장 (269.6, −2050.4) r60 · 망운도_선착장 (−962, 1402.8) r120 · 망운도_약초 (−960, 48, 1640) r14 · 중섬_선착장 (1281.6, 2452.2) r80 · 한양_선착장 (−2944.1, 3032.7) r90 · 광화문_앞 (−2745, 10.3, 3815) r14 · 근정전_마당 (−2745, 11, 4082) 북향 · 경회루_동쪽 (−2830, 12.3, 4270) 서향 · 강녕전_마당 (−2745, 11.8, 4244) 북향.
- `구로세지마_Kurose`: NPC_하야시(HayashiSoemon, hayashi) (1470, 4.9, −905) · NPC_구로세(KuroseMasatoki, kurose, 태그 kurose_castle) 성 안마당 (1776, 94.7, −967) 서향 · NPC_구로세_항구(KuroseMasatoki_Armor, kurose_harbor, 숨김) (1440, 5.1, −880) · NPC_전령(GwanaClerk, jeonryeong, 숨김) (1425, −868) · NPC_숙부댁_하인(Citizen/HanbokMale, sukbu_servant, 숨김) (1431, −873) · NPC_배신한_가신(JpRetainer, 태그 retainer, 숨김, QuestWalker + NavMeshAgent, 길 `미행길_RetainerPath` 4점 — NavMesh `일본마을_사람` 위, 계산 경로 약 289 m) · NPC_정체모를_조선인(MaskedMan, masked_man, 숨김) (1466, −873) · `불_상인거리` = 선착장에 가까운 MerchantDistrict 집 4채 위 3.5 m 에 `CamelotVFX_ Fire&Smoke/Prefabs/Fire_v2`(fire_kurose, 숨김).
- `일본군_성/영주_구로세_갑주`(Kurose_Armor_Sword, 태그 kurose_combat, 숨김, 2-8A 제압 대상 — 성 무리라 공격하면 성 병사도 덤빔). `왜구소굴_WokouDen/NPC_구로세_소굴`(KuroseMasatoki_Armor, kurose_den, 숨김) (212, 6.2, −2090).
- `망운도_Mangundo`(조선 작은섬 B `Joseon_Small_B`): 관아_Gwana(`Hanok_Giwa_Digeut_01`) (−1060, 9.8, 1465) · 한윤겸_집(`Hanok_Choga_Il_01`) (−875, 10.7, 1482) — 건물 발치 9점 터레인 중 가장 낮은 높이에 둠(땅은 고르지 않음) · NPC_관아_관리(GwanaClerk, clerk_mangun) · NPC_한윤겸(HanYungyeom, han_yungyeom). 이름 망운도는 스토리 표 지명을 작은섬 B 에 붙임(Claude).
- `한양_Hanyang`: NPC_권시헌_육조(GwonSiheon, gwon_hanyang) 육조거리 (−2752, 10.3, 3470) · 근정전 마당(태그 palace_day, 숨김 — 4-2 에만): NPC_내관(Eunuch, naegwan)·NPC_홍대비(HongDaebi, daebi)·NPC_궁녀_1·2(CourtLady) · 경회루(태그 gyeonghoeru, 숨김 — 4-3): NPC_권시헌_경회루(gwon_palace)·NPC_반란파_1·2(RebelSoldier) · NPC_현왕(YiDam_King, king, 숨김, talkRange 6) 강녕전 월대 (−2745, 13.0, 4249.5) 북향(등 돌림) · `불_대비전` = 교태전 자리 (−2720, 4302) 둘레 Fire_v2 5개(fire_daebi, 숨김).
- `궁궐호위_PalaceGuards`(SoldierGroup "궁궐 호위"): 궁궐호위_1~4(PalaceGuard_Sword, palace_guard, 숨김, questNoRespawn) 근정전 마당 + `NavMesh_근정전`(NavMeshSurface Volume, 중심 (−2745, 15, 4100), 100 × 20 × 56, 육조거리 표면 설정 복사 → `NavMesh_Geunjeongjeon.asset`). 네 명 모두 마당 → 근정전_마당 경로 완전.
- 태그만 붙인 기존 오브젝트: 왜구두목(`wokou_boss`), `병사_Soldiers/관군_육조거리`(`yukjo_guards` — 4-5A SoldierAggro).
- 궁 안 이동은 걸어서가 아니라 TeleportPlayer(광화문 앞 → 근정전 마당, → 경회루, → 근정전 마당, → 강녕전 마당, → 광화문 앞) — 궁 안 길이 다 이어져 있는지 확인하지 않아서(Claude 결정).
- 확인: 편집 모드 캡처(숙부·현감·한윤겸·하야시·성 영주·소굴 영주·강녕전 왕·근정전 마당·왜구 병사), Play 캡처(근정전 마당 대비·궁녀 Idle, 호위가 덤비는 장면, 강녕전 앞 왕).

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
  - **치마 `Cloth_Skirt` = MeshCloth(2026-10-05, 메인 씬 `panok/CharacterSeat/Haesin_Player` 만)** — 사용자 선택 "3번 — 메시 자체를 천으로, 확실하게". 예전 BoneCloth(치마 뼈 8가닥, Edge 4개)는 가닥 사이(밑단에서 약 30 cm)로 다리가 빠져나오고, 가닥 위 1/3 을 묶어(maxDistance 깊이 곡선 0 → 0.15) 엉덩이에서 옷이 꺾여 보였음 — 지움. 도구 `Editor/SkirtMeshClothTool.cs`(메뉴 **HaeSin/치마 MeshCloth 적용 (선택한 캐릭터)** — 그림이 없으면 만들고 `Apply`):
    - 치마 메시(측정): 정점 13,318 · 삼각형 11,728, 겉·안 두 겹(두께 1 cm 안팎) + 자른 정점이 많음(경계 변 12,528 / 23,855), UV 는 잘게 흩어짐. 높이(캐릭터 루트 기준) 허리 1.09 m ~ 밑단 0.02 m, 허리띠 쪽 1.0 m 위에 정점 4,321개.
    - 고정/움직임 그림 `Haesin/Cloth/Hanbok_Skirt_Paint.png`(2048, 읽기 가능·무압축·Point): 루트 기준 높이 0.995 m 위 정점 = 빨강(고정), 아래 = 초록(움직임) — UV 삼각형 안 각 텍셀을 가장 가까운 꼭짓점 색으로(`MakePaintMap`). 같은 UV 에 속성이 다른 정점 0.
    - 설정: 원본 메시를 줄인 대리 메시(simpleDistance 0.025 · shapeDistance 0.035 — 두 겹을 하나로) → 정점 571 · 삼각형 1,068(측정). 법선 맞춤 = Hips 기준 바깥(Transform). gravity 5, damping 0.08, radius 0.015, inertia world 0.4 · depth 0.5 · particleSpeedLimit 10, **teleport Reset(0.5 m)**, tether 0.4, triangleBending 0.6, angleRestoration 0.2(곡선 1 → 0.5), angleLimit 끔, **maxDistance 0.4 m + 깊이 곡선 (0,0)(0.15,0.08)(1,1)**(0.5 m 는 달려 베기 때 종처럼 부풀어 줄임), backstop 10/0.015, self collision FullMesh(두께 0.008), 충돌 **Edge** 7개 = 허벅지 2·정강이 2(기존) + 엉덩이 `Hips/SkirtHipCollider`(X 방향 캡슐 r 0.095 · 길이 0.34, 루트 (0, 0.88, 0.015) — 몸 단면 측정 x ±0.16 · z −0.07~0.11) + 발 `LeftFoot|RightFoot/SkirtFootCollider`(발목 → 발끝, Z 방향 r 0.06/0.045).
    - `SkirtLegFollow` 는 그대로 둠(치마 뼈 뿌리가 허벅지를 따라감 = MeshCloth 의 기준 자세), `untrap` 끔(뼈가 더 이상 계산되지 않아 끼임 검사가 맞지 않음), `cloth` = 새 MeshCloth.
    - 확인(Play, 육조거리 길, OnFootController `debugMove`/`debugRun`): MagicaCloth Result Success, 걷기·달리기·달려 베기 앞·옆·뒤 캡처 — 엉덩이 쪽이 꺾이지 않고 이어져 내려옴, 가닥 사이 빈틈 없음. 달릴 때 뒤로 찬 부츠가 뒷자락 아래로 보이는 장면이 있음(치마가 여러 자락으로 갈라진 디자인 — 트임 사이인지 뚫린 것인지 확인 필요). 콘솔 오류·경고 0.
  - 소매 `Cloth_Sleeves`: MeshCloth(Hanbok_Top, 페인트맵 `Cloth/Hanbok_Top_SleevePaint.png`, `Haesin_Outfit_Hanbok.fbx` isReadable), Line, normalAxis Up, maxDistance 0.12, backstop 10/0.01, 콜라이더 Point 5개.
  - MagicaCapsuleCollider(Y, 시작R/끝R/길이): UpLeg 0.09/0.08/0.48, Leg 0.07/0.05/0.36, Spine1 0.12/0.12/0.20, Arm 0.04/0.03/0.17, ForeArm 0.03/0.03/0.22.
  - 코드로 `CurveSerializeData` 필드는 `.value` 에 대입.
- `SkirtLegFollow`: LateUpdate 에서 치마 뿌리 본을 허벅지 흔들림만큼 돌림(Magica 는 그 뒤에 계산). weight F 0.5, FL/FR 0.9, L/R 0.9, BL/BR 0.3, B 0.2, globalWeight 1, `clearance` 0.12 m(자락이 허벅지 축 안쪽이면 더 벌림), `untrap`(자락이 다리 뒤에 끼면 0.35 s 동안 치마 충돌 끔).
- `SpringBoneSystem`(자체 Verlet): 이제 포니테일만.

### 환도 · 손
- 손 소켓 `Socket_Hwando_Hand_R`(RightHand 자식): localPosition (−0.0232, 0.0760, −0.0449), localEuler (358.7, 184.1, 180.0). 이 캐릭터 RightHand 로컬 +Z 는 몸 뒤쪽이라 소켓 회전으로 맞춤(애니메이션으로는 안 고쳐짐). 환도 메시: 피벗 = 코등이, +Z 칼날(끝 0.689 m), 등 +Y / 날 −Y.
- `WeaponHolder`: 허리 칼집 ↔ 손 소켓 재부모(런타임 — 프리팹 인스턴스 밖으로 자식을 옮기면 저장 안 됨). `Drawn` 파라미터 블렌드, `OnSwordMoved` 이벤트. Player 는 칼 넣고 시작. 기본값(2026-10-05) 뽑기 1.0 s·0.26 s 에 손으로 / 넣기 1.0 s·0.70 s 에 허리로. **칼집 돌리기**(`turnSheath`): 동작 중 허리 칼집(`Hwando_Sheathed`)을 원래 자리에서 `turnLocalEuler` (32.13, 246.02, 331.64) 쪽으로 돌렸다 되돌림, 가중치 = Smooth(x,y)·(1−Smooth(z,w)), 뽑기 `drawTurnTimes` (0.04, 0.22, 0.60, 0.85) / 넣기 `sheatheTurnTimes` (0.30, 0.46, 0.74, 0.92). 이 값을 CharacterTest 의 Haesin_Player·Haesin_Enemy 와 메인 씬 `panok/CharacterSeat/Haesin_Player`(2026-10-06 — 그 전엔 0.82/0.30·0.65/0.37 이었음)에 넣음. 육상 병사 프리팹도 해신 값을 복사함.
- **칼 뽑기·넣기 클립(2026-10-05, 사용자 지시 "허리·등이 곧은 상태에서 칼을 오른쪽으로 뽑고 전면으로 옮기며 두 손으로 잡는 식", 넣기도 같이, 상체만, 약 1.0 s)**: `Haesin/Animations/Combat/Custom/Haesin_Draw.anim`·`Haesin_Sheathe.anim`(Humanoid 머슬 + RootT/RootQ, 30 fps). 도구 `Editor/SwordDrawClipTool.cs` `Build(이름)` 이 해신 몸으로 칼 경로를 정하고 팔을 IK 로 풀어 굽는다. 뽑기 칼 경로: 꽂힘(≤0.26) → 칼집 축을 따라 0.22 m 뽑음(0.26–0.44) → 칼집 입구를 축으로 돌려 몸 앞 B(0.44–0.57) → 오른쪽 C(0.57–0.72) → `rig_Idle1` 의 칼 자리 D(0.72–0.92). 넣기는 그 반대(D→C 0–0.28, C→B 0.28–0.45, 되돌림 0.45–0.58, 밀어 넣기 0.58–0.70). 바탕 자세는 `rig_Idle_no_weapon` ↔ `rig_Idle1` 블렌드, 왼손은 칼집 잡기(Idle1 의 칼 대비 왼손 위치를 칼날 쪽 0.20 m)에서 0.62–0.86 사이 손잡이로. 측정: 오른손–손잡이 오차 뽑기 최대 6.2 cm(t 0.57 한 프레임, 0.2–0.5 는 0.0 cm), 넣기 최대 1.9 cm. CharacterTest Play 저속 캡처(`Captures/swordDraw/play/`)로 모양 확인.

- `HwandoGripIK`: 전투 상태에서 애니메이션 왼손목이 손잡이 축 0.20 m 안이면 IK 100 %(0.30 m 밖 0 %), 목표 = 손잡이 위 + 축에서 0.078 m. 컨트롤러 Base IK Pass 켬 + 레이어 `LeftGrip`(Kevin Iglesias 왼손가락 마스크, `Human@ObjectGripHands01`).
- 체형(측정): 해신 팔(위팔+아래팔) 0.386 m, 어깨 폭 0.236 m 로 팩 캐릭터보다 짧음 — **판정 거리·접점은 팩 값이 아니라 해신 몸으로 잰다**.

### 애니메이터 (`Haesin_TestLocomotion`)
- `Locomotion` = 블렌드 트리 `Loco_Root`(float `Drawn`): 칼 넣음 `rig_Idle_no_weapon` 0 / `rig_Walk_no_weapon_IPC` 2.69 / `rig_Sprint_Unequip_IPC` 4.06, 칼 듦 `rig_Idle1` 0 / `rig_Walk_Forward_IPC` 2.69 / `rig_Run_Forward_IPC` 3.30 / `rig_Sprint_IPC` 4.06 (Unity 판 rig_ 클립). 임계값 = 클립 몸 이동 속도.
- 전투 상태는 **Fab 판 FBX 클립**: Attack1~4 = Attack_Combo1~4, AttackB1~B5 = Attack_Combo_B1~B5, Thrust = Attack_Sprint, Sweep → Sweep_Swing = Attack_Heavy1_Start → Attack_Heavy1, Heavy = Attack_HeavyB, Heavy2 = Attack_Heavy2, SprintAttack = Attack_Sprint_Unequip, EvadeAttack = Attack_Evade (공격 상태 속도 0.8), Block/Parry/BlockHit(1~3)/Hit(1~3, Hit_Heavy = Hit8) = Guard/Parry/Guard_Hit1~3/Hit1~3, DodgeBack = Dodge, Death/Death_2 = Death2/Death, Broken → BrokenHold(속도 0.067) → BrokenRecover = Guard_Break. Dodge(옆·앞 회피)·Jump 는 아직 임시 머슬 클립.
- 레이어 `UpperBody`(Kevin Iglesias 상체 마스크, Equip/Unequip — 걸으며 뽑고 넣음), `LeftGrip`. Equip = `Haesin_Draw`, Unequip = `Haesin_Sheathe`(2026-10-05, 예전 ARPG `Equip`/`Unequip` 대체 — 예전 것은 몸을 왼쪽으로 틀어 뽑음).
- **쓰지 않는 클립**(치마 속이 보임·뒤집힘): Evade 8방향, Hit_knockdown 계열, 뒤로 넘어지는 Death, Airborne 계열.
- Fab 임포트(`07.Animation/ARPG_Samurai_Fab/`, 339개): 아바타 = `ARPG_Samurai_Anim_UE_A_Bindpose.FBX`(CreateFromThisModel, Chest = spine_02, UpperChest = spine_03 수동), 나머지 CopyFromOther. `Editor/ARPGFabImportPostprocessor.cs` — 이름에서 접두어 제거, 루프 규칙(`_IPC`/`_Rootmotion` 끝, Idle·Guard 등), **루트 회전·높이는 포즈에 굽고 XZ 만 루트 모션**(안 그러면 몸 비틀기가 루트 회전으로 빠져 사라짐). 확장자가 대문자 `.FBX` 라 경로 비교는 대소문자 무시.
- 타이밍 규칙: windup = (손 속도 최고점 − 0.06) / 0.8, 총 길이 = 클립 길이 / 0.8, active 0.1. chainAt = 다음 클립 첫 자세와 가장 비슷한 시각 / 0.8.

### 전투 (`13.Combat/`, 세키로식)
- 규칙: 체력 100 / 체간 100. 막기 +10, 튕겨내기(막기 누른 지 0.2 s 안) 성공 시 상대 +25·자신 +5. 마지막 체간 피해 3 s 뒤 초당 20 회복(체력 낮으면 최대 50 % 감소). 체간이 다 차면 3 s 무방비 → 맞으면 결정타 즉사. 회피 무적 0.3 s. 위험 공격(찌르기·베어 넘기기)은 막아도 피해 — 찌르기는 옆 회피, 베어 넘기기는 점프. 피격 경직 0.35 s(강공격 0.6~0.7).
- `SwordCombatant`(공용 상태기계 Idle/Attack/Block/Dodge/Jump/HitStun/Broken/Dead): 판정은 콜라이더 없이 거리·각도(active 시작 순간). 예비 동작 중 상대 추적은 windup 55 % 까지만(그 뒤 고정 → 옆 회피가 통함). 루트 모션은 Attack·HitStun·Dead·Broken·DodgeBack 에서만 XZ 적용, 상대와 0.9 m 이내로 못 파고듦. 이동 대상은 부모의 `ICharacterMover.MotionRoot`(OnFootRoot 면 CharacterController.Move). 콤보는 기본(4타)/B(5타) 자동 섞기(같은 콤보 최대 2번 연속).
- 공격 값(windup / active / recovery s, range m / arc °): 내려베기 0.24/0.1/0.685, 1.25/60 · 가로베기 0.40/0.1/0.7125, 1.25/60 · 찔러베기 0.26/0.1/0.7275, 1.3/45(chain 0.4625) · 마무리 베기 0.4875/·/0.85, 1.4/70, 피해 24·체간 32 · 찌르기(위험) 0.425/0.1/0.70, 1.35/25 · 베어 넘기기(위험) 1.2625/0.1/0.575, 1.3/100 · 강베기 0.2375/·/0.975, 2.0, 30·45 · 강베기 2 0.4625/·/0.85, 1.5 · 달려 베기 0.3875/·/0.7375, 1.8, 22·28 · 회피 베기 0.275/·/0.85, 1.4, 20·26 · B 콤보 비껌/돌려/올려/훑어/내리꽂기 windup 0.30/0.30/0.275/0.30/0.325, 피해 14/14/16/16/24. chainAt 기본 콤보 0.375 → 0.60 → 0.4625. range = 칼끝 도달(해신 측정) + 상대 반경 0.35.
- 조작(`PlayerCombatController`): 좌클릭 짧게 = 콤보(뗄 때 나감), **좌클릭 0.35 s 꾹 = 강베기**(후딜 중 다시 꾹 = 강베기 2), Shift 달리며 좌클릭 = 달려 베기, 회피 직후 좌클릭 = 회피 베기, 우클릭 = 튕겨내기·누른 채 막기, Space = 이동키와 회피 / 없으면 점프. 칼 자동 뽑기(살아 있는 적 6 m 안, 또는 클릭)·자동 넣기(5 s 무입력·비전투). E·F·Q 는 비어 있음(메인 씬 E = 승·하차). 테스트 키: T 부활, 1·2·3 옷·신발·머리, R 위치 초기화, Esc 커서.
- `EnemySwordAI`: 1.7 m 까지 접근 → 1.6~2.6 s 뒤 [콤보 60 % | 찌르기 20 % | 베어 넘기기 20 %], B 콤보 0.4, 강베기 0.12(+강베기 2 0.5), 플레이어 공격에 막기 35 %·튕겨내기 15 %, 위험 공격엔 50 % 회피. 이동 속도 2.69. transform 을 직접 옮김(중력·지형 없음). 현재 매우 공격적.
- **시험용 무적(2026-10-06 사용자 요청 "잠시 캐릭터 무적, 체력만")**: `CombatStats.invulnerableHealth`(새 필드) — 켜면 `Damage`·`Deathblow` 로 체력이 줄지 않음(체간은 그대로 차고 무너짐·경직은 일어남). **메인 씬 `Haesin_Player` 에 켜 둠 — 시험이 끝나면 끌 것.** 확인(Play): 관군에게 맞아 체간 18 → 80 이 되는 동안 체력 100 그대로.
- `CombatHUD`(OnGUI 임시), `CombatAutoTest`(MCP 검증용 자동 조작 — PlayerCombatController 를 끄고 써야 함).

### 표정
- `EyeBlink`: 2.5~5.5 s 무작위, 15 % 두 번, 감기 0.07 + 유지 0.03 + 뜨기 0.12 s. 반쯤 감김 보정 키 `_mid` 가중치 4w(1−w). `baseClosed` = 기본 감김.
- `FacialExpression`: 프리셋 Neutral/Smile/SmileLeft/SmileRight/Frown/Pucker/Press/MouthOpen/OpenSmile/Surprised/Custom, 말하기(Perlin 입 벌림). 캐릭터 왼쪽 = +x 키.

### 승·하차 (`08.Character/`, SampleScene)
- `BoardingSystem`(`Boarding`): 탑승 중 배가 LandingPoint 의 dockRange 안 & **E 3초** → 하차: `OnFootRoot`(자동 생성, CharacterController r 0.32 h 1.75 · stepOffset 0.4 · slopeLimit 50° + `OnFootController`) 를 판자 위에 세우고 외형을 그 아래로 옮긴 **다음** 켬, `PlayerCharacter.SetOnFoot()`, CannonControl·CameraFollow 끔, **MoveControl 은 끄지 않고 `SetDocked(true)`**, 카메라 부모·로컬 자세 저장. 캐릭터가 배 중심 `boardRange`(씬 12 m) 안 또는 선체 `boardHullRange` 9 m 안 & E 3초 → 승선(카메라 로컬 자세 복구, SetDocked(false)).
- `PlayerCharacter`: standOffset 0(피벗 = 발), `onFootOnly`(= PlayerCombatController) 는 배 위에서 끄고 칼을 즉시 넣음.
- `OnFootController`: 걷기 2.69 / 달리기 4.06 / 가속 8, 중력, `PlayerOrbitCamera`(거리 3.2, 높이 1.1, 감도 5, pitch −15~60, 마우스만 움직이면 돎, 휠 줌, SphereCast). Animator 가 없으면 Update 에서 다시 찾음. `walkThroughLayers`(StepSurface)는 CharacterController.excludeLayers 로 통과.
- `FootIK`(Haesin_Player, 컴포넌트 순서 HwandoGripIK 보다 위): OnFoot 에서만(사망·점프·공중 제외). 발목·발끝 아래로 구(r 0.04)를 쏴 높은 쪽 바닥 높이 − 피벗 높이만큼 발을 옮기고, 낮은 발 쪽만큼 골반(bodyPosition)을 옮김. 계단 `BaseGroup/CaveStair` = MeshCollider + StepSurface 레이어 — 몸은 디딤판을 통과해 밑 터레인 경사를 걷고 발만 디딤판에 놓임. 측정(오르기): 발바닥–디딤판 최저 −3.4 cm(1프레임), 발가락 뼈가 디딤판 아래로 들어간 프레임 0.6 %(최저 −5.8 cm). 칼 든 채 계단에서 왼손 그립 유지 확인.

### 일본 장군 모델 (`06.Character/Shogun/`, 2026-10-04)
- 원본: 사용자가 Tripo 로 만든 GLB 3개(`Downloads/1000.glb`·`8000.glb`·`10000.glb` — 이름과 달리 삼각형 1,077 / 5,579 / 11,023, 키 약 1.0 m, 텍스처 각 4096 jpg). 원본 리깅 상태(측정): 10000 은 정점 전부가 Hips 하나에 100 %, 8000 은 정점마다 뼈 하나(섞임 0), 1000 도 섞인 정점 32/1,692. 뼈대도 셋이 다름(skin 뼈 55/54/44, Hips 높이 0.447/0.459/0.463). 세 파일 모두 뼈 노드에 위치·회전 값이 없고 inverseBindMatrices 만 있으며, 그 뼈대는 메시보다 위에서 보아 90° 돌아가 있었음(팔이 앞뒤로 뻗음).
- 지금(사용자 선택 "뼈대 하나로 다시 입힘"): 10000 의 IBM 으로 만든 mixamorig 뼈대 55개 하나에 세 메시를 자동 웨이트로 다시 붙임(메시 형상은 원본 그대로, 정점당 최대 4뼈, 웨이트 없는 정점 0). 키 1.70 m(사용자 선택), T 자세, +Z 를 봄, 왼손 −X.
- `Shogun.fbx`: 자식 `Shogun`(뼈대, 회전 270·0·0 — 뼈 위치는 맞음) + `Shogun_LOD0/1/2`(SkinnedMeshRenderer, 같은 뼈 55개) → 가져오기가 이름(`_LOD#`)으로 LODGroup 을 자동으로 만듦. Rig = Humanoid(CreateFromThisModel, 아바타 valid·human), 애니메이션 가져오기 끔, 노멀 가져옴, 탄젠트 계산. 재질은 `AddRemap` 으로 `Materials/M_Shogun_LOD0~2`(HDRP/Lit, 금속 0, 매끄러움 0.35, BaseColorMap 만).
- 텍스처 `T_Shogun_LOD0/1/2_BaseColor.jpg`(원본 jpg 그대로) — 가져오기 최대 크기 4096 / 2048 / 1024, sRGB, 스트리밍 밉맵. LOD 마다 UV 가 달라 텍스처를 같이 쓸 수 없음.
- `Shogun.prefab` = FBX 의 프리팹 변형(Variant): LODGroup 크기 1.85 · 기준점 (0, 0.85, 0), CrossFade(폭 0.1), 전환 화면 높이 0.1068 / 0.0401 / 0.0107 = 약 15 m / 40 m / 150 m 에서 LOD1 / LOD2 / 끔(FOV 60 기준). Animator 컨트롤러·충돌체·AI 없음. 씬에는 안 넣음.
- 확인(편집 모드, 2026-10-04): 빈 추가 씬(y 3000)에 세 LOD 를 `ForceLOD` 로 띄우고 해신 휴머노이드 클립 `rig_Attack_Combo1` 을 `SampleAnimation` → 세 LOD 모두 같은 자세로 변형됨(BakeMesh 그림으로 확인), 콘솔 오류·경고 0, 추가 씬은 저장 안 하고 닫음. 가까이 모습(관절·갑옷 접힘)·실제 애니메이션은 사용자 확인 필요.

### 일본 병사 모델 (`06.Character/JPSoldier/`, 2026-10-04)
- 원본: 사용자가 Tripo 로 만든 GLB 3개(`Downloads/10000_jpsoldier.glb`·`5000_jpsoldier.glb`·`1000_jpsoldier.glb`, 삼각형 10,125 / 5,020 / 1,070, 키 1.0 m, 텍스처 각 4096 jpg). **10000 에만 리깅**(사용자) — 장군 때와 달리 뼈 노드에 위치·회전 값이 있고 뼈대가 메시와 맞음, mixamorig 65뼈, 정점마다 섞인 웨이트(측정: 섞인 정점 11,543 / 14,477, 웨이트 없는 정점 0, 정점당 최대 4뼈). 5000·1000 은 뼈대·웨이트 없음.
- 지금: 10000 의 뼈대·웨이트를 그대로 쓰고, 5000·1000 메시에는 10000 메시에서 가까운 면 기준으로 웨이트를 옮김(웨이트 없는 정점 0, 정점당 최대 4뼈). 메시 형상은 원본 그대로. 키 1.70 m, T 자세, +Z 를 봄, 왼손 −X.
- 나머지는 장군과 같은 설정: `JPSoldier.fbx`(자식 `JPSoldier` 뼈대 회전 270·0·0 + `JPSoldier_LOD0/1/2`, 같은 뼈 65개) → 이름으로 LODGroup 자동, Humanoid(아바타 valid·human), 재질 `Materials/M_JPSoldier_LOD0~2`(HDRP/Lit, 금속 0), 텍스처 `T_JPSoldier_LOD0/1/2_BaseColor.jpg` 최대 4096 / 2048 / 1024. `JPSoldier.prefab`(프리팹 변형): LODGroup 크기 1.85 · 기준점 (0, 0.85, 0), CrossFade(0.1), 약 15 / 40 / 150 m. 충돌체·AI 없음, 씬에는 안 넣음.
- **텍스처 고침(2026-10-04, 사용자 지시 "노란 원형이 안 예쁘고 저작권 이슈 — 갑옷은 검은 윤기 판, 모자는 원형 없애기")**: 세 LOD 텍스처를 각자 UV 로 칸마다 3D 위치를 구해(메시를 텍스처에 래스터) 고침. 갑옷 = 몸통(높이 0.55~1.43 m, |x| < 0.30)·정강이(0.12~0.56 m)의 갈색 가죽 + 가슴(0.85 m 위) 노란 칸 → 검정(sRGB 0.065·0.065·0.072 × 원래 밝기 비율^0.6 — 무늬 결은 남김). 모자 앞 노란 원(중심 약 (−0.04, 1.60, 0.11), 반지름 약 7 cm) → 같은 모자를 꼭짓점 축으로 90°/180° 돌린 자리의 텍스처를 가장 가까운 칸으로 가져다 채움(처음엔 단색으로 채웠더니 매끈한 원판이 보였음). 마스크 `T_JPSoldier_LOD0/1/2_Mask.png`(선형, A = 매끄러움: 갑옷 0.82 · 모자 0.45 · 나머지 0.35, G = AO 1) → 재질 `_MaskMap`(+`_MASKMAP`, 매끄러움·금속·AO 리맵 0~1). 원본 텍스처는 `Downloads/*_jpsoldier.glb` 안에 있음. 처리 스크립트 = Claude 작업 폴더(`raster.py`·`recolor.py`·`hatfix3.py`, 옮기지 않음).
- **조총 사격 모션(2026-10-04)**: 클립 `Animations/JPSoldier_MatchlockFire.fbx` → `MatchlockFire`(2.0 s, 30 fps, 루프, Humanoid — 아바타 = JPSoldier 아바타 복사, 루트 회전·높이·XZ 포즈에 굽기). 겨눔(몸 −35° 돌림, 오른뺨에 개머리, 오른손 손목·왼손 앞 총대 받침) → 1.0 s 발사 반동(총 뒤로 4.5 cm · 총구 7° 들림, 상체 뒤로) → 1.8 s 복귀. 컨트롤러 `Animations/JPSoldier_Matchlock.controller`(상태 하나). 프리팹 Animator: 컨트롤러 연결, applyRootMotion 끔, CullUpdateTransforms. 조총 = 오른손 뼈 아래 `Matchlock_Socket`(로컬 (0.0257, 0.0796, 0.0160) / (331.9, 312.9, 198.3)) + `Matchlock`, 렌더러는 LODGroup 세 단계에 모두 넣음. 확인: 편집 모드 샘플 — 왼손이 총에 대해 반동 내내 1 mm 안에서 그대로(측정), 캡처로 겨눔·반동 확인. 총구 불꽃·연기·소리 없음.
- 확인(편집 모드): `rig_Attack_Combo1` 을 세 LOD 에 샘플 → `BakeMesh` 그림으로 셋 다 같은 자세 확인, 콘솔 오류·경고 0. 가까이 모습·실제 애니메이션은 사용자 확인 필요.

### 조선 수군 병사 모델 (`06.Character/JoseonNavy/`, 2026-10-04)
- 원본: 사용자가 Tripo 로 만든 GLB 3개(`Downloads/joseon_navy_10000.glb`·`_3000`·`_1000`, 삼각형 10,381 / 3,378 / 1,208, 키 1.0 m, 텍스처 각 4096 jpg). 10000 에만 리깅(mixamorig 65뼈, 섞인 정점 5,459 / 6,941, 웨이트 없는 정점 0).
- 일본 병사와 같은 방식·설정: 10000 뼈대·웨이트 그대로, 3000·1000 은 10000 에서 웨이트를 옮김(웨이트 없는 정점 0). 키 1.70 m, +Z. `JoseonNavy.fbx`(뼈대 `JoseonNavy` + `JoseonNavy_LOD0/1/2`) Humanoid, 재질 `Materials/M_JoseonNavy_LOD0~2`(HDRP/Lit, 매끄러움 0.35), 텍스처 최대 4096 / 2048 / 1024, `JoseonNavy.prefab`(프리팹 변형, LOD 15 / 40 / 150 m).
- **활 쏘기 모션(2026-10-04, 사용자 지시 "활을 만들고 활 쏘는 모션")**: 클립 `Animations/JoseonNavy_BowShoot.fbx` → `BowShoot`(3.0 s, 30 fps, 루프, Humanoid — 아바타 = JoseonNavy 아바타 복사). 몸을 −70° 쯤 돌려 왼어깨가 과녁, 머리는 과녁. 0 s 가슴 앞에서 화살 건 채 → 0.47 s 활 들어 올림 → 1.0 s 만작(시위를 오른뺨 뒤까지, 줌통에서 약 0.59 m) → 1.67 s 까지 유지 → 1.68 s 놓음(오른손이 뒤로 13 cm 빠지고 손가락 펴짐, 활 위가 앞으로 5°) → 2.27 s 까지 유지 → 2.73 s 처음 자세로. 커브 `BowString`(가져오기 설정 커브, 0.561~0.906 정규 시간 = 50.5~81.5 프레임만 0, 나머지 1, 계단) → 컨트롤러 `Animations/JoseonNavy_Bow.controller`(상태 하나 + float `BowString` 기본 1).
- 프리팹: Animator 컨트롤러 연결, applyRootMotion 끔, CullUpdateTransforms. 활 = 왼손 뼈 아래 `Bow_Socket`(로컬 (−0.0087, 0.0678, −0.0356) / (298.0, 359.8, 90.1) — 2026-10-04 왼손 고침 후 값) + `Bow`. 오른손 뼈 아래 `String_Socket`(로컬 (0.0023, 0.0755, −0.0052) = 시위 거는 자리). 루트 아래 `BowRig`(`BowString` + `String_Top`·`String_Bottom` 토막 + `Arrow`). 활·화살 렌더러는 LOD 세 단계, 시위는 LOD0·1.
- `BowString`(08.Character, `[ExecuteAlways]`, LateUpdate): 시위 가운데 = `BowString` ≥ 0.5 면 `String_Socket`, 아니면 활 로컬 쉬는 자리 (0.02, 0.03, −0.17). 고자(활 로컬 (0, ±0.605, −0.170)) → 가운데로 토막 두 개를 늘여 놓음. 화살은 잡고 있을 때만 켜고 시위 가운데에서 줌통 옆 (0.02, 0.03, 0) 을 향함. 플레이가 아니면 `editorHold`(기본 켬)로 정함. 날아가는 화살·맞음·소리 없음.
- **왼손 고침(2026-10-04, 사용자 지시 "활대를 잡은 손이 반대로 뒤틀림")**: 원인(측정) — 활을 쥔 왼손이 위아래로 뒤집혀 있었음(검지·엄지가 아래, 새끼가 위; 검지−새끼 높이차 −0.064 m), 손가락 방향이 아래팔과 약 50° 꺾임. 지금: 엄지·검지가 위(높이차 +0.063 m), 주먹 안 줌통 자리는 그대로, 손을 아래팔 쪽으로 28° 돌리고 팔꿈치를 바깥(활 시위 반대쪽)으로 — 만작·유지(0.47~2.27 s) 손목 꺾임 2~8°, 올리고 내리는 순간(0.2~0.33 s, 2.4~2.55 s)엔 약 30~42° 까지(측정). 왼팔 세 뼈만 바뀌고 나머지는 그대로. `JoseonNavy_BowShoot.fbx` 다시 내보냄(가져오기 설정·`BowString` 커브 유지 확인), 두 프리팹(수군·해적)의 `Bow_Socket` 회전 갱신. 확인: 샘플에서 활 위 = 월드 위, 활 앞 = 쏘는 쪽, 만작 때 시위 손이 활 로컬 (0.02, 0.03, −0.59) = 화살 지나는 자리 바로 뒤, 화살 방향 일직선, 손 가까이 캡처 4방향.
- 확인(편집 모드, 2026-10-04): 클립 샘플 손 위치가 만든 값과 1 mm 안(측정), 캡처(0 s·1.0 s·1.83 s)로 걸기·만작·놓은 뒤 시위 복귀·화살 사라짐 확인, 콘솔 오류·경고 0. 실제 Play 는 안 해 봄 — 사용자 확인 필요.

### 시민 모델 (`06.Character/Citizen/`, 2026-10-04)
- 원본: 사용자가 Tripo 로 만든 GLB 12개(`Downloads/hanbokmale_10000/3000/1000.glb`, `hanbokwoman_10000.glb`·`hanbokwoman3000.glb`·`hanbokwoman_1000.glb`, `kimonomale_*`, `kimonofemale_*`, 키 1.0 m, 텍스처 각 4096 jpg). 10000 에만 리깅(사용자). 원본 리깅 상태(측정): 네 파일 모두 뼈 노드에 위치·회전 값이 없고 IBM 만 있음(장군과 같음). 한복 남·여(52뼈)는 섞인 정점 258 / 13,583 · 235 / 11,750 뿐(거의 정점마다 뼈 하나), 기모노 남·여(54뼈)는 Hips·RightFoot 두 뼈에만 섞임 없이 붙음 → 그대로 쓸 수 없었음.
- 지금: 조선 수군 모델의 mixamorig 65뼈 뼈대를 틀로 각 모델 몸에 맞춰(높이는 구간별 — 발·골반·어깨 높이·정수리, 팔 좌우는 팔 벌린 폭 비, 다리 좌우는 발 간격 비 — 측정 팔/다리 배율: 한복남 1.032/1.115, 기모노남 0.932/1.029, 한복여 0.848/0.814, 기모노여 0.82/0.825) 세 LOD 메시를 자동 웨이트로 다시 붙임(얇은 옷은 두께를 준 뒤 복셀로 다시 만든 대리 메시에서 웨이트를 구해 옮김, 정점당 최대 4뼈, 웨이트 없는 정점 0). 메시 형상은 원본 그대로. 키 남 1.70 m · 여 1.60 m(사용자 지시 "여성은 1.6m"), T 자세, +Z 를 봄, 왼손 −X.
- 이름(LOD0/1/2 삼각형): `HanbokMale` 10,570 / 3,222 / 1,104, `HanbokFemale` 9,909 / 2,977 / 1,061, `KimonoMale` 10,304 / 3,235 / 1,101, `KimonoFemale` 10,028 / 2,988 / 1,087.
- 설정은 다른 NPC 와 같음: `<이름>.fbx`(뼈대 `<이름>` + `<이름>_LOD0/1/2`, 같은 뼈 65개) → 이름으로 LODGroup 자동, Humanoid(CreateFromThisModel, 네 아바타 모두 valid·human), 재질 `Materials/M_<이름>_LOD0~2`(HDRP/Lit, 금속 0, 매끄러움 0.3), 텍스처 `T_<이름>_LOD0/1/2_BaseColor.jpg` 최대 4096 / 2048 / 1024. `<이름>.prefab`(프리팹 변형): CrossFade(0.1), 약 15 / 40 / 150 m(남 크기 1.85 · 기준점 (0, 0.85, 0), 여 1.75 · (0, 0.8, 0)), Animator 컨트롤러 없음, applyRootMotion 끔, CullUpdateTransforms. 충돌체·AI 없음, 씬에는 안 넣음.
- **어깨 고침(2026-10-05, 사용자 지적 "마을 NPC 어깨가 수축된 느낌, 걷는 게 위축됨")**: 원인(측정) — Quaternius `Walk_Loop`·`Idle_Loop` 을 이 모델들에 틀면 근육 값 `Shoulder Front-Back` 이 0.9~1.7(1 을 넘음 = 기본 범위 ±15° 끝 너머)이라 쇄골이 앞으로 크게 말리고, 걷는 동안 어깨 폭(위팔 뼈 사이)이 T 자세보다 8 % 좁아짐(한복남 0.315 → 0.290 m)·위팔이 1.6 cm 올라감. 고침 = 네 FBX 의 아바타 근육 범위(`ModelImporter.humanDescription` → 다시 가져오기): 어깨 Front-Back −15~**+3°**(앞으로 말리는 쪽을 줄임), Down-Up −15~30(기본), 위팔 Down-Up **−52**~100(기본 −60 — 팔을 조금 덜 내려 몸에서 뜨게). 결과(측정, 걷기 0.33 s): 어깨 폭 0.312 m(T 0.315), 위팔 높이 T 와 같음, 팔 끝 바깥으로 3.9 → 5.7 cm. 아바타 4개 valid·human. 앞·옆 렌더로 비교 — 변화는 크지 않음, 사용자 확인 필요. 웨이트는 안 건드림.
- 확인(편집 모드, 2026-10-04): 네 모델의 LOD0·LOD2 에 해신 걷기 클립 `rig_Walk_no_weapon_IPC` 를 샘플 → `BakeMesh` 그림으로 여덟 개 모두 걷는 자세로 변형됨(치마·도포 자락이 다리를 따라 늘어남). 가까이 모습·실제 애니메이션은 사용자 확인 필요.

### 해적 모델 (`06.Character/Pirate/`, 2026-10-04 — 사용자: "플레이어 판옥선의 해적, 활 쏘는 모션")
- 원본: 사용자가 Tripo 로 만든 GLB 3개(`Downloads/pirate_10000.glb`·`_3000`·`_1000`, 삼각형 9,504 / 3,116 / 1,094, 키 1.0 m, 텍스처 각 4096 jpg). 10000 에만 리깅. 원본 리깅 상태(측정): 뼈 노드에 위치·회전 값 없음(IBM 만, 장군·시민과 같음), 섞인 정점 279 / 13,351 뿐 → 그대로 못 씀.
- 지금: 시민 모델과 같은 방식 — 조선 수군 65뼈 뼈대를 몸에 맞춰(골반 0.906 m, 손 높이 배율 1.037, 팔 좌우 1.030, 다리 좌우 1.002 — 측정) 세 LOD 에 자동 웨이트. 웨이트는 이웃 정점끼리 한 번 더 고르게 폄(측정: 이웃 정점 간 웨이트 차 > 0.5 인 변 2,639 → 19 — 안 하면 팔을 들 때 몸통 옷이 가시처럼 튀어나왔음). 키 1.70 m, +Z, 왼손 −X.
- **머리 판때기 정리(사용자 지시 "머리 부분 이상한 판때기 — 오류, 정리")**: LOD0 = 머리 옆·뒤에 따로 떠 있던 판 5조각(정점 261) 지움 → 삼각형 9,343. LOD1·LOD2 = 머리(높이 1.40 m 위, 머리띠 꼬리 제외)에서 LOD0 머리 겉면보다 1.2 cm 넘게 튀어나온 정점을 LOD0 겉면으로 당김(LOD1 89개 + 귀 끝 18개, LOD2 38개 — 면은 안 지움). 렌더로 앞·옆·뒤 확인. 머리 뒤 상투 쪽 LOD1 은 면이 조금 각져 보임.
- 설정(다른 NPC 와 같음): `Pirate.fbx`(뼈대 `Pirate` + `Pirate_LOD0/1/2`, 같은 뼈 65개) Humanoid(아바타 valid·human), 재질 `Materials/M_Pirate_LOD0~2`(HDRP/Lit, 금속 0, 매끄러움 0.35), 텍스처 `T_Pirate_LOD0/1/2_BaseColor.jpg` 최대 4096 / 2048 / 1024. `Pirate.prefab`(프리팹 변형): LODGroup 크기 1.85 · 기준점 (0, 0.85, 0), CrossFade, 약 15 / 40 / 150 m. 충돌체·AI 없음, 씬·배에는 안 넣음.
- **활 쏘기**: 조선 수군과 같은 클립을 씀 — 컨트롤러 `Animations/Pirate_Bow.controller`(= `JoseonNavy_Bow.controller` 복사, 클립은 `JoseonNavy/Animations/JoseonNavy_BowShoot.fbx` 의 `BowShoot` 을 휴머노이드로 그대로 재생, float `BowString`). 프리팹에 수군과 같은 `Bow_Socket`(왼손)·`String_Socket`(오른손)·`BowRig`(`BowString` + 시위 토막 + 화살)을 같은 로컬 값으로 넣고 참조를 해적 것으로 연결. 활·화살은 LOD 세 단계, 시위는 LOD0·1. Animator applyRootMotion 끔, CullUpdateTransforms.
- 확인(편집 모드, 2026-10-04): 클립 샘플 — 만작(1.0 s) 때 줌통↔시위 0.610 m(수군 0.591), 시위↔머리 0.100 m(수군 0.090), 놓은 뒤(1.83 s) 화살 꺼짐. 캡처로 걸기·만작·놓음과 걷기 클립 `rig_Walk_no_weapon_IPC`(LOD0·LOD2) 변형 확인, 콘솔 오류·경고 0, 추가 씬은 저장 안 하고 닫음. 실제 Play 는 안 해 봄.

### 사공 노인 모델 (`06.Character/OldMan/`, 2026-10-05 — 나룻배 사공)
- `HanbokOldMan.fbx`: 메시 LOD 3단계 `HanbokOldMan_LOD0/1/2` + 노 `Ferry_Paddle`(왼손 뼈 `mixamorig:LeftHand` 자식, 길이 2.15 m) — 삼각형(노 포함) 10,401 / 3,210 / 1,138. 앞 = +Z. Rig = **Generic**(CreateFromThisModel — 클립을 이 뼈대에 맞춰 만들어 Humanoid 리타깃 오차를 피함, 아바타 valid), optimizeGameObjects 끔, 재질 가져오기 None, 애니메이션 압축 끔. 클립 `OldMan_Row` 0–89(2.97 s, 30 fps, 루프 — 서서 배 한쪽으로 젓는 SUP 방식, 노 쪽으로 상체를 틂) · `OldMan_Idle` 0–179(5.97 s, 루프 — 노를 세워 듦). 두 클립 모두 노는 손에 붙은 채.
- 재질 `Materials/M_HanbokOldMan_LOD0~2`(텍스처 `Textures/` jpg 최대 4096 / 2048 / 1024), `M_FerryPaddle`(512).
- 컨트롤러 `Animations/OldMan_Ferry.controller`: 기본 Idle ↔ Row, bool `Rowing`(넘김 0.4 / 0.6 s 고정 시간, exit time 없음).
- `HanbokOldMan.prefab`(프리팹 변형): Animator 컨트롤러 연결, applyRootMotion 끔, CullUpdateTransforms. LODGroup 크기 1.85 · 기준점 (0, 0.85, 0), 0.1068 / 0.0401 / 0.0107(약 15 / 40 / 150 m), 노 렌더러는 세 LOD 에 모두. 충돌체·AI 없음. 나룻배 프리팹(§4 호수·나룻배) 안에서 씀.
- 확인: 나룻배 Play 캡처에서 노 젓기·대기(§4). 가까이 모습은 사용자 확인 필요.

### 한양 조력자 모델 (`06.Character/HanyangAlly/`, 2026-10-08 — 사용자: "홍단령 모델은 한양에서 만날 조력자")
- `HanyangAlly.fbx`: 뼈대 `HanyangAlly`(mixamorig 52뼈) + `HanyangAlly_LOD0/1/2`(SkinnedMesh, 삼각형 22,184 / 6,910 / 1,946 — 2026-10-09 깃을 고친 뒤 측정). 재질 슬롯 2개: `M_HanyangAlly_Skin`(몸·눈·띠·과판·머리카락·수염·목화, 한 면) · `M_HanyangAlly_Cloth`(단령·깃·속깃·사모·사모 날개·망건 — **양면**: 소매 안·치맛단·날개 판이 한 겹). 키 1.65 m(사모 빼고), +Z 를 봄, 정점당 최대 4뼈·정규화.
- 깃(2026-10-09, 사용자 지적 "옷깃 부분이 깔끔하지 않음"): 둥근 깃을 좌우 대칭의 매끈한 띠(폭 약 3.4 cm, 뒤는 목 쪽으로 붙고 앞이 가장 낮음)로 다시 만들고, 깃 위로 비어져 나오던 단령 목둘레는 깃 아래로 넣음. 흰 속깃은 앞에서 교차(V)하게 함. 귀 앞쪽 피부의 작은 구멍(뒤의 머리카락이 비쳐 검은 점으로 보였음)도 메움.
- **바인드 자세는 A(팔 약 48° 아래), FBX 기본(노드) 자세는 T** — 팔을 내린 동작에서 어깨 변형을 줄이려고 A 로 붙이고, Humanoid 아바타는 T 로 잡히게 함. 가져온 모델의 위팔 방향 (∓1, 0, 0) = T 자세로 잡힘(측정, 2026-10-09).
- 텍스처 `Textures/`: `T_HanyangAlly_BaseColor.jpg`(sRGB, 눈썹·속눈썹·AO 일부 포함) · `T_HanyangAlly_Normal.png`(OpenGL, Y+, 가져오기 NormalMap) · `T_HanyangAlly_Mask.png`(선형, HDRP 마스크: R 금속 0, G AO, B 0, A 매끄러움). 4096 하나를 세 LOD 가 같이 씀(LOD 마다 따로 아님). 셋 다 최대 4096 · 스트리밍 밉맵.
- 가져오기(2026-10-09): Rig = Humanoid(CreateFromThisModel, 아바타 valid·human), 애니메이션 가져오기 끔, 노멀 가져옴·탄젠트 계산, 재질은 `AddRemap` 으로 `Materials/M_HanyangAlly_Skin`(HDRP/Lit 한 면) · `M_HanyangAlly_Cloth`(HDRP/Lit 양면, 노멀 모드 Flip) — 둘 다 BaseColor·Normal·Mask 세 장, 금속 리맵 0~0, 매끄러움·AO 리맵 0~1.
- `HanyangAlly.prefab`(프리팹 변형): LODGroup 크기 1.85 · 기준점 (0, 0.85, 0), CrossFade(폭 0.1), 0.1068 / 0.0401 / 0.0107(약 15 / 40 / 150 m), Animator 컨트롤러 없음·applyRootMotion 끔·CullUpdateTransforms. 충돌체·AI 없음, 씬에는 안 넣음.
- 확인(편집 모드, 2026-10-09): 빈 추가 씬에서 기본(T) 자세와 `rig_Walk_no_weapon_IPC` 를 샘플한 LOD0/1/2 를 `BakeMesh` 로 띄워 찍음(`Captures/hanyang/check_lods.png`) — 세 LOD 모두 걷는 자세로 변형, 치맛자락이 다리를 따라감. LOD2 는 단령에 작은 검은 점이 보임(40 m 밖 단계). 추가 씬은 저장 안 하고 닫음. 실제 Play·가까이 모습은 사용자 확인 필요.

### 퀘스트 NPC 모델 (`06.Character/<이름>/`, 2026-10-09 — 사용자: "다른 캐릭들도 밤동안 전부 작업")
- 구성·설정은 한양 조력자와 같음: `<이름>.fbx` = 뼈대 `<이름>`(mixamorig 52뼈) + `<이름>_LOD0/1/2`(SkinnedMesh), 재질 슬롯 2개 `M_<이름>_Skin`(한 면) · `M_<이름>_Cloth`(양면), 텍스처 `T_<이름>_BaseColor.jpg` · `_Normal.png` · `_Mask.png`(4096 하나를 세 LOD 가 같이 씀), 바인드 A·기본 자세 T, +Z. 가져오기·재질·프리팹은 `Editor/NpcModelSetupTool.cs`(`Setup`·`MakePrefab`·`Capture`)로 함 — 프리팹 변형 LODGroup 1.85 · (0, 0.85, 0) · CrossFade 0.1 · 15/40/150 m, Animator 컨트롤러 없음·applyRootMotion 끔·CullUpdateTransforms. 충돌체·AI 없음, 씬에는 안 넣음.
- **JoMyeongGu**(현감 조명구 — 16세기 융복): 홍철릭(허리 아래 잔주름 치마, 좁은 소매)·흰 동정 교차깃(우임) + 안에 옥색 속옷 깃이 보임, 남색 광다회(실띠, 앞 오른쪽 매듭·술 두 가닥), 흑립 + 갓끈, 망건, 목화. 손 소품 없음(사용자 선택). 삼각형 22,690 / 7,032 / 1,953. 아바타 valid·human, 위팔 (−1, 0, 0) = T(측정). 소매 끝이 걷기 자세에서 조금 너덜해 보임(확인 그림) — 다듬을 과제(§9).
- **YiDam_Dopo**(이담 1막 — 선비 차림): 옥색 도포(넓은 소매)·흰 동정 교차깃 + 흰 속옷 깃, 자색 세조대(가슴 높이), 흑립 + 갓끈, 망건, 낮은 신. 삼각형 23,346 / 7,142 / 2,012. 아바타 valid·human. 이담 4·5막(곤룡포·익선관)은 같은 얼굴·몸으로 따로 만듦(§9).
- **YiDam_King**(이담 4·5막): 대홍 곤룡포(둥근 깃 + 흰 속깃), 가슴·등·양 어깨에 금색 둥근 보(무늬는 단순한 금실 물결 — 용 무늬 아님), 옥대(검붉은 띠 + 옥색 과판), 익선관(뒤에 위로 선 날개 두 장), 망건, 목화. 얼굴·몸은 YiDam_Dopo 와 같은 설정으로 다시 만듦. 삼각형 21,926 / 6,713 / 1,996.
- **GwonSiheon**(병조참판): 홍단령(둥근 깃 + 흰 속깃), 가슴·등 네모 흉배(남색 바탕 + 밝은 물결 무늬 — 학 그림 아님), 금색 과판 띠(소금대 대용), 사모(옆 날개), 망건, 목화, 나이 든 얼굴·콧수염·짧은 턱수염. 삼각형 21,937 / 6,711 / 2,007.
- **HanYungyeom**(한윤겸): 늙고 마른 몸, 흰 머리·긴 흰 수염, 소박한 상아색 도포 + 흰 동정 교차깃, 검은 세조대(가슴 높이), 흑립 + 갓끈, 망건, 낮은 신. 삼각형 23,503 / 7,162 / 1,968.
- **HayashiSoemon**(지주 하야시): 갈색 고소데(교차깃, 흰 속깃) + 회청색 하카마(큰 주름) + 허리띠, 사카야키(이마~정수리 앞을 민 머리) + 촌마게, 콧수염, 흰 다비. 삼각형 20,112 / 6,149 / 1,737.
- **KuroseMasatoki**(영주 구로세 — 가미시모): 남색 가타기누(어깨 밖으로 뻗은 빳빳한 판, 가슴 둘·등 하나 흰 가문 문장 — 문장 모양은 임의) + 같은 색 하카마, 밝은 회갈색 고소데, 사카야키 + 촌마게. 삼각형 21,697 / 6,623 / 1,998.
- **KuroseMasatoki_Armor**(구로세 출전용): 같은 얼굴. 검은 옻칠 + 붉은 끈 갑주(도·어깨끈·구사즈리 6장·소데), 투구(사발 + 시코로 4단 + 후키가에시 + 차양 + 금색 구와가타), 정강이받이, 짙은 남색 속옷·짧은 하카마, 흰 다비. 손 소품 없음. 삼각형 23,791 / 7,248 / 2,173. "어린 성주" 몸은 만들지 않음(§9).
- **JpRetainer**(배신한 가신): 짙은 녹색 고소데 + 회갈색 하카마, 사카야키 + 촌마게. 삼각형 19,978 / 6,102 / 1,736.
- **WokouSoldier**(왜구 병사): 남색 짧은 고소데(무릎 위), 갈색 옻칠 + 남색 끈 하라마키(몸통 갑옷·구사즈리 5장·어깨끈), 흰 머리띠(하치마키), 사카야키 + 촌마게, 짙은 각반 다리, 짚신 색 신. 삼각형 20,069 / 6,135 / 1,745.
- **PalaceGuard**(궁궐 호위): 남색 철릭 + 붉은 주립(갓) + 붉은 실띠. 삼각형 23,379 / 7,122 / 1,947.
- **RebelSoldier**(반란파): 짙은 녹색 철릭 + 검은 전립(꼭대기 붉은 술 공) + 갈색 실띠, 턱수염. 삼각형 23,525 / 7,165 / 1,949.
- **Eunuch**(내관): 청록 단령(둥근 깃), 사모, 검은 띠 + 짙은 과판, 수염 없음. 삼각형 21,238 / 6,502 / 1,944.
- **GwanaClerk**(관아 하급 관리): 회청색 철릭 + 흑립 + 검은 실띠, 콧수염. 삼각형 23,429 / 7,141 / 1,948.
- **MaskedMan**(정체 모를 조선인): 짙은 회색 철릭, 삿갓(코 높이까지 내려와 얼굴을 가림), 검은 복면(코~턱), 갓·수염 없음. 삼각형 23,249 / 7,088 / 1,977.
- **CourtLady**(궁녀): 여자 몸(키 약 1.55 m). 연두 저고리(자주 깃·고름, 흰 동정) + 남색 치마(가슴 아래부터 바닥까지), 쪽머리 + 비녀, 자줏빛 신. 삼각형 19,985 / 6,105 / 1,733.
- **JpRegent**(섭정 가로 — 어린 성주 선택지용): 늙은 몸, 회색 사카야키 + 촌마게·수염, 갈색 가타기누(문장 셋) + 같은 색 하카마, 밝은 회색 고소데. 삼각형 22,174 / 6,765 / 2,018.
- **HongDaebi**(홍 대비 — 연출에 따라 바꿀 수 있는 기본안): 나이 든 여자 몸(키 약 1.52 m). 짙은 청록 당의(허벅지까지, 자주 깃·고름, 흰 동정, 가슴·등·양 어깨 금색 둥근 보 — 무늬는 단순 물결) + 남색 치마(아래쪽 금색 스란단 띠), 쪽머리 + 금색 비녀. 대례복(적의 등)·가체는 아님. 삼각형 20,177 / 6,165 / 1,735.
- 일반 인물 폴더 이름(JpRetainer·WokouSoldier·PalaceGuard·RebelSoldier·Eunuch·GwanaClerk·MaskedMan·CourtLady·JpRegent)은 Claude 가 정한 임시 이름(바꿔도 됨).
- 위 모델 모두: 아바타 valid·human, 위팔 (−1, 0, 0) = T, `Setup`·`MakePrefab` 로 프리팹 변형까지 만듦(2026-10-09 퀘스트 배치에 씀 — §4 퀘스트 배치, 싸우는 병사는 `SoldierPrefabTool.BuildQuestSoldiers`).
- 확인(편집 모드, 2026-10-09): 각 모델 `Capture` 그림(`Captures/npc/<이름>_lods.png`) — 기본 T 자세와 걷기 샘플에서 세 LOD 모두 변형됨. 넓은 소매는 손 뼈 웨이트를 아래팔로 옮겨(손목 따라 소매 끝이 날개처럼 튀던 것) 다시 내보냄. LOD1·LOD2 에 작은 흰·검은 점이 보이는 곳이 있음(15 m·40 m 밖 단계). 실제 Play·가까이 모습은 사용자 확인 필요.

### 무기 (`06.Character/Weapons/`, 2026-10-04 — Claude 가 만든 저삼각형 모델)
- `Matchlock/Matchlock.fbx` 조총(다네가시마 모양 화승총) 484 삼각형, 길이 1.32 m, 원점 = 오른손 손목(총대 잘록한 곳), 총구 +Z, 위 +Y. 팔각 총신·나무 총대·짧은 개머리·놋쇠 화문 판·용두(화승 집게)·화명·방아쇠·가늠쇠·꽂을대·놋쇠 못. 재질 `Materials/M_Matchlock_Iron/Wood/Brass/Cord`(HDRP/Lit 단색 — 텍스처 없음).
- `Bow/Bow.fbx` = `Bow`(각궁 모양 활, 324 삼각형, 높이 1.22 m, 원점 = 줌통, 쏘는 쪽 +Z, 시위 쪽 −Z, 위 +Y, 휘지 않는 통짜 — 활대는 당겨도 그대로) + `Arrow`(58 삼각형, 0.856 m, 원점 = 오늬, +Z 로 날아감) + `BowStringSegment`(16 삼각형, 로컬 +Y 로 길이 1 m 토막). 재질 `Materials/M_Bow_Limb/Grip/Ear/String`·`M_Arrow_Shaft/Head/Fletch`(깃은 양면).

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
| 폭풍우 때 육조거리 남쪽 길 위로 물이 뚫고 올라옴 | 호수를 판 뒤 해안 마스크를 다시 굽지 않았고(마스크 10:42, 터레인 16:12), 너비 약 200 m 호수는 육지 거리만으로는 가운데 너울이 0.55 배(±10 m) 남음 — 길은 약 10 m | 다시 굽고 호수를 `calmZones` 로 너울 0 |
| 하차해 걸으면 비가 배에만 내림 | 비 위치가 `rainFollowTarget`(씬 값 panok)에 고정 | 하차 중엔 `OnFootRoot` 를 따라감(`WeatherSystem.CurrentRainTarget`) |
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
| 화면 밖에서 쓰러진 선원이 선 채로 굳음 | Animator CullUpdateTransforms 라 안 보이는 동안 쓰러지는 뼈가 안 움직였는데 2.6 s 뒤 Animator 를 꺼 버림 | 쓰러지는 동안만 `AlwaysAnimate`, 되살릴 때 원래대로 |
| 조총 연기가 검은 네모로 보임 | Camelot 연기 재질(HDRP/Unlit, 8×8 시트)은 정점 색을 안 받고 섞기가 One/Zero + 알파 잘라내기 | Hovl `HDRP_Blend_CG`(정점 색·투명 섞기) + 한 장짜리 `Smoke20` 으로 새 재질 |
| 칼날이 손에서 뒤로 뻗음 | RightHand 로컬 +Z 가 몸 뒤쪽 | 소켓 회전으로 맞춤 |
| 마을 사람이 걸을 때 어깨가 움츠러들어 보임 | Quaternius 클립을 이 뼈대에 틀면 `Shoulder Front-Back` 근육이 1 을 넘어(0.9~1.7) 쇄골이 앞으로 말림 | 아바타 어깨 Front-Back 최대 +3°, 위팔 Down-Up 최소 −52° |
| Fab 클립을 씌우면 몸 비틀기가 사라짐 | 루트 회전을 포즈에 안 구움 + OnAnimatorMove 는 위치만 적용 | lockRootRotation·높이 굽기, XZ 만 루트 모션 |
| 루트 모션 사망 때 두 캐릭터가 겹침 | 거리 제한을 Attack 에만 | 모든 루트 모션 상태에 적용 |
| 콤보 3타 입력 무시 | 다음 타 입력을 늦게 받음 | 입력 버퍼(windup × 0.5 부터) |
| 치마 앞자락이 다리 뒤에 낌 | 빠른 동작에서 다리 콜라이더가 자락을 지나쳐 몸 쪽에서 밀어냄, maxDistance 가 전 구간 같아 뿌리 따라가기가 안 먹음 | maxDistance 깊이 곡선 + clearance + untrap (2026-10-05 MeshCloth 로 바꾸며 untrap 끔) |
| 다리가 치마를 뚫고 나오고 엉덩이에서 옷이 끊겨 보임 | BoneCloth 8가닥 사이(밑단 약 30 cm)는 충돌이 없고, 가닥 위 1/3 을 거의 묶어 둠 | 치마 메시 자체를 MeshCloth 로(대리 메시 571 정점, Edge 충돌 + 엉덩이·발 충돌체) |
| 대기에서 다리가 치마 앞으로 나옴 | 치마 뿌리 본은 골반만 따라감 | `SkirtLegFollow` |
| 스프링 치마 값을 바꿔도 날림이 그대로 | 날림의 주원인은 관성이 아니라 다리 콜라이더 | Magica 로 교체 |
| 하차 후 모델 Animator 에 Speed 가 안 감 | OnFootRoot 를 켠 뒤 외형을 옮김(OnEnable 에서 Animator 탐색) | 옮긴 뒤 켜기 + Update 재탐색 |
| 계단에서 발이 디딤판을 뚫음 | 몸(CharacterController)은 디딤판 밑 터레인 경사를 걷고 발 IK 없음 | `FootIK` + 디딤판 StepSurface 레이어(몸은 통과) |
| 다시 탔을 때 카메라가 기울어 물속을 비춤 | PlayerOrbitCamera 가 CameraPivot 자식인 Main Camera 의 월드 자세를 덮어씀 | 하차 때 로컬 자세 저장, 승선 때 복구 |
| 계단에서 발소리가 1~2 초씩 빠짐 | 레이로 잰 발 높이는 발 아래 디딤판이 바뀌어 '들림'이 안 잡힘 | FootIK 의 애니메이션(IK 전) 발 높이로 판정 |
| 표정 키를 켜면 얼굴 음영이 전부 바뀜 | Blend Shape Normals = Calculate | None + `BlinkBlendShapeNormals` |
| 나룻배를 타면 포니테일이 뒤로 뻗음 | `SpringBoneSystem` 은 캐릭터가 움직인 만큼의 `worldInfluence`(0.35)를 관성으로 받는데, 배에 태운 채 배를 옮겨 5 m/s 바람을 맞는 것처럼 됨 + 태울 때 순간이동 | 타는 동안 worldInfluence 0, 태우고 내릴 때 `ResetTails()` |
| 블렌드셰이프 법선 변화가 대부분 0 | `Vector3.normalized` 는 길이 1e-5 미만이면 0 | 직접 길이로 나눔 |
| 편집 모드에서 SetBlendShapeWeight 가 캡처에 안 나옴 | 같은 호출 안 렌더는 스키닝 미갱신 | Play 에서, 다음 호출에 렌더 |
| 병사 둘에게 둘러싸인 시험에서 체력 5000 으로 올려도 플레이어가 바로 쓰러짐(2026-10-06) | 체간이 차서 무너진(Broken) 동안 맞으면 결정타 = 즉사(체력과 상관없음) | 시험 땐 maxPosture 도 올림(게임 규칙은 그대로) |
| 칼 뽑기 클립을 처음 구우니 오차가 수천 m | HumanPoseHandler 로 몸 위치를 읽고 쓸 때 복제 캐릭터 루트가 y 5000 에 있으면 bodyPosition 이 두 번 더해짐 | 굽기용 복제 캐릭터를 원점에 둠 |
| 오른손이 허리에 꽂힌 칼 손잡이에 닿지 않음(IK 오차 4–15 cm, 손목 머슬 한계 초과) | 해신 팔이 짧음(위팔 0.167 + 아래팔 0.219 + 손→소켓 0.091 = 0.477 m), 목표 보정 반복으로는 기하 한계라 안 줄어듦 | 뽑고 넣는 동안 허리 칼집을 돌려 손잡이를 앞으로(`WeaponHolder.turnSheath`, 각도는 `SwordDrawClipTool.OptimizeTurn` 격자 탐색) |
| 편집 모드에서 클립을 틀어 렌더하면 팔이 안 움직이고 옷이 엉뚱하게 보임 | SampleAnimation 스키닝이 같은 호출 안에서 갱신 안 됨 + 복제한 캐릭터의 옷 SMR 이 원본 뼈에 묶임 | 확인 렌더는 Play 에서 실제 캐릭터를 Time.timeScale 0.03–0.1 로 늦춰 캡처 |

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
| Python `tarfile` 스트림 모드(`r\|gz`)로 .unitypackage 를 읽으면 첫 파일부터 `invalid compressed data` | 원인 확인 필요 — 이 패키지 gzip 헤더에 FEXTRA(Unity 메타 JSON, 372 바이트)가 있음 | gzip 헤더를 건너뛰고 zlib(raw deflate)로 풀어 `tarfile` `r\|` 로 읽음(1.9 GB 28 초) |
| 장치로 보낸 PNG 의 md5 가 원본과 다름 | 전송 과정에서 PNG 에 `caBX` 덩어리(5,758 바이트)가 붙음 — 픽셀(IHDR·IDAT)은 같음 | PNG 는 IHDR·IDAT 만 해시해 비교. JPG 도 앞에 APP11(`FF EB`) 덩어리가 붙어 약 5.8 KB 커짐(2026-10-04 관찰) |
| 애니메이션만 든 FBX(뼈대만)를 Humanoid·다른 아바타 복사로 가져오면 클립이 안 생김(2026-10-04) | 뿌리 노드가 하나라 Unity 가 뼈대 노드를 지워 경로가 `Hips/…` 로 바뀜 — 모델 FBX 는 `JPSoldier/Hips/…` | 가져오기 `preserveHierarchy = true` |
| 광화문 궁장 기와를 반복 부품으로 만들려니 기와줄 한 칸 옮겨 겹쳐도 정점이 약 70 % 만 맞음 | 기와줄(좁은 줄 간격 0.26816 m, ±2 mm)은 고르지만 처마 밑 서까래 끝 조각이 불규칙(0.45~1.35 m 간격), 용마루·처마 판은 긴 한 덩어리 | 불규칙 조각을 가르지 않는 줄 가운데(c_1·c_60)에서 평면으로 자르고, 용마루 긴 면은 u 를 X 에 비례하게 다시 매김 |
| 광화문 문 바닥을 궁장 기단 바닥(10.59)으로 봄 | 문루 기단 윗면·홍예 바닥 판석은 11.144, 데모 바닥도 날개 옆 11.13~11.18 — 궁장 기단은 원래 약 0.55 m 묻힘 | 땅을 11.08(판석 6 cm 아래)로 돋움 |
| 영추문 문짝 판 두 짝 중 하나(`SM_YCM_Door02A`)만 검게 보임(처음엔 거울 배치 문짝을 의심 — 법선·감김·재질 설정은 두 짝이 같았음) | 판 그림 자체가 검었음(널판 평균 RGB 약 46~57 vs 옆 짝 104). 데모 렌더러 재질이 서브메시보다 하나 많아(Metal01·WoodTile·Metal01 + Wood01) Unity 는 남는 Wood01 을 마지막 서브메시(널판)에 한 번 더 그려 붉게 보이는데, `PalaceBinExport` 는 서브메시 수만큼만 재질을 적어 널판이 Metal01 로 구워짐 | 같은 메시(정점·UV·삼각형 같음)인 건춘문 `SM_GCM_Door02A`(Metal01·WoodTile·Wood01) 판 그림 `GCM_137` 을 `YCM_145_SM_YCM_Door02A_card.png` 에 복사(Unity·`Source\Yeongchumun\Cards`·`LOD\Cards`). 남는 슬롯 재질이 다른 곳(검사): 영추문 문짝 2 · 건춘문 `SM_GCM_DoorB` 4개(둘째 서브메시 Wood01 위 WoodTile — 줄인 것은 Wood01), 흥례문·신무문 0 |
| 영추문 문 앞에서도 100 m 넘게 떨어진 건물까지 가까운 모델로 그려짐(LOD0 1,225,932 전부) | 부분을 건물 코드(YCM·SMJC)로 묶어 문과 문 남쪽 건물 3채가 같은 LODGroup(크기 155~163 m, 기준점이 문에서 남쪽 55~62 m) — LOD 거리는 기준점에서 잼 | 자리(데모 x 구간)별로 부분을 나누고 세 채는 약 70 m. 구운 먼 LOD 는 코드별로 합쳐져 있어 무게중심으로 잘라 씀(`COPYFARBOX`) — 문 안쪽에서 765,423 |
| 동궁 담장 9개가 임포스터 껍데기에서 빠짐("no roof") | 담장(높이 1.8~2.4 m)은 "땅 + 2.2 m 위 윗면 = 지붕" 에 안 걸리고, 정점 0.5 백분위로 잡은 땅 높이가 기와 머리(정점 대부분)에 걸림 | 높이 3 m 미만이거나 지붕이 없으면 담장 방식(땅 = 최저점, 높이 55 % 위 = 지붕) |
| 임포스터 계단이 기단 옆면에 세로 줄무늬로 늘어져 구워짐 | 기단을 평평한 판(덩어리 중앙값 높이)으로 만들어 계단 자리가 수직면 | 기단 윗면 = 위→아래 광선 높이(대표 높이 위로 솟은 주초 등은 자름) — 계단이 비탈 판에 그려짐 |
| 한 오브젝트에 기단 높이가 다른 건물(교태전 본채 1.4 m·행각 0.5 m)이 있으면 몸통이 기단 둘레까지 커짐 | 벽 단면 높이를 오브젝트 전체 바닥(중앙값) 하나로 정함 | 칸마다 바닥 높이(0.25 m 칸 7×7 중앙값)에서 0.4~1.76 m 만 벽으로 |
| 교태전 행각 협문 자리가 뚫려 하늘이 보임 | 통로 한쪽 벽이 옆 오브젝트(WGH) 것이라 한 오브젝트 단면으로는 닫히지 않음 | 이웃 오브젝트 벽(경계 + 3 m)까지 넣어 닫힌 곳 중 내 지붕 아래 안쪽만 채움(이웃 벽 칸은 이웃 껍데기 몫) |
| 동궁 그림 페이지 2장이 33 % 만 참 | 선반 방식 패킹이 키 큰 선반의 남는 폭을 못 씀 | 길로틴 빈칸 채우기(짧은 변 남김 최소) — 같은 페이지 수에서 밀도 약 1.3 배 |
| 벽·기단 옆면 그림 칸이 땅속에 많이 쓰임(교태전 기단 옆면 광선 맞음 53 %) | 판 밑바닥을 오브젝트 최저점(땅속 기초)까지 내림 | 판 밑바닥 = 땅 − 0.6 m |
| 빈터 묶음 하나(DG_E_11)만 0.39 m 들려 놓임 | 발자국 안 터레인 최고점을 땅으로 씀 — 발자국 가장자리에 사용자가 만든 작은 둔덕(최고 11.14)이 걸림 | 발자국 안 터레인 가운데값(최저 + 0.5 m 를 넘지 않게 — 기단이 땅 아래 0.6 m 까지 있어 뜨지 않음) |
| FBX 로 가져온 메시의 서브메시 순서가 내보낼 때 재질 칸 순서와 다름(간의대: 석축·전돌·계단돌·난간) | Unity 는 FBX 면에서 재질이 처음 나온 순서로 서브메시를 만듦(관찰) | 내보내기 전에 면을 재질 칸 순서로 정렬 |
| FBX 를 다시 내보내 가져온 뒤 취로정 연못 MeshCollider 에 레이가 안 맞음(충돌체 경계가 홀더 원점 한 점) | 확인 필요(다시 가져온 메시로 충돌체가 다시 만들어지지 않은 것으로 보임) | `sharedMesh` 를 비웠다 다시 넣음 + FBX 읽기 가능(isReadable) 켬 |
| 궁 단지 앞쪽(정면 행각 + 문 + 양옆 행각 앞머리)을 떼어 육조거리 길가에 놓자 줄 끝·관아 사이 모서리가 깨져 보임(옆 행각 앞머리가 열려 서까래가 드러남) | 단지에서 뗀 자리는 원래 다른 행각에 붙던 곳이라 열려 있음 | 멀쩡한 두 칸 조각과 문 조각을 일자로 이어 붙이고(사용자 제안), 모서리는 두 칸 조각 둘을 45° 로 잘라 맞댄 ㄱ자로 새로 만듦(§4 육조거리) |
| 행각을 한 칸씩 이어 붙이면 이음매마다 기와가 어긋남(최대 18 cm) | 칸 폭(2.89~3.17 m)이 기와 줄 간격(0.3565 m)의 정수배가 아님 | 기와 17줄이 맞는 두 칸(6.075 m, 어긋남 1.5 cm)을 한 조각으로 |
| 관아 안쪽을 행랑 뒤에서 자르자 강녕전·교태전 틀의 전각 앞부분이 잘림 | 자르는 면이 전각에 걸림 | 전각(`yj_build.py` 의 `HALLS`)은 자르지 않고 통째로 |
| 관아 옆 행각(임포스터)과 행랑 사이에 틈 | 옆 행각을 자르는 면이 행랑 뒤 처마(정면 + 9.88 m)보다 뒤였음 | 자르는 면 = 틀 기준점(정면 처마 끝) + 9.9 m(`ZREST`) |
| 육조거리 사정문 오른쪽 칸 문짝이 없어 안이 들여다보임(행랑 조각·먼 임포스터 그림 모두) | `load_cx.load_complex` 가 오브젝트 이름을 dict 키로 써서 같은 이름 인스턴스가 덮어써짐 — 사정전 `SM_SJM_DoorB_01`·`B_02` 각 2 · `DoorA_01` 4, 강녕전 `SM_HOM_Door01A/02A/03A`·`SM_GNJ_HG_Door02A_024` 8·`SM_GNJ_HG_Wall04A_001` 2(교태전·동궁 내보내기는 겹치는 이름 없음 — 확인) | 겹치는 이름 뒤에 `__2`, `__3` … 을 붙임. YJ_A·YJ_B 임포스터를 다시 구움 — 문짝이 껍데기 모양에도 들어가 껍데기·페이지 배치가 조금 바뀌어(ROW_GATE 2,336 → 2,452, 강녕전 행각 17,886 → 17,920) `Setup`·모서리·둘레 껍데기·관아 안쪽을 다시 만듦 |
| 흥례문 LOD 를 60 m 로 당기자 바뀌는 순간 지붕이 확 어두워짐 | LOD1 지붕판이 검은 재질(색 0.035). 비스듬히 보는 지붕은 하늘 반사가 밝기를 크게 좌우해, 색만 조금 올려서는 거의 안 바뀜 | 기와 재질에 맞춘 판 재질(색 0.23 — 기와 텍스처 평균 선형 약 0.20, 매끄러움 0.54 = 기와와 같음) |
| 일본 집 LOD1 지붕 판(위·아래 두 장)을 구운 그림이 깨알 같은 검은 점투성이 | 아래 장을 위 장과 같은 자리에 뒤집어 겹쳐 둠 — 굽기·확인 광선은 앞뒷면을 가리지 않아 두 장 중 아무 쪽이나 맞음 | 아래 장을 1 cm 내림 |
| 일본성 석축을 줄여도 삼각형이 거의 안 줄어듦(4 cm 에 143,364 → 112,262) | 석축 돌이 서로 떨어진 3,477 덩어리(덩어리마다 32 삼각형) — 줄이기는 덩어리를 합치지 못함 | 뒤에 있던 280 삼각형 둘레 껍데기에 돌 그림을 구워 LOD1·LOD2 로 씀 |
| 근정문 판 문짝이 서로 맞물리지 않고 남쪽에서 몇 짝은 뒤 그림이 보임 | 판 두 장이 0.48 m 떨어져 사이가 비었고, 거울 배치(자식 스케일 z −1) 문짝은 삼각형 감김이 뒤집혀 앞면이 컬링됨 | 경첩 공간에서 새로 만든 상자(감김을 면 법선으로 맞춤), 자식 스케일 1 |
| 경회루 계단을 오르다 중간에서 멈춤 | 1층 천장 판·2층 바닥이 계단 위를 덮음(머리 위 여유 2.0 m < 키 1.75 + 턱 0.4) + 천장을 자른 뒤에도 2층 계단 구멍 남쪽 끝 난간이 계단 위를 가로지름. 한 메시 콜라이더라 아래로 쏜 `RaycastAll` 은 한 번만 맞음, 아래를 보는 면은 위에서 쏜 광선에 안 잡힘 | 계단 위를 트고 난간을 새 구멍 끝으로 옮김. 겹친 면은 맞은 점 바로 아래에서 다시 쏘며 찾음 |
| 천수 안 2~4층 계단에 못 올라감(CharacterController 시험) | 계단 발치 앞에 구멍 난간(1 m 높이 가로대)이 가로질러 있었음 — 계단이 가파른 것(57°)은 원인이 아니었음(1층 계단은 그대로 올라감) | 난간을 지우고 계단을 늘림 |
| 계단을 늘리자 오히려 못 올라감 | CharacterController 는 턱을 오를 때 먼저 stepOffset(0.4 m) 만큼 위로 올리는데, 머리 위 천장(구멍 가장자리 앞)이 막음 — 계단이 완만할수록 구멍 앞에서 더 높아짐 | 위층 구멍을 계단 쪽으로 0.7 m 넓힘(조건: 구멍 끝 + 0.32 m 에서 계단 높이 + 1.75 + 0.4 < 천장) |
| FBX 를 다시 가져온 뒤 편집 모드에서 캡슐이 바닥을 뚫고 떨어짐 | MeshCollider 가 예전 메시로 남아 있었던 것으로 보임(확인 필요) | 성의 MeshCollider 를 껐다 켬 |
| 일본성 천수가 멀리서 1층이 비어 보임(사용자) | LOD2 지붕 모양 상자가 5겹 지붕 천수의 1층 처마·벽을 못 만듦(껍데기 바닥 추정이 1층 위) | LOD2 = LOD1 을 더 줄인 메시(`jc_lod2.py`) |

### 퀘스트·현상금
| 증상 | 원인 | 해결 |
|---|---|---|
| P-2 끝에 관군 현상금을 0 으로 했는데 바로 17 로 올라감 | `ShipHealth.Die()` 에서 `OnAnyDied` 가 `ReputationSystem.ReportPlayerAttack`(격침 현상금)보다 먼저 불림 → 퀘스트가 그 프레임에 끝나 0 으로 만든 뒤 격침 현상금이 더해짐 | 격침 목표가 찬 프레임엔 표시만 하고 다음 Update 에서 목표 완료(`_completeNext`) — Play 로 관군 0.0 / 0단계 확인 |
| 빈 현상금 아이콘이 바다 위에서 안 보임 | 빈 칸 색이 어두운 반투명 | `emptyColor` (0.62, 0.64, 0.68, 0.55) — 씬 값도 같게 |
| 퀘스트 추적 패널이 화면 가운데쯤(y −340) | 미니맵(왼쪽 위)을 피하려 내림 | 오른쪽 위 (−24, −24) |
| 왜구섬 선착장 경비가 허리까지 땅에 묻혀 보임(2026-10-09 캡처) | 다른 세션이 선착장 땅 높이를 맞추며(`DockRampTool`, 2026-10-07 21:39) 땅이 2.64 m 로 올라갔는데 경비는 1.8 m, NavMesh 도 예전 높이(2.0) | 경비 y 2.64, `NavMesh_WokouDen` 다시 구움(GUID 유지) |
| 같은 퀘스트 배를 GameObject.Find("Quest_Escort") 로 찾았더니 이미 끝난 호위 배가 잡힘 | 끝난 호위 배도 남은 배 목록에 같은 이름으로 남음 | 끝나면 이름을 `Quest_Escort_Done` 으로 |
| 밤으로 돌리자 달이 67,000 lux 로 낮처럼 밝음(2026-10-09) | `WeatherSystem` 이 해를 "첫 방향광"으로 찾아 새로 만든 달을 잡음 | 씬 값 `sun` 을 `Directional Light` 로 박아 둠 |
| 밤에 동굴 바위가 하얗게 빛남 | 낮 해로 구운 APV·동굴 리플렉션 프로브가 밤 노출(EV 1~2.5)에서 그대로 보임 | 밤 간접 확산·반사 프로브 배율 0.002 |

### 나룻배·궁 그림
| 증상 | 원인 | 해결 |
|---|---|---|
| HDRP AOV 로 찍은 알파(`MaterialSharedProperty.Alpha`)가 배경까지 1 | (측정, HDRP 17.3 — 회색 판 시험 `ProbeAOV`) | 기본색이 0 이 아니거나 노멀이 배경 노멀(기본색 0 인 픽셀에서 가장 흔한 값 — 이번 그림 (0, 0, 0), 시험 판 (0.5, 0.992, 0.5))과 다른 픽셀을 궁으로 |
| 4096×173 그림이 Unity 에서 4096×128 로 들어옴 | TextureImporter `npotScale` 기본값 ToNearest | `npotScale = None`, 크기를 4 의 배수로(BC7) |
| 광화문 기단 평평한 면에 삼각형 모양 밝기 차이(면이 겹친 듯), 돌 단에 둥근 띠 그림자 | 외부에서 줄인 메시가 원래 다른 면과 공유하던 정점 법선을 그대로 가져옴(모서리 38 % 가 면 법선과 40° 넘게 어긋남), 탄젠트도 그 정점에서 비뚤어짐 | 같은 자리 정점끼리 50° 안 면만 평균하는 자동 스무딩으로 법선을 다시 계산하고 탄젠트 다시 계산(`MeshNormalFixTool`) |
| 남쪽 선착장 판자 끝에 0.5 m 턱(걷는 캐릭터가 막힐 수 있음) | 경사로 끝(z 3165) 다음 헤이트맵 점(z 3166.4)이 원래 물밑이라 판자 바로 앞 터레인이 0.5 로 꺼짐(칸 2.44 m) | 판자 밑 헤이트맵 점을 판자보다 조금 낮게(0.93) 올림 |
| `NpcModelSetupTool.Setup` 이 `Creating asset at path …/Materials/M_<이름>_Skin.mat failed` 로 멈춤(2026-10-09 YiDam_King) | 장치에 새 폴더를 복사한 직후라 AssetDatabase 가 그 폴더를 아직 모름(`IsValidFolder` false) — `ImportAsset(폴더, ImportRecursive)` 만으로는 안 들어옴 | `AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport)` 먼저 부르고 `Setup` |
| Unity 가 꺼짐(AssetImportWorker "Could not allocate memory: System out of memory", 2026-10-05) | 나룻배·노인 FBX·텍스처를 넣고 새로 고침하던 중 — Blender 를 같이 켜 둔 상태(원인 확인 필요) | Unity 를 다시 켬 — 가져온 에셋은 정상이었음 |

## 7. Unity MCP 로 작업할 때

- 클라우드 작업 공간 네트워크(2026-10-06 확인): kenney.nl·bigsoundbank.com·opengameart.org·freesound.org·archive.org·wikimedia 는 프록시가 403 으로 막음, GitHub API 검색도 막힘, PyPI 의 `soundfile` 은 설치 안 됨 → 음원은 ffmpeg + numpy/scipy 로 만듦. 사용자 PC 쪽으로 받는 것은 하지 않음.
- 장치로 보낸 `.wav` 끝에 `C2PA` 청크(약 5.7 KB, 콘텐츠 출처 정보)가 붙어 md5 가 클라우드 파일과 달라짐(2026-10-06 확인 — RIFF 크기도 그만큼 늘어 있음, 소리 데이터는 같음) → wav 는 md5 대신 크기 차·클립 길이로 확인.
- `ScreenCapture.CaptureScreenshot` 을 execute_code 에서 부르면 파일이 안 생겼음(2026-10-06) → OnGUI 가 들어간 Game 뷰는 `manage_camera` `screenshot`(카메라 지정 없이)으로 찍힘.
- `execute_code` 는 CodeDom(C# 6): `UnityEngine.Object.` 로 한정, 문자열 보간·`out var` 불가. `LightUnit` 은 `UnityEngine.Rendering.LightUnit`, 세기는 `HDAdditionalLightData.SetIntensity(v, LightUnit.X)`. Volume 파라미터 값은 리플렉션으로 `value` 읽기. `HDMaterial` 접근 불가 → `SetFloat` + `EnableKeyword`.
- **같은 코드가 여러 번 실행될 수 있음** → 만들기 코드는 이름으로 이미 있는지 확인(멱등). 응답 대기 약 30 s 를 넘기면 클라이언트가 같은 호출을 다시 보냄 → 오래 걸리는 일은 시간 제한을 두고 나눠 호출(예: `ImportBakedStep`).
- MCP 에서 건 `EditorApplication.delayCall` 이 실행되지 않았음(원인 확인 필요). `File.Delete`·단수형 `AssetDatabase.MoveAssetToTrash`·`while(true)` 는 안전 검사에 막힘 → 복수형 `MoveAssetsToTrash`.
- 한글을 보내면 일부 음절이 깨지는 일이 있었음(원인 확인 필요) → 저장 후 U+FFFD·문구를 확인.
- 스크립트 폴더 전체를 한 번에 읽으면 출력 한도 초과 — 나눠 읽기.
- `Unity.Profiling.ProfilerCategory.Any` 는 없음(컴파일 오류 CS0117, 2026-10-04) — 표시(마커)는 카테고리 이름을 맞춰야 잡힘(`Scripts:BehaviourUpdate`·`Render:CullScriptable` 은 잡혔고, `Internal:EditorLoop`·`Render:RenderPipelineManager.DoRenderLoop_Internal` 은 0 — 카테고리가 다른 것으로 보임, 확인 필요).
- 리플렉션으로 `HDShaderUtils.ResetMaterialKeywords` 를 `GetMethod(이름)` 으로 찾으면 오버로드가 여럿이라 `AmbiguousMatchException` → `GetMethods` 에서 인자가 `Material` 하나인 것을 골라 부름.
- 스크립트로 씬 오브젝트를 넣고 빼도(Undo 없이) 씬이 dirty 로 안 잡힘(2026-09-30 관찰) → 끝에 `EditorSceneManager.MarkSceneDirty`.
- 기기로 보내는 파일은 한 파일 20 MB 까지(한 번에 100 MB) — 4096 임포스터 페이지 PNG 는 12~16 MB 라 됨(넘으면 jpg 로, `PalaceImpostorTool` 이 .jpg 도 찾음).
- 장치 파일 쓰기(`device_commit_files`)는 연결 폴더 안만 됨 — 2026-09-30 세션은 `D:\Unity\Haesin`·`D:\Blender\Enviroment\Cave\CaveRock` 만 연결돼 `…\PalaceJoseon\` 에는 Unity `Captures` 로 보낸 뒤 장치 안에서 복사.
- 20 MB 넘는 파일은 gzip 으로 보내 Unity 에서 `GZipStream` 으로 풀었음(2026-09-30).
- `AssetDatabase.GetDependencies`(재귀)를 `GyeongBok` 전체 인스턴스의 프리팹에 걸면 MCP 시간 초과, 다음 호출도 한동안 시간 초과 — 필요한 프리팹만 넣을 것(2026-09-30).
- 장치에 쓴 파일은 md5 로 확인할 것 — 2026-09-30 같은 자리에 세 번째로 올린 스크립트 2개가 직전 내용으로 쓰였음(쓴 시각은 새것, 다시 commit 하니 맞음, 원인 확인 필요). 2026-10-05 에도 **같은 staged 경로**(`/mnt/user-data/outputs/scripts/X.cs`)의 파일을 고쳐 다시 commit 하니 직전 내용이 써짐 → 고친 파일은 새 경로(`…/scripts/v2/X.cs`)에 두고 commit 하면 맞음.
- NavMeshSurface 를 다시 굽되 에셋 GUID 를 지키려면: `surface.BuildNavMesh()`(동기, 새 NavMeshData 생김) → `RemoveData()` → `EditorUtility.CopySerialized(새 데이터, 옛 에셋)` → `navMeshData = 옛 에셋` → `AddData()` → 새 데이터 `DestroyImmediate` → `SaveAssetIfDirty`(2026-10-05, 0.4 s).
- 캡처: `manage_camera` 위치 지정 캡처엔 HDRP 물이 안 그려짐(예전 기록 — 2026-09-26 y 3000 캡처에서는 바다가 보였음, 확인 필요). Play 캡처는 Main Camera 를 옮긴 뒤 다음 호출에서(모션 블러), PlayerOrbitCamera 는 꺼 둘 것. 편집 모드 파티클은 `ps.Simulate(...)` 로 강제 시뮬. 편집 모드 `SampleAnimation` 직후 스키닝은 한 번 늦음. `manage_camera` 위치 지정 캡처는 `include_image=false` 여도 그림을 돌려줌(1920×1080 원본은 `Captures/…` 에도 저장). LOD 단계별 모습을 비교할 땐 `LODGroup.ForceLOD(n)` 로 고정하고 찍을 것(2026-10-01 자동 선택으로 찍은 캡처에서는 LOD1 재질을 바꿔도 변화가 안 보였음 — CrossFade 때문으로 보이나 확인 필요) — 끝나면 `ForceLOD(-1)`.
- 스킨 메시 LOD 를 편집 모드에서 `ForceLOD(1/2)` 로 띄우고 `SampleAnimation` 하면 카메라 `Render()` 를 여러 번 해도 LOD1·LOD2 는 T 자세로 그려졌음(LOD0 은 자세가 나옴, `BakeMesh` 결과는 셋 다 자세가 맞음 — 2026-10-04 일본 장군). 변형 확인은 `BakeMesh` 한 메시를 MeshRenderer 로 띄워 찍음. 원인은 확인 필요(보이지 않던 렌더러의 스키닝이 갱신 안 된 것으로 보임).
- 휴머노이드 클립을 여러 인스턴스에 샘플하면 루트 모션이 인스턴스 위치를 덮어씀 — 인스턴스마다 부모를 두고 부모로 띄움.
- 걷기 시험: 캐릭터 대신 같은 크기의 `CharacterController` 를 임시로 만들어 편집 모드에서 `Move` 를 1/60 s 씩 부르고(중력 포함), 경유점마다 높이를 기록 — 메시를 바꾼 직후엔 성 MeshCollider 를 껐다 켜고 `Physics.SyncTransforms()`(2026-10-04).
- 캡처를 앱에 이미지로 띄우지 않으려면(사용자 요청, 2026-10-01): 임시 카메라(또는 씬 뷰 카메라)를 RenderTexture 에 `Render()` → `Captures/…png` 로 저장 → 파일을 받아 작게 줄여 봄. 임시 카메라는 끝나면 지움.
- 에디터 자세 샘플링은 `AnimationMode.Start/SampleAnimationClip/Stop`(씬 dirty 안 됨) 또는 `AnimationClip.SampleAnimation`(PlayableGraph 는 휴머노이드 자세가 깨짐).
- 일시정지 상태로 Play 를 끄면 다음 Play 가 멈춘 채 시작 → 끄기 전 `isPaused = false`.
- Play 중 자유 카메라 캡처: Main Camera 는 `panok/CameraPivot` 자식이라 월드 위치를 넣어도 배가 출렁이면 함께 움직여 땅 밑·뒤집힌 화면이 찍혔음(2026-10-11) → `SetParent(null)` 로 떼고 찍음(Play 를 끄면 원래대로). `CameraFollow`·`PlayerOrbitCamera` 는 꺼 둘 것 — 메뉴(인벤토리 등)를 닫으면 `GameMenuPause` 가 다시 켬. 하선 직후 원본 의존성 전체 검사(`CollectDependencies`)는 에셋을 메모리에 올려 에디터가 수 분 멈춤 → `AssetDatabase.GetDependencies`(경로만)로.
- 자동 시험(`CombatAutoTest`)을 쓸 땐 PlayerCombatController 를 끈다(매 프레임 막기 입력을 덮어씀).

- **한글이 섞인 스크립트를 평문 패치로 보내면 해시가 어긋난 일**(2026-10-07, SoldierPrefabTool — 다른 파일은 맞음, 한글 깨짐으로 보임 확인 필요) → 파일 전체를 gzip + base64 로 보내 Unity 에서 `GZipStream` 으로 풀고 sha256 을 맞춰 본 뒤 `File.WriteAllBytes`(기존 BOM·CRLF 유지). 크기도 작아짐.
- `AssetDatabase.DeleteAsset` 이 MCP 안전 검사에 막힘(2026-10-07) → NavMeshData 를 다시 구울 땐 새 데이터를 기존 에셋에 `EditorUtility.CopySerialized`(GUID 유지).
- execute_code 결과가 크면(캡처 base64 등) 결과가 파일로 저장됨 → `jq -r '.data.result' | base64 -d` 로 꺼냄.
- Overlay 캔버스 UI 는 카메라 RT 렌더에 안 나옴 → 찍는 동안 루트 캔버스를 ScreenSpaceCamera(Camera.main)로 바꿔 RT 에 렌더하고 되돌림(2026-10-07). 이 방식으로 찍은 캡처에 배 체력바가 안 보였음(원인 확인 필요).
- Play 에 들어간 직후 execute_code 가 시간 초과 — 몇 초 기다렸다 다시 부르면 됨(2026-10-07).
- **장치 폴더 쓰기(2026-10-09)**: 이 세션에 `D:\Unity\Haesin` 폴더 접근을 받아(사용자 승인) 스크립트·CLAUDE.md 를 `device_stage_files`(읽기)·`device_commit_files`(쓰기)로 옮김 — 큰 파일을 execute_code 문자열로 보내지 않아도 됨. 쓰기 전에 장치 파일을 다시 받아 기준 사본과 같은지 확인하고, 원래 BOM·CRLF 를 그대로 둠. **같은 stagedPath 로 세 번째 커밋했을 때 "written" 이라 나왔는데 장치에는 직전 내용이 남아 있었음**(QuestManager, 2026-10-09 — 해시로 발견) → 커밋할 때마다 새 파일 이름(예 `outputs/r2/…`)으로 올리고, 쓴 뒤 Unity 에서 sha256 으로 확인.
- 다른 세션이 같은 시각에 CLAUDE.md 를 고칠 수 있음(2026-10-07 퀘스트·왜구섬 기록이 다른 세션 사본으로 덮여 사라졌다가 2026-10-09 다시 합침) → 쓰기 직전에 장치 파일을 다시 받아 그 위에 합칠 것.
- 결과가 크면 파일로 저장되는 점을 이용해, 장치 파일·캡처는 `gzip + base64` 로 돌려받고 결과 끝에 긴 채움 글자를 붙여 일부러 파일로 받음(대화에 그대로 찍히지 않게).

## 8. 확정된 설계 결정

- 현상금(2026-10-06 사용자 지시, 10-07 우호도 → 현상금): 한 진영과 적대(1단계 이상)가 되면 저절로 돌아오지 않음. 판옥선(플레이어)은 멈춘 채로도 선회. 배 기본 속도 = 예전 1.7 배, Space 부스트는 서서히 1.7 배(4 배 → 2 배 → 2026-10-07 1.7 배).
- 육상 병사(2026-10-06, 사용자 선택): 관군은 육조거리·광화문 경비(환도), 일본군은 성 + 마을 거리(조총 대신 칼), 일본 장수는 성 안에만. **현상금과 상관없이 먼저 공격할 때만 덤빔**(그 병사를 치거나 근처에서 마을 사람을 쓰러뜨리면). 걷다 쓰러지면 배에서 되살아남.
- 퀘스트 NPC 옷차림(사용자, 2026-10-08): 현감 조명구는 **16세기 융복(철릭) 차림, 손 소품 없음** — 사용자가 보낸 군복(전립·동달이·전복) 그림은 자료상 임진왜란 전후부터의 차림이라, 인종·명종대에 맞춘 융복을 사용자가 고름. 홍단령·사모 모델은 한양 조력자용. 별감 옷은 만들지 않음.
- 진영 3개, 적 판정은 `ShipFaction` 한 곳. 포탄은 `ownerFaction` 을 반드시 가짐.
- 배 이동은 전부 힘 기반, 축 분담(높이·기울기 = ShipBuoyancy, Y 회전·XZ = 이동 스크립트).
- `EnemyShipBase` / `NavyShipBase` / `MoveControl` 은 공용 베이스로 합치지 않음.
- 씬의 원본 배 오브젝트가 스포너 템플릿(프리팹 참조 아님).
- 날씨 프리셋은 `WeatherSystem` 인스펙터 인라인 리스트.
- 진영별 현상금 두 개(관군·일본, 2026-10-07 — 한쪽을 치면 다른 쪽이 내려감은 유지). 0단계면 두 진영 모두 플레이어를 공격하지 않음. 섬 근처(영해)는 그 섬 진영 배만, 육지 가까이는 선착장 배만.
- 선착장 배는 선착장 옆에 세우지 않고 **앞바다(뱃길 옆)에서 원을 그리며 돌고, 부딪히면 밀려남**(사용자 지시, 2026-10-05) — 하차 지점에서 60 m·뱃길에서 25 m 밖. **적대면 대포를 쏨**(사용자 지시, 2026-10-05 — 예전엔 반격 안 함), 쫓아가지는 않음.
- 마을 사람은 **플레이어 칼 한 번에 죽음**, 시체는 마을을 떠날 때까지 남음(사용자 지시 "죽일 수 있게", 2026-10-05 — 한 번에·시체 유지는 Claude 가 정함).
- 배 선원은 병사 모델(해적·조선 수군 = 활, 일본군 = 조총), 큐브 자리 그대로 전원 사수(사용자 선택). **선원 사격은 배가 아니라 선원을 쓰러뜨리고, 선원이 모두 쓰러진 배는 대포를 못 쏨**(사용자 지시, 2026-10-04). 선원 사격은 현상금을 움직이지 않음. **선원끼리 싸움은 두 배 가장자리 사이 30 m 안에서 시작(처음 10 m → AI 배끼리 거의 안 붙을 수 있어 30 m), 카메라에서 90 m 넘는 배의 선원 오브젝트는 끔**(사용자 지시·선택, 2026-10-05 — 화면에서 약 91 m 떨어진 판옥선을 보고 정함).
- **경복궁(육조거리 빼고)은 평소 끄고 마을 시점 그림 한 장** — 육조거리로 가는 나룻배를 탈 때만 켜고, 마을 쪽으로 돌아갈 때 끔. 나룻배는 E 3초, 약 40초, 갈 때·올 때 모두 배 측면 고정 카메라(사용자 지시·선택, 2026-10-05). 나룻터는 물 가까이(판자 물 위 약 1 m) 내리고 땅은 경사로로 낮춤(사용자 지시).
- 마을 사람은 마을마다 약 20명, 섬(마을 구역)에 가까이 가면 생기고 멀어지면 사라짐(사용자 지시·선택, 2026-10-04). 마을 안에서도 카메라에서 60 m 넘는 사람·나룻배는 오브젝트를 끔(사용자 지시, 2026-10-05). 육조거리에는 일반 마을 사람을 넣지 않음 — 관복 입은 양반을 따로 넣을 계획(사용자).
- 근접전은 세키로식(튕겨내기 + 체간 + 결정타), **배에서 내린 상태에서만**. 공중제비·구르기류 동작은 넣지 않음(치마 속 노출). 결정타 쌍 애니메이션은 쓰지 않음.
- 두 번째 콤보는 별도 키 없이 좌클릭 콤보에 자동으로 섞음. 강베기는 좌클릭 꾹. 칼은 자동으로 뽑고 넣음.
- 걷는 캐릭터 카메라는 배 카메라와 같은 조작(마우스만 움직이면 돎).
- 하차 중 배는 정박(`SetDocked`) — 조종 스크립트를 끄지 않음.
- 해신 치마는 **치마 메시 자체를 천으로(Magica MeshCloth)** — 뼈 가닥(BoneCloth)은 쓰지 않음(사용자 선택, 2026-10-05 "일이 많더라도 확실하게").
- 조선 큰섬의 궁(경복궁)은 **인종·명종 시대(1544~1567)** 기준 — 고종 중건(1865~) 이후 새로 생긴 건물(집옥재·건청궁·향원정·자경전 등)은 넣지 않음(사용자 지시). 근정전 일곽을 포함해 궁 전체 에셋을 먼저 들여온 뒤 최적화함.
- 경복궁: **광화문(문루·월대·해태)·광화문 부속단지·흥례문·근정전·경회루·강녕전·사정전은 단지 전체(행각·담장 포함) 원본** — 플레이어가 언제든 가는 곳(사용자 지시, 2026-09-26; 처음엔 근정전·경회루·강녕전 건물만이었음). 사정전 실내 소품은 넣지 않음. 나머지(교태전·동궁·건춘문·영추문·신무문)는 겉모습만(구운 것 — 이후 모두 단순화판으로 바뀜, §4). 멀리서는 모두 구운 것(LOD, 사용자 선택). **모든 궐문 문짝은 분리**(열 수 있게). 같은 텍스처·머티리얼은 `_Shared` 한 곳으로 합침.
- 경복궁 최적화(사용자 지시, 2026-09-27): 실내를 남기는 건 **근정전·경회루만**(나머지는 밖에서 안 보이는 것 지움). 모든 창은 불투명 창호지, **창살은 메시 대신 구운 판(카드)**. 지붕은 **먼 거리만 매끈한 껍데기**, 가까이는 원본 기와. **경회루 2층은 문 없이 트임**. 행각 끝에서 기와가 잘린 모양은 원본 데모 씬도 같아 그대로 둠(원복).
- 동궁은 내부 진입 없음. 안쪽을 지우고 판으로 가리는 방식은 벽이 사라져 사용자가 취소 — 건물별로 나눠 모양을 유지하는 만큼만 줄임(사용자 지시, 2026-09-29). 경회루·동궁 홀더는 켜 둔다(사용자 지시).
- 경회루 연못 담장·담장 문(3곳)은 넣지 않음 — 고종 대 건립이라(사용자 확인·지시, 2026-09-28). 연못 다리는 남김. Unity `Gyeonghoeru_Simplified` 에서도 뺌(2026-09-29).
- 광화문 해태상(1870)·월대(1866)는 인종·명종대에 없던 것이라 별도 오브젝트로 분리(사용자 지시: 없었다면 분리). **해태상·월대 모두 고증에 안 맞아 단순화판에서 뺌 — 광화문은 문루(양옆 궁장 포함)만 단순화**(사용자 지시, 2026-09-29). 궁 배치는 건물별 이름 오브젝트 + 부모 `GyeongBok`, 정확한 위치는 사용자가 맞춤. 진입 막기(콜라이더)는 다 만든 뒤 사용자가 함.
- 궁장(궁궐 담): 인종·명종대 궁성 둘레는 확인하지 못해 **현재 경복궁 지도처럼 광화문·건춘문·신무문·영추문을 잇는 사각형**(사용자 지시, 2026-09-30). KCISA 스플라인 빌더(화성 성곽, 1.9 GB, Built-in 재질)는 가져오지 않고(사용자가 가져오기 취소) **광화문 궁장을 반복 부품으로** 씀. 문 높이는 **영추문·신무문을 땅(10)으로 내리고 광화문 주변만 땅을 돋움**(사용자 선택).
- 건춘문·영추문·신무문: **가까이(부분 중심 약 200 m 안)는 원형 유지 감면 + 판, 멀리는 예전 구운 덩어리**(사용자 선택 "흥례문처럼", 2026-09-30 — 가까이서도 구운 것만 보인다는 지적으로 바꿈). 영추문 문 남쪽 건물 3채는 문과 따로 떼어 **건물 약 70 m 안에서만 가까운 모델**(사용자 선택, 2026-09-30).
- 궁 안은 **광화문에서 경복궁 안으로 들어가는 일직선 경로만 열어 둘 계획**(사용자, 2026-09-30) — 옆 구역·옆 단지는 길에서 멀리 보이기만 함(길 가운데 x ≈ −2745 에서 서쪽·동쪽 빈터 가장 가까운 곳 약 66 m, 단지 경계 상자로 잰 어림).
- 동궁은 사정전·강녕전 땅 높이(약 11.75)에 맞춰 올린 자리(홀더 y 2.8, 사용자) — **터레인을 동궁 땅에 맞춰 올림**(사용자 지시, 2026-09-30).
- 빈터 묶음은 **배치마다 크기(와 거울)를 다르게** 해 반복을 줄임(사용자 지시, 2026-09-30).
- 간의대는 **경회루 북쪽, 삼각형 최소**(사용자 지시, 2026-09-30) — 석축·계단은 메시, 돌난간은 그림 판.
- 간의대 기구는 **『세종실록』 세종 19년 4월 15일 기사 배치대로** — 대 위 간의, 그 남쪽 정방안, 대 서쪽 규표, 표 서쪽 소각에 혼의(동)·혼상(서)(사용자 지시, 2026-09-30). 기록에 없는 모양은 가정.
- 취로정 연못은 **향원지 모양(모서리 둥근 네모)을 따르되 교태전 북쪽 가운데에 52 × 48 m 로 줄이고, 북쪽 둑에 사방이 트인 1칸 정자**(사용자 선택, 2026-09-30).
- 육조거리(2026-09-30): 광화문 축(x −2745)에 곧은 남북 길(폭 34 m)과 동서 길, 길은 땅 + 0.3 m·흙 칠(사용자 선택 "길을 낮춤"), 관아는 길에서 약 110 m 까지(사용자 선택 — 그 뒤 민가는 사용자). 길가는 **사정전 남행각 두 칸 조각·사정문 조각을 일자로 이어 붙인 행랑**(사용자 제안, 서울역사박물관 복원 모형처럼), 모서리는 **근정전·흥례문 행각 모서리 같은 ㄱ자(바깥 추녀·안쪽 골·추녀마루)**(사용자 지시), 현판 없음, 관아 안쪽은 임포스터·건물 섞음(사용자 지시). 길에서 보이는 행각은 진짜 메시, 길에서 안 보이는 뒤 행각·안쪽 옆 행각은 껍데기.
- 교태전·동궁은 **통째로 지붕 모양 상자(임포스터)** — 길이 닿지 않는 곳이라(사용자 선택, 2026-09-30). 옛 단순화판·Complex 는 끄기만 함. 빈터는 **서쪽 + 동쪽(A·B·B′)** 을 궐내각사처럼 묶음 임포스터로 채움(사용자 선택) — 동궁 묶음은 동궁 그림 페이지를 같이 씀. 북쪽 띠 C 는 비워 둠(간의대·기구·취로정 연못만).
- 일본성 천수각은 **플레이어가 안으로 걸어 들어감**(사용자, 2026-10-03) — 층별 내부는 히메지·마쓰모토 현존 천수의 군사 공간식으로 할지 정하는 중(§9 일본성 시대 고증).
- 일본 마을 일반민가는 **길을 따라 집을 붙여 세운 거리** — 선착장 상인 거리에서 성 길 시작점까지 이어지는 큰길 + 뒷길, 약 350채(사용자 지시·선택, 2026-10-04 — 그 전 25 m 격자 배치는 땅이 바뀌어 대체). 기와민가는 줄·열에 맞춰 남향.
- 일본 마을 집은 **LOD 3단계**(LOD0 원본 / LOD1 지붕만 구운 판 + 나머지 줄임 / LOD2 임포스터, 약 35 m · 90 m) — GTA5 식 구역 합치기(HLOD)는 아직 안 함(사용자 승인 "시작합시다", 2026-10-04). 일본성도 같은 방식으로 부분(천수·망루 2·석축)마다 LOD, 실내는 거리로 끔(사용자 지시 "일본성 작업하세요", 2026-10-04).
- 일본섬은 **동·남쪽을 바다로 가라앉혀 서쪽 마을 + 가운데 언덕(성·신사)만** 남김, 높이 0.5 배, 기와민가(귀족저택) 구역 없앰, 성은 180° 돌려 성문이 마을 쪽, 신사는 서쪽 비탈에 **올린 고원**(입구 = 북쪽), 길은 신사 안을 지나지 않고 **마을 → 신사 입구(도리이) 앞 → 성** 한 줄(사용자 지시·선택, 2026-10-04 — 길이 너무 길고 무거워서, 그 뒤 "길이 신사로 나 있는데 이러지 말고"). 신사 고원 둘레는 성과 같은 **돌 축대 + LOD**(사용자 지시). 망루 계단은 성벽 통로로 올라갈 수 있게, 천수 안 계단은 걸어 오를 수 있게(사용자 지시). 잠긴 자리의 집은 다시 채우지 않음(사용자 선택).
- 기와 마을: 집은 0/90/180/270° 만 돌림, 한 집 6~10채·8집(사용자 지시, 2026-10-03). 담장은 둘레를 따르되 **건물 선에 맞춰 안으로 파인 곳은 곧게 이음**, 대문은 남쪽 하나(사용자 지시, 2026-10-03). 초가집은 기와 마을 주변 사용자가 그린 영역에 한 집 1~2채(사용자 지시, 2026-10-03). 초가집 담장은 기와집과 같은 방법(사용자 지시 "똑같이"), 문은 사립문(나무 울타리 문, 사용자 지시), 모서리는 둥글게(사용자 지시) — 대문 방향은 안채가 보는 쪽(동·서향 집이 있어서), 문 안쪽이 건물에 막히면 마당 옆 변, 그래도 막히면 집을 옮김. 초가집 터는 담 둘레까지 한 높이로 고름(기와집은 계단식 담 그대로 — 사용자 지시).
- **왜구 = 네 번째 진영 `Faction.Wokou`**(2026-10-07 사용자 선택): 누구에게나 적(플레이어 포함), 현상금 없음. 왜구섬은 일본섬 남서쪽 무인도, 소굴은 선착장 + 일본 마을 건물 재사용, 두목은 일본 장군 모델의 갑주를 검게.
- **퀘스트 = ScriptableObject(`QuestData`) + 빌더 에디터 스크립트**(Claude 가 정함, 2026-10-07): 1막 데이터는 `QuestAct1Builder` 로 만듦. 범위는 "시스템 + 1막까지 실제 진행"(사용자 선택). 대화 키 F(E 와 겹치지 않게 — Claude 가 정함).
- **2막~에필로그 퀘스트**(2026-10-09 사용자 "나머지 퀘스트도 만드세요"): 스토리 표 2-1~E-2 를 모두 만듦. 표에 없는 세부(호위 목적지·미행 길·전투 상대·대사)는 Claude 가 정함 — 2-3 수송선은 조선 상선 모델(`merchant` 원본)에 일본 진영을 붙여 씀(일본 상선 모델 없음), 3-3·3-5 "해적"은 왜구 배로, 2-4 "어촌"은 구로세 상인 거리 항구로, 2-5 "밤" 미행은 낮에(시간 흐름 없음), 2-8B "해월도 진입 불가"는 관군 현상금 2단계로, 4막 궁 안 이동은 TeleportPlayer 로, 4-5A 왕을 베는 장면·4-5B 자결은 알림 글로(쓰러지는 연출 없음), E-2 유서는 한윤겸이 건넴.
- **퀘스트 인물 모델 교체**(2026-10-09 사용자 "인물 모델링이 추가되었으므로 현재 퀘스트 모델링도 다 바꾸세요"): 숙부 → YiDam_Dopo(4막 왕은 YiDam_King), 현감 → JoMyeongGu, 왜구 병사 → WokouSoldier. 왜구 두목은 그대로(검은 갑주 장군). 새 인물(하야시·구로세·가신·정체 모를 조선인·한윤겸·관아 관리·권시헌·내관·홍 대비·궁녀·반란파·궁궐 호위)은 §5 퀘스트 NPC 모델. 섭정 가로(JpRegent)는 "어린 성주" 선택안을 쓰지 않아 배치 안 함, 한양 조력자(HanyangAlly)도 배치 안 함.
- **밤낮**(2026-10-09 사용자 지시): 하늘은 Physically Based Sky(해·달 원반·별·노을), 하루 = 낮 16 분 + 밤 6 분(Claude 가 정함 — 밤이 너무 길지 않게), 밤은 어둡지만 보이게(노출 하한 1). 밤 마을 불빛·NPC 하루 일과·시간에 따른 퀘스트 변화는 넣지 않음.

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
- [ ] 상선 `merchant/SM_Flag01c` 2개 메시 Missing — FBX(`01.Asset/Models/JoseonTongsinsaseon/Meshes/Buildings/SM_Flag01c.fbx`)는 있는데 그 안의 메시를 못 찾음. 꺼진 `BuoyancyObjects/Corner_01_barrel (1)~(12)` 는 메시·재질 GUID(`793b5bd3…`, `bfceaa15…`)가 프로젝트에 없음. 둘 다 2026-10-11 원본 정리 때 발견 — 삭제 목록·휴지통에 없어 원래 있던 문제.(선박 루트 `panok`·`anteck`·`seki`·`panok_navy`·`anteck_wokou`·`seki_wokou` 의 MeshRenderer 재질 0번 None 도 원래 그대로.)
- [ ] 광화문 부속단지 `Gwanghwamun_Complex` 만 아직 원본 단지를 씀(단순화 안 함). 원본 데모 씬이 지워져 다른 단지처럼 외부 단순화하려면 이 단지 원본(`Gwanghwamun_Original` 프리팹)에서 내보내야 함.
- [ ] `D:\UnityAssetCache` 정리 — 사용자 결정 대기(2026-10-11 측정: `Asset Store-5.x` 35.27 GB, Claude 작업 폴더 `_bake` 37.07 GB(그중 이전 결과 `out_prev_*`·`out_old` 12.34 GB) · `_edit` 3.06 GB · `_extract`·`_cards`·`_scan`·`_gungjang` 합 0.26 GB). 원본 단지가 지워져 `_bake`·`_edit` 는 다시 쓸 데가 없음.
- [ ] 새 섬: 안전지대·적 스폰 규칙을 섬별로 정해야 함(지금은 base 만). 섬을 옮기거나 새로 만들면 `WaterShoreMask` 범위·다시 굽기. 섬 지형은 첫 생성본 — 마을·궁·성 배치하며 다듬기. APV·리플렉션 프로브 범위 밖(최종 베이크 때 확인 필요).
- [ ] 경복궁: 광화문 문루·월대 사이 틈(§4 궁터 배치) 확인. 광화문 부속단지·영추문·동궁·경회루·건춘문 방향 확인. 흥례문 바닥판 확인. 진입 못 하는 건물 막기(콜라이더 — 사용자가 나중에). 문 열기는 광화문·흥례문·근정문·근정전만(`PalaceGate`, §4 문 열기) — 나머지 문(행각·건춘문·영추문·신무문·사정전·강녕전 등)은 아직 없음, 문짝 경첩 쪽·여는 방향 확인. `PalaceGate` 안내 UI 는 OnGUI 임시. 원본 단지 실내 조명 없음(데모 조명 안 가져옴). 원본 LOD0 가 무거움(단지 하나 2천만 삼각형·1만 렌더러대) — 성능 실측 필요, 더 줄이려면 GPU Resident Drawer(같은 메시 반복이 많음)·작은 부재 거리 컬링 검토. 데칼(`_DC`, 현판 글씨) 미처리.
- [ ] 궁장(§4): 가까이 모습·LOD 전환(150 m 에서 검은 판)·실제 프레임 사용자 확인 필요. 문을 옮기면 `궁장_Gungjang/Nodes` 마디를 옮기고 "궁장 다시 깔기". 광화문 단순화판 프리팹을 다시 만들면 날개 끝 자르기(`TrimGwanghwamunWings`)를 다시 해야 함. 인종·명종대 궁성 둘레는 확인 못 함. 받아 둔 KCISA 에셋(캐시 `D:\UnityAssetCache\Asset Store-5.x\Korea Culture Information Service Agency kcisa\...`, 1,937 MB, 조사 목록 `D:\UnityAssetCache\_scan\KCISA_SplineBuilder\`)은 안 씀 — 지울지 사용자 결정.
- [ ] 궁 안 빈터(§4 임포스터): 서쪽 A·동남 B·동궁 옆 B′ 는 채움(43 묶음, 크기·거울 섞음 — 반복이 덜 보이는지 사용자 확인 필요). 북쪽 띠 C(경회루·강녕전·동궁 북쪽, 약 4.9 ha)는 간의대·기구·취로정 연못(§4) 말고 비어 있음. 조사(검색, 2026-09-30): 간의대 — 세종 15년(1433) 착공·16년 완공, 후원 경회루 북쪽에 돌로 쌓고 돌난간, 꼭대기 간의·남쪽 정방안·서쪽 규표(40척)·서쪽 작은 집에 혼의·혼상(원문 확인: 『세종실록』 세종 19년 4월 15일, §4); 연산군 11년(1505) "撤簡儀臺"(철거) → 중종 대 재개축 → 명종 원년(1546) 중수 → 선조 13년(1580) 보수 → 임진왜란으로 소실(우리역사넷 『신편 한국사』 「간의대의 설치」, 고천문융합연구센터 「간의대 역사기록」); 크기 환산은 자료마다 다름(위키백과 약 6.3 × 9.7 × 6.6 m / 우리문화신문 9 × 14 × 9.8 m); 위키백과 「경복궁」은 "궁궐의 서북쪽 모퉁이". 북쪽 뜰(궁 안) — 세조 2년(1456) 후원에 취로정 연못을 파고 연꽃을 심음(지금 향원정 자리로 봄 — 서울시 미디어허브), 충순당(후원 전각, 문종 때 기록 — 뉴스천지; 위키백과 「경무대」는 충순당·취로정이 경무대 자리(신무문 밖)에 있었다고 전함 — 자료마다 다름, 확인 필요). 신무문 밖 후원(지금 청와대 자리)은 세종 8년(1426) 조성(서현정·연무장·과거시험장), 임진왜란 뒤 방치, 고종 때 다시 조성(정책브리핑 「청와대 역사」 — 예전에 이 파일에 "고종 때 조성"이라 적은 것은 고종 때 다시 조성한 것). 명종 8년(1553) 강녕전 화재로 사정전·근정전·경회루·함원전·청연루만 남고 편전·침전 구역이 불탐, 1554년 9월 중건(위키백과 「경복궁」). 세종 때 경회루 남쪽 = 궐내각사 권역(집현전)·보루각. 동쪽 빈터가 조선 전기에 무엇이었는지는 확인 못 함. 영추문 데모의 돌 대 `SM_YCM_Ilyeongdae`(이름 = 일영대, 4,472 삼각형, 경계 6.93 × 3.30 m · 높이 4.76 m)는 기록의 간의대보다 작음(무엇을 본뜬 것인지 확인 필요). 프로젝트에 나무·풀·꽃 모델 없음(에셋 검색). 조선 큰섬 터레인 레이어 8칸이 다 차 있어 흙 마당은 기존 `Sand_aerial_sand` 로 칠하거나 칸을 바꿔야 함.
- [ ] 육조거리(§4): 가까이 모습·실제 프레임·LOD 전환(약 45 / 130 m) 사용자 확인 필요. 길 쪽 원본 기와로 길에서 그려지는 삼각형이 약 20 % 늘어남(80만~131만). 문 여섯 곳이 모두 같은 사정문 조각. 줄 끝 관아 둘레 껍데기(뒤 행각·안쪽 옆 행각)는 가까이 가면 흐림(길에서는 안 보임). 모서리 추녀마루(용마루 토막을 비스듬히 편 것)·안쪽 골 가까이 모습 확인 필요. 관아 안 건물은 궁 단지 건물을 섞은 것이라 실제 육조 관아 배치와 다름(고증 확인 필요). 충돌체는 임포스터 껍데기(LOD2) 모양이라 처마 밑·기단 모서리에서 실제 모습과 조금 다를 수 있음 — 실제 캐릭터로 사용자 확인 필요. 110 m 밖 민가는 사용자.
- [ ] 임포스터(교태전·동궁·빈터 묶음): 가까이(30 m 안) 흐림·잡상과 추녀 끝 납작·계단 비탈 판 — 길이 닿게 되면 옛 단순화판으로 되돌리거나 그림 밀도를 올려야 함(사용자 확인). 충돌체 없음. 실제 프레임 사용자 확인 필요. 끈 옛 `Gyotaejeon_Simplified`·`Donggung_Simplified`(메시 578 MB)·`*_Complex` 정리 여부 결정.
- [ ] 다른 단순화판(흥례문·근정전·사정전·강녕전·건춘문·영추문·신무문·경회루·육조거리 행랑 등)도 외부에서 줄인 메시라 광화문과 같은 법선 문제(§4 광화문 법선 고침)가 있을 수 있음 — `MeshNormalFixTool.BadPercent` 로 재 보고 고칠지 사용자 결정.
- [ ] 경복궁 정리: 원본 단지 폴더의 안 쓰는 텍스처(R·MT·AO 등)·FBX 추출 머티리얼·데모 씬·URP 설정 애셋 정리, 옛 파일(`*_clean.asset`, 예전 `_roof` 텍스처, `out_prev*`) 정리, 먼 거리용 LOD2/HLOD, 텍스처 메모리·스트리밍 실측 — 아직 안 함.
- [ ] 근정전 단순화판: 내부에 들어가면 내부를 켜고 외부(전각·월대)를 LOD1 로 바꾸는 스크립트 없음(사용자 계획). 행각 토막 LOD 전환 거리·실제 프레임 확인 필요. 근정문 계단 밑 창호 판 두 장(Door02A/B 옆)이 같은 면에 겹쳐 흰 줄이 보일 수 있음(확인 필요). 행각 옆 마당에 터레인 풀이 바닥 위로 올라온 곳이 있음(캡처로 봄).
- [ ] 동궁 위치 — 실제 배치도에서는 근정전 동쪽 행각 바로 옆인데 지금은 근정전 동쪽 담에서 약 78 m 떨어져 있음(옮길지 사용자 결정). 동궁을 옮기거나 높이를 바꾸면 동궁 자리 터레인(§4)도 다시 맞춰야 함. 올린 터레인 둘레 비탈(3.5~9 m)은 칠을 안 바꿈 — 가까이 모습 사용자 확인 필요.
- [ ] 간의대(§4): 계단 자리·모양, 난간 모양, 어느 변이 동서인지는 가정 — 확인 필요. 기구(간의·정방안·규표·소각·혼의·혼상)는 넣었으나 크기·짜임은 가정(§4) — 확인 필요, 가까이 모습·실제 프레임 사용자 확인 필요. 경회루 땅판 북쪽 끝(z 4403.3)이 그 밖 터레인(10.0)보다 2.3 m 높아 턱이 보임(원래 있던 것 — 터레인을 올릴지 사용자 결정).
- [ ] 취로정 연못(§4): 가까이 모습·실제 프레임 사용자 확인 필요. 잔디 둔덕 네모 끝이 바위·절벽 칠 흙바닥과 갈림 — 터레인 칠을 바꿀지 사용자 결정. 연못 남쪽이 강녕전 땅판 북쪽 1.74 m 턱(원래 있던 것)과 맞닿음. 석축(물 위 0.72 m)은 올라올 수 없어 빠지면 못 나옴 — 진입 막기와 함께 사용자가 정리. 교태전·신무문에서 정자로 가는 길 없음. 정자·연못 크기와 정자 짜임은 가정.
- [ ] 경회루 단순화판: 연못 자리 터레인을 파야 물·석축이 보임(확인 필요). 1·2층 천장·난간 판의 가까이 모습은 사용자 확인 필요.
- [ ] 광화문 문루 단순화판: 멀리서 기와 지글거림(움직이며)·LOD 전환(60 / 150 m)·실제 프레임 사용자 확인 필요. 월대·해태는 단순화판에서 뺐지만 씬의 옛 `GwanghwamunGate_Woldae`·`_Haetae` 는 켜진 채(끌지는 사용자 결정).
- [ ] 경복궁 단순화판: 강녕전·사정전·근정전·경회루·동궁·교태전·광화문 문루·흥례문·건춘문·영추문·신무문 적용 — 광화문 부속단지만 줄인 메시(`reduced.bin`)와 판 데이터(`cards.bin`, §4)만 있고 Unity 에 안 올림(사용자 지시 대기). 흥례문 방식(보임 검사 → 부분 나누기 → LOD → `PalaceLodBinImport`, §4)을 그대로 쓸 수 있음. 교태전·동궁은 임포스터로 바뀜(§4). 두 단지 모두 가까이 모습(기와 틈·받침판 가장자리·한쪽 면 처리로 방 안이 보이는지)·LOD 전환 거리·실제 프레임 사용자 확인 필요. 옛 방식 산출물(사정전 `_Shell.asset` 1.86 GB, `<단지>_RoofLOD`) 정리 여부 결정.
- [ ] 흥례문 단순화판(§4, 2026-09-30): 가까이 모습·실제 프레임·LOD 전환(약 60 / 150 m — 60 m 에서 기와가 판으로 바뀌는 모습, 판 재질 밝기는 정면 한 시점에서만 맞춤)·판 경계 사용자 확인 필요. LOD0 메시가 각진 음영이라 정점이 많음(LOD0 합 5,425,475 정점, 파일 기준) — 줄일지 결정. 충돌체는 LOD0 전체 메시(부분 13개)라 무거울 수 있음 — 진입 막기와 함께 사용자가 정리. 문루 서쪽 칸 계단 자리 옆 행각 사이로 하늘이 보이는 틈은 원래 모델에도 있음(계단이 가리고 있었음). 씬에서 끈 `Heungnyemun_Complex` 정리 여부 결정.
- [ ] 건춘문·영추문·신무문 단순화판(§4, 2026-09-30): 가까이 모습·실제 프레임·전환(부분 중심 약 200 m) 사용자 확인 필요 — 멀리 쓰는 예전 구운 덩어리는 석축이 LOD0 보다 밝고 얼룩져 보여 바뀌는 게 눈에 띌 수 있음(영추문 215 m 비교). 충돌체는 LOD0 메시 전체(부분마다) — 진입 막기와 함께 정리. 건춘문 `SM_GCM_DoorB` 4개 둘째 서브메시의 원래 의도 재질(Wood01 / 남는 슬롯 WoodTile) 확인 필요. 씬에서 끈 `<단지>_Complex` 정리 여부 결정. 영추문 문 남쪽 건물 3채(§4)는 약 70 m 에서 구운 것으로 바뀜 — 바뀌는 모습(구운 것은 기와가 매끈함)·실제 프레임 사용자 확인 필요, 무슨 건물인지·인종·명종대에 있었는지 확인 필요.
- [ ] 문·문 장식 판 교체(§4, 2026-09-30): 가까이 모습(옆에서 보면 납작함, 문 장식 판이 문 앞에 뜸)·실제 프레임 사용자 확인 필요. 광화문 가운데 문짝(`GHM_GateLeaf2/3`) 판 아래쪽에 몇 mm 투명 줄(원래 형상의 틈)이 있어 비쳐 보이는지 확인 필요. 새 메시 에셋 209 MB(원래 메시는 원래 에셋·FBX 안에 그대로 남아 있음 — 정리 여부 결정).
- [ ] 경복궁 최적화 뒤: 창살 카드 가까이 모습·지붕 껍데기 전환 거리·실내 컬링 누락(몇 픽셀짜리 틈) 사용자 확인 필요. 사정전 행각 끝 잘린 기와 막기(원하면). 실제 프레임 성능 측정 안 함. PC 메모리 사용률 높음(약 85 %).
- [ ] 초가 마을(§4): 마당·길·텃밭 없음. 집 사이가 좁은 곳은 담이 건물에 바짝 붙음(여유 0.3 m 까지 줄인 집 있음). 북쪽 바닷가 두 곳은 집 터를 돋워 비탈이 46~53° — 가까이 모습 사용자 확인 필요. 땅을 고친 뒤 `WaterShoreMask` 를 다시 굽지 않음. 사립문은 닫힌 모습(충돌체 = 문 메시라 못 들어감). 가까이 모습·프레임 사용자 확인 필요.
- [ ] 기와 마을(§4): 집 밑 땅을 고르지 않아 비탈 쪽은 기단·담이 땅에 조금 묻힘(건물 밑 높이 차 최대 1.0 m, 담 최대 0.72 m · 집 8의 3조각 1.0~1.43 m). 담장은 2.4 m 칸이라 한 칸짜리 바깥 계단꼴 꺾임이 남음 — 더 펴야 하는지 사용자 확인 필요. 대문은 닫힌 모습 그대로(여는 스크립트 없음, 충돌체 = 대문 메시라 못 들어감). 가까이 모습·프레임·LOD 필요 여부 사용자 확인 필요.
- [ ] 그려지는 삼각형 계산(2026-10-04, 일본섬 값은 다시 만들기 전, 카메라 시야 + LOD 선택(화면 높이·lodBias 1)만 — 가림·그림자·터레인·물 빼고 계산한 값, 실제 측정 아님): 경복궁 육조거리 남쪽 620만 · 광화문 앞 653만 · 근정전 마당 355만 · 경회루 남쪽(북쪽 봄) 49만 · 궁 위 공중 580만 · 남쪽 바다 1.2 km 634만 / 일본섬 상인 거리 끝 큰길 945만 · 남쪽 큰길 317만 · 성 안에서 마을 736만 · 선착장 앞바다 1,169만 · 섬 위 공중 1,170만. 까닭: 경복궁은 먼 LOD(예전 구운 덩어리)가 무거움 — 1.4 km 밖에서도 근정전 전각 `GJ_GJJ_LOD` LOD1 134만, 사정전 행각 56만, 강녕전 행각 56만(마지막 LOD 가 화면 높이 0.003 까지라 수 km 밖까지 그림). 일본 마을은 그때 LOD 가 없어 섬이 보이면 일반민가 약 990만이 다 그려졌음 — 2026-10-04 섬을 줄이고 집 LOD 를 넣음(§4, 측정은 성능 실측 항목).
- [ ] 성능 실측(2026-10-04, **일본섬 다시 만들기 전 값**, 사용자 PC RTX 5070 12 GB, 에디터 Play, Game 뷰 3840×2160 최대화, 모니터 60 Hz·vSync 1, 에디터가 포커스 없는 상태, 카메라 `Main Camera` 를 떼어 옮김, `PerfProbe` 240 프레임 평균): GPU ms / CPU 메인 ms / 삼각형(그림자 포함) / 드로우콜 — 하늘만 6.1 / 8.4 / 16만 / 45 · 경복궁 육조거리 남쪽 11.1 / 11.7 / 2,041만 / 14,153 · 광화문 앞 9.5 / 11.2 / 2,326만 / 7,909 · 근정전 마당 9.3 / 10.0 / 1,723만 / 4,151 · 경회루 앞 7.8 / 9.3 / 347만 / 1,307 · 궁 위 공중 8.4 / 10.4 / 1,802만 / 4,087 · 남쪽 바다 1.2 km 11.2 / 14.9 / 1,515만 / 21,861 · 일본 상인 거리 끝 큰길 12.6 / 13.1 / 3,429만 / 15,351 · 남쪽 큰길 9.0 / 10.7 / 1,357만 / 6,094 · 성 안→마을 10.6 / 12.1 / 2,486만 / 11,206 · 선착장 앞바다 11.2 / 14.4 / 3,626만 / 20,111 · 섬 위 공중 14.3 / 15.3 / 3,679만 / 24,006(최대 GPU 19.4 ms). 프로파일러 `Gfx Used Memory` 19.1 → 21.8 GB(돌아다니며 늘어남, 에디터 몫 포함 — 12 GB 보다 큰 까닭은 확인 필요). 4K 에서 60 fps(16.7 ms) 안이지만 여유가 적은 곳(일본섬 위·큰길)이 있음. 1080p·빌드에서는 안 잼.
- [ ] 성능 실측(2026-10-04, 일본섬 다시 만든 뒤, 같은 조건 — 4K 에디터 Play, `PerfProbe` 240 프레임 평균, GPU ms / CPU 메인 ms / 삼각형 / 드로우콜): ① 다시 만든 뒤·LOD 전: 큰길 (1531, −975) 남동쪽 봄 8.8 / 11.4 / 1,737만 / 8,271 · 성 안→마을 12.6 / 17.0 / 1,867만 / 19,469 · 앞바다 (1300, 6, −780) 11.4 / 13.7 / 1,883만 / 12,647 · 섬 위 (1500, 260, −700) 12.4 / 14.1 / 1,725만 / 8,711. ② + GPU Resident Drawer(Static Batching 끔): 10.5 / 11.6 / 1,751만 / 2,777 · 11.9 / 13.1 / 2,311만 / 5,968 · 9.7 / 12.9 / 1,886만 / 6,455 · 11.3 / 12.0 / 1,725만 / 2,882 — 드로우콜 약 1/3, GPU·CPU 는 곳에 따라 −3.9 ~ +1.7 ms. ③ + 집 LOD: 10.8 / 13.6 / 505만 / 2,652 · 11.4 / 14.6 / 505만 / 5,488 · 9.6 / 14.0 / 450만 / 6,061 · 11.1 / 13.8 / 369만 / 2,576. 같은 세션 맞비교(성 안→마을, LOD0 강제 ↔ 자동): GPU 12.4 → 11.3, CPU 14.5 → 14.6, 삼각형 1,882만 → 503만. → 삼각형은 70~80 % 줄었으나 4K 에디터에서 GPU 는 약 1 ms 만 줄어듦(하늘만 봐도 GPU 6.1 ms — 화면 크기에 드는 고정 비용이 큼), CPU 는 세션마다 1~2 ms 오르내림(에디터 몫). 빌드·1080p·약한 GPU 에서는 안 잼.
- [ ] 성능 실측(2026-10-04, 일본성 LOD 뒤, 에디터 Play, Game 뷰 1920×1080, vSync 1(60 Hz), `PerfProbe` 240 프레임 평균, 성 LOD0 강제 ↔ 자동 같은 세션 맞비교, GPU ms / CPU 메인 ms / 삼각형 / 드로우콜): 앞바다 (1300, 6, −780) 성 쪽 봄 5.3 / 13.3 / 435만 / 5,765 ↔ 5.7 / 14.4 / 229만 / 4,958 · 큰길 (1531, 6.7, −975) 성 쪽 봄 6.2 / 13.6 / 534만 / 2,976 ↔ 6.0 / 15.0 / 288만 / 2,095. → 1080p 에서는 GPU 가 5~6 ms 라 삼각형을 줄여도 프레임은 거의 그대로, 메인 스레드(약 12 ms)가 더 큼. 메인 스레드 중 스크립트(`Update.ScriptRunBehaviourUpdate` 1.2 + LateUpdate 0.6 + FixedUpdate 0.7 ms)·`CullScriptable` 0.6 ms 는 잼, 나머지 약 9 ms 는 어디인지 확인 필요(프로파일러 창으로 봐야 함). 둘 다 16.7 ms 안이라 이 두 자리에서는 60 fps 를 지킴. 빌드에서는 안 잼.
- [ ] 일본 마을·성(§4, 2026-10-04 다시 만든 뒤): 151채 + 성. 집 7종·성 LOD 넣음(§4). 성 오름길(최대 25°, 성문 앞 흙 경사로)이 가파르고 계단 모델 없음 — 캐릭터로 오를 수 있는지 사용자 확인 필요. 절 자리 계획 상자(`TemplePlot`)는 켜진 채. 마당·담 없음. 신사 돌 축대(최대 12 m) 아래 45° 흙 비탈·경사로 옆 모습 사용자 확인 필요. 본전이 북쪽(길 쪽)을 보게 돌렸음(예전 서쪽) — 사용자 확인 필요.
- [ ] 일본성(§4): 천수 안 계단·망루 계단 → 성벽 통로는 같은 크기 CharacterController 로만 시험함(통과) — 실제 캐릭터로 사용자 확인 필요. 망루 문이 막혀 있음(들어가게 할지 사용자 결정). 성문·천수 문 여는 스크립트 없음. 고원 가장자리가 조금 낮은 곳(0.1~4 m)에서 석축 밑이 뜨는지, LOD 전환 모습(천수 50 / 220 m)·가까이 모습 사용자 확인 필요. 천수 LOD1 은 처마 밑이 어두운 판이라 LOD0(흰 서까래)와 색이 다름. 실내는 실내 가운데에서 45 m 밖이면 꺼짐 — 문을 열어 두는 연출을 넣으면 밖에서 안이 비어 보일 수 있음. 석축 반복 텍스처 이음이 보이는지 확인 필요.
- [ ] 일본성 시대 고증(조사, 2026-10-03): 천수(天守)와 큰 석축 기단은 아즈치성(1576)이 처음이고, 그 전 센고쿠 시대 성은 대부분 나무로 된 방어 시설(위키백과 「Japanese castle」). 히메지 대천수는 1601 착공·1609 완공, 1561 에 구로다 가문이 고쳐 쌓은 기록이 있음(효고현립역사박물관 히메지성 연표). → 히메지식 `CastlePlan`(천수·석축)은 게임 시대(1544~1567)보다 늦음 — 그대로 갈지 사용자 결정 필요. 어전(御殿) A·B 는 특정 성의 실제 건물이 아니라 Claude 가 넣은 어림 배치(성주의 거처·정청은 천수 가까운 단층 건물들이었다는 일반 구성만 따름).
- [ ] 터레인 브러시 다듬기, 선착장 주변 정리, 나무·풀 없음. 제대로 된 횃불 메시 없음, 횃불 불티가 많음(모닥불용 파티클).
- [ ] 호수·나룻배·궁 그림(§4): 실제 E 키 3초 입력·타고 가는 느낌(40 s, 카메라 거리 9 m) 사용자 확인 필요. 남쪽으로 갈 때는 노가 배 너머라 노 젓는 모습이 잘 안 보임. 배·사공 충돌체 없음(타기는 E 로만). 타는 동안 화면 UI(체력·현상금·미니맵)는 그대로. 그림은 마을 쪽 한 눈 위치 기준이라 높은 곳이나 배로 호수 안 육조거리 가까이 가면 진짜 궁과 어긋나 보일 수 있고, 궁 안 그림자·AO 가 없음. 궁을 바꾸면 그림을 다시 찍어야 함. 육조거리 쪽에 있을 때 궁은 켜진 채(육조거리에서 내려 걸어 다닐 때 성능은 안 잼). 호수 서남쪽 입구가 바다와 이어져 있음 — 막을지 사용자 결정. 선착장 경사로로 땅을 낮춘 뒤 `WaterShoreMask` 는 다시 굽지 않음(낮춘 땅이 0.95 m 위라 마스크 판정(−0.5 m 위 = 육지)은 그대로일 것으로 보이나 확인 필요). 하차 비·안개 입자 끔은 Play 로 확인 안 함. 씬 저장은 사용자.
- [ ] APV·리플렉션 프로브 최종 베이크(바위 위치 확정 후).

### 배·전투(해상)
- [ ] T 자 정박: 노가 판자 쪽으로 뻗어 겹쳐 보이는지, 실제 E 키로 내리고 타는 손맛, 판자 끝 ↔ 선체 1.2~1.65 m 빈틈이 충분히 "밀착"인지 사용자 확인 필요.
- [ ] 들이받기 피해 크기(1.2 × 상대 속도 × 부스트)·전투 북 소리(합성)·속도는 사용자 확인 필요.
- [ ] 리스폰 화면 전환(`PlayerRespawn.fadeDelay` 자리), 리스폰 시 자원 손실.
- [ ] 안전지대 경계에서 AI 움직임, 관군 소집 지점이 경계 안일 때 — 플레이 확인 필요.
- [ ] 적 배 소리(ShipAudio 미부착), 침몰·착수 효과음 없음. 볼륨 밸런스 플레이 확인.
- [ ] 선착장 배(§4 섬 선착장): 실제로 몰고 들어가 내릴 때 방해가 없는지·원 크기(반지름 45 m)·속도(2.5 m/s)·밀리는 느낌 사용자 확인 필요. 섬 모양·선착장을 바꾸면 원을 다시 찾음(플레이 시작마다 계산). 선착장 배끼리·다른 선착장 배와 부딪힐 일은 따져 보지 않음(한 원에 1~2척). 받힌 배가 육지로 밀려 얕은 곳에 걸리면 빠져나오는지는 안 봄.
- [ ] 선원(§3): 제자리에서만 움직임(자리 이동·엄폐 없음), 표적 배 반대쪽 선원은 자기 배 갑판·난간을 가로질러 쏨(가림 검사 없음), 쓰러진 선원 다리가 난간에 걸칠 수 있음. 선원은 플레이어 리스폰 때만 되살아남(영입·회복 없음). 명중률·장전 시간은 시험값(측정: 관군 ↔ 안택선 빈틈 9.3 m 에서 25 s 에 10→7 / 12→11) — 손맛 확인 필요. 빈틈 30 m 에서 AI 배(안택선 공격 거리 atk 70 m 씬 값·관군 접현 회피 28 m)끼리 선원 싸움이 얼마나 일어나는지는 확인 필요. 카메라 90 m 밖 배의 선원은 꺼져 쏘지 않으므로, 화면 밖 먼 곳의 배끼리는 선원이 줄지 않음. 소리는 들어 보고 고른 것 아님. 선원 수가 많을 때(측정: Play 중 씬 전체 약 550명) 성능은 재지 않음. 상선은 선원 없음. 씬 저장은 사용자.
- [ ] 선원이 배 난간에 가려 가까이서도 거의 안 보임(§3 선원 — 특히 안택선) — 선원을 높이 세울지·난간을 낮출지·그대로 둘지 사용자 결정 필요.
- [ ] 마을 사람 죽이기(§4): 현상금 영향 없음, 다른 사람이 도망가거나 놀라지 않음, 피·소리 없음, 맞는 판정 거리(range + 0.35 m)가 손맛에 맞는지 사용자 확인 필요. 실제 마우스 클릭으로는 안 해 봄. 기모노 여자 옆모습 렌더에서 치마 쪽 어두운 삼각형 판이 보임(원래 있던 것으로 보임 — 확인 필요).
- [ ] 마을 사람(§4): 60 m 밖에서 꺼진 동안 제자리에 멈춰 있어, 멀리서 본 자리와 다가갔을 때 자리가 같음(눈에 띄는지 사용자 확인 필요). 사람끼리·플레이어와 부딪힘 없음(NavMeshAgent 회피만), 문이 닫힌 담 안·집 안으로는 안 들어감, 말·대화·하루 시간 반응 없음, 여자도 남자와 같은 걷기 클립. 다른 조선 섬(중간·작은 섬)은 마을이 없어 사람 없음. 오래 둔 모습·성능은 안 봄. 씬 저장은 사용자.
- [ ] 화살·선원 체력 강화는 값만 저장. 자원 획득 경로·저장/불러오기 없음. 깃발 이미지 실제 파일로 미시험. 인벤토리 아이템 시스템 없음.
- [ ] 예측 사격 난이도 조정, 미니맵 밖 적 방향 표시.
- [ ] 함선 메뉴 강화/영입 버튼은 단색(공용 아트 미적용).

### 캐릭터·전투(육상)
- [ ] 한양 조력자 모델(§5): 인물 이름 미정(파일 이름은 임시로 HanyangAlly). 실제 Play 에서 움직임·LOD 전환은 확인 필요.
- [ ] 퀘스트 NPC 모델(§5): 아직 안 만든 것 — 구로세 "어린 성주" 몸. 홍 대비는 일상 당의 차림 기본안이라 연출(예복 등)이 정해지면 바꿀 수 있음. 철릭 계열(JoMyeongGu·PalaceGuard·RebelSoldier·GwanaClerk·MaskedMan) 소매 끝 주름이 들쭉날쭉해 보임 — 다듬을 과제. LOD1·LOD2 의 작은 점(UV 경계) 정리. 보·흉배 무늬는 단순 무늬(용·학 그림 아님). 일반 인물 폴더 이름 확정 필요. 모두 실제 Play·가까이 모습 확인 필요.
- [ ] 육상 병사(§4 병사): 실제 키보드·마우스로 싸워 보기(난이도·한 번에 2명·둘러서기 거리), 병사끼리 겹치는 장면, 일본 병사·장수도 환도 모델(일본도 모델 없음), 일본 장수 메시 칼과 뽑은 환도가 같이 보임, NPC 몸에서 환도 뽑기 동작(해신 팔·칼집 돌리기 값 그대로라 손이 손잡이에 안 맞을 수 있음 — 확인 안 함), 경비가 관아 문 콜라이더 앞에 제대로 서 있는지 가까이 모습 — 사용자 확인 필요. 관군·일본군 사이 싸움 없음(만나지 않음). 현상금과 연결 안 함. 다른 섬 마을·성 안 천수 층에는 병사 없음.
- [ ] 새 음원(§3 소리): 베는 소리·노 소리(Kenney 녹음 + rowing 녹음 조각, 피치 0.5)·탄·화살 박힘(합성) — 사용자가 들어 보고 확인 필요. 안 쓰는 합성 파일(`Oar/oar_drive_*`·`oar_recover_*`, `Sea/rowing_quickly_loud.ogg`)을 지울지 결정.
- [ ] 배 부스트 2 배(약 51 m/s, 4 배 100 m/s 때만 Play 확인): 충돌·물결·카메라·적 AI 추격 균형 사용자 확인 필요.
- [ ] 메인 씬 `Haesin_Player` 시험용 무적(`CombatStats.invulnerableHealth`) 켜져 있음 — 시험 뒤 끄기.
- [ ] CharacterTest 씬 두 캐릭터 `SwordAudio.fleshClips` 비어 있음.
- [ ] 메인 씬 하차 후 칼싸움: CombatHUD·칼싸움 적(EnemySwordAI) 없음, EnemySwordAI 는 지형·중력 미지원.
- [ ] CharacterTest 와 메인 씬 캐릭터를 공유 프리팹으로 만들지 결정.
- [ ] 옆·앞 회피·점프는 임시 동작(팩에 공중제비 아닌 회피 없음). 락온 UI·카메라 없음. 적 AI 난이도 조정, 여러 적.
- [ ] 이동 블렌드 트리를 Fab 클립으로 바꿀지. 걷기 2.69 m/s 가 빠른지.
- [ ] 배가 빠르게 움직일 때 치마·포니테일 날림, 옷 전환(1번 키) 시 소매·치마 MeshCloth 가 깨지는지 — 확인 필요.
- [ ] 치마 MeshCloth(§5): CharacterTest 씬의 `Haesin_Player`·`Haesin_Enemy` 는 아직 옛 BoneCloth — 메뉴 "HaeSin/치마 MeshCloth 적용 (선택한 캐릭터)" 로 바꿀 수 있음(씬 저장은 사용자). 실제 키 입력으로 오래 움직인 모습·칼싸움 중 모습·성능(self collision FullMesh 포함) 사용자 확인 필요. 달릴 때 부츠가 뒷자락 아래로 보이는 장면(위 확인 참고).
- [ ] 적 예비 동작이 짧음(내려베기 0.24 s) — 사람이 튕겨낼 수 있는지 손맛 확인.
- [ ] 칼 뽑기·넣기(§5 환도): 칼집이 도는 동안 옆·뒤로 크게 흔들려 보이는 것, 칼집 잡은 왼손이 허리에 손 얹은 듯 보이는 것, 왼팔 머슬이 Idle1 보다 한계를 많이 넘는 것(초과량 17–25 vs Idle1 6.6) — 사용자 확인 필요. 걸으며 뽑기·전투 중 자동 뽑기 모습은 확인 안 함. 치마 MeshCloth·HwandoGripIK 와 함께 볼 때 문제 없는지 확인 필요.
- [ ] 효과음·인벤토리 조명 밝기는 수치로만 고름 — 사용자 확인.
- [ ] 발 IK: 계단 내려가기·전투 중 동작은 수치 확인 안 함 — 사용자 플레이 확인 필요. CharacterTest 의 캐릭터에는 FootIK 없음.
- [ ] 실제 키보드·마우스(E 3초, WASD, 좌클릭 꾹, 메뉴 Y/I/M/ESC, 드래그) — 대부분 메서드 호출로만 검증, 사용자 플레이 확인 필요.
- [ ] 일본 병사 모델(§5): LOD1·LOD2 웨이트는 LOD0 에서 옮긴 것이라 모양이 다른 곳(소매·허리 갑옷)에서 어긋날 수 있음 — 사용자 확인 필요. AI·충돌체 없음, 씬 배치 안 함. 조총 사격은 클립 하나(겨눔·반동 루프)만 — 걷기·장전·조준 전환·총구 불꽃·연기·소리·탄 없음. 갑옷 검정 칠은 색 범위로 고른 것이라 경계에 갈색이 조금 남거나 다른 갈색(띠 등)이 같이 검어졌을 수 있음 — 가까이 모습 사용자 확인 필요.
- [ ] 조선 수군 모델(§5): LOD1·2 웨이트는 옮긴 것(어긋남 확인 필요). AI·충돌체 없음, 씬 배치 안 함. 활 쏘기는 클립 하나(루프)만 — 날아가는 화살·맞음·소리 없음, 다시 걸 때 화살이 갑자기 나타남, 활대가 안 휨. Play 확인 필요.
- [ ] 일본 장군 모델(§5): 자동 웨이트라 갑옷 판·투구 장식이 관절에서 휘거나 늘어질 수 있음 — 가까이 모습 사용자 확인 필요. Animator 컨트롤러·AI·충돌체 없음, 씬 배치 안 함. LOD 전환 거리(15 / 40 / 150 m)는 어림값.
- [ ] 시민 모델 4종(§5): 원본 리깅을 쓸 수 없어 뼈대·웨이트를 새로 입힘(자동 웨이트) — 치마·도포 자락이 다리에 붙어 늘어나므로 걸을 때 모양 사용자 확인 필요(천 시뮬레이션 없음). Animator 컨트롤러·AI·충돌체 없음, 씬 배치 안 함. LOD1·2 는 렌더 확인만 하고 가까이 모습은 확인 안 함.
- [ ] 해적 모델(§5): 판옥선 선원으로 쓸 예정(사용자) — 배에 태우기·AI·충돌체 없음. 활 쏘기는 수군 클립 그대로(루프 하나, 날아가는 화살·소리 없음, 활대 안 휨). 자동 웨이트라 허리 칼집·옷자락이 다리를 따라 늘어날 수 있음, 가까이 모습·Play 사용자 확인 필요.

### 퀘스트 (2026-10-07 1막, 2026-10-09 2막~에필로그)
- [ ] 실제 키보드·마우스(F 대화, 걸어가 말 걸기, 실제 포격, 실제 칼싸움)로 해 보지 않음. 2막~에필로그를 처음부터 끝까지 이어서 해 보지 않음(새 기능은 메서드 호출로 하나씩 확인 — §3 퀘스트) — 사용자 확인 필요. 선택지 버튼은 `Choose` 호출로만 확인.
- [ ] 퀘스트 진행 저장/불러오기 없음 — Play 마다 P-1 부터. 시험용 F8(목표 바로 끝냄)이 켜져 있음 — 출시 전 `debugCompleteKey = None`.
- [ ] `QuestAct1Builder`·`QuestStoryBuilder`·`QuestSceneSetup` 을 다시 부르면 인스펙터·씬에서 손으로 고친 것을 덮어씀.
- [ ] 궁 안에서 쓰러지면(4-4 호위 싸움) `PlayerLandDeath` 가 배(한양 선착장)에 태워 되살림 → 근정전 마당까지 걸어서 돌아갈 수 있는지 확인 안 함(궁 안 이동은 TeleportPlayer 로만 함). 4-3·4-5 시작 때 배에 타고 있으면 옮기지 않음(경고만).
- [ ] 호위 배 = 조선 상선 모델(일본 진영). 습격 왜구는 가장 가까운 적을 노려서 호위 배보다 플레이어 배·일본 선착장 배를 먼저 칠 때가 있음. 대비전 불(Fire_v2)·상인 거리 불은 편집 모드 캡처에 안 나오고 Play 다른 시점 캡처에서 작고 연기가 흰 덩어리로 보였음 — 크기·모양 사용자 확인 필요.
- [ ] 미행: 가신이 짝수 점에서 2.5 s 멈춤, 들킴 5 m·놓침 45 m — 손맛 확인 필요. 낮에 함(시간 흐름 없음).
- [ ] 4-5A 왕을 베는 장면·4-5B 자결 장면 없음(알림 글 + 왕을 숨김). 4-5 왕이 등 돌린 모습은 캡처로 앞뒤 구분이 어려움 — 확인 필요.
- [ ] 망운도 관아·초가는 땅을 고르지 않고 가장 낮은 높이에 둠(비탈 쪽은 땅에 묻힘). 마을·사람 없음. 해월도도 관아 한 채뿐.
- [ ] 2-8A 뒤 일본 현상금 5단계·4-5A 뒤 관군 5단계는 그대로 남음(되돌리는 퀘스트 없음). 2-8B 의 "해월도 진입 불가"는 관군 현상금으로만 함.
- [ ] 현상금: 단계 문턱(25/40/55/70/85)·오르는 양(맞힘 2, 군선 15, 상선 10)·손맛, 왜구 돛·깃발 색 — 사용자 확인 필요.
- [ ] 씬 변경(2026-10-07·10-09 퀘스트 배치·모델 교체·왜구섬·HUD 값·NavMesh)은 **씬 저장 안 함** — 사용자가 저장.

### 밤낮 (2026-10-09)
- [ ] 밤 밝기(노출 하한 1·달 2.5 lux)·하루 길이(16 + 6 분)·노을 색 — 사용자 확인 필요. 해 뜨는 무렵·흐린 날 밤은 캡처 안 함.
- [ ] 마을·관아·궁에 밤 불빛(등롱·창 불빛) 없음 — 밤에 마을이 어두움. 횃불은 베이스 섬 8개뿐.
- [ ] 베이크된 APV 는 낮 해 기준 하나뿐(밤 장면용 굽기 없음) — 밤에는 간접광을 거의 끄는 방식이라 그림자 속이 아주 어두움.
- [ ] 궁궐 원경 그림(`궁궐원경_PalaceVista`)은 낮에 찍은 기본색·노멀 판이라 밤·노을에 진짜 궁과 밝기가 어긋날 수 있음 — 확인 필요.
- [ ] PBS 해 둘레가 회백색 안개처럼 보임(볼류메트릭 안개 산란으로 보임 — 확인 필요). 예전 GradientSky 의 진한 하늘색과 달라짐.
- [ ] 시험 키 `=`(1 시간 건너뜀)가 켜져 있음 — 출시 전 `skipKey = None`.
- [ ] 씬 변경(DayNightCycle·Moon·WeatherSystem 참조·해 원반 값)은 씬 저장 안 함 — 사용자가 저장. 볼륨 프로필 에셋(PBS 추가·하늘 종류)과 별 큐브맵은 에셋이라 이미 저장됨.
