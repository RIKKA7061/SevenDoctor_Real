using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SevenDoctors.Data
{
    /// <summary>
    /// Resources/Data 아래의 CSV를 전부 읽어 메모리에 올려두는 읽기 전용 데이터베이스.
    ///
    /// 왜 ScriptableObject가 아니라 CSV 직접 로드인가:
    ///  - 기획자가 시트를 고치고 임포터를 돌리면 CSV만 갈리면 되고, 에셋 재생성/GUID 충돌이 없습니다.
    ///  - 이 규모(수천 행)에서는 파싱 비용이 프레임 하나도 안 됩니다.
    ///  - 나중에 로딩이 정말 문제가 되면 이 클래스 안쪽만 SO 로드로 바꾸면 되고, 바깥 코드는 그대로입니다.
    /// </summary>
    public class GameDatabase
    {
        public const string ResourceFolder = "Data";

        public readonly Dictionary<string, CharacterRow> Characters = new();
        public readonly Dictionary<string, RoomRow>      Rooms      = new();
        public readonly Dictionary<string, EvidenceRow>  Evidences  = new();
        public readonly Dictionary<string, PuzzleRow>    Puzzles    = new();
        public readonly Dictionary<string, FlagRow>      Flags      = new();

        public readonly List<HotspotRow>       Hotspots       = new();
        public readonly List<DialogueRow>      Dialogues      = new();
        public readonly List<ChoiceRow>        Choices        = new();
        public readonly List<AskTopicRow>      AskTopics      = new();
        public readonly List<DeductionSlotRow> DeductionSlots = new();
        public readonly List<EndingRow>        Endings        = new();

        // 조회 성능용 인덱스
        readonly Dictionary<string, List<DialogueRow>> _dialogueById = new();
        readonly Dictionary<string, List<HotspotRow>>  _hotspotByRoom = new();

        public List<string> LoadWarnings { get; } = new();

        // ── 로드 ──────────────────────────────────────────────────────────────

        public static GameDatabase Load()
        {
            var db = new GameDatabase();
            db.LoadAll();
            return db;
        }

        void LoadAll()
        {
            LoadTable("Characters", r => { var x = CharacterRow.FromRow(r); if (Valid(x.Id, "Characters")) Characters[x.Id] = x; });
            LoadTable("Rooms",      r => { var x = RoomRow.FromRow(r);      if (Valid(x.Id, "Rooms"))      Rooms[x.Id] = x; });
            LoadTable("Evidence",   r => { var x = EvidenceRow.FromRow(r);  if (Valid(x.Id, "Evidence"))   Evidences[x.Id] = x; });
            LoadTable("Puzzles",    r => { var x = PuzzleRow.FromRow(r);    if (Valid(x.Id, "Puzzles"))    Puzzles[x.Id] = x; });
            LoadTable("Flags",      r => { var x = FlagRow.FromRow(r);      if (Valid(x.Id, "Flags"))      Flags[x.Id] = x; });

            LoadTable("Hotspots",       r => { var x = HotspotRow.FromRow(r);       if (Valid(x.Id, "Hotspots"))            Hotspots.Add(x); });
            LoadTable("Dialogues",      r => { var x = DialogueRow.FromRow(r);      if (Valid(x.DialogueId, "Dialogues"))    Dialogues.Add(x); });
            LoadTable("Choices",        r => { var x = ChoiceRow.FromRow(r);        if (Valid(x.Id, "Choices"))             Choices.Add(x); });
            LoadTable("AskTopics",      r => { var x = AskTopicRow.FromRow(r);      if (Valid(x.Id, "AskTopics"))           AskTopics.Add(x); });
            LoadTable("DeductionSlots", r => { var x = DeductionSlotRow.FromRow(r); if (Valid(x.PuzzleId, "DeductionSlots")) DeductionSlots.Add(x); });
            LoadTable("Endings",        r => { var x = EndingRow.FromRow(r);        if (Valid(x.Id, "Endings"))             Endings.Add(x); });

            // UI 문구는 게임 데이터가 아니라 화면에 박혀 있던 말들이라 Loc 이 직접 들고 갑니다.
            Core.Loc.ClearUiStrings();
            LoadTable("UIStrings", r =>
            {
                var key = CsvParser.Str(r, "string_id");
                if (!Valid(key, "UIStrings")) return;
                Core.Loc.RegisterUiString(key, CsvParser.Str(r, "ko"), CsvParser.Str(r, "en"));
            });

            BuildIndices();
        }

        bool Valid(string id, string table)
        {
            if (!string.IsNullOrEmpty(id)) return true;
            LoadWarnings.Add($"[{table}] ID가 비어 있는 행을 건너뛰었습니다.");
            return false;
        }

        void LoadTable(string tableName, System.Action<Dictionary<string, string>> handler)
        {
            var asset = Resources.Load<TextAsset>($"{ResourceFolder}/{tableName}");
            if (asset == null)
            {
                LoadWarnings.Add($"[{tableName}] Resources/{ResourceFolder}/{tableName}.csv 를 찾지 못했습니다.");
                return;
            }

            var rows = CsvParser.Parse(asset.text);
            foreach (var r in rows) handler(r);
        }

        void BuildIndices()
        {
            _dialogueById.Clear();
            foreach (var d in Dialogues)
            {
                if (!_dialogueById.TryGetValue(d.DialogueId, out var list))
                    _dialogueById[d.DialogueId] = list = new List<DialogueRow>();
                list.Add(d);
            }
            foreach (var kv in _dialogueById)
                kv.Value.Sort((a, b) => a.LineNo.CompareTo(b.LineNo));

            _hotspotByRoom.Clear();
            foreach (var h in Hotspots)
            {
                if (!_hotspotByRoom.TryGetValue(h.RoomId, out var list))
                    _hotspotByRoom[h.RoomId] = list = new List<HotspotRow>();
                list.Add(h);
            }
        }

        // ── 조회 ──────────────────────────────────────────────────────────────

        public List<DialogueRow> GetDialogue(string dialogueId)
        {
            return _dialogueById.TryGetValue(dialogueId, out var list) ? list : null;
        }

        public List<HotspotRow> GetHotspots(string roomId)
        {
            return _hotspotByRoom.TryGetValue(roomId, out var list) ? list : new List<HotspotRow>();
        }

        public List<ChoiceRow> GetChoices(string dialogueId, int lineNo)
        {
            var result = new List<ChoiceRow>();
            foreach (var c in Choices)
                if (c.DialogueId == dialogueId && c.LineNo == lineNo) result.Add(c);
            return result;
        }

        public AskTopicRow GetAskTopic(string characterId, string evidenceId)
        {
            foreach (var a in AskTopics)
                if (a.CharacterId == characterId && a.EvidenceId == evidenceId) return a;
            return null;
        }

        public List<DeductionSlotRow> GetDeductionSlots(string puzzleId)
        {
            var result = new List<DeductionSlotRow>();
            foreach (var s in DeductionSlots)
                if (s.PuzzleId == puzzleId) result.Add(s);
            result.Sort((a, b) => a.SlotNo.CompareTo(b.SlotNo));
            return result;
        }

        public CharacterRow FindCharacterInRoom(string roomId)
        {
            foreach (var kv in Characters)
                if (kv.Value.HomeRoom == roomId) return kv.Value;
            return null;
        }

        public string CharacterName(string characterId)
        {
            if (string.IsNullOrEmpty(characterId)) return string.Empty;
            return Characters.TryGetValue(characterId, out var c) ? c.DisplayName : characterId;
        }

        // ── 무결성 검사 ───────────────────────────────────────────────────────

        /// <summary>
        /// 시트끼리 ID 참조가 깨진 곳을 찾아냅니다.
        /// 오타 하나로 게임이 조용히 멈추는 걸 막아주는, 이 프로젝트에서 가장 값싼 보험입니다.
        /// </summary>
        public List<string> Validate()
        {
            var errors = new List<string>(LoadWarnings);

            void MustDialogue(string id, string where)
            {
                if (!string.IsNullOrEmpty(id) && GetDialogue(id) == null)
                    errors.Add($"{where} → 존재하지 않는 대화 '{id}'");
            }
            void MustRoom(string id, string where)
            {
                if (!string.IsNullOrEmpty(id) && !Rooms.ContainsKey(id))
                    errors.Add($"{where} → 존재하지 않는 방 '{id}'");
            }
            void MustEvidence(string id, string where)
            {
                if (!string.IsNullOrEmpty(id) && !Evidences.ContainsKey(id))
                    errors.Add($"{where} → 존재하지 않는 증거 '{id}'");
            }
            // 조건식은 "!flg_a", "flg_a,flg_b", "flg_a|flg_b" 형태가 올 수 있으므로 토큰으로 쪼개 검사합니다.
            void MustFlag(string expression, string where)
            {
                if (string.IsNullOrWhiteSpace(expression)) return;

                foreach (var raw in expression.Split(',', '|'))
                {
                    var token = raw.Trim().TrimStart('!').Trim();
                    if (token.Length == 0) continue;
                    if (!Flags.ContainsKey(token))
                        errors.Add($"{where} → Flags 탭에 없는 플래그 '{token}'");
                }
            }

            foreach (var h in Hotspots)
            {
                MustRoom(h.RoomId, $"Hotspots[{h.Id}].room_id");
                MustFlag(h.RequiredFlag, $"Hotspots[{h.Id}].필요플래그");
                if (h.Type == "move") MustRoom(h.Target, $"Hotspots[{h.Id}].연결대화(이동 대상)");
                else MustDialogue(h.Target, $"Hotspots[{h.Id}].연결대화");
            }

            foreach (var d in Dialogues)
            {
                if (!string.IsNullOrEmpty(d.SpeakerId) && !Characters.ContainsKey(d.SpeakerId))
                    errors.Add($"Dialogues[{d.DialogueId}:{d.LineNo}] → 존재하지 않는 인물 '{d.SpeakerId}'");
                MustDialogue(d.NextDialogue, $"Dialogues[{d.DialogueId}:{d.LineNo}].다음대화");
                MustFlag(d.RequiredFlag, $"Dialogues[{d.DialogueId}:{d.LineNo}].조건플래그");
                foreach (var tag in TagUtil.ExtractTags(d.Tags))
                {
                    switch (tag.Key)
                    {
                        case "flag":   MustFlag(tag.Value, $"Dialogues[{d.DialogueId}:{d.LineNo}] [flag:]"); break;
                        case "get":    MustEvidence(tag.Value, $"Dialogues[{d.DialogueId}:{d.LineNo}] [get:]"); break;
                        case "goto":   MustDialogue(tag.Value, $"Dialogues[{d.DialogueId}:{d.LineNo}] [goto:]"); break;
                        case "puzzle":
                            if (!Puzzles.ContainsKey(tag.Value))
                                errors.Add($"Dialogues[{d.DialogueId}:{d.LineNo}] [puzzle:] → 존재하지 않는 퍼즐 '{tag.Value}'");
                            break;
                    }
                }
            }

            foreach (var c in Choices)
            {
                MustDialogue(c.DialogueId, $"Choices[{c.Id}].dialogue_id");
                MustDialogue(c.NextDialogue, $"Choices[{c.Id}].이동대화");
                MustFlag(c.SetFlag, $"Choices[{c.Id}].설정플래그");
            }

            foreach (var a in AskTopics)
            {
                if (!Characters.ContainsKey(a.CharacterId))
                    errors.Add($"AskTopics[{a.Id}] → 존재하지 않는 인물 '{a.CharacterId}'");
                MustEvidence(a.EvidenceId, $"AskTopics[{a.Id}]");
                MustDialogue(a.DialogueId, $"AskTopics[{a.Id}].연결대화");
            }

            foreach (var kv in Puzzles)
            {
                var p = kv.Value;
                MustDialogue(p.SuccessDialogue, $"Puzzles[{p.Id}].성공대화");
                MustDialogue(p.FailDialogue, $"Puzzles[{p.Id}].실패대화");
                MustFlag(p.SuccessFlag, $"Puzzles[{p.Id}].성공플래그");
                if (p.Type.Equals("Deduction", System.StringComparison.OrdinalIgnoreCase)
                    && GetDeductionSlots(p.Id).Count == 0)
                    errors.Add($"Puzzles[{p.Id}] 는 Deduction 인데 DeductionSlots 탭에 슬롯이 하나도 없습니다.");
            }

            foreach (var s in DeductionSlots)
            {
                if (!Puzzles.ContainsKey(s.PuzzleId))
                    errors.Add($"DeductionSlots[{s.PuzzleId}:{s.SlotNo}] → 존재하지 않는 퍼즐");
                MustEvidence(s.AnswerEvidence, $"DeductionSlots[{s.PuzzleId}:{s.SlotNo}].정답evidence");
                MustDialogue(s.WrongDialogue, $"DeductionSlots[{s.PuzzleId}:{s.SlotNo}].오답반응대화");
            }

            foreach (var r in Rooms.Values) MustFlag(r.RequiredFlag, $"Rooms[{r.Id}].해금조건플래그");
            foreach (var e in Endings) { MustDialogue(e.DialogueId, $"Endings[{e.Id}].연출대화"); MustFlag(e.RequiredFlag, $"Endings[{e.Id}].조건플래그"); }

            return errors;
        }

        public string Summary()
        {
            var sb = new StringBuilder();
            sb.Append($"인물 {Characters.Count} / 방 {Rooms.Count} / 핫스팟 {Hotspots.Count} / ");
            sb.Append($"대사 {Dialogues.Count}줄 / 선택지 {Choices.Count} / 증거 {Evidences.Count} / ");
            sb.Append($"질문 {AskTopics.Count} / 퍼즐 {Puzzles.Count} / 플래그 {Flags.Count} / 엔딩 {Endings.Count}");
            return sb.ToString();
        }
    }
}
