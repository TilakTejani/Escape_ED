using UnityEngine;
using System.Collections.Generic;

namespace EscapeED
{
    /// <summary>
    /// Procedural mesh generator for the Arrow system.
    /// Handles all vertex, triangle, and UV logic for complex cube-surface paths.
    /// </summary>
    public static class ArrowMeshBuilder
    {
        public struct MeshData
        {
            public List<Vector3> verts;
            public List<int>     tris;
            public List<Vector2> uvs;
            public List<Vector3> normals;
        }

        /// <summary>
        /// Generates a stable orthogonal 'right' vector relative to a normal and direction.
        /// Prevents geometric collapse when direction and normal are parallel (common during world-space ejection).
        /// </summary>
        private static Vector3 GetSafeRight(Vector3 normal, Vector3 dir)
        {
            Vector3 right = Vector3.Cross(normal, dir);

            if (right.sqrMagnitude < 1e-6f)
            {
                // Stability Fallback: Choose the most orthogonal axis to normal to prevent flipping.
                Vector3 fallback = Mathf.Abs(normal.y) < 0.99f ? Vector3.up : Vector3.right;
                right = Vector3.Cross(normal, fallback);
            }

            return right.normalized;
        }

        public class Context
        {
            // Core Inputs
            public List<Vector3>        positions;
            public List<List<Vector3>> allNormals;
            public List<DotType>        dotTypes;

            // Settings
            public float lineWidth;
            public float surfaceOffset;
            public float tipLength;
            public float tipHalfWidth;

            // Computed Path Data
            public List<Vector3> localPos;
            public float[]       dist;
            public float         totalDist;

            // Buffers
            public List<Vector3> verts;
            public List<int>     tris;
            public List<Vector2> uvs;
            public List<Vector3> meshNormals;

            // Pipeline state tracking
            public Vector3 lastValidDir;
            public int     n; // dot count
            public bool    isEjecting;
        }

        public static void BuildBody(Context ctx)
        {
            float halfW = ctx.lineWidth * 0.5f;
            Vector3 preTipDir = (ctx.positions[ctx.n - 1] - ctx.positions[ctx.n - 2]).normalized;

            for (int i = 0; i < ctx.n - 1; i++)
            {
                Vector3 a = ctx.localPos[i];
                Vector3 b = ctx.localPos[i + 1];

                float segLen = Vector3.Distance(a, b);
                if (i == ctx.n - 2 && ctx.dotTypes[ctx.n - 1] != DotType.Face && segLen > ctx.tipLength + ArrowConstants.MIN_SEG_LEN)
                    b = ctx.localPos[ctx.n - 1] - preTipDir * ctx.tipLength;

                if (segLen < ArrowConstants.MIN_SEG_LEN)
                {
                    b      = a + ctx.lastValidDir * ArrowConstants.MIN_SEG_LEN;
                    segLen = ArrowConstants.MIN_SEG_LEN;
                }
                
                float actualU1 = (i == ctx.n - 2 && (b - ctx.localPos[ctx.n - 1]).sqrMagnitude > ArrowConstants.EPS)
                    ? (ctx.dist[ctx.n - 1] - ctx.tipLength) / ctx.totalDist
                    : ctx.dist[i + 1] / ctx.totalDist;

                float u0 = ctx.dist[i] / ctx.totalDist;
                float u1 = actualU1;

                Vector3 nA = PrimaryNormal(ctx.allNormals[i]);
                Vector3 nB = PrimaryNormal(ctx.allNormals[i + 1]);

                Vector3 edgeN1, edgeN2;
                if (IsEdgeSegment(ctx.allNormals[i], ctx.allNormals[i + 1], ctx.dotTypes[i], ctx.dotTypes[i + 1], out edgeN1, out edgeN2))
                {
                    Vector3 dir = (b - a).normalized;
                    AddFoldQuads(a, b, dir, edgeN1, edgeN2, halfW, ctx.surfaceOffset, u0, u1,
                                 ctx.verts, ctx.tris, ctx.uvs, ctx.meshNormals);
                }
                else
                {
                    Vector3 faceN = (ctx.dotTypes[i] == DotType.Face) ? nA : (ctx.dotTypes[i + 1] == DotType.Face ? nB : nA);
                    Vector3 dir = (b - a).normalized;
                    if (dir.sqrMagnitude > ArrowConstants.EPS) ctx.lastValidDir = dir;
                    else                                       dir = ctx.lastValidDir;

                    // Robust basis calculation for face-based segments
                    Vector3 right = GetSafeRight(faceN, dir);
                    Vector3 lift  = faceN * ctx.surfaceOffset;

                    AddQuad(a - right * halfW + lift, a + right * halfW + lift,
                            b + right * halfW + lift, b - right * halfW + lift,
                            faceN, faceN, faceN, faceN,
                            u0, u1, ctx.verts, ctx.tris, ctx.uvs, ctx.meshNormals);
                }
            }
        }

        public static void BuildTailCap(Context ctx)
        {
            AddFoldedRoundCap(ctx.localPos[0], ctx.allNormals[0], ctx.lineWidth * 0.5f, 16, 0f, ctx.surfaceOffset,
                              ctx.verts, ctx.tris, ctx.uvs, ctx.meshNormals);
        }

        public static void BuildBends(Context ctx)
        {
            float halfW = ctx.lineWidth * 0.5f;
            for (int i = 1; i < ctx.n - 1; i++)
            {
                if (IsFoldSeg(i - 1, ctx.allNormals, ctx.dotTypes)) continue;
                if (IsFoldSeg(i,     ctx.allNormals, ctx.dotTypes)) continue;

                if (ctx.dotTypes[i] != DotType.Face)
                {
                    AddFoldedRoundCap(ctx.localPos[i], ctx.allNormals[i], halfW, 16,
                                      ctx.dist[i] / ctx.totalDist, ctx.surfaceOffset,
                                      ctx.verts, ctx.tris, ctx.uvs, ctx.meshNormals);
                    continue;
                }

                Vector3 dirIn  = (ctx.localPos[i]     - ctx.localPos[i - 1]).normalized;
                Vector3 dirOut = (ctx.localPos[i + 1] - ctx.localPos[i]    ).normalized;
                if (Vector3.Dot(dirIn, dirOut) > ArrowConstants.STRAIGHT_DOT_THR) continue;

                Vector3 faceN  = PrimaryNormal(ctx.allNormals[i]);
                Vector3 lift   = faceN * ctx.surfaceOffset;
                Vector3 center = ctx.localPos[i] + lift;

                Vector3 rightIn  = Vector3.Cross(faceN, dirIn ).normalized;
                Vector3 rightOut = Vector3.Cross(faceN, dirOut).normalized;

                float turn        = Vector3.Dot(Vector3.Cross(dirIn, dirOut), faceN);
                float outsideSign = turn >= 0f ? -1f : 1f;
                float insideSign  = -outsideSign;

                AddBendArc(center, rightIn * outsideSign * halfW, rightOut * outsideSign * halfW,
                           faceN, halfW, ctx.dist[i] / ctx.totalDist,
                           ctx.verts, ctx.tris, ctx.uvs, ctx.meshNormals);

                Vector3 innerA = center + rightIn  * insideSign * halfW;
                Vector3 innerB = center + rightOut * insideSign * halfW;
                int ti = ctx.verts.Count;
                ctx.verts.AddRange(new[] { center, innerA, innerB });
                ctx.uvs.AddRange(new[] {
                    new Vector2(ctx.dist[i] / ctx.totalDist, 0.5f), new Vector2(ctx.dist[i] / ctx.totalDist, 0f), new Vector2(ctx.dist[i] / ctx.totalDist, 1f)
                });
                ctx.tris.AddRange(new[] { ti + 1, ti + 2, ti });
                ctx.meshNormals.Add(faceN); ctx.meshNormals.Add(faceN); ctx.meshNormals.Add(faceN);
            }
        }

        public static void BuildTip(Context ctx, out List<Vector3> tipVerts)
        {
            tipVerts = new List<Vector3>(8);
            Vector3 tipPos = ctx.localPos[ctx.n - 1];
            Vector3 tipDir = (ctx.localPos[ctx.n - 1] - ctx.localPos[ctx.n - 2]).normalized;
            float   uBase  = ctx.dist[ctx.n - 1] / ctx.totalDist;

            if (ctx.dotTypes[ctx.n - 1] != DotType.Face)
            {
                Vector3 edgeTN1, edgeTN2;
                bool lastSegOnEdge = IsEdgeSegment(ctx.allNormals[ctx.n - 2], ctx.allNormals[ctx.n - 1],
                                                   ctx.dotTypes[ctx.n - 2], ctx.dotTypes[ctx.n - 1],
                                                   out edgeTN1, out edgeTN2);
                if (lastSegOnEdge)
                {
                    AddFoldTip(tipPos, tipDir, edgeTN1, edgeTN2, ctx.allNormals[ctx.n - 1],
                               ctx.tipHalfWidth, ctx.tipLength, ctx.surfaceOffset, uBase, ctx.totalDist,
                               ctx.verts, ctx.tris, ctx.uvs, ctx.meshNormals);

                    Vector3 edgeLift = GetCorrectedLift(edgeTN1, edgeTN2, ctx.surfaceOffset);
                    // To avoid [Physics.PhysX] coplanar errors, we must provide at least one non-coplanar point for the Convex MeshCollider.
                    // Adding tipPos (on the surface) while others are lifted (edgeLift) creates a 3D wedge.
                    tipVerts.Add(tipPos + GetCorrectedLift(ctx.allNormals[ctx.n - 1], ctx.surfaceOffset)); // Apex
                    tipVerts.Add(tipPos - tipDir * ctx.tipLength + edgeLift + InwardDir(edgeTN1, tipDir, edgeTN2) * ctx.tipHalfWidth + edgeTN1 * ctx.surfaceOffset);
                    tipVerts.Add(tipPos - tipDir * ctx.tipLength + edgeLift + InwardDir(edgeTN2, tipDir, edgeTN1) * ctx.tipHalfWidth + edgeTN2 * ctx.surfaceOffset);
                    tipVerts.Add(tipPos - tipDir * ctx.tipLength + edgeLift);
                    tipVerts.Add(tipPos - tipDir * ctx.tipLength); // Non-lifted base point to ensure volume
                }
                else
                {
                    Vector3 faceN = PrimaryNormal(ctx.allNormals[ctx.n - 2]);
                    Vector3 right = Vector3.Cross(faceN, tipDir).normalized;
                    Vector3 lift  = faceN * ctx.surfaceOffset;
                    Vector3 apex  = tipPos + lift;
                    Vector3 basePos = tipPos - tipDir * ctx.tipLength;
                    Vector3 baseL = basePos - right * ctx.tipHalfWidth + lift;
                    Vector3 baseR = basePos + right * ctx.tipHalfWidth + lift;

                    int ti = ctx.verts.Count;
                    ctx.verts.AddRange(new[] { apex, baseL, baseR });
                    ctx.uvs.AddRange(new[] { new Vector2(uBase, 0.5f), new Vector2(uBase, 0f), new Vector2(1f, 1f) }); // Fixed UV from previous logic typo
                    ctx.tris.AddRange(new[] { ti + 2, ti + 1, ti });
                    ctx.meshNormals.Add(faceN); ctx.meshNormals.Add(faceN); ctx.meshNormals.Add(faceN);

                    tipVerts.AddRange(new[] { apex, baseL, baseR });
                }
            }
            else
            {
                Vector3 tipNormal = PrimaryNormal(ctx.allNormals[ctx.n - 1]);
                // tipDir is already declared at line 162
                Vector3 tipRight  = GetSafeRight(tipNormal, tipDir);
                Vector3 tipLift   = tipNormal * ctx.surfaceOffset;

                Vector3 apex  = tipPos + tipDir   * ctx.tipLength + tipLift;
                Vector3 baseL = tipPos - tipRight * ctx.tipHalfWidth + tipLift;
                Vector3 baseR = tipPos + tipRight * ctx.tipHalfWidth + tipLift;

                // Symmetric Degenerate Triangle Guard: Prevent zero-area triangles if math collapses
                if ((baseL - baseR).sqrMagnitude < 1e-6f)
                {
                    Vector3 offset = tipRight * 0.001f;
                    baseL -= offset;
                    baseR += offset;
                }

                int ti = ctx.verts.Count;
                ctx.verts.AddRange(new[] { apex, baseL, baseR });
                ctx.uvs.AddRange(new[] { new Vector2(1f, 0.5f), new Vector2(uBase, 0f), new Vector2(uBase, 1f) });
                ctx.tris.AddRange(new[] { ti + 2, ti + 1, ti });
                ctx.meshNormals.Add(tipNormal); ctx.meshNormals.Add(tipNormal); ctx.meshNormals.Add(tipNormal);

                // Index 0 (apex) is used as the fan center in ArrowPhysicsHandler. 
                // By adding tipPos (WITHOUT lift), we guarantee a 3D volume (pyramid/wedge).
                tipVerts.Add(apex);         
                tipVerts.Add(baseL);        
                tipVerts.Add(baseR);        
                tipVerts.Add(tipPos); // Non-lifted center base point for volume
            }
        }

        public static void FinalizeMesh(Mesh mesh, Context ctx, bool smoothShading)
        {
            var uv2s = new Vector2[ctx.meshNormals.Count];
            for (int vi = 0; vi < ctx.meshNormals.Count; vi++)
                uv2s[vi] = new Vector2(FaceIndexFromNormal(ctx.meshNormals[vi]), 0f);

            mesh.SetVertices(ctx.verts);
            mesh.SetTriangles(ctx.tris, 0);
            mesh.SetUVs(0, ctx.uvs);
            mesh.uv2 = uv2s;

            if (smoothShading) mesh.RecalculateNormals();
            else               mesh.SetNormals(ctx.meshNormals);

            if (ctx.verts.Count > 0)
            {
                Vector3 bMin = ctx.verts[0], bMax = ctx.verts[0];
                for (int vi = 1; vi < ctx.verts.Count; vi++)
                {
                    Vector3 v = ctx.verts[vi];
                    bMin = Vector3.Min(bMin, v); bMax = Vector3.Max(bMax, v);
                }
                mesh.bounds = new Bounds((bMin + bMax) * 0.5f, bMax - bMin);
            }
        }

        // --- Low Level Geometry Helpers (Internal) ---

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
            if (Vector3.Dot(Vector3.Cross(v2 - v0, v1 - v0), n0) < 0)
                tris.AddRange(new[] { bi, bi+1, bi+2,  bi, bi+2, bi+3 });
            else
                tris.AddRange(new[] { bi, bi+2, bi+1,  bi, bi+3, bi+2 });
            normals.Add(n0); normals.Add(n1); normals.Add(n2); normals.Add(n3);
        }

        static void AddFoldQuads(Vector3 a, Vector3 b, Vector3 dir, Vector3 n1, Vector3 n2,
                                  float halfW, float offset, float u0, float u1,
                                  List<Vector3> verts, List<int> tris,
                                  List<Vector2> uvs, List<Vector3> normals)
        {
            Vector3 miterLift = GetCorrectedLift(n1, n2, offset);
            Vector3 sideN1 = n1 * offset;
            Vector3 sideN2 = n2 * offset;
            Vector3 in1 = InwardDir(n1, dir, n2) * halfW;
            Vector3 in2 = InwardDir(n2, dir, n1) * halfW;

            AddQuad(a + sideN1 + in1, a + miterLift, b + miterLift, b + sideN1 + in1,
                    n1, n1, n1, n1, u0, u1, verts, tris, uvs, normals);
            AddQuad(a + miterLift, a + sideN2 + in2, b + sideN2 + in2, b + miterLift,
                    n2, n2, n2, n2, u0, u1, verts, tris, uvs, normals);
        }

        static void AddFoldTip(Vector3 tipPos, Vector3 tipDir, Vector3 n1, Vector3 n2,
                                List<Vector3> allTipNormals, float halfW, float len, float offset,
                                float uBase, float totalDist,
                                List<Vector3> verts, List<int> tris,
                                List<Vector2> uvs, List<Vector3> normals)
        {
            Vector3 in1        = InwardDir(n1, tipDir, n2);
            Vector3 in2        = InwardDir(n2, tipDir, n1);
            Vector3 edgeLift   = GetCorrectedLift(n1, n2, offset);
            Vector3 apexLift   = GetCorrectedLift(allTipNormals, offset);

            Vector3 apex       = tipPos + apexLift;
            Vector3 basePos    = tipPos - tipDir * len;
            Vector3 baseCenter = basePos + edgeLift;
            Vector3 wing1      = basePos + in1 * halfW + n1 * offset;
            Vector3 wing2      = basePos + in2 * halfW + n2 * offset;

            float uBack = Mathf.Max(0f, uBase - len / totalDist);

            int ti = verts.Count;
            verts.AddRange(new[] { apex, wing1, baseCenter });
            uvs.AddRange(new[] { new Vector2(uBase, 0.5f), new Vector2(uBack, 0f), new Vector2(uBack, 1f) });
            if (Vector3.Dot(Vector3.Cross(wing1 - baseCenter, apex - baseCenter), n1) < 0)
                tris.AddRange(new[] { ti, ti + 1, ti + 2 });
            else
                tris.AddRange(new[] { ti + 2, ti + 1, ti });
            normals.Add(n1); normals.Add(n1); normals.Add(n1);

            ti = verts.Count;
            verts.AddRange(new[] { apex, baseCenter, wing2 });
            uvs.AddRange(new[] { new Vector2(uBase, 0.5f), new Vector2(uBack, 1f), new Vector2(uBack, 0f) });
            if (Vector3.Dot(Vector3.Cross(baseCenter - wing2, apex - wing2), n2) < 0)
                tris.AddRange(new[] { ti, ti + 1, ti + 2 });
            else
                tris.AddRange(new[] { ti + 2, ti + 1, ti });
            normals.Add(n2); normals.Add(n2); normals.Add(n2);
        }

        static void AddFoldedRoundCap(Vector3 center, List<Vector3> faces, float radius, int segments,
                                      float u, float offset, List<Vector3> verts, List<int> tris,
                                      List<Vector2> uvs, List<Vector3> meshNormals)
        {
            if (faces == null || faces.Count == 0) return;

            foreach (var faceN in faces)
            {
                Vector3 basisU = Vector3.Cross(faceN, Vector3.up);
                if (basisU.sqrMagnitude < 0.001f) basisU = Vector3.Cross(faceN, Vector3.forward);
                basisU.Normalize();
                Vector3 basisV = Vector3.Cross(basisU, faceN).normalized;

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
                verts.Add(center + GetCorrectedLift(faces, offset));
                uvs.Add(new Vector2(u, 0.5f));
                meshNormals.Add(faceN);

                int arcStart = verts.Count;
                int arcCount = 0;

                foreach (float ang in angleList)
                {
                    Vector3 dir = basisU * Mathf.Cos(ang) + basisV * Mathf.Sin(ang);

                    bool belongs = true;
                    foreach (var otherN in faces)
                    {
                        if (otherN == faceN) continue;
                        if (Vector3.Dot(dir, otherN) > 0.02f) { belongs = false; break; }
                    }
                    if (!belongs) continue;

                    var vertexFaces = new List<Vector3> { faceN };
                    foreach (var otherN in faces)
                        if (otherN != faceN && Mathf.Abs(Vector3.Dot(dir, otherN)) < 0.05f)
                            vertexFaces.Add(otherN);

                    verts.Add(center + dir * radius + GetCorrectedLift(vertexFaces, offset));
                    uvs.Add(new Vector2(u, 0.5f));
                    meshNormals.Add(faceN);
                    arcCount++;
                }

                for (int i = 0; i < arcCount - 1; i++)
                    tris.AddRange(new[] { centerIdx, arcStart + i + 1, arcStart + i });
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

        static void AddBendArc(Vector3 center, Vector3 fromVec, Vector3 toVec, Vector3 faceN, float radius, float u,
                                List<Vector3> verts, List<int> tris, List<Vector2> uvs, List<Vector3> normals)
        {
            Vector3 basisU = fromVec.normalized;
            Vector3 basisV = Vector3.Cross(faceN, basisU).normalized;
            float angleTo = Mathf.Atan2(Vector3.Dot(toVec, basisV), Vector3.Dot(toVec, basisU));
            int steps = Mathf.Max(2, Mathf.CeilToInt(Mathf.Abs(angleTo) / (ArrowConstants.BEND_ANGLE_STEP)));

            int centerIdx = verts.Count;
            verts.Add(center);
            uvs.Add(new Vector2(u, 0.5f));
            normals.Add(faceN);

            int arcStart = verts.Count;
            for (int s = 0; s <= steps; s++)
            {
                float a = Mathf.Lerp(0f, angleTo, (float)s / steps);
                verts.Add(center + (basisU * Mathf.Cos(a) + basisV * Mathf.Sin(a)) * radius);
                uvs.Add(new Vector2(u, 0.5f));
                normals.Add(faceN);
            }
            for (int s = 0; s < steps; s++)
                tris.AddRange(new[] { centerIdx, arcStart + s + 1, arcStart + s });
        }

        static Vector3 GetCorrectedLift(List<Vector3> normals, float offset)
        {
            if (normals == null || normals.Count == 0) return Vector3.zero;
            if (normals.Count == 1) return normals[0] * offset;
            Vector3 bisector = Vector3.zero;
            foreach (var n in normals) bisector += n;
            bisector.Normalize();
            float dot = Vector3.Dot(normals[0], bisector);
            return bisector * (offset / Mathf.Max(dot, ArrowConstants.EPS));
        }

        static Vector3 GetCorrectedLift(Vector3 n1, Vector3 n2, float offset)
            => GetCorrectedLift(new List<Vector3> { n1, n2 }, offset);

        static bool IsEdgeSegment(List<Vector3> facesA, List<Vector3> facesB, DotType typeA, DotType typeB, out Vector3 n1, out Vector3 n2)
        {
            n1 = n2 = Vector3.zero;
            if (typeA == DotType.Face || typeB == DotType.Face) return false;

            var shared = new List<Vector3>(2);
            foreach (var nA in facesA)
                foreach (var nB in facesB)
                    if (Vector3.Dot(nA, nB) > ArrowConstants.STRAIGHT_DOT_THR) { shared.Add(nA); break; }

            if (shared.Count < 2) return false;
            n1 = shared[0];
            n2 = shared[1];
            return true;
        }

        static bool IsFoldSeg(int i, List<List<Vector3>> allNormals, List<DotType> dotTypes)
        {
            Vector3 n1, n2;
            return IsEdgeSegment(allNormals[i], allNormals[i + 1], dotTypes[i], dotTypes[i + 1], out n1, out n2);
        }

        static Vector3 InwardDir(Vector3 faceNormal, Vector3 edgeDir, Vector3 otherFaceNormal)
        {
            Vector3 d = Vector3.Cross(faceNormal, edgeDir).normalized;
            if (Vector3.Dot(d, otherFaceNormal) > 0) d = -d;
            return d;
        }

        static Vector3 PrimaryNormal(List<Vector3> faceNormals)
            => faceNormals != null && faceNormals.Count > 0 ? faceNormals[0] : Vector3.up;

        static float FaceIndexFromNormal(Vector3 n)
        {
            if (n.y >  0.5f) return 0f;
            if (n.y < -0.5f) return 1f;
            if (n.x < -0.5f) return 2f;
            if (n.x >  0.5f) return 3f;
            if (n.z >  0.5f) return 4f;
            return 5f;
        }
    }
}
