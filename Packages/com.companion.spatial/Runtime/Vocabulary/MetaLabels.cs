using System;
using System.Collections.Generic;

namespace Companion.Spatial.Vocabulary
{
    /// <summary>
    /// The 16 semantic labels of Meta's Space Setup export, as the scanner lists them in
    /// <c>SemanticLabelMapper.SupportedMetaLabels</c> (same spelling, same order). Every tag the vocabulary knows maps
    /// to one of these. Two of them, <see cref="InvisibleWallFace"/> and <see cref="GlobalMesh"/>, come only from the
    /// headset's scene model: no detector class maps to them.
    /// </summary>
    public static class MetaLabels
    {
        /// <summary>The ceiling plane.</summary>
        public const string Ceiling = "CEILING";

        /// <summary>A door (the scene model labels the frame).</summary>
        public const string DoorFrame = "DOOR_FRAME";

        /// <summary>The floor plane.</summary>
        public const string Floor = "FLOOR";

        /// <summary>A wall face the user did not draw: the headset closes a gap in the room outline with it.</summary>
        public const string InvisibleWallFace = "INVISIBLE_WALL_FACE";

        /// <summary>Art hanging on a wall (a picture, a painting, a poster).</summary>
        public const string WallArt = "WALL_ART";

        /// <summary>A wall.</summary>
        public const string WallFace = "WALL_FACE";

        /// <summary>A window (the scene model labels the frame).</summary>
        public const string WindowFrame = "WINDOW_FRAME";

        /// <summary>A couch or sofa.</summary>
        public const string Couch = "COUCH";

        /// <summary>A table or a desk.</summary>
        public const string Table = "TABLE";

        /// <summary>A bed.</summary>
        public const string Bed = "BED";

        /// <summary>A lamp.</summary>
        public const string Lamp = "LAMP";

        /// <summary>A plant.</summary>
        public const string Plant = "PLANT";

        /// <summary>A TV or a monitor.</summary>
        public const string Screen = "SCREEN";

        /// <summary>A shelf, a cabinet or other storage.</summary>
        public const string Storage = "STORAGE";

        /// <summary>The room's whole scanned mesh.</summary>
        public const string GlobalMesh = "GLOBAL_MESH";

        /// <summary>Everything Meta has no label for (a chair, a cup, a laptop) and anything unknown.</summary>
        public const string Other = "OTHER";

        private static readonly string[] Labels =
        {
            Ceiling, DoorFrame, Floor, InvisibleWallFace, WallArt, WallFace, WindowFrame, Couch,
            Table, Bed, Lamp, Plant, Screen, Storage, GlobalMesh, Other
        };

        /// <summary>The 16 labels in the scanner's order (a read-only view).</summary>
        public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(Labels);

        /// <summary>True when <paramref name="label"/> is exactly one of the 16 labels (upper case, with underscores).</summary>
        public static bool IsMetaLabel(string label)
        {
            if (label == null)
            {
                return false;
            }

            for (int i = 0; i < Labels.Length; i++)
            {
                if (string.Equals(Labels[i], label, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
