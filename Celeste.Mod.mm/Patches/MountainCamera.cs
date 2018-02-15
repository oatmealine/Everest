#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Celeste.Mod.Meta;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace Celeste {
    // AreaKey is a struct.
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    unsafe struct patch_MountainCamera {

        public Vector3 Position;

        public Vector3 Target;

        public Quaternion Rotation;

        public string Name;

    }
    public static class MountainCameraExt {

        // Mods can't access patch_ classes directly.
        // We thus expose any new members through extensions.

        public static string GetName(this MountainCamera self)
            => ((patch_MountainCamera) (object) self).Name;
        public static MountainCamera SetName(this MountainCamera self, string value) {
            patch_MountainCamera p = (patch_MountainCamera) (object) self;
            p.Name = value;
            return (MountainCamera) (object) p;
        }

    }
}
