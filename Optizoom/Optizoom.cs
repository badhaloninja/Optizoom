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
        public override string Version => "1.1.0";
        public override string Link => "https://github.com/badhaloninja/Optizoom";


        [AutoRegisterConfigKey]
        public static readonly ModConfigurationKey<bool> Enabled =
            new ModConfigurationKey<bool>("Enabled", "Enable Optizoom", () => true);
        [AutoRegisterConfigKey]
        public static readonly ModConfigurationKey<Key> ZoomKey =
            new ModConfigurationKey<Key>("keyBind", "Zoom Key", () => Key.Tab);
        [AutoRegisterConfigKey]
        public static readonly ModConfigurationKey<float> ZoomFOV =
            new ModConfigurationKey<float>("zoomFOV", "Zoom FOV", () => 7f);



        [AutoRegisterConfigKey]
        public static readonly ModConfigurationKey<bool> LerpZoom =
            new ModConfigurationKey<bool>("lerpZoom", "Lerp Zoom", () => true);
        [AutoRegisterConfigKey]
        public static readonly ModConfigurationKey<float> ZoomSpeed =
            new ModConfigurationKey<float>("zoomSpeed", "Zoom Speed", () => 50f);


        [AutoRegisterConfigKey]
        public static readonly ModConfigurationKey<bool> overlaySpyglass =
            new ModConfigurationKey<bool>("overlaySpyglass", "Overlay Spyglass", () => true);

        private static ModConfiguration config;

        private static Slot spyglassOverlay;


        public override void OnEngineInit()
        {
            config = GetConfiguration();

            Harmony harmony = new Harmony("me.badhaloninja.Optizoom");
            harmony.PatchAll();

/*
            Engine.Current.RunPostInit(() => // Userspace does not exist at this point
            {
                Slot overlayRoot = Userspace.Current.World.GetGloballyRegisteredComponent<OverlayManager>().OverlayRoot;
                spyglassOverlay = overlayRoot.AddSlot("SpyglassOverlay");
                var texture = spyglassOverlay.AttachTexture(NeosAssets.Graphics.Logos.NeosAssets_LOGO_2021_LogoMark_FullColor, wrapMode: TextureWrapMode.Clamp);
                var unlit = spyglassOverlay.AttachComponent<UnlitMaterial>();
                unlit.Texture.TrySet(texture);
                unlit.BlendMode.Value = BlendMode.Alpha;
                spyglassOverlay.AttachQuad(float2.One, unlit, false);
            });*/
        }


        [HarmonyPatch(typeof(Userspace))]
        class SpyglassUserspacePatch
        {
            [HarmonyPostfix]
            [HarmonyPatch("OnAttach")]
            public static void Postfix(Userspace __instance)
            {
                Slot overlayRoot = __instance.World.GetGloballyRegisteredComponent<OverlayManager>().OverlayRoot;
                spyglassOverlay = overlayRoot.AddSlot("SpyglassOverlay");
                spyglassOverlay.LocalPosition = float3.Forward * 0.1f;
                spyglassOverlay.ActiveSelf = false;


                Uri texUri = new Uri("neosdb:///55b0aea6dcdce645b3f01ff83877b88f16402155f4ba54bced02aa6bdae528b9.png");
                var texture = spyglassOverlay.AttachTexture(texUri, wrapMode: TextureWrapMode.Clamp);
                texture.FilterMode.Value = TextureFilterMode.Point;

                var unlit = spyglassOverlay.AttachComponent<UnlitMaterial>();
                unlit.Texture.TrySet(texture);
                unlit.BlendMode.Value = BlendMode.Alpha;

                spyglassOverlay.AttachQuad(float2.One * 1.12f, unlit, false);


                var frameUnlit = spyglassOverlay.AttachComponent<UnlitMaterial>();
                frameUnlit.TintColor.Value = color.Black;

                var frame = spyglassOverlay.AttachMesh<FrameMesh>(frameUnlit);
                frame.ContentSize.Value = float2.One * 1.12f;
                frame.Thickness.Value = 5f;




            }

            [HarmonyPostfix]
            [HarmonyPatch("OnCommonUpdate")]
            public static void update(Userspace __instance)
            {
                var flag = config.GetValue(Enabled) && config.GetValue(overlaySpyglass)
                        && !__instance.LocalUser.HasActiveFocus() // Not focused in any field
                        && !Userspace.HasFocus // Not focused in userspace field
                        && __instance.InputInterface.GetKey(config.GetValue(ZoomKey)); // Key pressed


                if (flag != spyglassOverlay.ActiveSelf)
                {
                    spyglassOverlay.ActiveSelf = flag;
                }
            }
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
                if (config == null) return;

                


                var flag =  config.GetValue(Enabled)
                        && !__instance.LocalUser.HasActiveFocus() // Not focused in any field
                        && !Userspace.HasFocus // Not focused in userspace field
                        && __instance.Engine.WorldManager.FocusedWorld == __instance.World // Focused in the same world as the UserRoot
                        && __instance.InputInterface.GetKey(config.GetValue(ZoomKey)); // Key pressed



                float target = flag ? Settings.ReadValue("Settings.Graphics.DesktopFOV", 60f) - config.GetValue(ZoomFOV) : 0f;//__result;

                if (config.GetValue(LerpZoom))
                {
                    lerp.currentLerp = MathX.SmoothDamp(lerp.currentLerp, target, ref lerp.lerpVelocity, config.GetValue(ZoomSpeed), 179f, __instance.Time.Delta); // Funny lerp
                    __result -= lerp.currentLerp;
                } else
                {
                    __result -= target;
                }

                
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