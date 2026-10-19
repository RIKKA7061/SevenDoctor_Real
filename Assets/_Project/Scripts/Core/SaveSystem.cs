using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SevenDoctors.Core
{
    /// <summary>게임을 어떻게 시작할지. 시작 화면 버튼과 엔딩 카드가 고릅니다.</summary>
    public enum StartMode { NewGame, Continue, Branch }

    /// <summary>
    /// 세이브 한 칸에 들어가는 것. JsonUtility 로 그대로 씁니다.
    /// 상태는 전부 시트 ID 로만 적습니다 — 시트가 바뀌어도 없는 ID 만 건너뛰고 나머지는 살립니다.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public int version;
        public string savedAt;          // "yyyy-MM-dd HH:mm" — 화면에 보여주기용
        public string roomId;
        public List<string> flags = new List<string>();
        public List<string> evidence = new List<string>();
        public List<string> consumed = new List<string>();   // 한 번 쓰고 사라진 핫스팟

        // 분기점 저장에만 있습니다: 이 대화의 이 줄부터 다시 틉니다.
        public string resumeDialogue;
        public int resumeLine;

        public List<SavedLogEntry> log = new List<SavedLogEntry>();

        public bool Resumes => !string.IsNullOrEmpty(resumeDialogue);
    }

    /// <summary>대화 기록 한 줄. kind 는 DialogueLog.Kind, id 는 대화·선택지·증거·방 ID.</summary>
    [Serializable]
    public class SavedLogEntry
    {
        public int kind;
        public string id;
        public int line;
    }

    /// <summary>세이브 칸과 상관없이 남는 것 — 지금은 본 엔딩 목록뿐입니다.</summary>
    [Serializable]
    public class SaveProfile
    {
        public List<string> endingsSeen = new List<string>();
    }

    /// <summary>
    /// 세이브. 칸은 셋입니다 (Application.persistentDataPath 아래 JSON):
    ///
    ///   save_main.json    이어하기. 메뉴의 '저장하기', 그리고 종료·타이틀로 갈 때 자동으로
    ///   save_branch.json  엔딩 분기점. 대사에 [checkpoint] 가 달린 줄에 닿으면 자동으로
    ///   profile.json      본 엔딩 목록. 엔딩 카드가 뜰 때마다
    ///
    /// 저장은 '탐색 중'에만 정확합니다 — 대사·퍼즐은 코루틴 한가운데라 그 자리를 그대로 담을 수
    /// 없습니다. 그래서 탐색 상태가 될 때마다(대화가 끝나거나 방에 들어설 때) 그 순간을 기억해
    /// 두고(NoteSafePoint), 대사 도중에 종료하면 그 기억을 씁니다. 다시 켜면 그 대화를 걸기 직전입니다.
    /// 분기점만은 예외로 '이 대화 몇 번째 줄' 까지 적어서, 불러오면 그 줄부터 다시 흐릅니다.
    /// </summary>
    public static class SaveSystem
    {
        public const int Version = 1;

        static string MainPath    => Path.Combine(Application.persistentDataPath, "save_main.json");
        static string BranchPath  => Path.Combine(Application.persistentDataPath, "save_branch.json");
        static string ProfilePath => Path.Combine(Application.persistentDataPath, "profile.json");

        static SaveData _safe;          // 마지막으로 탐색 상태였던 순간
        static bool _safePointsOn;      // 시작 대화가 끝나기 전엔 꺼 둡니다
        static SaveProfile _profile;

        public static bool HasMain => File.Exists(MainPath);
        public static bool HasBranch => File.Exists(BranchPath);

        /// <summary>게임을 새로 띄울 때마다(재시작 포함). 정적 필드라 씬을 다시 불러도 남아 있습니다.</summary>
        public static void BeginSession(bool safePointsOn)
        {
            _safe = null;
            _safePointsOn = safePointsOn;
            _profile = null;   // 파일에서 다시 읽습니다 (Play 사이에 정적 필드가 남는 설정 대비)
        }

        /// <summary>
        /// 시작 대화(프롤로그)가 끝나면 켭니다. 그 전에 종료하면 저장할 게 없습니다 —
        /// 연구실에 서 있기만 한 상태를 저장하면, 불러왔을 때 프롤로그가 안 나옵니다.
        /// </summary>
        public static void EnableSafePoints()
        {
            _safePointsOn = true;
            NoteSafePoint();
        }

        /// <summary>지금이 탐색 상태면 이 순간을 기억합니다. 대화가 끝날 때·방에 들어설 때 부릅니다.</summary>
        public static void NoteSafePoint()
        {
            if (!_safePointsOn || Game.State != GameState.Exploring || !Game.IsReady || Game.Room == null) return;
            if (string.IsNullOrEmpty(Game.Room.CurrentRoomId)) return;
            _safe = Capture();
        }

        public static bool CanSaveNow => Game.State == GameState.Exploring && _safePointsOn;

        // ── 쓰기 ──────────────────────────────────────────────────────────────

        /// <summary>메뉴의 '저장하기'. 탐색 중에만 됩니다.</summary>
        public static bool SaveNow()
        {
            if (!CanSaveNow) return false;
            _safe = Capture();
            return Write(MainPath, _safe);
        }

        /// <summary>종료·타이틀로 갈 때. 탐색 중이면 지금을, 아니면 마지막 탐색 순간을 씁니다.</summary>
        public static void AutoSave()
        {
            if (CanSaveNow) _safe = Capture();
            if (_safe == null) return;
            if (Write(MainPath, _safe)) Debug.Log($"[Save] 자동 저장 — {_safe.roomId} ({_safe.savedAt})");
        }

        /// <summary>[checkpoint] 줄에 닿았을 때. 그 줄을 보여주기 직전 상태를 분기점 칸에 씁니다.</summary>
        public static void SaveCheckpoint(string dialogueId, int lineNo)
        {
            var data = Capture();
            data.resumeDialogue = dialogueId;
            data.resumeLine = lineNo;
            if (Write(BranchPath, data)) Debug.Log($"[Save] 분기점 저장 — {dialogueId}:{lineNo}");
        }

        static SaveData Capture()
        {
            var data = new SaveData
            {
                version = Version,
                savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture),
                roomId = Game.Room != null ? Game.Room.CurrentRoomId : null,
            };
            data.flags.AddRange(Game.Flags.ActiveFlags);
            data.evidence.AddRange(Game.Evidence.Owned);
            if (Game.Room != null) data.consumed.AddRange(Game.Room.ConsumedHotspots);
            if (Game.Dialogue != null) data.log.AddRange(Game.Dialogue.Log.Export());
            return data;
        }

        static bool Write(string path, object data)
        {
            try
            {
                // 쓰다가 꺼져도 예전 세이브가 깨지지 않게 옆에 쓰고 바꿔 끼웁니다.
                string temp = path + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(data, true));
                if (File.Exists(path)) File.Delete(path);
                File.Move(temp, path);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save] 저장 실패 ({path}): {e.Message}");
                return false;
            }
        }

        // ── 읽기 ──────────────────────────────────────────────────────────────

        public static SaveData LoadMain() => Read(MainPath);
        public static SaveData LoadBranch() => Read(BranchPath);
        public static SaveData Load(StartMode mode) =>
            mode == StartMode.Continue ? LoadMain() : mode == StartMode.Branch ? LoadBranch() : null;

        static SaveData Read(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
                if (data == null || data.version != Version || string.IsNullOrEmpty(data.roomId))
                {
                    Debug.LogWarning($"[Save] 읽을 수 없는 세이브라 무시합니다: {path}");
                    return null;
                }
                return data;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] 세이브를 읽지 못했습니다 ({path}): {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// 플래그·증거·쓴 핫스팟·대화 기록을 되돌립니다. 방 입장과 대사 재생은 부르는 쪽(GameBootstrap)이 합니다.
        /// 시트에서 사라진 증거·대사는 건너뜁니다.
        /// </summary>
        public static void Apply(SaveData data)
        {
            if (data == null) return;
            Game.Flags.RestoreFrom(data.flags);

            var evidence = new List<string>();
            foreach (var id in data.evidence) if (Game.Db.Evidences.ContainsKey(id)) evidence.Add(id);
            Game.Evidence.RestoreFrom(evidence);

            Game.Room?.RestoreConsumed(data.consumed);
            Game.Dialogue?.Log.Import(data.log);
        }

        // ── 본 엔딩 ───────────────────────────────────────────────────────────

        static SaveProfile Profile
        {
            get
            {
                if (_profile != null) return _profile;
                try
                {
                    if (File.Exists(ProfilePath))
                        _profile = JsonUtility.FromJson<SaveProfile>(File.ReadAllText(ProfilePath));
                }
                catch (Exception e) { Debug.LogWarning($"[Save] 엔딩 기록을 읽지 못했습니다: {e.Message}"); }
                return _profile ??= new SaveProfile();
            }
        }

        public static bool IsEndingSeen(string endingId) => Profile.endingsSeen.Contains(endingId);

        public static int EndingsSeenCount
        {
            get
            {
                int n = 0;
                if (Game.Db != null) foreach (var e in Game.Db.Endings) if (IsEndingSeen(e.Id)) n++;
                return n;
            }
        }

        public static void MarkEndingSeen(string endingId)
        {
            if (string.IsNullOrEmpty(endingId) || IsEndingSeen(endingId)) return;
            Profile.endingsSeen.Add(endingId);
            Write(ProfilePath, Profile);
        }

        // ── 초기화 ────────────────────────────────────────────────────────────

        /// <summary>설정의 '세이브 초기화'. 이어하기·분기점·본 엔딩을 전부 지웁니다. 언어 설정은 남깁니다.</summary>
        public static void DeleteAll()
        {
            foreach (var path in new[] { MainPath, BranchPath, ProfilePath })
            {
                try { if (File.Exists(path)) File.Delete(path); }
                catch (Exception e) { Debug.LogError($"[Save] 지우지 못했습니다 ({path}): {e.Message}"); }
            }
            _profile = null;
            _safe = null;
            Debug.Log("[Save] 세이브를 전부 지웠습니다.");
        }

        /// <summary>시작 화면 '이어하기' 아래 작게 적을 말. "10-19 14:32 · 1층 로비"</summary>
        public static string Describe(SaveData data)
        {
            if (data == null) return string.Empty;
            string when = data.savedAt != null && data.savedAt.Length >= 16 ? data.savedAt.Substring(5) : data.savedAt;
            string where = Game.Db != null && Game.Db.Rooms.TryGetValue(data.roomId, out var room) ? room.DisplayName : data.roomId;
            return $"{when} · {where}";
        }
    }
}
