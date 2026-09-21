using System;
using System.Collections.Generic;
using SevenDoctors.Data;
using UnityEngine;

namespace SevenDoctors.Core
{
    public enum GameState { Boot, Title, Exploring, InDialogue, InPuzzle }

    /// <summary>
    /// 전역 접근점. 싱글톤 10개 대신 이거 하나만 씁니다.
    /// 초기화 순서 버그는 대부분 싱글톤을 여러 개 두는 데서 나옵니다.
    /// </summary>
    public static class Game
    {
        public static GameDatabase        Db        { get; internal set; }
        public static FlagManager         Flags     { get; internal set; }
        public static EvidenceInventory   Evidence  { get; internal set; }
        public static SevenDoctors.Dialogue.DialogueRunner Dialogue { get; internal set; }
        public static SevenDoctors.Room.RoomController Room { get; internal set; }
        public static SevenDoctors.Puzzle.PuzzleDirector Puzzle { get; internal set; }
        public static SevenDoctors.UI.UIRoot UI { get; internal set; }

        public static bool IsReady => Db != null && Flags != null;

        static GameState _state = GameState.Boot;

        /// <summary>상태가 바뀔 때마다 호출됩니다. (이전 상태, 새 상태)</summary>
        public static event Action<GameState, GameState> StateChanged;

        public static GameState State
        {
            get => _state;
            set
            {
                if (_state == value) return;
                var prev = _state;
                _state = value;
                StateChanged?.Invoke(prev, _state);
            }
        }

        /// <summary>
        /// 탐색 중일 때만 핫스팟 클릭을 받습니다.
        /// 이걸 안 막으면 대화 도중에 방이 바뀌어 대사가 붕 뜨는 버그가 반드시 납니다.
        /// </summary>
        public static bool CanInteract => _state == GameState.Exploring;

        internal static void Reset()
        {
            Db = null; Flags = null; Evidence = null;
            Dialogue = null; Room = null; Puzzle = null; UI = null;
            _state = GameState.Boot;
            StateChanged = null;
        }
    }

    /// <summary>진행 플래그. 세이브 대상이며, 조건식 평가도 여기서 합니다.</summary>
    public class FlagManager
    {
        readonly HashSet<string> _on = new HashSet<string>();

        public event Action<string, bool> FlagChanged;

        public FlagManager(GameDatabase db)
        {
            if (db == null) return;
            foreach (var kv in db.Flags)
                if (kv.Value.InitialValue) _on.Add(kv.Key);
        }

        public bool Get(string flagId) => !string.IsNullOrEmpty(flagId) && _on.Contains(flagId);

        public void Set(string flagId, bool value = true)
        {
            if (string.IsNullOrEmpty(flagId)) return;
            bool changed = value ? _on.Add(flagId) : _on.Remove(flagId);
            if (changed)
            {
                FlagChanged?.Invoke(flagId, value);
                Debug.Log($"[Flag] {flagId} = {value}");
            }
        }

        /// <summary>
        /// 조건 문자열 평가. 빈 칸이면 항상 통과.
        /// 지원 형식: "flg_a"  /  "!flg_a"  /  "flg_a,flg_b" (AND)  /  "flg_a|flg_b" (OR)
        /// </summary>
        public bool Check(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression)) return true;

            if (expression.Contains("|"))
            {
                foreach (var part in expression.Split('|'))
                    if (CheckSingleGroup(part)) return true;
                return false;
            }

            return CheckSingleGroup(expression);
        }

        bool CheckSingleGroup(string group)
        {
            foreach (var raw in group.Split(','))
            {
                var token = raw.Trim();
                if (token.Length == 0) continue;

                bool expected = true;
                if (token[0] == '!') { expected = false; token = token.Substring(1).Trim(); }

                if (Get(token) != expected) return false;
            }
            return true;
        }

        public IEnumerable<string> ActiveFlags => _on;

        public void RestoreFrom(IEnumerable<string> flags)
        {
            _on.Clear();
            if (flags == null) return;
            foreach (var f in flags) _on.Add(f);
        }
    }

    /// <summary>보유 증거 목록.</summary>
    public class EvidenceInventory
    {
        readonly List<string> _owned = new List<string>();
        readonly HashSet<string> _ownedSet = new HashSet<string>();

        public event Action<string> EvidenceAdded;

        public IReadOnlyList<string> Owned => _owned;
        public int Count => _owned.Count;
        public bool Has(string evidenceId) => _ownedSet.Contains(evidenceId);

        public bool Add(string evidenceId)
        {
            if (string.IsNullOrEmpty(evidenceId)) return false;
            if (!_ownedSet.Add(evidenceId)) return false;

            _owned.Add(evidenceId);
            EvidenceAdded?.Invoke(evidenceId);
            Debug.Log($"[Evidence] 획득: {evidenceId}");
            return true;
        }

        /// <summary>카테고리로 거른 보유 증거. 추리 퍼즐의 후보 카드 목록을 만들 때 씁니다.</summary>
        public List<EvidenceRow> OwnedByCategory(string category)
        {
            var result = new List<EvidenceRow>();
            if (Game.Db == null) return result;

            foreach (var id in _owned)
            {
                if (!Game.Db.Evidences.TryGetValue(id, out var row)) continue;
                if (string.IsNullOrEmpty(category) || row.Category == category) result.Add(row);
            }
            return result;
        }

        public void RestoreFrom(IEnumerable<string> ids)
        {
            _owned.Clear(); _ownedSet.Clear();
            if (ids == null) return;
            foreach (var id in ids) if (_ownedSet.Add(id)) _owned.Add(id);
        }
    }
}
