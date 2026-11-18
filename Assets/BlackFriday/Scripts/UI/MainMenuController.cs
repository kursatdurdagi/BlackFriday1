using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.UIElements;

namespace BlackFriday
{
    /// <summary>Ana menü (UI Toolkit).</summary>
    [RequireComponent(typeof(UIDocument))]
    public class MainMenuController : MonoBehaviour
    {
        public CharacterPreview preview;

        VisualElement mainPanel, playPanel, characterPanel, settingsPanel, connectingPanel, titleBlock;
        Label statusLabel, charName, connectingLabel;
        TextField ipField, nameField;
        VisualElement levelList, modeList;
        float connectTimer;

        void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            mainPanel = root.Q("main-panel");
            titleBlock = root.Q("title-block");
            playPanel = root.Q("play-panel");
            characterPanel = root.Q("character-panel");
            settingsPanel = root.Q("settings-panel");
            connectingPanel = root.Q("connecting-panel");
            statusLabel = root.Q<Label>("status-label");
            charName = root.Q<Label>("char-name");
            connectingLabel = root.Q<Label>("connecting-label");
            ipField = root.Q<TextField>("ip-field");
            nameField = root.Q<TextField>("name-field");
            levelList = root.Q("level-list");
            modeList = root.Q("mode-list");

            Bind(root, "btn-play", () => Show(playPanel));
            Bind(root, "btn-character", () => Show(characterPanel));
            Bind(root, "btn-settings", () => Show(settingsPanel));
            Bind(root, "btn-quit", Quit);
            Bind(root, "btn-play-back", () => Show(mainPanel));
            Bind(root, "btn-char-back", () => { GameSettings.PlayerName = nameField.value; Show(mainPanel); });
            Bind(root, "btn-settings-back", () => Show(mainPanel));
            Bind(root, "btn-prev", () => ChangeSkin(-1));
            Bind(root, "btn-next", () => ChangeSkin(1));
            Bind(root, "btn-host", () => Host(false));
            Bind(root, "btn-solo", () => Host(true));
            Bind(root, "btn-join", Join);
            Bind(root, "btn-cancel", () => { BFNetworkManager.Instance?.Leave(); Show(playPanel); });

            ipField.value = GameSettings.LastIP;
            nameField.value = GameSettings.PlayerName;
            nameField.RegisterValueChangedCallback(e => GameSettings.PlayerName = e.newValue);

            var vol = root.Q<Slider>("vol-slider");
            vol.value = GameSettings.MasterVolume;
            vol.RegisterValueChangedCallback(e => GameSettings.MasterVolume = e.newValue);
            var sens = root.Q<Slider>("sens-slider");
            sens.value = GameSettings.MouseSensitivity;
            sens.RegisterValueChangedCallback(e => GameSettings.MouseSensitivity = e.newValue);
            var invy = root.Q<Toggle>("invy-toggle");
            invy.value = GameSettings.InvertY;
            invy.RegisterValueChangedCallback(e => GameSettings.InvertY = e.newValue);
            var fpsT = root.Q<Toggle>("fps-toggle");
            fpsT.value = GameSettings.FirstPerson;
            fpsT.RegisterValueChangedCallback(e => GameSettings.FirstPerson = e.newValue);
            var vs = root.Q<Toggle>("vsync-toggle");
            vs.value = GameSettings.VSync;
            vs.RegisterValueChangedCallback(e => GameSettings.VSync = e.newValue);
            var sf = root.Q<Toggle>("showfps-toggle");
            sf.value = GameSettings.ShowFps;
            sf.RegisterValueChangedCallback(e => GameSettings.ShowFps = e.newValue);
            var fs = root.Q<Toggle>("fs-toggle");
            fs.value = GameSettings.Fullscreen;
            fs.RegisterValueChangedCallback(e => GameSettings.Fullscreen = e.newValue);
            var q = root.Q<DropdownField>("quality-dd");
            q.choices = new List<string>(QualitySettings.names);
            q.index = Mathf.Clamp(QualitySettings.GetQualityLevel(), 0, QualitySettings.names.Length - 1);
            q.RegisterValueChangedCallback(e => GameSettings.Quality = q.index);

            BuildCards();
            UpdateCharacter();
            Show(mainPanel);

            statusLabel.text = BFNetworkManager.LastStatus ?? "";
            BFInput.Blocked = false;
            UnityEngine.Cursor.visible = true;
            UnityEngine.Cursor.lockState = CursorLockMode.None;
        }

        void Bind(VisualElement root, string name, System.Action a)
        {
            var b = root.Q<Button>(name);
            if (b == null) { Debug.LogWarning("[BF] buton yok: " + name); return; }
            b.clicked += () => { Sfx.Play(SfxId.Click, 0.4f); a(); };
        }

        void Show(VisualElement panel)
        {
            foreach (var p in new[] { mainPanel, playPanel, characterPanel, settingsPanel, connectingPanel })
                p.EnableInClassList("hidden", p != panel);
            titleBlock?.EnableInClassList("hidden", panel != mainPanel && panel != characterPanel);
        }

        void BuildCards()
        {
            levelList.Clear();
            for (int i = 0; i < GameCatalog.Levels.Count; i++)
            {
                var info = GameCatalog.Levels[i];
                int idx = i;
                levelList.Add(MakeCard(info.displayName, info.description, info.available, GameCatalog.SelectedLevel == i,
                    () => { GameCatalog.SelectedLevel = idx; BuildCards(); }, IconKind.Home));
            }
            modeList.Clear();
            for (int i = 0; i < GameCatalog.Modes.Count; i++)
            {
                var info = GameCatalog.Modes[i];
                int idx = i;
                modeList.Add(MakeCard(info.displayName, info.description, info.available, GameCatalog.SelectedMode == i,
                    () => { GameCatalog.SelectedMode = idx; BuildCards(); }, IconKind.Receipt));
            }
        }

        VisualElement MakeCard(string title, string desc, bool available, bool selected, System.Action onClick, IconKind icon)
        {
            var card = new VisualElement();
            card.AddToClassList("card");
            if (selected && available) card.AddToClassList("card--selected");
            if (!available) card.AddToClassList("card--locked");
            card.Add(new BFIcon(available ? icon : IconKind.Lock));
            var col = new VisualElement(); col.style.flexGrow = 1; col.style.flexShrink = 1;
            var t = new Label(title); t.AddToClassList("card-title");
            var d = new Label(available ? desc : "Yakında"); d.AddToClassList("card-desc");
            col.Add(t); col.Add(d); card.Add(col);
            if (selected && available) { var ck = new BFIcon(IconKind.Check); ck.style.marginRight = 0; card.Add(ck); }
            if (available) card.RegisterCallback<ClickEvent>(_ => { Sfx.Play(SfxId.Click, 0.4f); onClick(); });
            return card;
        }

        void ChangeSkin(int dir)
        {
            var lib = CharacterLibrary.Instance;
            int count = lib != null ? Mathf.Max(1, lib.playerModels.Count) : 1;
            GameSettings.SkinIndex = ((GameSettings.SkinIndex + dir) % count + count) % count;
            UpdateCharacter();
        }

        void UpdateCharacter()
        {
            var lib = CharacterLibrary.Instance;
            int idx = GameSettings.SkinIndex;
            charName.text = lib != null ? lib.PlayerModelName(idx) : "Karakter";
            if (preview != null) preview.Show(idx);
        }

        bool CheckSelection()
        {
            if (!GameCatalog.Levels[GameCatalog.SelectedLevel].available || !GameCatalog.Modes[GameCatalog.SelectedMode].available)
            {
                statusLabel.text = "Bu bölüm/mod henüz hazır değil.";
                return false;
            }
            return true;
        }

        void Host(bool solo)
        {
            if (!CheckSelection()) return;
            var nm = BFNetworkManager.Instance;
            if (nm == null) { statusLabel.text = "NetworkManager bulunamadı!"; return; }
            GameSettings.PlayerName = nameField.value;
            statusLabel.text = "";
            nm.HostGame(solo);
        }

        void Join()
        {
            var nm = BFNetworkManager.Instance;
            if (nm == null) { statusLabel.text = "NetworkManager bulunamadı!"; return; }
            GameSettings.LastIP = ipField.value;
            GameSettings.PlayerName = nameField.value;
            statusLabel.text = "";
            connectingLabel.text = "Bağlanıyor: " + ipField.value;
            connectTimer = 0;
            Show(connectingPanel);
            nm.JoinGame(ipField.value);
        }

        void Update()
        {
            if (connectingPanel != null && !connectingPanel.ClassListContains("hidden"))
            {
                connectTimer += Time.deltaTime;
                if (!NetworkClient.active)
                {
                    statusLabel.text = BFNetworkManager.LastStatus ?? "Bağlanılamadı.";
                    Show(playPanel);
                }
                else if (connectTimer > 12f)
                {
                    BFNetworkManager.Instance?.Leave();
                    statusLabel.text = "Zaman aşımı: sunucu bulunamadı.";
                    Show(playPanel);
                }
            }
        }

        void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
