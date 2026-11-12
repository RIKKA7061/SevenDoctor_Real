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

        Text _title, _tagline, _continueLabel, _startLabel, _languageLabel, _quitLabel;
        GameObject _continueButton;

        // 세이브가 있을 때만 '이어 하기' 가 보이고, 시작 버튼은 '새 게임' 이 됩니다.
        bool _hasSave;
        Func<string> _continueDetail;

        // '새 게임' 을 한 번 눌러 둔 상태. 세이브는 하나뿐이라 새로 시작하면 지워지므로
        // 두 번 눌러야 시작합니다. 확인 창을 따로 띄우지 않고 버튼 글자를 바꿔서 묻습니다.
        bool _newGameArmed;

        /// <summary>새 게임을 시작하라고 했을 때 (세이브가 있었다면 한 번 더 확인을 받은 뒤).</summary>
        public event Action StartRequested;

        /// <summary>'이어 하기' 를 눌렀을 때.</summary>
        public event Action ContinueRequested;

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
                             new Vector2(-220, -358), new Vector2(220, -30));
            UIFactory.VLayout(menu, 16, new RectOffset(0, 0, 0, 0), TextAnchor.UpperCenter);

            // 캡션을 비워서 만들면 UIFactory.Btn 이 글자 자식을 아예 안 답니다.
            // 그러면 잡을 Text 가 없으니, 처음부터 지금 언어의 문구로 만듭니다.

            // 이어 하기는 맨 위에 둡니다 — 세이브가 있는 사람이 가장 자주 누를 버튼입니다.
            // 세이브가 없으면 꺼 두고, 꺼진 버튼은 세로 배치에서 자리도 차지하지 않습니다.
            var resume = UIFactory.Btn("Continue", menu, Loc.T("ui.title.continue"), 30,
                                       new Color(0.20f, 0.17f, 0.10f), UIFactory.Accent);
            UIFactory.Height(resume.gameObject, 72);
            v._continueLabel = resume.GetComponentInChildren<Text>();
            v._continueButton = resume.gameObject;
            resume.onClick.AddListener(() => v.ContinueRequested?.Invoke());
            resume.gameObject.SetActive(false);

            var start = UIFactory.Btn("Start", menu, Loc.T("ui.title.start"), 30,
                                      new Color(0.20f, 0.17f, 0.10f), UIFactory.Accent);
            UIFactory.Height(start.gameObject, 72);
            v._startLabel = start.GetComponentInChildren<Text>();
            start.onClick.AddListener(v.OnStartClicked);

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

            if (_startLabel != null)    _startLabel.text    = StartCaption();
            if (_quitLabel != null)     _quitLabel.text     = Loc.T("ui.title.quit");
            if (_languageLabel != null) _languageLabel.text = LanguageCaption();

            if (_continueButton != null) _continueButton.SetActive(_hasSave);
            if (_continueLabel != null)  _continueLabel.text = ContinueCaption();
        }

        /// <summary>
        /// 세이브가 있는지 알려 줍니다. 있으면 '이어 하기' 가 나오고 시작 버튼이 '새 게임' 으로 바뀝니다.
        /// detail 은 버튼에 덧붙일 말(이어질 방 이름). 언어가 바뀌면 다시 물어보므로 함수로 받습니다.
        /// </summary>
        public void SetContinue(bool available, Func<string> detail = null)
        {
            _hasSave = available;
            _continueDetail = available ? detail : null;
            _newGameArmed = false;
            Refresh();
        }

        string ContinueCaption()
        {
            string detail = _continueDetail != null ? _continueDetail() : null;
            return string.IsNullOrEmpty(detail)
                ? Loc.T("ui.title.continue")
                : $"{Loc.T("ui.title.continue")}  ·  {detail}";
        }

        string StartCaption()
        {
            if (!_hasSave) return Loc.T("ui.title.start");
            return Loc.T(_newGameArmed ? "ui.title.new_game_confirm" : "ui.title.new_game");
        }

        void OnStartClicked()
        {
            // 세이브가 있으면 첫 번째 클릭은 경고만 띄웁니다. 잘못 눌러서 진행을 날리는 일을 막습니다.
            if (_hasSave && !_newGameArmed)
            {
                _newGameArmed = true;
                Refresh();
                return;
            }
            StartRequested?.Invoke();
        }

        public void Show()
        {
            if (Root == null) return;
            _newGameArmed = false;
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
