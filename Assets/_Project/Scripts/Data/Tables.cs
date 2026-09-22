using System;
using System.Collections.Generic;
using SevenDoctors.Core;
using UnityEngine;

namespace SevenDoctors.Data
{
    /// <summary>
    /// 구글 시트 각 탭에 1:1로 대응하는 행 클래스들.
    /// 시트에 컬럼을 추가하면 여기 필드를 추가하고 FromRow 에 한 줄만 더하면 됩니다.
    ///
    /// 화면에 보이는 칸은 '_en' 컬럼을 짝으로 두고, 원래 이름은 프로퍼티로
    /// 남겨 뒀습니다 (DisplayName → DisplayNameKo/En 중 하나). 그래서 부르는
    /// 쪽은 언어를 전혀 몰라도 되고, 언어를 바꾸면 다음 접근부터 바로 바뀝니다.
    /// 번역이 비어 있으면 한국어로 돌아갑니다.
    /// </summary>

    [Serializable]
    public class CharacterRow
    {
        public string Id, Emotion, HomeRoom, DefaultFace, PortraitKey, Note;
        public string DisplayNameKo, DisplayNameEn;

        public string DisplayName => Loc.Pick(DisplayNameKo, DisplayNameEn);

        public static CharacterRow FromRow(Dictionary<string, string> r) => new CharacterRow
        {
            Id            = CsvParser.Str(r, "character_id"),
            DisplayNameKo = CsvParser.Str(r, "표시명"),
            DisplayNameEn = CsvParser.Str(r, "표시명_en"),
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
        public string Id, BackgroundKey, RequiredFlag, Bgm, Note;
        public string DisplayNameKo, DisplayNameEn;

        public string DisplayName => Loc.Pick(DisplayNameKo, DisplayNameEn);

        public static RoomRow FromRow(Dictionary<string, string> r) => new RoomRow
        {
            Id            = CsvParser.Str(r, "room_id"),
            DisplayNameKo = CsvParser.Str(r, "표시명"),
            DisplayNameEn = CsvParser.Str(r, "표시명_en"),
            BackgroundKey = CsvParser.Str(r, "배경키"),
            RequiredFlag  = CsvParser.Str(r, "해금조건플래그"),
            Bgm           = CsvParser.Str(r, "BGM"),
            Note          = CsvParser.Str(r, "비고"),
        };
    }

    [Serializable]
    public class HotspotRow
    {
        public string Id, RoomId, Type, Target, RequiredFlag, Note;
        public string DisplayNameKo, DisplayNameEn;
        public bool Once;

        public string DisplayName => Loc.Pick(DisplayNameKo, DisplayNameEn);
        /// <summary>정규화 좌표(0~1). 넷 다 0이면 자동 배치 모드로 동작합니다.</summary>
        public float X, Y, W, H;

        public bool HasRect => W > 0.0001f && H > 0.0001f;

        public static HotspotRow FromRow(Dictionary<string, string> r) => new HotspotRow
        {
            Id           = CsvParser.Str(r, "hotspot_id"),
            RoomId       = CsvParser.Str(r, "room_id"),
            DisplayNameKo = CsvParser.Str(r, "표시명"),
            DisplayNameEn = CsvParser.Str(r, "표시명_en"),
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
        public string DialogueId, SpeakerId, Face, Tags, RequiredFlag, NextDialogue, Note;
        public string TextKo, TextEn;
        public int LineNo;

        public string Text => Loc.Pick(TextKo, TextEn);

        public static DialogueRow FromRow(Dictionary<string, string> r) => new DialogueRow
        {
            DialogueId   = CsvParser.Str(r, "dialogue_id"),
            LineNo       = CsvParser.Int(r, "line_no"),
            SpeakerId    = CsvParser.Str(r, "speaker_id"),
            Face         = CsvParser.Str(r, "표정"),
            TextKo       = CsvParser.Str(r, "대사"),
            TextEn       = CsvParser.Str(r, "대사_en"),
            Tags         = CsvParser.Str(r, "연출태그"),
            RequiredFlag = CsvParser.Str(r, "조건플래그"),
            NextDialogue = CsvParser.Str(r, "다음대화"),
            Note         = CsvParser.Str(r, "비고"),
        };
    }

    [Serializable]
    public class ChoiceRow
    {
        public string Id, DialogueId, NextDialogue, RequiredFlag, SetFlag, Note;
        public string TextKo, TextEn;
        public int LineNo;

        public string Text => Loc.Pick(TextKo, TextEn);

        public static ChoiceRow FromRow(Dictionary<string, string> r) => new ChoiceRow
        {
            Id           = CsvParser.Str(r, "choice_id"),
            DialogueId   = CsvParser.Str(r, "dialogue_id"),
            LineNo       = CsvParser.Int(r, "line_no"),
            TextKo       = CsvParser.Str(r, "선택지텍스트"),
            TextEn       = CsvParser.Str(r, "선택지텍스트_en"),
            NextDialogue = CsvParser.Str(r, "이동대화"),
            RequiredFlag = CsvParser.Str(r, "조건플래그"),
            SetFlag      = CsvParser.Str(r, "설정플래그"),
            Note         = CsvParser.Str(r, "비고"),
        };
    }

    [Serializable]
    public class EvidenceRow
    {
        public string Id, Category, IconKey, Source, Note;
        public string DisplayNameKo, DisplayNameEn, DescriptionKo, DescriptionEn;

        public string DisplayName => Loc.Pick(DisplayNameKo, DisplayNameEn);
        public string Description => Loc.Pick(DescriptionKo, DescriptionEn);

        /// <summary>카테고리는 ID 라 번역하지 않습니다. 화면에 보일 때만 Loc.Category 로 바꿉니다.</summary>
        public static EvidenceRow FromRow(Dictionary<string, string> r) => new EvidenceRow
        {
            Id            = CsvParser.Str(r, "evidence_id"),
            DisplayNameKo = CsvParser.Str(r, "이름"),
            DisplayNameEn = CsvParser.Str(r, "이름_en"),
            Category      = CsvParser.Str(r, "카테고리"),
            DescriptionKo = CsvParser.Str(r, "설명"),
            DescriptionEn = CsvParser.Str(r, "설명_en"),
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
        public string Id, Type, RoomId, StartDialogue, Answer,
                      SuccessFlag, SuccessDialogue, FailDialogue, Note;
        public string TitleKo, TitleEn, HintKo, HintEn;

        public string Title => Loc.Pick(TitleKo, TitleEn);
        public string Hint  => Loc.Pick(HintKo, HintEn);

        public static PuzzleRow FromRow(Dictionary<string, string> r) => new PuzzleRow
        {
            Id              = CsvParser.Str(r, "puzzle_id"),
            Type            = CsvParser.Str(r, "타입", "Code"),
            TitleKo         = CsvParser.Str(r, "제목"),
            TitleEn         = CsvParser.Str(r, "제목_en"),
            RoomId          = CsvParser.Str(r, "발생방"),
            StartDialogue   = CsvParser.Str(r, "발동대화"),
            Answer          = CsvParser.Str(r, "정답"),
            SuccessFlag     = CsvParser.Str(r, "성공플래그"),
            SuccessDialogue = CsvParser.Str(r, "성공대화"),
            FailDialogue    = CsvParser.Str(r, "실패대화"),
            HintKo          = CsvParser.Str(r, "힌트텍스트"),
            HintEn          = CsvParser.Str(r, "힌트텍스트_en"),
            Note            = CsvParser.Str(r, "비고"),
        };
    }

    [Serializable]
    public class DeductionSlotRow
    {
        public string PuzzleId, AnswerEvidence, Category, WrongDialogue, Note;
        public string TextBeforeKo, TextBeforeEn, TextAfterKo, TextAfterEn;
        public int SlotNo;

        public string TextBefore => Loc.Pick(TextBeforeKo, TextBeforeEn);
        public string TextAfter  => Loc.Pick(TextAfterKo, TextAfterEn);

        public static DeductionSlotRow FromRow(Dictionary<string, string> r) => new DeductionSlotRow
        {
            PuzzleId       = CsvParser.Str(r, "puzzle_id"),
            SlotNo         = CsvParser.Int(r, "slot_no"),
            TextBeforeKo   = CsvParser.Str(r, "앞문장"),
            TextBeforeEn   = CsvParser.Str(r, "앞문장_en"),
            TextAfterKo    = CsvParser.Str(r, "뒷문장"),
            TextAfterEn    = CsvParser.Str(r, "뒷문장_en"),
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
        public string Id, RequiredFlag, DialogueId, Note;
        public string DisplayNameKo, DisplayNameEn, ChoiceTextKo, ChoiceTextEn;
        public int Priority;

        public string DisplayName => Loc.Pick(DisplayNameKo, DisplayNameEn);
        public string ChoiceText  => Loc.Pick(ChoiceTextKo, ChoiceTextEn);

        public static EndingRow FromRow(Dictionary<string, string> r) => new EndingRow
        {
            Id            = CsvParser.Str(r, "ending_id"),
            DisplayNameKo = CsvParser.Str(r, "엔딩명"),
            DisplayNameEn = CsvParser.Str(r, "엔딩명_en"),
            ChoiceTextKo  = CsvParser.Str(r, "선택지텍스트"),
            ChoiceTextEn  = CsvParser.Str(r, "선택지텍스트_en"),
            RequiredFlag = CsvParser.Str(r, "조건플래그"),
            DialogueId   = CsvParser.Str(r, "연출대화"),
            Priority     = CsvParser.Int(r, "우선순위"),
            Note         = CsvParser.Str(r, "비고"),
        };
    }

    /// <summary>
    /// 도우미 로봇이 읽어 주는 힌트 한 줄.
    ///
    /// 대상퍼즐 칸이 비어 있으면 '진행 힌트' 입니다 — 탐색 중에 막혔을 때 씁니다.
    /// 조건플래그가 맞는 줄 중 시트에서 제일 위에 있는 것이 뽑히므로,
    /// 시트는 이야기 순서대로 적어 두어야 합니다.
    ///
    /// 대상퍼즐 칸이 채워져 있으면 그 퍼즐이 떠 있을 때만 뽑힙니다.
    ///
    /// 단계는 같은 상황에서 다시 물었을 때 얼마나 더 알려 줄지입니다.
    /// 1 은 방향만, 숫자가 커질수록 답에 가깝게 적으세요.
    /// </summary>
    [Serializable]
    public class HintRow
    {
        public string Id, PuzzleId, RequiredFlag, Note;
        public string TextKo, TextEn;
        public int Step;

        public string Text => Loc.Pick(TextKo, TextEn);

        /// <summary>퍼즐 칸이 비어 있으면 탐색 중에 쓰는 진행 힌트입니다.</summary>
        public bool IsProgressHint => string.IsNullOrEmpty(PuzzleId);

        public static HintRow FromRow(Dictionary<string, string> r) => new HintRow
        {
            Id           = CsvParser.Str(r, "hint_id"),
            PuzzleId     = CsvParser.Str(r, "대상퍼즐"),
            RequiredFlag = CsvParser.Str(r, "조건플래그"),
            Step         = Mathf.Max(1, CsvParser.Int(r, "단계", 1)),
            TextKo       = CsvParser.Str(r, "힌트"),
            TextEn       = CsvParser.Str(r, "힌트_en"),
            Note         = CsvParser.Str(r, "비고"),
        };
    }
}
