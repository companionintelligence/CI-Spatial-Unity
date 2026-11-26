using System.Collections.Generic;
using Companion.Spatial.Vocabulary;

namespace Companion.Spatial.Extras
{
    /// <summary>
    /// A surface from the headset's scene model (Space Setup): a floor, a wall, a table top. It is the geometry of the
    /// room, where an <see cref="ScanObject"/> is a labelled thing in it. The web Scan contract has the same concept,
    /// <c>Plane</c>.
    /// </summary>
    public sealed class ScanPlane : ExtrasNode
    {
        /// <summary>Creates an empty plane.</summary>
        public ScanPlane()
        {
            Id = string.Empty;
            Label = string.Empty;
            Semantic = MetaLabels.Other;
            Pose = new ScanPose();
            Boundary = new List<Vec3>();
        }

        /// <summary>The plane's id (JSON <c>id</c>): the headset's anchor uuid.</summary>
        public string Id { get; set; }

        /// <summary>The canonical tag of the plane's label ("wall", "floor", "table"; JSON <c>label</c>).</summary>
        public string Label { get; set; }

        /// <summary>The Meta label of the plane, one of <see cref="MetaLabels.All"/> ("WALL_FACE"; JSON <c>semantic</c>).</summary>
        public string Semantic { get; set; }

        /// <summary>Where the plane is, in the space's coordinate frame (JSON <c>pose</c>).</summary>
        public ScanPose Pose { get; set; }

        /// <summary>The centre of the plane's rectangle on its own x and y axes (the axes of <see cref="Pose"/>), or null when unknown (JSON <c>center</c>).</summary>
        public Vec2? Center { get; set; }

        /// <summary>The size of the plane's rectangle along its own x and y axes, in metres, or null when unknown (JSON <c>size</c>).</summary>
        public Vec2? Size { get; set; }

        /// <summary>The outline of the plane in world space, as points; empty when the headset gave none; never null (JSON <c>boundary</c>).</summary>
        public List<Vec3> Boundary { get; }
    }
}
