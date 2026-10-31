using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Airships.Game
{
    // Builds faceted, vertex-colored meshes from code. One mesh per model keeps draw calls low on
    // phones, and swapping the colors passed in is how skins will work later.
    public class MeshKit
    {
        readonly List<Vector3> positions = new List<Vector3>();
        readonly List<Vector3> normals = new List<Vector3>();
        readonly List<Color32> colors = new List<Color32>();

        public Matrix4x4 Transform = Matrix4x4.identity;

        public int TriangleCount => positions.Count / 3;

        public Mesh Build(string name)
        {
            var mesh = new Mesh { name = name, indexFormat = positions.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(positions);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            var indices = new int[positions.Count];
            for (var i = 0; i < indices.Length; i++) indices[i] = i;
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        public void Tri(Vector3 a, Vector3 b, Vector3 c, Color color, Vector3 outward)
        {
            a = Transform.MultiplyPoint3x4(a);
            b = Transform.MultiplyPoint3x4(b);
            c = Transform.MultiplyPoint3x4(c);
            outward = Transform.MultiplyVector(outward);
            var normal = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(normal, outward) < 0f)
            {
                (b, c) = (c, b);
                normal = -normal;
            }
            normal.Normalize();
            Push(a, normal, color);
            Push(b, normal, color);
            Push(c, normal, color);
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color, Vector3 outward)
        {
            Tri(a, b, c, color, outward);
            Tri(a, c, d, color, outward);
        }

        public void DoubleSidedQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color, Vector3 outward)
        {
            Quad(a, b, c, d, color, outward);
            Quad(a, b, c, d, color, -outward);
        }

        // Box whose top can taper, for chunky hand-built shapes.
        public void Box(Vector3 center, Vector3 size, Color color, float topScale = 1f, Quaternion? rotation = null, Color? topColor = null)
        {
            var saved = Transform;
            Transform = saved * Matrix4x4.TRS(center, rotation ?? Quaternion.identity, Vector3.one);
            var half = size * 0.5f;
            var top = topColor ?? color;
            var t = topScale;
            var v = new[]
            {
                new Vector3(-half.x, -half.y, -half.z), new Vector3(half.x, -half.y, -half.z),
                new Vector3(half.x, -half.y, half.z), new Vector3(-half.x, -half.y, half.z),
                new Vector3(-half.x * t, half.y, -half.z * t), new Vector3(half.x * t, half.y, -half.z * t),
                new Vector3(half.x * t, half.y, half.z * t), new Vector3(-half.x * t, half.y, half.z * t),
            };
            Quad(v[0], v[1], v[2], v[3], color, Vector3.down);
            Quad(v[4], v[5], v[6], v[7], top, Vector3.up);
            Quad(v[0], v[1], v[5], v[4], color, Vector3.back);
            Quad(v[2], v[3], v[7], v[6], color, Vector3.forward);
            Quad(v[1], v[2], v[6], v[5], color, Vector3.right);
            Quad(v[3], v[0], v[4], v[7], color, Vector3.left);
            Transform = saved;
        }

        // Tapered cylinder between two points, with end caps.
        public void Cylinder(Vector3 from, Vector3 to, float fromRadius, float toRadius, int sides, Color color)
        {
            var axis = (to - from).normalized;
            var side = Vector3.Cross(axis, Mathf.Abs(axis.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
            var other = Vector3.Cross(axis, side);
            for (var i = 0; i < sides; i++)
            {
                var a0 = i * Mathf.PI * 2f / sides;
                var a1 = (i + 1) * Mathf.PI * 2f / sides;
                var d0 = side * Mathf.Cos(a0) + other * Mathf.Sin(a0);
                var d1 = side * Mathf.Cos(a1) + other * Mathf.Sin(a1);
                Quad(from + d0 * fromRadius, from + d1 * fromRadius, to + d1 * toRadius, to + d0 * toRadius, color, d0 + d1);
                if (fromRadius > 0f) Tri(from, from + d0 * fromRadius, from + d1 * fromRadius, color, -axis);
                if (toRadius > 0f) Tri(to, to + d0 * toRadius, to + d1 * toRadius, color, axis);
            }
        }

        // Faceted ball. Smooth blobs get rounded normals, which suits clouds.
        public void Blob(Vector3 center, Vector3 radii, Color color, int subdivisions = 1, float jitter = 0f, int seed = 0, bool smooth = false, Quaternion? rotation = null)
        {
            var rotate = rotation ?? Quaternion.identity;
            var random = new System.Random(seed);
            var cache = new Dictionary<Vector3, Vector3>();

            Vector3 Displace(Vector3 unit)
            {
                if (jitter <= 0f) return unit;
                if (!cache.TryGetValue(unit, out var displaced))
                {
                    displaced = unit * (1f + ((float)random.NextDouble() * 2f - 1f) * jitter);
                    cache[unit] = displaced;
                }
                return displaced;
            }

            Vector3 Place(Vector3 unit) => center + rotate * Vector3.Scale(unit, radii);

            foreach (var tri in Icosphere(subdivisions))
            {
                var a = Displace(tri.a);
                var b = Displace(tri.b);
                var c = Displace(tri.c);
                var outward = rotate * (tri.a + tri.b + tri.c);
                if (smooth) SmoothTri(Place(a), Place(b), Place(c), rotate * tri.a, rotate * tri.b, rotate * tri.c, color, outward);
                else Tri(Place(a), Place(b), Place(c), color, outward);
            }
        }

        void SmoothTri(Vector3 a, Vector3 b, Vector3 c, Vector3 na, Vector3 nb, Vector3 nc, Color color, Vector3 outward)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0f)
            {
                (b, c) = (c, b);
                (nb, nc) = (nc, nb);
            }
            Push(Transform.MultiplyPoint3x4(a), Transform.MultiplyVector(na).normalized, color);
            Push(Transform.MultiplyPoint3x4(b), Transform.MultiplyVector(nb).normalized, color);
            Push(Transform.MultiplyPoint3x4(c), Transform.MultiplyVector(nc).normalized, color);
        }

        void Push(Vector3 position, Vector3 normal, Color color)
        {
            positions.Add(position);
            normals.Add(normal);

            // Vertex colors skip Unity's automatic gamma conversion, so convert for linear rendering.
            colors.Add(color.linear);
        }

        struct UnitTri
        {
            public Vector3 a, b, c;
        }

        static readonly Dictionary<int, List<UnitTri>> IcosphereCache = new Dictionary<int, List<UnitTri>>();

        static List<UnitTri> Icosphere(int subdivisions)
        {
            if (IcosphereCache.TryGetValue(subdivisions, out var cached)) return cached;

            var p = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var v = new[]
            {
                new Vector3(-1, p, 0), new Vector3(1, p, 0), new Vector3(-1, -p, 0), new Vector3(1, -p, 0),
                new Vector3(0, -1, p), new Vector3(0, 1, p), new Vector3(0, -1, -p), new Vector3(0, 1, -p),
                new Vector3(p, 0, -1), new Vector3(p, 0, 1), new Vector3(-p, 0, -1), new Vector3(-p, 0, 1),
            };
            int[] faces =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };

            var tris = new List<UnitTri>();
            for (var i = 0; i < faces.Length; i += 3)
            {
                tris.Add(new UnitTri { a = v[faces[i]].normalized, b = v[faces[i + 1]].normalized, c = v[faces[i + 2]].normalized });
            }

            for (var level = 0; level < subdivisions; level++)
            {
                var next = new List<UnitTri>(tris.Count * 4);
                foreach (var t in tris)
                {
                    var ab = (t.a + t.b).normalized;
                    var bc = (t.b + t.c).normalized;
                    var ca = (t.c + t.a).normalized;
                    next.Add(new UnitTri { a = t.a, b = ab, c = ca });
                    next.Add(new UnitTri { a = t.b, b = bc, c = ab });
                    next.Add(new UnitTri { a = t.c, b = ca, c = bc });
                    next.Add(new UnitTri { a = ab, b = bc, c = ca });
                }
                tris = next;
            }

            IcosphereCache[subdivisions] = tris;
            return tris;
        }
    }
}
