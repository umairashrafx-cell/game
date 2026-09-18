using System.Collections.Generic;
using UnityEngine;

namespace RoyalVault.Game.Visual3D
{
    /// <summary>
    /// Builds the low-poly primitives the jewelry is assembled from.
    ///
    /// Two decisions drive everything here:
    ///
    /// 1. **Gems are flat-shaded.** Every facet gets its own vertices and its own normal, so
    ///    light breaks sharply across the surface instead of smearing. That hard facet response
    ///    is what makes a stone read as cut glass rather than a coloured ball, and it costs
    ///    nothing at runtime.
    /// 2. **Everything stays low-poly.** A ring is ~480 triangles, a gem ~100. The target is a
    ///    budget Android phone, so the look has to come from lighting and material response,
    ///    not from density.
    ///
    /// Meshes are generated once and cached; nothing here allocates during play.
    /// </summary>
    public static class JewelryMeshFactory
    {
        private static readonly Dictionary<string, Mesh> Cache = new Dictionary<string, Mesh>();

        private static Mesh Cached(string key, System.Func<Mesh> build)
        {
            Mesh mesh;
            if (Cache.TryGetValue(key, out mesh) && mesh != null) return mesh;
            mesh = build();
            mesh.name = key;
            Cache[key] = mesh;
            return mesh;
        }

        // ------------------------------------------------------------------------------ torus

        /// <summary>A smooth-shaded ring band. Used for rings, bracelets, chains and loops.</summary>
        public static Mesh Torus(float majorRadius, float minorRadius, int majorSegments = 28, int minorSegments = 10)
        {
            string key = string.Format("torus_{0:F3}_{1:F3}_{2}_{3}", majorRadius, minorRadius, majorSegments, minorSegments);
            return Cached(key, () => BuildTorus(majorRadius, minorRadius, majorSegments, minorSegments));
        }

        private static Mesh BuildTorus(float majorRadius, float minorRadius, int majorSegments, int minorSegments)
        {
            Mesh mesh = new Mesh();

            int vertexCount = (majorSegments + 1) * (minorSegments + 1);
            Vector3[] vertices = new Vector3[vertexCount];
            Vector3[] normals = new Vector3[vertexCount];
            Vector2[] uvs = new Vector2[vertexCount];

            for (int i = 0; i <= majorSegments; i++)
            {
                float u = (float)i / majorSegments;
                float theta = u * Mathf.PI * 2f;
                Vector3 centre = new Vector3(Mathf.Cos(theta) * majorRadius, Mathf.Sin(theta) * majorRadius, 0f);
                Vector3 outward = new Vector3(Mathf.Cos(theta), Mathf.Sin(theta), 0f);

                for (int j = 0; j <= minorSegments; j++)
                {
                    float v = (float)j / minorSegments;
                    float phi = v * Mathf.PI * 2f;

                    Vector3 normal = outward * Mathf.Cos(phi) + Vector3.forward * Mathf.Sin(phi);
                    int index = i * (minorSegments + 1) + j;

                    vertices[index] = centre + normal * minorRadius;
                    normals[index] = normal;
                    uvs[index] = new Vector2(u, v);
                }
            }

            List<int> triangles = new List<int>(majorSegments * minorSegments * 6);
            for (int i = 0; i < majorSegments; i++)
            {
                for (int j = 0; j < minorSegments; j++)
                {
                    int a = i * (minorSegments + 1) + j;
                    int b = a + minorSegments + 1;

                    triangles.Add(a); triangles.Add(b); triangles.Add(a + 1);
                    triangles.Add(a + 1); triangles.Add(b); triangles.Add(b + 1);
                }
            }

            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // -------------------------------------------------------------------------------- gem

        /// <summary>
        /// A round brilliant cut: flat table on top, crown facets angling down to the girdle,
        /// then a pavilion tapering to a point underneath. Flat-shaded so every facet catches
        /// the light independently.
        /// </summary>
        public static Mesh BrilliantGem(int sides = 12, float girdleRadius = 0.5f, float tableRadius = 0.28f,
                                        float crownHeight = 0.22f, float pavilionDepth = 0.48f)
        {
            string key = string.Format("gem_{0}_{1:F2}_{2:F2}_{3:F2}_{4:F2}",
                                       sides, girdleRadius, tableRadius, crownHeight, pavilionDepth);
            return Cached(key, () => BuildGem(sides, girdleRadius, tableRadius, crownHeight, pavilionDepth));
        }

        private static Mesh BuildGem(int sides, float girdleRadius, float tableRadius,
                                     float crownHeight, float pavilionDepth)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();

            Vector3[] table = new Vector3[sides];
            Vector3[] girdle = new Vector3[sides];
            Vector3 apex = new Vector3(0f, -pavilionDepth, 0f);

            for (int i = 0; i < sides; i++)
            {
                float angle = (float)i / sides * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                table[i] = new Vector3(cos * tableRadius, crownHeight, sin * tableRadius);
                girdle[i] = new Vector3(cos * girdleRadius, 0f, sin * girdleRadius);
            }

            // Table, as a fan of flat triangles.
            for (int i = 1; i < sides - 1; i++)
            {
                AddFlatTriangle(vertices, triangles, table[0], table[i], table[i + 1]);
            }

            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;

                // Crown: table edge down to the girdle, as two facets per side.
                AddFlatTriangle(vertices, triangles, table[i], girdle[i], girdle[next]);
                AddFlatTriangle(vertices, triangles, table[i], girdle[next], table[next]);

                // Pavilion: girdle down to the point.
                AddFlatTriangle(vertices, triangles, girdle[next], girdle[i], apex);
            }

            Mesh mesh = new Mesh();
            MakeOutward(vertices, triangles);
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();   // per-facet, since no vertices are shared
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddFlatTriangle(List<Vector3> vertices, List<int> triangles,
                                            Vector3 a, Vector3 b, Vector3 c)
        {
            int index = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c);
            triangles.Add(index); triangles.Add(index + 1); triangles.Add(index + 2);
        }

        /// <summary>
        /// Forces every triangle to wind outward from the mesh centre.
        ///
        /// Getting winding right by hand across a table fan, crown facets and a pavilion is
        /// error-prone, and getting it wrong is not a subtle bug: back-facing triangles are
        /// culled, so a gem renders as crescents and wedges with chunks simply missing. Rather
        /// than reason about winding per face, each triangle is checked against the direction
        /// from the centre and flipped when it points the wrong way. Correct by construction.
        /// </summary>
        private static void MakeOutward(List<Vector3> vertices, List<int> triangles)
        {
            Vector3 centre = Vector3.zero;
            for (int i = 0; i < vertices.Count; i++) centre += vertices[i];
            centre /= Mathf.Max(1, vertices.Count);

            for (int t = 0; t < triangles.Count; t += 3)
            {
                Vector3 a = vertices[triangles[t]];
                Vector3 b = vertices[triangles[t + 1]];
                Vector3 c = vertices[triangles[t + 2]];

                Vector3 normal = Vector3.Cross(b - a, c - a);
                Vector3 outward = (a + b + c) / 3f - centre;

                if (Vector3.Dot(normal, outward) < 0f)
                {
                    int swap = triangles[t + 1];
                    triangles[t + 1] = triangles[t + 2];
                    triangles[t + 2] = swap;
                }
            }
        }

        // ------------------------------------------------------------------------------ band

        /// <summary>An open cylinder â€” the body of a crown, or a wide cuff.</summary>
        public static Mesh Band(float radius, float height, int sides = 24)
        {
            string key = string.Format("band_{0:F3}_{1:F3}_{2}", radius, height, sides);
            return Cached(key, () => BuildBand(radius, height, sides));
        }

        private static Mesh BuildBand(float radius, float height, int sides)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();

            float half = height * 0.5f;
            for (int i = 0; i < sides; i++)
            {
                float a0 = (float)i / sides * Mathf.PI * 2f;
                float a1 = (float)(i + 1) / sides * Mathf.PI * 2f;

                Vector3 b0 = new Vector3(Mathf.Cos(a0) * radius, -half, Mathf.Sin(a0) * radius);
                Vector3 b1 = new Vector3(Mathf.Cos(a1) * radius, -half, Mathf.Sin(a1) * radius);
                Vector3 t0 = new Vector3(b0.x, half, b0.z);
                Vector3 t1 = new Vector3(b1.x, half, b1.z);

                AddFlatTriangle(vertices, triangles, b0, t0, t1);
                AddFlatTriangle(vertices, triangles, b0, t1, b1);
            }

            Mesh mesh = new Mesh();
            MakeOutward(vertices, triangles);
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ----------------------------------------------------------------------------- spike

        /// <summary>A tapering point â€” a crown's peaks.</summary>
        public static Mesh Spike(float radius, float height, int sides = 6)
        {
            string key = string.Format("spike_{0:F3}_{1:F3}_{2}", radius, height, sides);
            return Cached(key, () => BuildSpike(radius, height, sides));
        }

        private static Mesh BuildSpike(float radius, float height, int sides)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();

            Vector3 tip = new Vector3(0f, height, 0f);
            for (int i = 0; i < sides; i++)
            {
                float a0 = (float)i / sides * Mathf.PI * 2f;
                float a1 = (float)(i + 1) / sides * Mathf.PI * 2f;

                Vector3 p0 = new Vector3(Mathf.Cos(a0) * radius, 0f, Mathf.Sin(a0) * radius);
                Vector3 p1 = new Vector3(Mathf.Cos(a1) * radius, 0f, Mathf.Sin(a1) * radius);

                AddFlatTriangle(vertices, triangles, p0, tip, p1);
                AddFlatTriangle(vertices, triangles, p0, p1, Vector3.zero);
            }

            Mesh mesh = new Mesh();
            MakeOutward(vertices, triangles);
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ------------------------------------------------------------------------------ bead

        /// <summary>A smooth sphere â€” pearls and bead accents.</summary>
        public static Mesh Bead(float radius, int rings = 10, int segments = 14)
        {
            string key = string.Format("bead_{0:F3}_{1}_{2}", radius, rings, segments);
            return Cached(key, () => BuildBead(radius, rings, segments));
        }

        private static Mesh BuildBead(float radius, int rings, int segments)
        {
            int vertexCount = (rings + 1) * (segments + 1);
            Vector3[] vertices = new Vector3[vertexCount];
            Vector3[] normals = new Vector3[vertexCount];
            Vector2[] uvs = new Vector2[vertexCount];

            for (int y = 0; y <= rings; y++)
            {
                float v = (float)y / rings;
                float phi = v * Mathf.PI;

                for (int x = 0; x <= segments; x++)
                {
                    float u = (float)x / segments;
                    float theta = u * Mathf.PI * 2f;

                    Vector3 normal = new Vector3(
                        Mathf.Sin(phi) * Mathf.Cos(theta),
                        Mathf.Cos(phi),
                        Mathf.Sin(phi) * Mathf.Sin(theta));

                    int index = y * (segments + 1) + x;
                    vertices[index] = normal * radius;
                    normals[index] = normal;
                    uvs[index] = new Vector2(u, v);
                }
            }

            List<int> triangles = new List<int>();
            for (int y = 0; y < rings; y++)
            {
                for (int x = 0; x < segments; x++)
                {
                    int a = y * (segments + 1) + x;
                    int b = a + segments + 1;

                    triangles.Add(a); triangles.Add(b); triangles.Add(a + 1);
                    triangles.Add(a + 1); triangles.Add(b); triangles.Add(b + 1);
                }
            }

            Mesh mesh = new Mesh();
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // ------------------------------------------------------------------------------- box

        /// <summary>A rounded-ish slab for trays and vault furniture.</summary>
        public static Mesh Slab(Vector3 size)
        {
            string key = string.Format("slab_{0:F3}_{1:F3}_{2:F3}", size.x, size.y, size.z);
            return Cached(key, () => BuildSlab(size));
        }

        private static Mesh BuildSlab(Vector3 size)
        {
            Vector3 h = size * 0.5f;
            Vector3[] corners =
            {
                new Vector3(-h.x, -h.y, -h.z), new Vector3( h.x, -h.y, -h.z),
                new Vector3( h.x, -h.y,  h.z), new Vector3(-h.x, -h.y,  h.z),
                new Vector3(-h.x,  h.y, -h.z), new Vector3( h.x,  h.y, -h.z),
                new Vector3( h.x,  h.y,  h.z), new Vector3(-h.x,  h.y,  h.z)
            };

            int[,] faces =
            {
                {0,1,2,3}, {5,4,7,6}, {4,5,1,0}, {6,7,3,2}, {7,4,0,3}, {5,6,2,1}
            };

            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();

            for (int f = 0; f < 6; f++)
            {
                AddFlatTriangle(vertices, triangles, corners[faces[f, 0]], corners[faces[f, 1]], corners[faces[f, 2]]);
                AddFlatTriangle(vertices, triangles, corners[faces[f, 0]], corners[faces[f, 2]], corners[faces[f, 3]]);
            }

            Mesh mesh = new Mesh();
            MakeOutward(vertices, triangles);
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
