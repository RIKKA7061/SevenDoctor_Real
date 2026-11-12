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

        [Tooltip("켜두면 마우스를 올린 곳에 따라 커서가 돋보기·손·말풍선으로 바뀝니다. 끄면 늘 화살표 — 누를 곳을 눈으로 찾게 하고 싶을 때 끕니다.")]
        public bool ContextCursors = true;

        // AudioManager 는 실행할 때 코드로 붙이기 때문에, 재생 전에는 인스펙터에
        // 뜨지 않습니다. 여기 있으면 껐다 켜지 않고도 미리 맞춰 둘 수 있습니다.
        [Header("소리 크기")]
        [Tooltip("배경음")]
        [Range(0f, 1f)] public float BgmVolume = 0.50f;

        [Tooltip("효과음")]
        [Range(0f, 1f)] public float SfxVolume = 0.55f;

        [Tooltip("말할 때 나는 소리")]
        [Range(0f, 1f)] public float VoiceVolume = 0.40f;

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
        ///
        /// 세이브 파일은 여기서 건드리지 않습니다. 엔딩 카드가 뜰 때 이미 지워졌고(SaveDirector.OnEndingReached),
        /// 다시 뜨는 시작 화면은 디스크에 있는 것만 보고 '이어 하기' 를 낼지 정합니다.
        /// </summary>
        public static void Restart()
        {
            var current = FindFirstObjectByType<GameBootstrap>();
            if (current != null) Destroy(current.gameObject);
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }

        IEnumerator Start()
        {
            Game.Reset();

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

            // 자동 저장도 같은 이유로 여기서 — 플래그·증거·방의 이벤트를 듣고 있다가
            // 탐색 상태로 돌아올 때 씁니다. 실제로 쓰기 시작하는 건 게임에 들어간 뒤(Begin)입니다.
            Game.Save = managers.AddComponent<SevenDoctors.Save.SaveDirector>();

            // 마우스 커서 — 올려놓은 곳이 무엇을 하는 곳인지에 따라 모양이 바뀝니다.
            managers.AddComponent<GameCursor>().ContextCursors = ContextCursors;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // F1 — 퍼즐 미리보기. 정식 빌드에는 클래스 자체가 없습니다.
            managers.AddComponent<SevenDoctors.Dev.DevPuzzleMenu>();
#endif

            // 5) 배선
            ui.NotebookButton.onClick.AddListener(() => ToggleNotebook());
            ui.MapButton.onClick.AddListener(ToggleMap);
            ui.Notebook.AskRequested += OnAskRequested;
            ui.RefreshEvidenceCount();

            if (ui.Bot != null)
            {
                ui.Bot.AskRequested += OnHintRequested;
                ui.Bot.InventoryRequested += OnInventoryRequested;
                ui.Bot.CanOpenInventory = CanOpenNotebook;
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

            // 세이브는 시작 화면을 띄우기 전에 읽어 봅니다. 없거나 깨졌으면 null 이고,
            // 그러면 '이어 하기' 버튼이 아예 안 나옵니다 — 눌러 보고 나서야 안 된다고 하지 않습니다.
            var saved = Game.Save.Load();
            bool resume = false;

            if (ShowTitleOnStart && ui.Title != null)
            {
                Game.State = GameState.Title;

                // 버튼에 방 이름을 같이 적습니다. 어디서 이어지는지 알아야 누를지 말지 정할 수 있습니다.
                // 함수로 넘기는 건 시작 화면에서 언어를 바꾸면 방 이름도 따라 바뀌어야 해서입니다.
                ui.Title.SetContinue(saved != null, () =>
                    db.Rooms.TryGetValue(saved.RoomId, out var room) ? room.DisplayName : null);
                ui.Title.Show();

                bool started = false;
                Action onStart = () => started = true;
                Action onContinue = () => { resume = true; started = true; };
                ui.Title.StartRequested += onStart;
                ui.Title.ContinueRequested += onContinue;
                while (!started) yield return null;
                ui.Title.StartRequested -= onStart;
                ui.Title.ContinueRequested -= onContinue;

                ui.Title.Hide();
            }

            // 시작 화면을 꺼 둔 경우(ShowTitleOnStart)는 늘 새 게임입니다. 대사를 손보려고 끄는 옵션이라
            // 처음부터 나오는 게 맞습니다.
            if (resume) ContinueGame(saved, startRoom);
            else EnterGame(startRoom, db);
        }

        /// <summary>새 게임. 옛 세이브가 있었다면 여기서 지웁니다 (시작 화면이 한 번 더 물어본 뒤입니다).</summary>
        void EnterGame(string startRoom, GameDatabase db)
        {
            Game.Save.DeleteSave();
            Game.Save.Begin(null);

            // 타이틀 화면 위에 로봇이 떠 있으면 안 되므로 여기서 켭니다.
            Game.UI.Bot?.Show();

            Game.Room.Enter(startRoom);

            if (!string.IsNullOrEmpty(StartDialogueId) && db.GetDialogue(StartDialogueId) != null)
                Game.Dialogue.Play(StartDialogueId);
        }

        /// <summary>
        /// 이어 하기. 플래그·증거·소모 핫스팟을 채운 뒤에 방에 들어갑니다 — 순서가 바뀌면
        /// 방의 해금 검사와 핫스팟 조건이 빈 상태를 보고 판단합니다. 시작 대사는 틀지 않습니다.
        /// </summary>
        void ContinueGame(SevenDoctors.Save.SaveData saved, string startRoom)
        {
            string room = Game.Save.Restore(saved, startRoom);
            Game.Save.Begin(saved);

            Game.UI.RefreshEvidenceCount();
            Game.UI.Bot?.Show();
            Game.Room.Enter(room);
        }

        void OnDestroy()
        {
            Loc.Changed -= OnLanguageChanged;

            if (Game.UI != null && Game.UI.Bot != null)
            {
                Game.UI.Bot.AskRequested -= OnHintRequested;
                Game.UI.Bot.InventoryRequested -= OnInventoryRequested;
            }
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
        /// 도우미 로봇 창에서 '인벤토리' 를 골랐을 때. 지금까지 얻은 단서를 보여 줍니다 — 증거 노트를 엽니다.
        /// 노트를 열 수 없는 때(대사·퍼즐 도중)에는 창의 버튼이 흐려져 있어서 여기까지 오지 않습니다.
        /// </summary>
        void OnInventoryRequested()
        {
            var notebook = Game.UI != null ? Game.UI.Notebook : null;
            if (notebook != null && !notebook.IsVisible && CanOpenNotebook()) notebook.Show();
        }

        /// <summary>노트는 탐색 중에만 엽니다 (닫는 건 언제든). 로봇 창의 버튼도 이걸 보고 흐려집니다.</summary>
        static bool CanOpenNotebook()
        {
            var notebook = Game.UI != null ? Game.UI.Notebook : null;
            return notebook != null && (notebook.IsVisible || Game.State == GameState.Exploring);
        }

        /// <summary>
        /// 증거 노트 여닫기. 상단 버튼·Tab 이 이걸 부릅니다.
        /// 여는 건 탐색 중에만, 닫는 건 언제든 됩니다. 여닫았으면 true.
        /// </summary>
        static bool ToggleNotebook()
        {
            if (!CanOpenNotebook()) return false;
            Game.UI.Notebook.Toggle();
            return true;
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

            // 로봇 창이 떠 있으면 키보드는 그 창만 봅니다. Esc 로 닫고, Tab·M 은 창 뒤에서 몰래 열리지 않게 막습니다.
            if (Game.UI.Bot != null && Game.UI.Bot.IsMenuOpen)
            {
                if (WasEscapePressed()) Game.UI.Bot.CloseMenu();
                return;
            }

            if (WasNotebookKeyPressed()) ToggleNotebook();
            else if (WasMapKeyPressed()) ToggleMap();
            else if (Game.UI.Map != null && Game.UI.Map.IsVisible && WasEscapePressed()) Game.UI.Map.Hide();
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
