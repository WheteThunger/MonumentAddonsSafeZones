using System;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using System.Collections.Generic;
using System.Text;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;

using InitializeCallback = System.Func<BasePlayer, string[], System.ValueTuple<bool, object>>;
using EditCallback = System.Func<BasePlayer, string[], UnityEngine.Component, Newtonsoft.Json.Linq.JObject, System.ValueTuple<bool, object>>;
using SpawnAddonCallback = System.Func<System.Guid, UnityEngine.Component, UnityEngine.Vector3, UnityEngine.Quaternion, Newtonsoft.Json.Linq.JObject, UnityEngine.Component>;
using KillAddonCallback = System.Action<UnityEngine.Component>;
using UpdateAddonCallback = System.Func<UnityEngine.Component, Newtonsoft.Json.Linq.JObject, UnityEngine.Component>;
using DisplayCallback = System.Action<UnityEngine.Component, Newtonsoft.Json.Linq.JObject, BasePlayer, System.Text.StringBuilder, float>;

namespace Oxide.Plugins;

[Info("Monument Addons Safe Zones", "WhiteThunder", "0.1.0")]
[Description("Addon plugin for Monument Addons that allows placing safe zones at monuments.")]
internal class MonumentAddonsSafeZones : CovalencePlugin
{
    #region Fields

    private const string AddonName = "safezone";

    [PluginReference]
    private readonly Plugin MonumentAddons;

    #endregion

    #region Hooks

    private void OnServerInitialized()
    {
        if (MonumentAddons == null)
        {
            LogError($"{nameof(MonumentAddons)} is not loaded, get it at https://umod.org");
            return;
        }

        RegisterCustomAddon();
    }

    private void OnPluginLoaded(Plugin plugin)
    {
        if (plugin.Name == nameof(MonumentAddons))
        {
            RegisterCustomAddon();
        }
    }

    #endregion

    #region Helpers

    private static bool TryParseVector3(string arg, out Vector3 vector)
    {
        vector = default;

        var parts = arg.Split(",");
        if (parts.Length != 3)
            return false;

        return float.TryParse(parts[0], out vector.x)
               && float.TryParse(parts[1], out vector.y)
               && float.TryParse(parts[2], out vector.z);
    }

    private static string FormatVector3(Vector3 vector)
    {
        return $"{vector.x:0.##},{vector.y:0.##},{vector.z:0.##}";
    }

    private void RegisterCustomAddon()
    {
        if (MonumentAddons.Call("API_RegisterCustomAddon", this, AddonName, new Dictionary<string, object>
                {
                    ["Initialize"] = new InitializeCallback(InitializeAddon),
                    ["Edit"] = new EditCallback(EditAddon),
                    ["Spawn"] = new SpawnAddonCallback(SpawnAddon),
                    ["Kill"] = new KillAddonCallback(KillAddon),
                    ["Update"] = new UpdateAddonCallback(UpdateAddon),
                    ["Display"] = new DisplayCallback(Display),
                }
            ) == null)
        {
            LogError("Error registering addon with Monument Addons.");
        }
    }

    private bool VerifyValidArgs(BasePlayer player, string cmd, string[] args, out ParsedArgs parsedArgs)
    {
        parsedArgs = default;

        for (var i = 0; i < args.Length - 1; i += 2)
        {
            var argName = args[i];
            var argValue = args[i + 1];

            switch (argName.ToLower())
            {
                case "offset":
                    if (!TryParseVector3(argValue, out var offset))
                    {
                        ReplyToPlayer(player.IPlayer, LangEntry.ErrorOffsetSyntax, argValue, cmd, AddonName);
                        return false;
                    }

                    parsedArgs.Offset = offset;
                    break;

                case "size":
                    if (!TryParseVector3(argValue, out var size))
                    {
                        ReplyToPlayer(player.IPlayer, LangEntry.ErrorSizeSyntax, argValue, cmd, AddonName);
                        return false;
                    }

                    parsedArgs.Size = size;
                    break;

                case "radius":
                    if (!float.TryParse(argValue, out var radius))
                    {
                        ReplyToPlayer(player.IPlayer, LangEntry.ErrorRadiusSyntax, argValue, cmd, AddonName);
                        return false;
                    }

                    parsedArgs.Radius = radius;
                    break;

                default:
                {
                    ReplyToPlayer(player.IPlayer, LangEntry.ErrorUnknownOption, argName);
                    return false;
                }
            }
        }

        if (parsedArgs is { Size: not null, Radius: not null })
        {
            ReplyToPlayer(player.IPlayer, LangEntry.ErrorSizeOrRadius);
            return false;
        }

        return true;
    }

    // Called when a player runs the "maspawn safezone" command.
    // Example: /maspawn safezone offset 0,5,0 radius 10
    private (bool, object) InitializeAddon(BasePlayer player, string[] args)
    {
        if (!VerifyValidArgs(player, "maspawn", args, out var parsedArgs))
            return (false, null);

        var safeZoneData = new SafeZoneData().WithArgs(parsedArgs);
        if (!safeZoneData.IsBox && safeZoneData.Radius <= 0)
        {
            safeZoneData.Radius = 10;
        }

        return (true, safeZoneData);
    }

    // Called when a player runs the "maedit safezone" command while looking at an existing instance.
    // Example: /maedit safezone offset 0,5,0 radius 10
    private (bool, object) EditAddon(BasePlayer player, string[] args, Component component, JObject data)
    {
        if (args.Length < 2)
        {
            ReplyToPlayer(player.IPlayer, LangEntry.GeneralSyntax, "maedit",AddonName);
            return (false, null);
        }

        if (!VerifyValidArgs(player, "maedit", args, out var parsedArgs))
            return (false, null);

        var safeZoneData = data?.ToObject<SafeZoneData>() ?? new SafeZoneData();
        return (true, safeZoneData.WithArgs(parsedArgs));
    }

    private Component SpawnAddon(Guid guid, Component monument, Vector3 position, Quaternion rotation, JObject data)
    {
        return SafeZoneComponent.Create(position, rotation, data?.ToObject<SafeZoneData>());
    }

    // Called when a player runs "maedit" if `EditAddon` succeeded.
    private Component UpdateAddon(Component component, JObject data)
    {
        if (component is not SafeZoneComponent safeZoneComponent)
            return component;

        var safeZoneData = data?.ToObject<SafeZoneData>();
        if (safeZoneData != null)
        {
            safeZoneComponent.UpdateData(safeZoneData);
        }

        return component;
    }

    private void KillAddon(Component component)
    {
        UnityEngine.Object.Destroy(component.gameObject);
    }

    private void Display(Component component, JObject data, BasePlayer player, StringBuilder sb, float duration)
    {
        var safeZoneData = data?.ToObject<SafeZoneData>();
        if (safeZoneData == null)
            return;

        if (safeZoneData.Offset != default)
        {
            sb.AppendLine(GetMessage(player.UserIDString, LangEntry.ShowOffset, FormatVector3(safeZoneData.Offset)));
        }

        sb.AppendLine(safeZoneData.IsBox
            ? GetMessage(player.UserIDString, LangEntry.ShowSize, FormatVector3(safeZoneData.Size))
            : GetMessage(player.UserIDString, LangEntry.ShowRadius, safeZoneData.Radius));

        var drawer = new Ddraw(player, duration, Color.green);
        var transform = component.transform;
        var offset = transform.TransformPoint(safeZoneData.Offset);

        drawer.Sphere(transform.position, 0.25f);

        if (safeZoneData.IsBox)
        {
            drawer.Box(offset, transform.rotation, safeZoneData.Extents);
        }
        else
        {
            drawer.Sphere(offset, safeZoneData.Radius);
        }
    }

    #endregion

    #region Helper Classes

    private struct ParsedArgs
    {
        public Vector3? Offset;
        public Vector3? Size;
        public float? Radius;
    }

    private struct Ddraw
    {
        public static void Sphere(BasePlayer player, float duration, Color color, Vector3 origin, float radius)
        {
            player.SendConsoleCommand("ddraw.sphere", duration, color, origin, radius);
        }

        public static void Line(BasePlayer player, float duration, Color color, Vector3 origin, Vector3 target)
        {
            player.SendConsoleCommand("ddraw.line", duration, color, origin, target);
        }

        public static void Arrow(BasePlayer player, float duration, Color color, Vector3 origin, Vector3 target, float headSize)
        {
            player.SendConsoleCommand("ddraw.arrow", duration, color, origin, target, headSize);
        }

        public static void Arrow(BasePlayer player, float duration, Color color, Vector3 center, Quaternion rotation, float length, float headSize)
        {
            var origin = center - rotation * Vector3.forward * length;
            var target = center + rotation * Vector3.forward * length;
            Arrow(player, duration, color, origin, target, headSize);
        }

        public static void Text(BasePlayer player, float duration, Color color, Vector3 origin, string text)
        {
            player.SendConsoleCommand("ddraw.text", duration, color, origin, text);
        }

        public static void Box(BasePlayer player, float duration, Color color, Vector3 center, Quaternion rotation, Vector3 extents)
        {
            var sphereRadius = 0.5f;

            var forwardUpperLeft = center + rotation * extents.WithX(-extents.x);
            var forwardUpperRight = center + rotation * extents;
            var forwardLowerLeft = center + rotation * extents.WithX(-extents.x).WithY(-extents.y);
            var forwardLowerRight = center + rotation * extents.WithY(-extents.y);

            var backLowerRight = center + rotation * -extents.WithX(-extents.x);
            var backLowerLeft = center + rotation * -extents;
            var backUpperRight = center + rotation * -extents.WithX(-extents.x).WithY(-extents.y);
            var backUpperLeft = center + rotation * -extents.WithY(-extents.y);

            Sphere(player, duration, color, forwardUpperLeft, sphereRadius);
            Sphere(player, duration, color, forwardUpperRight, sphereRadius);
            Sphere(player, duration, color, forwardLowerLeft, sphereRadius);
            Sphere(player, duration, color, forwardLowerRight, sphereRadius);

            Sphere(player, duration, color, backLowerRight, sphereRadius);
            Sphere(player, duration, color, backLowerLeft, sphereRadius);
            Sphere(player, duration, color, backUpperRight, sphereRadius);
            Sphere(player, duration, color, backUpperLeft, sphereRadius);

            Line(player, duration, color, forwardUpperLeft, forwardUpperRight);
            Line(player, duration, color, forwardLowerLeft, forwardLowerRight);
            Line(player, duration, color, forwardUpperLeft, forwardLowerLeft);
            Line(player, duration, color, forwardUpperRight, forwardLowerRight);

            Line(player, duration, color, backUpperLeft, backUpperRight);
            Line(player, duration, color, backLowerLeft, backLowerRight);
            Line(player, duration, color, backUpperLeft, backLowerLeft);
            Line(player, duration, color, backUpperRight, backLowerRight);

            Line(player, duration, color, forwardUpperLeft, backUpperLeft);
            Line(player, duration, color, forwardLowerLeft, backLowerLeft);
            Line(player, duration, color, forwardUpperRight, backUpperRight);
            Line(player, duration, color, forwardLowerRight, backLowerRight);
        }

        public static void Box(BasePlayer player, float duration, Color color, OBB obb)
        {
            Box(player, duration, color, obb.position, obb.rotation, obb.extents);
        }

        private BasePlayer _player;
        private Color _color;
        private float _duration;

        public Ddraw(BasePlayer player, float duration, Color? color = null)
        {
            _player = player;
            _color = color ?? Color.white;
            _duration = duration;
        }

        public void Sphere(Vector3 position, float radius, float? duration = null, Color? color = null)
        {
            Sphere(_player, duration ?? _duration, color ?? _color, position, radius);
        }

        public void Line(Vector3 origin, Vector3 target, float? duration = null, Color? color = null)
        {
            Line(_player, duration ?? _duration, color ?? _color, origin, target);
        }

        public void Arrow(Vector3 origin, Vector3 target, float headSize, float? duration = null, Color? color = null)
        {
            Arrow(_player, duration ?? _duration, color ?? _color, origin, target, headSize);
        }

        public void Arrow(Vector3 center, Quaternion rotation, float length, float headSize, float? duration = null, Color? color = null)
        {
            Arrow(_player, duration ?? _duration, color ?? _color, center, rotation, length, headSize);
        }

        public void Text(Vector3 position, string text, float? duration = null, Color? color = null)
        {
            Text(_player, duration ?? _duration, color ?? _color, position, text);
        }

        public void Box(Vector3 center, Quaternion rotation, Vector3 extents, float? duration = null, Color? color = null)
        {
            Box(_player, duration ?? _duration, color ?? _color, center, rotation, extents);
        }

        public void Box(OBB obb, float? duration = null, Color? color = null)
        {
            Box(_player, duration ?? _duration, color ?? _color, obb);
        }
    }

    #endregion

    #region Component

    private class SafeZoneComponent : ListComponent<SafeZoneComponent>
    {
        public static SafeZoneComponent Create(Vector3 position, Quaternion rotation, SafeZoneData data)
        {
            var gameObject = new GameObject();
            gameObject.transform.SetPositionAndRotation(position, rotation);

            var component = gameObject.AddComponent<SafeZoneComponent>();

            // Using a separate child object for offsetting the safe zone from the addon origin.
            var child = gameObject.CreateChild();
            child.layer = (int)Rust.Layer.Trigger;
            component._child = child;

            var trigger = child.AddComponent<TriggerSafeZone>();
            trigger.interestLayers = Rust.Layers.Mask.Player_Server;
            trigger.maxAltitude = -1;
            trigger.maxDepth = -1;

            component.UpdateData(data);
            return component;
        }

        private GameObject _child;
        private Collider _collider;

        public void UpdateData(SafeZoneData data)
        {
            _child.transform.localPosition = data.Offset;

            if (data.Size != Vector3.zero)
            {
                if (_collider is BoxCollider boxCollider)
                {
                    boxCollider.size = data.Size;
                }
                else
                {
                    Destroy(_collider);
                    _collider = CreateBoxCollider(data.Size);
                }
            }
            else
            {
                if (_collider is SphereCollider sphereCollider)
                {
                    sphereCollider.radius = data.Radius;
                }
                else
                {
                    Destroy(_collider);
                    _collider = CreateSphereCollider(data.Radius);
                }
            }
        }

        private BoxCollider CreateBoxCollider(Vector3 size)
        {
            var collider = _child.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = size;
            return collider;
        }

        private SphereCollider CreateSphereCollider(float radius)
        {
            var collider = _child.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.radius = radius;
            return collider;
        }
    }

    #endregion

    #region Data

    [JsonObject(MemberSerialization.OptIn)]
    private class SafeZoneData
    {
        [JsonProperty("Offset")]
        public Vector3 Offset;

        [JsonProperty("Size")]
        public Vector3 Size;

        [JsonProperty("Radius")]
        public float Radius;

        public bool IsBox => Size != default;
        public Vector3 Extents => Size / 2;

        public SafeZoneData WithArgs(ParsedArgs parsedArgs)
        {
            if (parsedArgs.Offset is {} offset)
            {
                Offset = offset;
            }

            if (parsedArgs.Size is { } size)
            {
                Size = size;
                Radius = default;
            }

            if (parsedArgs.Radius is { } radius)
            {
                Radius = radius;
                Size = default;
            }

            return this;
        }
    }

    #endregion

    #region Localization

    private abstract class LangEntry
    {
        public static List<LangEntry> AllLangEntries = new();

        public static readonly LangEntry2 GeneralSyntax = new("GeneralSyntax", "Syntax: {0} {1} offset <x>,<y>,<z> size <x>,<y>,<z> radius <number>");
        public static readonly LangEntry3 ErrorOffsetSyntax = new("Error.Offset.Syntax", "Invalid value for offset: '{0}'\nSyntax: {1} {2} offset <x>,<y>,<z>\nExample: {1} {2} offset 0,15,0");
        public static readonly LangEntry3 ErrorSizeSyntax = new("Error.Size.Syntax", "Invalid value for size: '{0}'\nSyntax: {1} {2} size <x>,<y>,<z>\nExample: {1} {2} size 30,30,30");
        public static readonly LangEntry3 ErrorRadiusSyntax = new("Error.Radius.Syntax", "Invalid value for radius: '{0}'\nSyntax: {1} {2} radius <number>\nExample: {1} {2} radius 50");
        public static readonly LangEntry1 ErrorUnknownOption = new("Error.UnknownOption", "Error: Unrecognized option: '{0}'");
        public static readonly LangEntry0 ErrorSizeOrRadius = new("Error.SizeOrRadius", "You cannot specify both size and radius. Use size for a box zone, or radius for a sphere zone.");

        public static readonly LangEntry1 ShowOffset = new("Show.Offset", "Offset: {0}");
        public static readonly LangEntry1 ShowSize = new("Show.Size", "Size: {0}");
        public static readonly LangEntry1 ShowRadius = new("Show.Radius", "Radius: {0}");

        public string Name;
        public string English;

        protected LangEntry(string name, string english)
        {
            Name = name;
            English = english;

            AllLangEntries.Add(this);
        }
    }

    private class LangEntry0 : LangEntry
    {
        public LangEntry0(string name, string english) : base(name, english) {}
    }

    private class LangEntry1 : LangEntry
    {
        public LangEntry1(string name, string english) : base(name, english) {}
    }

    private class LangEntry2 : LangEntry
    {
        public LangEntry2(string name, string english) : base(name, english) {}
    }

    private class LangEntry3 : LangEntry
    {
        public LangEntry3(string name, string english) : base(name, english) {}
    }

    private class LangEntry4 : LangEntry
    {
        public LangEntry4(string name, string english) : base(name, english) {}
    }

    private class LangEntry5 : LangEntry
    {
        public LangEntry5(string name, string english) : base(name, english) {}
    }

    private string GetMessage(string playerId, string langKey) => lang.GetMessage(langKey, this, playerId);
    private string GetMessage(string playerId, LangEntry0 langEntry) => GetMessage(playerId, langEntry.Name);
    private string GetMessage(string playerId, LangEntry1 langEntry, object arg1) => string.Format(GetMessage(playerId, langEntry.Name), arg1);
    private string GetMessage(string playerId, LangEntry2 langEntry, object arg1, object arg2) => string.Format(GetMessage(playerId, langEntry.Name), arg1, arg2);
    private string GetMessage(string playerId, LangEntry3 langEntry, object arg1, object arg2, object arg3) => string.Format(GetMessage(playerId, langEntry.Name), arg1, arg2, arg3);

    private void ReplyToPlayer(IPlayer player, LangEntry0 langEntry) => player.Reply(GetMessage(player.Id, langEntry));
    private void ReplyToPlayer(IPlayer player, LangEntry1 langEntry, object arg1) => player.Reply(GetMessage(player.Id, langEntry, arg1));
    private void ReplyToPlayer(IPlayer player, LangEntry2 langEntry, object arg1, object arg2) => player.Reply(GetMessage(player.Id, langEntry, arg1, arg2));
    private void ReplyToPlayer(IPlayer player, LangEntry3 langEntry, object arg1, object arg2, object arg3) => player.Reply(GetMessage(player.Id, langEntry, arg1, arg2, arg3));

    protected override void LoadDefaultMessages()
    {
        var englishLangKeys = new Dictionary<string, string>();

        foreach (var langEntry in LangEntry.AllLangEntries)
        {
            englishLangKeys[langEntry.Name] = langEntry.English;
        }

        lang.RegisterMessages(englishLangKeys, this);
    }

    #endregion
}
