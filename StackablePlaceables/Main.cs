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

namespace StackablePlaceables { 
    public class Main : Mod
    {
        private Harmony harmony;
        static string configPath = Path.Combine(SaveAndLoad.WorldPath, "StackablePlaceables.json");
        public static JSONObject Config = getSaveJson();
        public static bool overrideAccept = false;
        private static bool _oR = false;
        private static bool _oS = false;
        public static GameObject imageContainer;
        public static timeDiff control;
        public static string togglePopupKey;
        public static string moveBlockKey;
        public static bool pToggle;
        public static bool popupToggled;
        public static DPS surfaceType;
        public static float? yRotationOverride = null;
        public static int ignoreCount = 0;

        public static bool overrideRayhit
        {
            get
            {
                return _oR;
            }
            set
            {
                _oR = value;
                if (!value)
                    Patch_raycast.ResetPositions();
            }
        }
        public static bool overrideSurface
        {
            get
            {
                return _oS;
            }
            set
            {
                _oS = value;
                if (!value)
                    Patch_raycast.ResetSides();
            }
        }
        public static bool fullOverride
        {
            get
            {
                if (Config.IsNull || !Config.HasField("fullSupport"))
                    return false;
                return Config.GetField("fullSupport").b;
            }
            set
            {
                if (!Config.IsNull && Config.HasField("fullSupport"))
                    Config.SetField("fullSupport", value);
                else
                    Config.AddField("fullSupport", value);
                saveJson(Config);
            }
        }
        public static bool overrideSupport
        {
            get
            {
                if (Config.IsNull || !Config.HasField("overrideSupport"))
                    return false;
                return Config.GetField("overrideSupport").b;
            }
            set
            {
                if (!Config.IsNull && Config.HasField("overrideSupport"))
                    Config.SetField("overrideSupport", value);
                else
                    Config.AddField("overrideSupport", value);
                saveJson(Config);
                if (!value && !_oR)
                    Patch_raycast.ResetPositions();
            }
        }
        public static bool overridePipeCollisions
        {
            get
            {
                if (Config.IsNull || !Config.HasField("overridePipeCollisions"))
                    return false;
                return Config.GetField("overridePipeCollisions").b;
            }
            set
            {
                if (!Config.IsNull && Config.HasField("overridePipeCollisions"))
                    Config.SetField("overridePipeCollisions", value);
                else
                    Config.AddField("overridePipeCollisions", value);
                saveJson(Config);
            }
        }
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
        public static Vector3 rotation = Vector3.zero;
        public static Main instance;
        public static string logPrefix => "[" + instance.modlistEntry.jsonmodinfo.name + "]: ";
        public static Settings settingsController = ComponentManager<Settings>.Value;
        static Traverse settingsTraverse = Traverse.Create(settingsController);
        static GameObject OptionMenuContainer = settingsTraverse.Field("optionsCanvas").GetValue<GameObject>().transform.FindChildRecursively("OptionMenuParent").gameObject;
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
        public void Start()
        {
            instance = this;
            harmony = new Harmony("com.aidanamite.StackablePlaceables");
            harmony.PatchAll();
            control = new timeDiff(0.3f, true, true);
            if (RAPI.GetLocalPlayer() != null)
                AddImageObject(canvas.transform, 0);
            Log("Mod has been loaded!");
        }
        public override void WorldEvent_WorldLoaded() => AddImageObject(canvas.transform, 0);
        public override void WorldEvent_WorldUnloaded()
        {
            if (imageContainer != null)
            {
                Destroy(imageContainer);
                imageContainer = null;
            }
        }

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

        [ConsoleCommand(name: "toggleNoSupport", docs: "When active, allows certain blocks to be placed without support")]
        public static string MyCommand3(string[] args)
        {
            overrideSupport = !overrideSupport;
            return logPrefix+ "Is " + (overrideSupport ? "now" : "no longer") + " overriding support requirements";
        }

        [ConsoleCommand(name: "toggleFullSupport", docs: "When active, extends the No Support Override to affect ALL blocks")]
        public static string MyCommand4(string[] args)
        {
            fullOverride = !fullOverride;
            return logPrefix + "Is " + (fullOverride ? "now" : "no longer") + " overriding all support requirements";
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
            float x;
            try
            {
                x = float.Parse(args[0]);
            } catch
            {
                return logPrefix + args[0] + " cannot be parsed";
            }
            try
            {
                rotation = new Vector3(x,0,float.Parse(args[1]));
                return logPrefix + "Rotations modified";
            }
            catch
            {
                return logPrefix + args[1] + " cannot be parsed";
            }
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
            try
            {
                yRotationOverride = float.Parse(args[0]);
                return logPrefix + "Y rotation modified";
            }
            catch
            {
                return logPrefix + args[0] + " cannot be parsed";
            }
        }

        Memory<Text, (float width, float height)> DisplaySize = new Memory<Text, (float width, float height)>(x => (x.preferredWidth, x.preferredHeight));
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
            if (imageContainer != null)
            {
                if (pToggle != togglePopsKey)
                {
                    pToggle = !pToggle;
                    if (pToggle)
                        popupToggled = !popupToggled;
                }
                if (GenerateInfo(false, out var msg))
                    display.text = msg;
                var f = DisplaySize.GetValue(display, out var size);
                if (control.Update(!showTips, out var newValue) || f)
                {
                    var trans = imageContainer.GetComponent<Image>().rectTransform;
                    size.width += display.rectTransform.offsetMin.x - display.rectTransform.offsetMax.x;
                    size.height += display.rectTransform.offsetMin.y - display.rectTransform.offsetMax.y;
                    trans.offsetMin = new Vector2(size.width * newValue - size.width, size.height * -0.5f);
                    trans.offsetMax = new Vector2(size.width * newValue, size.height * 0.5f);
                }
            }
            var message = RAPI.ListenForNetworkMessagesOnChannel(MessageType.ChannelID);
            while (message != null && message.message != null && message.message is Message_InitiateConnection && message.message.Type == MessageType.MessageID)
            {
                var msg = (Message_InitiateConnection)message.message;
                if (msg.appBuildID == MessageType.IgnoreBuildCheck)
                    ignoreCount++;
                message = RAPI.ListenForNetworkMessagesOnChannel(MessageType.ChannelID);
            }
        }

        public void OnModUnload()
        {
            if (imageContainer != null)
            {
                Destroy(imageContainer);
                imageContainer = null;
            }
            harmony.UnpatchAll(harmony.Id);
            Patch_raycast.ResetPositions();
            Patch_raycast.ResetSides();
            Log("Mod has been unloaded!");
        }

        private static JSONObject getSaveJson()
        {
            JSONObject data;
            try
            {
                data = new JSONObject(File.ReadAllText(configPath));
            }
            catch
            {
                data = JSONObject.Create();
                saveJson(data);
            }
            return data;
        }

        private static void saveJson(JSONObject data)
        {
            try
            {
                File.WriteAllText(configPath, data.ToString());
            }
            catch (Exception err)
            {
                instance.Log("An error occured while trying to save settings: " + err.Message);
            }
        }
        public static void AddImageObject(Transform transform, float scale)
        {
            if (imageContainer == null)
            {
                OptionMenuContainer = settingsTraverse.Field("optionsCanvas").GetValue<GameObject>().transform.FindChildRecursively("OptionMenuParent").gameObject;
                GameObject backgroundImg = OptionMenuContainer.transform.FindChildRecursively("BrownBackground").gameObject;
                GameObject divider = OptionMenuContainer.transform.FindChildRecursively("Divider").gameObject;

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
        }
        static Memory<(KeyCode sprintKey, bool alt, bool accept, bool ray, Vector3 rot, KeyCode rotateKey, bool sprint)> InfoChange = new Memory<(KeyCode, bool, bool, bool, Vector3, KeyCode, bool)>(() => (
            MyInput.Keybinds["Sprint"].MainKey,
            Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt),
            overrideAccept,
            overrideRayhit,
            rotation,
            MyInput.Keybinds["Rotate"].MainKey,
            MyInput.GetButton("Sprint")
        ));
        public static bool GenerateInfo(bool force, out string message)
        {
            var f = InfoChange.GetValue(out var data);
            string sprintName = data.sprintKey.ToShortString();
            if (f || force)
            {
                var s = new StringBuilder();
                s.Append("Hold <color=#FFFFFF>[");
                s.Append(sprintName);
                s.Append("]</color> + \n <color=#");
                s.Append(data.accept ? "B0E0B0>-[Down]: Disable" : "E0B0B0>-[Up]: Enable");
                s.Append(" Force Accept</color>\n <color=#");
                s.Append(data.ray ? "B0E0B0>-[Right]: Disable" : "E0B0B0>-[Left]: Enable");
                s.Append(" Override Raycast</color>\n -<color=#FFFFFF>[Alt]</color>: Disable Both\n\nX Rotation: ");
                s.Append(data.rot.x);
                s.Append("\nZ Rotation: ");
                s.Append(data.rot.z);
                s.Append("\nHold <color=#FFFFFF>[");
                s.Append(data.rotateKey.ToShortString());
                s.Append("] +</color>\n   -<color=#B0E0B0>[Left]</color><color=#E0B0B0>[Right]</color>: Rotate X by 90\n   -<color=#B0E0B0>[Up]</color><color=#E0B0B0>[Down]</color>: Rotate Z by 90\n\nHold <color=#FFFFFF>[Alt]</color>:\n <color=#");
                s.Append(data.alt ? "B0E0B0" : "E0B0B0");
                s.Append(">-Ignore permissions</color>\n <color=#");
                s.Append(data.alt && data.sprint ? "B0E0B0" : "E0B0B0");
                s.Append(">-[");
                s.Append(sprintName);
                s.Append("]: Ignore instability</color>");
                message = s.ToString();
            }
            else
                message = "";
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
            togglePopupKey = ExtraSettingsAPI_GetKeybindName("popupKeybind");
            moveBlockKey = ExtraSettingsAPI_GetKeybindName("moveKeybind");
            ExtraSettingsAPI_SettingsClose();
        }
        public void ExtraSettingsAPI_SettingsClose()
        {
            overrideSupport = ExtraSettingsAPI_GetCheckboxState("supportOverride");
            fullOverride = ExtraSettingsAPI_GetComboboxSelectedIndex("supportMode") == 1;
            var i = ExtraSettingsAPI_GetComboboxSelectedIndex("surfaceOverride") - 1;
            if (overrideSurface = i >= 0)
                surfaceType = (DPS)i;
            overridePipeCollisions = ExtraSettingsAPI_GetCheckboxState("pipeOverride");
        }
        public void ExtraSettingsAPI_SettingsOpen()
        {
            ExtraSettingsAPI_SetCheckboxState("supportOverride", overrideSupport);
            ExtraSettingsAPI_SetComboboxSelectedIndex("supportMode", fullOverride ? 1 : 0);
            var names = Enum.GetNames(typeof(DPS)).ToList();
            names.Insert(0, "None");
            ExtraSettingsAPI_SetComboboxContent("surfaceOverride", names.ToArray());
            ExtraSettingsAPI_SetComboboxSelectedIndex("surfaceOverride", overrideSurface ? (int)surfaceType + 1 : 0);
            ExtraSettingsAPI_SetCheckboxState("pipeOverride", overridePipeCollisions);
        }

        static bool ExtraSettingsAPI_Loaded = false;
        public bool ExtraSettingsAPI_GetCheckboxState(string SettingName) => false;
        public void ExtraSettingsAPI_SetCheckboxState(string SettingName, bool value) { }
        public string ExtraSettingsAPI_GetKeybindName(string SettingName) => "";
        public void ExtraSettingsAPI_SetComboboxSelectedIndex(string SettingName, int value) { }
        public int ExtraSettingsAPI_GetComboboxSelectedIndex(string SettingName) => -1;
        public static string[] ExtraSettingsAPI_GetComboboxContent(string SettingName) => new string[0];
        public static void ExtraSettingsAPI_SetComboboxContent(string SettingName, string[] value) { }
    }

    static class ExtentionMethods
    {
        public static Y Join<X,Y>(this IEnumerable<X> collection, Func<X,Y> converter, Func<Y, Y, Y> joiner)
        {
            bool flag = false;
            Y r = default(Y);
            foreach (var v in collection)
                if (flag)
                    r = joiner(r, converter(v));
                else
                {
                    r = converter(v);
                    flag = true;
                }
            return r;
        }
        public static List<T> Cast<T>(this IEnumerable collection)
        {
            List<T> r = new List<T>();
            foreach (var o in collection)
                r.Add((T)o);
            return r;
        }
        public static string ToTitle(this string value)
        {
            var values = value.Split(' ');
            for (int i = 0; i < values.Length; i++)
                if (values[i] != "")
                    values[i] = char.ToUpper(values[i][0]) + values[i].Remove(0, 1).ToLower();
            return values.Join((x) => x," ");
        }

        public static string String(this byte[] bytes, int length = -1, int offset = 0)
        {
            if (bytes.Length % 2 == 1)
            {
                var n = new byte[bytes.Length + 1];
                bytes.CopyTo(n, 0);
                bytes = n;
            }
            string str = "";
            if (length == -1)
                length = (bytes.Length - offset) / 2;
            while (str.Length < length)
            {
                str += BitConverter.ToChar(bytes, offset + str.Length * 2);
            }
            return str;

        }
        public static string String(this List<byte> bytes) => bytes.ToArray().String();
        public static byte[] Bytes(this string str)
        {
            var data = new List<byte>();
            foreach (char chr in str)
                data.AddRange(BitConverter.GetBytes(chr));
            return data.ToArray();
        }
        public static int Integer(this byte[] bytes, int offset = 0) => BitConverter.ToInt32(bytes, offset);
        public static uint UInteger(this byte[] bytes, int offset = 0) => BitConverter.ToUInt32(bytes, offset);
        public static float Float(this byte[] bytes, int offset = 0) => BitConverter.ToSingle(bytes, offset);
        public static Vector3 Vector3(this byte[] bytes, int offset = 0) => new Vector3(bytes.Float(offset), bytes.Float(offset + 4), bytes.Float(offset + 8));
        public static byte[] Bytes(this int value) => BitConverter.GetBytes(value);
        public static byte[] Bytes(this uint value) => BitConverter.GetBytes(value);
        public static byte[] Bytes(this float value) => BitConverter.GetBytes(value);
        public static byte[] Bytes(this Vector3 value)
        {
            var data = new byte[12];
            value.x.Bytes().CopyTo(data, 0);
            value.y.Bytes().CopyTo(data, 4);
            value.z.Bytes().CopyTo(data, 8);
            return data;
        }

        public static void Broadcast(this Message message, NetworkChannel channel = MessageType.Channel) => ComponentManager<Raft_Network>.Value.RPC(message, Target.Other, EP2PSend.k_EP2PSendReliable, channel);
        public static void Send(this Message message, CSteamID steamID, NetworkChannel channel = MessageType.Channel) => ComponentManager<Raft_Network>.Value.SendP2P(steamID, message, EP2PSend.k_EP2PSendReliable, channel);

        public static bool IsOnlyXOrZ(this Vector3 vector) => vector.y == 0 && ((vector.x == 0 && vector.z != 0) || (vector.x != 0 && vector.z == 0));
    }

    [HarmonyPatch(typeof(BlockQuad), "AcceptsBlock")]
    static class Patch_accepter
    {
        static void Postfix(BlockQuad __instance, Item_Base blockItem, ref bool __result)
        {
            if (Main.overrideAccept || (Main.overrideSupport && __instance.transform.localPosition.IsOnlyXOrZ() && Patch_raycast.NeedsSupport(__instance.ParentBlock) && blockItem != null && Patch_raycast.NeedsSupport(blockItem.settings_buildable.GetBlockPrefab(0))))
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
            if (Main.overrideSupport && (!Main.overrideRayhit) && NeedsSupport(__instance.selectedBlock))
                UpdateNoSupport();
        }
        static void Postfix(BlockCreator __instance, ref BlockQuad __result, ref BlockSurface ___quadSurface, ref RaycastHit ___quadHit, Block ___selectedBlock)
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
            if (__result && ___quadSurface == null)
            {
                if (hit != null)
                    ___quadSurface = __result.GetSurfaceFromNormal(hit.Value.normal);
                if (___quadSurface == null && __result.acceptableBuildSides.Length > 0)
                    ___quadSurface = __result.GetSurfaceFromNormal(Vector3.down);
                if (___quadSurface == null && __result.acceptableBuildSides.Length > 0)
                    ___quadSurface = __result.acceptableBuildSides[0];
                if (___quadSurface == null)
                    __result = null;
                if (__result && hit != null)
                    ___quadHit = hit.Value;
            }
            if (Main.overrideSurface && ___quadSurface != null && ___quadSurface.dpsType != Main.surfaceType)
            {
                Add(___quadSurface);
                ___quadSurface.dpsType = Main.surfaceType;
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
        public static void OnDeSelect(Network_Player ___playerNetwork, bool __state)
        {
            if (__state || (___playerNetwork.IsLocalPlayer && moving))
            {
                moving = null;
                ___playerNetwork.BlockCreator.SetGhostBlockVisibility(false);
                ___playerNetwork.BlockCreator.gameObject.SetActive(false);
            }
        }

        [HarmonyPatch("Update")]
        [HarmonyPrefix]
        static void Update_Prefix(Network_Player ___playerNetwork)
        {
            if (moving && ___playerNetwork.IsLocalPlayer)
                Patch_MyInput.prevent.Add("LMB");
        }
        [HarmonyPatch("Update")]
        [HarmonyPostfix]
        static void Update_Postfix(Network_Player ___playerNetwork)
        {
            return; // Disabled until proper data copy system sorted
            Patch_MyInput.prevent.Remove("LMB");
            if (Main.MoveBlockKey && ___playerNetwork.IsLocalPlayer && Physics.Raycast(___playerNetwork.CameraTransform.position, ___playerNetwork.CameraTransform.forward, out var hit, Player.UseDistance * 2f, LayerMasks.MASK_Block))
            {
                var target = hit.transform.GetComponentInParent<Block>();
                if (target && target.buildableItem)
                {
                    moving = target;
                    if (!___playerNetwork.BlockCreator.gameObject.activeInHierarchy)
                        ___playerNetwork.BlockCreator.gameObject.SetActive(true);
                    ___playerNetwork.BlockCreator.SetBlockTypeToBuild(target.buildableItem);
                }
            }
            if (___playerNetwork.IsLocalPlayer && (object)moving != null && !moving) OnDeSelect(___playerNetwork, true);
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

    [HarmonyPatch(typeof(BlockCreator),"Update")]
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
                Patch_Axe.OnDeSelect(RAPI.GetLocalPlayer(), true);
                return true;
            }
            return false;
        }
        public static bool OverridePlaceable(bool original) => original || Patch_Axe.moving;
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
            if (Main.overrideSupport && (Patch_raycast.NeedsSupport(__instance) || Main.fullOverride))
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
        static void Postfix(Message_NetworkBehaviour msg, bool __result)
        {
            if (__result && msg.Type == Messages.BlockCreator_PlaceBlock && Main.ignoreCount > 0)
                Main.ignoreCount--;
        }
    }

    [HarmonyPatch(typeof(Block), "IsOverlapping")]
    class Patch_CheckBlockOverlap
    {
        static void Postfix(ref OverlappType __result)
        {
            if (Main.ignoreCount > 0)
            {
                __result = OverlappType.None;
                Main.ignoreCount--;
            }
        }
    }

    [HarmonyPatch(typeof(Network_Player), "SendP2P")]
    class Patch_SendNetworkMessage
    {
        static void Prefix(Network_Player __instance, Message message)
        {
            if (!Raft_Network.IsHost && message.Type == Messages.BlockCreator_PlaceBlock)
                Message_IgnoreBuildCheck.Message.Send(__instance.Network.HostID);
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

    static class MessageType
    {
        public const Messages MessageID = (Messages)110;
        public const int ChannelID = 1002;
        public const NetworkChannel Channel = (NetworkChannel)ChannelID;
        public const int IgnoreBuildCheck = 0;
    }

    static class Message_IgnoreBuildCheck
    {
        public static Message_InitiateConnection Message => new Message_InitiateConnection(MessageType.MessageID, MessageType.IgnoreBuildCheck, "");
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
}