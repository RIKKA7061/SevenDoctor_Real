using System;
using SevenDoctors.Core;
using SevenDoctors.Data;
using UnityEngine;

namespace SevenDoctors.Hint
{
    /// <summary>
    /// 플레이어가 막혔는지 지켜보다가 도우미 로봇에게 알려 줍니다.
    ///
    /// '막혔다' 를 시간만으로 재면 안 됩니다. 대사를 읽거나 방을 구경하는
    /// 사람까지 막힌 걸로 잡습니다. 그래서 두 가지를 따로 봅니다.
    ///
    ///   탐색 중 — 핫스팟을 누르긴 하는데 플래그도 증거도 안 늘어남.
    ///             이야기가 안 나아가는 채로 사물만 뒤지고 있다는 뜻입니다.
    ///   퍼즐 중 — 오래 붙들고 있거나, 여러 번 틀림.
    ///
    /// 어느 쪽이든 '막혔다' 가 되면 로봇이 안테나를 반짝일 뿐, 힌트를 들이밀지는
    /// 않습니다. 묻지도 않았는데 답을 말해 버리면 푸는 재미가 사라집니다.
    /// </summary>
    public class HintDirector : MonoBehaviour
    {
        [Header("탐색 중 판정")]
        [Tooltip("이야기가 안 나아간 채로 이만큼 누르면 막힌 것으로 봅니다.")]
        public int BarrenClicksToStuck = 4;

        [Tooltip("아무것도 안 하고 이만큼 지나도 막힌 것으로 봅니다(초).")]
        public float IdleSecondsToStuck = 90f;

        [Header("퍼즐 중 판정")]
        [Tooltip("퍼즐을 이만큼 붙들고 있으면 막힌 것으로 봅니다(초).")]
        public float PuzzleSecondsToStuck = 60f;

        [Tooltip("이만큼 틀리면 시간과 상관없이 막힌 것으로 봅니다.")]
        public int WrongAttemptsToStuck = 2;

        /// <summary>막혔는지 여부가 바뀔 때 울립니다. 로봇이 이걸 보고 안테나를 켭니다.</summary>
        public event Action<bool> StuckChanged;

        public bool IsStuck { get; private set; }

        int _barrenClicks;
        float _idleSeconds;
        float _puzzleSeconds;
        string _watchedPuzzle;

        // 같은 상황에서 몇 번째로 묻는지. 물을 때마다 한 단계씩 더 알려 줍니다.
        int _askStep = 1;
        string _askSituation;

        void OnEnable()
        {
            if (Game.Flags != null) Game.Flags.FlagChanged += OnFlagChanged;
            if (Game.Evidence != null) Game.Evidence.EvidenceAdded += OnEvidenceAdded;
            if (Game.Room != null) Game.Room.Interacted += OnInteracted;
        }

        void OnDisable()
        {
            if (Game.Flags != null) Game.Flags.FlagChanged -= OnFlagChanged;
            if (Game.Evidence != null) Game.Evidence.EvidenceAdded -= OnEvidenceAdded;
            if (Game.Room != null) Game.Room.Interacted -= OnInteracted;
        }

        void OnFlagChanged(string flag, bool value) { if (value) MarkProgress(); }
        void OnEvidenceAdded(string evidenceId) => MarkProgress();

        /// <summary>
        /// 이야기가 한 칸 나아갔습니다. 세던 것을 전부 되돌립니다.
        ///
        /// 힌트 단계까지 되돌리는 게 중요합니다 — 안 그러면 1장에서 세 번 물어본
        /// 사람이 2장 첫 힌트부터 정답을 듣게 됩니다.
        /// </summary>
        public void MarkProgress()
        {
            _barrenClicks = 0;
            _idleSeconds = 0f;
            _askStep = 1;
            _askSituation = null;
            SetStuck(false);
        }

        void OnInteracted(HotspotRow h)
        {
            _idleSeconds = 0f;

            // 방을 옮기는 건 '뒤지는 중' 이 아니라 이동입니다. 세지 않습니다.
            if (h != null && h.Type == "move") return;

            _barrenClicks++;
            if (_barrenClicks >= BarrenClicksToStuck) SetStuck(true);
        }

        void Update()
        {
            if (Game.State == GameState.InPuzzle)
            {
                string id = Game.Puzzle != null ? Game.Puzzle.CurrentPuzzleId : null;
                if (id != _watchedPuzzle) { _watchedPuzzle = id; _puzzleSeconds = 0f; }

                _puzzleSeconds += Time.deltaTime;

                bool tooLong = _puzzleSeconds >= PuzzleSecondsToStuck;
                bool tooManyWrong = Game.Puzzle != null && Game.Puzzle.WrongAttempts >= WrongAttemptsToStuck;
                if (tooLong || tooManyWrong) SetStuck(true);
                return;
            }

            if (_watchedPuzzle != null) { _watchedPuzzle = null; _puzzleSeconds = 0f; }

            // 대사가 흐르는 동안은 세지 않습니다. 읽는 시간은 막힌 시간이 아닙니다.
            if (Game.State != GameState.Exploring) return;

            _idleSeconds += Time.deltaTime;
            if (_idleSeconds >= IdleSecondsToStuck) SetStuck(true);
        }

        void SetStuck(bool value)
        {
            if (IsStuck == value) return;
            IsStuck = value;
            StuckChanged?.Invoke(value);
        }

        /// <summary>
        /// 로봇을 눌렀을 때 읽어 줄 힌트. 같은 상황에서 다시 물으면 한 단계 더 자세해집니다.
        /// 힌트를 받았다고 해서 막힘 표시를 끄지는 않습니다 — 한 번 읽고도 못 풀 수 있습니다.
        /// </summary>
        public string Ask()
        {
            string puzzleId = Game.State == GameState.InPuzzle && Game.Puzzle != null
                ? Game.Puzzle.CurrentPuzzleId : null;

            // 퍼즐과 탐색은 서로 다른 상황입니다. 오가면 단계를 처음부터 셉니다.
            string situation = puzzleId ?? "explore";
            if (situation != _askSituation) { _askSituation = situation; _askStep = 1; }

            var hint = Game.Db != null ? Game.Db.FindHint(puzzleId, _askStep, Game.Flags) : null;

            // 퍼즐에 전용 힌트를 안 적어 뒀으면 Puzzles 탭의 힌트텍스트로 버팁니다.
            if (hint == null && !string.IsNullOrEmpty(puzzleId) &&
                Game.Db.Puzzles.TryGetValue(puzzleId, out var puzzle) &&
                !string.IsNullOrEmpty(puzzle.Hint))
            {
                _askStep++;
                return puzzle.Hint;
            }

            if (hint == null) return Loc.T("ui.hint.none");

            _askStep++;
            return hint.Text;
        }
    }
}
