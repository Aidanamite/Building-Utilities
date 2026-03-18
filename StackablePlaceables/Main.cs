using HarmonyLib;
using HMLLibrary;
using Steamworks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using I2.Loc;
using RaftModLoader;
using System.Text;
using System.Runtime.CompilerServices;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;
using UnityEngine.Networking;
using System.Globalization;
using static SO_TradingPost_Buyable;
using static Unity.Collections.AllocatorManager;
using UnityEngine.InputSystem;
using PlayFab.MultiplayerModels;
using System.Runtime.InteropServices;

namespace StackablePlaceables
{ 
    public class Main : Mod
    {
        private Harmony harmony;
        public static bool overrideAccept = false;
        private static bool _rayhit = false;
        private static bool _surface = false;
        public static GameObject uiCanvas;
        public static RectTransform displayArea;
        public static timeDiff control;
        public static string togglePopupKey;
        public static string moveBlockKey;
        public static bool pToggle;
        public static bool popupToggled;
        public static DPS surfaceType;
        public static float? yRotationOverride = null;

        public static bool overrideRayhit
        {
            get
            {
                return _rayhit;
            }
            set
            {
                _rayhit = value;
                if (!value)
                    Patch_raycast.ResetPositions();
            }
        }
        public static bool overrideSurface
        {
            get => _surface;
            set
            {
                _surface = value;
                if (!value)
                    Patch_raycast.ResetSides();
            }
        }
        static SupportMode _sM;
        public static SupportMode supportMode
        {
            get => _sM;
            set
            {
                _sM = value;
                if (value == SupportMode.Disabled && !_rayhit)
                    Patch_raycast.ResetPositions();
            }
        }
        public static bool overridePipeCollisions;
        public static bool showTips
        {
            get
            {
                if (popupToggled)
                    return false;
                Network_Player player = RAPI.GetLocalPlayer();
                return player != null && player.Inventory.GetSelectedHotbarItem() != null && (player.Inventory.GetSelectedHotbarItem().UniqueName == "Hammer" || (player.Inventory.GetSelectedHotbarItem().settings_buildable != null && player.Inventory.GetSelectedHotbarItem().settings_buildable.HasBuildablePrefabs));
            }
        }
        public static bool togglePopsKey
        {
            get
            {
                if (ExtraSettingsAPI_Loaded)
                    return MyInput.GetButton(togglePopupKey);
                return Input.GetKey(KeyCode.H);
            }
        }
        public static bool MoveBlockKey
        {
            get
            {
                if (ExtraSettingsAPI_Loaded)
                    return MyInput.GetButtonDown(moveBlockKey);
                return Input.GetKeyDown(KeyCode.G);
            }
        }
        public static string snugKey;
        public static bool SnugHoldKeyDown => ExtraSettingsAPI_Loaded ? MyInput.GetButtonDown(snugKey) : Input.GetKeyDown(KeyCode.M);
        public static bool SnugHoldKey => ExtraSettingsAPI_Loaded ? MyInput.GetButton(snugKey) : Input.GetKey(KeyCode.M);
        public static Keybind SnugKey;
        static bool _snug;
        static int _snugupdate;
        public static bool SnugEnabled
        {
            get
            {
                if (snugHold == SnugKeyMode.Toggle)
                {
                    if (SnugHoldKeyDown && _snugupdate != Time.frameCount)
                    {
                        _snugupdate = Time.frameCount; // prevent toggling on and off in the same frame by 2 different checks
                        _snug = !_snug;
                    }
                    return _snug;
                }
                return (snugHold == SnugKeyMode.HoldEnable) == SnugHoldKey;
            }
        }
        public static SnugKeyMode snugHold = SnugKeyMode.HoldEnable;
        public static SnugDetect snugDetect = SnugDetect.DoubleCast;
        public static string recursiveKey;
        public static string lockKey;
        public static bool RecursiveModeKeyDown => ExtraSettingsAPI_Loaded ? MyInput.GetButtonDown(recursiveKey) : Input.GetKeyDown(KeyCode.B);
        public static bool MatchPlacementKey => ExtraSettingsAPI_Loaded ? MyInput.GetButton(lockKey) : Input.GetKey(KeyCode.J);
        public static Keybind RecursiveModeKey;
        public static Keybind MatchKey;
        public static RecursiveMode recursiveMode;
        public static Vector3 rotation = Vector3.zero;
        public static Main instance;
        public static string logPrefix => "[" + instance.modlistEntry.jsonmodinfo.name + "]: ";
        public static Settings settingsController = ComponentManager<Settings>.Value;
        static Text display;
        static CanvasHelper _can = null;
        public static CanvasHelper canvas
        {
            get
            {
                if (_can == null)
                    _can = ComponentManager<CanvasHelper>.Value;
                return _can;
            }
        }
        public static Transform prefabHolder;
        public void Start()
        {
            instance = this;
            harmony = new Harmony("com.aidanamite.StackablePlaceables");
            harmony.PatchAll();
            control = new timeDiff(0.3f, true, true);
            CreateUI();
            prefabHolder = new GameObject("BuildingUtilitiesPrefabHolder").transform;
            prefabHolder.gameObject.SetActive(false);
            DontDestroyOnLoad(prefabHolder.gameObject);
            Log("Mod has been loaded!");
        }
        //public override void WorldEvent_WorldLoaded() => AddImageObject(canvas.transform, 0);
        /*public override void Event_ReturnToMainMenu()
        {
            if (imageContainer != null)
            {
                Destroy(imageContainer);
                imageContainer = null;
            }
        }*/

        [ConsoleCommand(name: "toggleForceAccept", docs: "When active, ignores most build location restrictions")]
        public static string MyCommand(string[] args)
        {
            overrideAccept = !overrideAccept;
            return logPrefix + "Is " + (overrideAccept ? "now" : "no longer") + " overriding acceptable build location";
        }

        [ConsoleCommand(name: "toggleOverrideRaycast", docs: "When active, uses alternate placement location finder")]
        public static string MyCommand2(string[] args)
        {
            overrideRayhit = !overrideRayhit;
            return logPrefix + "Is " + (overrideRayhit ? "now" : "no longer") + " overriding build location raycast";
        }

        [ConsoleCommand(name: "toggleNoSupport", docs: "Enables or disables the support override to affect certain blocks")]
        public static string MyCommand3(string[] args)
        {
            supportMode = supportMode == SupportMode.Normal ? SupportMode.Disabled : SupportMode.Normal;
            return $"{logPrefix}Support requirements override is now {supportMode}";
        }

        [ConsoleCommand(name: "toggleFullSupport", docs: "Enables or disables the support override to affect ALL blocks")]
        public static string MyCommand4(string[] args)
        {
            supportMode = supportMode == SupportMode.Full ? SupportMode.Disabled : SupportMode.Full;
            return $"{logPrefix}Support requirements override is now {supportMode}";
        }

        [ConsoleCommand(name: "togglePipeCollisions", docs: "When active, allows pipes to connect through solid blocks")]
        public static string MyCommand8(string[] args)
        {
            overridePipeCollisions = !overridePipeCollisions;
            return logPrefix + "Is " + (overridePipeCollisions ? "now" : "no longer") + " overriding pipe collisions";
        }

        [ConsoleCommand(name: "setBuildRotations", docs: "Syntax: setBuildRotations <xRot> <zRot> - Recommended to use multiples of 90")]
        public static string MyCommand5(string[] args)
        {
            if (args.Length < 2)
                return "Not enough arguments";
            if (args.Length > 2)
                return "Too many arguments";
            if (!float.TryParse(args[0],NumberStyles.Float,CultureInfo.InvariantCulture,out var x))
                return $"{logPrefix}{args[0]} cannot be parsed";
            if (!float.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
                return $"{logPrefix}{args[1]} cannot be parsed";
            rotation = new Vector3(x,0,z);
            return logPrefix + "Rotations modified";
        }

        [ConsoleCommand(name: "setSurfaceType", docs: "Syntax: setSurfaceType <surfaceType> - Forces the placement to use the prefab for a specific surface type. Use \"None\" to disable this override")]
        public static string MyCommand6(string[] args)
        {
            if (args.Length < 1)
                return "Not enough arguments";
            if (args.Length > 1)
                return "Too many arguments";
            if (args[0].ToLower() == "none")
            {
                overrideSurface = false;
                return $"{logPrefix}Surface override has been disabled";
            }
            if (Enum.TryParse(args[0], out DPS t))
            {
                surfaceType = t;
                overrideSurface = true;
                return $"{logPrefix}Surface override has been set to {t}";
            }
            return $"\"{args[0]}\" is not a valid surface type. Please use one of the following:\nNone\n" + Enum.GetValues(typeof(DPS)).Cast<DPS>().Join((x)=> x.ToString(),"\n");
        }

        [ConsoleCommand(name: "forceYBuildRotation", docs: "Syntax: forceYBuildRotation <Rot> - Recommended to use multiples of 90 with objects that snap to the grid. Use \"None\" to disable this override")]
        public static string MyCommand7(string[] args)
        {
            if (args.Length < 1)
                return "Not enough arguments";
            if (args.Length > 1)
                return "Too many arguments";
            if (args[0].ToLower() == "none")
            {
                yRotationOverride = null;
                return $"{logPrefix}Y rotation override has been disabled";
            }
            if (!float.TryParse(args[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
                return logPrefix + args[0] + " cannot be parsed";
            yRotationOverride = y;
            return logPrefix + "Y rotation modified";
        }

        //Memory<Text, (float width, float height)> DisplaySize = new Memory<Text, (float width, float height)>(x => (x.preferredWidth, x.preferredHeight));
        public void Update()
        {
            if (MyInput.GetButton("Rotate"))
            {
                if (Input.GetKeyDown(KeyCode.LeftArrow))
                    rotation.x += 90;
                if (Input.GetKeyDown(KeyCode.RightArrow))
                    rotation.x -= 90;
                if (Input.GetKeyDown(KeyCode.UpArrow))
                    rotation.z += 90;
                if (Input.GetKeyDown(KeyCode.DownArrow))
                    rotation.z -= 90;
                if (Input.GetKeyDown(KeyCode.LeftAlt) || Input.GetKeyDown(KeyCode.RightAlt))
                    rotation = Vector3.zero;
                rotation.x %= 360;
                rotation.z %= 360;
            }
            if (MyInput.GetButton("Sprint"))
            {
                if (Input.GetKeyDown(KeyCode.LeftArrow))
                    overrideRayhit = true;
                if (Input.GetKeyDown(KeyCode.RightArrow))
                    overrideRayhit = false;
                if (Input.GetKeyDown(KeyCode.UpArrow))
                    overrideAccept = true;
                if (Input.GetKeyDown(KeyCode.DownArrow))
                    overrideAccept = false;
                if (Input.GetKeyDown(KeyCode.LeftAlt) || Input.GetKeyDown(KeyCode.RightAlt))
                {
                    overrideRayhit = false;
                    overrideAccept = false;
                }
            }
            if (uiCanvas != null)
            {
                if (pToggle != togglePopsKey)
                {
                    pToggle = !pToggle;
                    if (pToggle)
                        popupToggled = !popupToggled;
                }
                if (GenerateInfo(false, out var msg))
                    display.text = msg;
                //var f = DisplaySize.GetValue(display, out var size);
                if (control.Update(showTips, out var newValue))// || f)
                {
                    //var trans = imageContainer.GetComponent<Image>().rectTransform;
                    displayArea.pivot = new Vector2(newValue, displayArea.pivot.y);
                    //size.width += display.rectTransform.offsetMin.x - display.rectTransform.offsetMax.x;
                    //size.height += display.rectTransform.offsetMin.y - display.rectTransform.offsetMax.y;
                    //trans.offsetMin = new Vector2(size.width * newValue - size.width, size.height * -0.5f);
                    //trans.offsetMax = new Vector2(size.width * newValue, size.height * 0.5f);
                }
            }
        }

        public void OnModUnload()
        {
            if (uiCanvas != null)
            {
                Destroy(uiCanvas);
                uiCanvas = null;
            }
            if (prefabHolder)
            {
                Destroy(prefabHolder.gameObject);
                prefabHolder = null;
            }
            if (Patch_BlockCreator.cost)
            {
                Destroy(Patch_BlockCreator.cost.gameObject);
                Patch_BlockCreator.cost = null;
            }
            foreach (var b in Patch_BlockCreator.ghostBlocks)
                if (b)
                    Destroy(b.gameObject);
            Patch_BlockCreator.ghostBlocks.Clear();
            harmony.UnpatchAll(harmony.Id);
            Patch_raycast.ResetPositions();
            Patch_raycast.ResetSides();
            Log("Mod has been unloaded!");
        }

        public static void CreateUI()
        {
            var options = ComponentManager<Settings>.Value.optionsCanvas.transform.FindChildRecursively("OptionMenuParent");
            var optionsBack = options.FindChildRecursively("BrownBackground").GetComponent<Image>();
            var optionsDivider = options.FindChildRecursively("Divider").GetComponent<Image>();
            var optionsTitle = options.GetComponentInChildren<Text>(true);
            var optionsCanvas = options.GetComponentsInParent<CanvasScaler>(true)[0];

            var canvas = new GameObject("uiCanvas", typeof(RectTransform)).AddComponent<Canvas>();
            canvas.gameObject.SetActive(false);
            DontDestroyOnLoad(uiCanvas = canvas.gameObject);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 2000;
            canvas.sortingLayerID = 0;
            var scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = optionsCanvas.uiScaleMode;
            scaler.screenMatchMode = optionsCanvas.screenMatchMode;
            scaler.matchWidthOrHeight = optionsCanvas.matchWidthOrHeight;
            scaler.referenceResolution = optionsCanvas.referenceResolution;

            var body = displayArea = canvas.transform.NewChild("body").AddComponent<RectTransform>();
            body.anchorMax = body.anchorMin = new Vector2(1, 0.5f);
            body.offsetMax = body.offsetMin = Vector2.zero;
            body.pivot = new Vector2(0, 0.5f);

            var back = Instantiate(optionsBack, body);
            back.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            var fit = body.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var layout = body.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 0, 0);
            layout.spacing = 10;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childForceExpandWidth = true;

            var title = body.transform.NewChild("title").AddComponent<Text>();
            var titleFit = title.gameObject.AddComponent<ContentSizeFitter>();
            titleFit.horizontalFit = titleFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            title.text = "Building Utilities";
            title.font = optionsTitle.font;
            title.fontSize = optionsTitle.fontSize;
            title.color = optionsTitle.color;

            var div = body.transform.NewChild("titleDiv").AddComponent<Image>();
            div.sprite = optionsDivider.sprite;
            div.type = optionsDivider.type;
            div.pixelsPerUnitMultiplier = optionsDivider.pixelsPerUnitMultiplier;
            div.rectTransform.anchorMin = Vector2.zero;
            div.rectTransform.anchorMax = Vector2.zero;
            div.rectTransform.offsetMin = Vector2.zero;
            div.rectTransform.offsetMax = new Vector2(0, 8);
            var elem = div.gameObject.AddComponent<LayoutElement>();
            elem.flexibleWidth = float.PositiveInfinity;
            elem.preferredWidth = 0;
            elem.minWidth = 0;

            var text = display = body.transform.NewChild("text").AddComponent<Text>();
            var textFit = text.gameObject.AddComponent<ContentSizeFitter>();
            textFit.horizontalFit = textFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            text.text = "placeholder text";
            text.font = optionsTitle.font;
            text.fontSize = (int)(optionsTitle.fontSize * 0.7f);
            text.color = optionsTitle.color;



            canvas.gameObject.SetLayerRecursivly(LayerMask.NameToLayer("UI"));
            canvas.gameObject.SetActive(true);
        }

        /*public static void AddImageObject(Transform transform, float scale)
        {
            if (imageContainer == null)
            {


                imageContainer = Instantiate(backgroundImg, transform, false);
                Image image = imageContainer.GetComponent<Image>();
                image.rectTransform.anchorMin = new Vector2(1, 0.5f);
                image.rectTransform.anchorMax = new Vector2(1, 0.5f);
                image.rectTransform.offsetMin = new Vector2(0, 0);
                image.rectTransform.offsetMax = new Vector2(0, 0);
                var header = CreateText(imageContainer.transform, 0, 1, "Keybinds", canvas.dropText.fontSize, canvas.dropText.color, 1, 0, canvas.dropText.font).GetComponent<RectTransform>();
                header.offsetMin = new Vector2(0, -canvas.dropText.fontSize * 1.5f);
                header.offsetMax = new Vector2(0, -canvas.dropText.fontSize * 0.25f);
                header.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;
                Image newDiv = Instantiate(divider, imageContainer.transform, false).GetComponent<Image>();
                newDiv.rectTransform.anchorMin = new Vector2(0, 1);
                newDiv.rectTransform.anchorMax = new Vector2(1, 1);
                newDiv.rectTransform.offsetMin += new Vector2(0, canvas.dropText.fontSize);
                newDiv.rectTransform.offsetMax += new Vector2(0, canvas.dropText.fontSize);
                GenerateInfo(true, out var msg);
                display = CreateText(imageContainer.transform, 0, 0,msg, canvas.dropText.fontSize, canvas.dropText.color, 1, 1, canvas.dropText.font).GetComponent<Text>();
                var cont = display.GetComponent<RectTransform>();
                cont.offsetMin = new Vector2(canvas.dropText.fontSize, canvas.dropText.fontSize);
                cont.offsetMax = new Vector2(-canvas.dropText.fontSize, -canvas.dropText.fontSize * 2.5f);
            }
        }*/
        struct UIInfo
        {
            public KeyCode sprintKey;
            public bool alt;
            public bool accept;
            public bool ray;
            public Vector3 rot;
            public KeyCode rotateKey;
            public bool sprint;
            public KeyCode snugkey;
            public SnugKeyMode snugmode;
            public KeyCode recursKey;
            public RecursiveMode recurs;
            public int recursState;
            public KeyCode matchKey;
            public bool match;
            public static UIInfo Current() => new UIInfo()
            {
                sprintKey = MyInput.Keybinds["Sprint"].MainKey,
                alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt),
                accept = overrideAccept,
                ray = overrideRayhit,
                rot = rotation,
                rotateKey = MyInput.Keybinds["Rotate"].MainKey,
                sprint = MyInput.GetButton("Sprint"),
                snugkey = ExtraSettingsAPI_Loaded ? SnugKey.MainKey : KeyCode.M,
                snugmode = snugHold,
                recursKey = ExtraSettingsAPI_Loaded ? RecursiveModeKey.MainKey : KeyCode.B,
                recurs = recursiveMode,
                recursState = Patch_BlockCreator.point1 == null ? 0 : Patch_BlockCreator.point2 == null ? 1 : 2,
                matchKey = ExtraSettingsAPI_Loaded ? MatchKey.MainKey : KeyCode.J,
                match = MatchPlacementKey
            };
        }
        static Memory<UIInfo> InfoChange = new Memory<UIInfo>(UIInfo.Current);
        public static bool GenerateInfo(bool force, out string message)
        {
            var f = InfoChange.GetValue(out var data);
            if (f || force)
            {
                string sprintName = data.sprintKey.ToShortString();
                message =
$@"Hold <color=#FFFFFF>[{sprintName}]</color> + 
   -<color=#{(data.accept ? "B0E0B0>[Down]: Disable" : "E0B0B0>[Up]: Enable")} Force Accept</color>
   -<color=#{(data.ray ? "B0E0B0>[Right]: Disable" : "E0B0B0>[Left]: Enable")} Override Raycast</color>
   -<color=#FFFFFF>[Alt]</color>: Disable Both

X Rotation: {data.rot.x}
Z Rotation: {data.rot.z}
Hold <color=#FFFFFF>[{data.rotateKey.ToShortString()}] +</color>
   -<color=#B0E0B0>[Left]</color><color=#E0B0B0>[Right]</color>: Rotate X by 90
   -<color=#B0E0B0>[Up]</color><color=#E0B0B0>[Down]</color>: Rotate Z by 90

Hold <color=#FFFFFF>[Alt]</color>:
 <color=#{(data.alt ? "B0E0B0" : "E0B0B0")}>-Ignore permissions</color>
 <color=#{(data.alt && data.sprint ? "B0E0B0" : "E0B0B0")}>-[{sprintName}]: Ignore instability</color>

{(data.snugmode == SnugKeyMode.Toggle ? "" : "Hold ")}<color=#FFFFFF>[{data.snugkey.ToShortString()}]</color>: {(data.snugmode == SnugKeyMode.Toggle ? "Toggle" : data.snugmode == SnugKeyMode.HoldEnable ? "Enable" : "Disable")} snug building mode
<color=#FFFFFF>[{data.recursKey.ToShortString()}]</color>: Cycle recursive mode. Current: {data.recurs}{(
data.recurs != RecursiveMode.Single
    ? data.recursState == 0
        ? "\n   -<color=#FFFFFF>[LMB]</color>: Start Placement"
        : data.recursState == 2
        ? "\n   -<color=#FFFFFF>[LMB]</color>: Finish Placement\n   -<color=#FFFFFF>[RMB]</color>: Unset Second Point"
        : data.recurs == RecursiveMode.Line
        ? "\n   -<color=#FFFFFF>[LMB]</color>: Finish Placement\n   -<color=#FFFFFF>[RMB]</color>: Cancel Placement"
        : "\n   -<color=#FFFFFF>[LMB]</color>: Set Second Point\n   -<color=#FFFFFF>[RMB]</color>: Cancel Placement"
    : "")}
Hold <color=#{(data.match ? "B0E0B0" : "E0B0B0")}>[{data.matchKey.ToShortString()}]</color>: Lock placing to looking at
";
            }
            else
                message = string.Empty;
            return f;
        }

        public static GameObject CreateText(Transform canvas_transform, float x, float y, string text_to_print, int font_size, Color text_color, float width, float height, Font font, string name = "Text")
        {
            GameObject UItextGO = new GameObject("Text");
            UItextGO.transform.SetParent(canvas_transform, false);
            RectTransform trans = UItextGO.AddComponent<RectTransform>();
            trans.anchorMin = new Vector2(x, y);
            trans.anchorMax = trans.anchorMin + new Vector2(width, height);
            //trans.sizeDelta = new Vector2(width, height);
            //trans.anchoredPosition = new Vector2(x, y);
            Text text = UItextGO.AddComponent<Text>();
            text.text = text_to_print;
            text.font = font;
            text.fontSize = font_size;
            text.color = text_color;
            text.name = name;
            Shadow shadow = UItextGO.AddComponent<Shadow>();
            shadow.effectColor = new Color();
            return UItextGO;
        }
        public static void AddTextShadow(GameObject textObject, Color shadowColor, Vector2 shadowOffset)
        {
            Shadow shadow = textObject.AddComponent<Shadow>();
            shadow.effectColor = shadowColor;
            shadow.effectDistance = shadowOffset;
        }
        public static void CopyTextShadow(GameObject textObject, GameObject shadowSource)
        {
            Shadow sourcesShadow = shadowSource.GetComponent<Shadow>();
            if (sourcesShadow == null)
                sourcesShadow = shadowSource.GetComponentInChildren<Shadow>();
            AddTextShadow(textObject, sourcesShadow.effectColor, sourcesShadow.effectDistance);
        }

        public void ExtraSettingsAPI_Load()
        {
            SnugKey = ExtraSettingsAPI_GetKeybind("snugKeybind");
            RecursiveModeKey = ExtraSettingsAPI_GetKeybind("recursiveKey");
            MatchKey = ExtraSettingsAPI_GetKeybind("lockKey");
            
            ExtraSettingsAPI_SettingsClose();
        }
        public void ExtraSettingsAPI_SettingsClose()
        {
            var i = ExtraSettingsAPI_GetComboboxSelectedIndex("surfaceOverride") - 1;
            if (overrideSurface = i >= 0)
                surfaceType = (DPS)i;
        }
        public void ExtraSettingsAPI_SettingsOpen()
        {
            ExtraSettingsAPI_SetComboboxContent("surfaceOverride", Concat("None", Enum.GetNames(typeof(DPS))));
            ExtraSettingsAPI_SetComboboxSelectedIndex("surfaceOverride", overrideSurface ? (int)surfaceType + 1 : 0);
        }
        public bool ExtraSettingsAPI_HandleSettingVisible(string name) => false;

        public T[] Concat<T>(T value, T[] values)
        {
            var n = new T[values.Length+1];
            n[0] = value;
            values.CopyTo(n, 1);
            return n;
        }

        static bool ExtraSettingsAPI_Loaded = false;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void ExtraSettingsAPI_SetComboboxSelectedIndex(string SettingName, int value) { }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public int ExtraSettingsAPI_GetComboboxSelectedIndex(string SettingName) => -1;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ExtraSettingsAPI_SetComboboxContent(string SettingName, string[] value) { }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public Keybind ExtraSettingsAPI_GetKeybind(string SettingName) => null;


        [DllImport("kernel32")]
        public static extern void GetSystemTimeAsFileTime(out long value);

        public static IEnumerator HandleMultiplayerRecursive(List<Block> blocks, BlockCreator creator, Item_Base placing)
        {
            var toPlace = blocks.Select(b => (b.transform.localPosition, b.transform.localEulerAngles, b.dpsType)).ToList();
            var placed = true;
            var costs = 
                placing.settings_buildable.Placeable
                    ? new[] { new CostMultiple(new[] { placing }, 1) }
                    : placing.settings_recipe.NewCost;
            while (placed && toPlace.Count != 0)
            {
                placed = false;
                for (int i = 0; i < toPlace.Count; i++)
                {
                    var helper = new Patch_BlockCreator.MultiCraftHelper(costs, creator.playerInventory);
                    if (!helper.HasEnough())
                        goto exitLoop;
                    var b = toPlace[i];
                    var id = Patch_HandlePingPong.SendPlace(creator, placing.UniqueIndex, b.localPosition, b.localEulerAngles, b.dpsType);
                    bool success;
                    while (!Patch_HandlePingPong.HasRecievedResponce(id, out success))
                        yield return new WaitForEndOfFrame();
                    if (success)
                    {
                        if (GameModeValueManager.GetCurrentGameModeValue().playerSpecificVariables.unlimitedResources)
                            creator.playerInventory.RemoveCostMultiple(costs);
                        placed = true;
                        toPlace.RemoveAt(i);
                        i--;
                    }
                }
            }
        exitLoop:;
            yield break;
        }
    }

    public enum SupportMode
    {
        Disabled,
        Normal,
        Full
    }

    public enum SnugDetect
    {
        DoubleCast,
        SimpleNudge,
        SimpleNudgeX2
    }

    public enum SnugKeyMode
    {
        HoldEnable,
        Toggle,
        HoldDisable
    }

    public enum BuildMode
    {
        Vanilla,
        Line,
        Plane
    }
    
    public enum RecursiveMode
    {
        Single,
        Line,
        Rectangle
    }

    static class ExtentionMethods
    {

        public static bool IsOnlyXOrZ(this Vector3 vector) => vector.y == 0 && ((vector.x == 0 && vector.z != 0) || (vector.x != 0 && vector.z == 0));

        public static int GetMask(this int layer)
        {
            var result = 0;
            for (int i = 0; i < 32; i++)
                if (!Physics.GetIgnoreLayerCollision(layer, i))
                    result |= 1 << i;
            return result;
        }
        public static bool Collides(this Block block, Block otherBlock)
            => block.blockCollisionMask == null
            || (
                block.blockCollisionMask.ignoreAll
                ? block.blockCollisionMask.exceptSelf && block.buildableItem.UniqueIndex == otherBlock.buildableItem.UniqueIndex
                : !block.blockCollisionMask.IgnoresBlock(otherBlock.buildableItem)
            );
        public static IEnumerable<(Collider col, RaycastHit hit)> BoxCastAllColliders(this Block block, Vector3 vector, bool raycastTo = true)
        {
            if (block.blockCollisionMask != null && block.blockCollisionMask.ignoreAll && !block.blockCollisionMask.exceptSelf)
                yield break;
            var len = vector.magnitude;
            foreach (var collision in block.occupyingComponent.advancedCollisions)
                foreach (var collider in collision.colliders)
                {
                    Vector3 center = Helper.GetColliderCenter(collider);
                    if (raycastTo)
                        center -= vector;
                    foreach (var hit in Physics.BoxCastAll(center, Helper.GetColliderSize(collider) / 2, vector, collider.transform.rotation, len, LayerMasks.MASK_BlockCreatorOverlap))
                    {
                        var otherBlock = hit.collider.GetComponentInParent<Block>();
                        if (!otherBlock || (otherBlock != block && (block.Collides(otherBlock) || otherBlock.Collides(block))))
                            yield return (collider, hit);
                    }
                }
            yield break;
        }
        public static float ColliderThicknessInDirection(this Block block, Vector3 direction)
        {
            var irot = Quaternion.Inverse(Quaternion.LookRotation(direction));
            var min = float.PositiveInfinity;
            var max = float.NegativeInfinity;
            foreach (var collision in block.occupyingComponent.advancedCollisions)
                foreach (var collider in collision.colliders)
                {
                    Vector3 center = Helper.GetColliderCenter(collider);
                    Vector3 extents = Helper.GetColliderSize(collider) / 2;
                    var rotation = collider.transform.rotation;
                    for (int i = 0; i < 8; i++)
                    {
                        var point = extents;
                        if ((i & 1) == 0)
                            point.x = -point.x;
                        if ((i & 2) == 0)
                            point.y = -point.y;
                        if ((i & 4) == 0)
                            point.z = -point.z;
                        var dist = (irot * (center + rotation * point)).z;
                        if (dist < min)
                            min = dist;
                        if (dist > max)
                            max = dist;
                    }
                }
            return max - min;
        }
        public static float ColliderThicknessInDirection(this BoxCollider[] block, Vector3 direction)
        {
            var irot = Quaternion.Inverse(Quaternion.LookRotation(direction));
            var min = float.PositiveInfinity;
            var max = float.NegativeInfinity;
                foreach (var collider in block)
                {
                    Vector3 center = Helper.GetColliderCenter(collider);
                    Vector3 extents = Helper.GetColliderSize(collider) / 2;
                    var rotation = collider.transform.rotation;
                    for (int i = 0; i < 8; i++)
                    {
                        var point = extents;
                        if ((i & 1) == 0)
                            point.x = -point.x;
                        if ((i & 2) == 0)
                            point.y = -point.y;
                        if ((i & 4) == 0)
                            point.z = -point.z;
                        var dist = (irot * (center + rotation * point)).z;
                        if (dist < min)
                            min = dist;
                        if (dist > max)
                            max = dist;
                    }
                }
            return max - min;
        }
        public static HashSet<Collider> GetAllOverlaps(this Block block)
        {
            var found = new HashSet<Collider>();
            var check = new HashSet<Collider>();
            if (block.blockCollisionMask != null && block.blockCollisionMask.ignoreAll && !block.blockCollisionMask.exceptSelf)
                return found;
            foreach (var collision in block.occupyingComponent.advancedCollisions)
                foreach (var collider in collision.colliders)
                {
                    Vector3 center = Helper.GetColliderCenter(collider);
                    foreach (var otherCollider in Physics.OverlapBox(center, Helper.GetColliderSize(collider) / 2, collider.transform.rotation, LayerMasks.MASK_BlockCreatorOverlap))
                        if (check.Add(otherCollider))
                        {
                            var otherBlock = otherCollider.GetComponentInParent<Block>();
                            if (!otherBlock || (otherBlock != block && (block.Collides(otherBlock) || otherBlock.Collides(block))))
                                found.Add(otherCollider);
                        }
                }
            return found;
        }

        public static GameObject NewChild(this Transform transform, string name, params Type[] components)
        {
            var g = new GameObject(name);
            g.transform.SetParent(transform, false);
            if (components != null)
                foreach (var t in components)
                    try
                    {
                        g.AddComponent(t);
                    } catch (Exception e)
                    {
                        Debug.LogWarning(e);
                    }
            return g;
        }

        public static void PlaceBlock(this BlockCreator self, Item_Base blockItem, Vector3 position, Vector3 rotation, DPS dps)
        {
            if (Raft_Network.IsHost)
            {
                Message_BlockCreator_PlaceBlock message_BlockCreator_PlaceBlock = new Message_BlockCreator_PlaceBlock(Messages.BlockCreator_PlaceBlock, self, blockItem.UniqueIndex, SaveAndLoad.GetUniqueObjectIndex(), SaveAndLoad.GetUniqueObjectIndex(), NetworkUpdateManager.GetUniqueBehaviourIndex(), position, rotation, -1, dps);
                ComponentManager<Raft_Network>.Value.RPC(message_BlockCreator_PlaceBlock, Target.Other, EP2PSend.k_EP2PSendReliable, NetworkChannel.Channel_Game);
                self.CreateBlock(blockItem, message_BlockCreator_PlaceBlock.LocalPosition, message_BlockCreator_PlaceBlock.LocalEuler, message_BlockCreator_PlaceBlock.dpsType, -1, false, message_BlockCreator_PlaceBlock.blockObjectIndex, message_BlockCreator_PlaceBlock.networkedObjectIndex, message_BlockCreator_PlaceBlock.networkedBehaviourIndex);
                return;
            }
            Message_BlockCreator_PlaceBlock message = new Message_BlockCreator_PlaceBlock(Messages.BlockCreator_PlaceBlock, self, blockItem.UniqueIndex, 0U, 0U, 0U, position, rotation, -1, dps);
            RAPI.GetLocalPlayer().SendP2P(message, EP2PSend.k_EP2PSendReliable, NetworkChannel.Channel_Game);
        }

        static ConditionalWeakTable<Block, GameObject> collidables = new ConditionalWeakTable<Block, GameObject>();


        public static float Nearest(this Block block, Vector3 vector, Vector3 offset = default, float fallback = float.PositiveInfinity)
        {
            if (block.blockCollisionMask != null && block.blockCollisionMask.ignoreAll && !block.blockCollisionMask.exceptSelf)
                return fallback;
            if (!collidables.TryGetValue(block.buildableItem.settings_buildable.GetBlockPrefab(block.dpsType), out var checker))
            {
                checker = Object.Instantiate(block, Main.prefabHolder,false).gameObject;
                var l = new List<BoxCollider>();
                var occ = checker.GetComponent<OccupyingComponent>();
                occ.Awake();
                foreach (var collision in occ.advancedCollisions)
                    foreach (var collider in collision.colliders)
                        l.Add(collider);
                checker.AddComponent<CollisionsHolder>().colliders = l.ToArray();
                var changed = true;
                while (changed) {
                    changed = false;
                    foreach (var comp in checker.GetComponentsInChildren<Component>(true))
                        if (comp is Collider col)
                        {
                            if ((!col.isTrigger || Physics.queriesHitTriggers) && (comp.gameObject.layer == 30 || ((1 << comp.gameObject.layer) & LayerMasks.MASK_BlockCreatorOverlap) != 0))
                            {
                                comp.gameObject.layer = 30;
                                col.enabled = true;
                            }
                            else
                            {
                                Object.DestroyImmediate(comp);
                                changed = true;
                            }
                        }
                        else if (!(comp is Transform || comp is CollisionsHolder))
                        {
                            Object.DestroyImmediate(comp);
                            changed = true;
                        }
                }
                foreach (var transform in checker.GetComponentsInChildren<Transform>(true))
                    transform.gameObject.SetActive(true);
                collidables.Add(block.buildableItem.settings_buildable.GetBlockPrefab(block.dpsType), checker);
                checker.transform.SetParent(null, false);
            }
            else
                checker.SetActive(true);
            checker.transform.SetLocalPositionAndRotation(block.transform.localPosition, block.transform.localRotation);
            var layer = 1 << 30;
            float minDistance = float.MaxValue;
            var colliders = checker.GetComponent<CollisionsHolder>().colliders;
            if (vector.sqrMagnitude != 1)
                vector = vector.normalized;
            var len = colliders.ColliderThicknessInDirection(vector) * 2.1f;
            foreach (var collider in colliders)
            {
                var center = Helper.GetColliderCenter(collider) - (vector * (len / 2));
                var halfSize = Helper.GetColliderSize(collider) / 2;
                var rotation = collider.transform.rotation;
                foreach (var hit in Physics.BoxCastAll(center, halfSize, vector, rotation, len, layer))
                    if (hit.distance < minDistance && hit.collider.transform.IsChildOf(checker.transform))
                        minDistance = hit.distance;
            }
            checker.SetActive(false);
            if (minDistance == float.MaxValue)
                return fallback;
            return len / 2 - minDistance;
        }

        public static Vector3 Divide(this Vector3 a, Vector3 b) => new Vector3(a.x / b.x, a.y / b.y, a.z / b.z);
        public static Quaternion TransformRotation(this Transform t, Quaternion localRotation) => t ? t.rotation * localRotation : localRotation;
        public static Quaternion InverseTransformRotation(this Transform t, Quaternion globalRotation) => t ? Quaternion.Inverse(t.rotation) * globalRotation : globalRotation;

        public static unsafe int UnsafeCastInt(this float value) => *(int*)&value;
        public static unsafe float UnsafeCastFloat(this int value) => *(float*)&value;
    }

    public class CollisionsHolder : MonoBehaviour
    {
        public BoxCollider[] colliders;
    }

    [HarmonyPatch(typeof(Raft_Network), "HandleMessage")]
    static class Patch_HandlePingPong
    {
        static int pendingId = -1;
        static Dictionary<(int blockIndex,DPS dps,Vector3 position),int> waitingFor = new Dictionary<(int, DPS, Vector3), int>();
        static HashSet<int> pings = new HashSet<int>();
        static HashSet<int> handled = new HashSet<int>();
        static Dictionary<int, bool> pongs = new Dictionary<int, bool>();
        public static int SendPlace(BlockCreator creator,int uniqueIndex, Vector3 position, Vector3 rotation, DPS dps)
        {
            var id = pendingId--;
            if (pendingId == -1000000000)
                pendingId = -1;
            creator.network.SendP2P(creator.network.HostID, new Message_Compound(new List<Message>()
            {
                new Message_BlockCreator_PlaceBlock(Messages.BlockCreator_PlaceBlock,creator,uniqueIndex,0,0,0,position,rotation,-1,dps),
                new Message_PingPong(Messages.Ping,id.UnsafeCastFloat())
            }), EP2PSend.k_EP2PSendReliable, NetworkChannel.Channel_Game);
            waitingFor[(uniqueIndex, dps, position)] = id;
            pings.Add(id);
            return id;
        }
        public static bool HasRecievedResponce(int id, out bool success) => pongs.Remove(id, out success);
        static bool Prefix(Message message)
        {
            if (message is Message_PingPong msg)
            {
                var id = msg.timeSent.UnsafeCastInt();
                if (pings.Remove(id))
                {
                    pongs[id] = false;
                    foreach (var p in waitingFor)
                        if (p.Value == id)
                        {
                            waitingFor.Remove(p.Key);
                            break;
                        }
                    return false;
                }
                else if (handled.Remove(id))
                    return false;
            }
            else if (message is Message_BlockCreator_PlaceBlock place && waitingFor.Remove((place.uniqueBlockIndex,place.dpsType,place.LocalPosition), out var id))
            {
                handled.Add(id);
                pings.Remove(id);
                pongs[id] = true;
                Patch_NetworkManagerMessage.replicateNext = true;
            }
            return true;
        }
    }
    [HarmonyPatch(typeof(NetworkUpdateManager), "DeserializeSingleMessage")]
    static class Patch_NetworkManagerMessage
    {
        public static bool replicateNext = false;
        static void Prefix(ref bool __state)
        {
            if (replicateNext)
            {
                replicateNext = false;
                __state = true;
                Patch_CreateBlock.forceReplicating = true;
            }
        }
        static void Finalizer(bool __state)
        {
            if (__state)
                Patch_CreateBlock.forceReplicating = false;
        }
    }

    [HarmonyPatch(typeof(BlockCreator),"CreateBlock")]
    static class Patch_CreateBlock
    {
        public static bool forceReplicating = false;
        static void Prefix(ref bool replicating)
        {
            if (forceReplicating)
                replicating = true;
        }
    }

    [HarmonyPatch(typeof(BlockQuad), "AcceptsBlock")]
    static class Patch_accepter
    {
        static void Postfix(BlockQuad __instance, Item_Base blockItem, ref bool __result)
        {
            if (Main.overrideAccept || (Main.supportMode != 0 && __instance.transform.localPosition.IsOnlyXOrZ() && Patch_raycast.NeedsSupport(__instance.ParentBlock) && blockItem != null && Patch_raycast.NeedsSupport(blockItem.settings_buildable.GetBlockPrefab(0))))
                __result = true;
        }
    }

    [HarmonyPatch(typeof(BlockCreator), "GetQuadAtCursor")]
    static class Patch_raycast
    {
        static Dictionary<BlockQuad, Vector3> modifiedPositions = new Dictionary<BlockQuad, Vector3>();
        static Dictionary<BlockSurface, DPS> modifiedSides = new Dictionary<BlockSurface, DPS>();

        static void Prefix(BlockCreator __instance)
        {
            if (Main.supportMode != 0 && (!Main.overrideRayhit) && NeedsSupport(__instance.selectedBlock))
                UpdateNoSupport();
        }
        static void Postfix(BlockCreator __instance, ref BlockQuad __result)
        {
            RaycastHit? hit = null;
            if (Main.overrideRayhit)
            {
                RaycastHit raycastHit;
                if (__result != null && (Helper.HitAtCursor(out raycastHit, Player.UseDistance, LayerMasks.MASK_Item) || Helper.HitAtCursor(out raycastHit, Player.UseDistance, LayerMasks.MASK_RaycastInteractable)))
                {
                    hit = raycastHit;
                    Add(__result);
                    __result.transform.position = raycastHit.point;
                }
            }
            if (__result && __instance.quadSurface == null)
            {
                if (hit != null)
                    __instance.quadSurface = __result.GetSurfaceFromNormal(hit.Value.normal);
                if (__instance.quadSurface == null && __result.acceptableBuildSides.Length > 0)
                    __instance.quadSurface = __result.GetSurfaceFromNormal(Vector3.down);
                if (__instance.quadSurface == null && __result.acceptableBuildSides.Length > 0)
                    __instance.quadSurface = __result.acceptableBuildSides[0];
                if (__instance.quadSurface == null)
                    __result = null;
                if (__result && hit != null)
                    __instance.quadHit = hit.Value;
            }
            if (__instance.quadSurface != null && ((Main.overrideSurface && __instance.quadSurface.dpsType != Main.surfaceType) || (Patch_BlockCreator.startedDPS != ~DPS.Default && __instance.quadSurface.dpsType != Patch_BlockCreator.startedDPS)))
            {
                Add(__instance.quadSurface);
                __instance.quadSurface.dpsType = Patch_BlockCreator.startedDPS != ~DPS.Default ? Patch_BlockCreator.startedDPS : Main.surfaceType;
            }

        }
        public static void ResetPositions()
        {
            foreach (var item in modifiedPositions)
                if (item.Key != null)
                    item.Key.transform.localPosition = item.Value;
            modifiedPositions.Clear();
        }
        public static void ResetSides()
        {
            foreach (var item in modifiedSides)
                if (item.Key != null)
                    item.Key.dpsType = item.Value;
            modifiedSides.Clear();
        }
        public static void UpdateNoSupport()
        {
            foreach (Block block in ComponentManager<Raft>.Value.GetComponentsInChildren<Block>())
            {
                if (NeedsSupport(block))
                    foreach (BlockQuad quad in block.GetComponentsInChildren<BlockQuad>())
                        if (quad.transform.localPosition.IsOnlyXOrZ() && Add(quad))
                            quad.transform.localPosition = modifiedPositions[quad] * 2;
            }
        }
        public static bool Add(BlockQuad blockQuad) => modifiedPositions.TryAdd(blockQuad, blockQuad.transform.localPosition);
        public static bool Add(BlockSurface blockSurface) => modifiedSides.TryAdd(blockSurface, blockSurface.dpsType);

        public static bool NeedsSupport(Block block)
        {
            return block != null && block.buildableItem != null && block.IsWalkable() && !(block is Block_Foundation);
        }
    }

    [HarmonyPatch(typeof(BlockCreator), "SetGhostBlockPositionAndRotation")]
    static class Patch_Rotation
    {
        static void Postfix(BlockCreator __instance)
        {
            var v = __instance.selectedBlock.transform.localEulerAngles;
            if (Main.yRotationOverride != null)
                v.y = Main.yRotationOverride.Value;
            v += Main.rotation;
            __instance.selectedBlock.transform.localEulerAngles = v;
        }
    }

    [HarmonyPatch(typeof(BlockCreator), "SetBlockTypeToBuild", new Type[] { typeof(Item_Base) } )]
    static class Patch_Deselect
    {
        static bool p = false;
        static void Postfix(BlockCreator __instance)
        {
            if (Patch_raycast.NeedsSupport(__instance.selectedBlock) != p)
            {
                p = !p;
                Patch_raycast.ResetPositions();
                Patch_raycast.ResetSides();
            }
        }
    }

    [HarmonyPatch(typeof(Axe))] 
    static class Patch_Axe
    {
        public static Block moving;

        [HarmonyPatch("OnDeSelect")]
        [HarmonyPostfix]
        public static void OnDeSelect(Axe __instance, bool __state) => Patch_BlockPlacement.DeselectMoving(__instance.playerNetwork, __state);

        [HarmonyPatch("Update")]
        [HarmonyPrefix]
        static void Update_Prefix(Axe __instance)
        {
            if (moving && __instance.playerNetwork.IsLocalPlayer)
                Patch_MyInput.prevent.Add("LMB");
        }
        [HarmonyPatch("Update")]
        [HarmonyPostfix]
        static void Update_Postfix(Axe __instance)
        {
            return; // Disabled until proper data copy system sorted
            Patch_MyInput.prevent.Remove("LMB");
            if (Main.MoveBlockKey && __instance.playerNetwork.IsLocalPlayer && __instance.aimedAtBlock)
            {
                var target = __instance.aimedAtBlock;
                if (target && target.buildableItem)
                {
                    moving = target;
                    if (!__instance.playerNetwork.BlockCreator.gameObject.activeSelf)
                        __instance.playerNetwork.BlockCreator.gameObject.SetActive(true);
                    __instance.playerNetwork.BlockCreator.SetBlockTypeToBuild(target.buildableItem);
                }
            }
            if (__instance.playerNetwork.IsLocalPlayer && (object)moving != null && !moving) OnDeSelect(__instance, true);
        }
    }

    [HarmonyPatch]
    static class Patch_AdditionPlaceableStops
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(BuildMenu), "Update");
            yield return AccessTools.Method(typeof(BlockCreator), "HandleBlockPick");
        }
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator iL)
        {
            var code = instructions.ToList();
            for (int i = code.Count - 1; i >= 0; i--)
                if (code[i].operand is MethodInfo m && m.Name == "get_Placeable")
                    code.Insert(i + 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Patch_BlockPlacement), nameof(Patch_BlockPlacement.OverridePlaceable))));
            return code;
        }
    }

    [HarmonyPatch(typeof(BlockCreator), "Update")]
    static class Patch_BlockPlacement
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator iL)
        {
            var code = instructions.ToList();
            var f = true;
            for (int i = code.Count - 1; i >= 0; i--)
                if (code[i].operand is MethodInfo m)
                {
                    if (f && m.Name == "get_IsHost")
                    {
                        f = false;
                        var lbls = code[i].labels;
                        var lbl = iL.DefineLabel();
                        code[i].labels = new List<Label>() { lbl };
                        code.InsertRange(i, new[]
                        {
                            new CodeInstruction(OpCodes.Ldarg_0) {labels = lbls},
                            new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(Patch_BlockPlacement),nameof(OverridePlaceAction))),
                            new CodeInstruction(OpCodes.Brfalse_S,lbl),
                            new CodeInstruction(OpCodes.Ret)
                        });
                    }
                    else if (m.Name == "get_Placeable")
                        code.Insert(i + 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Patch_BlockPlacement), nameof(OverridePlaceable))));
                }
            return code;
        }
        static bool OverridePlaceAction(BlockCreator creator)
        {
            if (Patch_Axe.moving)
            {
                Patch_Axe.moving.GetBlockCreationData().RestoreBlock(creator.selectedBlock);
                var msg = new Message_BlockCreator_Create(Messages.BlockCreator_Create, creator, new[] { creator.selectedBlock.GetBlockCreationData() });
                // copy extra block stuff
                BlockCreator.RemoveBlockNetwork(Patch_Axe.moving, null, true);
                if (Raft_Network.IsHost)
                    creator.Deserialize(msg, default);
                ComponentManager<Raft_Network>.Value.RPC(msg, Raft_Network.IsHost ? Target.Other : Target.All, EP2PSend.k_EP2PSendReliable, NetworkChannel.Channel_Game);
                DeselectMoving(creator.playerNetwork, true);
                return true;
            }
            return false;
        }
        public static void DeselectMoving(Network_Player player, bool force)
        {
            if (force || (player.IsLocalPlayer && Patch_Axe.moving))
            {
                Patch_Axe.moving = null;
                player.BlockCreator.SetGhostBlockVisibility(false);
                player.BlockCreator.gameObject.SetActive(false);
            }
        }
        public static bool OverridePlaceable(bool original) => original || Patch_Axe.moving;
    }

    [HarmonyPatch]
    static class Patch_BlockCreator
    {
        public static List<Vector3> currentBlocks = new List<Vector3>();
        public static List<Block> ghostBlocks = new List<Block>();
        public static Vector3? point1;
        public static Vector3? point2;

        public static CostCollection cost;
        static int placingCount;
        static Item_Base lastPlacing; // use instead of setblocktypetobuild patch
        public static DPS startedDPS = ~DPS.Default;

        [HarmonyPatch(typeof(BlockCreator), "Update")]
        [HarmonyPrefix]
        static void BlockCreator_Update()
        {
            if (Main.recursiveMode != RecursiveMode.Single && !Patch_Axe.moving)
            {
                Patch_MyInput.prevent.Add("LMB");
                if (point1 != null)
                    Patch_MyInput.prevent.Add("RMB");
            }
        }
        [HarmonyPatch(typeof(BlockCreator), "Update")]
        [HarmonyPostfix]
        static void BlockCreator_Update(BlockCreator __instance)
        {
            Patch_MyInput.prevent.Remove("LMB");
            Patch_MyInput.prevent.Remove("RMB");
            if (!__instance.playerNetwork.IsLocalPlayer)
                return;

            if (cost == null)
            {
                var obj = ComponentManager<BuildMenu>.Value.costColletionCursor.gameObject;
                cost = Object.Instantiate(obj, ComponentManager<CanvasHelper>.Value.transform).GetComponent<CostCollection>();
                cost.transform.position = obj.transform.position;
                cost.GetComponent<RectTransform>().offsetMin = obj.GetComponent<RectTransform>().offsetMin;
                cost.GetComponent<RectTransform>().offsetMax = obj.GetComponent<RectTransform>().offsetMax;
                cost.GetComponent<RectTransform>().anchorMin = obj.GetComponent<RectTransform>().anchorMin;
                cost.GetComponent<RectTransform>().anchorMax = obj.GetComponent<RectTransform>().anchorMax;
            }
            if (Main.RecursiveModeKeyDown)
                Main.recursiveMode = Main.recursiveMode == RecursiveMode.Single ? RecursiveMode.Line : Main.recursiveMode == RecursiveMode.Line ? RecursiveMode.Rectangle : RecursiveMode.Single;
            if (Main.recursiveMode == RecursiveMode.Single || Patch_Axe.moving)
            {
                if (ghostBlocks.Count != 0)
                {
                    foreach (var b in ghostBlocks)
                        Object.Destroy(b.gameObject);
                    ghostBlocks.Clear();
                }
                point1 = null;
                point2 = null;
                if (startedDPS != ~DPS.Default)
                {
                    startedDPS = ~DPS.Default;
                    Patch_raycast.ResetSides();
                }
                placingCount = 0;
                cost.gameObject.SetActiveSafe(false);
                return;
            }
            //SingletonGeneric<GameManager>.Singleton.ghostMaterialGreen
            if (Main.recursiveMode == RecursiveMode.Line)
                point2 = null;
            var justDisabledPoint = false;
            if (__instance.playerInput.currentActionMap.name == "Player" && CustomInputConfig.Instance.WasPressedThisFrame(__instance.inputPlayerContext, "RMB"))
            {
                if (point2 != null)
                {
                    point2 = null;
                    justDisabledPoint = true;
                }
                else if (point1 != null)
                {
                    point1 = null;
                    cost.gameObject.SetActive(false);
                    justDisabledPoint = true;
                }
            }
            if (lastPlacing != __instance.selectedBuildableItem)
            {
                lastPlacing = __instance.selectedBuildableItem;
                if (ghostBlocks.Count != 0)
                {
                    foreach (var b in ghostBlocks)
                        Object.Destroy(b.gameObject);
                    ghostBlocks.Clear();
                }
                point1 = null;
                point2 = null;
                startedDPS = ~DPS.Default;
                Patch_raycast.ResetSides();
                placingCount = 0;

                if (__instance.selectedBuildableItem)
                    cost.ShowCost(new CostMultiple[] { new CostMultiple(new Item_Base[] { __instance.selectedBuildableItem }, 0) });
                cost.gameObject.SetActive(false);
            }
            if (__instance.selectedBlock)
            {
                var mainGhost = __instance.selectedBlock.transform.localPosition;
                if (justDisabledPoint || __instance.selectedBlock.gameObject.activeInHierarchy)
                {
                    if (point1 == null)
                        startedDPS = ~DPS.Default;
                    if (Main.MatchPlacementKey && Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out var hit, Player.UseDistance * 2, LayerMasks.MASK_Block))
                    {
                        var b = hit.collider.GetComponentInParent<Block>();
                        if (b?.buildableItem == __instance.selectedBlock.buildableItem)
                        {
                            if (startedDPS == ~DPS.Default)
                                startedDPS = b.dpsType;
                            __instance.selectedBlock.transform.localPosition = b.transform.localPosition;
                            __instance.selectedBlock.transform.localRotation = b.transform.localRotation;
                        }
                    }
                    var rot = __instance.selectedBlock.transform.localRotation;
                    int blockInd = 0;
                    Block GetNextBlock()
                    {
                        if (ghostBlocks.Count > blockInd)
                            return ghostBlocks[blockInd++];
                        var n = Object.Instantiate(__instance.selectedBlock, __instance.selectedBlock.transform.parent);
                        ghostBlocks.Add(n);
                        blockInd++;
                        return n;
                    }
                    void RemoveUnusedBlocks()
                    {
                        placingCount = blockInd;
                        if (ghostBlocks.Count <= blockInd)
                            return;
                        for (int i = blockInd; i < ghostBlocks.Count; i++)
                            if (ghostBlocks[i])
                                Object.Destroy(ghostBlocks[i].gameObject);
                        ghostBlocks.RemoveRange(blockInd, ghostBlocks.Count - blockInd);
                    }
                    if (point1 != null)
                    {
                        var firstLine = (point2 ?? mainGhost) - point1.Value;
                        if (firstLine == Vector3.zero)
                            goto skipPos;
                        if (point2 == null && MyInput.GetButton("Sprint"))
                        {
                            var x = Math.Abs(firstLine.x);
                            var y = Math.Abs(firstLine.y);
                            var z = Math.Abs(firstLine.z);
                            if (x >= y && x >= z)
                                firstLine = new Vector3(firstLine.x, 0, 0);
                            else if (y >= x && y >= z)
                                firstLine = new Vector3(0, firstLine.y, 0);
                            else
                                firstLine = new Vector3(0, 0, firstLine.z);
                            mainGhost = point1.Value + firstLine;
                        }

                        var firstDir = firstLine.normalized;
                        float firstDist;
                        if (__instance.selectedBlock.snapsToQuads)
                        {
                            var x = Math.Abs(firstLine.x / BlockCreator.BlockSize);
                            var y = Math.Abs(firstLine.y / BlockCreator.HalfFloorHeight);
                            var z = Math.Abs(firstLine.z / BlockCreator.BlockSize);
                            if (x >= y && x >= z)
                                firstDist = BlockCreator.BlockSize / Math.Abs(firstDir.x);
                            else if (y >= x && y >= z)
                                firstDist = BlockCreator.HalfFloorHeight / Math.Abs(firstDir.y);
                            else
                                firstDist = BlockCreator.BlockSize / Math.Abs(firstDir.z);
                        }
                        else
                            firstDist = __instance.selectedBlock.Nearest(firstDir);
                        var firstLineCount = (int)((firstLine.magnitude+0.01f) / firstDist) + 1;
                        if (point2 == null)
                            for (int i = 0; i < firstLineCount; i++)
                            {
                                Vector3 nextPos = firstDir * firstDist * i;
                                if (__instance.selectedBlock.snapsToQuads)
                                {
                                    nextPos.x = Mathf.Round(nextPos.x / BlockCreator.BlockSize) * BlockCreator.BlockSize;
                                    nextPos.y = Mathf.Round(nextPos.y / BlockCreator.HalfFloorHeight) * BlockCreator.HalfFloorHeight;
                                    nextPos.z = Mathf.Round(nextPos.z / BlockCreator.BlockSize) * BlockCreator.BlockSize;
                                }
                                nextPos += point1.Value;

                                var taken = false;
                                foreach (var p in currentBlocks)
                                    if ((p - nextPos).sqrMagnitude < 0.001f)
                                    {
                                        taken = true;
                                        break;
                                    }
                                if (!taken)
                                {
                                    var b = GetNextBlock();
                                    b.transform.localPosition = nextPos;
                                    b.transform.localRotation = rot;
                                    placingCount++;
                                }
                            }
                        else
                        {
                            var secondLine = mainGhost - point2.Value;
                            if (secondLine == Vector3.zero)
                                goto skipPos;
                            var lineRot = Quaternion.LookRotation(firstLine, secondLine);
                            secondLine = Quaternion.Inverse(lineRot) * secondLine;
                            secondLine = lineRot * new Vector3(0, secondLine.y, 0);
                            var secondDir = secondLine.normalized;
                            float secondDist;
                            if (__instance.selectedBlock.snapsToQuads)
                            {
                                var x = Math.Abs(secondLine.x / BlockCreator.BlockSize);
                                var y = Math.Abs(secondLine.y / BlockCreator.HalfFloorHeight);
                                var z = Math.Abs(secondLine.z / BlockCreator.BlockSize);
                                if (x >= y && x >= z)
                                    secondDist = BlockCreator.BlockSize / Math.Abs(secondDir.x);
                                else if (y >= x && y >= z)
                                    secondDist = BlockCreator.HalfFloorHeight / Math.Abs(secondDir.y);
                                else
                                    secondDist = BlockCreator.BlockSize / Math.Abs(secondDir.z);
                            }
                            else
                            {
                                secondDist = Math.Max(Math.Max(
                                    __instance.selectedBlock.Nearest(secondDir, fallback: 0),
                                    __instance.selectedBlock.Nearest(secondDir, firstDir * firstDist, 0)),
                                    __instance.selectedBlock.Nearest(secondDir, firstDir * -firstDist, 0));
                                if (secondDist == 0)
                                    secondDist = float.PositiveInfinity;
                            }
                            var secondLineCount = (int)((secondLine.magnitude + 0.01f) / secondDist) + 1;
                            for (int j = 0; j < secondLineCount; j++)
                                for (int i = 0; i < firstLineCount; i++)
                                {
                                    Vector3 nextPos = (firstDir * firstDist * i) + (secondDir * secondDist * j);
                                    if (__instance.selectedBlock.snapsToQuads)
                                    {
                                        nextPos.x = Mathf.Round(nextPos.x / BlockCreator.BlockSize) * BlockCreator.BlockSize;
                                        nextPos.y = Mathf.Round(nextPos.y / BlockCreator.HalfFloorHeight) * BlockCreator.HalfFloorHeight;
                                        nextPos.z = Mathf.Round(nextPos.z / BlockCreator.BlockSize) * BlockCreator.BlockSize;
                                    }
                                    nextPos += point1.Value;

                                    var taken = false;
                                    foreach (var p in currentBlocks)
                                        if ((p - nextPos).sqrMagnitude < 0.001f)
                                        {
                                            taken = true;
                                            break;
                                        }
                                    if (!taken)
                                    {
                                        var b = GetNextBlock();
                                        b.transform.localPosition = nextPos;
                                        b.transform.localRotation = rot;
                                        placingCount++;
                                    }
                                }
                        }
                        skipPos:;
                    }
                    RemoveUnusedBlocks();
                    foreach (var b in ghostBlocks)
                        b.occupyingComponent?.SetNewMaterial(__instance.CanBuildBlock(b) == BuildError.None ? SingletonGeneric<GameManager>.Singleton.ghostMaterialGreen : SingletonGeneric<GameManager>.Singleton.ghostMaterialRed);
                }
                if (__instance.playerInput.currentActionMap.name == "Player" && CustomInputConfig.Instance.WasPressedThisFrame(__instance.inputUse, "LMB"))
                {
                    if (point1 == null)
                    {
                        cost.gameObject.SetActive(__instance.selectedBuildableItem.settings_buildable.Placeable);
                        point1 = mainGhost;
                        currentBlocks.Clear();
                        foreach (var b in BlockCreator.GetPlacedBlocks())
                            if (b && b.buildableItem == __instance.selectedBuildableItem)
                                currentBlocks.Add(b.transform.localPosition);
                        if (startedDPS == ~DPS.Default)
                            startedDPS = __instance.selectedBlock.dpsType;
                    }
                    else if (point2 == null && Main.recursiveMode == RecursiveMode.Rectangle)
                    {
                        point2 = mainGhost;
                        currentBlocks.Clear();
                        foreach (var b in BlockCreator.GetPlacedBlocks())
                            if (b && b.buildableItem == __instance.selectedBuildableItem)
                                currentBlocks.Add(b.transform.localPosition);
                    }
                    else
                    {
                        var placing = __instance.selectedBuildableItem;
                        if (Raft_Network.IsHost)
                        {
                            var placed = true;
                            var costs = new MultiCraftHelper(
                                placing.settings_buildable.Placeable
                                    ? new[] { new CostMultiple(new[] { placing }, 1) }
                                    : placing.settings_recipe.NewCost,
                                __instance.playerInventory);
                            while (placed && ghostBlocks.Count != 0)
                            {
                                placed = false;
                                for (int i = 0; i < ghostBlocks.Count; i++)
                                {
                                    if (!costs.HasEnough())
                                        goto exitLoop;
                                    var b = ghostBlocks[i];
                                    if (__instance.CanBuildBlock(b) == BuildError.None)
                                    {
                                        costs.TakeItems();
                                        __instance.PlaceBlock(placing, b.transform.localPosition, b.transform.localEulerAngles, b.dpsType);
                                        placed = true;
                                        Object.Destroy(b.gameObject);
                                        ghostBlocks.RemoveAt(i);
                                        i--;
                                    }
                                }
                            }
                        }
                        else
                            __instance.playerNetwork.StartCoroutine(Main.HandleMultiplayerRecursive(ghostBlocks, __instance, placing));
                    exitLoop:;
                        if (__instance.playerInventory.GetSelectedHotbarSlot().IsEmpty)
                            __instance.playerInventory.hotbar.ReselectCurrentSlot();
                        else
                            __instance.SetBlockTypeToBuild(placing);
                        foreach (var b in ghostBlocks)
                            Object.Destroy(b.gameObject);
                        ghostBlocks.Clear();
                        startedDPS = ~DPS.Default;
                        Patch_raycast.ResetSides();
                        placingCount = 0;
                        point1 = null;
                        point2 = null;
                        cost.gameObject.SetActive(false);
                    }
                }
            }
            if (placingCount != 0)
            {
                if (__instance.selectedBuildableItem.settings_buildable.Placeable)
                    cost.costBoxes[0].SetRequiredAmount(placingCount);
                else
                {
                    ComponentManager<BuildMenu>.Value.costColletionCursor.ShowCost(__instance.selectedBuildableItem.settings_recipe.NewCost);
                    foreach (var box in ComponentManager<BuildMenu>.Value.costColletionCursor.costBoxes)
                        box.SetRequiredAmount(box.requiredAmount * placingCount);
                }
            }
            /*
            if (Main.instance.)
            for (int i = ghostBlocks.Count - 1; i >= 0; i--)
                if (!ghostBlocks[i])
                    ghostBlocks.RemoveAt(i);
            bool hasBlockSelected = __instance.selectedBlock && __instance.selectedBlock.gameObject.activeInHierarchy;
            bool isLocked = false;
            bool hasValidSelected = __instance.quadSurface != null && hasBlockSelected;
            bool buildKeyPressed = Main.ExtraSettingsAPI_Loaded && MyInput.GetButton(Main.buildKey);
            if (__instance.selectedBlock && buildKeyPressed)
            {
                if (ghostBlocks.Count == 0 && hasValidSelected)
                {
                    startedLocked = isLocked;
                    ghostBlocks.Add(Object.Instantiate(___selectedBuildablePrefab, lockedPivot));
                    ghostBlocks[0].OnStartingPlacement();
                    ghostBlocks[0].transform.localPosition = __instance.selectedBlock.transform.localPosition;
                    cost.gameObject.SetActive(___selectedBuildableItem.settings_buildable.Placeable);
                }
            }
            else if (ghostBlocks.Count != 0)
            {
                int placed = 0;
                bool unlimited = GameModeValueManager.GetCurrentGameModeValue().playerSpecificVariables.unlimitedResources;
                dontClear = true;
                foreach (Block block in ghostBlocks)
                {
                    if (___selectedBuildableItem)
                    {
                        if (startedLocked)
                            startedLocked = false;
                        else if (!buildKeyPressed && hasBlockSelected && (___selectedBuildableItem.settings_buildable.Placeable ? cost.MeetsRequirements() : buildCost.MeetsRequirements()) && __instance.CanBuildBlock(block) == BuildError.None && (unlimited || placed < placing))
                        {
                            __instance.PlaceBlock(___selectedBuildableItem, block.transform.localPosition, block.transform.localEulerAngles, ___quadSurface.dpsType);
                            placed++;
                        }
                    }
                    Object.Destroy(block?.gameObject);
                }
                dontClear = false;
                if ((___selectedBuildableItem && cost.costBoxes.Count != 0 && ___selectedBuildableItem.settings_buildable.Placeable ? cost.MeetsRequirements() : buildCost.MeetsRequirements()) && !unlimited)
                {
                    if (___selectedBuildableItem.settings_buildable.Placeable)
                        __instance.GetPlayerNetwork().Inventory.RemoveItem(___selectedBuildableItem.UniqueName, placed);
                    else
                        for (int i = 0; i < placed; i++)
                            __instance.GetPlayerNetwork().Inventory.RemoveCostMultiple(___selectedBuildableItem.settings_recipe.NewCost);
                }
                ghostBlocks.Clear();
                cost.gameObject.SetActive(false);
                if (placed > 0)
                    __instance.GetPlayerNetwork().Inventory.hotbar.ReselectCurrentSlot();
            }

            if (!__instance.selectedBlock)
                return;
            foreach (var block in ghostBlocks)
                block.gameObject.SetActive(hasValidSelected);
            foreach (var block in ghostBlocks)
            {
                block.transform.localRotation = __instance.selectedBlock.transform.localRotation;
                block.occupyingComponent?.SetNewMaterial(__instance.CanBuildBlock(block) == BuildError.None ? materialGreen : materialRed);
            }
            if (hasValidSelected && ghostBlocks.Count > 0)
            {
                var dir = ghostBlocks[0].transform.localPosition - __instance.selectedBlock.transform.localPosition;
                if (MyInput.GetButton("Sprint"))
                {
                    var x = Math.Abs(dir.x);
                    var y = Math.Abs(dir.y);
                    var z = Math.Abs(dir.z);
                    if (x >= y && x >= z)
                        dir = new Vector3(dir.x, 0, 0);
                    else if (y >= x && y >= z)
                        dir = new Vector3(0, dir.y, 0);
                    else
                        dir = new Vector3(0, 0, dir.z);
                }
                float dist;
                if (__instance.selectedBlock.snapsToQuads)
                {
                    var x = Math.Abs(dir.x / 1.5f);
                    var y = Math.Abs(dir.y / 1.21f);
                    var z = Math.Abs(dir.z / 1.5f);
                    if (x >= y && x >= z)
                        dist = 1.5f / Math.Abs(dir.normalized.x);
                    else if (y >= x && y >= z)
                        dist = 1.21f / Math.Abs(dir.normalized.y);
                    else
                        dist = 1.5f / Math.Abs(dir.normalized.z);
                }
                else
                {
                    ghostBlocks[0].SetOnOffColliderState(true);
                    dist = ghostBlocks[0].Closest(lockedPivot.TransformDirection(dir));
                    ghostBlocks[0].SetOnOffColliderState(false);
                }
                int blocks = 1;
                if (dist != 0)
                    blocks = (int)((dir.magnitude + (__instance.selectedBlock.snapsToQuads ? 0.1f : 0)) / dist) + 1;
                while (ghostBlocks.Count > blocks)
                {
                    Object.Destroy(ghostBlocks[1].gameObject);
                    ghostBlocks.RemoveAt(1);
                }
                int j = 0;
                for (int i = 1; i < blocks; i++)
                {
                    if (ghostBlocks.Count <= i)
                    {
                        var b = Object.Instantiate(___selectedBuildablePrefab, lockedPivot);
                        b.OnStartingPlacement();
                        b.transform.localRotation = ghostBlocks[0].transform.localRotation;
                        ghostBlocks.Add(b);
                    }
                    Vector3 pos = dir.normalized * dist * i;
                    if (__instance.selectedBlock.snapsToQuads)
                    {
                        pos.x = Mathf.Round(pos.x / 1.5f) * 1.5f;
                        pos.y = Mathf.Round(pos.y / 1.21f) * 1.21f;
                        pos.z = Mathf.Round(pos.z / 1.5f) * 1.5f;
                    }
                    ghostBlocks[i].transform.localPosition = ghostBlocks[0].transform.localPosition - pos;
                    if (__instance.CanBuildBlock(ghostBlocks[i]) != BuildError.None)
                    {
                        ghostBlocks[i].occupyingComponent?.SetNewMaterial(materialRed);
                        j++;
                    }
                    else
                        ghostBlocks[i].occupyingComponent?.SetNewMaterial(materialGreen);
                }
                var placing = blocks - j;
                if (startedLocked)
                    placing--;
                if (___selectedBuildableItem.settings_buildable.Placeable)
                {
                    cost.costBoxes[0].SetRequiredAmount(placing);
                    cost.gameObject.SetActive(true);
                }
                else
                {
                    buildCost.ShowCost(___selectedBuildableItem.settings_recipe.NewCost);
                    foreach (var box in buildCost.costBoxes)
                        box.SetRequiredAmount(Traverse.Create(box).Field("requiredAmount").GetValue<int>() * placing);
                }
            }*/
        }

        /*[HarmonyPatch(typeof(BlockCreator), "SetGhostBlockVisibility")]
        [HarmonyPostfix]
        static void BlockCreator_SetGhostBlockVisibility(bool visible)
        {
            if (!visible)
                foreach (Block block in ghostBlocks)
                    if (block != null)
                        block.gameObject.SetActive(false);
        }*/

        public class MultiCraftHelper
        {
            CostMultiple[] costs;
            int[] totals;
            PlayerInventory inventory;
            public MultiCraftHelper(CostMultiple[] costs, PlayerInventory inventory)
            {
                if (GameModeValueManager.GetCurrentGameModeValue().playerSpecificVariables.unlimitedResources)
                    return;
                this.inventory = inventory;
                this.costs = costs;
                totals =  new int[costs.Length];
                for (int i =0; i < totals.Length; i++)
                {
                    var total = 0;
                    var cost = costs[i];
                    for (int j = 0; j < cost.items.Length; j++)
                        total += inventory.GetItemCount(cost.items[j]);
                    totals[i] = total;
                }
            }
            public bool HasEnough()
            {
                if (totals == null)
                    return true;
                for (int i = 0; i < costs.Length; i++)
                    if (totals[i] < costs[i].amount)
                        return false;
                return true;
            }
            public void TakeItems()
            {
                if (totals == null)
                    return;
                inventory.RemoveCostMultiple(costs);
                for (int i = 0; i < costs.Length; i++)
                    totals[i] -= costs[i].amount;
            }
        }
    }

    [HarmonyPatch(typeof(BlockCreator), "SnapBuildRotation")]
    static class Patch_CheckRotation
    {
        static bool Prefix()
        {
            return false;
        }
    }

    [HarmonyPatch(typeof(Block), "IsStable")]
    static class Patch_ForceStable
    {
        static bool Prefix(Block __instance, ref bool __result)
        {
            if (Main.supportMode != 0 && (Patch_raycast.NeedsSupport(__instance) || Main.supportMode == SupportMode.Full))
            {
                __result = true;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(BlockCreator), "CanBuildBlock")]
    static class Patch_CheckBuildError
    {
        static void Postfix(BlockCreator __instance, Block block, ref BuildError __result)
        {
            if ((Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) && (__result == BuildError.PositionOccupied || __result == BuildError.PositionOccupied_Same || __result == BuildError.InWater || (__result == BuildError.NotStable && MyInput.GetButton("Sprint"))))
                __result = __instance.HasEnoughResourcesToBuild(block) ? BuildError.None : BuildError.NoResources;
            if (Patch_Axe.moving && __result == BuildError.NoResources)
                __result = BuildError.None;
        }
    }

    [HarmonyPatch(typeof(BlockCreator), "Deserialize")]
    static class Patch_RecieveNetworkMessage
    {
        static void Prefix(Message_NetworkBehaviour msg, out bool __state)
        {
            __state = Patch_CheckBlockOverlap.ignore;
            if (msg.Type == Messages.BlockCreator_PlaceBlock && msg is Message_BlockCreator_PlaceBlock message && (((int)message.LocalEuler.y / 360) & 1) != 0)
                Patch_CheckBlockOverlap.ignore = true;
        }
        static void Postfix(bool __state) => Patch_CheckBlockOverlap.ignore = __state;
    }

    [HarmonyPatch(typeof(Block), "IsOverlapping")]
    static class Patch_CheckBlockOverlap
    {
        public static bool ignore;
        static void Postfix(ref OverlappType __result)
        {
            if (ignore)
                __result = OverlappType.None;
        }
    }

    [HarmonyPatch(typeof(Network_Player), "SendP2P")]
    static class Patch_SendNetworkMessage
    {
        static void Prefix(Network_Player __instance, Message message)
        {
            if (!Raft_Network.IsHost && message.Type == Messages.BlockCreator_PlaceBlock && message is Message_BlockCreator_PlaceBlock msg)
                msg.LocalEuler += new Vector3(0,360,0);
        }
    }

    [HarmonyPatch(typeof(ZiplinePlayer), "Update")]
    static class Patch_ZiplinePlacement
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            code.InsertRange(code.FindIndex(x => x.opcode == OpCodes.Ldsfld && (x.operand as FieldInfo).Name == "MeshPathCreationIsObstructed") + 1, new[] {
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Patch_ZiplinePlacement),nameof(SkipCollisionCheck)))
                });
            return code;
        }
        static bool SkipCollisionCheck(bool alreadySkipped)
        {
            if (alreadySkipped)
                return alreadySkipped;
            return Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        }
    }

    [HarmonyPatch(typeof(BitmaskTile), "HasVisionToBitmaskTile")]
    static class Patch_CheckBitmaskVision
    {
        static bool Prefix(ref bool __result)
        {
            if (!Main.overridePipeCollisions)
                return true;
            __result = true;
            return false;
        }
    }

    public class timeDiff
    {
        float value;
        public float maxTime;
        bool gradient;
        float progress
        {
            get
            {
                if (gradient)
                    return (float)(0.5 - Math.Cos(Math.PI * value / maxTime) / 2);
                return (float)(value / maxTime);
            }
        }
        public timeDiff(float maxTime, bool useGradient = false, bool startAtMax = false)
        {
            this.maxTime = maxTime;
            gradient = useGradient;
            value = startAtMax ? maxTime : 0;
        }
        public bool Update(bool toMax, out float progress)
        {
            float timePassed = Time.deltaTime;
            var prev = value;
            value = Mathf.Clamp(value + (toMax ? timePassed : -timePassed), 0, maxTime);
            progress = this.progress;
            return prev != value;
        }
    }

    class Memory<R>
    {
        Func<R> getter;
        R last;
        public R Last => last;
        public Memory(Func<R> Getter) => getter = Getter;
        public static implicit operator Memory<R>(Func<R> Getter) => new Memory<R>(Getter);
        public bool GetValue(out R current) => GetValue(out _, out current);
        public bool GetValue(out R prev, out R current)
        {
            current = getter == null ? default : getter();
            prev = last;
            last = current;
            return !(current?.Equals(prev) ?? prev?.Equals(current) ?? true);
        }
    }

    class Memory<A, R>
    {
        Func<A, R> getter;
        R last;
        public R Last => last;
        public Memory(Func<A, R> Getter) => getter = Getter;
        public static implicit operator Memory<A, R>(Func<A, R> Getter) => new Memory<A, R>(Getter);
        public bool GetValue(A a, out R current) => GetValue(a, out _, out current);
        public bool GetValue(A a, out R prev, out R current)
        {
            current = getter == null ? default : getter(a);
            prev = last;
            last = current;
            return !(current?.Equals(prev) ?? prev?.Equals(current) ?? true);
        }
    }

    class Memory<A, B, R>
    {
        Func<A, B, R> getter;
        R last;
        public R Last => last;
        public Memory(Func<A, B, R> Getter) => getter = Getter;
        public static implicit operator Memory<A, B, R>(Func<A, B, R> Getter) => new Memory<A, B, R>(Getter);
        public bool GetValue(A a, B b, out R current) => GetValue(a, b, out _, out current);
        public bool GetValue(A a, B b, out R prev, out R current)
        {
            current = getter == null ? default : getter(a, b);
            prev = last;
            last = current;
            return !(current?.Equals(prev) ?? prev?.Equals(current) ?? true);
        }
    }

    [HarmonyPatch(typeof(MyInput))]
    static class Patch_MyInput
    {
        public static HashSet<string> prevent = new HashSet<string>();

        [HarmonyPatch("GetButton")]
        [HarmonyPostfix]
        static void GetButton(string identifier,ref bool __result)
        {
            if (prevent.Contains(identifier))
                __result = false;
        }

        [HarmonyPatch("GetButtonDown")]
        [HarmonyPostfix]
        static void GetButtonDown(string identifier, ref bool __result)
        {
            if (prevent.Contains(identifier))
                __result = false;
        }

        [HarmonyPatch("GetButtonUp")]
        [HarmonyPostfix]
        static void GetButtonUp(string identifier, ref bool __result)
        {
            if (prevent.Contains(identifier))
                __result = false;
        }
    }

    [HarmonyPatch(typeof(CustomInputConfig))]
    static class Patch_MyInput2
    {
        [HarmonyPatch("IsPressed", typeof(InputAction), typeof(string))]
        [HarmonyPostfix]
        static void GetButton(string key, ref bool __result)
        {
            if (Patch_MyInput.prevent.Contains(key))
                __result = false;
        }

        [HarmonyPatch("WasPressedThisFrame", typeof(InputAction), typeof(string))]
        [HarmonyPostfix]
        static void GetButtonDown(string key, ref bool __result)
        {
            if (Patch_MyInput.prevent.Contains(key))
                __result = false;
        }

        [HarmonyPatch("WasReleasedThisFrame", typeof(InputAction), typeof(string))]
        [HarmonyPostfix]
        static void GetButtonUp(string key, ref bool __result)
        {
            if (Patch_MyInput.prevent.Contains(key))
                __result = false;
        }
    }

    [HarmonyPatch(typeof(BlockCreator), "SetGhostBlockPositionAndRotation")]
    static class Patch_UpdateGhostLocation
    {
        static void Postfix(BlockCreator __instance, BlockQuad ___quadAtCursor, RaycastHit ___quadHit)
        {
            if (__instance.selectedBlock.snapsToQuads || ___quadAtCursor.snapToQuadPosition || !Main.SnugEnabled)
                return;
            var rot = Quaternion.LookRotation(Quaternion.LookRotation(___quadHit.normal, GameManager.Singleton.lockedPivot.forward) * Vector3.up, ___quadHit.normal);
            var iRot = Quaternion.Inverse(rot);
            var ray = iRot * ComponentManager<Network_Player>.Value.CameraTransform.forward;
            ray.y = 0;
            if (ray == Vector3.zero)
                return;
            var forward = ray = rot * ray.normalized;
            var rayLen = Math.Max(__instance.selectedBlock.ColliderThicknessInDirection(forward), 0.2f);
            ray *= rayLen;
            var originalPos = __instance.selectedBlock.transform.position;
            if (Main.snugDetect == SnugDetect.DoubleCast)
            {
                (RaycastHit hit, Vector3 basePoint, Vector3 slideDirection) first = default;
                first.hit.distance = float.PositiveInfinity;
                foreach (var hit in __instance.selectedBlock.BoxCastAllColliders(ray))
                {
                    if (hit.hit.distance <= 0 || first.hit.distance <= hit.hit.distance)
                        continue;
                    var hitNormal = hit.hit.normal;
                    if (hit.hit.collider.Raycast(new Ray(hit.hit.point - (forward * 0.1f), forward), out var hit2, 0.2f)) // pick normal based on if a raycast normal is the same as the boxcast `hit`
                    {
                        if (Vector3.Angle(hit2.normal, hitNormal) < 1)
                            hitNormal = hit2.normal;
                        else if (hit.col.Raycast(new Ray(hit.hit.point + (forward * 0.1f), -forward), out var hit3, 0.2f))
                            hitNormal = -hit3.normal;
                    }
                    var normal = iRot * hitNormal;
                    normal.y = 0;
                    if (normal.sqrMagnitude < 0.01)
                        continue;
                    first = (hit.hit, originalPos - ray + forward * hit.hit.distance, Quaternion.LookRotation(hitNormal, forward) * Vector3.up);
                }
                if (float.IsInfinity(first.hit.distance))
                    return;
                __instance.selectedBlock.transform.position = first.basePoint;
                var ray2 = first.slideDirection * ((rayLen - first.hit.distance) * (Quaternion.Inverse(Quaternion.LookRotation(first.slideDirection)) * forward).z);
                float second = float.PositiveInfinity;
                foreach (var hit in __instance.selectedBlock.BoxCastAllColliders(ray2, false))
                {
                    if (hit.hit.distance > 0 && second > hit.hit.distance)
                        second = hit.hit.distance;
                }
                __instance.selectedBlock.transform.position = first.basePoint + (float.IsInfinity(second) ? ray2 : (first.slideDirection * second));
            }
            else
            {
                for (int i = 0; i < (Main.snugDetect == SnugDetect.SimpleNudgeX2 ? 2 : 1); i++)
                {
                    var overlaps = __instance.selectedBlock.GetAllOverlaps();
                    var pos = __instance.selectedBlock.transform.position;
                    var start = pos - ray;
                    var cur = iRot * pos;
                    foreach (var hit in __instance.selectedBlock.BoxCastAllColliders(ray))
                    {
                        var hitNormal = hit.hit.normal;
                        if (hit.hit.collider.Raycast(new Ray(hit.hit.point - (forward * 0.1f), forward), out var hit2, 0.2f)) // pick normal based on if a raycast normal is the same as the boxcast `hit`
                        {
                            if (Vector3.Angle(hit2.normal, hitNormal) < 1)
                                hitNormal = hit2.normal;
                            else if (hit.col.Raycast(new Ray(hit.hit.point + (forward * 0.1f), -forward), out var hit3, 0.2f))
                                hitNormal = -hit3.normal;
                        }
                        var normal = iRot * hitNormal;
                        normal.y = 0;
                        if (normal.sqrMagnitude < 0.01)
                            continue;
                        if (!overlaps.Contains(hit.hit.collider))
                            continue;
                        var point = iRot * (start + forward * hit.hit.distance);
                        var hitRot = Quaternion.LookRotation(normal);
                        var iHitRot = Quaternion.Inverse(hitRot);
                        var localCur = iHitRot * cur;
                        var push = iHitRot * point;
                        if (push.z > localCur.z)
                        {
                            localCur.z = push.z;
                            cur = hitRot * localCur;
                        }
                    }
                    __instance.selectedBlock.transform.position = rot * cur;
                }
                if (__instance.selectedBlock.IsOverlapping() != OverlappType.None)
                    __instance.selectedBlock.transform.position = originalPos;
            }
        }
    }
}