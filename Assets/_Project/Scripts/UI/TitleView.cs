using System;
using SevenDoctors.Core;
using UnityEngine;
using UnityEngine.UI;

namespace SevenDoctors.UI
{
    /// <summary>
    /// 시작 화면. 게임에 들어가기 전에 잠깐 서 있는 자리입니다.
    ///
    /// 언어를 여기 둔 건 순서 때문입니다 — 대사가 한 줄이라도 나온 뒤에 언어를
    /// 바꾸면 이미 읽은 부분만 다른 언어로 남습니다. 시작 전에 고르게 하면
    /// 그럴 일이 없습니다. (게임 중에도 바꿀 수는 있고, 그때는 화면을 다시 그립니다)
    ///
    /// 언어 버튼에는 두 언어 이름을 늘 같이 적습니다. 지금 언어로만 적어 두면
    /// 영어를 못 읽는 사람이 영어로 바꿨을 때 되돌아올 방법이 사라집니다.
    /// </summary>
    public class TitleView
    {
        public RectTransform Root { get; private set; }

        Text _title, _tagline, _startLabel, _languageLabel, _quitLabel;

        /// <summary>시작 버튼을 눌렀을 때.</summary>
        public event Action StartRequested;

        public bool IsVisible => Root != null && Root.gameObject.activeSelf;

        TitleView() { }

        public static TitleView Create(RectTransform parent)
        {
            var v = new TitleView();

            v.Root = UIFactory.Rect("TitleScreen", parent);
            UIFactory.Anchor(v.Root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var bg = UIFactory.Box("Bg", v.Root, new Color(0.04f, 0.05f, 0.07f, 1f));
            bg.raycastTarget = true;   // 뒤쪽이 눌리지 않게

            v._title = UIFactory.Label("Title", v.Root, "", 92, TextAnchor.MiddleCenter, UIFactory.Accent);
            UIFactory.Anchor(v._title.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                             new Vector2(0, -320), new Vector2(0, -180));

            v._tagline = UIFactory.Label("Tagline", v.Root, "", 26, TextAnchor.MiddleCenter, UIFactory.InkDim);
            UIFactory.Anchor(v._tagline.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                             new Vector2(0, -376), new Vector2(0, -322));

            var menu = UIFactory.Rect("Menu", v.Root);
            UIFactory.Anchor(menu, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                             new Vector2(-220, -270), new Vector2(220, -30));
            UIFactory.VLayout(menu, 16, new RectOffset(0, 0, 0, 0), TextAnchor.UpperCenter);

            // 캡션을 비워서 만들면 UIFactory.Btn 이 글자 자식을 아예 안 답니다.
            // 그러면 잡을 Text 가 없으니, 처음부터 지금 언어의 문구로 만듭니다.
            var start = UIFactory.Btn("Start", menu, Loc.T("ui.title.start"), 30,
                                      new Color(0.20f, 0.17f, 0.10f), UIFactory.Accent);
            UIFactory.Height(start.gameObject, 72);
            v._startLabel = start.GetComponentInChildren<Text>();
            start.onClick.AddListener(() => v.StartRequested?.Invoke());

            var language = UIFactory.Btn("Language", menu, v.LanguageCaption(), 26,
                                         new Color(0.13f, 0.14f, 0.19f), UIFactory.Ink);
            UIFactory.Height(language.gameObject, 64);
            v._languageLabel = language.GetComponentInChildren<Text>();
            language.onClick.AddListener(() => Loc.Toggle());

            var quit = UIFactory.Btn("Quit", menu, Loc.T("ui.title.quit"), 24,
                                     new Color(0.13f, 0.14f, 0.19f), UIFactory.InkDim);
            UIFactory.Height(quit.gameObject, 60);
            v._quitLabel = quit.GetComponentInChildren<Text>();
            quit.onClick.AddListener(Quit);

            v.Refresh();
            return v;
        }

        /// <summary>
        /// 두 언어를 늘 같이 보여주고, 지금 언어에 표시를 답니다.
        /// 지금 언어로만 적어 두면 영어를 못 읽는 사람이 되돌아올 길이 없어집니다.
        /// </summary>
        string LanguageCaption()
        {
            string ko = Loc.NameOf(Language.Korean);
            string en = Loc.NameOf(Language.English);
            return Loc.IsEnglish
                ? $"{Loc.T("ui.title.language")} :  {ko}  /  <b>{en}</b>"
                : $"{Loc.T("ui.title.language")} :  <b>{ko}</b>  /  {en}";
        }

        /// <summary>언어가 바뀌면 다시 불립니다.</summary>
        public void Refresh()
        {
            if (_title != null)   _title.text   = Loc.T("ui.title.name");
            if (_tagline != null) _tagline.text = Loc.T("ui.title.tagline");

            if (_startLabel != null)    _startLabel.text    = Loc.T("ui.title.start");
            if (_quitLabel != null)     _quitLabel.text     = Loc.T("ui.title.quit");
            if (_languageLabel != null) _languageLabel.text = LanguageCaption();
        }

        public void Show()
        {
            if (Root == null) return;
            Root.gameObject.SetActive(true);
            Root.SetAsLastSibling();
            Refresh();
        }

        public void Hide()
        {
            if (Root != null) Root.gameObject.SetActive(false);
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
