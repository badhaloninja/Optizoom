using FrooxEngine;
using HarmonyLib;
using NeosModLoader;
using System;
using BaseX;
using System.Collections.Generic;

namespace Optizoom
{
    public class Optizoom : NeosMod
    {
        public override string Name => "Optizoom";
        public override string Author => "badhaloninja";
        public override string Version => "1.0.0";
        public override string Link => "https://github.com/badhaloninja/Optizoom";


        [AutoRegisterConfigKey]
        public static readonly ModConfigurationKey<bool> Enabled =
            new ModConfigurationKey<bool>("Enabled", "Enable Optizoom", () => true);
        [AutoRegisterConfigKey]
        public static readonly ModConfigurationKey<Key> ZoomKey =
            new ModConfigurationKey<Key>("keyBind", "Zoom Key", () => Key.Z);
        [AutoRegisterConfigKey]
        public static readonly ModConfigurationKey<float> ZoomFOV =
            new ModConfigurationKey<float>("zoomFOV", "Zoom FOV", () => 20f);

        private static ModConfiguration config;

        public override void OnEngineInit()
        {
            config = GetConfiguration();

            Harmony harmony = new Harmony("me.badhaloninja.Optizoom");
            harmony.PatchAll();
        }

        [HarmonyPatch(typeof(UserRoot), "get_DesktopFOV")]
        class Optizoom_Patch
        {
            static Dictionary<UserRoot, UserRootFOVLerps> lerps = new Dictionary<UserRoot, UserRootFOVLerps>();

            public static void Postfix(UserRoot __instance, ref float __result)
            {
                if (!lerps.TryGetValue(__instance, out UserRootFOVLerps lerp))
                {
                    lerp = new UserRootFOVLerps(); // Needs one per UserRoot or else userspace and focused world fights
                    lerps.Add(__instance, lerp);
                }
                var flag = config != null
                        && config.GetValue(Enabled)
                        && !__instance.LocalUser.HasActiveFocus() // Not focused in any field
                        && !Userspace.HasFocus // Not focused in userspace field
                        && __instance.Engine.WorldManager.FocusedWorld == __instance.World // Focused in the same world as the UserRoot
                        && __instance.InputInterface.GetKey(config.GetValue(ZoomKey)); // Key pressed

                float target = flag ? config.GetValue(ZoomFOV) : __result;

                lerp.currentLerp = MathX.SmoothDamp(lerp.currentLerp, target, ref lerp.lerpVelocity, 50f, 179f, __instance.Time.Delta); // Funny lerp
                __result = lerp.currentLerp;

                
                __result = MathX.FilterInvalid(__result, 60f); // fallback to 60 fov if invalid
                __result = MathX.Clamp(__result, 1f, 179f);

                //Msg($"{__instance.World.Name}: {__instance.ActiveUser.UserID} - {flag} | {lerp.lerpVelocity} | {__result}");
            }
        }

        class UserRootFOVLerps
        {
            public float currentLerp = 0f;
            public float lerpVelocity = 0f;
        }
    }
}