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
    ///   save_main.json    이어하기. 탐색으로 돌아올 때마다 자동으로, 메뉴의 '저장하기', 종료·타이틀로 갈 때
    ///   save_branch.json  엔딩 분기점. 대사에 [checkpoint] 가 달린 줄에 닿으면 자동으로
    ///   profile.json      본 엔딩 목록. 엔딩 카드가 뜰 때마다
    ///
    /// 저장은 '탐색 중'에만 정확합니다 — 대사·퍼즐은 코루틴 한가운데라 그 자리를 그대로 담을 수
    /// 없습니다. 1회성 핫스팟은 누르는 순간 소모되는데 그 대사가 켤 플래그는 몇 줄 뒤에 켜지므로,
    /// 그 사이에 저장하면 '핫스팟은 사라졌는데 플래그는 없는' 막힌 세이브가 나옵니다.
    /// 그래서 탐색 상태가 될 때마다(대화가 끝나거나 방에 들어설 때, 탐색 중 플래그·증거가 바뀔 때)
    /// 표시를 해 두고(NoteSafePoint), 프레임 끝에 여전히 탐색 중이면 그 순간을 이어하기 칸에 씁니다
    /// (FlushPending — GameBootstrap.LateUpdate). 대사 도중에 꺼져도 다시 켜면 그 대화를 걸기 직전입니다.
    /// 분기점만은 예외로 '이 대화 몇 번째 줄' 까지 적어서, 불러오면 그 줄부터 다시 흐릅니다.
    ///
    /// 세이브 파일은 사용자가 손댈 수 있는 입력이라, 못 읽는 파일은 *.bad.json 으로 치워 두고
    /// 없는 셈 칩니다 (버그 신고 때 들여다볼 수 있게 지우지는 않습니다).
    /// </summary>
    public static class SaveSystem
    {
        public const int Version = 1;

        static string MainPath    => Path.Combine(Application.persistentDataPath, "save_main.json");
        static string BranchPath  => Path.Combine(Application.persistentDataPath, "save_branch.json");
        static string ProfilePath => Path.Combine(Application.persistentDataPath, "profile.json");

        // 다른 노트북에서 만들었던 단일 칸 자동 저장이 쓰던 파일. 이어하기 칸이 없을 때 한 번 옮겨 옵니다.
        static string LegacyPath  => Path.Combine(Application.persistentDataPath, "save.json");

        static SaveData _safe;          // 마지막으로 탐색 상태였던 순간
        static bool _safePointsOn;      // 시작 대화가 끝나기 전엔 꺼 둡니다
        static bool _pending;           // 탐색 순간을 다시 잡아야 함 — 프레임 끝에 처리
        static string _lastMainKey;     // 이어하기 칸에 마지막으로 쓴(읽은) 진행 상황. 같으면 다시 안 씁니다
        static bool _warnedWriteFailure;
        static SaveProfile _profile;

        public static bool HasMain => File.Exists(MainPath);
        public static bool HasBranch => File.Exists(BranchPath);

        /// <summary>게임을 새로 띄울 때마다(재시작 포함). 정적 필드라 씬을 다시 불러도 남아 있습니다.</summary>
        public static void BeginSession(bool safePointsOn)
        {
            _safe = null;
            _safePointsOn = safePointsOn;
            _pending = false;
            _lastMainKey = null;
            _warnedWriteFailure = false;
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

        /// <summary>
        /// 저장할 순간일 수 있다는 표시. 대화가 끝날 때·방에 들어설 때·탐색 중 플래그·증거·1회성 핫스팟이
        /// 바뀔 때 부릅니다. 바로 쓰지 않고 FlushPending(프레임 끝)까지 미룹니다 — 방에 들어서자마자
        /// 같은 프레임에 대화가 시작되거나, 대화가 끝나자마자 퍼즐이 이어지는 경우가 있기 때문입니다.
        /// </summary>
        public static void NoteSafePoint() => _pending = true;

        /// <summary>
        /// 프레임 끝에 부릅니다 (GameBootstrap.LateUpdate). 표시가 있고 지금도 탐색 중이면 이 순간을
        /// 기억하고, 진행 상황이 달라졌으면 이어하기 칸에 씁니다. 아직 어중간하면 표시를 남겨 둡니다.
        /// </summary>
        public static void FlushPending()
        {
            if (!_pending || !_safePointsOn || !IsSafeMoment()) return;
            _pending = false;
            _safe = Capture();
            WriteMainIfChanged(_safe);
        }

        static bool IsSafeMoment()
        {
            if (Game.State != GameState.Exploring || !Game.IsReady || Game.Room == null) return false;
            if (string.IsNullOrEmpty(Game.Room.CurrentRoomId)) return false;
            return Game.Dialogue == null || !Game.Dialogue.IsPlaying;
        }

        public static bool CanSaveNow => Game.State == GameState.Exploring && _safePointsOn;

        // ── 쓰기 ──────────────────────────────────────────────────────────────

        /// <summary>메뉴의 '저장하기'. 탐색 중에만 됩니다.</summary>
        public static bool SaveNow()
        {
            if (!CanSaveNow) return false;
            _safe = Capture();
            _pending = false;
            if (!Write(MainPath, _safe)) return false;
            _lastMainKey = ProgressKey(_safe);
            return true;
        }

        /// <summary>종료·타이틀로 갈 때. 탐색 중이면 지금을, 아니면 마지막 탐색 순간을 씁니다.</summary>
        public static void AutoSave()
        {
            if (CanSaveNow && IsSafeMoment()) _safe = Capture();
            if (_safe == null) return;
            if (WriteMainIfChanged(_safe)) Debug.Log($"[Save] 자동 저장 — {_safe.roomId} ({_safe.savedAt})");
        }

        /// <summary>진행 상황이 지난번에 쓴 것과 다를 때만 이어하기 칸에 씁니다. 썼으면 true.</summary>
        static bool WriteMainIfChanged(SaveData data)
        {
            string key = ProgressKey(data);
            if (key == _lastMainKey && File.Exists(MainPath)) return false;
            if (!Write(MainPath, data)) return false;
            _lastMainKey = key;
            return true;
        }

        /// <summary>저장 시각을 뺀 내용. 달라진 게 없는데 디스크에 다시 쓰는 일을 막으려고 비교합니다.</summary>
        static string ProgressKey(SaveData data)
        {
            if (data == null) return null;
            string at = data.savedAt;
            data.savedAt = null;
            string key = JsonUtility.ToJson(data);
            data.savedAt = at;
            return key;
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
                roomId = SavableRoom(),
            };
            data.flags.AddRange(Game.Flags.ActiveFlags);
            data.evidence.AddRange(Game.Evidence.Owned);
            if (Game.Room != null) data.consumed.AddRange(Game.Room.ConsumedHotspots);
            if (Game.Dialogue != null) data.log.AddRange(Game.Dialogue.Log.Export());

            // HashSet 은 순서가 없어서 그대로 쓰면 저장할 때마다 줄이 뒤섞입니다. 정렬해 두면 '달라진 게 없음' 을
            // 알아볼 수 있고, 파일 두 개를 나란히 놓고 비교할 수도 있습니다. 증거는 노트가 얻은 순서로 보여 주므로 그대로.
            data.flags.Sort(StringComparer.Ordinal);
            data.consumed.Sort(StringComparer.Ordinal);
            return data;
        }

        /// <summary>
        /// 저장할 방. 개발자 방 이동(숫자 키)은 잠긴 방에도 들어갑니다 — 그 방을 저장하면 이어 하기 때
        /// 잠금에 걸리므로, 정상적으로 들어갈 수 있는 마지막 방을 그대로 둡니다.
        /// </summary>
        static string SavableRoom()
        {
            string room = Game.Room != null ? Game.Room.CurrentRoomId : null;
            if (CanEnter(room)) return room;
            return _safe != null && !string.IsNullOrEmpty(_safe.roomId) ? _safe.roomId : room;
        }

        static bool Write(string path, object data)
        {
            try
            {
                // 쓰다가 꺼져도 예전 세이브가 깨지지 않게 옆에 쓰고 바꿔 끼웁니다.
                // 바꿔 끼우기 직전에 꺼지면 다 쓴 임시 파일만 남는데, Read 가 그걸 주워 씁니다.
                string temp = path + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(data, true));
                if (File.Exists(path)) File.Delete(path);
                File.Move(temp, path);
                _warnedWriteFailure = false;
                return true;
            }
            catch (Exception e)
            {
                // 저장이 한 번 실패하면 대개 계속 실패합니다(디스크 가득·권한). 자동 저장이 자주 돌아서
                // 같은 오류로 콘솔을 메우지 않게 한 번만 알립니다. 게임은 계속됩니다.
                if (!_warnedWriteFailure) Debug.LogError($"[Save] 저장 실패 ({path}): {e.Message}");
                _warnedWriteFailure = true;
                return false;
            }
        }

        // ── 읽기 ──────────────────────────────────────────────────────────────

        public static SaveData LoadMain()
        {
            MigrateLegacy();
            return Read(MainPath);
        }

        public static SaveData LoadBranch() => Read(BranchPath);
        public static SaveData Load(StartMode mode) =>
            mode == StartMode.Continue ? LoadMain() : mode == StartMode.Branch ? LoadBranch() : null;

        static SaveData Read(string path)
        {
            string json;
            try
            {
                string temp = path + ".tmp";
                if (!File.Exists(path) && File.Exists(temp)) File.Move(temp, path);
                if (!File.Exists(path)) return null;
                json = File.ReadAllText(path);
            }
            catch (Exception e)
            {
                // 권한·잠금 같은 문제. 파일 내용은 멀쩡할 수 있으니 치우지 않고 그대로 둡니다.
                Debug.LogWarning($"[Save] 세이브를 열 수 없습니다 ({path}): {e.Message}");
                return null;
            }

            SaveData data = null;
            string error = null;
            try { data = JsonUtility.FromJson<SaveData>(json); }
            catch (Exception e) { error = e.Message; }

            if (error == null)
            {
                if (data == null) error = "내용이 비어 있습니다";
                else if (data.version != Version) error = $"형식 번호가 다릅니다 (파일 v{data.version}, 이 빌드 v{Version})";
                else if (string.IsNullOrEmpty(data.roomId)) error = "방이 적혀 있지 않습니다";
            }
            if (error == null) return data;

            Quarantine(path, error);
            return null;
        }

        /// <summary>
        /// 못 쓰는 세이브를 옆으로 치웁니다. 치워 두지 않으면 켤 때마다 같은 경고가 뜨고, 곧 자동 저장이
        /// 그 위를 덮어써서 흔적도 사라집니다. 지우지 않는 건 버그 신고 때 들여다볼 수 있게 하려는 것입니다.
        /// </summary>
        static void Quarantine(string path, string reason)
        {
            string bad = Path.ChangeExtension(path, ".bad.json");
            try
            {
                if (File.Exists(bad)) File.Delete(bad);
                File.Move(path, bad);
                Debug.LogWarning($"[Save] 세이브를 읽을 수 없어 {Path.GetFileName(bad)} 로 옮겨 두었습니다: {reason}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] 세이브를 읽을 수 없습니다 ({reason}). 옮겨 두지도 못했습니다: {e.Message}");
            }
        }

        /// <summary>예전 save.json 의 모양. 진행 상황 칸은 지금과 같고 방 칸 이름만 다릅니다.</summary>
        [Serializable]
        class LegacySave
        {
            public int version;
            public string savedAt;      // UTC, ISO 8601
            public string room;
            public List<string> flags = new List<string>();
            public List<string> evidence = new List<string>();
            public List<string> consumed = new List<string>();
        }

        /// <summary>
        /// 다른 노트북에서 쓰던 단일 칸 자동 저장(save.json)이 남아 있고 이어하기 칸이 비어 있으면, 한 번 옮겨 옵니다.
        /// 옮긴 원본은 save.legacy.json 으로 이름만 바꿔 둡니다.
        /// </summary>
        static void MigrateLegacy()
        {
            try
            {
                if (File.Exists(MainPath) || !File.Exists(LegacyPath)) return;

                var old = JsonUtility.FromJson<LegacySave>(File.ReadAllText(LegacyPath));
                if (old != null && !string.IsNullOrEmpty(old.room))
                {
                    var data = new SaveData { version = Version, roomId = old.room };
                    data.savedAt = DateTime.TryParse(old.savedAt, System.Globalization.CultureInfo.InvariantCulture,
                                                     System.Globalization.DateTimeStyles.RoundtripKind, out var at)
                        ? at.ToLocalTime().ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture)
                        : null;
                    if (old.flags != null) data.flags.AddRange(old.flags);
                    if (old.evidence != null) data.evidence.AddRange(old.evidence);
                    if (old.consumed != null) data.consumed.AddRange(old.consumed);
                    if (Write(MainPath, data)) Debug.Log($"[Save] 예전 세이브(save.json)를 이어하기 칸으로 옮겼습니다 — {data.roomId}");
                }

                string done = Path.Combine(Application.persistentDataPath, "save.legacy.json");
                if (File.Exists(done)) File.Delete(done);
                File.Move(LegacyPath, done);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] 예전 세이브(save.json)를 옮기지 못했습니다: {e.Message}");
            }
        }

        /// <summary>
        /// 플래그·증거·쓴 핫스팟·대화 기록을 되돌립니다. 방 입장과 대사 재생은 부르는 쪽(GameBootstrap)이 합니다.
        ///
        /// 세이브와 시트는 따로 나이를 먹습니다. 저장한 뒤에 플래그·증거를 지웠을 수 있으므로 지금 시트에
        /// 없는 ID 는 건너뜁니다. 소모 핫스팟은 거르지 않습니다 — 없어진 ID 가 남아 있어도 아무 일도 안 일어납니다.
        /// </summary>
        public static void Apply(SaveData data)
        {
            if (data == null) return;

            int dropped = 0;
            var flags = new List<string>();
            foreach (var id in data.flags) { if (Game.Db.Flags.ContainsKey(id)) flags.Add(id); else dropped++; }
            Game.Flags.RestoreFrom(flags);

            var evidence = new List<string>();
            foreach (var id in data.evidence) { if (Game.Db.Evidences.ContainsKey(id)) evidence.Add(id); else dropped++; }
            Game.Evidence.RestoreFrom(evidence);

            if (dropped > 0)
                Debug.LogWarning($"[Save] 세이브에 있던 ID {dropped}개가 지금 시트에 없어 버렸습니다.");

            Game.Room?.RestoreConsumed(data.consumed);
            Game.Dialogue?.Log.Import(data.log);

            // 불러오자마자 같은 내용을 다시 쓰지 않게, 이어하기라면 읽은 내용을 '마지막으로 쓴 것' 으로 둡니다.
            if (!data.Resumes) _lastMainKey = ProgressKey(data);
        }

        /// <summary>
        /// 이어 할 방. 저장된 방이 지금도 들어갈 수 있으면 그대로, 아니면(방 ID 가 바뀌었거나 잠김)
        /// 지도에 나오는 열린 방 가운데 첫 방, 그것도 없으면 fallback. Apply 뒤에 불러야 해금 플래그를 봅니다.
        /// 연구실은 지도에 없어서, 본편 도중의 세이브를 거기로 돌려보내면 다른 방으로 갈 길이 없습니다.
        /// </summary>
        public static string ResolveRoom(string roomId, string fallback)
        {
            if (CanEnter(roomId)) return roomId;
            if (Game.Db != null)
                foreach (var kv in Game.Db.Rooms)
                    if (kv.Value.OnMap && Game.Flags.Check(kv.Value.RequiredFlag)) return kv.Key;
            return fallback;
        }

        /// <summary>해금 플래그까지 본, 정상적으로 들어갈 수 있는 방인지.</summary>
        static bool CanEnter(string roomId)
        {
            return !string.IsNullOrEmpty(roomId)
                && Game.Db != null && Game.Flags != null
                && Game.Db.Rooms.TryGetValue(roomId, out var room)
                && Game.Flags.Check(room.RequiredFlag);
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

        /// <summary>
        /// 시작 화면에서 '새 게임' 을 골랐을 때 (세이브가 있으면 한 번 더 확인을 받은 뒤). 이어하기 칸만 지웁니다 —
        /// 엔딩 분기점과 본 엔딩 기록은 회차를 넘어 남습니다. 프롤로그 도중에 꺼도 옛 이어하기가 되살아나지 않습니다.
        /// </summary>
        public static void DeleteMain()
        {
            foreach (var path in new[] { MainPath, MainPath + ".tmp", LegacyPath })
            {
                try { if (File.Exists(path)) File.Delete(path); }
                catch (Exception e) { Debug.LogWarning($"[Save] 지우지 못했습니다 ({path}): {e.Message}"); }
            }
            _safe = null;
            _pending = false;
            _lastMainKey = null;
        }

        /// <summary>설정의 '세이브 초기화'. 이어하기·분기점·본 엔딩을 전부 지웁니다. 언어 설정은 남깁니다.</summary>
        public static void DeleteAll()
        {
            foreach (var path in new[] { MainPath, BranchPath, ProfilePath, LegacyPath,
                                         MainPath + ".tmp", BranchPath + ".tmp", ProfilePath + ".tmp" })
            {
                try { if (File.Exists(path)) File.Delete(path); }
                catch (Exception e) { Debug.LogError($"[Save] 지우지 못했습니다 ({path}): {e.Message}"); }
            }
            _profile = null;
            _safe = null;
            _pending = false;
            _lastMainKey = null;
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
