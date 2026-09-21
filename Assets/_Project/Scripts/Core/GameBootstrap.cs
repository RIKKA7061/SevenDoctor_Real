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

        void Awake()
        {
            // 방 전환 시에도 살아남아야 하므로 (씬은 하나지만, 타이틀/엔딩 씬을 붙일 때를 대비)
            DontDestroyOnLoad(gameObject);
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
            Game.Audio = managers.AddComponent<SevenDoctors.Audio.AudioManager>();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // F1 — 퍼즐 미리보기. 정식 빌드에는 클래스 자체가 없습니다.
            managers.AddComponent<SevenDoctors.Dev.DevPuzzleMenu>();
#endif

            // 5) 배선
            ui.NotebookButton.onClick.AddListener(() =>
            {
                if (Game.State == GameState.Exploring || ui.Notebook.IsVisible) ui.Notebook.Toggle();
            });
            ui.Notebook.AskRequested += OnAskRequested;
            ui.RefreshEvidenceCount();

            yield return null; // DialogueRunner.Awake 가 돌 기회를 줍니다

            // 6) 시작 화면
            string startRoom = ResolveStartRoom(db);
            if (string.IsNullOrEmpty(startRoom))
            {
                Debug.LogError("[Boot] Rooms 탭이 비어 있습니다. Resources/Data/Rooms.csv 를 확인하세요.");
                yield break;
            }

            Loc.Changed += OnLanguageChanged;

            if (ShowTitleOnStart && ui.Title != null)
            {
                Game.State = GameState.Title;
                ui.Title.Show();

                bool started = false;
                Action onStart = () => started = true;
                ui.Title.StartRequested += onStart;
                while (!started) yield return null;
                ui.Title.StartRequested -= onStart;

                ui.Title.Hide();
            }

            EnterGame(startRoom, db);
        }

        void EnterGame(string startRoom, GameDatabase db)
        {
            Game.Room.Enter(startRoom);

            if (!string.IsNullOrEmpty(StartDialogueId) && db.GetDialogue(StartDialogueId) != null)
                Game.Dialogue.Play(StartDialogueId);
        }

        void OnDestroy()
        {
            Loc.Changed -= OnLanguageChanged;
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
            if (WasNotebookKeyPressed())
            {
                if (Game.State == GameState.Exploring || Game.UI.Notebook.IsVisible)
                    Game.UI.Notebook.Toggle();
            }
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
