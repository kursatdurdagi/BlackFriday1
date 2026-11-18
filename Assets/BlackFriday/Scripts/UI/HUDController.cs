using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.UIElements;

namespace BlackFriday
{
    /// <summary>
    /// Oyun içi arayüz (UI Toolkit, tamamı vektörel): fiş şeklinde alışveriş listesi, süre halkası,
    /// fiyat etiketi puan, kombo, flaş indirim bandı, akış, tuş ipuçları, kasa işareti, lobi, bitiş fişi, mola.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class HUDController : MonoBehaviour
    {
        VisualElement root, listContainer, feed, nameLayer, staminaFill, prompt, marker, centerWrap, lobbyPlayers, endLines;
        VisualElement lobbyPanel, endPanel, pausePanel, listPanel, flashBanner, combo;
        BFCrosshair crosshair; VisualElement timerRing, timerFill; BFSeg timer; BFStars endStars;
        Label modeName, score, delivered, centerBig, camMode, fpsCounter, markerText, flashText, comboText;
        Label lobbyMode, lobbyDesc, lobbyCount, lobbyIp, lobbyWait, endTitle, endScore, endDetail;
        Button btnStart, btnRestart;

        readonly Dictionary<BFPlayer, Label> nameTags = new Dictionary<BFPlayer, Label>();
        static readonly List<BFPlayer> deadTags = new List<BFPlayer>();
        float fpsAccum, fpsTimer; int fpsFrames;
        ProductItem cachedTarget;
        bool paused;
        string localIp;
        // kare başına string/layout üretmemek için son değerler
        int listHash = -1, lastCountdown = -1, lastTimerSec = -1, lastScore = -1, lastDelivered = -1, lastRequired = -1, lastStamina = -1;
        int lastPromptKey = int.MinValue, lastCombo = -1, lastFlash = -2, lastLobbyHash = -1, lastEndHash = -1, lastMarkerDist = -1, lastCross = -1;
        string lastCamMode; RoundState lastState = (RoundState)255; bool lastLow, lastCenterText;

        static readonly (string key, string text)[] Controls =
        {
            ("WASD", "Hareket"), ("Boşluk", "Zıpla"), ("Shift", "Koş"),
            ("E", "Al / Bırak"), ("Sağ Tık", "Nişan al"), ("Sol Tık", "Fırlat / İt"),
            ("F", "İt"), ("V", "TPS / FPS"), ("Esc", "Mola"),
        };

        void OnEnable()
        {
            root = GetComponent<UIDocument>().rootVisualElement;
            listContainer = root.Q("list-container");
            listPanel = root.Q("list-panel");
            feed = root.Q("feed");
            nameLayer = root.Q("name-layer");
            marker = root.Q("marker");
            markerText = root.Q<Label>("marker-text");
            staminaFill = root.Q("stamina-fill");
            lobbyPanel = root.Q("lobby-panel");
            endPanel = root.Q("end-panel");
            pausePanel = root.Q("pause-panel");
            timer = root.Q<BFSeg>("timer");
            timerRing = root.Q("timer-ring");
            timerFill = root.Q("timer-fill");
            modeName = root.Q<Label>("mode-name");
            score = root.Q<Label>("score");
            delivered = root.Q<Label>("delivered");
            centerBig = root.Q<Label>("center-big");
            centerWrap = root.Q("center-wrap");
            prompt = root.Q("prompt");
            crosshair = root.Q<BFCrosshair>("crosshair");
            camMode = root.Q<Label>("cam-mode");
            fpsCounter = root.Q<Label>("fps-counter");
            flashBanner = root.Q("flash-banner");
            flashText = root.Q<Label>("flash-text");
            combo = root.Q("combo");
            comboText = root.Q<Label>("combo-text");
            lobbyMode = root.Q<Label>("lobby-mode");
            lobbyDesc = root.Q<Label>("lobby-desc");
            lobbyCount = root.Q<Label>("lobby-count");
            lobbyPlayers = root.Q("lobby-players");
            lobbyIp = root.Q<Label>("lobby-ip");
            lobbyWait = root.Q<Label>("lobby-wait");
            endTitle = root.Q<Label>("end-title");
            endStars = root.Q<BFStars>("end-stars");
            endScore = root.Q<Label>("end-score");
            endDetail = root.Q<Label>("end-detail");
            endLines = root.Q("end-lines");
            btnStart = root.Q<Button>("btn-start");
            btnRestart = root.Q<Button>("btn-restart");

            btnStart.clicked += StartRound;
            btnRestart.clicked += StartRound;
            root.Q<Button>("btn-menu").clicked += BackToMenu;
            root.Q<Button>("btn-pause-menu").clicked += BackToMenu;
            root.Q<Button>("btn-resume").clicked += () => SetPaused(false);
            var sens = root.Q<Slider>("sens-slider");
            sens.value = GameSettings.MouseSensitivity;
            sens.RegisterValueChangedCallback(e => GameSettings.MouseSensitivity = e.newValue);
            var fpsT = root.Q<Toggle>("fps-toggle");
            fpsT.value = GameSettings.FirstPerson;
            fpsT.RegisterValueChangedCallback(e => { if (PlayerCamera.Instance != null) PlayerCamera.Instance.SetFirstPerson(e.newValue); else GameSettings.FirstPerson = e.newValue; });

            FillControls(root.Q("controls"));
            FillControls(root.Q("pause-controls"));

            GameModeBase.OnFeed += AddFeed;
            localIp = GetLocalIP();
            SetPaused(false);
        }

        void OnDisable()
        {
            GameModeBase.OnFeed -= AddFeed;
            BFInput.Blocked = false;
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
        }

        static void FillControls(VisualElement grid)
        {
            if (grid == null) return;
            grid.Clear();
            foreach (var c in Controls)
            {
                var row = new VisualElement(); row.AddToClassList("control");
                var k = new Label(c.key); k.AddToClassList("key");
                var t = new Label(c.text); t.AddToClassList("key-text");
                row.Add(k); row.Add(t);
                grid.Add(row);
            }
        }

        void StartRound()
        {
            Sfx.Play(SfxId.Click, 0.4f);
            var mode = GameModeBase.Current;
            if (mode != null && NetworkServer.active) mode.ServerStartRound();
        }

        void BackToMenu()
        {
            Sfx.Play(SfxId.Click, 0.4f);
            BFInput.Blocked = false;
            BFNetworkManager.Instance?.Leave();
        }

        void SetPaused(bool p)
        {
            paused = p;
            pausePanel.EnableInClassList("hidden", !p);
            BFInput.Blocked = p;
        }

        void Update()
        {
            var mode = GameModeBase.Current;
            var local = BFPlayer.Local;

            if (BFInput.Pause()) SetPaused(!paused);

            // FPS sayacı
            fpsAccum += Time.unscaledDeltaTime; fpsFrames++; fpsTimer += Time.unscaledDeltaTime;
            if (fpsTimer >= 0.4f)
            {
                fpsCounter.text = GameSettings.ShowFps ? Mathf.RoundToInt(fpsFrames / Mathf.Max(fpsAccum, 0.0001f)) + " FPS" : "";
                fpsAccum = 0; fpsFrames = 0; fpsTimer = 0;
            }

            if (mode == null) return;
            var state = mode.state;
            bool isHost = NetworkServer.active;
            var slm = mode as ShoppingListMode;
            bool stateChanged = state != lastState;
            lastState = state;

            // --- liste (fiş)
            if (slm != null) RefreshList(slm);
            if (stateChanged) listPanel.EnableInClassList("hidden", state == RoundState.Lobby || state == RoundState.Ended);
            if (stateChanged) timerRing.EnableInClassList("hidden", state == RoundState.Ended);

            // --- süre halkası
            float t = state == RoundState.Playing ? mode.TimeLeft : (state == RoundState.Ended ? 0 : mode.roundSeconds);
            int tSec = Mathf.CeilToInt(Mathf.Max(0f, t));
            if (tSec != lastTimerSec)
            {
                lastTimerSec = tSec; timer.Text = FormatTime(t);
                timerFill.style.width = Length.Percent(mode.roundSeconds > 0 ? Mathf.Clamp01(t / mode.roundSeconds) * 100f : 0f);
            }
            bool low = state == RoundState.Playing && t <= 30f;
            if (low != lastLow) { lastLow = low; timerRing.EnableInClassList("timer-box--low", low); }

            // --- puan / teslim
            if (mode.score != lastScore) { lastScore = mode.score; score.text = lastScore.ToString(); }
            if (slm != null && (slm.TotalDelivered != lastDelivered || slm.TotalRequired != lastRequired))
            {
                lastDelivered = slm.TotalDelivered; lastRequired = slm.TotalRequired;
                delivered.text = $"{lastDelivered} / {lastRequired}";
            }

            // --- kombo + flaş indirim
            if (slm != null)
            {
                int cmb = state == RoundState.Playing ? slm.ComboNow : 1;
                if (cmb != lastCombo)
                {
                    lastCombo = cmb;
                    combo.EnableInClassList("hidden", cmb < 2);
                    comboText.text = "x" + cmb;
                }
                int fl = state == RoundState.Playing && slm.FlashActive ? slm.flashProductId : -1;
                if (fl != lastFlash)
                {
                    lastFlash = fl;
                    flashBanner.EnableInClassList("hidden", fl < 0);
                    if (fl >= 0)
                    {
                        var d = ProductDatabase.Instance != null ? ProductDatabase.Instance.Get(fl) : null;
                        flashText.text = (d != null ? d.displayName.ToUpper(new System.Globalization.CultureInfo("tr-TR")) : "") + "  ·  ÇİFT PUAN";
                    }
                    listHash = -1; // listede vurgula
                }
            }

            // --- geri sayım / başla
            bool showCenter = false;
            if (state == RoundState.Countdown)
            {
                int c = Mathf.CeilToInt(mode.TimeLeft);
                if (c != lastCountdown) { centerBig.text = c > 0 ? c.ToString() : ""; SetCenterText(false); }
                if (c != lastCountdown && c > 0 && lastCountdown != -1) Sfx.Play(SfxId.Beep);
                lastCountdown = c;
                showCenter = c > 0;
            }
            else
            {
                if (state == RoundState.Playing && mode.roundSeconds - mode.TimeLeft < 1.3f)
                {
                    if (lastCountdown != -2) { centerBig.text = "KOŞ!"; SetCenterText(true); lastCountdown = -2; }
                    showCenter = true;
                }
                else lastCountdown = 0;
            }
            centerWrap.EnableInClassList("hidden", !showCenter);

            // --- lobi
            if (stateChanged || paused != lastPausedShown) { lobbyPanel.EnableInClassList("hidden", state != RoundState.Lobby || paused); }
            if (state == RoundState.Lobby) RefreshLobby(mode, isHost);

            // --- bitiş fişi
            if (stateChanged || paused != lastPausedShown) endPanel.EnableInClassList("hidden", state != RoundState.Ended || paused);
            lastPausedShown = paused;
            if (state == RoundState.Ended)
            {
                RefreshEnd(mode, slm, isHost);
                if (isHost && !paused && BFInput.Confirm()) StartRound();
            }
            else lastEndHash = -1;

            // --- imleç: oyun sırasında kilitli, menü/bitiş ekranında serbest
            bool wantLock = !paused && state != RoundState.Ended && Application.isFocused;
            var lm = wantLock ? CursorLockMode.Locked : CursorLockMode.None;
            if (UnityEngine.Cursor.lockState != lm) UnityEngine.Cursor.lockState = lm;
            UnityEngine.Cursor.visible = !wantLock;

            // --- nişangâh / kamera modu
            var pc = PlayerCamera.Instance;
            bool fps = pc != null && pc.FirstPerson;
            bool aiming = local != null && local.IsAiming;
            cachedTarget = local != null && !local.IsCarrying && mode.CanPickUp ? local.FindTargetItem() : null;
            int cross = paused || !(fps || aiming) || state == RoundState.Ended ? -1
                      : (local != null && local.IsCarrying && aiming ? 2 : (cachedTarget != null ? 1 : 0));
            if (cross != lastCross)
            {
                lastCross = cross;
                crosshair.EnableInClassList("hidden", cross < 0);
                if (cross >= 0) crosshair.Mode = cross;
            }
            string cm = fps ? "FPS" : (aiming ? "TPS • NİŞAN" : "TPS");
            if (!ReferenceEquals(cm, lastCamMode)) { lastCamMode = cm; camMode.text = cm; }

            // --- yerel oyuncu
            UpdatePrompt(local, mode);
            if (local != null)
            {
                int st = Mathf.RoundToInt(local.Stamina / local.staminaMax * 200f);
                if (st != lastStamina) { lastStamina = st; staminaFill.style.width = Length.Percent(st * 0.5f); }
            }

            UpdateMarker(local, mode);
            UpdateNameTags();
        }

        bool lastPausedShown;

        void SetCenterText(bool text)
        {
            if (text == lastCenterText) return;
            lastCenterText = text;
            centerBig.EnableInClassList("center-big--text", text);
        }

        // ================================================================ tuş ipuçları
        void UpdatePrompt(BFPlayer local, GameModeBase mode)
        {
            int key = 17;
            if (local != null)
            {
                var h = local.HeldItem;
                key = key * 31 + (paused ? 1 : 0);
                key = key * 31 + (local.IsStunned ? 1 : 0);
                key = key * 31 + (h != null ? (int)h.netId + 1 : 0);
                key = key * 31 + (local.IsAiming ? 1 : 0);
                key = key * 31 + (cachedTarget != null ? (int)cachedTarget.netId + 1 : 0);
                key = key * 31 + (mode.CanPickUp ? 1 : 0);
                key = key * 31 + (int)mode.state;
            }
            if (key == lastPromptKey) return;
            lastPromptKey = key;

            prompt.Clear();
            bool any = false;
            void Title(IconKind icon, string text)
            {
                prompt.Add(new BFIcon(icon));
                var l = new Label(text); l.AddToClassList("prompt-title"); prompt.Add(l); any = true;
            }
            void Key(string k, string text)
            {
                var kl = new Label(k); kl.AddToClassList("key"); prompt.Add(kl);
                var tl = new Label(text); tl.AddToClassList("key-text"); prompt.Add(tl); any = true;
            }

            if (local != null && !paused && mode.state != RoundState.Ended)
            {
                if (local.IsStunned) Title(IconKind.Warning, "Sersemledin!");
                else if (local.IsCarrying)
                {
                    var held = local.HeldItem;
                    Title(held != null ? BFDraw.ProductIcon(held.productId) : IconKind.Tag, held != null ? held.DisplayName : "Ürün");
                    if (local.IsAiming) Key("Sol Tık", "Fırlat");
                    else { Key("Sağ Tık", "Nişan"); Key("Sol Tık", "Fırlat"); Key("E", "Bırak"); }
                }
                else if (mode.CanPickUp)
                {
                    if (cachedTarget != null) { Title(BFDraw.ProductIcon(cachedTarget.productId), cachedTarget.DisplayName); Key("E", "Al"); }
                }
                else if (mode.state == RoundState.Lobby) Title(IconKind.Cart, "Mağazaları keşfet — kapılar açılınca ürünleri kap!");
            }
            prompt.EnableInClassList("prompt--hidden", !any);
        }

        // ================================================================ liste
        void RefreshList(ShoppingListMode slm)
        {
            int h = 17;
            foreach (var e in slm.list) h = h * 31 + e.productId * 7 + e.delivered * 3 + e.required;
            h = h * 31 + lastFlash;
            if (h == listHash) return;
            listHash = h;
            listContainer.Clear();
            var db = ProductDatabase.Instance;
            foreach (var e in slm.list)
            {
                var def = db != null ? db.Get(e.productId) : null;
                var row = new VisualElement(); row.AddToClassList("list-row");
                if (e.Done) row.AddToClassList("list-row--done");
                bool flash = !e.Done && lastFlash == e.productId;
                if (flash) row.AddToClassList("list-row--flash");
                var q = new Label($"{e.required}x"); q.AddToClassList("list-qty"); row.Add(q);
                var n = new Label(def != null ? def.displayName.ToUpper(new System.Globalization.CultureInfo("tr-TR")) : "?"); n.AddToClassList("list-name");
                row.Add(n);
                if (flash) { var b = new Label("x2"); b.AddToClassList("list-flash"); row.Add(b); }
                var lead = new VisualElement(); lead.AddToClassList("list-lead"); row.Add(lead);
                var c = new Label($"{Mathf.Min(e.delivered, e.required)}/{e.required}"); c.AddToClassList("list-count");
                row.Add(c);
                var box = new VisualElement(); box.AddToClassList("list-check"); box.Add(new BFIcon(IconKind.Check));
                row.Add(box);
                var strike = new VisualElement(); strike.AddToClassList("list-strike"); strike.pickingMode = PickingMode.Ignore; row.Add(strike);
                listContainer.Add(row);
            }
        }

        // ================================================================ lobi
        void RefreshLobby(GameModeBase mode, bool isHost)
        {
            int h = BFPlayer.All.Count * 7 + (isHost ? 1 : 0);
            foreach (var p in BFPlayer.All) h = h * 31 + (p.playerName != null ? p.playerName.GetHashCode() : 0) + p.playerIndex;
            if (h != lastLobbyHash)
            {
                lastLobbyHash = h;
                lobbyMode.text = GameCatalog.Levels[GameCatalog.SelectedLevel].displayName + "  •  " + mode.ModeName;
                lobbyDesc.text = mode.ModeHint;
                lobbyCount.text = $"EKİP  {BFPlayer.All.Count}/{BFNetworkManager.MaxPlayers}";
                lobbyPlayers.Clear();
                foreach (var p in BFPlayer.All)
                {
                    var chip = new VisualElement(); chip.AddToClassList("player-chip");
                    var dot = new VisualElement(); dot.AddToClassList("player-dot"); dot.style.backgroundColor = p.PlayerColor;
                    var nm = new Label(p.playerName); nm.AddToClassList("player-name");
                    chip.Add(dot); chip.Add(nm); lobbyPlayers.Add(chip);
                }
                lobbyIp.text = isHost ? "Arkadaşların bu IP ile katılabilir: " + localIp : "";
                btnStart.EnableInClassList("hidden", !isHost);
                lobbyWait.EnableInClassList("hidden", isHost);
            }
            if (isHost && !paused && BFInput.Confirm()) StartRound();
        }

        // ================================================================ bitiş fişi
        void RefreshEnd(GameModeBase mode, ShoppingListMode slm, bool isHost)
        {
            int h = mode.score * 31 + mode.stars * 7 + (mode.success ? 1 : 0) + (isHost ? 2 : 0) + BFPlayer.All.Count * 13;
            if (h == lastEndHash) return;
            lastEndHash = h;
            endTitle.text = mode.success ? "LİSTE TAMAM" : "SÜRE BİTTİ";
            endTitle.EnableInClassList("end-stamp--win", mode.success);
            endStars.Filled = Mathf.Clamp(mode.stars, 0, 3);
            endScore.text = mode.score.ToString();
            endLines.Clear();
            void Line(string name, string val, string cls = null)
            {
                var row = new VisualElement(); row.AddToClassList("end-line");
                if (cls != null) row.AddToClassList(cls);
                var a = new Label(name); a.AddToClassList("end-line-name");
                var b = new Label(val); b.AddToClassList("end-line-val");
                row.Add(a); row.Add(b); endLines.Add(row);
            }
            if (slm != null)
            {
                var db = ProductDatabase.Instance;
                foreach (var e in slm.list)
                {
                    var def = db != null ? db.Get(e.productId) : null;
                    int got = Mathf.Min(e.delivered, e.required);
                    int pts = (def != null ? def.points : 100) * e.delivered;
                    Line($"{(def != null ? def.displayName : "?")}   {got}/{e.required}", pts > 0 ? pts.ToString() : "—");
                }
                if (slm.bonusPoints > 0) Line("Kombo + flaş indirim", "+" + slm.bonusPoints, "end-line--bonus");
                if (slm.timeBonus > 0) Line("Süre bonusu", "+" + slm.timeBonus, "end-line--bonus");
                if (slm.penaltyTotal > 0) Line("Listede olmayan ürün", "-" + slm.penaltyTotal, "end-line--bad");
                var sb = new System.Text.StringBuilder();
                foreach (var p in BFPlayer.All) { if (sb.Length > 0) sb.Append("   •   "); sb.Append(p.playerName).Append(": ").Append(p.deliveredCount).Append(" ürün"); }
                endDetail.text = sb.ToString();
            }
            btnRestart.EnableInClassList("hidden", !isHost);
        }

        // ================================================================ akış
        void AddFeed(string msg, FeedKind kind)
        {
            var item = new VisualElement(); item.AddToClassList("feed-item");
            IconKind icon = IconKind.Tag;
            if (kind == FeedKind.Good) { item.AddToClassList("feed-item--good"); icon = IconKind.Check; }
            else if (kind == FeedKind.Bad) { item.AddToClassList("feed-item--bad"); icon = IconKind.Warning; }
            else if (kind == FeedKind.Announce) { item.AddToClassList("feed-item--announce"); icon = msg.StartsWith("FLAŞ") ? IconKind.Bolt : IconKind.Megaphone; }
            if (kind == FeedKind.Announce) Sfx.Play(SfxId.Go, 0.3f); // anons zili
            item.Add(new BFIcon(icon));
            var l = new Label(msg); l.AddToClassList("feed-text"); item.Add(l);
            feed.Insert(0, item);
            while (feed.childCount > 5) feed.RemoveAt(feed.childCount - 1);
            int life = kind == FeedKind.Announce ? 5200 : 3400;
            item.schedule.Execute(() => item.AddToClassList("feed-item--out")).StartingIn(life);
            item.schedule.Execute(() => item.RemoveFromHierarchy()).StartingIn(life + 400);
        }

        // ================================================================ kasa işareti (ürün taşırken en yakın kasa)
        void UpdateMarker(BFPlayer local, GameModeBase mode)
        {
            var cam = Camera.main;
            bool show = local != null && cam != null && local.IsCarrying && mode.state == RoundState.Playing && !paused && root.panel != null;
            CheckoutZone best = null; float bd = float.MaxValue;
            if (show)
            {
                Vector3 p = local.transform.position;
                foreach (var z in CheckoutZone.All)
                {
                    float d = (z.transform.position - p).sqrMagnitude;
                    if (d < bd) { bd = d; best = z; }
                }
                show = best != null && bd > 2.5f * 2.5f;
            }
            marker.EnableInClassList("hidden", !show);
            if (!show) { lastMarkerDist = -1; return; }

            int dist = Mathf.RoundToInt(Mathf.Sqrt(bd));
            if (dist != lastMarkerDist) { lastMarkerDist = dist; markerText.text = $"{dist} m"; }
            Vector3 sp = cam.WorldToScreenPoint(best.transform.position + Vector3.up * 2.4f);
            float w = Screen.width, hgt = Screen.height;
            if (sp.z < 0) { sp.x = w - sp.x; sp.y = 0; } // arkadaysa alt kenara yansıt
            float mx = w * 0.07f, my = hgt * 0.09f;
            sp.x = Mathf.Clamp(sp.x, mx, w - mx);
            sp.y = Mathf.Clamp(sp.y, my + hgt * 0.12f, hgt - my - hgt * 0.14f);
            Vector2 pp = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(sp.x, hgt - sp.y));
            marker.style.left = pp.x; marker.style.top = pp.y;
        }

        void UpdateNameTags()
        {
            var cam = Camera.main;
            if (cam == null || root.panel == null) return;
            deadTags.Clear();
            foreach (var kv in nameTags) if (kv.Key == null || !BFPlayer.All.Contains(kv.Key) || kv.Key.isLocalPlayer) deadTags.Add(kv.Key);
            foreach (var d in deadTags) { nameTags[d].RemoveFromHierarchy(); nameTags.Remove(d); }

            foreach (var p in BFPlayer.All)
            {
                if (p.isLocalPlayer) continue;
                if (!nameTags.TryGetValue(p, out var tag))
                {
                    tag = new Label(); tag.AddToClassList("name-tag"); tag.pickingMode = PickingMode.Ignore;
                    nameLayer.Add(tag); nameTags[p] = tag;
                }
                if (tag.text != p.playerName) { tag.text = p.playerName; tag.style.color = p.PlayerColor; }
                Vector3 sp = cam.WorldToScreenPoint(p.transform.position + Vector3.up * 2.1f);
                bool vis = sp.z > 0;
                tag.style.display = vis ? DisplayStyle.Flex : DisplayStyle.None;
                if (!vis) continue;
                Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(sp.x, Screen.height - sp.y));
                tag.style.left = panelPos.x;
                tag.style.top = panelPos.y;
            }
        }

        static string FormatTime(float t)
        {
            int s = Mathf.CeilToInt(t);
            return $"{s / 60}:{s % 60:00}";
        }

        static string GetLocalIP()
        {
            try
            {
                foreach (var ip in System.Net.Dns.GetHostAddresses(System.Net.Dns.GetHostName()))
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !System.Net.IPAddress.IsLoopback(ip))
                        return ip.ToString();
            }
            catch { }
            return "localhost";
        }
    }
}
