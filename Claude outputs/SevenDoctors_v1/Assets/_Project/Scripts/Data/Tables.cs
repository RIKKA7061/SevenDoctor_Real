using System;
using System.Collections.Generic;

namespace SevenDoctors.Data
{
    /// <summary>
    /// 구글 시트 각 탭에 1:1로 대응하는 행 클래스들.
    /// 시트에 컬럼을 추가하면 여기 필드를 추가하고 FromRow 에 한 줄만 더하면 됩니다.
    /// </summary>

    [Serializable]
    public class CharacterRow
    {
        public string Id, DisplayName, Emotion, HomeRoom, DefaultFace, PortraitKey, Note;

        public static CharacterRow FromRow(Dictionary<string, string> r) => new CharacterRow
        {
            Id          = CsvParser.Str(r, "character_id"),
            DisplayName = CsvParser.Str(r, "표시명"),
            Emotion     = CsvParser.Str(r, "감정속성"),
            HomeRoom    = CsvParser.Str(r, "상주방"),
            DefaultFace = CsvParser.Str(r, "기본표정", "normal"),
            PortraitKey = CsvParser.Str(r, "포트레이트키"),
            Note        = CsvParser.Str(r, "비고"),
        };
    }

    [Serializable]
    public class RoomRow
    {
        public string Id, DisplayName, BackgroundKey, RequiredFlag, Bgm, Note;

        public static RoomRow FromRow(Dictionary<string, string> r) => new RoomRow
        {
            Id            = CsvParser.Str(r, "room_id"),
            DisplayName   = CsvParser.Str(r, "표시명"),
            BackgroundKey = CsvParser.Str(r, "배경키"),
            RequiredFlag  = CsvParser.Str(r, "해금조건플래그"),
            Bgm           = CsvParser.Str(r, "BGM"),
            Note          = CsvParser.Str(r, "비고"),
        };
    }

    [Serializable]
    public class HotspotRow
    {
        public string Id, RoomId, DisplayName, Type, Target, RequiredFlag, Note;
        public bool Once;
        /// <summary>정규화 좌표(0~1). 넷 다 0이면 자동 배치 모드로 동작합니다.</summary>
        public float X, Y, W, H;

        public bool HasRect => W > 0.0001f && H > 0.0001f;

        public static HotspotRow FromRow(Dictionary<string, string> r) => new HotspotRow
        {
            Id           = CsvParser.Str(r, "hotspot_id"),
            RoomId       = CsvParser.Str(r, "room_id"),
            DisplayName  = CsvParser.Str(r, "표시명"),
            Type         = CsvParser.Str(r, "타입", "look").ToLowerInvariant(),
            Target       = CsvParser.Str(r, "연결대화"),
            RequiredFlag = CsvParser.Str(r, "필요플래그"),
            Once         = CsvParser.Bool(r, "1회성"),
            X            = CsvParser.Float(r, "x"),
            Y            = CsvParser.Float(r, "y"),
            W            = CsvParser.Float(r, "w"),
            H            = CsvParser.Float(r, "h"),
            Note         = CsvParser.Str(r, "비고"),
        };
    }

    [Serializable]
    public class DialogueRow
    {
        public string DialogueId, SpeakerId, Face, Text, Tags, RequiredFlag, NextDialogue, Note;
        public int LineNo;

        public static DialogueRow FromRow(Dictionary<string, string> r) => new DialogueRow
        {
            DialogueId   = CsvParser.Str(r, "dialogue_id"),
            LineNo       = CsvParser.Int(r, "line_no"),
            SpeakerId    = CsvParser.Str(r, "speaker_id"),
            Face         = CsvParser.Str(r, "표정"),
            Text         = CsvParser.Str(r, "대사"),
            Tags         = CsvParser.Str(r, "연출태그"),
            RequiredFlag = CsvParser.Str(r, "조건플래그"),
            NextDialogue = CsvParser.Str(r, "다음대화"),
            Note         = CsvParser.Str(r, "비고"),
        };
    }

    [Serializable]
    public class ChoiceRow
    {
        public string Id, DialogueId, Text, NextDialogue, RequiredFlag, SetFlag, Note;
        public int LineNo;

        public static ChoiceRow FromRow(Dictionary<string, string> r) => new ChoiceRow
        {
            Id           = CsvParser.Str(r, "choice_id"),
            DialogueId   = CsvParser.Str(r, "dialogue_id"),
            LineNo       = CsvParser.Int(r, "line_no"),
            Text         = CsvParser.Str(r, "선택지텍스트"),
            NextDialogue = CsvParser.Str(r, "이동대화"),
            RequiredFlag = CsvParser.Str(r, "조건플래그"),
            SetFlag      = CsvParser.Str(r, "설정플래그"),
            Note         = CsvParser.Str(r, "비고"),
        };
    }

    [Serializable]
    public class EvidenceRow
    {
        public string Id, DisplayName, Category, Description, IconKey, Source, Note;

        public static EvidenceRow FromRow(Dictionary<string, string> r) => new EvidenceRow
        {
            Id          = CsvParser.Str(r, "evidence_id"),
            DisplayName = CsvParser.Str(r, "이름"),
            Category    = CsvParser.Str(r, "카테고리"),
            Description = CsvParser.Str(r, "설명"),
            IconKey     = CsvParser.Str(r, "아이콘키"),
            Source      = CsvParser.Str(r, "획득처"),
            Note        = CsvParser.Str(r, "비고"),
        };
    }

    [Serializable]
    public class AskTopicRow
    {
        public string Id, CharacterId, EvidenceId, DialogueId, RequiredFlag, Note;
        public bool Once;

        public static AskTopicRow FromRow(Dictionary<string, string> r) => new AskTopicRow
        {
            Id           = CsvParser.Str(r, "ask_id"),
            CharacterId  = CsvParser.Str(r, "character_id"),
            EvidenceId   = CsvParser.Str(r, "evidence_id"),
            DialogueId   = CsvParser.Str(r, "연결대화"),
            RequiredFlag = CsvParser.Str(r, "필요플래그"),
            Once         = CsvParser.Bool(r, "1회성"),
            Note         = CsvParser.Str(r, "비고"),
        };
    }

    [Serializable]
    public class PuzzleRow
    {
        public string Id, Type, Title, RoomId, StartDialogue, Answer,
                      SuccessFlag, SuccessDialogue, FailDialogue, Hint, Note;

        public static PuzzleRow FromRow(Dictionary<string, string> r) => new PuzzleRow
        {
            Id              = CsvParser.Str(r, "puzzle_id"),
            Type            = CsvParser.Str(r, "타입", "Code"),
            Title           = CsvParser.Str(r, "제목"),
            RoomId          = CsvParser.Str(r, "발생방"),
            StartDialogue   = CsvParser.Str(r, "발동대화"),
            Answer          = CsvParser.Str(r, "정답"),
            SuccessFlag     = CsvParser.Str(r, "성공플래그"),
            SuccessDialogue = CsvParser.Str(r, "성공대화"),
            FailDialogue    = CsvParser.Str(r, "실패대화"),
            Hint            = CsvParser.Str(r, "힌트텍스트"),
            Note            = CsvParser.Str(r, "비고"),
        };
    }

    [Serializable]
    public class DeductionSlotRow
    {
        public string PuzzleId, TextBefore, TextAfter, AnswerEvidence, Category, WrongDialogue, Note;
        public int SlotNo;

        public static DeductionSlotRow FromRow(Dictionary<string, string> r) => new DeductionSlotRow
        {
            PuzzleId       = CsvParser.Str(r, "puzzle_id"),
            SlotNo         = CsvParser.Int(r, "slot_no"),
            TextBefore     = CsvParser.Str(r, "앞문장"),
            TextAfter      = CsvParser.Str(r, "뒷문장"),
            AnswerEvidence = CsvParser.Str(r, "정답evidence"),
            Category       = CsvParser.Str(r, "후보카테고리"),
            WrongDialogue  = CsvParser.Str(r, "오답반응대화"),
            Note           = CsvParser.Str(r, "비고"),
        };
    }

    [Serializable]
    public class FlagRow
    {
        public string Id, Description, Chapter, Note;
        public bool InitialValue;

        public static FlagRow FromRow(Dictionary<string, string> r) => new FlagRow
        {
            Id           = CsvParser.Str(r, "flag_id"),
            Description  = CsvParser.Str(r, "설명"),
            InitialValue = CsvParser.Bool(r, "초기값"),
            Chapter      = CsvParser.Str(r, "챕터"),
            Note         = CsvParser.Str(r, "비고"),
        };
    }

    [Serializable]
    public class EndingRow
    {
        public string Id, DisplayName, ChoiceText, RequiredFlag, DialogueId, Note;
        public int Priority;

        public static EndingRow FromRow(Dictionary<string, string> r) => new EndingRow
        {
            Id           = CsvParser.Str(r, "ending_id"),
            DisplayName  = CsvParser.Str(r, "엔딩명"),
            ChoiceText   = CsvParser.Str(r, "선택지텍스트"),
            RequiredFlag = CsvParser.Str(r, "조건플래그"),
            DialogueId   = CsvParser.Str(r, "연출대화"),
            Priority     = CsvParser.Int(r, "우선순위"),
            Note         = CsvParser.Str(r, "비고"),
        };
    }
}
