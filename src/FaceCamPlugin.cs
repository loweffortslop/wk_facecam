using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FaceCam
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class FaceCamPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.slopgaming.facecam";
        public const string PluginName = "FaceCam";
        public const string PluginVersion = "1.3.2";

        public enum Corner { TopLeft, TopRight, BottomLeft, BottomRight }

        // General
        private ConfigEntry<KeyboardShortcut> toggleKey;
        private ConfigEntry<bool> enabledOnStart;
        private ConfigEntry<bool> hideWhenNoThreat;
        private ConfigEntry<float> maxTrackDistance;
        // Threats
        private ConfigEntry<bool> trackMass;
        private ConfigEntry<bool> trackFaces;
        private ConfigEntry<bool> trackHunter;
        private ConfigEntry<bool> trackRemains;
        private ConfigEntry<bool> trackRatKing;
        // Viewport
        private ConfigEntry<Corner> corner;
        private ConfigEntry<float> viewportSize;
        private ConfigEntry<float> margin;
        private ConfigEntry<float> borderThickness;
        private ConfigEntry<Color> borderColor;
        private ConfigEntry<bool> showName;
        private ConfigEntry<bool> showDistance;
        // Camera
        private ConfigEntry<float> fieldOfView;
        private ConfigEntry<float> aimSmoothing;
        private ConfigEntry<int> resolutionScale;
        // Shake
        private ConfigEntry<float> shakeDistance;
        private ConfigEntry<float> shakeAngle;
        private ConfigEntry<float> shakePixels;
        private ConfigEntry<float> shakeSpeed;

        private bool active;
        private Camera cam;
        private RenderTexture rt;

        // HUD objects, parented to the game's HUD canvas (the one holding the hand icons).
        private Canvas hudCanvas;
        private RectTransform hudRoot;
        private Image border;
        private RawImage feed;
        private RectTransform tagRoot;
        private TextMeshProUGUI tagText;

        private Threat current;
        private bool hasThreat;
        private Quaternion aimRotation;
        private bool aimInitialized;
        private Vector2 shakeOffset;

        private static readonly FieldInfo FaceIsPrimary =
            typeof(DEN_Face).GetField("isPrimary", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo GameDead =
            typeof(CL_GameManager).GetField("dead", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo GameLoading =
            typeof(CL_GameManager).GetField("loading", BindingFlags.Instance | BindingFlags.NonPublic);

        private struct Threat
        {
            public string Name;
            public Vector3 Point;
            public float Distance;
        }

        private void Awake()
        {
            toggleKey = Config.Bind("General", "ToggleKey", new KeyboardShortcut(KeyCode.F7),
                "Key that turns the FaceCam viewport on and off.");
            enabledOnStart = Config.Bind("General", "EnabledOnStart", false,
                "Whether the FaceCam starts switched on when the game launches.");
            hideWhenNoThreat = Config.Bind("General", "HideWhenNoThreat", true,
                "Hide the viewport while no tracked threat exists (or all are out of range).");
            maxTrackDistance = Config.Bind("General", "MaxTrackDistance", 250f,
                "Threats further away than this (metres) are ignored.");

            trackMass = Config.Bind("Threats", "TrackMass", true, "Track the Mass (rising goo below you).");
            trackFaces = Config.Bind("Threats", "TrackFaces", true, "Track Faces. Each Face is tracked separately and labelled.");
            trackHunter = Config.Bind("Threats", "TrackHunter", true, "Track the Hunter.");
            trackRemains = Config.Bind("Threats", "TrackRemains", true, "Track active Remains (the climbing bodies).");
            trackRatKing = Config.Bind("Threats", "TrackRatKing", true, "Track the Rat King.");

            corner = Config.Bind("Viewport", "Corner", Corner.BottomRight, "Screen corner the viewport sits in.");
            viewportSize = Config.Bind("Viewport", "Size", 0.28f,
                new ConfigDescription("Side length of the square viewport as a fraction of screen height.", new AcceptableValueRange<float>(0.05f, 0.9f)));
            margin = Config.Bind("Viewport", "Margin", 24f, "Gap in pixels between the viewport and the screen edges.");
            borderThickness = Config.Bind("Viewport", "BorderThickness", 3f, "Border thickness in pixels.");
            borderColor = Config.Bind("Viewport", "BorderColor", new Color(0.85f, 0.85f, 0.85f, 0.9f), "Border colour (RGBA).");
            showName = Config.Bind("Viewport", "ShowName", true, "Show a name tag with the tracked threat's name.");
            showDistance = Config.Bind("Viewport", "ShowDistance", false, "Add the distance to the threat to the name tag.");

            fieldOfView = Config.Bind("Camera", "FieldOfView", 60f,
                new ConfigDescription("Field of view of the FaceCam.", new AcceptableValueRange<float>(20f, 120f)));
            aimSmoothing = Config.Bind("Camera", "AimSmoothing", 10f, "How quickly the camera turns towards the threat. Higher is snappier; 0 snaps instantly.");
            resolutionScale = Config.Bind("Camera", "ResolutionScale", 1,
                new ConfigDescription("Divides the render texture resolution (1 = full, 2 = half). Raise for performance.", new AcceptableValueRange<int>(1, 4)));

            shakeDistance = Config.Bind("Shake", "ShakeDistance", 10f, "The viewport starts to shake when the threat is closer than this (metres). 0 disables shake.");
            shakeAngle = Config.Bind("Shake", "ShakeAngle", 1.2f, "Maximum camera wobble in degrees at point blank.");
            shakePixels = Config.Bind("Shake", "ShakePixels", 4f, "Maximum viewport jitter in pixels at point blank.");
            shakeSpeed = Config.Bind("Shake", "ShakeSpeed", 18f, "Shake frequency.");

            active = enabledOnStart.Value;
            Log = Logger;
            instance = this;
            new Harmony(PluginGuid).PatchAll(typeof(RemainsVisibilityPatch));
            Logger.LogInfo($"{PluginName} {PluginVersion} loaded. Toggle with {toggleKey.Value}. Starts {(active ? "on" : "off")}.");
        }

        internal static ManualLogSource Log;
        private static FaceCamPlugin instance;

        // True while the FaceCam camera is drawing, so its renders count towards Renderer.isVisible.
        internal static bool Rendering => instance != null && instance.cam != null && instance.cam.enabled;

        private void Update()
        {
            if (toggleKey.Value.IsDown())
            {
                active = !active;
                Logger.LogInfo($"FaceCam {(active ? "on" : "off")}");
            }

            ENT_Player player = ENT_Player.playerObject;
            if (!active || player == null || player.cam == null || !InPlayableState())
            {
                Hide();
                return;
            }

            CL_GameManager gMan = CL_GameManager.gMan;
            bool paused = gMan != null && gMan.isPaused;

            // While paused the feed stays on screen (under the pause menu) but freezes.
            if (!paused)
            {
                Vector3 eye = player.cam.transform.position;
                hasThreat = FindClosestThreat(player, eye, out current);
            }

            if (!hasThreat && hideWhenNoThreat.Value)
            {
                Hide();
                return;
            }

            if (!EnsureHud(player))
            {
                Hide();
                return;
            }

            LayoutHud();
            EnsureCamera(player.cam);
            if (!paused) UpdateCameraPose(player.cam.transform.position);
            UpdateTag();

            hudRoot.gameObject.SetActive(true);
            cam.enabled = !paused;
        }

        private static bool InPlayableState()
        {
            CL_GameManager gMan = CL_GameManager.gMan;
            if (gMan == null) return false;
            if (CL_GameManager.runHasEnded) return false;
            if (GameDead != null && (bool)GameDead.GetValue(null)) return false;
            if (GameLoading != null && (bool)GameLoading.GetValue(gMan)) return false;
            return true;
        }

        private void Hide()
        {
            aimInitialized = false;
            if (cam != null) cam.enabled = false;
            if (hudRoot != null) hudRoot.gameObject.SetActive(false);
        }

        private bool FindClosestThreat(ENT_Player player, Vector3 eye, out Threat best)
        {
            Threat closest = default;
            float bestDist = maxTrackDistance.Value;
            bool found = false;

            void Consider(string name, Vector3 point)
            {
                float d = Vector3.Distance(eye, point);
                if (d < bestDist)
                {
                    bestDist = d;
                    closest = new Threat { Name = name, Point = point, Distance = d };
                    found = true;
                }
            }

            if (trackMass.Value && DEN_DeathFloor.instance != null && DEN_DeathFloor.instance.isActiveAndEnabled)
            {
                // GetHeight() returns a huge negative value when the Mass is inactive or harmless.
                float height = DEN_DeathFloor.GetHeight();
                if (height > -10000f)
                {
                    // The Mass is a rising plane: the nearest point is straight below the player.
                    Vector3 p = player.transform.position;
                    Consider("The Mass", new Vector3(p.x, height, p.z));
                }
            }

            if (trackFaces.Value && DEN_Face.faceInstances != null)
            {
                List<DEN_Face> faces = DEN_Face.faceInstances;
                int secondaryIndex = 2;
                for (int i = 0; i < faces.Count; i++)
                {
                    DEN_Face face = faces[i];
                    if (face == null || !face.isActiveAndEnabled) continue;
                    bool primary = FaceIsPrimary != null && (bool)FaceIsPrimary.GetValue(face);
                    string name = primary ? "Face" : $"Face #{secondaryIndex++}";
                    Consider(name, face.transform.position);
                }
            }

            if (trackHunter.Value && DEN_Hunter.activeHunters != null)
            {
                List<DEN_Hunter> hunters = DEN_Hunter.activeHunters;
                for (int i = 0; i < hunters.Count; i++)
                {
                    DEN_Hunter hunter = hunters[i];
                    if (hunter == null || !hunter.isActiveAndEnabled) continue;
                    Transform t = hunter.headObject != null ? hunter.headObject : hunter.transform;
                    Consider(hunters.Count > 1 ? $"Hunter #{i + 1}" : "Hunter", t.position);
                }
            }

            if (trackRatKing.Value && DEN_RatKing.activeRatKings != null)
            {
                List<DEN_RatKing> kings = DEN_RatKing.activeRatKings;
                for (int i = 0; i < kings.Count; i++)
                {
                    DEN_RatKing king = kings[i];
                    if (king == null || !king.isActiveAndEnabled) continue;
                    Consider(kings.Count > 1 ? $"Rat King #{i + 1}" : "Rat King", king.transform.position);
                }
            }

            if (trackRemains.Value && GameEntity.entities != null)
            {
                // Remains have no registry of their own; the game finds them by scanning all entities too.
                List<GameEntity> entities = GameEntity.entities;
                for (int i = 0; i < entities.Count; i++)
                {
                    if (!(entities[i] is DEN_Remains_Active remains) || remains == null) continue;
                    if (!remains.active || !remains.isActiveAndEnabled) continue;
                    Vector3 point = remains.capsuleCollider != null
                        ? remains.capsuleCollider.bounds.center
                        : remains.transform.position;
                    Consider("Remains", point);
                }
            }

            best = closest;
            return found;
        }

        private void UpdateCameraPose(Vector3 eye)
        {
            cam.transform.position = eye;
            cam.fieldOfView = fieldOfView.Value;

            if (hasThreat)
            {
                Vector3 dir = current.Point - eye;
                if (dir.sqrMagnitude > 0.0001f)
                {
                    // Orient against the world, not the player, so the feed doesn't roll or spin as the player looks around.
                    Vector3 up = Vector3.up;
                    // Looking straight up or down (e.g. at the Mass): use a fixed world axis instead.
                    if (Mathf.Abs(Vector3.Dot(dir.normalized, up)) > 0.98f) up = Vector3.forward;
                    Quaternion target = Quaternion.LookRotation(dir, up);
                    if (!aimInitialized || aimSmoothing.Value <= 0f)
                        aimRotation = target;
                    else
                        aimRotation = Quaternion.Slerp(aimRotation, target, 1f - Mathf.Exp(-aimSmoothing.Value * Time.unscaledDeltaTime));
                    aimInitialized = true;
                }
            }

            // Light shake when the threat is close: a small Perlin wobble of the camera and the viewport.
            float shake = 0f;
            if (hasThreat && shakeDistance.Value > 0f && current.Distance < shakeDistance.Value)
                shake = 1f - current.Distance / shakeDistance.Value;

            Quaternion wobble = Quaternion.identity;
            shakeOffset = Vector2.zero;
            if (shake > 0f)
            {
                float t = Time.unscaledTime * shakeSpeed.Value;
                float nx = Mathf.PerlinNoise(t, 0.1f) * 2f - 1f;
                float ny = Mathf.PerlinNoise(0.7f, t) * 2f - 1f;
                float nz = Mathf.PerlinNoise(t, 3.3f) * 2f - 1f;
                float a = shakeAngle.Value * shake;
                wobble = Quaternion.Euler(nx * a, ny * a, nz * a * 0.5f);
                shakeOffset = new Vector2(nx, ny) * shakePixels.Value * shake;
            }

            cam.transform.rotation = aimRotation * wobble;
        }

        private void EnsureCamera(Camera playerCam)
        {
            int scale = Mathf.Max(1, resolutionScale.Value);
            int side = Mathf.Max(16, Mathf.RoundToInt(SidePixels() / scale));

            if (rt == null || rt.width != side)
            {
                if (rt != null)
                {
                    if (cam != null) cam.targetTexture = null;
                    rt.Release();
                    Destroy(rt);
                }
                rt = new RenderTexture(side, side, 24, OpaqueFormat()) { name = "FaceCamRT" };
                rt.Create();
                if (feed != null) feed.texture = rt;
            }

            if (cam == null)
            {
                var go = new GameObject("FaceCam_Camera");
                DontDestroyOnLoad(go);
                cam = go.AddComponent<Camera>();
                cam.CopyFrom(playerCam);
                cam.depth = playerCam.depth - 1f;
                cam.enabled = false;
            }

            // Keep world layers in sync with the player camera (it can change between levels).
            cam.cullingMask = playerCam.cullingMask;
            cam.clearFlags = playerCam.clearFlags;
            cam.backgroundColor = playerCam.backgroundColor;
            cam.nearClipPlane = playerCam.nearClipPlane;
            cam.farClipPlane = playerCam.farClipPlane;
            cam.aspect = 1f;
            cam.targetTexture = rt;
        }

        // The sky renders with alpha 0, and the uGUI RawImage alpha-blends, so an RGBA texture lets the sky show
        // through as the border behind it. A format without an alpha channel always samples as opaque.
        private static RenderTextureFormat OpaqueFormat()
        {
            if (SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGB111110Float)) return RenderTextureFormat.RGB111110Float;
            return RenderTextureFormat.RGB565;
        }

        private float SidePixels() => Screen.height * viewportSize.Value;

        // Builds the overlay inside the canvas that holds the hand icons, placed just before the hand
        // icons' branch so everything the game draws later in that canvas (and any canvas sorted above it,
        // such as the pause menu and end screen) covers it.
        private bool EnsureHud(ENT_Player player)
        {
            if (hudRoot != null && hudCanvas != null) return true;

            Image handIcon = null;
            if (player.hands != null)
                foreach (var hand in player.hands)
                    if (hand != null && hand.uiInteract != null) { handIcon = hand.uiInteract; break; }
            if (handIcon == null || handIcon.canvas == null) return false;

            hudCanvas = handIcon.canvas.rootCanvas;
            Transform canvasT = hudCanvas.transform;

            // The direct child of the canvas that contains the hand icons.
            Transform branch = handIcon.transform;
            while (branch.parent != null && branch.parent != canvasT) branch = branch.parent;
            int siblingIndex = branch.parent == canvasT ? branch.GetSiblingIndex() : 0;

            // If the pause menu lives in the same canvas, make sure it still draws on top.
            CL_GameManager gMan = CL_GameManager.gMan;
            if (gMan != null && gMan.pauseMenu != null && gMan.pauseMenu.transform.IsChildOf(canvasT))
            {
                Transform pauseBranch = gMan.pauseMenu.transform;
                while (pauseBranch.parent != null && pauseBranch.parent != canvasT) pauseBranch = pauseBranch.parent;
                if (pauseBranch.parent == canvasT) siblingIndex = Mathf.Min(siblingIndex, pauseBranch.GetSiblingIndex());
            }

            var rootGo = new GameObject("FaceCam_HUD", typeof(RectTransform));
            hudRoot = (RectTransform)rootGo.transform;
            hudRoot.SetParent(canvasT, false);
            hudRoot.SetSiblingIndex(siblingIndex);
            rootGo.layer = branch.gameObject.layer;

            border = NewChild<Image>("Border", hudRoot);
            border.raycastTarget = false;
            Stretch(border.rectTransform, 0f);

            feed = NewChild<RawImage>("Feed", hudRoot);
            feed.raycastTarget = false;
            feed.texture = rt;

            var tagBg = NewChild<Image>("NameTag", feed.rectTransform);
            tagBg.raycastTarget = false;
            tagBg.color = new Color(0f, 0f, 0f, 0.55f);
            tagRoot = tagBg.rectTransform;
            tagRoot.anchorMin = new Vector2(0f, 0f);
            tagRoot.anchorMax = new Vector2(1f, 0f);
            tagRoot.pivot = new Vector2(0.5f, 0f);
            tagRoot.anchoredPosition = Vector2.zero;

            tagText = NewChild<TextMeshProUGUI>("Text", tagRoot);
            tagText.raycastTarget = false;
            tagText.alignment = TextAlignmentOptions.Center;
            tagText.textWrappingMode = TextWrappingModes.NoWrap;
            tagText.overflowMode = TextOverflowModes.Ellipsis;
            tagText.color = Color.white;
            Stretch(tagText.rectTransform, 0f);
            ApplySubtitleFont(tagText);
            liveFontApplied = false;

            Logger.LogInfo($"FaceCam HUD attached to canvas '{hudCanvas.name}' at index {siblingIndex} (before '{branch.name}').");
            return true;
        }

        // The subtitle template's font isn't the one players see, so prefer a live subtitle line once one appears.
        private void ApplySubtitleFont(TextMeshProUGUI text)
        {
            CL_UIManager ui = CL_UIManager.instance;
            TMP_Text subtitle = ui != null && ui.subtitleObject != null ? ui.subtitleObject.text : null;
            if (subtitle != null && subtitle.font != null) CopyFont(subtitle, text, "subtitle template");
            else Logger.LogWarning("Subtitle font not found; the name tag uses the default TMP font.");
        }

        private float nextLiveFontCheck;

        private void TryApplyLiveSubtitleFont()
        {
            if (liveFontApplied || Time.unscaledTime < nextLiveFontCheck) return;
            nextLiveFontCheck = Time.unscaledTime + 1f;

            CL_UIManager ui = CL_UIManager.instance;
            if (ui == null || ui.subtitleParent == null) return;
            foreach (UI_Subtitle line in ui.subtitleParent.GetComponentsInChildren<UI_Subtitle>(false))
            {
                if (line.text == null || line.text.font == null || string.IsNullOrEmpty(line.text.text)) continue;
                CopyFont(line.text, tagText, "live subtitle");
                liveFontApplied = true;
                return;
            }
        }

        private bool liveFontApplied;

        private void CopyFont(TMP_Text from, TMP_Text to, string source)
        {
            to.font = from.font;
            to.fontSharedMaterial = from.fontSharedMaterial;
            to.fontStyle = from.fontStyle;
            to.characterSpacing = from.characterSpacing;
            Logger.LogInfo($"Name tag font from {source}: '{from.font.name}', material '{from.fontSharedMaterial?.name}'.");
        }

        private static T NewChild<T>(string name, Transform parent) where T : Component
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            return go.AddComponent<T>();
        }

        private static void Stretch(RectTransform r, float inset)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(inset, inset);
            r.offsetMax = new Vector2(-inset, -inset);
        }

        private void LayoutHud()
        {
            // Config values are in screen pixels; convert to canvas units.
            float scale = hudCanvas.scaleFactor > 0f ? hudCanvas.scaleFactor : 1f;
            float side = SidePixels() / scale;
            float m = margin.Value / scale;
            float b = borderThickness.Value / scale;

            bool left = corner.Value == Corner.TopLeft || corner.Value == Corner.BottomLeft;
            bool top = corner.Value == Corner.TopLeft || corner.Value == Corner.TopRight;
            var anchor = new Vector2(left ? 0f : 1f, top ? 1f : 0f);

            hudRoot.anchorMin = anchor;
            hudRoot.anchorMax = anchor;
            hudRoot.pivot = anchor;
            hudRoot.sizeDelta = new Vector2(side + 2f * b, side + 2f * b);
            hudRoot.anchoredPosition = new Vector2(left ? m : -m, top ? -m : m) + shakeOffset / scale;

            border.color = borderColor.Value;
            Stretch(feed.rectTransform, b);

            float tagHeight = side * 0.12f;
            tagRoot.sizeDelta = new Vector2(0f, tagHeight);
            tagText.fontSize = tagHeight * 0.62f;
        }

        private void UpdateTag()
        {
            TryApplyLiveSubtitleFont();
            bool show = (showName.Value || showDistance.Value) && hasThreat;
            tagRoot.gameObject.SetActive(show);
            if (!show) return;

            string label = showName.Value ? current.Name : "";
            if (showDistance.Value)
                label = showName.Value ? $"{label}  {current.Distance:0}m" : $"{current.Distance:0}m";
            if (tagText.text != label) tagText.text = label;
        }

        private void OnDestroy()
        {
            if (cam != null) Destroy(cam.gameObject);
            if (hudRoot != null) Destroy(hudRoot.gameObject);
            if (rt != null) { rt.Release(); Destroy(rt); }
        }
    }
}
