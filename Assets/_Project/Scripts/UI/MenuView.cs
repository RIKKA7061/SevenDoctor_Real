using SevenDoctors.Core;
using UnityEngine;
using UnityEngine.UI;

namespace SevenDoctors.UI
{
    /// <summary>
    /// 게임 중 메뉴. Esc(다른 창이 안 떠 있을 때) 또는 상단 '메뉴' 버튼.
    ///
    ///   계속하기   닫기
    ///   저장하기   이어하기 칸에 지금을 저장. 탐색 중에만 됩니다 — 대사·퍼즐 도중엔 그 자리를 담을 수 없습니다
    ///   설정       언어 · 세이브 초기화
    ///   타이틀로   자동 저장하고 시작 화면으로
    ///   게임 종료  자동 저장하고 끕니다
    /// </summary>
    public class MenuView
    {
        public RectTransform Root { get; private set; }
        public bool IsVisible => Root != null && Root.gameObject.activeSelf;

        Text _title, _resume, _save, _settings, _toTitle, _quit, _status;
        Button _saveButton;

        MenuView() { }

        public static MenuView Create(RectTransform screen)
        {
            // 예전에 구운 UI 프리팹에 같은 이름이 남아 있으면 치웁니다 — 겹쳐서 두 개가 뜨지 않게.
            var old = screen.Find("Menu");
            if (old != null) Object.Destroy(old.gameObject);

            var v = new MenuView { Root = UIFactory.Rect("Menu", screen) };

            var scrim = UIFactory.Btn("Scrim", v.Root, string.Empty, 1, UIFactory.Scrim, UIFactory.Ink);
            UIFactory.Anchor(scrim.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var blank = scrim.GetComponentInChildren<Text>();
            if (blank != null) blank.gameObject.SetActive(false);
            scrim.transition = Selectable.Transition.None;
            scrim.onClick.AddListener(v.Hide);

            var panel = UIFactory.Box("Panel", v.Root, UIFactory.PanelSolid);
            UIFactory.Anchor(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                             new Vector2(-300, -300), new Vector2(300, 300));
            panel.raycastTarget = true;

            v._title = UIFactory.Label("Title", panel.transform, "", 36, TextAnchor.UpperCenter, UIFactory.Accent);
            UIFactory.Anchor(v._title.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                             new Vector2(40, -88), new Vector2(-40, -32));

            var list = UIFactory.Rect("List", panel.transform);
            UIFactory.Anchor(list, new Vector2(0, 0), new Vector2(1, 1), new Vector2(60, 40), new Vector2(-60, -112));
            UIFactory.VLayout(list, 14, new RectOffset(0, 0, 0, 0), TextAnchor.UpperCenter);

            v._resume = AddButton(list, "Resume", 68, new Color(0.20f, 0.17f, 0.10f), UIFactory.Accent, v.Hide);
            v._save = AddButton(list, "Save", 68, new Color(0.13f, 0.14f, 0.19f), UIFactory.Ink, v.OnSave);
            v._saveButton = v._save.GetComponentInParent<Button>();

            v._status = UIFactory.Label("Status", list, "", 20, TextAnchor.UpperCenter, UIFactory.InkDim);
            UIFactory.Height(v._status.gameObject, 34);

            v._settings = AddButton(list, "Settings", 64, new Color(0.13f, 0.14f, 0.19f), UIFactory.Ink,
                                    () => Game.UI.Settings?.Show());
            v._toTitle = AddButton(list, "ToTitle", 64, new Color(0.13f, 0.14f, 0.19f), UIFactory.Ink,
                                   GameBootstrap.ReturnToTitle);
            v._quit = AddButton(list, "Quit", 60, new Color(0.13f, 0.14f, 0.19f), UIFactory.InkDim,
                                GameBootstrap.QuitGame);

            v.Relocalize();
            v.Root.gameObject.SetActive(false);
            return v;
        }

        static Text AddButton(RectTransform list, string name, float height, Color bg, Color fg, UnityEngine.Events.UnityAction onClick)
        {
            var btn = UIFactory.Btn(name, list, "", 28, bg, fg);
            UIFactory.Height(btn.gameObject, height);
            btn.onClick.AddListener(onClick);
            return UIFactory.EnsureLabel(btn, 28, fg);
        }

        public void Relocalize()
        {
            if (_title == null) return;
            _title.text = Loc.T("ui.menu.title");
            _resume.text = Loc.T("ui.menu.resume");
            _save.text = Loc.T("ui.menu.save");
            _settings.text = Loc.T("ui.menu.settings");
            _toTitle.text = Loc.T("ui.menu.to_title");
            _quit.text = Loc.T("ui.menu.quit");
            RefreshStatus();
        }

        public void Toggle() { if (IsVisible) Hide(); else Show(); }

        public void Show()
        {
            if (Root == null) return;
            Root.gameObject.SetActive(true);
            Root.SetAsLastSibling();
            RefreshStatus();
        }

        public void Hide()
        {
            if (Root != null) Root.gameObject.SetActive(false);
        }

        /// <summary>저장 버튼 아래 한 줄 — 왜 못 하는지, 또는 마지막으로 언제 저장했는지.</summary>
        void RefreshStatus()
        {
            if (_status == null) return;
            bool can = SaveSystem.CanSaveNow;
            if (_saveButton != null) _saveButton.interactable = can;

            if (!can) _status.text = Loc.T("ui.menu.save_blocked");
            else if (SaveSystem.HasMain) _status.text = Loc.T("ui.menu.last_save", SaveSystem.Describe(SaveSystem.LoadMain()));
            else _status.text = string.Empty;
        }

        void OnSave()
        {
            if (SaveSystem.SaveNow()) Game.UI.Toast(Loc.T("ui.save.saved"));
            else Game.UI.Toast(Loc.T("ui.menu.save_blocked"));
            RefreshStatus();
        }
    }
}
