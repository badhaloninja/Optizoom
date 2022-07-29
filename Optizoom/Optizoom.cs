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
        public override string Version => "1.2.0";
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
        public static readonly ModConfigurationKey<bool> ToggleZoom =
            new ModConfigurationKey<bool>("toggleZoom", "Toggle Zoom", () => false);


        [AutoRegisterConfigKey]
        public static readonly ModConfigurationKey<bool> LerpZoom =
            new ModConfigurationKey<bool>("lerpZoom", "Lerp Zoom", () => true);
        [AutoRegisterConfigKey]
        public static readonly ModConfigurationKey<float> ZoomSpeed =
            new ModConfigurationKey<float>("zoomSpeed", "Zoom Speed", () => 50f);


        [AutoRegisterConfigKey]
        public static readonly ModConfigurationKey<bool> enableOverlay =
            new ModConfigurationKey<bool>("enableOverlay", "Enable Overlay", () => true);
        [AutoRegisterConfigKey]
        public static readonly ModConfigurationKey<float2> overlaySize =
            new ModConfigurationKey<float2>("overlaySize", "Overlay Size", () => float2.One * 1.12f);
        [AutoRegisterConfigKey]
        public static readonly ModConfigurationKey<Uri> overlayUri =
            new ModConfigurationKey<Uri>("overlayUri", "Overlay Uri", () => new Uri("neosdb:///55b0aea6dcdce645b3f01ff83877b88f16402155f4ba54bced02aa6bdae528b9.png"));
        [AutoRegisterConfigKey]
        public static readonly ModConfigurationKey<bool> overlayBg =
            new ModConfigurationKey<bool>("overlayBg", "Enable Overlay Background", () => true);
        [AutoRegisterConfigKey]
        public static readonly ModConfigurationKey<color> overlayBgColor =
            new ModConfigurationKey<color>("overlayBgColor", "Overlay Background Color", () => color.Black);

        private static ModConfiguration config;

        private static Slot overlayVisual;

        private static bool toggleState = false;

        public override void OnEngineInit()
        {
            config = GetConfiguration();

            Harmony harmony = new Harmony("me.badhaloninja.Optizoom");
            harmony.PatchAll();

            config.OnThisConfigurationChanged += ConfigChanged;

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


        
        private void ConfigChanged(ConfigurationChangedEvent @event)
        {
            if (@event.Key == overlaySize)
            {
                TryWriteDynamicValue(overlayVisual, "OverlayVisual/overlaySize", config.GetValue(overlaySize));
                return;
            }
            if (@event.Key == overlayUri)
            {
                TryWriteDynamicValue(overlayVisual, "OverlayVisual/overlayUri", config.GetValue(overlayUri));
                return;
            }
            if (@event.Key == overlayBg)
            {
                TryWriteDynamicValue(overlayVisual, "OverlayVisual/overlayBg", config.GetValue(overlayBg));
                return;
            }
            if (@event.Key == overlayBgColor)
            {
                TryWriteDynamicValue(overlayVisual, "OverlayVisual/overlayBgColor", config.GetValue(overlayBgColor));
                return;
            }
            if(@event.Key == ToggleZoom)
            {
                toggleState = false;
            }
        }

        [HarmonyPatch(typeof(Userspace))]
        class SpyglassUserspacePatch
        {
            [HarmonyPostfix]
            [HarmonyPatch("OnAttach")]
            public static void Postfix(Userspace __instance)
            {
                Slot overlayRoot = __instance.World.GetGloballyRegisteredComponent<OverlayManager>().OverlayRoot;
                overlayVisual = overlayRoot.AddSlot("OverlayVisual");
                overlayVisual.LocalPosition = float3.Forward * 0.1f;
                overlayVisual.ActiveSelf = false;

                overlayVisual.AttachComponent<DynamicVariableSpace>().SpaceName.Value = "OverlayVisual";

                Uri texUri = config.GetValue(overlayUri);//new Uri("");
                var texture = overlayVisual.AttachTexture(texUri, wrapMode: TextureWrapMode.Clamp);
                texture.FilterMode.Value = TextureFilterMode.Point;
                // Overlay Texture
                texture.URL.SyncWithVariable("overlayUri");
                
                var unlit = overlayVisual.AttachComponent<UnlitMaterial>();
                unlit.Texture.TrySet(texture);
                unlit.BlendMode.Value = BlendMode.Alpha;

                var overlayQuad = overlayVisual.AttachQuad(config.GetValue(overlaySize), unlit, false);
                overlayQuad.Size.SyncWithVariable("overlaySize");

                var frameUnlit = overlayVisual.AttachComponent<UnlitMaterial>();
                frameUnlit.TintColor.Value = config.GetValue(overlayBgColor);
                // BGColor
                frameUnlit.TintColor.SyncWithVariable("overlayBgColor");


                var frame = overlayVisual.AttachComponent<FrameMesh>();
                
                var frameRenderer = overlayVisual.AttachMesh(frame, frameUnlit);
                frame.Thickness.Value = 5f;
                frame.ContentSize.DriveFrom(overlayQuad.Size);
                frameRenderer.EnabledField.SyncWithVariable("overlayBg");
            }

            [HarmonyPostfix]
            [HarmonyPatch("OnCommonUpdate")]
            public static void Update(Userspace __instance)
            {
                if (config.GetValue(ToggleZoom) && __instance.InputInterface.GetKeyDown(config.GetValue(ZoomKey)))
                {
                    toggleState = !toggleState;
                }
                
                var flag = config.GetValue(Enabled) && config.GetValue(enableOverlay)
                        && !__instance.LocalUser.HasActiveFocus() // Not focused in any field
                        && !Userspace.HasFocus // Not focused in userspace field
                        && (toggleState || __instance.InputInterface.GetKey(config.GetValue(ZoomKey))); // Key pressed

                

                if (flag != overlayVisual.ActiveSelf)
                {
                    overlayVisual.ActiveSelf = flag;
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
                        && (toggleState || __instance.InputInterface.GetKey(config.GetValue(ZoomKey))); // Key pressed

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


        public static bool TryWriteDynamicValue<T>(Slot root, string name, T value)
        {
            DynamicVariableHelper.ParsePath(name, out string spaceName, out string text);

            if (string.IsNullOrEmpty(text)) return false;

            DynamicVariableSpace dynamicVariableSpace = root.FindSpace(spaceName);
            if (dynamicVariableSpace == null) return false;
            return dynamicVariableSpace.TryWriteValue<T>(text, value);
        }
    }
}