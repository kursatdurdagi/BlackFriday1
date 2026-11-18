using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace BlackFriday.EditorTools
{
    /// <summary>
    /// Animatörler: karakter (TPS, ağdaki herkes) ve FPS el modeli.
    /// Kol pozları humanoid kas eğrileriyle üretilir (bu rigde "Arm Front-Back" negatif = öne).
    /// </summary>
    public static partial class BFBuilder
    {
        // ---------------------------------------------------------------- poz kütüphanesi
        // Her poz: kas adı -> değer (iki kola simetrik uygulanır). "Fingers" = parmak kıvrımı (1 açık, -1 yumruk)
        /// <summary>Tek kollu poz: sağ ve sol kol ayrı değerler ("R:" / "L:" önekleri).</summary>
        public static Dictionary<string, float> Arms(Dictionary<string, float> right, Dictionary<string, float> left)
        {
            var d = new Dictionary<string, float>();
            foreach (var kv in right) d["R:" + kv.Key] = kv.Value;
            foreach (var kv in left) d["L:" + kv.Key] = kv.Value;
            return d;
        }

        static string Bare(string n) => n.StartsWith("R:") || n.StartsWith("L:") ? n.Substring(2) : n;

        public static Dictionary<string, float> Pz(params object[] kv)
        {
            var d = new Dictionary<string, float>();
            for (int i = 0; i + 1 < kv.Length; i += 2) d[(string)kv[i]] = System.Convert.ToSingle(kv[i + 1]);
            return d;
        }

        public static Dictionary<string, float> PoseCarry = Pz("Arm Down-Up", -0.5f, "Arm Front-Back", -0.72f, "Arm Twist In-Out", 0.25f, "Forearm Stretch", 0.75f, "Forearm Twist In-Out", 0.4f, "Hand Down-Up", 0.1f, "Hand In-Out", 0.3f, "Shoulder Down-Up", 0f, "Shoulder Front-Back", 0f, "Fingers", 0.15f);
        public static Dictionary<string, float> PoseWindup = Pz("Arm Down-Up", 0.75f, "Arm Front-Back", -0.15f, "Arm Twist In-Out", 0.2f, "Forearm Stretch", -0.1f, "Forearm Twist In-Out", 0.4f, "Hand Down-Up", -0.3f, "Hand In-Out", 0.3f, "Shoulder Down-Up", 0.5f, "Shoulder Front-Back", 0f, "Fingers", -0.2f);
        public static Dictionary<string, float> PoseRelease = Pz("Arm Down-Up", 0.0f, "Arm Front-Back", -1f, "Arm Twist In-Out", 0.1f, "Forearm Stretch", 1f, "Forearm Twist In-Out", 0.3f, "Hand Down-Up", 0.4f, "Hand In-Out", 0f, "Shoulder Down-Up", 0.15f, "Shoulder Front-Back", 0f, "Fingers", 0.9f);
        // fırlatma sonrası takip: kollar ileri-aşağı uzanmış, parmaklar açık
        public static Dictionary<string, float> PoseFollow = Pz("Arm Down-Up", -0.3f, "Arm Front-Back", -0.85f, "Arm Twist In-Out", 0.1f, "Forearm Stretch", 0.95f, "Forearm Twist In-Out", 0.2f, "Hand Down-Up", -0.35f, "Hand In-Out", 0f, "Shoulder Down-Up", 0f, "Shoulder Front-Back", 0.3f, "Fingers", 1f);
        // ---- tek elle taşıma / fırlatma (sağ el); sol kol rahat
        public static Dictionary<string, float> PoseHang = Pz("Arm Down-Up", -0.78f, "Arm Front-Back", -0.12f, "Arm Twist In-Out", 0.1f, "Forearm Stretch", 0.85f, "Forearm Twist In-Out", 0.2f, "Hand Down-Up", 0f, "Hand In-Out", 0f, "Shoulder Down-Up", 0f, "Shoulder Front-Back", 0f, "Fingers", -0.45f);
        public static Dictionary<string, float> PoseLeftRelax = Pz("Arm Down-Up", -0.68f, "Arm Front-Back", -0.15f, "Arm Twist In-Out", 0f, "Forearm Stretch", 0.75f, "Forearm Twist In-Out", 0.2f, "Hand Down-Up", 0f, "Hand In-Out", 0f, "Shoulder Down-Up", 0f, "Shoulder Front-Back", 0f, "Fingers", 0.1f);
        public static Dictionary<string, float> PoseLeftAim = Pz("Arm Down-Up", -0.15f, "Arm Front-Back", -0.75f, "Arm Twist In-Out", 0.2f, "Forearm Stretch", 0.75f, "Forearm Twist In-Out", 0f, "Hand Down-Up", 0f, "Hand In-Out", 0f, "Shoulder Down-Up", 0.1f, "Shoulder Front-Back", 0f, "Fingers", 0.6f);
        public static Dictionary<string, float> PoseRightCock = Pz("Arm Down-Up", 0.55f, "Arm Front-Back", 0.35f, "Arm Twist In-Out", 0.3f, "Forearm Stretch", -0.35f, "Forearm Twist In-Out", 0.3f, "Hand Down-Up", -0.4f, "Hand In-Out", 0f, "Shoulder Down-Up", 0.6f, "Shoulder Front-Back", -0.3f, "Fingers", -0.4f);
        public static Dictionary<string, float> PoseRightRelease = Pz("Arm Down-Up", 0.15f, "Arm Front-Back", -1f, "Arm Twist In-Out", 0.1f, "Forearm Stretch", 1f, "Forearm Twist In-Out", 0.3f, "Hand Down-Up", 0.4f, "Hand In-Out", 0f, "Shoulder Down-Up", 0.25f, "Shoulder Front-Back", 0.4f, "Fingers", 0.9f);
        public static Dictionary<string, float> PoseRightFollow = Pz("Arm Down-Up", -0.45f, "Arm Front-Back", -0.75f, "Arm Twist In-Out", -0.2f, "Forearm Stretch", 0.95f, "Forearm Twist In-Out", 0.2f, "Hand Down-Up", -0.4f, "Hand In-Out", 0f, "Shoulder Down-Up", 0f, "Shoulder Front-Back", 0.45f, "Fingers", 1f);
        public static Dictionary<string, float> PoseRightReach = Pz("Arm Down-Up", -0.45f, "Arm Front-Back", -0.95f, "Arm Twist In-Out", 0.2f, "Forearm Stretch", 1f, "Forearm Twist In-Out", 0.3f, "Hand Down-Up", 0.1f, "Hand In-Out", 0.1f, "Shoulder Down-Up", 0f, "Shoulder Front-Back", 0.2f, "Fingers", 0.9f);
        public static Dictionary<string, float> PoseRest = Pz("Arm Down-Up", -0.62f, "Arm Front-Back", -0.3f, "Arm Twist In-Out", 0f, "Forearm Stretch", 0.6f, "Forearm Twist In-Out", 0.2f, "Hand Down-Up", 0f, "Hand In-Out", 0f, "Shoulder Down-Up", 0f, "Shoulder Front-Back", 0f, "Fingers", 0.2f);
        public static Dictionary<string, float> PoseReach = Pz("Arm Down-Up", -0.4f, "Arm Front-Back", -1f, "Arm Twist In-Out", 0.2f, "Forearm Stretch", 1f, "Forearm Twist In-Out", 0.3f, "Hand Down-Up", 0.2f, "Hand In-Out", 0.2f, "Shoulder Down-Up", 0f, "Shoulder Front-Back", 0f, "Fingers", 0.9f);
        public static Dictionary<string, float> PosePush = Pz("Arm Down-Up", -0.15f, "Arm Front-Back", -1f, "Arm Twist In-Out", 0f, "Forearm Stretch", 1f, "Forearm Twist In-Out", -0.2f, "Hand Down-Up", -0.9f, "Hand In-Out", 0f, "Shoulder Down-Up", 0.1f, "Shoulder Front-Back", 0f, "Fingers", 1f);
        public static Dictionary<string, float> PosePushPrep = Pz("Arm Down-Up", -0.45f, "Arm Front-Back", -0.55f, "Arm Twist In-Out", 0f, "Forearm Stretch", 0.1f, "Forearm Twist In-Out", -0.2f, "Hand Down-Up", -0.6f, "Hand In-Out", 0f, "Shoulder Down-Up", 0f, "Shoulder Front-Back", 0f, "Fingers", 0.8f);
        // FPS hazır duruş: kollar önde, eller ekranın altında görünür
        // (kamera önünde ölçülerek ayarlandı: eller ekranın alt köşelerinde, avuçlar içe dönük)
        public static Dictionary<string, float> PoseFPReady = Pz("Arm Down-Up", -0.35f, "Arm Front-Back", -0.4f, "Arm Twist In-Out", 0.9f, "Forearm Stretch", 0.45f, "Forearm Twist In-Out", 0.15f, "Hand Down-Up", -0.05f, "Hand In-Out", 0.15f, "Shoulder Down-Up", 0f, "Shoulder Front-Back", 0f, "Fingers", -0.55f);
        public static Dictionary<string, float> PoseFPCarry = Pz("Arm Down-Up", -0.25f, "Arm Front-Back", -0.55f, "Arm Twist In-Out", 0.7f, "Forearm Stretch", 0.6f, "Forearm Twist In-Out", 0.3f, "Hand Down-Up", 0.1f, "Hand In-Out", 0.2f, "Shoulder Down-Up", 0f, "Shoulder Front-Back", 0f, "Fingers", 0.2f);
        public static Dictionary<string, float> PoseFPWindup = Pz("Arm Down-Up", -0.1f, "Arm Front-Back", -0.3f, "Arm Twist In-Out", 0.7f, "Forearm Stretch", 0.1f, "Forearm Twist In-Out", 0.3f, "Hand Down-Up", -0.4f, "Hand In-Out", 0.2f, "Shoulder Down-Up", 0.3f, "Shoulder Front-Back", 0f, "Fingers", -0.2f);
        public static Dictionary<string, float> PoseFPRelease = Pz("Arm Down-Up", -0.2f, "Arm Front-Back", -0.95f, "Arm Twist In-Out", 0.5f, "Forearm Stretch", 1f, "Forearm Twist In-Out", 0.2f, "Hand Down-Up", 0.3f, "Hand In-Out", 0f, "Shoulder Down-Up", 0.1f, "Shoulder Front-Back", 0f, "Fingers", 0.9f);
        public static Dictionary<string, float> PoseFPReach = Pz("Arm Down-Up", -0.3f, "Arm Front-Back", -0.85f, "Arm Twist In-Out", 0.6f, "Forearm Stretch", 0.9f, "Forearm Twist In-Out", 0.2f, "Hand Down-Up", 0.2f, "Hand In-Out", 0.2f, "Shoulder Down-Up", 0f, "Shoulder Front-Back", 0f, "Fingers", 0.8f);
        public static Dictionary<string, float> PoseFPPush = Pz("Arm Down-Up", -0.2f, "Arm Front-Back", -1f, "Arm Twist In-Out", 0.6f, "Forearm Stretch", 1f, "Forearm Twist In-Out", -0.4f, "Hand Down-Up", -0.9f, "Hand In-Out", 0.1f, "Shoulder Down-Up", 0.1f, "Shoulder Front-Back", 0f, "Fingers", 1f);

        static readonly string[] FingerNames = { "Thumb", "Index", "Middle", "Ring", "Little" };

        /// <summary>Anahtar karelerden humanoid kol klibi üretir.</summary>
        public static AnimationClip MuscleClip(string name, bool loop, params (float t, Dictionary<string, float> pose)[] keys)
        {
            string path = $"{R}/Animation/{name}.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null) { clip = new AnimationClip { name = name }; AssetDatabase.CreateAsset(clip, path); }
            clip.ClearCurves();
            var names = new HashSet<string>();
            foreach (var k in keys) foreach (var n in k.pose.Keys) names.Add(Bare(n));
            foreach (var side in new[] { "Left", "Right" })
                foreach (var n in names)
                {
                    string pre = side == "Left" ? "L:" : "R:";
                    if (n == "Fingers")
                    {
                        foreach (var f in FingerNames)
                            for (int j = 1; j <= 3; j++)
                                SetCurve(clip, $"{side}Hand.{f}.{j} Stretched", keys, n, f == "Thumb" ? 0.5f : 1f, pre);
                        continue;
                    }
                    SetCurve(clip, side + " " + n, keys, n, 1f, pre);
                }
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        static void SetCurve(AnimationClip clip, string prop, (float t, Dictionary<string, float> pose)[] keys, string key, float mul, string sidePrefix = "")
        {
            var curve = new AnimationCurve();
            float last = 0;
            foreach (var k in keys)
            {
                if (k.pose.TryGetValue(sidePrefix + key, out var sv)) last = sv * mul;
                else if (k.pose.TryGetValue(key, out var v)) last = v * mul;
                curve.AddKey(new Keyframe(k.t, last));
            }
            for (int i = 0; i < curve.length; i++) AnimationUtility.SetKeyBroken(curve, i, false);
            for (int i = 0; i < curve.length; i++) curve.SmoothTangents(i, 0f);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), prop), curve);
        }

        static AnimationClip Clip(string path, string clipName = null)
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is AnimationClip c && !c.name.StartsWith("__preview") && (clipName == null || c.name == clipName)) return c;
            Debug.LogWarning("[BF] Klip bulunamadı: " + path + " " + clipName);
            return null;
        }

        /// <summary>ToonPeople düşme kliplerinde kök yüksekliği poza gömülü olsun (yoksa karakter havada yatar).</summary>
        static void FixReactionImports()
        {
            foreach (var file in new[] { "M_fallbackwards.FBX", "M_hitbackwards.FBX", "M_clap.FBX" })
            {
                string p = "Assets/ToonPeople/animations/" + file;
                var imp = AssetImporter.GetAtPath(p) as ModelImporter;
                if (imp == null) continue;
                var clips = imp.clipAnimations;
                if (clips == null || clips.Length == 0) clips = imp.defaultClipAnimations;
                bool changed = false;
                foreach (var c in clips)
                {
                    if (!c.lockRootHeightY || !c.keepOriginalPositionY || !c.lockRootRotation || !c.lockRootPositionXZ)
                    {
                        c.lockRootHeightY = true; c.keepOriginalPositionY = true;
                        c.lockRootRotation = true; c.keepOriginalOrientation = true;
                        c.lockRootPositionXZ = true; c.keepOriginalPositionXZ = true;
                        changed = true;
                    }
                }
                if (changed) { imp.clipAnimations = clips; imp.SaveAndReimport(); }
            }
        }

        // =====================================================================
        // 3) Animatörler
        // =====================================================================
        [MenuItem("BlackFriday/3 - Animator")]
        public static void BuildAnimator()
        {
            EnsureFolders();
            FixReactionImports();
            const string syn = "Assets/Synty/AnimationBaseLocomotion/Animations/Polygon/Masculine/";
            const string tp = "Assets/ToonPeople/animations/";
            var idle = Clip(syn + "Idle/A_Idle_Standing_Masc.fbx");
            var sprint = Clip(syn + "Locomotion/Sprint/A_Sprint_F_Masc.fbx");
            var stagger = Clip(tp + "M_hitbackwards.FBX");
            var fallIn = Clip(tp + "M_fallbackwards.FBX", "TPM_fallbackwardsIN");
            var fallOut = Clip(tp + "M_fallbackwards.FBX", "TPM_fallbackwardsOUT");
            var clap = Clip(tp + "M_clap.FBX");
            var jumpIdle = Clip(syn + "InAir/A_Jump_Idle_Masc.fbx", "A_Jump_Idle_Masc");
            var jumpRun = Clip(syn + "InAir/A_Jump_Running_Masc.fbx", "A_Jump_Running_Masc");
            var inAir = Clip(syn + "InAir/A_InAir_FallShort_Masc.fbx");
            var land = Clip(syn + "InAir/A_Land_IdleSoft_Masc.fbx");

            string[] dirs = { "F", "FL", "L", "BL", "B", "BR", "R", "FR" };
            Vector2[] dvec = { new Vector2(0, 1), new Vector2(-0.7071f, 0.7071f), new Vector2(-1, 0), new Vector2(-0.7071f, -0.7071f),
                               new Vector2(0, -1), new Vector2(0.7071f, -0.7071f), new Vector2(1, 0), new Vector2(0.7071f, 0.7071f) };
            System.Func<string, string, AnimationClip> loc = (kind, d) =>
            {
                string file;
                switch (d)
                {
                    case "F": file = $"A_{kind}_F_Masc"; break;
                    case "FL": file = $"A_{kind}_FwdStrafeFL_Masc"; break;
                    case "FR": file = $"A_{kind}_FwdStrafeFR_Masc"; break;
                    case "L": file = $"A_{kind}_FwdStrafeL_Masc"; break;
                    case "R": file = $"A_{kind}_FwdStrafeR_Masc"; break;
                    case "B": file = $"A_{kind}_BckStrafeB_Masc"; break;
                    case "BL": file = $"A_{kind}_BckStrafeBL_Masc"; break;
                    default: file = $"A_{kind}_BckStrafeBR_Masc"; break;
                }
                return Clip($"{syn}Locomotion/{kind}/{file}.fbx");
            };

            // kol klipleri (TPS aksiyon katmanı + FPS el modeli ortak)
            var carry = MuscleClip("BF_CarryPose", true, (0f, Arms(PoseHang, PoseLeftRelax)), (1f, Arms(PoseHang, PoseLeftRelax)));
            var throwC = MuscleClip("BF_Throw", false,
                (0f, Arms(PoseRightCock, PoseLeftAim)), (0.09f, Arms(PoseRightRelease, PoseLeftRelax)),
                (0.26f, Arms(PoseRightFollow, PoseLeftRelax)), (0.6f, Arms(PoseRest, PoseRest)));
            var grab = MuscleClip("BF_Grab", false, (0f, Arms(PoseRest, PoseRest)), (0.14f, Arms(PoseRightReach, PoseLeftRelax)), (0.34f, Arms(PoseHang, PoseLeftRelax)));
            var shove = MuscleClip("BF_Shove", false, (0f, PosePushPrep), (0.1f, PosePush), (0.2f, PosePush), (0.42f, PoseRest));

            // avatar mask: kollar + parmaklar
            string maskPath = R + "/Animation/BF_ArmsMask.mask";
            AssetDatabase.DeleteAsset(maskPath);
            var mask = new AvatarMask();
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++) mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
            AssetDatabase.CreateAsset(mask, maskPath);
            // taşıma katmanı: sadece sağ kol (sol kol yürüyüş/koşu sallanmasında kalır)
            string rMaskPath = R + "/Animation/BF_RightArmMask.mask";
            AssetDatabase.DeleteAsset(rMaskPath);
            var rmask = new AvatarMask();
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++) rmask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
            rmask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
            rmask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
            AssetDatabase.CreateAsset(rmask, rMaskPath);

            string path = R + "/Animation/BF_Humanoid.controller";
            AssetDatabase.DeleteAsset(path);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            foreach (var p in new[] { "Speed", "MoveX", "MoveZ", "CycleOffset" }) ctrl.AddParameter(p, AnimatorControllerParameterType.Float);
            ctrl.AddParameter("VertSpeed", AnimatorControllerParameterType.Float);
            foreach (var p in new[] { "Stunned", "Celebrate" }) ctrl.AddParameter(p, AnimatorControllerParameterType.Bool);
            ctrl.AddParameter(new AnimatorControllerParameter { name = "Grounded", type = AnimatorControllerParameterType.Bool, defaultBool = true });
            foreach (var p in new[] { "Hit", "Pushed", "Throw", "Grab", "Shove", "Jump" }) ctrl.AddParameter(p, AnimatorControllerParameterType.Trigger);

            var sm = ctrl.layers[0].stateMachine;
            var locoState = ctrl.CreateBlendTreeInController("Locomotion", out var tree, 0);
            tree.blendType = BlendTreeType.FreeformDirectional2D;
            tree.blendParameter = "MoveX";
            tree.blendParameterY = "MoveZ";
            tree.AddChild(idle, Vector2.zero);
            for (int i = 0; i < dirs.Length; i++)
            {
                var w = loc("Walk", dirs[i]); if (w != null) tree.AddChild(w, dvec[i] * CharacterVisualSpeeds.Walk);
                var r = loc("Run", dirs[i]); if (r != null) tree.AddChild(r, dvec[i] * CharacterVisualSpeeds.Run);
            }
            tree.AddChild(sprint, new Vector2(0, CharacterVisualSpeeds.Sprint));
            locoState.cycleOffsetParameter = "CycleOffset";
            locoState.cycleOffsetParameterActive = true;
            sm.defaultState = locoState;

            var sFallIn = sm.AddState("FallIn"); sFallIn.motion = fallIn;
            var sFallOut = sm.AddState("FallOut"); sFallOut.motion = fallOut;
            var sStagger = sm.AddState("Stagger"); sStagger.motion = stagger;
            var sCele = sm.AddState("Celebrate"); sCele.motion = clap;

            var t = sm.AddAnyStateTransition(sFallIn);
            t.AddCondition(AnimatorConditionMode.If, 0, "Hit"); t.hasExitTime = false; t.duration = 0.05f; t.canTransitionToSelf = false;
            t = sFallIn.AddTransition(sFallOut); t.hasExitTime = true; t.exitTime = 0.95f; t.duration = 0.05f;
            t = sFallOut.AddTransition(locoState); t.hasExitTime = true; t.exitTime = 0.92f; t.duration = 0.2f;
            t = sm.AddAnyStateTransition(sStagger);
            t.AddCondition(AnimatorConditionMode.If, 0, "Pushed"); t.hasExitTime = false; t.duration = 0.05f; t.canTransitionToSelf = false;
            t = sStagger.AddTransition(locoState); t.hasExitTime = true; t.exitTime = 0.85f; t.duration = 0.15f;
            t = locoState.AddTransition(sCele); t.hasExitTime = false; t.duration = 0.25f;
            t.AddCondition(AnimatorConditionMode.If, 0, "Celebrate");
            t = sCele.AddTransition(locoState); t.hasExitTime = false; t.duration = 0.25f;
            t.AddCondition(AnimatorConditionMode.IfNot, 0, "Celebrate");

            // ---- zıplama
            var sJumpIdle = sm.AddState("JumpIdle"); sJumpIdle.motion = jumpIdle;
            var sJumpRun = sm.AddState("JumpRun"); sJumpRun.motion = jumpRun;
            var sAir = sm.AddState("InAir"); sAir.motion = inAir;
            var sLand = sm.AddState("Land"); sLand.motion = land;
            t = sm.AddAnyStateTransition(sJumpIdle); t.hasExitTime = false; t.duration = 0.06f; t.canTransitionToSelf = false;
            t.AddCondition(AnimatorConditionMode.If, 0, "Jump"); t.AddCondition(AnimatorConditionMode.Less, 1.5f, "Speed"); t.AddCondition(AnimatorConditionMode.IfNot, 0, "Stunned");
            t = sm.AddAnyStateTransition(sJumpRun); t.hasExitTime = false; t.duration = 0.06f; t.canTransitionToSelf = false;
            t.AddCondition(AnimatorConditionMode.If, 0, "Jump"); t.AddCondition(AnimatorConditionMode.Greater, 1.5f, "Speed"); t.AddCondition(AnimatorConditionMode.IfNot, 0, "Stunned");
            foreach (var js in new[] { sJumpIdle, sJumpRun })
            {
                t = js.AddTransition(sAir); t.hasExitTime = true; t.exitTime = 0.85f; t.duration = 0.15f;
                t = js.AddTransition(sLand); t.hasExitTime = false; t.duration = 0.08f;
                t.AddCondition(AnimatorConditionMode.If, 0, "Grounded"); t.AddCondition(AnimatorConditionMode.Less, -0.5f, "VertSpeed");
            }
            t = locoState.AddTransition(sAir); t.hasExitTime = false; t.duration = 0.2f;
            t.AddCondition(AnimatorConditionMode.IfNot, 0, "Grounded"); t.AddCondition(AnimatorConditionMode.Less, -2.5f, "VertSpeed");
            t = sAir.AddTransition(locoState); t.hasExitTime = false; t.duration = 0.12f;
            t.AddCondition(AnimatorConditionMode.If, 0, "Grounded"); t.AddCondition(AnimatorConditionMode.Greater, 2f, "Speed");
            t = sAir.AddTransition(sLand); t.hasExitTime = false; t.duration = 0.06f;
            t.AddCondition(AnimatorConditionMode.If, 0, "Grounded"); t.AddCondition(AnimatorConditionMode.Less, 2f, "Speed");
            t = sLand.AddTransition(locoState); t.hasExitTime = true; t.exitTime = 0.55f; t.duration = 0.15f;
            t = sLand.AddTransition(locoState); t.hasExitTime = false; t.duration = 0.1f;
            t.AddCondition(AnimatorConditionMode.Greater, 2f, "Speed");

            ctrl.AddLayer("Carry");
            ctrl.AddLayer("Action");
            var layers = ctrl.layers;
            layers[1].avatarMask = rmask; layers[1].defaultWeight = 0f; layers[1].blendingMode = AnimatorLayerBlendingMode.Override;
            layers[1].iKPass = true; // eller ürünü IK ile kavrar (HandIK)
            layers[2].avatarMask = mask; layers[2].defaultWeight = 1f; layers[2].blendingMode = AnimatorLayerBlendingMode.Override;
            ctrl.layers = layers;

            var carrySm = ctrl.layers[1].stateMachine;
            var sCarry = carrySm.AddState("CarryPose"); sCarry.motion = carry;
            carrySm.defaultState = sCarry;

            var actSm = ctrl.layers[2].stateMachine;
            var sEmpty = actSm.AddState("Empty");
            actSm.defaultState = sEmpty;
            foreach (var (trig, clip) in new[] { ("Throw", throwC), ("Grab", grab), ("Shove", shove) })
            {
                var st = actSm.AddState(trig); st.motion = clip;
                var at = actSm.AddAnyStateTransition(st);
                at.AddCondition(AnimatorConditionMode.If, 0, trig); at.hasExitTime = false; at.duration = 0.04f; at.canTransitionToSelf = false;
                var back = st.AddTransition(sEmpty); back.hasExitTime = true; back.exitTime = 0.95f; back.duration = 0.12f;
            }
            EditorUtility.SetDirty(ctrl);

            BuildFPArmsController(throwC, grab, shove);
            AssetDatabase.SaveAssets();
            Debug.Log("[BF] Animatörler kuruldu.");
        }

        static void BuildFPArmsController(AnimationClip throwC, AnimationClip grab, AnimationClip shove)
        {
            var breathe = Lerp(PoseFPReady, Pz("Arm Down-Up", -0.32f, "Forearm Stretch", 0.42f, "Fingers", -0.48f), 1f);
            var ready = MuscleClip("BF_FP_Ready", true, (0f, PoseFPReady), (1.6f, breathe), (3.2f, PoseFPReady));
            // tek elle: sağ el ürünü tutar, sol kol aşağıda (görüş alanı dışında)
            var leftDown = Pz("Arm Down-Up", -0.95f, "Arm Front-Back", 0.1f, "Arm Twist In-Out", 0f, "Forearm Stretch", 0.35f, "Forearm Twist In-Out", 0f, "Hand Down-Up", 0f, "Hand In-Out", 0f, "Shoulder Down-Up", -0.2f, "Shoulder Front-Back", 0f, "Fingers", -0.4f);
            var fpCarry1 = Arms(PoseFPCarry, leftDown);
            var carry = MuscleClip("BF_FP_Carry", true, (0f, fpCarry1), (1f, fpCarry1));
            // FPS'te fırlatma: hazırlık IK ile (ürün geri-yukarı); klip bırakma anında başlar: sağ kol ileri savrulur
            // bırakış kolu ekranın sağ altında kalır (tam ileri uzanan kol ekranı ortadan kapatıyordu)
            var fpRel = Lerp(PoseFPCarry, PoseFPRelease, 0.45f);
            var fpThrow = MuscleClip("BF_FP_Throw", false, (0f, Arms(PoseFPWindup, leftDown)), (0.08f, Arms(fpRel, leftDown)),
                (0.26f, Arms(fpRel, leftDown)), (0.6f, Arms(PoseFPReady, leftDown)));
            var fpGrab = MuscleClip("BF_FP_Grab", false, (0f, PoseFPReady), (0.14f, Arms(PoseFPReach, PoseFPReady)), (0.34f, fpCarry1));
            var fpShove = MuscleClip("BF_FP_Shove", false, (0f, PoseFPReady), (0.09f, PoseFPPush), (0.2f, PoseFPPush), (0.45f, PoseFPReady));

            string path = R + "/Animation/BF_FPArms.controller";
            AssetDatabase.DeleteAsset(path);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            ctrl.AddParameter("Carry", AnimatorControllerParameterType.Bool);
            foreach (var p in new[] { "Throw", "Grab", "Shove" }) ctrl.AddParameter(p, AnimatorControllerParameterType.Trigger);
            var fl = ctrl.layers; fl[0].iKPass = true; ctrl.layers = fl;
            var sm = ctrl.layers[0].stateMachine;
            var sReady = sm.AddState("Ready"); sReady.motion = ready;
            var sCarry = sm.AddState("Carry"); sCarry.motion = carry;
            sm.defaultState = sReady;
            var t = sReady.AddTransition(sCarry); t.hasExitTime = false; t.duration = 0.18f; t.AddCondition(AnimatorConditionMode.If, 0, "Carry");
            t = sCarry.AddTransition(sReady); t.hasExitTime = false; t.duration = 0.18f; t.AddCondition(AnimatorConditionMode.IfNot, 0, "Carry");
            foreach (var (trig, clip) in new[] { ("Throw", fpThrow), ("Grab", fpGrab), ("Shove", fpShove) })
            {
                var st = sm.AddState(trig); st.motion = clip;
                var at = sm.AddAnyStateTransition(st);
                at.AddCondition(AnimatorConditionMode.If, 0, trig); at.hasExitTime = false; at.duration = 0.04f; at.canTransitionToSelf = false;
                var back = st.AddTransition(sReady); back.hasExitTime = true; back.exitTime = 0.95f; back.duration = 0.12f;
            }
            EditorUtility.SetDirty(ctrl);
        }

        static Dictionary<string, float> Lerp(Dictionary<string, float> a, Dictionary<string, float> b, float t)
        {
            var d = new Dictionary<string, float>(a);
            foreach (var kv in b) d[kv.Key] = a.TryGetValue(kv.Key, out var av) ? Mathf.Lerp(av, kv.Value, t) : kv.Value;
            return d;
        }

        static class CharacterVisualSpeeds
        {
            public const float Walk = CharacterVisual.WalkClipSpeed;
            public const float Run = CharacterVisual.RunClipSpeed;
            public const float Sprint = CharacterVisual.SprintClipSpeed;
        }
    }
}
