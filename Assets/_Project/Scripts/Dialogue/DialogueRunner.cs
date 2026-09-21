using System;
using System.Collections;
using System.Collections.Generic;
using SevenDoctors.Core;
using SevenDoctors.Data;
using UnityEngine;

namespace SevenDoctors.Dialogue
{
    /// <summary>
    /// 대사 재생기. 시트의 Dialogues / Choices 를 그대로 먹고 돌립니다.
    ///
    /// 한 줄의 처리 순서:
    ///   1) 연출 태그 중 '보여주기 전'에 해당하는 것 실행  (bgm, sfx, portrait, fade, shake, wait)
    ///   2) 타이핑 출력 → 클릭 대기 (클릭하면 즉시 전체 출력, 한 번 더 누르면 다음 줄)
    ///   3) 상태 태그 실행                                (flag, get)
    ///   4) 이 줄에 선택지가 달려 있으면 선택지 표시
    ///   5) 흐름 태그 실행                                (puzzle, goto)
    /// </summary>
    public class DialogueRunner : MonoBehaviour
    {
        [Tooltip("글자 하나가 찍히는 간격(초)")]
        public float CharInterval = 0.022f;

        public bool IsPlaying { get; private set; }

        bool _advanceRequested;
        Coroutine _current;

        void Awake()
        {
            // UIRoot 가 먼저 만들어져 있어야 합니다 (GameBootstrap 이 순서를 보장).
            if (Game.UI != null) Game.UI.Dialogue.Clicked += OnDialogueClicked;
        }

        void OnDestroy()
        {
            if (Game.UI != null && Game.UI.Dialogue != null) Game.UI.Dialogue.Clicked -= OnDialogueClicked;
        }

        void OnDialogueClicked() => _advanceRequested = true;

        /// <summary>
        /// 스페이스/엔터도 대화창 클릭과 똑같이 칩니다. 한 손으로 계속 넘기게 됩니다.
        ///
        /// 노트나 퍼즐이 떠 있을 때는 받지 않습니다 — 스페이스는 버튼을 누르는
        /// 키이기도 해서, 그대로 두면 노트에서 스페이스를 눌렀을 때 뒤에서
        /// 대사까지 같이 넘어갑니다. 선택지가 떠 있을 때도 마찬가지로 빠집니다:
        /// 어느 쪽을 고를지 정하지 않은 채 넘겨 버리면 안 됩니다.
        /// </summary>
        void Update()
        {
            if (!IsPlaying || !WasAdvanceKeyPressed()) return;
            if (Game.State == GameState.InPuzzle) return;
            if (Game.UI == null) return;
            if (Game.UI.Notebook != null && Game.UI.Notebook.IsVisible) return;
            if (Game.UI.Choices != null && Game.UI.Choices.IsVisible) return;

            _advanceRequested = true;
        }

        static bool WasAdvanceKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            var kb = UnityEngine.InputSystem.Keyboard.current;
            return kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame);
#else
            return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return);
#endif
        }

        // ── 진입점 ────────────────────────────────────────────────────────────

        public void Play(string dialogueId, Action onComplete = null)
        {
            if (string.IsNullOrEmpty(dialogueId)) { onComplete?.Invoke(); return; }

            if (IsPlaying)
            {
                Debug.LogWarning($"[Dialogue] 이미 재생 중이라 '{dialogueId}' 요청을 무시했습니다.");
                return;
            }

            _current = StartCoroutine(RunRoutine(dialogueId, onComplete));
        }

        IEnumerator RunRoutine(string startId, Action onComplete)
        {
            IsPlaying = true;
            var previousState = Game.State;
            Game.State = GameState.InDialogue;

            yield return PlayCore(startId);

            Game.UI.Dialogue.Hide();
            Game.UI.Choices.Hide();
            Game.UI.Portrait?.Hide();
            IsPlaying = false;
            _current = null;

            Game.State = previousState == GameState.InDialogue ? GameState.Exploring : previousState;
            onComplete?.Invoke();
        }

        /// <summary>
        /// 대화 재생의 알맹이. 상태(IsPlaying/GameState)를 건드리지 않으므로
        /// 퍼즐 안에서 오답 반응 대사를 틀 때처럼 '중첩 호출'이 가능합니다.
        /// 다른 코루틴에서 yield return 으로 직접 부르세요.
        /// </summary>
        public IEnumerator PlayCore(string startId)
        {
            string currentId = startId;
            int guard = 0;

            while (!string.IsNullOrEmpty(currentId))
            {
                if (++guard > 512)
                {
                    Debug.LogError($"[Dialogue] 대화가 512회 이상 연쇄됐습니다. 순환 참조를 의심하세요. (마지막: {currentId})");
                    break;
                }

                var lines = Game.Db.GetDialogue(currentId);
                if (lines == null)
                {
                    Debug.LogError($"[Dialogue] '{currentId}' 대화를 찾을 수 없습니다. 시트의 dialogue_id 를 확인하세요.");
                    break;
                }

                string jumpTo = null;
                DialogueRow lastShown = null;

                foreach (var line in lines)
                {
                    if (!Game.Flags.Check(line.RequiredFlag)) continue;

                    var tags = TagUtil.ExtractTags(line.Tags);

                    // 1) 보여주기 전 연출
                    yield return ProcessPresentationTags(tags);

                    // 2) 출력 + 클릭 대기
                    lastShown = line;
                    yield return ShowLine(line);

                    // 3) 상태 변화
                    ProcessStateTags(tags);

                    // 4) 선택지
                    var choices = FilterChoices(Game.Db.GetChoices(currentId, line.LineNo));
                    if (choices.Count > 0)
                    {
                        ChoiceRow picked = null;
                        Game.UI.Choices.Show(choices, c => picked = c);
                        while (picked == null) yield return null;

                        Game.Flags.Set(picked.SetFlag, true);
                        if (!string.IsNullOrEmpty(picked.NextDialogue)) { jumpTo = picked.NextDialogue; break; }
                    }

                    // 5) 흐름 제어
                    string gotoTarget = null;
                    foreach (var tag in tags)
                    {
                        if (tag.Key == "puzzle")
                        {
                            Game.UI.Dialogue.Hide();
                            yield return Game.Puzzle.RunRoutine(tag.Value);
                        }
                        else if (tag.Key == "goto")
                        {
                            gotoTarget = tag.Value;
                        }
                    }
                    if (!string.IsNullOrEmpty(gotoTarget)) { jumpTo = gotoTarget; break; }
                }

                if (!string.IsNullOrEmpty(jumpTo)) { currentId = jumpTo; continue; }
                currentId = lastShown != null ? lastShown.NextDialogue : null;
            }
        }

        List<ChoiceRow> FilterChoices(List<ChoiceRow> source)
        {
            var result = new List<ChoiceRow>();
            foreach (var c in source)
                if (Game.Flags.Check(c.RequiredFlag)) result.Add(c);
            return result;
        }

        // ── 한 줄 출력 ────────────────────────────────────────────────────────

        IEnumerator ShowLine(DialogueRow line)
        {
            string speaker = Game.Db.CharacterName(line.SpeakerId);
            string body = TagUtil.StripTags(line.Text);

            ShowPortrait(line);
            Game.UI.Dialogue.Show(speaker, string.Empty, false);
            _advanceRequested = false;

            // 글자가 찍히는 동안만 입이 움직입니다. 지문(화자 없음)이면 포트레이트가
            // 숨겨져 있으니 그대로 둡니다.
            bool lipSync = !string.IsNullOrEmpty(line.SpeakerId) && Game.UI.Portrait != null;
            if (lipSync) Game.UI.Portrait.SetSpeaking(true);

            // 타이핑
            int shown = 0;
            float timer = 0f;
            int sinceVoice = 0;
            while (shown < body.Length)
            {
                if (_advanceRequested) { shown = body.Length; _advanceRequested = false; break; }

                timer += Time.deltaTime;
                while (timer >= CharInterval && shown < body.Length)
                {
                    timer -= CharInterval;
                    shown++;

                    if (lipSync && ShouldVoice(body[shown - 1], ref sinceVoice))
                        Game.Audio?.PlayVoice(body[shown - 1], line.SpeakerId);
                }
                Game.UI.Dialogue.SetBodyTyping(body, shown);
                yield return null;
            }

            if (lipSync) Game.UI.Portrait.SetSpeaking(false);
            Game.Audio?.StopVoice();

            Game.UI.Dialogue.SetBody(body);
            Game.UI.Dialogue.SetArrow(true);

            // 클릭 대기 (같은 프레임에 눌린 클릭이 그대로 먹히지 않게 한 프레임 비움)
            yield return null;
            _advanceRequested = false;
            while (!_advanceRequested) yield return null;
            _advanceRequested = false;

            Game.UI.Dialogue.SetArrow(false);
        }

        /// <summary>
        /// 이 글자에서 말소리를 낼지. 글자마다 전부 내면 기관총처럼 들립니다.
        ///
        /// 공백과 문장부호는 건너뜁니다 — 사람은 거기서 소리를 내지 않고,
        /// 쉼표에서 한 박 쉬는 게 오히려 말처럼 들립니다. 나머지는 두 글자에
        /// 한 번만 냅니다. 한글은 한 글자가 한 음절이라 이 정도가 맞습니다.
        /// </summary>
        static bool ShouldVoice(char c, ref int sinceVoice)
        {
            if (char.IsWhiteSpace(c) || char.IsPunctuation(c) || char.IsSymbol(c))
            {
                sinceVoice = 1;   // 쉬고 나면 다음 글자에서 바로 소리가 나게
                return false;
            }

            if (++sinceVoice < 2) return false;
            sinceVoice = 0;
            return true;
        }

        // ── 태그 처리 ─────────────────────────────────────────────────────────

        IEnumerator ProcessPresentationTags(List<TagUtil.Tag> tags)
        {
            foreach (var tag in tags)
            {
                switch (tag.Key)
                {
                    case "wait":
                        if (float.TryParse(tag.Value, out var seconds))
                            yield return new WaitForSeconds(Mathf.Clamp(seconds, 0f, 10f));
                        break;

                    case "shake":
                        yield return Game.UI.Shake();
                        break;

                    case "fade_out":
                        yield return Game.UI.Fade(0f, 1f, 0.45f);
                        break;

                    case "fade_in":
                        yield return Game.UI.Fade(1f, 0f, 0.45f);
                        break;

                    case "bgm":
                        Game.Audio?.PlayBgm(tag.Value);
                        break;

                    case "sfx":
                        Game.Audio?.PlaySfx(tag.Value);
                        break;

                    case "portrait":
                        ApplyPortraitTag(tag.Value, tag.Value2);
                        break;
                }
            }
        }

        /// <summary>화자에 맞는 포트레이트를 띄웁니다. 지문(화자 없음)이면 숨깁니다.</summary>
        void ShowPortrait(DialogueRow line)
        {
            if (Game.UI == null || Game.UI.Portrait == null) return;

            if (string.IsNullOrEmpty(line.SpeakerId) ||
                !Game.Db.Characters.TryGetValue(line.SpeakerId, out var character))
            {
                Game.UI.Portrait.Hide();
                return;
            }

            string face = !string.IsNullOrEmpty(line.Face) ? line.Face : character.DefaultFace;
            Game.UI.Portrait.Show(character.PortraitKey, face);
        }

        /// <summary>[portrait:인물ID:표정] 태그 처리.</summary>
        void ApplyPortraitTag(string characterId, string faceKey)
        {
            if (Game.UI == null || Game.UI.Portrait == null) return;

            if (!string.IsNullOrEmpty(characterId) &&
                Game.Db.Characters.TryGetValue(characterId, out var character))
                Game.UI.Portrait.Show(character.PortraitKey, faceKey);
            else
                Game.UI.Portrait.SetFace(faceKey);
        }

        void ProcessStateTags(List<TagUtil.Tag> tags)
        {
            foreach (var tag in tags)
            {
                switch (tag.Key)
                {
                    case "flag":
                        Game.Flags.Set(tag.Value, true);
                        break;

                    case "unflag":
                        Game.Flags.Set(tag.Value, false);
                        break;

                    case "get":
                        if (Game.Evidence.Add(tag.Value))
                        {
                            string label = Game.Db.Evidences.TryGetValue(tag.Value, out var ev)
                                ? ev.DisplayName : tag.Value;
                            Game.UI.Toast(Loc.T("ui.evidence.gained", label));
                            Game.UI.RefreshEvidenceCount();
                        }
                        break;
                }
            }
        }

        public void StopAll()
        {
            if (_current != null) StopCoroutine(_current);
            _current = null;
            IsPlaying = false;
            if (Game.UI != null) { Game.UI.Dialogue.Hide(); Game.UI.Choices.Hide(); }
        }
    }
}
