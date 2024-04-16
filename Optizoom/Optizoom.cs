using FrooxEngine;
using HarmonyLib;
using ResoniteModLoader;
using System;
using Elements.Core;
using System.Collections.Generic;

namespace Optizoom
{
    public class Optizoom : ResoniteMod
    {
        public override string Name => "Optizoom";
        public override string Author => "badhaloninja";
        public override string Version => "2.1.1";
        public override string Link => "https://github.com/badhaloninja/Optizoom";


        [AutoRegisterConfigKey]
        public static readonly ModConfigurationKey<bool> Enabled = new("Enabled", "Enable Optizoom", () => true);
        [AutoRegisterConfigKey]
        public static readonly ModConfigurationKey<Key> ZoomKey = new("keyBind", "Zoom Key", () => Key.Tab);
        [AutoRegisterConfigKey]
        public static readonly ModConfigurationKey<float> ZoomFOV = new("zoomFOV", "Zoom FOV", () => 7f);


        [AutoRegisterConfigKey]
        public static readonly ModConfigurationKey<bool> ToggleZoom = new("toggleZoom", "Toggle Zoom", () => false);


        [AutoRegisterConfigKey]
        public static readonly ModConfigurationKey<bool> LerpZoom = new("lerpZoom", "Lerp Zoom", () => true);
        [AutoRegisterConfigKey]
        public static readonly ModConfigurationKey<float> ZoomSpeed = new("zoomSpeed", "Zoom Speed", () => 50f);

        //ScrollZoomSpeed

        [AutoRegisterConfigKey]
        public static readonly ModConfigurationKey<bool> ScrollZoom = new("scrollZoom", "Zoom with scroll", () => true);
        [AutoRegisterConfigKey]
        public static readonly ModConfigurationKey<float> ScrollZoomSpeed = new("scrollZoomSpeed", "Scroll Zoom Speed", () => 50f);


        [AutoRegisterConfigKey]
        public static readonly ModConfigurationKey<bool> enableOverlay = new("enableOverlay", "Enable Overlay", () => true);
        [AutoRegisterConfigKey]
        public static readonly ModConfigurationKey<float2> overlaySize = new("overlaySize", "Overlay Size", () => float2.One * 1.12f);
        [AutoRegisterConfigKey]
        public static readonly ModConfigurationKey<Uri> overlayUri = new("overlayUri", "Overlay Uri", () => new Uri("resdb:///55b0aea6dcdce645b3f01ff83877b88f16402155f4ba54bced02aa6bdae528b9.png"));
        [AutoRegisterConfigKey]
        public static readonly ModConfigurationKey<bool> overlayBg = new("overlayBg", "Enable Overlay Background", () => true);
        [AutoRegisterConfigKey]
        public static readonly ModConfigurationKey<colorX> overlayBgColor = new("overlayBgColor", "Overlay Background Color", () => colorX.Black);


        [AutoRegisterConfigKey]
        public static readonly ModConfigurationKey<Uri> zoomInSound = new("zoomInSound", "Zoom In Sound URI, Null to disable", () => null);
        [AutoRegisterConfigKey]
        public static readonly ModConfigurationKey<Uri> zoomOutSound = new("zoomOutSound", "Zoom Out Sound URI, Null to disable", () => null);
        [AutoRegisterConfigKey]
        public static readonly ModConfigurationKey<float> zoomVolume = new("zoomVolume", "Zoom Volume", () => 1f, valueValidator: f=>f.IsBetween(0f,1f));
        // Add random range for volume and speed


        private static ModConfiguration config;

        private static Slot overlayVisual;

        private static bool toggleState = false;

        public override void OnEngineInit()
        {
            config = GetConfiguration();

            Harmony harmony = new("ninja.badhalo.Optizoom");
            harmony.PatchAll();

            config.OnThisConfigurationChanged += ConfigChanged;
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
            if (@event.Key == ToggleZoom)
            {
                toggleState = false;
            }
            if (@event.Key == zoomInSound)
            {
                TryWriteDynamicValue(overlayVisual, "OverlayVisual/zoomInSoundUri", config.GetValue(zoomInSound));
                return;
            }
            if (@event.Key == zoomOutSound)
            {
                TryWriteDynamicValue(overlayVisual, "OverlayVisual/zoomOutSoundUri", config.GetValue(zoomOutSound));
                return;
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


                var zoomIn = overlayVisual.AttachComponent<StaticAudioClip>();
                zoomIn.URL.Value = config.GetValue(zoomInSound);
                zoomIn.URL.SyncWithVariable("zoomInSoundUri");
                var zoomOut = overlayVisual.AttachComponent<StaticAudioClip>();
                zoomOut.URL.Value = config.GetValue(zoomOutSound);
                zoomOut.URL.SyncWithVariable("zoomOutSoundUri");
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

                    var soundUri = config.GetValue(flag ? zoomInSound : zoomOutSound);
                    if (soundUri == null) return;
                    var clip = overlayVisual.GetComponent<StaticAudioClip>(a => a.URL.Value == soundUri);
                    if (clip == null) return;
                    overlayVisual.PlayOneShot(clip, config.GetValue(zoomVolume), false, parent: false);
                }
            }
        }



        [HarmonyPatch(typeof(UserRoot), "get_DesktopFOV")]
        class Optizoom_Patch
        {
            static readonly Dictionary<UserRoot, UserRootFOVLerps> FOVLerps = new();

            public static void Postfix(UserRoot __instance, ref float __result, DesktopRenderSettings ____renderSettings)
            {
                if (config == null) return;
                if (!FOVLerps.TryGetValue(__instance, out UserRootFOVLerps lerp))
                {
                    lerp = new UserRootFOVLerps(); // Needs one per UserRoot or else userspace and focused world fights
                    FOVLerps.Add(__instance, lerp);
                }

                var flag =  config.GetValue(Enabled)
                        && !__instance.LocalUser.HasActiveFocus() // Not focused in any field
                        && !Userspace.HasFocus // Not focused in userspace field
                        && __instance.Engine.WorldManager.FocusedWorld == __instance.World // Focused in the same world as the UserRoot
                        && (toggleState || __instance.InputInterface.GetKey(config.GetValue(ZoomKey))); // Key pressed
                
                float fovSetting = (____renderSettings != null) ? ____renderSettings.FieldOfView.Value : 60f;
                float target = flag ? fovSetting - config.GetValue(ZoomFOV) : 0f;//__result;
                
                if (flag && config.GetValue(ScrollZoom))
                {
                    var scrollDelta = -__instance.InputInterface.Mouse.NormalizedScrollWheelDelta.Value.Y;//.ScrollWheelDelta.Delta.y;

                    lerp.scroll += scrollDelta * config.GetValue(ScrollZoomSpeed);

                    lerp.scroll = MathX.Clamp(lerp.scroll, -config.GetValue(ZoomFOV), 179f - config.GetValue(ZoomFOV)); // Clamp to the available fov
                    target -= lerp.scroll;
                   
                    /* Compensation is not complete yet
                    lerp.scroll = MathX.Clamp(lerp.scroll, -1, 1);

                    var remap = MathX.Remap11_01(lerp.scroll);
                    remap *= remap;
                    remap = MathX.Remap(remap, 0f, 1f, -config.GetValue(ZoomFOV), 179f - config.GetValue(ZoomFOV));

                    target -= remap;
                    */
                }
                else if(config.GetValue(ScrollZoom) && !MathX.Approximately(lerp.scroll, 0f, 0.001)) {
                    lerp.scroll = 0f;
                }

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
            }
        }

        class UserRootFOVLerps
        {
            public float currentLerp = 0f;
            public float lerpVelocity = 0f;
            public float scroll = 0f;
        }


        public static bool TryWriteDynamicValue<T>(Slot root, string name, T value)
        {
            DynamicVariableHelper.ParsePath(name, out string spaceName, out string text);

            if (string.IsNullOrEmpty(text)) return false;

            DynamicVariableSpace dynamicVariableSpace = root.FindSpace(spaceName);
            if (dynamicVariableSpace == null) return false;
            return dynamicVariableSpace.TryWriteValue(text, value);
        }
        public static bool TryReadDynamicValue<T>(Slot root, string name, out T value)
        {
            value = Coder<T>.Default;
            DynamicVariableHelper.ParsePath(name, out string spaceName, out string text);

            if (string.IsNullOrEmpty(text)) return false;

            DynamicVariableSpace dynamicVariableSpace = root.FindSpace(spaceName);
            if (dynamicVariableSpace == null) return false;
            return dynamicVariableSpace.TryReadValue(text, out value);
        }
    }
}