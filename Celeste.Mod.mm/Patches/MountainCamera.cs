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

        public const int NameLength = 511;
        [XmlIgnore]
        [NonSerialized]
        public fixed char _Name[NameLength + 1];
        public string Name {
            get {
                fixed (char* ptr = _Name)
                    return Marshal.PtrToStringUni((IntPtr) ptr);
            }
            set {
                // Can probably be optimized.
                char[] chars = value.ToCharArray();
                int length = Math.Min(NameLength - 1, chars.Length);
                fixed (char* to = _Name) {
                    Marshal.Copy(chars, 0, (IntPtr) to, length);
                    for (int i = length; i < NameLength; i++) {
                        to[i] = '\0';
                    }
                }
            }
        }

    }
    public static class MountainCameraExt {

        // Mods can't access patch_ classes directly.
        // We thus expose any new members through extensions.

        unsafe static patch_MountainCamera ToPatch(this MountainCamera self)
            => *((patch_MountainCamera*) &self);
        unsafe static MountainCamera ToOrig(this patch_MountainCamera self)
            => *((MountainCamera*) &self);

        public static string GetName(this MountainCamera self)
            => ToPatch(self).Name;
        public static MountainCamera SetName(this MountainCamera self, string value) {
            patch_MountainCamera p = self.ToPatch();
            p.Name = value;
            return p.ToOrig();
        }

    }
}
