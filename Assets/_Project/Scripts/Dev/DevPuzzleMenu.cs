#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using SevenDoctors.Core;
using SevenDoctors.Data;
using SevenDoctors.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SevenDoctors.Dev
{
    /// <summary>
    /// F1 로 여는 개발자용 퍼즐 미리보기.
    ///
    /// 3장 퍼즐 하나 고치려고 앞을 30분 다시 플레이하는 일을 없애려는 것이
    /// 전부입니다. 목록에서 고르면 실제 퍼즐이 그대로 돕니다 — 미리보기 전용
    /// 사본을 따로 두면 그 사본만 고쳐 놓고 본편은 깨진 채 두는 사고가 납니다.
    ///
    /// 대신 진행 상황은 건드리지 않습니다:
    ///   · 들어가기 전에 플래그와 증거를 통째로 복사해 두고
    ///   · 증거를 전부 지급합니다 (Deduction 은 손에 증거가 있어야 카드가 생깁니다)
    ///   · 끝나면 원래대로 되돌립니다
    /// 그래서 퍼즐을 풀든 말든 진행 중이던 플레이에는 아무 일도 일어나지 않습니다.
    ///
    /// 목록 맨 위에는 대화 미리보기(DialoguePreviews)가 붙습니다. 새 인물이나
    /// 포트레이트 연출을 확인하려고 그 방까지 걸어가지 않아도 됩니다. 진행 중인
    /// 대화가 있으면 끊고 틀기 때문에 프롤로그 도중에도 바로 볼 수 있습니다.
    ///
    /// 창을 열지 않고 쓰는 방 바로 가기 키도 여기 있습니다 (HandleRoomKeys):
    /// 숫자 1~9 는 Rooms 시트 순서대로, PageUp/PageDown 은 이전·다음 방.
    ///
    /// 빌드에서는 통째로 빠집니다 — 파일 전체가 UNITY_EDITOR || DEVELOPMENT_BUILD
    /// 안에 들어 있어서 정식 빌드에는 클래스 자체가 존재하지 않습니다.
    /// </summary>
    public class DevPuzzleMenu : MonoBehaviour
    {
        const string LayerName = "DevMenuLayer";

        /// <summary>F1 목록에 올릴 대화. 시트에 없는 id 는 조용히 빠집니다.</summary>
        static readonly (string Id, string Label)[] DialoguePreviews =
        {
            ("dlg_part3", "파트 3 — 의심 (끝나면 지목 퍼즐)"),
            ("dlg_part4", "파트 4 — 지목 → 파트 5"),
            ("dlg_part5", "파트 5 — 진실 → 엔딩 선택 (엔딩 후 처음부터)"),
        };

        RectTransform _root;
        Coroutine _running;

        public bool IsOpen => _root != null;
        public bool IsRunningPuzzle => _running != null;

        void Update()
        {
            if (Game.UI == null || Game.Db == null) return;

            if (WasToggleKeyPressed()) Toggle();
            else if (IsOpen && WasCancelKeyPressed()) Close();
            else if (!IsOpen && !IsRunningPuzzle) HandleRoomKeys();
        }

        // ── 방 바로 가기 ──────────────────────────────────────────────────────
        //
        //   숫자 1~9            Rooms 시트의 위에서부터 차례대로 (지금은 1 연구실 · 2 앞마당 · 3 로비 ·
        //                       4 슬픔 · 5 나태 · 6 탐욕 · 7 분노 · 8 오만 · 9 색욕)
        //   PageUp / PageDown   이전 방 / 다음 방 (10번째부터는 이걸로 — 교만·식당·정원·복도·지하실)
        //
        // 방 하나 보려고 프롤로그부터 다시 걷지 않게 하려는 것입니다. 해금 플래그는 건드리지 않고
        // 잠금 검사만 끕니다 — 그래서 진행 상황은 그대로고, 한 번 쓰고 나면 지도에서도 잠긴 방을 누를 수 있습니다.
        // 대신 조건이 안 맞는 핫스팟(예: 도면을 받기 전의 교만 방 안 교만)은 여전히 안 보입니다.

        void HandleRoomKeys()
        {
            if (Game.Room == null || Game.Db.Rooms.Count == 0) return;
            // 시작 화면·퍼즐 중에는 받지 않습니다. 퍼즐은 화면이 남고, 시작 화면은 아직 방이 없습니다.
            if (Game.State == GameState.Title || Game.State == GameState.InPuzzle) return;

            var rooms = new List<RoomRow>(Game.Db.Rooms.Values);
            int current = rooms.FindIndex(r => r.Id == Game.Room.CurrentRoomId);

            int target = PressedRoomNumber();
            if (target < 0)
            {
                int step = PressedRoomStep();
                if (step == 0) return;
                target = ((current < 0 ? 0 : current) + step + rooms.Count) % rooms.Count;
            }
            if (target >= rooms.Count || target == current) return;

            // 흐르던 대사와 열려 있던 노트는 접습니다. 방만 바뀌고 대사가 남으면 붕 뜹니다.
            if (Game.Dialogue != null && Game.Dialogue.IsPlaying) Game.Dialogue.Stop();
            if (Game.UI.Notebook != null && Game.UI.Notebook.IsVisible) Game.UI.Notebook.Hide();

            Game.Room.IgnoreLocks = true;
            Game.Room.Enter(rooms[target].Id);
            Game.UI.Toast($"개발자 이동 [{target + 1}] {rooms[target].DisplayName}");
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        // ── 목록 ──────────────────────────────────────────────────────────────

        void Open()
        {
            // 퍼즐이 도는 중에 또 열면 퍼즐 위에 목록이 겹쳐서 클릭이 엉킵니다.
            // 시작 화면에서도 막습니다 — 아직 방에 들어가지도 않았습니다.
            if (IsRunningPuzzle || Game.State == GameState.InPuzzle || Game.State == GameState.Title) return;

            _root = UIFactory.Rect(LayerName, Game.UI.Screen);
            UIFactory.Anchor(_root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _root.SetAsLastSibling();   // 대화창·퍼즐창보다 위

            var scrim = UIFactory.Box("Scrim", _root, UIFactory.Scrim);
            scrim.raycastTarget = true;   // 뒤쪽 클릭이 새어 나가지 않게

            var panel = UIFactory.Box("Panel", _root, UIFactory.PanelSolid);
            UIFactory.Anchor(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                             new Vector2(-560, -420), new Vector2(560, 420));

            var title = UIFactory.Label("Title", panel.transform, "개발자 — 대화 · 퍼즐 미리보기", 34,
                                        TextAnchor.UpperLeft, UIFactory.Accent);
            UIFactory.Anchor(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                             new Vector2(40, -84), new Vector2(-40, -28));

            var note = UIFactory.Label("Note", panel.transform,
                "F1 또는 Esc 로 닫기 · 진행 상황(플래그·증거)은 바뀌지 않습니다\n" +
                "방 바로 가기: 숫자 1~9 · PageUp / PageDown (이 창을 닫고)", 22,
                TextAnchor.UpperLeft, UIFactory.InkDim);
            UIFactory.Anchor(note.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                             new Vector2(40, -150), new Vector2(-40, -84));

            var list = UIFactory.Rect("List", panel.transform);
            UIFactory.Anchor(list, new Vector2(0, 0), new Vector2(1, 1),
                             new Vector2(40, 88), new Vector2(-40, -162));
            UIFactory.VLayout(list, 10, new RectOffset(0, 0, 0, 0), TextAnchor.UpperCenter);

            foreach (var preview in DialoguePreviews)
                if (Game.Db.GetDialogue(preview.Id) != null)
                    AddDialogueRow(list, preview.Id, preview.Label);

            foreach (var kv in Game.Db.Puzzles)
                AddRow(list, kv.Value);

            var close = UIFactory.Btn("Close", panel.transform, "닫기 (Esc)", 26,
                                      new Color(0.16f, 0.15f, 0.11f), UIFactory.Accent);
            UIFactory.Anchor(close.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                             new Vector2(-130, 24), new Vector2(130, 76));
            close.onClick.AddListener(Close);
        }

        void AddDialogueRow(RectTransform list, string dialogueId, string label)
        {
            var btn = UIFactory.Btn($"Dlg_{dialogueId}", list, $"[대화] {label}", 26,
                                    new Color(0.11f, 0.14f, 0.20f), UIFactory.Ink);
            UIFactory.Height(btn.gameObject, 72);
            btn.onClick.AddListener(() => LaunchDialogue(dialogueId));
        }

        void AddRow(RectTransform list, PuzzleRow puzzle)
        {
            string room = Game.Db.Rooms.TryGetValue(puzzle.RoomId, out var r) ? r.DisplayName : puzzle.RoomId;
            bool implemented = IsImplemented(puzzle.Type);

            // 미구현 타입은 눌러도 통과 처리만 되므로 미리 표시해 둡니다.
            string caption = implemented
                ? $"[{puzzle.Type}] {puzzle.Title}   ·   {room}"
                : $"[{puzzle.Type}] {puzzle.Title}   ·   {room}   (엔진 미구현)";

            var btn = UIFactory.Btn($"Pzl_{puzzle.Id}", list, caption, 26,
                                    new Color(0.16f, 0.15f, 0.11f),
                                    implemented ? UIFactory.Ink : UIFactory.InkDim);
            UIFactory.Height(btn.gameObject, 72);

            string id = puzzle.Id;
            btn.onClick.AddListener(() => Launch(id));
        }

        /// <summary>PuzzleDirector 가 실제로 돌릴 수 있는 타입인지. 목록 표시에만 씁니다.</summary>
        static bool IsImplemented(string type)
        {
            switch ((type ?? "").Trim().ToLowerInvariant())
            {
                case "code":
                case "codelock":
                case "deduction":
                case "slide":
                case "sliding": return true;
                default: return false;
            }
        }

        public void Close()
        {
            if (_root == null) return;
            Destroy(_root.gameObject);
            _root = null;
        }

        // ── 실행 ──────────────────────────────────────────────────────────────

        void Launch(string puzzleId)
        {
            if (IsRunningPuzzle) return;
            Close();
            _running = StartCoroutine(PreviewRoutine(puzzleId));
        }

        void LaunchDialogue(string dialogueId)
        {
            if (IsRunningPuzzle) return;
            Close();

            // 프롤로그처럼 이미 돌고 있는 대화가 있으면 끊습니다. 끝날 때까지
            // 기다리게 하면 '바로 확인' 이 안 됩니다.
            Game.Dialogue.Stop();
            _running = StartCoroutine(DialoguePreviewRoutine(dialogueId));
        }

        /// <summary>대화를 실제로 틀고, 끝나면 플래그·증거를 되돌립니다. 퍼즐 미리보기와 같은 규칙입니다.</summary>
        IEnumerator DialoguePreviewRoutine(string dialogueId)
        {
            var savedFlags = new List<string>(Game.Flags.ActiveFlags);
            var savedEvidence = new List<string>(Game.Evidence.Owned);

            bool done = false;
            Game.Dialogue.Play(dialogueId, () => done = true);
            while (!done) yield return null;

            Game.Flags.RestoreFrom(savedFlags);
            Game.Evidence.RestoreFrom(savedEvidence);
            Game.UI.RefreshEvidenceCount();
            Game.Room?.RebuildHotspots();

            Game.UI.Toast("미리보기: 대화 끝 — 진행 상황은 그대로입니다");
            _running = null;
        }

        /// <summary>
        /// 퍼즐을 실제로 돌리되, 앞뒤로 진행 상황을 떠 두고 되돌립니다.
        /// 성공 플래그도 성공 대사도 본편과 똑같이 나오지만 흔적은 남지 않습니다.
        /// </summary>
        IEnumerator PreviewRoutine(string puzzleId)
        {
            var savedFlags = new List<string>(Game.Flags.ActiveFlags);
            var savedEvidence = new List<string>(Game.Evidence.Owned);
            var savedRoom = Game.Room != null ? Game.Room.CurrentRoomId : null;

            // Deduction 은 손에 든 증거로 후보 카드를 만듭니다. 초반에 띄우면
            // 카드가 하나도 없어서 풀 수가 없으므로 전부 쥐여 줍니다.
            // Add 대신 RestoreFrom 을 쓰는 건 획득 로그를 줄줄이 찍지 않기 위해서입니다.
            Game.Evidence.RestoreFrom(new List<string>(Game.Db.Evidences.Keys));
            Game.UI.RefreshEvidenceCount();

            yield return Game.Puzzle.RunRoutine(puzzleId);

            bool solved = Game.Puzzle.LastResultSuccess;

            // 성공 대사가 방을 옮겼거나 대화창을 열어 둔 채 끝났을 수 있습니다.
            Game.UI.Dialogue.Hide();
            Game.UI.Choices.Hide();

            Game.Flags.RestoreFrom(savedFlags);
            Game.Evidence.RestoreFrom(savedEvidence);
            Game.UI.RefreshEvidenceCount();

            if (!string.IsNullOrEmpty(savedRoom) && Game.Room != null &&
                Game.Room.CurrentRoomId != savedRoom)
                Game.Room.Enter(savedRoom);
            else
                Game.Room?.RebuildHotspots();

            Game.State = GameState.Exploring;
            Game.UI.Toast(solved ? "미리보기: 성공 — 진행 상황은 그대로입니다"
                                 : "미리보기: 종료 — 진행 상황은 그대로입니다");
            _running = null;
        }

        // ── 입력 ──────────────────────────────────────────────────────────────
        // GameBootstrap 의 노트북 키와 같은 방식입니다 — 두 입력 시스템 다 받습니다.

        static bool WasToggleKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            var kb = UnityEngine.InputSystem.Keyboard.current;
            return kb != null && kb.f1Key.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.F1);
#endif
        }

        /// <summary>이번 프레임에 눌린 숫자 키를 방 번호(0부터)로. 없으면 -1. 숫자패드도 받습니다.</summary>
        static int PressedRoomNumber()
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return -1;
            var row = new[] { kb.digit1Key, kb.digit2Key, kb.digit3Key, kb.digit4Key, kb.digit5Key,
                              kb.digit6Key, kb.digit7Key, kb.digit8Key, kb.digit9Key };
            var pad = new[] { kb.numpad1Key, kb.numpad2Key, kb.numpad3Key, kb.numpad4Key, kb.numpad5Key,
                              kb.numpad6Key, kb.numpad7Key, kb.numpad8Key, kb.numpad9Key };
            for (int i = 0; i < 9; i++)
                if (row[i].wasPressedThisFrame || pad[i].wasPressedThisFrame) return i;
            return -1;
#else
            for (int i = 0; i < 9; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i)) return i;
            return -1;
#endif
        }

        /// <summary>PageDown 은 +1(다음 방), PageUp 은 -1(이전 방), 아니면 0.</summary>
        static int PressedRoomStep()
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return 0;
            if (kb.pageDownKey.wasPressedThisFrame) return 1;
            if (kb.pageUpKey.wasPressedThisFrame) return -1;
            return 0;
#else
            if (Input.GetKeyDown(KeyCode.PageDown)) return 1;
            if (Input.GetKeyDown(KeyCode.PageUp)) return -1;
            return 0;
#endif
        }

        static bool WasCancelKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            var kb = UnityEngine.InputSystem.Keyboard.current;
            return kb != null && kb.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }
    }
}
#endif
