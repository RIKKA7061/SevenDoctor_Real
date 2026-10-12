using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SevenDoctors.UI
{
    /// <summary>
    /// 화자 포트레이트. 몸 스프라이트 위에 표정을 겹칩니다.
    ///
    /// 몸과 표정을 나눈 이유는 리소스 절약이기도 하지만 설정 그 자체이기도 합니다 —
    /// 일곱 박사는 전부 같은 얼굴이라, 표정 한 세트를 전원이 공유하는 게 맞습니다.
    /// (교만만 얼굴이 어둠에 묻힌 몸 스프라이트를 씁니다)
    ///
    /// 표정은 두 가지 방식 중 되는 쪽으로 자동으로 붙습니다:
    ///   · 부위별  Art/Faces/{표정}/{eyes,pupils,brows,mouth,mouth_open,extra}.png
    ///             + Art/Faces/faceparts.json
    ///             → 눈을 깜빡이고 시선이 흔들리고 말할 때 입이 움직입니다
    ///   · 합본    Art/Faces/{표정}.png
    ///             → 표정은 통째로 바뀌지만 호흡·흔들림·반응 모션은 그대로 돕니다
    /// 둘 다 없으면 조용히 숨깁니다. 아트가 안 들어온 인물이 있어도 게임은 안 멈춥니다.
    ///
    /// 손으로 그린 포트레이트는 Art/Portraits/{포트레이트키}.png 에 둡니다. 여기에 있으면
    /// Art/Characters 보다 먼저 쓰고, 그림에 얼굴이 이미 있으므로 공용 표정을 겹치지
    /// 않습니다. (턴어라운드 시트를 자르는 도구: ArtSource/cut_turnaround.py)
    ///
    /// 손그림이 아직 없는 인물은 FallbackPaintedKey(슬픔) 그림을 빌려 씁니다. 그림체를
    /// 통일해 두려는 임시 조치라, 그 인물의 {키}.png 가 Portraits 에 들어오는 순간
    /// 코드 수정 없이 그 그림으로 바뀝니다. 빈 문자열로 두면 예전 도형 몸으로 돌아갑니다.
    ///
    /// 몸 방향(SetPose)은 몸 스프라이트만 갈아 끼웁니다:
    ///   {폴더}/{포트레이트키}_{방향}.png   예) sorrow_side_left, sorrow_back
    /// 그림이 없는 방향은 가장 비슷한 방향(PoseFallbacks), 그것도 없으면 정면으로 둡니다.
    /// back 일 때는 표정을 숨깁니다.
    ///
    /// 실제 움직임은 전부 PortraitAnimator 가 맡습니다. 여기서는 부위를 원래
    /// 캔버스 좌표대로 제자리에 놓아 주는 것까지만 합니다.
    /// </summary>
    public class PortraitView
    {
        public RectTransform Root { get; private set; }

        public const string PoseFront = "front";
        public const string PoseBack = "back";

        const string PaintedFolder = "Art/Portraits";       // 얼굴까지 그려진 그림
        const string PlaceholderFolder = "Art/Characters";  // 표정을 겹치는 몸 (gen_art.py)

        /// <summary>손그림이 없는 인물이 대신 쓰는 그림. 전원 그림이 들어오면 필요 없습니다.</summary>
        public const string FallbackPaintedKey = "sorrow";

        Image _body;
        Image _face;                 // 합본 표정이자 부위들의 부모
        PortraitAnimator _anim;
        FacePartManifest _manifest;

        string _characterKey;        // 시트의 포트레이트키. 화자가 바뀌었는지 볼 때 씁니다
        string _artKey;              // 실제로 불러온 그림 파일 이름 (빌려 쓰면 FallbackPaintedKey)
        string _faceKey;
        string _pose = PoseFront;
        bool _painted;               // 지금 인물이 Art/Portraits 그림인지
        Image _paintedMouth;         // 손그림용 벌린 입 덮개. 몸의 자식
        bool _faceReady;             // 표정이 한 번이라도 지어졌는지

        string BodyFolder => _painted ? PaintedFolder : PlaceholderFolder;

        // 그림에 얼굴이 이미 있으면 겹치면 얼굴이 둘이 됩니다. 뒷모습에도 눈코입이
        // 떠 있으면 안 됩니다.
        bool FaceHidden => _painted || _pose == PoseBack;

        // 부위 리그. 표정이 바뀔 때마다 부수고 다시 만들면 Destroy 가 프레임
        // 끝까지 지연돼서 옛 부위와 새 부위가 한 프레임 겹칩니다. 그래서 껍데기는
        // 한 번만 만들고 스프라이트와 위치만 갈아 끼웁니다.
        RectTransform _parts;
        Image _pExtra, _pBrows, _pEyes, _pPupils, _pMouth;
        RectTransform _pEyeGroup;

        // 1920x1080 기준. 세로 720이면 화면의 약 2/3 — 비주얼 노벨의 통상 비율입니다.
        // 상단바(72)에 머리가 닿지 않고, 아래는 대화창(340)에 살짝 가려집니다.
        const float Width = 420f;
        const float Height = 720f;       // 700:1200 비율 유지
        const float BottomOffset = 258f;
        const float AnchorX = 0.78f;

        PortraitView() { }

        /// <summary>프리팹이든 코드 생성이든 상관없이 붙습니다. 없으면 만들고, 있으면 그걸 씁니다.</summary>
        public static PortraitView CreateOrBind(RectTransform portraitLayer)
        {
            if (portraitLayer == null) return null;

            var v = new PortraitView { Root = portraitLayer };

            v._body = portraitLayer.Find("Body")?.GetComponent<Image>();
            v._face = portraitLayer.Find("Face")?.GetComponent<Image>();

            if (v._body == null) v._body = MakeSlot("Body", portraitLayer);
            if (v._face == null) v._face = MakeSlot("Face", portraitLayer);

            v._manifest = FacePartManifest.Load();

            // 애니메이터는 포트레이트 레이어에 얹습니다. 프리팹에서 이미 붙어
            // 온 경우가 있으니 있으면 그걸 씁니다.
            v._anim = portraitLayer.GetComponent<PortraitAnimator>();
            if (v._anim == null) v._anim = portraitLayer.gameObject.AddComponent<PortraitAnimator>();
            v._anim.Bind(v._body.rectTransform, v._face.rectTransform, v.PartScale);

            v.Hide();
            return v;
        }

        float CanvasW => _manifest != null ? _manifest.canvasW : 700f;
        float CanvasH => _manifest != null ? _manifest.canvasH : 1200f;
        float PartScale => Width / CanvasW;

        static Image MakeSlot(string name, RectTransform parent)
        {
            var img = UIFactory.Box(name, parent, new Color(1, 1, 1, 1));
            img.raycastTarget = false;
            img.preserveAspect = true;
            img.sprite = null;

            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(AnchorX, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(Width, Height);
            rt.anchoredPosition = new Vector2(0f, BottomOffset);
            return img;
        }

        // ── 동작 ──────────────────────────────────────────────────────────────

        public void Show(string portraitKey, string faceKey)
        {
            if (string.IsNullOrEmpty(portraitKey)) { Hide(); return; }

            bool characterChanged = portraitKey != _characterKey;
            if (characterChanged)
            {
                var sprite = ResolveBody(portraitKey, out string artKey, out bool painted);
                if (sprite == null) { Hide(); return; }

                _body.sprite = sprite;
                _painted = painted;
                _characterKey = portraitKey;
                _artKey = artKey;
                _faceKey = null;          // 인물이 바뀌었으니 표정도 다시 깝니다
                _pose = PoseFront;        // 방향도 정면부터
                if (_paintedMouth != null) _paintedMouth.enabled = false;
            }

            _body.color = Color.white;
            _body.gameObject.SetActive(true);
            _anim.SetActive(true);

            if (string.IsNullOrEmpty(faceKey)) faceKey = "normal";

            // 인물이 막 등장한 참이면 눈을 감았다 뜰 이유가 없습니다. 바로 짓고
            // 반응 모션만 실어 줍니다.
            if (characterChanged)
            {
                BuildFace(faceKey);
                _anim.PlayReaction(faceKey);
                return;
            }

            SetFace(faceKey);
        }

        /// <summary>
        /// 포트레이트키에 맞는 정면 그림. 자기 손그림 → 빌려 쓰는 손그림 → 도형 몸 순서로 찾습니다.
        /// 방 안에 세워 두는 인물(RoomController)도 같은 규칙으로 찾아야 대화창의 초상화와
        /// 다른 사람이 서 있는 일이 없습니다.
        /// </summary>
        public static Sprite ResolveBody(string portraitKey, out string artKey, out bool painted)
        {
            artKey = portraitKey;
            painted = false;
            if (string.IsNullOrEmpty(portraitKey)) return null;

            var sprite = Resources.Load<Sprite>($"{PaintedFolder}/{artKey}");
            if (sprite == null && !string.IsNullOrEmpty(FallbackPaintedKey))
            {
                artKey = FallbackPaintedKey;
                sprite = Resources.Load<Sprite>($"{PaintedFolder}/{artKey}");
            }
            painted = sprite != null;
            if (!painted)
            {
                artKey = portraitKey;
                sprite = Resources.Load<Sprite>($"{PlaceholderFolder}/{artKey}");
            }
            return sprite;
        }

        public void SetFace(string faceKey)
        {
            if (_body == null || !_body.gameObject.activeSelf) return;

            if (string.IsNullOrEmpty(faceKey)) faceKey = "normal";
            if (faceKey == _faceKey) { RestoreFaceVisibility(); return; }

            // 눈을 감은 순간에 갈아 끼웁니다 — 바뀌는 장면을 보여주지 않는 쪽이
            // 크로스페이드보다 깔끔합니다. 선화를 반투명하게 겹치면 탁해집니다.
            string target = faceKey;
            _anim.SwapFace(() => BuildFace(target), target);
        }

        /// <summary>
        /// 몸 방향을 바꿉니다. front / side_left / side_right / back_left / back_right / back.
        /// 이름은 정해져 있지 않고 파일이 있으면 됩니다 — {키}_{방향}.png.
        /// 'sorrow_side_left' 처럼 포트레이트키를 붙여 써도 알아듣습니다.
        /// 표정은 그대로 둡니다 — 돌아섰다가 다시 봐도 같은 얼굴입니다.
        /// </summary>
        public void SetPose(string pose)
        {
            if (string.IsNullOrEmpty(_characterKey) || _body == null) return;

            pose = NormalizePose(pose);
            if (pose == _pose) return;

            Sprite sprite = null;
            if (pose != PoseFront)
            {
                sprite = LoadPose(pose);
                if (sprite == null && PoseFallbacks.TryGetValue(pose, out var alternatives))
                {
                    foreach (var alt in alternatives)
                        if ((sprite = LoadPose(alt)) != null) { pose = alt; break; }
                }
                // 대사마다 찍히면 콘솔이 묻히니 그림 하나당 한 번만 알립니다.
                if (sprite == null && WarnedPoses.Add($"{_artKey}_{pose}"))
                    Debug.LogWarning($"[Portrait] {BodyFolder}/{_artKey}_{pose} 이 없어 정면으로 둡니다.");
            }
            if (sprite == null)
            {
                pose = PoseFront;
                sprite = Resources.Load<Sprite>($"{BodyFolder}/{_artKey}");
            }

            _pose = pose;
            if (sprite != null) _body.sprite = sprite;

            if (_painted) ApplyPaintedRig();   // 입 덮개도 방향마다 따로입니다

            if (FaceHidden) { if (_face != null) _face.gameObject.SetActive(false); }
            else RestoreFaceVisibility();
        }

        Sprite LoadPose(string pose) => Resources.Load<Sprite>($"{BodyFolder}/{_artKey}_{pose}");

        /// <summary>
        /// 그림이 없는 방향은 가장 비슷한 방향으로 대신합니다. 뒷모습이 없는 인물이 [back] 을
        /// 받으면, 정면으로 돌아오는 것보다 가장 많이 돌아선 그림이라도 보여 주는 쪽이 연출에 가깝습니다.
        /// </summary>
        static readonly Dictionary<string, string[]> PoseFallbacks = new Dictionary<string, string[]>
        {
            { "back",       new[] { "back_left", "back_right", "side_left", "side_right" } },
            { "back_left",  new[] { "side_left", "back" } },
            { "back_right", new[] { "side_right", "back" } },
            { "side_left",  new[] { "back_left" } },
            { "side_right", new[] { "back_right" } },
        };

        static readonly HashSet<string> WarnedPoses = new HashSet<string>();

        string NormalizePose(string pose)
        {
            if (string.IsNullOrEmpty(pose)) return PoseFront;

            pose = pose.Trim().ToLowerInvariant();
            foreach (var key in new[] { _characterKey, _artKey })
            {
                string prefix = key.ToLowerInvariant() + "_";
                if (pose.StartsWith(prefix)) { pose = pose.Substring(prefix.Length); break; }
            }
            return pose.Length == 0 ? PoseFront : pose;
        }

        /// <summary>대사가 찍히는 동안 입이 움직이게 합니다.</summary>
        public void SetSpeaking(bool speaking)
        {
            if (_anim != null) _anim.SetSpeaking(speaking);
        }

        public void Hide()
        {
            if (_anim != null) _anim.SetActive(false);
            if (_body != null) _body.gameObject.SetActive(false);
            if (_face != null) _face.gameObject.SetActive(false);
        }

        /// <summary>Hide 뒤에 같은 표정으로 다시 나올 때 꺼 둔 걸 되살립니다.</summary>
        void RestoreFaceVisibility()
        {
            if (_faceReady && _face != null) _face.gameObject.SetActive(!FaceHidden);
        }

        // ── 표정 짓기 ─────────────────────────────────────────────────────────

        void BuildFace(string faceKey)
        {
            _faceKey = faceKey;
            if (_painted) { ApplyPaintedRig(); return; }
            if (!BuildRiggedFace(faceKey)) BuildFlatFace(faceKey);
        }

        /// <summary>
        /// 손그림은 얼굴 부위가 따로 없으니 입 덮개 하나만 리그로 넘깁니다.
        /// 다문 입은 그림에 이미 있어서, 말하는 동안 벌린 입({그림}_mouth.png)을
        /// 켰다 껐다 하면 됩니다. 덮개는 그림과 같은 캔버스라 몸에 그대로 겹칩니다.
        /// 덮개가 없는 방향(뒷모습 등)에서는 입이 안 움직입니다.
        /// </summary>
        void ApplyPaintedRig()
        {
            string key = _pose == PoseFront ? _artKey : $"{_artKey}_{_pose}";
            var open = Resources.Load<Sprite>($"{PaintedFolder}/{key}_mouth");
            if (open == null)
            {
                if (_paintedMouth != null) _paintedMouth.enabled = false;
                _anim.ClearRig();
                return;
            }

            if (_paintedMouth == null)
            {
                // 몸의 자식으로 둬야 호흡·흔들림을 몸과 똑같이 따라갑니다.
                _paintedMouth = UIFactory.Box("PaintedMouth", _body.rectTransform, Color.white);
                UIFactory.Anchor(_paintedMouth.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                _paintedMouth.preserveAspect = true;
                _paintedMouth.raycastTarget = false;
            }

            _paintedMouth.sprite = open;
            _paintedMouth.enabled = false;
            _paintedMouth.gameObject.SetActive(true);

            _anim.SetRig(new PortraitAnimator.FaceRig
            {
                Mouth = _paintedMouth,
                MouthOpen = open,     // MouthClosed 가 없으면 애니메이터가 켜고 끄기만 합니다
                CanBlink = false,
            });
        }

        /// <summary>부위 PNG + 배치표가 다 있을 때. 성공하면 true.</summary>
        bool BuildRiggedFace(string faceKey)
        {
            var entry = _manifest?.Find(faceKey);
            if (entry == null) return false;

            // 최소한 눈과 입은 있어야 리그가 성립합니다.
            var eyesRect = entry.Find("eyes");
            var mouthRect = entry.Find("mouth");
            if (eyesRect == null || mouthRect == null) return false;

            var eyesSprite = Load(faceKey, "eyes");
            var mouthSprite = Load(faceKey, "mouth");
            if (eyesSprite == null || mouthSprite == null) return false;

            EnsureRig();

            ApplyPart(_pExtra, entry.Find("extra"), faceKey);
            ApplyPart(_pBrows, entry.Find("brows"), faceKey);

            // 눈은 통을 하나 씌워서 통째로 눌러 깜빡입니다. 동공이 그 안에
            // 들어가 있어야 감았을 때 같이 사라집니다.
            Place(_pEyeGroup, eyesRect);
            _pEyes.sprite = eyesSprite;
            _pEyes.enabled = true;

            var rig = new PortraitAnimator.FaceRig
            {
                CanBlink = entry.canBlink,
                EyeGroup = _pEyeGroup,
                Mouth = _pMouth,
            };

            var pupilRect = entry.Find("pupils");
            var pupilSprite = pupilRect != null ? Load(faceKey, "pupils") : null;
            if (pupilSprite != null)
            {
                _pPupils.sprite = pupilSprite;
                _pPupils.enabled = true;
                _pPupils.gameObject.SetActive(true);
                var rt = _pPupils.rectTransform;
                rt.sizeDelta = new Vector2(pupilRect.w, pupilRect.h) * PartScale;
                // EyeGroup 중심 기준으로 다시 잡습니다.
                rt.anchoredPosition = LocalCenter(pupilRect) - LocalCenter(eyesRect);
                rig.Pupils = rt;
            }
            else
            {
                _pPupils.gameObject.SetActive(false);
            }

            _pMouth.sprite = mouthSprite;
            _pMouth.enabled = true;
            _pMouth.gameObject.SetActive(true);
            Place(_pMouth.rectTransform, mouthRect);

            rig.MouthClosed = mouthSprite;
            rig.MouthClosedPos = _pMouth.rectTransform.anchoredPosition;
            rig.MouthClosedSize = _pMouth.rectTransform.sizeDelta;

            var openRect = entry.Find("mouth_open");
            if (openRect != null)
            {
                rig.MouthOpen = Load(faceKey, "mouth_open");
                rig.MouthOpenPos = LocalCenter(openRect);
                rig.MouthOpenSize = new Vector2(openRect.w, openRect.h) * PartScale;
            }

            // 합본이 뒤에 비쳐 보이면 안 되므로 이미지 자체를 끕니다.
            // 오브젝트는 켜 둡니다 — 부위들의 부모라서요.
            _face.sprite = null;
            _face.enabled = false;
            _face.gameObject.SetActive(!FaceHidden);
            _parts.gameObject.SetActive(true);

            _anim.SetRig(rig);
            _faceReady = true;
            return true;
        }

        /// <summary>부위가 없을 때. 예전처럼 한 장으로 갈아 끼웁니다.</summary>
        void BuildFlatFace(string faceKey)
        {
            if (_parts != null) _parts.gameObject.SetActive(false);
            _anim.ClearRig();

            var sprite = Resources.Load<Sprite>($"Art/Faces/{faceKey}");
            if (sprite == null && faceKey != "normal")
                sprite = Resources.Load<Sprite>("Art/Faces/normal");

            _face.sprite = sprite;
            _face.enabled = sprite != null;
            _face.color = Color.white;
            _face.gameObject.SetActive(sprite != null && !FaceHidden);
            _faceReady = sprite != null;
        }

        void EnsureRig()
        {
            if (_parts != null) return;

            _parts = UIFactory.Rect("Parts", _face.rectTransform);
            UIFactory.Anchor(_parts, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // 뒤에서 앞 순서로 쌓습니다: 기타(코·눈물) → 눈썹 → 눈 → 입
            _pExtra = MakePart("Extra", _parts);
            _pBrows = MakePart("Brows", _parts);

            _pEyeGroup = UIFactory.Rect("EyeGroup", _parts);
            _pEyes = MakePart("Eyes", _pEyeGroup);
            UIFactory.Anchor(_pEyes.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _pPupils = MakePart("Pupils", _pEyeGroup);
            _pPupils.rectTransform.anchorMin = _pPupils.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);

            _pMouth = MakePart("Mouth", _parts);
        }

        static Image MakePart(string name, RectTransform parent)
        {
            var img = UIFactory.Box(name, parent, Color.white);
            img.raycastTarget = false;
            img.preserveAspect = false;   // 크기를 배치표로 정확히 주므로 손대면 안 됩니다
            img.sprite = null;
            img.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            return img;
        }

        Sprite Load(string faceKey, string part) => Resources.Load<Sprite>($"Art/Faces/{faceKey}/{part}");

        /// <summary>표정에 그 부위가 없으면(예: normal 의 눈물) 그냥 끕니다.</summary>
        void ApplyPart(Image img, FacePartRect r, string faceKey)
        {
            var sprite = r != null ? Load(faceKey, r.name) : null;
            if (sprite == null) { img.gameObject.SetActive(false); return; }

            img.sprite = sprite;
            img.enabled = true;
            img.gameObject.SetActive(true);
            Place(img.rectTransform, r);
        }

        // ── 부위 배치 ─────────────────────────────────────────────────────────
        //
        // 부위 PNG 는 내용 경계로 잘려 있어서 그냥 겹치면 위치가 안 맞습니다.
        // faceparts.json 이 원본 700x1200 캔버스에서의 사각형(y 는 위에서부터)을
        // 들고 있으니, 그걸 포트레이트 좌표(아래가 0)로 옮겨 놓습니다.

        Vector2 LocalCenter(FacePartRect r)
        {
            float cx = r.x + r.w * 0.5f;
            float cyFromBottom = CanvasH - (r.y + r.h * 0.5f);
            return new Vector2((cx - CanvasW * 0.5f) * PartScale, cyFromBottom * PartScale);
        }

        void Place(RectTransform rt, FacePartRect r)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(r.w, r.h) * PartScale;
            rt.anchoredPosition = LocalCenter(r);
        }
    }
}
