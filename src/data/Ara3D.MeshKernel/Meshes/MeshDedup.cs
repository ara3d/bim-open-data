using Ara3D.Geometry;
using Ara3D.Models;

namespace Ara3D.MeshKernel;

/// <summary>Shares one mesh between parts whose geometry is identical, within a dedup scope. A cheap
/// fingerprint picks the bucket; an exact point-and-index comparison settles collisions.</summary>
public static class MeshDedup
{
    public static int GetOrAddMesh(
        Model3DBuilder builder,
        Dictionary<long, List<int>> meshBuckets,
        int dedupScope,
        TriangleMesh3D mesh)
    {
        var bucketKey = CombineBucketKey(dedupScope, ComputeMeshFingerprint(mesh));
        if (meshBuckets.TryGetValue(bucketKey, out var bucket))
        {
            foreach (var idx in bucket)
            {
                if (MeshesTopologyEqual(builder.Meshes[idx], mesh))
                    return idx;
            }
        }
        else
        {
            bucket = new List<int>();
            meshBuckets[bucketKey] = bucket;
        }

        var meshIdx = builder.Meshes.Count;
        builder.Meshes.Add(mesh);
        bucket.Add(meshIdx);
        return meshIdx;
    }

    static long CombineBucketKey(int dedupScope, int fingerprint)
        => ((long)dedupScope << 32) | (uint)fingerprint;

    /// <summary>
    /// Fast pre-filter for mesh dedup. Samples vertices across the mesh (not only the first ring:
    /// bolt caps share identical coarse hex heads while shanks differ) plus spread triangle indices.
    /// Collisions are resolved by <see cref="MeshesTopologyEqual"/> in <see cref="GetOrAddMesh"/>.
    /// </summary>
    static int ComputeMeshFingerprint(TriangleMesh3D mesh)
    {
        unchecked
        {
            var h = mesh.FaceIndices.Count;
            h = h * 397 ^ mesh.Points.Count;
            var bounds = MeshMeasure.Bounds(mesh);
            h = h * 397 ^ bounds.Min.X.GetHashCode();
            h = h * 397 ^ bounds.Min.Y.GetHashCode();
            h = h * 397 ^ bounds.Min.Z.GetHashCode();
            h = h * 397 ^ bounds.Max.X.GetHashCode();
            h = h * 397 ^ bounds.Max.Y.GetHashCode();
            h = h * 397 ^ bounds.Max.Z.GetHashCode();

            var pointCount = mesh.Points.Count;
            var sampleCount = Math.Min(pointCount, 16);
            for (var s = 0; s < sampleCount; s++)
            {
                var i = sampleCount <= 1 ? 0 : (int)((long)s * (pointCount - 1) / (sampleCount - 1));
                var p = mesh.Points[i];
                h = h * 397 ^ p.X.GetHashCode();
                h = h * 397 ^ p.Y.GetHashCode();
                h = h * 397 ^ p.Z.GetHashCode();
            }

            var faceCount = mesh.FaceIndices.Count;
            var triSamples = Math.Min(faceCount, 4);
            for (var s = 0; s < triSamples; s++)
            {
                var ti = triSamples <= 1 ? 0 : (int)((long)s * (faceCount - 1) / (triSamples - 1));
                var t = mesh.FaceIndices[ti];
                h = h * 397 ^ t.A;
                h = h * 397 ^ t.B;
                h = h * 397 ^ t.C;
            }

            return h;
        }
    }

    static bool MeshesTopologyEqual(TriangleMesh3D a, TriangleMesh3D b)
    {
        if (a.Points.Count != b.Points.Count || a.FaceIndices.Count != b.FaceIndices.Count)
            return false;

        for (var i = 0; i < a.Points.Count; i++)
        {
            if (!a.Points[i].Equals(b.Points[i]))
                return false;
        }

        for (var i = 0; i < a.FaceIndices.Count; i++)
        {
            var fa = a.FaceIndices[i];
            var fb = b.FaceIndices[i];
            if (fa.A != fb.A || fa.B != fb.B || fa.C != fb.C)
                return false;
        }

        return true;
    }
}
