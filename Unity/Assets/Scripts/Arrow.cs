using UnityEngine;
using System.Collections.Generic;
using EscapeED.InputHandling;

namespace EscapeED
{
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(MeshCollider))]
    public class Arrow : MonoBehaviour, IInteractable
    {
        [Header("Visuals")]
        public Material arrowMaterial;       // ArrowPulseMat   — ZTest LEqual (on cube)
        public Material arrowEjectMaterial;  // ArrowEjectMat   — ZTest Always  (exiting)

        [Header("Shape")]
        public float lineWidth      = 0.08f;
        public float tipLengthMult  = 2.5f; 
        public float tipWidthMult   = 2.5f; 
        public float surfaceOffset  = 0.005f;

        [HideInInspector] public float tipLength; 

        private MeshFilter   mf;
        private MeshRenderer mr;
        private MeshCollider mc;
        private Mesh         mesh;

        // --- Optimized Buffers (Pre-allocated to zero GC) ---
        private List<Vector3>      originalPositions;
        private List<List<Vector3>> originalNormals;
        private List<DotType>      originalDotTypes;
        private bool               isEjecting = false;

        void Awake()
        {
            mf = GetComponent<MeshFilter>();
            mr = GetComponent<MeshRenderer>();
            mc = GetComponent<MeshCollider>();
            if (arrowMaterial != null) mr.material = arrowMaterial;
        }

        public void SetPath(List<Vector3> positions, List<List<Vector3>> allNormals, List<DotType> dotTypes)
        {
            if (positions == null || positions.Count < 2) return;

            // Only cache original data if we aren't currently animating/ejecting
            if (!isEjecting)
            {
                originalPositions = new List<Vector3>(positions);
                originalNormals   = new List<List<Vector3>>(allNormals);
                originalDotTypes  = new List<DotType>(dotTypes);
            }

            if (mf == null) mf = GetComponent<MeshFilter>();
            if (mr == null) mr = GetComponent<MeshRenderer>();
            if (arrowMaterial != null && !isEjecting) mr.material = arrowMaterial;

            // Calculate dynamic arrowhead dimensions
            tipLength = lineWidth * tipLengthMult;
            float tipHalfWidth = (lineWidth * tipWidthMult) * 0.5f;

            int   n        = positions.Count;
            float halfW    = lineWidth * 0.5f;

            // ── Local Conversion ──
            var localPos = new List<Vector3>(n);
            for (int i = 0; i < n; i++)
                localPos.Add(transform.InverseTransformPoint(positions[i]));

            // Cumulative distances for continuous UV
            float[] dist = new float[n];
            dist[0] = 0f;
            for (int i = 1; i < n; i++)
                dist[i] = dist[i - 1] + Vector3.Distance(localPos[i - 1], localPos[i]);
            float totalDist = dist[n - 1] + tipLength;

            // Clear buffers instead of re-allocating
            verts.Clear();
            tris.Clear();
            uvs.Clear();
            meshNormals.Clear();

            // ── Body ──
            Vector3 preTipDir = (positions[n - 1] - positions[n - 2]).normalized;

            for (int i = 0; i < n - 1; i++)
            {
                Vector3 a = localPos[i];
                Vector3 b = localPos[i + 1];

                float segLen = Vector3.Distance(a, b);
                if (i == n - 2 && dotTypes[n - 1] != DotType.Face && segLen > tipLength + 0.01f)
                    b = localPos[n - 1] - preTipDir * tipLength;
                
                float actualU1 = (i == n - 2 && b != localPos[n - 1])
                    ? (dist[n - 1] - tipLength) / totalDist
                    : dist[i + 1] / totalDist;

                float u0 = dist[i] / totalDist;
                float u1 = actualU1;

                Vector3 nA = PrimaryNormal(allNormals[i]);
                Vector3 nB = PrimaryNormal(allNormals[i + 1]);

                Vector3 edgeN1, edgeN2;
                if (IsEdgeSegment(allNormals[i], allNormals[i + 1], dotTypes[i], dotTypes[i + 1], out edgeN1, out edgeN2))
                {
                    Vector3 dir = (b - a).normalized;
                    AddFoldQuads(a, b, dir, edgeN1, edgeN2,
                                 halfW, surfaceOffset, u0, u1,
                                 verts, tris, uvs, meshNormals);
                }
                else
                {
                    Vector3 faceN;
                    if      (dotTypes[i]     == DotType.Face) faceN = nA;
                    else if (dotTypes[i + 1] == DotType.Face) faceN = nB;
                    else                                       faceN = nA;

                    Vector3 dir   = (b - a).normalized;
                    if (dir.sqrMagnitude < 0.0001f) dir = preTipDir;

                    Vector3 right = Vector3.Cross(faceN, dir).normalized;
                    Vector3 lift  = faceN * surfaceOffset;

                    Vector3 v0 = a - right * halfW + lift;
                    Vector3 v1 = a + right * halfW + lift;
                    Vector3 v2 = b + right * halfW + lift;
                    Vector3 v3 = b - right * halfW + lift;

                    AddQuad(v0, v1, v2, v3, faceN, faceN, faceN, faceN,
                            u0, u1, verts, tris, uvs, meshNormals);
                }
            }

            // ── Tail cap ──
            AddFoldedRoundCap(localPos[0], allNormals[0], halfW, 16, 0f, surfaceOffset,
                              verts, tris, uvs, meshNormals);

            // ── Round joins at bends ──
            for (int i = 1; i < n - 1; i++)
            {
                if (IsFoldSeg(i - 1, allNormals, dotTypes)) continue;
                if (IsFoldSeg(i,     allNormals, dotTypes)) continue;

                if (dotTypes[i] != DotType.Face)
                {
                    AddFoldedRoundCap(localPos[i], allNormals[i], halfW, 16,
                                      dist[i] / totalDist, surfaceOffset,
                                      verts, tris, uvs, meshNormals);
                    continue;
                }

                Vector3 dirIn  = (localPos[i]     - localPos[i - 1]).normalized;
                Vector3 dirOut = (localPos[i + 1] - localPos[i]    ).normalized;
                if (Vector3.Dot(dirIn, dirOut) > 0.99f) continue; 

                Vector3 faceN  = PrimaryNormal(allNormals[i]);
                Vector3 lift   = faceN * surfaceOffset;
                Vector3 center = localPos[i] + lift;

                Vector3 rightIn  = Vector3.Cross(faceN, dirIn ).normalized;
                Vector3 rightOut = Vector3.Cross(faceN, dirOut).normalized;

                float turn        = Vector3.Dot(Vector3.Cross(dirIn, dirOut), faceN);
                float outsideSign = turn >= 0f ? -1f : 1f;
                float insideSign  = -outsideSign;

                Vector3 fromVec = rightIn  * outsideSign * halfW;
                Vector3 toVec   = rightOut * outsideSign * halfW;
                float   u       = dist[i] / totalDist;
                AddBendArc(center, fromVec, toVec, faceN, halfW, u,
                           verts, tris, uvs, meshNormals);

                Vector3 innerA = center + rightIn  * insideSign * halfW;
                Vector3 innerB = center + rightOut * insideSign * halfW;
                int ti = verts.Count;
                verts.AddRange(new[] { center, innerA, innerB });
                uvs.AddRange(new[] {
                    new Vector2(u, 0.5f), new Vector2(u, 0f), new Vector2(u, 1f)
                });
                tris.AddRange(new[] { ti, ti + 1, ti + 2 });
                meshNormals.Add(faceN); meshNormals.Add(faceN); meshNormals.Add(faceN);
            }

            // ── Tip ──
            Vector3 tipPos = localPos[n - 1];
            Vector3 tipDir = (localPos[n - 1] - localPos[n - 2]).normalized;
            float   uBase  = dist[n - 1] / totalDist;

            if (dotTypes[n - 1] != DotType.Face)
            {
                Vector3 edgeTN1, edgeTN2;
                bool lastSegOnEdge = IsEdgeSegment(allNormals[n - 2], allNormals[n - 1],
                                                   dotTypes[n - 2], dotTypes[n - 1],
                                                   out edgeTN1, out edgeTN2);
                if (lastSegOnEdge)
                {
                    AddFoldTip(tipPos, tipDir, edgeTN1, edgeTN2, allNormals[n - 1],
                               tipHalfWidth, tipLength, surfaceOffset, uBase,
                               verts, tris, uvs, meshNormals);
                }
                else
                {
                    Vector3 faceN = PrimaryNormal(allNormals[n - 2]);
                    Vector3 right = Vector3.Cross(faceN, tipDir).normalized;
                    Vector3 lift  = faceN * surfaceOffset;

                    Vector3 apex    = tipPos + lift;
                    Vector3 basePos = tipPos - tipDir * tipLength;
                    Vector3 baseL   = basePos - right * tipHalfWidth + lift;
                    Vector3 baseR   = basePos + right * tipHalfWidth + lift;

                    int ti = verts.Count;
                    verts.AddRange(new[] { apex, baseL, baseR });
                    uvs.AddRange(new[] {
                        new Vector2(uBase, 0.5f),
                        new Vector2(uBase, 0f),
                        new Vector2(uBase, 1f)
                    });
                    tris.AddRange(new[] { ti, ti+1, ti+2 });
                    meshNormals.Add(faceN); meshNormals.Add(faceN); meshNormals.Add(faceN);
                }
            }
            else
            {
                Vector3 tipNormal = PrimaryNormal(allNormals[n - 1]);
                Vector3 tipRight  = Vector3.Cross(tipNormal, tipDir).normalized;
                Vector3 tipLift   = tipNormal * surfaceOffset;

                Vector3 apex  = tipPos + tipDir   * tipLength + tipLift;
                Vector3 baseL = tipPos - tipRight * tipHalfWidth + tipLift;
                Vector3 baseR = tipPos + tipRight * tipHalfWidth + tipLift;

                int ti = verts.Count;
                verts.AddRange(new[] { apex, baseL, baseR });
                uvs.AddRange(new[] {
                    new Vector2(1f, 0.5f),
                    new Vector2(uBase, 0f),
                    new Vector2(uBase, 1f)
                });
                tris.AddRange(new[] { ti, ti+1, ti+2 });
                meshNormals.Add(tipNormal); meshNormals.Add(tipNormal); meshNormals.Add(tipNormal);
            }

            uv2s.Clear();
            for (int vi = 0; vi < meshNormals.Count; vi++)
                uv2s.Add(new Vector2(FaceIndexFromNormal(meshNormals[vi]), 0f));

            Mesh mesh = new Mesh { name = "Arrow_" + name };
            mesh.vertices  = verts.ToArray();
            mesh.triangles = tris.ToArray();
            mesh.uv        = uvs.ToArray();
            mesh.uv2       = uv2s;
            mesh.normals   = meshNormals.ToArray();
            mesh.RecalculateBounds();
            mf.mesh = mesh;
            if (mc == null) mc = GetComponent<MeshCollider>();
            if (mc != null) mc.sharedMesh = mesh;
        }

        [ContextMenu("Eject Arrow")]
        public void Eject()
        {
            if (isEjecting) return;
            if (mc != null) mc.enabled = false;
            StartCoroutine(EjectCoroutine());
        }

        private System.Collections.IEnumerator EjectCoroutine()
        {
            isEjecting = true;
            int n = originalPositions.Count;

            Vector3[] worldPos = new Vector3[n];
            for (int i = 0; i < n; i++)
                worldPos[i] = transform.TransformPoint(originalPositions[i]);

            Vector3 headDir = (worldPos[n - 1] - worldPos[n - 2]).normalized;
            float gridStep = Vector3.Distance(worldPos[0], worldPos[1]);

            var slideNormals  = new List<List<Vector3>>(originalNormals);
            var slideDotTypes = new List<DotType>(originalDotTypes);

            transform.SetParent(null, false);
            gameObject.layer = LayerMask.NameToLayer("EjectingArrow");

            // --- Phase 1: On-Cube Slide (Still needs Mesh Updates) ---
            int   onCubeSteps   = n;
            float onStepDur     = 0.10f; 

            Vector3[] stepFrom = (Vector3[])worldPos.Clone();
            Vector3[] stepTo   = new Vector3[n];

            for (int step = 0; step < onCubeSteps; step++)
            {
                for (int i = 0; i < n - 1; i++) stepTo[i] = stepFrom[i + 1];
                stepTo[n - 1] = stepFrom[n - 1] + headDir * gridStep;

                slideNormals.RemoveAt(0);
                slideNormals.Add(new List<Vector3>(slideNormals[slideNormals.Count - 1]));
                slideDotTypes.RemoveAt(0);
                slideDotTypes.Add(slideDotTypes[slideDotTypes.Count - 1]);

                float elapsed = 0f;
                while (elapsed < onStepDur)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / onStepDur));
                    for (int i = 0; i < n; i++)
                        worldPos[i] = Vector3.Lerp(stepFrom[i], stepTo[i], t);
                    
                    SetPath(new List<Vector3>(worldPos), slideNormals, slideDotTypes);
                    yield return null;
                }

                System.Array.Copy(stepTo, stepFrom, n);
            }

            // --- Phase 2: Straight Slide (Transform Only - NO MESH UPDATES) ---
            float curSpeed  = gridStep / onStepDur; 
            float accel     = 1.2f;
            float elapsed2  = 0f;

            while (elapsed2 < 1.0f)
            {
                elapsed2           += Time.deltaTime;
                curSpeed           += accel * Time.deltaTime;
                transform.position += headDir * curSpeed * Time.deltaTime;
                yield return null;
            }

            Destroy(gameObject);
        }

        // ── Interaction Helpers ───────────────────────────────────────────────────

        public event System.Action<Arrow> OnInteractionTriggered;

        public void OnInteract()
        {
            if (!isEjecting)
            {
                OnInteractionTriggered?.Invoke(this);
            }
        }

        public void GetEjectionData(out Vector3 tipPos, out Vector3 tipDir, out Vector3 faceNormal)
        {
            if (originalPositions == null || originalPositions.Count < 2)
            {
                tipPos = transform.position; tipDir = transform.forward; faceNormal = transform.up;
                return;
            }
            int n = originalPositions.Count;
            tipPos = transform.TransformPoint(originalPositions[n - 1]);
            Vector3 preTip = transform.TransformPoint(originalPositions[n - 2]);
            tipDir = (tipPos - preTip).normalized;
            faceNormal = transform.TransformDirection(PrimaryNormal(originalNormals[n - 1]));
        }

        public void PlayBlockedAnimation()
        {
            if (isEjecting) return;
            if (activeShake != null) StopCoroutine(activeShake);
            activeShake = StartCoroutine(BlockedShakeCoroutine());
        }

        private System.Collections.IEnumerator BlockedShakeCoroutine()
        {
            GetEjectionData(out _, out Vector3 tipDir, out _);
            Vector3 startPos = transform.localPosition;
            
            float duration = 0.15f;
            float elapsed = 0f;
            while(elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                // A quick push forward and snap back using sine wave
                float offset = Mathf.Sin(t * Mathf.PI) * (lineWidth * 1.5f); 
                transform.localPosition = startPos + transform.InverseTransformDirection(tipDir) * offset;
                yield return null;
            }
            transform.localPosition = startPos;
            activeShake = null;
        }

        // --- Static Helpers ---

        static void AddBendArc(Vector3 center, Vector3 fromVec, Vector3 toVec, Vector3 faceN, float radius, float u, List<Vector3> verts, List<int> tris, List<Vector2> uvs, List<Vector3> normals) {
            Vector3 basisU = fromVec.normalized;
            Vector3 basisV = Vector3.Cross(faceN, basisU).normalized;
            float angleTo = Mathf.Atan2(Vector3.Dot(toVec, basisV), Vector3.Dot(toVec, basisU));
            int steps = Mathf.Max(2, Mathf.CeilToInt(Mathf.Abs(angleTo) / (Mathf.PI / 8f)));
            int centerIdx = verts.Count;
            verts.Add(center); uvs.Add(new Vector2(u, 0.5f)); normals.Add(faceN);
            int arcStart = verts.Count;
            for (int s = 0; s <= steps; s++) {
                float a = Mathf.Lerp(0f, angleTo, (float)s / steps);
                verts.Add(center + (basisU * Mathf.Cos(a) + basisV * Mathf.Sin(a)) * radius);
                uvs.Add(new Vector2(u, 0.5f)); normals.Add(faceN);
            }

            for (int s = 0; s < steps; s++)
                tris.AddRange(new[] { centerIdx, arcStart + s, arcStart + s + 1 });
        }

        static bool IsFoldSeg(int i, List<List<Vector3>> allNormals, List<DotType> dotTypes) {
            Vector3 n1, n2;
            return IsEdgeSegment(allNormals[i], allNormals[i + 1], dotTypes[i], dotTypes[i + 1], out n1, out n2);
        }

        static float FaceIndexFromNormal(Vector3 n) {
            if (n.y >  0.5f) return 0f; if (n.y < -0.5f) return 1f;
            if (n.x < -0.5f) return 2f; if (n.x >  0.5f) return 3f;
            if (n.z >  0.5f) return 4f; return 5f;
        }

        static Vector3 PrimaryNormal(List<Vector3> faceNormals) => faceNormals != null && faceNormals.Count > 0 ? faceNormals[0] : Vector3.up;

        static Vector3 GetCorrectedLift(List<Vector3> normals, float offset) {
            if (normals == null || normals.Count == 0) return Vector3.zero;
            if (normals.Count == 1) return normals[0] * offset;
            Vector3 bisector = Vector3.zero; foreach (var n in normals) bisector += n;
            bisector.Normalize();
            float dot = Vector3.Dot(normals[0], bisector);
            if (dot < 0.001f) return bisector * offset;
            return bisector * (offset / dot);
        }

        static Vector3 GetCorrectedLift(Vector3 n1, Vector3 n2, float offset) => GetCorrectedLift(new List<Vector3> { n1, n2 }, offset);

        static bool IsEdgeSegment(List<Vector3> facesA, List<Vector3> facesB, DotType typeA, DotType typeB, out Vector3 n1, out Vector3 n2) {
            n1 = Vector3.zero; n2 = Vector3.zero;
            if (typeA == DotType.Face || typeB == DotType.Face) return false;
            var shared = new List<Vector3>(2);
            foreach (var nA in facesA) foreach (var nB in facesB) if (Vector3.Dot(nA, nB) > 0.99f) { shared.Add(nA); break; }
            if (shared.Count < 2) return false;
            n1 = shared[0]; n2 = shared[1]; return true;
        }

        static Vector3 InwardDir(Vector3 faceNormal, Vector3 edgeDir, Vector3 otherFaceNormal) {
            Vector3 d = Vector3.Cross(faceNormal, edgeDir).normalized;
            if (Vector3.Dot(d, otherFaceNormal) > 0) d = -d; return d;
        }

        static void AddFoldQuads(Vector3 a, Vector3 b, Vector3 dir, Vector3 n1, Vector3 n2, float width, float offset, float u0, float u1, List<Vector3> verts, List<int> tris, List<Vector2> uvs, List<Vector3> normals) {
            Vector3 in1 = InwardDir(n1, dir, n2); Vector3 in2 = InwardDir(n2, dir, n1);
            Vector3 lift1 = n1 * offset; Vector3 lift2 = n2 * offset; Vector3 edgeLift = GetCorrectedLift(n1, n2, offset);
            AddQuad(a + edgeLift, a + in1 * width + lift1, b + in1 * width + lift1, b + edgeLift, n1, n1, n1, n1, u0, u1, verts, tris, uvs, normals);
            AddQuad(a + edgeLift, a + in2 * width + lift2, b + in2 * width + lift2, b + edgeLift, n2, n2, n2, n2, u0, u1, verts, tris, uvs, normals);
        }

        static void AddFoldTip(Vector3 tipPos, Vector3 tipDir, Vector3 n1, Vector3 n2, List<Vector3> tipNormals, float width, float length, float offset, float uBase, List<Vector3> verts, List<int> tris, List<Vector2> uvs, List<Vector3> normals) {
            Vector3 in1 = InwardDir(n1, tipDir, n2); Vector3 in2 = InwardDir(n2, tipDir, n1);
            Vector3 edgeLift = GetCorrectedLift(n1, n2, offset); Vector3 apexLift = GetCorrectedLift(tipNormals, offset);
            Vector3 apex = tipPos + apexLift; Vector3 basePos = tipPos - tipDir * length; Vector3 baseCenter = basePos + edgeLift;
            Vector3 wing1 = basePos + in1 * width + n1 * offset; Vector3 wing2 = basePos + in2 * width + n2 * offset;
            float uBack = Mathf.Max(0f, uBase - length / (length + 0.001f) * uBase);

            // Face 1 triangle
            int ti = verts.Count;
            verts.AddRange(new[] { apex, wing1, baseCenter });
            uvs.AddRange(new[] { new Vector2(uBase, 0.5f), new Vector2(uBack, 0f), new Vector2(uBack, 1f) });
            tris.AddRange(new[] { ti, ti+1, ti+2 });
            normals.Add(n1); normals.Add(n1); normals.Add(n1);

            // Face 2 triangle
            ti = verts.Count;
            verts.AddRange(new[] { apex, baseCenter, wing2 });
            uvs.AddRange(new[] { new Vector2(uBase, 0.5f), new Vector2(uBack, 1f), new Vector2(uBack, 0f) });
            tris.AddRange(new[] { ti, ti+1, ti+2 });
            normals.Add(n2); normals.Add(n2); normals.Add(n2);

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
            foreach (var faceN in faces) {
                Vector3 basisU = Vector3.Cross(faceN, Vector3.up); if (basisU.sqrMagnitude < 0.001f) basisU = Vector3.Cross(faceN, Vector3.forward);
                basisU.Normalize(); Vector3 basisV = Vector3.Cross(basisU, faceN).normalized;
                var angleList = new List<float>(segments + 4); for (int s = 0; s <= segments; s++) angleList.Add(s * Mathf.PI * 2f / segments);
                foreach (var otherN in faces) {
                    if (otherN == faceN) continue; Vector3 seam = Vector3.Cross(faceN, otherN); if (seam.sqrMagnitude < 0.001f) continue;
                    seam.Normalize(); InsertAngle(angleList, Atan2Basis(seam, basisU, basisV)); InsertAngle(angleList, Atan2Basis(-seam, basisU, basisV));
                }
                angleList.Sort(); int centerIdx = verts.Count; verts.Add(center + GetCorrectedLift(faces, offset));
                uvs.Add(new Vector2(u, 0.5f)); normals.Add(faceN);
                int arcStart = verts.Count; int arcCount = 0;
                foreach (float ang in angleList) {
                    Vector3 dir = basisU * Mathf.Cos(ang) + basisV * Mathf.Sin(ang);
                    bool belongs = true; foreach (var otherN in faces) if (otherN != faceN && Vector3.Dot(dir, otherN) > 0.02f) { belongs = false; break; }
                    if (!belongs) continue; var vFaces = new List<Vector3> { faceN };
                    foreach (var otherN in faces) if (otherN != faceN && Mathf.Abs(Vector3.Dot(dir, otherN)) < 0.05f) vFaces.Add(otherN);
                    verts.Add(center + dir * radius + GetCorrectedLift(vFaces, offset)); uvs.Add(new Vector2(u, 0.5f)); normals.Add(faceN); arcCount++;
                }

                for (int i = 0; i < arcCount - 1; i++)
                {
                    tris.Add(centerIdx); tris.Add(arcStart + i); tris.Add(arcStart + i + 1);
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
            tris.AddRange(new[] { bi, bi+1, bi+2,  bi, bi+2, bi+3 });
            normals.Add(n0); normals.Add(n1); normals.Add(n2); normals.Add(n3);
        }
    }
}
