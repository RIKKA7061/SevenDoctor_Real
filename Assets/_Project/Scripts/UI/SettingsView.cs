using SevenDoctors.Core;
using UnityEngine;
using UnityEngine.UI;

namespace SevenDoctors.UI
{
    /// <summary>
    /// 설정. 시작 화면의 '설정' 과 게임 중 메뉴의 '설정' 이 같은 창을 띄웁니다.
    ///
    ///   언어           한국어 / English. 두 언어 이름을 늘 같이 적습니다 (TitleView 와 같은 이유)
    ///   세이브 초기화  이어하기 · 엔딩 분기점 · 본 엔딩 기록을 전부 지웁니다. 언어 설정은 남깁니다.
    ///                  잘못 누르지 않게 한 번 더 눌러야 지워집니다.
    ///                  게임 도중이면 지운 뒤 시작 화면으로 돌아갑니다 — 그대로 두면 종료할 때 자동
    ///                  저장이 다시 만들어 버려서, 지웠는데 왜 남아 있냐는 일이 생깁니다.
    /// </summary>
    public class SettingsView
    {
        const float ConfirmSeconds = 5f;   // 첫 번째 누름 뒤 이 안에 다시 눌러야 지웁니다

        public RectTransform Root { get; private set; }
        public bool IsVisible => Root != null && Root.gameObject.activeSelf;

        Text _title, _language, _reset, _resetNote, _close;
        float _confirmUntil = -1f;

        SettingsView() { }

        public static SettingsView Create(RectTransform screen)
        {
            // 예전에 구운 UI 프리팹에 같은 이름이 남아 있으면 치웁니다 — 겹쳐서 두 개가 뜨지 않게.
            var old = screen.Find("Settings");
            if (old != null) Object.Destroy(old.gameObject);

            var v = new SettingsView { Root = UIFactory.Rect("Settings", screen) };

            var scrim = UIFactory.Btn("Scrim", v.Root, string.Empty, 1, UIFactory.Scrim, UIFactory.Ink);
            UIFactory.Anchor(scrim.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var blank = scrim.GetComponentInChildren<Text>();
            if (blank != null) blank.gameObject.SetActive(false);
            scrim.transition = Selectable.Transition.None;
            scrim.onClick.AddListener(v.Hide);

            var panel = UIFactory.Box("Panel", v.Root, UIFactory.PanelSolid);
            UIFactory.Anchor(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                             new Vector2(-380, -260), new Vector2(380, 260));
            panel.raycastTarget = true;

            v._title = UIFactory.Label("Title", panel.transform, "", 36, TextAnchor.UpperCenter, UIFactory.Accent);
            UIFactory.Anchor(v._title.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                             new Vector2(40, -88), new Vector2(-40, -32));

            var list = UIFactory.Rect("List", panel.transform);
            UIFactory.Anchor(list, new Vector2(0, 0), new Vector2(1, 1), new Vector2(60, 40), new Vector2(-60, -112));
            UIFactory.VLayout(list, 14, new RectOffset(0, 0, 0, 0), TextAnchor.UpperCenter);

            var language = UIFactory.Btn("Language", list, "", 26, new Color(0.13f, 0.14f, 0.19f), UIFactory.Ink);
            UIFactory.Height(language.gameObject, 68);
            v._language = UIFactory.EnsureLabel(language, 26, UIFactory.Ink);
            language.onClick.AddListener(() => Loc.Toggle());

            var reset = UIFactory.Btn("ResetSave", list, "", 26, new Color(0.24f, 0.10f, 0.10f), new Color(0.96f, 0.72f, 0.70f));
            UIFactory.Height(reset.gameObject, 68);
            v._reset = UIFactory.EnsureLabel(reset, 26, new Color(0.96f, 0.72f, 0.70f));
            reset.onClick.AddListener(v.OnReset);

            v._resetNote = UIFactory.Label("ResetNote", list, "", 20, TextAnchor.UpperCenter, UIFactory.InkDim);
            UIFactory.Height(v._resetNote.gameObject, 60);

            var close = UIFactory.Btn("Close", list, "", 26, new Color(0.16f, 0.17f, 0.23f), UIFactory.Ink);
            UIFactory.Height(close.gameObject, 60);
            v._close = UIFactory.EnsureLabel(close, 26, UIFactory.Ink);
            close.onClick.AddListener(v.Hide);

            v.Relocalize();
            v.Root.gameObject.SetActive(false);
            return v;
        }

        public void Relocalize()
        {
            if (_title == null) return;
            _title.text = Loc.T("ui.settings.title");
            _close.text = Loc.T("ui.settings.close");

            string ko = Loc.NameOf(Language.Korean), en = Loc.NameOf(Language.English);
            _language.text = Loc.IsEnglish
                ? $"{Loc.T("ui.title.language")} :  {ko}  /  <b>{en}</b>"
                : $"{Loc.T("ui.title.language")} :  <b>{ko}</b>  /  {en}";

            RefreshReset();
        }

        public void Show()
        {
            if (Root == null) return;
            _confirmUntil = -1f;
            Root.gameObject.SetActive(true);
            Root.SetAsLastSibling();
            Relocalize();
        }

        public void Hide()
        {
            _confirmUntil = -1f;
            if (Root != null) Root.gameObject.SetActive(false);
        }

        bool Confirming => Time.unscaledTime < _confirmUntil;
        static bool InGame => Game.UI != null && (Game.UI.Title == null || !Game.UI.Title.IsVisible);

        void RefreshReset()
        {
            bool any = SaveSystem.HasMain || SaveSystem.HasBranch || SaveSystem.EndingsSeenCount > 0;
            _reset.text = Confirming ? Loc.T("ui.settings.reset_confirm") : Loc.T("ui.settings.reset");
            _resetNote.text = !any && !Confirming ? Loc.T("ui.settings.reset_none")
                            : InGame ? Loc.T("ui.settings.reset_note_ingame")
                            : Loc.T("ui.settings.reset_note");
        }

        void OnReset()
        {
            if (!Confirming)
            {
                _confirmUntil = Time.unscaledTime + ConfirmSeconds;
                RefreshReset();
                return;
            }

            _confirmUntil = -1f;
            SaveSystem.DeleteAll();

            if (InGame)
            {
                // 저장하지 않고 시작 화면으로. 지운 세이브가 종료 자동 저장으로 되살아나지 않게 합니다.
                GameBootstrap.Restart();
                return;
            }

            Game.UI.Toast(Loc.T("ui.settings.reset_done"));
            Game.UI.Title?.Refresh();
            RefreshReset();
        }
    }
}
