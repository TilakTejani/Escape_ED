using UnityEngine;
using System.Collections.Generic;

namespace EscapeED
{
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class Arrow : MonoBehaviour
    {
        [Header("Visuals")]
        public Material arrowMaterial;

        [Header("Shape")]
        public float lineWidth     = 0.08f;
        public float tipLength     = 0.16f;
        public float surfaceOffset = 0.005f;

        private MeshFilter   mf;
        private MeshRenderer mr;

        void Awake()
        {
            mf = GetComponent<MeshFilter>();
            mr = GetComponent<MeshRenderer>();
            if (arrowMaterial != null) mr.material = arrowMaterial;
        }

        /// <summary>
        /// Builds the arrow mesh.
        /// allNormals[i] = all face normals for positions[i].
        ///   - 1 normal  → face-interior vertex  → flat quad
        ///   - 2 normals → edge vertex            → fold when both ends share the same edge
        /// </summary>
        public void SetPath(List<Vector3> positions, List<List<Vector3>> allNormals)
        {
            if (positions.Count < 2) return;
            if (mf == null) mf = GetComponent<MeshFilter>();
            if (mr == null) mr = GetComponent<MeshRenderer>();
            if (arrowMaterial != null) mr.material = arrowMaterial;

            int   n        = positions.Count;
            float halfW    = lineWidth * 0.5f;

            // Cumulative distances for continuous UV
            float[] dist = new float[n];
            dist[0] = 0f;
            for (int i = 1; i < n; i++)
                dist[i] = dist[i - 1] + Vector3.Distance(positions[i - 1], positions[i]);
            float totalDist = dist[n - 1] + tipLength;

            var verts       = new List<Vector3>();
            var tris        = new List<int>();
            var uvs         = new List<Vector2>();
            var meshNormals = new List<Vector3>();

            // ── Body ─────────────────────────────────────────────────────────────
            for (int i = 0; i < n - 1; i++)
            {
                float u0 = dist[i]     / totalDist;
                float u1 = dist[i + 1] / totalDist;

                Vector3 nA = PrimaryNormal(allNormals[i]);
                Vector3 nB = PrimaryNormal(allNormals[i + 1]);

                // Check if both vertices share the same cube edge (2 common face normals)
                Vector3 edgeN1, edgeN2;
                if (IsEdgeSegment(allNormals[i], allNormals[i + 1], out edgeN1, out edgeN2))
                {
                    // ── Fold: two quads, one per face ─────────────────────────────
                    Vector3 a   = positions[i];
                    Vector3 b   = positions[i + 1];
                    Vector3 dir = (b - a).normalized;

                    AddFoldQuads(a, b, dir, edgeN1, edgeN2,
                                 halfW, surfaceOffset, u0, u1,
                                 verts, tris, uvs, meshNormals);
                }
                else
                {
                    // ── Normal: single quad, per-vertex lift ──────────────────────
                    Vector3 a  = positions[i];
                    Vector3 b  = positions[i + 1];

                    Vector3 seg = (nA + nB).normalized;
                    if (seg.sqrMagnitude < 0.01f) seg = nA;

                    Vector3 dir   = (b - a).normalized;
                    Vector3 right = Vector3.Cross(seg, dir).normalized;

                    Vector3 liftA = nA * surfaceOffset;
                    Vector3 liftB = nB * surfaceOffset;

                    Vector3 v0 = a - right * halfW + liftA;
                    Vector3 v1 = a + right * halfW + liftA;
                    Vector3 v2 = b + right * halfW + liftB;
                    Vector3 v3 = b - right * halfW + liftB;

                    AddQuad(v0, v1, v2, v3, nA, nA, nB, nB,
                            u0, u1, verts, tris, uvs, meshNormals);
                }
            }

            // ── Round caps at interior vertices AND the tail (index 0) ──────────
            float capRadius = halfW;
            for (int i = 0; i < n - 1; i++)
            {
                float capU = dist[i] / totalDist;
                // Higher segment count (24) for smooth corner wrapping on mobile
                AddFoldedRoundCap(positions[i], allNormals[i], capRadius, 24, capU, surfaceOffset,
                                  verts, tris, uvs, meshNormals);
            }

            // ── Tip ──────────────────────────────────────────────────────────────
            Vector3 tipPos = positions[n - 1];
            Vector3 tipDir = (positions[n - 1] - positions[n - 2]).normalized;
            float   uBase  = dist[n - 1] / totalDist;

            Vector3 edgeTN1 = Vector3.zero, edgeTN2 = Vector3.zero;
            bool tipOnEdge = allNormals[n - 1].Count >= 2 &&
                             IsEdgeSegment(allNormals[n - 2], allNormals[n - 1], out edgeTN1, out edgeTN2);

            if (tipOnEdge)
            {
                // Folded arrowhead: one triangle per face
                AddFoldTip(tipPos, tipDir, edgeTN1, edgeTN2,
                           halfW, tipLength, surfaceOffset, uBase,
                           verts, tris, uvs, meshNormals);
            }
            else
            {
                // Flat arrowhead on single face
                Vector3 tipNormal = PrimaryNormal(allNormals[n - 1]);
                Vector3 tipRight  = Vector3.Cross(tipNormal, tipDir).normalized;
                Vector3 tipLift   = tipNormal * surfaceOffset;

                Vector3 apex  = tipPos + tipDir   * tipLength + tipLift;
                Vector3 baseL = tipPos - tipRight  * lineWidth + tipLift;
                Vector3 baseR = tipPos + tipRight  * lineWidth + tipLift;

                int ti = verts.Count;
                verts.AddRange(new[] { apex, baseL, baseR });
                uvs.AddRange(new[] {
                    new Vector2(1f, 0.5f),
                    new Vector2(uBase, 0f),
                    new Vector2(uBase, 1f)
                });
                tris.AddRange(new[] { ti, ti+1, ti+2,  ti, ti+2, ti+1 });
                meshNormals.Add(tipNormal); meshNormals.Add(tipNormal); meshNormals.Add(tipNormal);
            }

            // ── Build mesh ───────────────────────────────────────────────────────
            Mesh mesh = new Mesh { name = "Arrow_" + name };
            mesh.vertices  = verts.ToArray();
            mesh.triangles = tris.ToArray();
            mesh.uv        = uvs.ToArray();
            mesh.normals   = meshNormals.ToArray();
            mesh.RecalculateBounds();
            mf.mesh = mesh;
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        static Vector3 PrimaryNormal(List<Vector3> faceNormals)
            => faceNormals != null && faceNormals.Count > 0 ? faceNormals[0] : Vector3.up;

        /// <summary>
        /// Returns true when both vertex normal lists share exactly 2 common normals
        /// (i.e., both vertices lie on the same cube edge).
        /// </summary>
        static bool IsEdgeSegment(List<Vector3> facesA, List<Vector3> facesB,
                                   out Vector3 n1, out Vector3 n2)
        {
            n1 = Vector3.zero; n2 = Vector3.zero;
            if (facesA.Count < 2 || facesB.Count < 2) return false;

            var shared = new List<Vector3>(2);
            foreach (var n in facesA)
                if (facesB.Contains(n)) shared.Add(n);

            if (shared.Count < 2) return false;
            n1 = shared[0];
            n2 = shared[1];
            return true;
        }

        /// <summary>
        /// Inward direction from the cube edge into face (n = that face's normal).
        /// Verified to point away from the other face.
        /// </summary>
        static Vector3 InwardDir(Vector3 faceNormal, Vector3 edgeDir, Vector3 otherFaceNormal)
        {
            Vector3 d = Vector3.Cross(faceNormal, edgeDir).normalized;
            // Make sure it points AWAY from the other face
            if (Vector3.Dot(d, otherFaceNormal) > 0) d = -d;
            return d;
        }

        /// <summary>
        /// Generates two quads for a segment that runs along a cube edge —
        /// one quad flat on each adjacent face.
        /// Both quads share the same edge-line vertices (bisector lift) so there is no gap.
        /// </summary>
        static void AddFoldQuads(
            Vector3 a, Vector3 b, Vector3 dir,
            Vector3 n1, Vector3 n2,
            float width, float offset, float u0, float u1,
            List<Vector3> verts, List<int> tris,
            List<Vector2> uvs, List<Vector3> normals)
        {
            Vector3 in1        = InwardDir(n1, dir, n2);
            Vector3 in2        = InwardDir(n2, dir, n1);
            Vector3 lift1      = n1 * offset;
            Vector3 lift2      = n2 * offset;
            // Shared edge vertices sit on the bisector — no gap between the two halves
            Vector3 edgeLift   = (n1 + n2).normalized * offset;

            // Face 1: shared edge → inward on face 1
            Vector3 f1v0 = a + edgeLift;
            Vector3 f1v1 = a + in1 * width + lift1;
            Vector3 f1v2 = b + in1 * width + lift1;
            Vector3 f1v3 = b + edgeLift;
            AddQuad(f1v0, f1v1, f1v2, f1v3, n1, n1, n1, n1,
                    u0, u1, verts, tris, uvs, normals);

            // Face 2: shared edge → inward on face 2
            Vector3 f2v0 = a + edgeLift;
            Vector3 f2v1 = a + in2 * width + lift2;
            Vector3 f2v2 = b + in2 * width + lift2;
            Vector3 f2v3 = b + edgeLift;
            AddQuad(f2v0, f2v1, f2v2, f2v3, n2, n2, n2, n2,
                    u0, u1, verts, tris, uvs, normals);
        }

        /// <summary>
        /// 3D spike arrowhead when the tip vertex sits on a cube edge.
        /// Apex pops outward along the bisector direction (diagonal away from cube),
        /// with wing bases spreading on each adjacent face — creates a spike that
        /// visually "erupts" from the edge rather than lying flat on the surface.
        /// </summary>
        static void AddFoldTip(
            Vector3 tipPos, Vector3 tipDir,
            Vector3 n1, Vector3 n2,
            float width, float length, float offset, float uBase,
            List<Vector3> verts, List<int> tris,
            List<Vector2> uvs, List<Vector3> normals)
        {
            Vector3 in1      = InwardDir(n1, tipDir, n2);
            Vector3 in2      = InwardDir(n2, tipDir, n1);
            Vector3 spikeDir = (n1 + n2).normalized;          // diagonal outward from cube edge

            Vector3 apex       = tipPos + spikeDir * length;          // spike tip in space
            Vector3 baseCenter = tipPos + spikeDir * offset;          // base centre on edge
            Vector3 wing1      = tipPos + in1 * width + n1 * offset;  // base wing on face 1
            Vector3 wing2      = tipPos + in2 * width + n2 * offset;  // base wing on face 2

            // Left face of spike (face 1 side)
            int ti = verts.Count;
            verts.AddRange(new[] { apex, wing1, baseCenter });
            uvs.AddRange(new[] { new Vector2(1f, 0.5f), new Vector2(uBase, 0f), new Vector2(uBase, 1f) });
            tris.AddRange(new[] { ti, ti+1, ti+2,  ti, ti+2, ti+1 });
            normals.Add(n1); normals.Add(n1); normals.Add(n1);

            // Right face of spike (face 2 side)
            ti = verts.Count;
            verts.AddRange(new[] { apex, baseCenter, wing2 });
            uvs.AddRange(new[] { new Vector2(1f, 0.5f), new Vector2(uBase, 0f), new Vector2(uBase, 1f) });
            tris.AddRange(new[] { ti, ti+1, ti+2,  ti, ti+2, ti+1 });
            normals.Add(n2); normals.Add(n2); normals.Add(n2);

            // Front face of spike — visible from outside the cube, straddles both faces
            ti = verts.Count;
            verts.AddRange(new[] { apex, wing2, wing1 });
            uvs.AddRange(new[] { new Vector2(1f, 0.5f), new Vector2(uBase, 1f), new Vector2(uBase, 0f) });
            tris.AddRange(new[] { ti, ti+1, ti+2,  ti, ti+2, ti+1 });
            normals.Add(spikeDir); normals.Add(spikeDir); normals.Add(spikeDir);
        }

        /// <summary>
        /// Per-face arc generator with exact seam boundaries.
        /// Builds a per-face 2D basis so arcs lie correctly in each face's plane —
        /// no projection step, so no degenerate collapse on non-primary faces.
        /// Seam endpoints are pinned to exact Cross(faceN, otherN) directions
        /// so adjacent face arcs share identical 3D boundary vertices.
        /// </summary>
        static void AddFoldedRoundCap(
            Vector3 center, List<Vector3> faces, float radius, int segments, float u, float offset,
            List<Vector3> verts, List<int> tris,
            List<Vector2> uvs, List<Vector3> normals)
        {
            if (faces == null || faces.Count == 0) return;

            foreach (var faceN in faces)
            {
                // Per-face 2D basis lying IN this face's plane
                Vector3 basisU = Vector3.Cross(faceN, Vector3.up);
                if (basisU.sqrMagnitude < 0.001f) basisU = Vector3.Cross(faceN, Vector3.forward);
                basisU.Normalize();
                Vector3 basisV = Vector3.Cross(basisU, faceN).normalized;

                // Uniform samples + exact seam boundary angles so arc endpoints
                // match in 3D space regardless of segment count
                var angleList = new List<float>(segments + 4);
                for (int s = 0; s <= segments; s++)
                    angleList.Add(s * Mathf.PI * 2f / segments);

                foreach (var otherN in faces)
                {
                    if (otherN == faceN) continue;
                    Vector3 seam = Vector3.Cross(faceN, otherN);
                    if (seam.sqrMagnitude < 0.001f) continue;
                    seam.Normalize();
                    InsertAngle(angleList, Atan2Basis( seam, basisU, basisV));
                    InsertAngle(angleList, Atan2Basis(-seam, basisU, basisV));
                }
                angleList.Sort();

                int centerIdx = verts.Count;
                verts.Add(center + faceN * offset);
                uvs.Add(new Vector2(u, 0.5f));
                normals.Add(faceN);

                int arcStart = verts.Count;
                int arcCount = 0;

                foreach (float ang in angleList)
                {
                    // dir already lies in faceN's plane — no projection needed
                    Vector3 dir = basisU * Mathf.Cos(ang) + basisV * Mathf.Sin(ang);

                    bool belongs = true;
                    foreach (var otherN in faces)
                    {
                        if (otherN == faceN) continue;
                        if (Vector3.Dot(dir, otherN) > 0.02f) { belongs = false; break; }
                    }
                    if (!belongs) continue;

                    verts.Add(center + dir * radius + faceN * offset);
                    uvs.Add(new Vector2(u, 0.5f));
                    normals.Add(faceN);
                    arcCount++;
                }

                for (int i = 0; i < arcCount - 1; i++)
                {
                    tris.Add(centerIdx); tris.Add(arcStart + i);     tris.Add(arcStart + i + 1);
                    tris.Add(centerIdx); tris.Add(arcStart + i + 1); tris.Add(arcStart + i);
                }
            }
        }

        static float Atan2Basis(Vector3 dir, Vector3 u, Vector3 v)
        {
            float a = Mathf.Atan2(Vector3.Dot(dir, v), Vector3.Dot(dir, u));
            return a < 0f ? a + Mathf.PI * 2f : a;
        }

        static void InsertAngle(List<float> list, float a, float eps = 0.001f)
        {
            foreach (var x in list) if (Mathf.Abs(x - a) < eps) return;
            list.Add(a);
        }

        /// <summary>Adds a quad with both winding orders (visible from inside and outside).</summary>
        static void AddQuad(
            Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3,
            Vector3 n0, Vector3 n1, Vector3 n2, Vector3 n3,
            float u0, float u1,
            List<Vector3> verts, List<int> tris,
            List<Vector2> uvs, List<Vector3> normals)
        {
            int bi = verts.Count;
            verts.AddRange(new[] { v0, v1, v2, v3 });
            uvs.AddRange(new[] {
                new Vector2(u0, 0f), new Vector2(u0, 1f),
                new Vector2(u1, 1f), new Vector2(u1, 0f)
            });
            tris.AddRange(new[] { bi, bi+1, bi+2,  bi, bi+2, bi+3 }); // front
            tris.AddRange(new[] { bi, bi+2, bi+1,  bi, bi+3, bi+2 }); // back
            normals.Add(n0); normals.Add(n1); normals.Add(n2); normals.Add(n3);
        }
    }
}
