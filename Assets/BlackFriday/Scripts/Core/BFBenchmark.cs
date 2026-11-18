using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Mirror;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BlackFriday
{
    /// <summary>
    /// Otomatik test. Oyunu "-bfbench" ve/veya "-bfshots" argümanıyla başlat:
    /// tek başına host kurar, turu başlatır.
    ///  -bfshots: ortam + ürün tutma (TPS/FPS/nişan) + zıplama ekran görüntülerini bench_shots klasörüne kaydeder.
    ///  -bfbench: AVM'de belirli noktalardan TPS ve FPS kamerayla geçer, ortalama / %1 düşük FPS ve
    ///            render istatistiklerini oyunun klasöründeki bench.txt dosyasına yazar.
    /// Sonra kapanır.
    /// </summary>
    public class BFBenchmark : MonoBehaviour
    {
        static bool requested, shots, bench;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            foreach (var a in System.Environment.GetCommandLineArgs())
            {
                if (a == "-bfbench") bench = true;
                if (a == "-bfshots") shots = true;
            }
            requested = bench || shots;
            if (!requested) return;
            var go = new GameObject("BFBenchmark");
            DontDestroyOnLoad(go);
            go.AddComponent<BFBenchmark>();
        }

        struct Shot { public Vector3 pos; public float yaw, pitch; public Shot(Vector3 p, float y, float pi) { pos = p; yaw = y; pitch = pi; } }

        static readonly Shot[] Path =
        {
            new Shot(new Vector3(-36f, 0.05f, 0f), 90, 5),
            new Shot(new Vector3(-22f, 0.05f, -2f), 75, 0),
            new Shot(new Vector3(-8f, 0.05f, 3f), 100, -5),
            new Shot(new Vector3(0f, 0.05f, -5.5f), 20, 10),
            new Shot(new Vector3(12f, 0.05f, 2f), 250, 0),
            new Shot(new Vector3(-26f, 0.05f, 10f), 0, 5),
            new Shot(new Vector3(0f, 0.05f, 12f), 30, 5),
            new Shot(new Vector3(26f, 0.05f, -11f), 180, 5),
            new Shot(new Vector3(-26f, 0.05f, -12f), 200, 0),
            new Shot(new Vector3(30f, 0.05f, 0f), 270, 0),
            new Shot(new Vector3(42f, 0.05f, 0f), 90, 3),      // yemek alanı
            new Shot(new Vector3(58.5f, 0.05f, 0f), 90, 3),    // market girişi
            new Shot(new Vector3(77f, 0.05f, -15.6f), 270, 3), // market reyonu
            new Shot(new Vector3(28f, 5.4f, 0f), 270, 12),     // üst kat köprüsü
        };

        IEnumerator Start()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            yield return new WaitForSeconds(1f);
            string shotDir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), "bench_shots");
            if (shots) { Directory.CreateDirectory(shotDir); yield return new WaitForSeconds(1f); yield return Cap(shotDir, "ui_menu"); }
            while (BFNetworkManager.Instance == null) yield return null;
            GameSettings.FirstPerson = false;
            BFNetworkManager.Instance.HostGame(true);
            while (BFPlayer.Local == null || GameModeBase.Current == null) yield return null;
            yield return new WaitForSeconds(1f);
            if (shots) { yield return new WaitForSeconds(0.5f); yield return Cap(shotDir, "ui_lobby"); }
            GameModeBase.Current.ServerStartRound();
            if (shots) { yield return new WaitForSeconds(1.2f); yield return Cap(shotDir, "ui_countdown"); }
            yield return new WaitForSeconds(4f);

            if (shots) yield return Shots(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), "bench_shots"));
            if (!bench) { Application.Quit(); yield break; }

            var log = new StringBuilder();
            log.AppendLine($"GPU: {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType})  CPU: {SystemInfo.processorType}");
            log.AppendLine($"Çözünürlük: {Screen.width}x{Screen.height}  Kalite: {QualitySettings.names[QualitySettings.GetQualityLevel()]}");

            var all = new List<float>();
            foreach (bool fps in new[] { false, true })
            {
                var cam = PlayerCamera.Instance;
                cam.SetFirstPerson(fps);
                var mode = new List<float>();
                foreach (var s in Path)
                {
                    Teleport(s.pos, s.yaw);
                    cam.SetAngles(s.yaw, s.pitch);
                    yield return new WaitForSeconds(0.35f); // ısınma
                    var shot = new List<float>();
                    using (var batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count"))
                    using (var setpass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count"))
                    using (var tris = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count"))
                    using (var casters = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Shadow Casters Count"))
                    {
                        float t = 0;
                        while (t < 1.4f)
                        {
                            cam.SetAngles(s.yaw + Mathf.Sin(t * 2f) * 25f, s.pitch);
                            yield return null;
                            float dt = Time.unscaledDeltaTime;
                            t += dt;
                            shot.Add(dt);
                        }
                        float avg = Avg(shot);
                        log.AppendLine($"{(fps ? "FPS" : "TPS")} {s.pos}  ort {1f / avg:F0} fps  batches {batches.LastValue} setpass {setpass.LastValue} tris {tris.LastValue / 1000}k casters {casters.LastValue}");
                    }
                    mode.AddRange(shot);
                }
                all.AddRange(mode);
                log.AppendLine($"== {(fps ? "FPS" : "TPS")} ortalama {1f / Avg(mode):F0} fps, %1 düşük {Low1(mode):F0} fps");
            }
            log.AppendLine($"=== GENEL ortalama {1f / Avg(all):F0} fps, %1 düşük {Low1(all):F0} fps");
            string path = Path_();
            File.WriteAllText(path, log.ToString());
            Debug.Log("[BF] Benchmark yazıldı: " + path + "\n" + log);
            yield return new WaitForSeconds(0.2f);
            Application.Quit();
        }

        // ================================================================ görsel kontrol
        IEnumerator Shots(string dir)
        {
            Directory.CreateDirectory(dir);
            var cam = PlayerCamera.Instance;
            var pl = BFPlayer.Local;

            cam.SetFirstPerson(false);
            for (int i = 0; i < Path.Length; i++)
            {
                Teleport(Path[i].pos, Path[i].yaw);
                cam.SetAngles(Path[i].yaw, Path[i].pitch);
                yield return new WaitForSeconds(0.6f);
                yield return Cap(dir, $"env_{i:00}");
            }

            Vector3 spot = new Vector3(-18f, 0.05f, 0f);
            float yaw = 90f;
            foreach (int id in new[] { 0, 2, 7, 5 })
            {
                var item = FindItem(id, spot);
                if (item == null) continue;
                Teleport(spot, yaw);
                cam.SetFirstPerson(false);
                pl.ServerDebugGive(item);
                cam.SetAngles(yaw, 8);
                yield return new WaitForSeconds(1.3f);
                yield return Cap(dir, $"item{id}_tps_back");
                var tik = pl.visual != null ? pl.visual.GetComponentInChildren<HandIK>() : null;
                Debug.Log($"[BF] IK TPS item{id}: " + (tik != null ? tik.DebugReport() : "yok"));
                cam.SetAngles(yaw + 145, 4);
                yield return new WaitForSeconds(0.5f);
                yield return Cap(dir, $"item{id}_tps_front");
                cam.SetAngles(yaw + 90, 4);
                yield return new WaitForSeconds(0.5f);
                yield return Cap(dir, $"item{id}_tps_side");
                BFInput.SimulateAim = true;
                cam.SetAngles(yaw, 4);
                yield return new WaitForSeconds(0.6f);
                yield return Cap(dir, $"item{id}_tps_aim");
                BFInput.SimulateAim = false;

                cam.SetFirstPerson(true);
                cam.SetAngles(yaw, 0);
                yield return new WaitForSeconds(0.6f);
                yield return Cap(dir, $"item{id}_fps");
                var fik = FirstPersonArms.Instance != null ? FirstPersonArms.Instance.GetComponentInChildren<HandIK>() : null;
                Debug.Log($"[BF] IK FPS item{id}: " + (fik != null ? fik.DebugReport() : "yok"));
                cam.SetAngles(yaw, -40);
                yield return new WaitForSeconds(0.4f);
                yield return Cap(dir, $"item{id}_fps_down");
                BFInput.SimulateAim = true;
                cam.SetAngles(yaw, 0);
                yield return new WaitForSeconds(0.5f);
                yield return Cap(dir, $"item{id}_fps_aim");
                BFInput.SimulateAim = false;
                cam.SetFirstPerson(false);
                var held = pl.HeldItem;
                pl.ServerDropHeld(Vector3.zero);
                if (held != null)
                {
                    // test yığını oluşmasın: bırakılan ürünü kenara kaldır
                    var rb = held.GetComponent<Rigidbody>();
                    Vector3 away = spot + new Vector3(0f, 0.6f, 5f + id * 0.6f);
                    if (rb != null) { rb.position = away; rb.linearVelocity = Vector3.zero; }
                    held.transform.position = away;
                }
                yield return new WaitForSeconds(0.4f);
            }

            // fırlatma dizisi: sabit 60 kare/sn zaman adımıyla her 2 karede bir görüntü
            foreach (var (tag, fp, camYaw) in new[] { ("tps_side", false, yaw + 90f), ("tps_back", false, yaw), ("fps", true, yaw) })
            {
                var ti = FindItem(2, spot);
                if (ti == null) break;
                Teleport(spot, yaw);
                cam.SetFirstPerson(fp);
                pl.ServerDebugGive(ti);
                cam.SetAngles(camYaw, fp ? 0 : 6);
                yield return new WaitForSeconds(1.0f);
                Time.captureFramerate = 60;
                pl.DebugThrow();
                for (int f = 0; f < 26; f++) yield return Cap(dir, $"thr_{tag}_{f:00}");
                Time.captureFramerate = 0;
                Debug.Log($"[BF] Throw seq {tag}: item at {ti.transform.position}");
                cam.SetFirstPerson(false);
                yield return new WaitForSeconds(0.8f);
                var trb = ti.GetComponent<Rigidbody>();
                Vector3 away = spot + new Vector3(0f, 0.6f, 8f);
                if (trb != null) { trb.position = away; trb.linearVelocity = Vector3.zero; }
                ti.transform.position = away;
            }

            // boş el FPS + zıplama (yandan)
            Teleport(spot, yaw);
            cam.SetFirstPerson(true);
            cam.SetAngles(yaw, 0);
            yield return new WaitForSeconds(0.6f);
            yield return Cap(dir, "empty_fps");
            cam.SetFirstPerson(false);
            cam.SetAngles(yaw + 90, 4);
            yield return new WaitForSeconds(0.6f);
            yield return Cap(dir, "jump_0_idle");
            BFInput.SimulateJump = true;
            yield return new WaitForSeconds(0.15f);
            yield return Cap(dir, "jump_1_up");
            yield return new WaitForSeconds(0.15f);
            yield return Cap(dir, "jump_2_top");
            yield return new WaitForSeconds(0.2f);
            yield return Cap(dir, "jump_3_fall");
            yield return new WaitForSeconds(0.25f);
            yield return Cap(dir, "jump_4_land");
            Debug.Log($"[BF] Jump test: y={pl.transform.position.y:F2}");

            // çevre etkileşimi: koli kulesi devirme, araba itme, bölgeler
            if (BoxTower.All.Count > 0)
            {
                var tw = BoxTower.All[0];
                Teleport(tw.transform.position + new Vector3(-3.6f, 0, 0.6f), 90);
                cam.SetAngles(90, 8);
                yield return new WaitForSeconds(0.7f);
                yield return Cap(dir, "int_tower_0");
                tw.ServerTopple(Vector3.right, pl);
                yield return new WaitForSeconds(0.6f);
                yield return Cap(dir, "int_tower_1");
                Debug.Log($"[BF] Etkileşim: kule devrildi={tw.Toppled}, kule sayısı={BoxTower.All.Count}");
            }
            var cart = FindFirstObjectByType<PushableProp>();
            if (cart != null)
            {
                Vector3 c0 = cart.transform.position;
                cart.ServerPush(cart.transform.forward, 2f);
                yield return new WaitForSeconds(0.8f);
                Debug.Log($"[BF] Etkileşim: araba {Vector3.Distance(c0, cart.transform.position):F2} m kaydı; kaygan bölge={SlipZone.All.Count}, bant hızı={ConveyorZone.VelocityAt(new Vector3(-10f, 2f, -6f))}");
            }

            // arayüz durumları: flaş indirim + kombo, bitiş fişi
            var slm = GameModeBase.Current as ShoppingListMode;
            if (slm != null && slm.list.Count > 0)
            {
                slm.flashProductId = slm.list[0].productId; slm.flashEndTime = NetworkTime.time + 30;
                slm.combo = 3; slm.comboEndTime = NetworkTime.time + 30;
                slm.ServerFeed("FLAŞ İNDİRİM: test ürünü şimdi çift puan!", FeedKind.Announce);
                slm.ServerFeed("Oyuncu: Televizyon kasada! +900  FLAŞ x2  KOMBO x3", FeedKind.Good);
                slm.ServerFeed("Blender listede yok! -30", FeedKind.Bad);
                var it = FindItem(slm.list[0].productId, pl.transform.position);
                if (it != null) pl.ServerDebugGive(it);
                cam.SetAngles(yaw, 6);
                yield return new WaitForSeconds(0.8f);
                yield return Cap(dir, "ui_play");
                slm.score = 1250; slm.bonusPoints = 450; slm.penaltyTotal = 30;
                slm.ServerEndRound(false);
                yield return new WaitForSeconds(0.8f);
                yield return Cap(dir, "ui_end");
            }
        }

        static IEnumerator Cap(string dir, string name)
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(System.IO.Path.Combine(dir, name + ".jpg"), tex.EncodeToJPG(82));
            Destroy(tex);
            yield return null;
        }

        static ProductItem FindItem(int id, Vector3 near)
        {
            ProductItem best = null; float bd = float.MaxValue;
            foreach (var it in ProductItem.All)
            {
                if (it == null || it.IsCarried || it.productId != id) continue;
                float d = (it.transform.position - near).sqrMagnitude;
                if (d < bd) { bd = d; best = it; }
            }
            return best;
        }

        static string Path_() => System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), "bench.txt");

        static void Teleport(Vector3 p, float yaw)
        {
            var pl = BFPlayer.Local;
            var cc = pl.GetComponent<CharacterController>();
            cc.enabled = false;
            pl.transform.SetPositionAndRotation(p, Quaternion.Euler(0, yaw, 0));
            cc.enabled = true;
        }

        static float Avg(List<float> l) { float s = 0; foreach (var v in l) s += v; return s / Mathf.Max(1, l.Count); }

        static float Low1(List<float> l)
        {
            var c = new List<float>(l); c.Sort(); c.Reverse();
            int n = Mathf.Max(1, c.Count / 100);
            float s = 0; for (int i = 0; i < n; i++) s += c[i];
            return 1f / (s / n);
        }
    }
}
