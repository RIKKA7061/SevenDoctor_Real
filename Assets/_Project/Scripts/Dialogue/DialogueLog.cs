using System.Collections.Generic;
using SevenDoctors.Core;
using SevenDoctors.Data;

namespace SevenDoctors.Dialogue
{
    /// <summary>
    /// 지금까지 화면에 나온 대사를 처음부터 차례대로 쌓아 둡니다. 대화 기록창(LogView)이 이걸 그립니다.
    ///
    /// 글자 대신 시트의 행을 들고 있습니다 — 언어를 바꾸면 지난 대사도 바뀐 언어로 다시 그려집니다.
    /// 대사 말고도 고른 선택지, 얻은 증거, 대사가 나온 장소(방이 바뀔 때만)를 같이 남겨서
    /// 나중에 '어디서 누가 뭐라고 했더라' 를 되짚을 수 있게 합니다.
    ///
    /// DialogueRunner 가 들고 있어서 '처음으로' 재시작하면 같이 비워집니다.
    /// 세이브에는 ID 로 적어 두었다가(Export) 불러올 때 시트의 행으로 되돌립니다(Import).
    /// </summary>
    public class DialogueLog
    {
        public enum Kind { Line, Choice, Evidence, Place }

        public class Entry
        {
            public Kind Kind;
            public DialogueRow Line;
            public ChoiceRow Choice;
            public EvidenceRow Evidence;
            public RoomRow Room;
        }

        readonly List<Entry> _entries = new List<Entry>();
        string _lastRoomId;

        public IReadOnlyList<Entry> Entries => _entries;
        public int Count => _entries.Count;

        public void AddLine(DialogueRow line)
        {
            if (line == null) return;
            MarkPlace();
            _entries.Add(new Entry { Kind = Kind.Line, Line = line });
        }

        public void AddChoice(ChoiceRow choice)
        {
            if (choice == null) return;
            _entries.Add(new Entry { Kind = Kind.Choice, Choice = choice });
        }

        public void AddEvidence(EvidenceRow evidence)
        {
            if (evidence == null) return;
            _entries.Add(new Entry { Kind = Kind.Evidence, Evidence = evidence });
        }

        // ── 세이브 ────────────────────────────────────────────────────────────

        public List<SavedLogEntry> Export()
        {
            var list = new List<SavedLogEntry>(_entries.Count);
            foreach (var e in _entries)
            {
                switch (e.Kind)
                {
                    case Kind.Line:     list.Add(new SavedLogEntry { kind = (int)e.Kind, id = e.Line.DialogueId, line = e.Line.LineNo }); break;
                    case Kind.Choice:   list.Add(new SavedLogEntry { kind = (int)e.Kind, id = e.Choice.Id }); break;
                    case Kind.Evidence: list.Add(new SavedLogEntry { kind = (int)e.Kind, id = e.Evidence.Id }); break;
                    case Kind.Place:    list.Add(new SavedLogEntry { kind = (int)e.Kind, id = e.Room.Id }); break;
                }
            }
            return list;
        }

        /// <summary>세이브의 기록으로 갈아 끼웁니다. 시트에서 사라진 줄은 건너뜁니다.</summary>
        public void Import(List<SavedLogEntry> saved)
        {
            _entries.Clear();
            _lastRoomId = null;
            if (saved == null || Game.Db == null) return;

            foreach (var s in saved)
            {
                var e = Resolve(s);
                if (e == null) continue;
                _entries.Add(e);
                if (e.Kind == Kind.Place) _lastRoomId = e.Room.Id;
            }
        }

        static Entry Resolve(SavedLogEntry s)
        {
            var db = Game.Db;
            switch ((Kind)s.kind)
            {
                case Kind.Line:
                    var lines = db.GetDialogue(s.id);
                    if (lines != null)
                        foreach (var l in lines)
                            if (l.LineNo == s.line) return new Entry { Kind = Kind.Line, Line = l };
                    return null;

                case Kind.Choice:
                    foreach (var c in db.Choices)
                        if (c.Id == s.id) return new Entry { Kind = Kind.Choice, Choice = c };
                    return null;

                case Kind.Evidence:
                    return db.Evidences.TryGetValue(s.id, out var ev) ? new Entry { Kind = Kind.Evidence, Evidence = ev } : null;

                case Kind.Place:
                    return db.Rooms.TryGetValue(s.id, out var room) ? new Entry { Kind = Kind.Place, Room = room } : null;
            }
            return null;
        }

        /// <summary>
        /// 대사가 나온 방이 앞 대사와 다르면 장소 줄을 하나 끼웁니다.
        /// 대사 없이 지나친 방은 남기지 않습니다 — 지도로 이리저리 다닌 흔적까지 남으면 읽기 어렵습니다.
        /// </summary>
        void MarkPlace()
        {
            if (Game.Room == null || Game.Db == null) return;

            string here = Game.Room.CurrentRoomId;
            if (string.IsNullOrEmpty(here) || here == _lastRoomId) return;
            _lastRoomId = here;

            if (Game.Db.Rooms.TryGetValue(here, out var room))
                _entries.Add(new Entry { Kind = Kind.Place, Room = room });
        }
    }
}
