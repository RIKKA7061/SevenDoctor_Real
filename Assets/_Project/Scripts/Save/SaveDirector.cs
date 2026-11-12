using System;
using System.Collections.Generic;
using SevenDoctors.Core;
using SevenDoctors.Data;
using UnityEngine;

namespace SevenDoctors.Save
{
    /// <summary>
    /// 자동 저장과 이어 하기.
    ///
    /// 저장 버튼은 없습니다. 방 안에 서 있는 상태(탐색)로 돌아올 때마다 알아서 씁니다.
    ///
    /// 탐색 상태에서만 쓰는 이유 — 대사와 퍼즐 도중은 상태가 어중간합니다. 1회성 핫스팟은 누르는
    /// 순간 소모되는데 그 대사가 켤 플래그는 몇 줄 뒤에 켜지고, 대사가 몇 번째 줄인지는 세이브에
    /// 담지도 않습니다. 그 사이에 저장하면 '핫스팟은 사라졌는데 플래그는 없는' 진행 불가 세이브가
    /// 나옵니다. 그래서 도중에 끄면 그 대사를 시작하기 전(마지막으로 방에 서 있던 때)으로 돌아갑니다.
    /// 대사 하나를 다시 보는 쪽이 막힌 세이브보다 낫습니다.
    ///
    /// '바뀌었다' 는 신호를 받은 그 자리에서 쓰지 않고 프레임 끝(LateUpdate)까지 미룹니다.
    /// 게임 시작이 좋은 예입니다 — 방에 들어가자마자(탐색) 같은 프레임에 프롤로그가 시작됩니다.
    /// 신호를 받자마자 썼다면 '프롤로그를 안 본 채 연구실에 서 있는' 세이브가 생깁니다.
    /// </summary>
    public class SaveDirector : MonoBehaviour
    {
        SaveStore _store;

        // 디스크에 마지막으로 쓴(또는 디스크에서 읽은) 내용. 달라진 게 없으면 다시 쓰지 않습니다.
        SaveData _last;

        bool _active;
        bool _dirty;

        // 저장이 한 번 실패하면 대개 계속 실패합니다(디스크 가득·권한). 같은 경고로 콘솔을 메우지 않습니다.
        bool _warnedWriteFailure;

        SaveStore Store => _store ?? (_store = new SaveStore(Application.persistentDataPath));

        /// <summary>세이브 파일이 놓이는 자리. 콘솔 로그와 문서용입니다.</summary>
        public string FilePath => Store.FilePath;

        void OnEnable()
        {
            Game.StateChanged += OnStateChanged;
            if (Game.Flags != null) Game.Flags.FlagChanged += OnFlagChanged;
            if (Game.Evidence != null) Game.Evidence.EvidenceAdded += OnEvidenceAdded;
            if (Game.Room != null) Game.Room.Interacted += OnInteracted;
        }

        void OnDisable()
        {
            Game.StateChanged -= OnStateChanged;
            if (Game.Flags != null) Game.Flags.FlagChanged -= OnFlagChanged;
            if (Game.Evidence != null) Game.Evidence.EvidenceAdded -= OnEvidenceAdded;
            if (Game.Room != null) Game.Room.Interacted -= OnInteracted;
        }

        void OnStateChanged(GameState previous, GameState current) => _dirty = true;
        void OnFlagChanged(string flag, bool value) => _dirty = true;
        void OnEvidenceAdded(string evidenceId) => _dirty = true;

        // 대사가 안 달린 1회성 핫스팟은 눌러도 상태가 안 바뀌고 플래그도 안 켜집니다. 소모된 것만 남습니다.
        void OnInteracted(HotspotRow hotspot) => _dirty = true;

        // ── 불러오기 ──────────────────────────────────────────────────────────

        /// <summary>
        /// 세이브 파일을 읽습니다. 없거나 못 쓰는 파일이면 null — 시작 화면은 '이어 하기' 를 내놓지 않습니다.
        /// 깨진 파일은 SaveStore 가 옆으로 치워 두므로 다음 실행에서는 그냥 '없음' 입니다.
        /// </summary>
        public SaveData Load()
        {
            var result = Store.Load(out var data, out string message);

            if (result == SaveLoadResult.Unusable)
            {
                Debug.LogWarning($"[Save] {message}. 새 게임으로만 시작할 수 있습니다.");
                return null;
            }
            if (result == SaveLoadResult.None) return null;

            Debug.Log($"[Save] 세이브 발견 — {data.RoomId}, 플래그 {data.Flags.Count}, 증거 {data.Evidence.Count} ({Store.FilePath})");
            return data;
        }

        /// <summary>
        /// 세이브 내용을 게임 상태에 채워 넣고, 들어갈 방을 돌려줍니다. 방에 들어가는 건 호출한 쪽 몫입니다.
        ///
        /// 세이브와 시트는 따로 나이를 먹습니다. 저장한 뒤에 기획자가 증거를 지우거나 방 ID 를 바꿨을 수
        /// 있으므로, 지금 시트에 없는 ID 는 버리고 방은 들어갈 수 있는 곳으로 바꿉니다.
        /// </summary>
        public string Restore(SaveData data, string fallbackRoom)
        {
            var db = Game.Db;

            int dropped = 0;
            var flags = Keep(data.Flags, id => db.Flags.ContainsKey(id), ref dropped);
            var evidence = Keep(data.Evidence, id => db.Evidences.ContainsKey(id), ref dropped);

            Game.Flags.RestoreFrom(flags);
            Game.Evidence.RestoreFrom(evidence);
            // 소모 핫스팟은 거르지 않습니다. 없어진 ID 가 남아 있어도 아무 일도 안 일어납니다.
            Game.Room.RestoreConsumed(data.Consumed);

            if (dropped > 0)
                Debug.LogWarning($"[Save] 세이브에 있던 ID {dropped}개가 지금 시트에 없어 버렸습니다.");

            string room = data.RoomId;
            if (!CanEnter(room))
            {
                string replacement = FirstOpenMapRoom() ?? fallbackRoom;
                Debug.LogWarning($"[Save] 저장된 방 '{room}' 에 들어갈 수 없어 '{replacement}' 에서 이어 합니다.");
                room = replacement;
            }
            return room;
        }

        static List<string> Keep(List<string> ids, Func<string, bool> known, ref int dropped)
        {
            var kept = new List<string>(ids.Count);
            foreach (var id in ids)
            {
                if (known(id)) kept.Add(id);
                else dropped++;
            }
            return kept;
        }

        /// <summary>해금 플래그까지 본, 정상적으로 들어갈 수 있는 방인지.</summary>
        static bool CanEnter(string roomId)
        {
            return !string.IsNullOrEmpty(roomId)
                && Game.Db.Rooms.TryGetValue(roomId, out var room)
                && Game.Flags.Check(room.RequiredFlag);
        }

        /// <summary>
        /// 지도에 나오는 방 가운데 열려 있는 첫 방. 지도에 있는 방이어야 거기서 다른 방으로 갈 수 있습니다
        /// (연구실은 지도에 없어서, 본편 도중의 세이브를 거기로 돌려보내면 헤맵니다).
        /// </summary>
        static string FirstOpenMapRoom()
        {
            foreach (var kv in Game.Db.Rooms)
                if (kv.Value.OnMap && Game.Flags.Check(kv.Value.RequiredFlag)) return kv.Key;
            return null;
        }

        // ── 저장 ──────────────────────────────────────────────────────────────

        /// <summary>
        /// 자동 저장을 켭니다. 게임에 들어갈 때 부릅니다 — 시작 화면에 서 있는 동안에는 쓸 것이 없습니다.
        /// 이어 하기라면 방금 읽은 세이브를 넘기세요. 불러오자마자 같은 내용을 다시 쓰지 않게 됩니다.
        /// </summary>
        public void Begin(SaveData loaded)
        {
            _last = loaded;
            _active = true;
            _dirty = true;
        }

        /// <summary>새 게임을 고르면 옛 세이브를 지웁니다. 시작 화면에서 한 번 더 확인을 받은 뒤입니다.</summary>
        public void DeleteSave()
        {
            _last = null;
            if (!Store.Delete(out string error))
                Debug.LogWarning($"[Save] 세이브 파일을 지우지 못했습니다: {error}");
        }

        /// <summary>
        /// 엔딩 카드가 뜰 때. 이야기가 끝났으므로 세이브를 지우고 저장을 멈춥니다.
        ///
        /// 남겨 두면 '처음으로' 를 누른 뒤의 시작 화면에 '이어 하기' 가 떠서, 누르면 마지막 대사 직전으로
        /// 돌아갑니다. '처음으로' 가 한 말과 어긋나고, 엔딩을 본 사람에게 이어 할 것은 없습니다.
        /// 엔딩 카드에서 그냥 꺼도 마찬가지여야 하므로 '처음으로' 를 누를 때가 아니라 카드가 뜰 때 지웁니다.
        /// </summary>
        public void OnEndingReached()
        {
            _active = false;

            // F1 미리보기로 엔딩을 틀어 본 것이라면 진짜 진행 상황은 그대로 둡니다.
            if (IsDevPreviewRunning()) return;

            DeleteSave();
            Debug.Log("[Save] 엔딩 — 세이브를 지웠습니다.");
        }

        void LateUpdate()
        {
            if (!_active || Game.Room == null) return;

            // 지도로 방을 옮기면 상태도 플래그도 안 바뀝니다 (탐색 → 탐색). 방은 직접 봅니다.
            if (_last == null || _last.RoomId != Game.Room.CurrentRoomId) _dirty = true;
            if (!_dirty) return;

            // 아직 어중간하면 표시를 남겨 둔 채 기다립니다. 탐색으로 돌아온 프레임에 씁니다.
            if (Game.State != GameState.Exploring) return;
            if (Game.Dialogue != null && Game.Dialogue.IsPlaying) return;
            if (IsDevPreviewRunning()) return;

            _dirty = false;
            WriteIfChanged();
        }

        void WriteIfChanged()
        {
            var data = Capture();
            if (data == null || data.SameProgressAs(_last)) return;

            if (Store.Save(data, out string error))
            {
                _last = data;
                _warnedWriteFailure = false;
                return;
            }

            if (!_warnedWriteFailure)
                Debug.LogWarning($"[Save] 저장하지 못했습니다: {error} ({Store.FilePath}). 게임은 계속됩니다.");
            _warnedWriteFailure = true;
        }

        SaveData Capture()
        {
            // 개발자 방 이동(숫자 키)은 잠긴 방에도 들어갑니다. 그 방을 저장하면 이어 하기 때
            // 잠금에 걸리므로, 정상적으로 들어갈 수 있는 마지막 방을 그대로 둡니다.
            string room = Game.Room.CurrentRoomId;
            if (!CanEnter(room)) room = _last != null ? _last.RoomId : null;
            if (string.IsNullOrEmpty(room)) return null;

            var data = new SaveData
            {
                SavedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture),
                RoomId = room,
            };

            data.Flags.AddRange(Game.Flags.ActiveFlags);
            data.Evidence.AddRange(Game.Evidence.Owned);
            data.Consumed.AddRange(Game.Room.ConsumedHotspots);

            // HashSet 은 순서가 없어서 그대로 쓰면 저장할 때마다 줄이 뒤섞입니다.
            // 정렬해 두면 파일 두 개를 나란히 놓고 무엇이 달라졌는지 볼 수 있습니다.
            data.Flags.Sort(StringComparer.Ordinal);
            data.Consumed.Sort(StringComparer.Ordinal);
            return data;
        }

        /// <summary>
        /// F1 미리보기가 도는 중인지. 미리보기는 증거를 전부 쥐여 주고 끝나면 되돌리는데,
        /// 그 사이에도 상태는 탐색을 오갑니다. 그때 저장하면 가짜 진행 상황이 파일에 남습니다.
        /// </summary>
        bool IsDevPreviewRunning()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var dev = GetComponent<SevenDoctors.Dev.DevPuzzleMenu>();
            return dev != null && dev.IsRunningPuzzle;
#else
            return false;
#endif
        }
    }
}
