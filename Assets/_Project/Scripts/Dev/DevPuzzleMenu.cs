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
    /// 빌드에서는 통째로 빠집니다 — 파일 전체가 UNITY_EDITOR || DEVELOPMENT_BUILD
    /// 안에 들어 있어서 정식 빌드에는 클래스 자체가 존재하지 않습니다.
    /// </summary>
    public class DevPuzzleMenu : MonoBehaviour
    {
        const string LayerName = "DevMenuLayer";

        RectTransform _root;
        Coroutine _running;

        public bool IsOpen => _root != null;
        public bool IsRunningPuzzle => _running != null;

        void Update()
        {
            if (Game.UI == null || Game.Db == null) return;

            if (WasToggleKeyPressed()) Toggle();
            else if (IsOpen && WasCancelKeyPressed()) Close();
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
            if (Game.Db.Puzzles.Count == 0)
            {
                Game.UI.Toast("Puzzles 시트가 비어 있습니다.");
                return;
            }

            _root = UIFactory.Rect(LayerName, Game.UI.Screen);
            UIFactory.Anchor(_root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _root.SetAsLastSibling();   // 대화창·퍼즐창보다 위

            var scrim = UIFactory.Box("Scrim", _root, UIFactory.Scrim);
            scrim.raycastTarget = true;   // 뒤쪽 클릭이 새어 나가지 않게

            var panel = UIFactory.Box("Panel", _root, UIFactory.PanelSolid);
            UIFactory.Anchor(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                             new Vector2(-560, -420), new Vector2(560, 420));

            var title = UIFactory.Label("Title", panel.transform, "개발자 — 퍼즐 미리보기", 34,
                                        TextAnchor.UpperLeft, UIFactory.Accent);
            UIFactory.Anchor(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                             new Vector2(40, -84), new Vector2(-40, -28));

            var note = UIFactory.Label("Note", panel.transform,
                "F1 또는 Esc 로 닫기 · 진행 상황(플래그·증거)은 바뀌지 않습니다", 22,
                TextAnchor.UpperLeft, UIFactory.InkDim);
            UIFactory.Anchor(note.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                             new Vector2(40, -118), new Vector2(-40, -84));

            var list = UIFactory.Rect("List", panel.transform);
            UIFactory.Anchor(list, new Vector2(0, 0), new Vector2(1, 1),
                             new Vector2(40, 88), new Vector2(-40, -130));
            UIFactory.VLayout(list, 10, new RectOffset(0, 0, 0, 0), TextAnchor.UpperCenter);

            foreach (var kv in Game.Db.Puzzles)
                AddRow(list, kv.Value);

            var close = UIFactory.Btn("Close", panel.transform, "닫기 (Esc)", 26,
                                      new Color(0.16f, 0.15f, 0.11f), UIFactory.Accent);
            UIFactory.Anchor(close.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                             new Vector2(-130, 24), new Vector2(130, 76));
            close.onClick.AddListener(Close);
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
                case "deduction": return true;
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
