using System;
using System.Collections;
using SevenDoctors.Data;
using SevenDoctors.UI;
using UnityEngine;

namespace SevenDoctors.Core
{
    /// <summary>
    /// 게임의 유일한 진입점.
    ///
    /// 씬에는 이 컴포넌트가 붙은 오브젝트 하나만 있으면 됩니다.
    /// UI도, 매니저도 전부 여기서 코드로 만들기 때문에
    /// 씬 파일에 참조가 거의 없고, 따라서 깃 충돌과 Missing Reference 가 생기지 않습니다.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameBootstrap : MonoBehaviour
    {
        [Header("시작 지점")]
        [Tooltip("게임을 시작할 방 ID (Rooms 탭)")]
        public string StartRoomId = "room_lab";

        [Tooltip("시작하자마자 재생할 대화 ID. 비워두면 바로 탐색 상태로 들어갑니다.")]
        public string StartDialogueId = "dlg_prologue_01";

        [Header("옵션")]
        [Tooltip("켜두면 실행 시 시트 참조 무결성을 검사해 콘솔에 보고합니다.")]
        public bool ValidateOnStart = true;

        [Tooltip("글자가 찍히는 속도(초/글자)")]
        public float CharInterval = 0.022f;

        [Tooltip("끄면 시작 화면 없이 바로 게임에 들어갑니다. 대사를 손볼 때 편합니다.")]
        public bool ShowTitleOnStart = true;

        // AudioManager 는 실행할 때 코드로 붙이기 때문에, 재생 전에는 인스펙터에
        // 뜨지 않습니다. 여기 있으면 껐다 켜지 않고도 미리 맞춰 둘 수 있습니다.
        [Header("소리 크기")]
        [Tooltip("배경음")]
        [Range(0f, 1f)] public float BgmVolume = 0.50f;

        [Tooltip("효과음")]
        [Range(0f, 1f)] public float SfxVolume = 0.55f;

        [Tooltip("말할 때 나는 소리")]
        [Range(0f, 1f)] public float VoiceVolume = 0.40f;

        /// <summary>
        /// 재시작 뒤 시작 화면을 건너뛰고 바로 들어갈 방식. 엔딩 카드의 '분기점으로' 가 정합니다.
        /// 씬을 다시 불러도 남도록 정적으로 둡니다.
        /// </summary>
        static StartMode? _pendingStart;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        SevenDoctors.Dev.DevPuzzleMenu _dev;
#endif

        void Awake()
        {
            // 방 전환 시에도 살아남아야 하므로 (씬은 하나지만, 타이틀/엔딩 씬을 붙일 때를 대비)
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// 처음부터 다시. 엔딩 카드의 '처음으로' 가 부릅니다.
        /// 이 오브젝트는 DontDestroyOnLoad 라 씬만 다시 불러서는 안 사라집니다 — 먼저
        /// 부수고 씬을 다시 부르면, 씬에 들어 있는 새 부트스트랩이 전부 새로 만듭니다.
        /// 플래그·증거·UI 가 전부 이 오브젝트 아래나 Game 에 있어서 흔적이 남지 않습니다.
        /// 저장은 하지 않습니다 — 저장하고 돌아가려면 ReturnToTitle 을 쓰세요.
        /// then 을 주면 시작 화면 없이 그 방식으로 바로 들어갑니다.
        /// </summary>
        public static void Restart(StartMode? then = null)
        {
            _pendingStart = then;
            var current = FindFirstObjectByType<GameBootstrap>();
            if (current != null) Destroy(current.gameObject);
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }

        /// <summary>메뉴의 '타이틀로'. 자동 저장하고 시작 화면으로 돌아갑니다.</summary>
        public static void ReturnToTitle()
        {
            SaveSystem.AutoSave();
            Restart();
        }

        /// <summary>메뉴·시작 화면의 '종료'. 게임 중이면 자동 저장하고 끕니다.</summary>
        public static void QuitGame()
        {
            SaveSystem.AutoSave();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // 창을 닫거나(에디터는 Play 정지) 모바일에서 앱이 내려갈 때도 자동 저장합니다.
        void OnApplicationQuit() => SaveSystem.AutoSave();
        void OnApplicationPause(bool paused) { if (paused) SaveSystem.AutoSave(); }

        IEnumerator Start()
        {
            Game.Reset();
            SaveSystem.BeginSession(false);

            // 0) 언어 — 데이터보다 먼저. 시트를 읽는 순간부터 표시명이 언어를 봅니다.
            Loc.LoadSetting();

            // 1) 데이터
            var db = GameDatabase.Load();
            Game.Db = db;
            Debug.Log($"[Boot] 데이터 로드 완료 — {db.Summary()}");

            if (ValidateOnStart) ReportValidation(db);

            // 2) 상태
            Game.Flags = new FlagManager(db);
            Game.Evidence = new EvidenceInventory();

            // 3) UI (매니저보다 먼저 — DialogueRunner 가 Awake 에서 UI 이벤트를 구독합니다)
            var uiGo = new GameObject("UIRoot");
            uiGo.transform.SetParent(transform, false);
            var ui = uiGo.AddComponent<UIRoot>();
            ui.Build();
            Game.UI = ui;

            EnsureCamera();

            // 4) 매니저
            var managers = new GameObject("Managers");
            managers.transform.SetParent(transform, false);

            var dialogue = managers.AddComponent<SevenDoctors.Dialogue.DialogueRunner>();
            dialogue.CharInterval = CharInterval;
            Game.Dialogue = dialogue;

            Game.Room = managers.AddComponent<SevenDoctors.Room.RoomController>();
            Game.Puzzle = managers.AddComponent<SevenDoctors.Puzzle.PuzzleDirector>();
            var audio = managers.AddComponent<SevenDoctors.Audio.AudioManager>();
            audio.BgmVolume   = BgmVolume;
            audio.SfxVolume   = SfxVolume;
            audio.VoiceVolume = VoiceVolume;
            Game.Audio = audio;

            // 힌트 담당은 Room/Puzzle 이 만들어진 뒤에 붙입니다 — OnEnable 에서
            // 그 둘의 이벤트를 구독하기 때문입니다.
            Game.Hints = managers.AddComponent<SevenDoctors.Hint.HintDirector>();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // F1 — 퍼즐 미리보기. 정식 빌드에는 클래스 자체가 없습니다.
            _dev = managers.AddComponent<SevenDoctors.Dev.DevPuzzleMenu>();
#endif

            // 5) 배선
            ui.NotebookButton.onClick.AddListener(() =>
            {
                if (Game.State == GameState.Exploring || ui.Notebook.IsVisible) ui.Notebook.Toggle();
            });
            ui.MapButton.onClick.AddListener(ToggleMap);
            ui.LogButton.onClick.AddListener(ToggleLog);
            ui.MenuButton.onClick.AddListener(ToggleMenu);
            // 대화·퍼즐이 끝나 탐색으로 돌아올 때마다 그 순간을 기억합니다 (종료 시 자동 저장용).
            Game.StateChanged += (_, next) => { if (next == GameState.Exploring) SaveSystem.NoteSafePoint(); };
            ui.Notebook.AskRequested += OnAskRequested;
            ui.RefreshEvidenceCount();

            if (ui.Bot != null)
            {
                ui.Bot.AskRequested += OnHintRequested;
                Game.Hints.StuckChanged += ui.Bot.SetStuck;
            }

            yield return null; // DialogueRunner.Awake 가 돌 기회를 줍니다

            // 6) 시작 화면
            string startRoom = ResolveStartRoom(db);
            if (string.IsNullOrEmpty(startRoom))
            {
                Debug.LogError("[Boot] Rooms 탭이 비어 있습니다. Resources/Data/Rooms.csv 를 확인하세요.");
                yield break;
            }

            Loc.Changed += OnLanguageChanged;

            var mode = StartMode.NewGame;
            if (_pendingStart.HasValue)
            {
                // 엔딩 카드의 '분기점으로' — 시작 화면 없이 바로.
                mode = _pendingStart.Value;
                _pendingStart = null;
            }
            else if (ShowTitleOnStart && ui.Title != null)
            {
                Game.State = GameState.Title;
                ui.Title.Show();

                StartMode? picked = null;
                Action<StartMode> onStart = m => picked = m;
                ui.Title.StartRequested += onStart;
                while (picked == null) yield return null;
                ui.Title.StartRequested -= onStart;

                ui.Settings?.Hide();
                ui.Title.Hide();
                mode = picked.Value;
            }

            EnterGame(mode, startRoom, db);
        }

        /// <summary>
        /// 새 게임 / 이어하기 / 엔딩 분기점. 세이브를 못 읽으면 새 게임으로 들어갑니다.
        ///
        /// 새 게임은 시작 대화(프롤로그)가 끝날 때까지 자동 저장할 순간을 잡지 않습니다 — 연구실에
        /// 서 있기만 한 상태가 저장되면, 불러왔을 때 프롤로그 없이 연구실에 덩그러니 서게 됩니다.
        /// 분기점도 이어 붙인 대화가 끝날 때까지 잡지 않습니다 — 그래서 분기점에서 엔딩을 보다가
        /// 종료해도 이어하기 칸은 그대로 남습니다.
        /// </summary>
        void EnterGame(StartMode mode, string startRoom, GameDatabase db)
        {
            // 타이틀 화면 위에 로봇이 떠 있으면 안 되므로 여기서 켭니다.
            Game.UI.Bot?.Show();

            var save = SaveSystem.Load(mode);
            if (mode != StartMode.NewGame && save == null)
            {
                Debug.LogWarning($"[Boot] {mode} 세이브를 읽지 못해 새 게임으로 시작합니다.");
                mode = StartMode.NewGame;
            }

            if (mode == StartMode.NewGame)
            {
                Game.Room.Enter(startRoom);
                if (!string.IsNullOrEmpty(StartDialogueId) && db.GetDialogue(StartDialogueId) != null)
                    Game.Dialogue.Play(StartDialogueId, SaveSystem.EnableSafePoints);
                else
                    SaveSystem.EnableSafePoints();
                return;
            }

            SaveSystem.Apply(save);
            Game.UI.RefreshEvidenceCount();

            if (!save.Resumes) SaveSystem.EnableSafePoints();   // 방에 들어서는 순간이 곧 저장할 순간
            Game.Room.Enter(save.roomId);
            if (Game.Room.CurrentRoomId != save.roomId)
            {
                // 시트가 바뀌어 그 방이 없어졌거나 잠겼으면 시작 방으로.
                Debug.LogWarning($"[Boot] 세이브의 방 '{save.roomId}' 에 들어갈 수 없어 시작 방으로 갑니다.");
                Game.Room.Enter(startRoom);
            }

            if (save.Resumes)
            {
                Game.Dialogue.Play(save.resumeDialogue, save.resumeLine, SaveSystem.EnableSafePoints);
                Debug.Log($"[Boot] 엔딩 분기점에서 시작 — {save.resumeDialogue}:{save.resumeLine}");
            }
            else
            {
                Debug.Log($"[Boot] 이어하기 — {save.roomId} ({save.savedAt})");
            }
        }

        void OnDestroy()
        {
            Loc.Changed -= OnLanguageChanged;

            if (Game.UI != null && Game.UI.Bot != null) Game.UI.Bot.AskRequested -= OnHintRequested;
            if (Game.Hints != null && Game.UI != null && Game.UI.Bot != null)
                Game.Hints.StuckChanged -= Game.UI.Bot.SetStuck;
        }

        /// <summary>도우미 로봇을 눌렀을 때. 지금 상황에 맞는 힌트를 받아 말풍선에 띄웁니다.</summary>
        void OnHintRequested()
        {
            if (Game.Hints == null || Game.UI == null || Game.UI.Bot == null) return;
            Game.UI.Bot.Say(Game.Hints.Ask());
        }

        /// <summary>
        /// 언어가 바뀌면 이미 그려 둔 글자는 그대로 남습니다. 화면에 보이는 것들을
        /// 다시 그려야 합니다. 시트에서 오는 말은 행 객체가 알아서 바뀌므로,
        /// 여기서는 '다시 그리라'고만 시키면 됩니다.
        /// </summary>
        void OnLanguageChanged()
        {
            var ui = Game.UI;
            if (ui == null) return;

            ui.Title?.Refresh();
            ui.Bot?.HideBubble();
            ui.RelocalizeChrome();
            ui.RefreshEvidenceCount();

            if (Game.Room != null && !string.IsNullOrEmpty(Game.Room.CurrentRoomId))
            {
                if (Game.Db.Rooms.TryGetValue(Game.Room.CurrentRoomId, out var room))
                    ui.SetRoomLabel(room.DisplayName);
                Game.Room.RebuildHotspots();
            }

            if (ui.Notebook != null)
            {
                ui.Notebook.Relocalize();
                // 열려 있으면 증거 목록째로 다시 그립니다.
                if (ui.Notebook.IsVisible) ui.Notebook.Show();
            }
        }

        string ResolveStartRoom(GameDatabase db)
        {
            if (!string.IsNullOrEmpty(StartRoomId) && db.Rooms.ContainsKey(StartRoomId)) return StartRoomId;

            if (!string.IsNullOrEmpty(StartRoomId))
                Debug.LogWarning($"[Boot] 시작 방 '{StartRoomId}' 을 찾지 못해 첫 번째 방으로 들어갑니다.");

            foreach (var kv in db.Rooms) return kv.Key;
            return null;
        }

        static void ReportValidation(GameDatabase db)
        {
            var errors = db.Validate();
            if (errors.Count == 0)
            {
                Debug.Log("[Boot] 시트 무결성 검사 통과 — 깨진 ID 참조 없음.");
                return;
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[Boot] 시트에서 {errors.Count}건의 문제를 찾았습니다:");
            foreach (var e in errors) sb.AppendLine("  · " + e);
            Debug.LogWarning(sb.ToString());
        }

        static void EnsureCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
                cam.orthographic = true;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.04f, 0.04f, 0.06f);
            }

            // 씬 카메라에 AudioListener 가 빠져 있는 경우가 있습니다. 하나도 없으면
            // 어떤 소리도 나지 않는데, 콘솔에는 아무 말도 안 나와서 원인을 찾느라
            // 한참 헤매게 됩니다. 그래서 여기서 확인하고 없으면 붙입니다.
            if (UnityEngine.Object.FindFirstObjectByType<AudioListener>() == null)
            {
                cam.gameObject.AddComponent<AudioListener>();
                Debug.Log("[Boot] 씬에 AudioListener 가 없어 카메라에 붙였습니다.");
            }
        }

        /// <summary>노트북에서 증거를 골라 방 안 인물에게 들이댔을 때.</summary>
        void OnAskRequested(string evidenceId)
        {
            var character = Game.Db.FindCharacterInRoom(Game.Room.CurrentRoomId);
            if (character == null) return;

            var topic = Game.Db.GetAskTopic(character.Id, evidenceId);

            if (topic == null || !Game.Flags.Check(topic.RequiredFlag))
            {
                string evName = Game.Db.Evidences.TryGetValue(evidenceId, out var ev) ? ev.DisplayName : evidenceId;
                Game.UI.Toast(Loc.T("ui.ask.no_reaction", character.DisplayName, evName));
                return;
            }

            Game.Dialogue.Play(topic.DialogueId, () => Game.Room.RebuildHotspots());
        }

        void Update()
        {
            if (Game.UI == null) return;
            var ui = Game.UI;

            // 설정·메뉴·기록창이 떠 있으면 닫는 키만 받습니다. 뒤에서 노트·지도가 따로 열리면 엉킵니다.
            // 설정은 메뉴 위에 뜨므로 먼저 닫습니다 (Esc 한 번에 하나씩).
            if (ui.Settings != null && ui.Settings.IsVisible) { if (WasEscapePressed()) ui.Settings.Hide(); return; }
            if (ui.Menu != null && ui.Menu.IsVisible) { if (WasEscapePressed()) ui.Menu.Hide(); return; }

            var log = ui.Log;
            if (log != null && log.IsVisible)
            {
                if (WasLogKeyPressed() || WasEscapePressed()) log.Hide();
                return;
            }

            if (WasNotebookKeyPressed())
            {
                if (Game.State == GameState.Exploring || Game.UI.Notebook.IsVisible)
                    Game.UI.Notebook.Toggle();
            }
            else if (WasMapKeyPressed()) ToggleMap();
            else if (WasLogKeyPressed()) ToggleLog();
            else if (Game.State == GameState.InDialogue && WasWheelUp()) ToggleLog();   // 대사 중 휠 위로 = 지난 대사 보기
            else if (WasEscapePressed()) OnEscape();
        }

        /// <summary>
        /// Esc — 떠 있는 창이 있으면 그걸 닫고(지도·노트), 아무것도 없으면 메뉴를 엽니다.
        /// 증거 확대·F1 개발자 메뉴는 각자 Esc 로 닫히므로 여기서는 건드리지 않습니다.
        /// </summary>
        void OnEscape()
        {
            var ui = Game.UI;
            if (EvidenceZoomView.IsOpen) return;   // 노트 위에 떠 있으니 확대만 닫혀야 합니다
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_dev != null && _dev.IsOpen) return;
#endif
            if (ui.Map != null && ui.Map.IsVisible) { ui.Map.Hide(); return; }
            if (ui.Notebook != null && ui.Notebook.IsVisible) { ui.Notebook.Hide(); return; }
            ToggleMenu();
        }

        /// <summary>
        /// 게임 중 메뉴. 대사·퍼즐 도중에도 열립니다 (저장만 탐색 중에 됩니다).
        /// 시작·엔딩 화면, 지도 말이 걷는 중에는 열지 않습니다.
        /// </summary>
        static void ToggleMenu()
        {
            var menu = Game.UI != null ? Game.UI.Menu : null;
            if (menu == null) return;
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);

            if (menu.IsVisible) { menu.Hide(); return; }
            if (Game.State == GameState.Title || Game.State == GameState.Boot) return;
            if (Game.UI.Map != null && Game.UI.Map.IsWalking) return;
            Game.UI.Log?.Hide();
            menu.Show();
        }

        /// <summary>
        /// 대화 기록은 탐색·대사·퍼즐 어디서든 엽니다 — 퍼즐 도중에 앞에서 들은 말을 되짚는 게 추리입니다.
        /// 시작·엔딩 화면, 증거 확대 중, 지도 말이 걷는 중에는 열지 않습니다.
        /// </summary>
        static void ToggleLog()
        {
            var log = Game.UI != null ? Game.UI.Log : null;
            if (log == null) return;

            // 버튼을 마우스로 눌렀으면 선택이 버튼에 남아, 다음 스페이스가 버튼을 또 누릅니다.
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);

            if (log.IsVisible) { log.Hide(); return; }
            if (Game.State == GameState.Title || Game.State == GameState.Boot) return;
            if (EvidenceZoomView.IsOpen) return;
            if (Game.UI.Map != null && Game.UI.Map.IsWalking) return;
            log.Show();
        }

        static bool WasLogKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            var kb = UnityEngine.InputSystem.Keyboard.current;
            return kb != null && kb.lKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.L);
#endif
        }

        static bool WasWheelUp()
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            var mouse = UnityEngine.InputSystem.Mouse.current;
            return mouse != null && mouse.scroll.ReadValue().y > 0f;
#else
            return Input.mouseScrollDelta.y > 0f;
#endif
        }

        /// <summary>지도는 탐색 중에만 엽니다. 대사·퍼즐 도중에 방을 옮기면 장면이 붕 뜹니다.</summary>
        static void ToggleMap()
        {
            var map = Game.UI != null ? Game.UI.Map : null;
            if (map == null) return;
            if (map.IsVisible) { map.Hide(); return; }
            if (Game.State == GameState.Exploring) map.Show();
        }

        static bool WasMapKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            var kb = UnityEngine.InputSystem.Keyboard.current;
            return kb != null && kb.mKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.M);
#endif
        }

        static bool WasEscapePressed()
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            var kb = UnityEngine.InputSystem.Keyboard.current;
            return kb != null && kb.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        static bool WasNotebookKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            var kb = UnityEngine.InputSystem.Keyboard.current;
            return kb != null && kb.tabKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Tab);
#endif
        }
    }
}
