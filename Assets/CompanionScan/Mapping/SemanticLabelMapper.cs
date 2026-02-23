using System.Collections.Generic;
using System.Globalization;

namespace CompanionScan.Mapping
{
    public static class SemanticLabelMapper
    {
        public static readonly string[] SupportedMetaLabels =
        {
            "CEILING",
            "DOOR_FRAME",
            "FLOOR",
            "INVISIBLE_WALL_FACE",
            "WALL_ART",
            "WALL_FACE",
            "WINDOW_FRAME",
            "COUCH",
            "TABLE",
            "BED",
            "LAMP",
            "PLANT",
            "SCREEN",
            "STORAGE",
            "GLOBAL_MESH",
            "OTHER"
        };

        private static readonly Dictionary<string, string> Map = new Dictionary<string, string>
        {
            { "couch", "COUCH" },
            { "sofa", "COUCH" },
            { "bed", "BED" },
            { "table", "TABLE" },
            { "desk", "TABLE" },
            { "lamp", "LAMP" },
            { "plant", "PLANT" },
            { "screen", "SCREEN" },
            { "tv", "SCREEN" },
            { "monitor", "SCREEN" },
            { "storage", "STORAGE" },
            { "cabinet", "STORAGE" },
            { "shelf", "STORAGE" },
            { "door", "DOOR_FRAME" },
            { "door_frame", "DOOR_FRAME" },
            { "window", "WINDOW_FRAME" },
            { "window_frame", "WINDOW_FRAME" },
            { "wall", "WALL_FACE" },
            { "wall_face", "WALL_FACE" },
            { "ceiling", "CEILING" },
            { "floor", "FLOOR" },
            { "wall_art", "WALL_ART" },
            { "other", "OTHER" }
        };

        public static string MapToMetaLabel(string rawLabel)
        {
            if (string.IsNullOrWhiteSpace(rawLabel))
            {
                return "OTHER";
            }

            var normalized = rawLabel.Trim().ToLower(CultureInfo.InvariantCulture);
            normalized = normalized.Replace(' ', '_').Replace('-', '_');
            if (Map.TryGetValue(normalized, out var mapped))
            {
                return mapped;
            }

            return "OTHER";
        }
    }
}

