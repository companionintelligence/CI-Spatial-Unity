using System;
using System.Collections.Generic;

namespace Companion.Spatial.Vocabulary
{
    /// <summary>
    /// The default tag table: the one place where the scanner's words are decided. One row per canonical tag.
    /// </summary>
    /// <remarks>
    /// <para>
    /// To add a synonym, add the phrase (singular, any case) to the row of the tag it means. To add a tag, add a row
    /// with the Meta label it stands for (<see cref="MetaLabels.Other"/> when Meta has no label). The tests check the
    /// whole table: every tag is canonical, every synonym normalizes to its row's tag, no phrase is in two rows.
    /// </para>
    /// <para>
    /// The choices behind the rows: a tag is the lower-case word the web Scan app and Meta's own label share ("couch",
    /// "screen", "wall art"), so one name covers the headset's label, the scanner's detector class and the web app.
    /// Synonyms that detectors swap for each other share a tag so one object stays one track (sofa and couch; TV,
    /// television and monitor are all a "screen"). A distinct class that Meta lumps into one label keeps its own tag
    /// (shelf and cabinet are both STORAGE, desk and table are both TABLE). Meta has no label for a chair, a cup or a
    /// laptop, so those are OTHER, but each is a tag, so tracking can tell a cup from a chair.
    /// </para>
    /// </remarks>
    internal static class DefaultTags
    {
        private static readonly TagEntry[] Rows =
        {
            // Meta's room structure and furniture labels.
            new TagEntry("floor", MetaLabels.Floor, "flooring"),
            new TagEntry("ceiling", MetaLabels.Ceiling),
            new TagEntry("wall", MetaLabels.WallFace, "wall face"),
            new TagEntry("invisible wall", MetaLabels.InvisibleWallFace, "invisible wall face"),
            new TagEntry("door", MetaLabels.DoorFrame, "door frame", "doorframe", "doorway", "door way"),
            new TagEntry("window", MetaLabels.WindowFrame, "window frame", "windowframe"),
            new TagEntry("wall art", MetaLabels.WallArt, "picture frame", "picture", "painting", "poster", "artwork", "photo frame", "framed picture", "wall picture"),
            new TagEntry("couch", MetaLabels.Couch, "sofa", "sofa bed", "sofabed", "loveseat", "love seat", "settee", "sectional", "sectional sofa"),
            new TagEntry("table", MetaLabels.Table, "coffee table", "dining table", "side table", "end table", "kitchen table", "dinner table"),
            new TagEntry("desk", MetaLabels.Table, "writing desk", "office desk", "computer desk"),
            new TagEntry("bed", MetaLabels.Bed, "bed frame", "bunk bed", "double bed", "mattress"),
            new TagEntry("lamp", MetaLabels.Lamp, "floor lamp", "table lamp", "desk lamp", "reading lamp", "standing lamp"),
            new TagEntry("plant", MetaLabels.Plant, "houseplant", "house plant", "potted plant", "indoor plant"),
            new TagEntry("screen", MetaLabels.Screen, "tv", "tv screen", "tv set", "television", "television set", "monitor", "computer monitor", "computer screen", "display", "flat screen", "flatscreen"),
            new TagEntry("shelf", MetaLabels.Storage, "bookshelf", "book shelf", "bookcase", "shelving", "shelving unit"),
            new TagEntry("cabinet", MetaLabels.Storage, "cupboard", "wardrobe", "closet", "dresser", "chest of drawers", "sideboard"),
            new TagEntry("storage", MetaLabels.Storage, "storage unit"),
            new TagEntry("global mesh", MetaLabels.GlobalMesh),
            new TagEntry("other", MetaLabels.Other),

            // Things detectors find that Meta has no label for: OTHER, but valid tags.
            new TagEntry("chair", MetaLabels.Other, "armchair", "office chair", "desk chair", "dining chair"),
            new TagEntry("cup", MetaLabels.Other, "coffee cup", "tea cup", "teacup"),
            new TagEntry("mug", MetaLabels.Other, "coffee mug"),
            new TagEntry("bottle", MetaLabels.Other, "water bottle"),
            new TagEntry("bowl", MetaLabels.Other),
            new TagEntry("book", MetaLabels.Other),
            new TagEntry("laptop", MetaLabels.Other, "laptop computer", "notebook computer"),
            new TagEntry("keyboard", MetaLabels.Other),
            new TagEntry("phone", MetaLabels.Other, "cell phone", "cellphone", "mobile phone", "smartphone", "telephone"),
            new TagEntry("remote control", MetaLabels.Other, "remote", "tv remote", "remote controller"),
            new TagEntry("vase", MetaLabels.Other),
            new TagEntry("clock", MetaLabels.Other, "wall clock", "alarm clock"),
            new TagEntry("box", MetaLabels.Other, "cardboard box"),
            new TagEntry("bag", MetaLabels.Other),
            new TagEntry("pillow", MetaLabels.Other, "cushion", "throw pillow")
        };

        /// <summary>The rows, in table order (a read-only view).</summary>
        internal static IReadOnlyList<TagEntry> Entries { get; } = Array.AsReadOnly(Rows);
    }
}
