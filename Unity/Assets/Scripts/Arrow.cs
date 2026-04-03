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
        public float tipLengthMult  = 2.5f; // New: Arrowhead length relative to lineWidth
        public float tipWidthMult   = 2.5f; // New: Arrowhead base width relative to lineWidth
        public float surfaceOffset  = 0.005f;

        // Hidden from inspector as they are now derived
        [HideInInspector] public float tipLength; 

        private MeshFilter   mf;
        private MeshRenderer mr;
        private MeshCollider mc;

        private List<Vector3>      originalPositions;
        private List<List<Vector3>> originalNormals;
        private List<DotType>      originalDotTypes;
        private bool               isEjecting = false;
        public  bool               IsEjecting => isEjecting;
        private Coroutine          activeShake;

        void Awake()
        {
            mf = GetComponent<MeshFilter>();
            mr = GetComponent<MeshRenderer>();
            mc = GetComponent<MeshCollider>();

            // Automatically set layer to "Arrow" so it's detectable by the LevelManager's LayerMask
            int arrowLayer = LayerMask.NameToLayer("Arrow");
            if (arrowLayer != -1) gameObject.layer = arrowLayer;

            if (arrowMaterial != null) mr.material = arrowMaterial;
        }

        /// <summary>
        /// Builds the arrow mesh.
        /// allNormals[i] = all face normals for positions[i].
        ///   - 1 normal  → face-interior vertex  → flat quad
        ///   - 2 normals → edge vertex            → fold when both ends share the same edge
        /// </summary>
        public void SetPath(List<Vector3> positions, List<List<Vector3>> allNormals, List<DotType> dotTypes)
        {
            if (positions.Count < 2) return;

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

            // Reuse existing mesh instance to prevent memory leaks during animation
            if (mf.sharedMesh == null) 
            {
                mf.sharedMesh = new Mesh { name = "Arrow_" + name };
                mf.sharedMesh.MarkDynamic();
            }
            Mesh mesh = mf.sharedMesh;
            mesh.Clear();

            // Calculate dynamic arrowhead dimensions
            tipLength = lineWidth * tipLengthMult;
            float tipHalfWidth = (lineWidth * tipWidthMult) * 0.5f;

            int   n        = positions.Count;
            float halfW    = lineWidth * 0.5f;

            // ── Local Conversion ──────────────────────────────────────────────────
            // Convert world-space positions to local-space relative to the arrow.
            // This prevents misalignment if the Cube/LevelManager is moved from (0,0,0).
            var localPos = new List<Vector3>(n);
            for (int i = 0; i < n; i++)
                localPos.Add(transform.InverseTransformPoint(positions[i]));

            // Cumulative distances for continuous UV
            float[] dist = new float[n];
            dist[0] = 0f;
            for (int i = 1; i < n; i++)
                dist[i] = dist[i - 1] + Vector3.Distance(localPos[i - 1], localPos[i]);
            float totalDist = dist[n - 1] + tipLength;

            var verts       = new List<Vector3>();
            var tris        = new List<int>();
            var uvs         = new List<Vector2>();
            var meshNormals = new List<Vector3>();

            // ── Body ─────────────────────────────────────────────────────────────
            // Pre-compute tip direction so the last segment can be trimmed if needed.
            Vector3 preTipDir = (positions[n - 1] - positions[n - 2]).normalized;

            for (int i = 0; i < n - 1; i++)
            {
                Vector3 a = localPos[i];
                Vector3 b = localPos[i + 1];

                // Trim the last segment so it ends where the arrowhead base begins,
                // preventing the body from overlapping the wider arrowhead wings.
                // SAFETY: Only trim if the segment is long enough to avoid degenerate flipping.
                float segLen = Vector3.Distance(a, b);
                if (i == n - 2 && dotTypes[n - 1] != DotType.Face && segLen > tipLength + 0.01f)
                    b = localPos[n - 1] - preTipDir * tipLength;
                
                // Adjust U1 UV scaling if we trimmed
                float actualU1 = (i == n - 2 && b != localPos[n - 1])
                    ? (dist[n - 1] - tipLength) / totalDist
                    : dist[i + 1] / totalDist;

                float u0 = dist[i] / totalDist;
                float u1 = actualU1;

                Vector3 nA = PrimaryNormal(allNormals[i]);
                Vector3 nB = PrimaryNormal(allNormals[i + 1]);

                // Check if both vertices share the same cube edge (2 common face normals)
                Vector3 edgeN1, edgeN2;
                if (IsEdgeSegment(allNormals[i], allNormals[i + 1], dotTypes[i], dotTypes[i + 1], out edgeN1, out edgeN2))
                {
                    // ── Fold: two quads, one per face ─────────────────────────────
                    Vector3 dir = (b - a).normalized;
                    AddFoldQuads(a, b, dir, edgeN1, edgeN2,
                                 halfW, surfaceOffset, u0, u1,
                                 verts, tris, uvs, meshNormals);
                }
                else
                {
                    // ── Normal: single quad on face-interior's face ───────────────
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


            // ── Tail cap ─────────────────────────────────────────────────────────
            AddFoldedRoundCap(localPos[0], allNormals[0], halfW, 16, 0f, surfaceOffset,
                              verts, tris, uvs, meshNormals);

            // ── Round joins at bends ──────────────────────────────────────────────
            // Each bend gets:
            //   • an outer arc  — fills the convex gap between the two segment ends
            //   • an inner bevel triangle — fills the small concave gap
            // No overlap with the body quads → no overdraw → no dark dots.
            for (int i = 1; i < n - 1; i++)
            {
                if (IsFoldSeg(i - 1, allNormals, dotTypes)) continue;
                if (IsFoldSeg(i,     allNormals, dotTypes)) continue;

                if (dotTypes[i] != DotType.Face)
                {
                    // Edge/corner bend: AddFoldedRoundCap handles multi-face arc splitting correctly.
                    // Minor overlap with segment ends is acceptable vs a flat cut.
                    AddFoldedRoundCap(localPos[i], allNormals[i], halfW, 16,
                                      dist[i] / totalDist, surfaceOffset,
                                      verts, tris, uvs, meshNormals);
                    continue;
                }

                Vector3 dirIn  = (localPos[i]     - localPos[i - 1]).normalized;
                Vector3 dirOut = (localPos[i + 1] - localPos[i]    ).normalized;
                if (Vector3.Dot(dirIn, dirOut) > 0.99f) continue; // straight — nothing to fill

                Vector3 faceN  = PrimaryNormal(allNormals[i]);
                Vector3 lift   = faceN * surfaceOffset;
                Vector3 center = localPos[i] + lift;

                Vector3 rightIn  = Vector3.Cross(faceN, dirIn ).normalized;
                Vector3 rightOut = Vector3.Cross(faceN, dirOut).normalized;

                // Determine which side is outside (convex) based on turn direction
                float turn        = Vector3.Dot(Vector3.Cross(dirIn, dirOut), faceN);
                float outsideSign = turn >= 0f ? -1f : 1f;
                float insideSign  = -outsideSign;

                // Outer arc — exactly fills the convex gap, no segment overlap
                Vector3 fromVec = rightIn  * outsideSign * halfW;
                Vector3 toVec   = rightOut * outsideSign * halfW;
                float   u       = dist[i] / totalDist;
                AddBendArc(center, fromVec, toVec, faceN, halfW, u,
                           verts, tris, uvs, meshNormals);

                // Inner bevel triangle — fills the small concave gap
                Vector3 innerA = center + rightIn  * insideSign * halfW;
                Vector3 innerB = center + rightOut * insideSign * halfW;
                int ti = verts.Count;
                verts.AddRange(new[] { center, innerA, innerB });
                uvs.AddRange(new[] {
                    new Vector2(u, 0.5f), new Vector2(u, 0f), new Vector2(u, 1f)
                });
                // Correct Winding: 1, 2, 0 for Clockwise/Outward from Center
                tris.AddRange(new[] { ti + 1, ti + 2, ti });
                meshNormals.Add(faceN); meshNormals.Add(faceN); meshNormals.Add(faceN);
            }

            // ── Tip ──────────────────────────────────────────────────────────────
            Vector3 tipPos = localPos[n - 1];
            Vector3 tipDir = (localPos[n - 1] - localPos[n - 2]).normalized;
            float   uBase  = dist[n - 1] / totalDist;

            if (dotTypes[n - 1] != DotType.Face)
            {
                // Tip is on an edge or corner — apex pulled back to tipPos so nothing floats off surface.
                Vector3 edgeTN1, edgeTN2;
                bool lastSegOnEdge = IsEdgeSegment(allNormals[n - 2], allNormals[n - 1],
                                                   dotTypes[n - 2], dotTypes[n - 1],
                                                   out edgeTN1, out edgeTN2);
                if (lastSegOnEdge)
                {
                    // Incoming segment runs along an edge → fold base across both faces.
                    // Pass tip's full normals so apex uses correct lift (corner = 3 normals).
                    // tipHalfWidth is (lineWidth * tipWidthMult) * 0.5f
                    AddFoldTip(tipPos, tipDir, edgeTN1, edgeTN2, allNormals[n - 1],
                               tipHalfWidth, tipLength, surfaceOffset, uBase,
                               verts, tris, uvs, meshNormals);
                }
                else
                {
                    // Incoming segment is on a face → single flat reversed tip on that face
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
                        new Vector2(0.5f, 1f)
                    });
                    // Correct Winding: Outward (baseR -> baseL -> apex)
                    tris.AddRange(new[] { ti + 2, ti + 1, ti });
                    meshNormals.Add(faceN); meshNormals.Add(faceN); meshNormals.Add(faceN);
                }
            }
            else
            {
                // Tip on face interior — flat forward-pointing triangle
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
                // Winding: baseR -> baseL -> apex
                tris.AddRange(new[] { ti + 2, ti + 1, ti });
                meshNormals.Add(tipNormal); meshNormals.Add(tipNormal); meshNormals.Add(tipNormal);
            }

            // ── Build mesh ───────────────────────────────────────────────────────
            // Derive face index per vertex from its normal, stored in UV2 for per-face fading.
            var uv2s = new Vector2[meshNormals.Count];
            for (int vi = 0; vi < meshNormals.Count; vi++)
                uv2s[vi] = new Vector2(FaceIndexFromNormal(meshNormals[vi]), 0f);

            mesh.vertices  = verts.ToArray();
            mesh.triangles = tris.ToArray();
            mesh.uv        = uvs.ToArray();
            mesh.uv2       = uv2s;
            mesh.normals   = meshNormals.ToArray();
            mesh.RecalculateBounds();
            // Assign to MeshFilter and MeshCollider
            mf.sharedMesh = mesh;
            if (mc == null) mc = GetComponent<MeshCollider>();
            if (mc != null) 
            {
                mc.sharedMesh = null; // Force Unity physics state to recalculate
                mc.sharedMesh = mesh;
            }
            
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

            // Snapshot world-space positions before detaching from the cube
            Vector3[] worldPos = new Vector3[n];
            for (int i = 0; i < n; i++)
                worldPos[i] = transform.TransformPoint(originalPositions[i]);

            // Head direction in world space (parallel to the face it's on)
            Vector3 headDir = (worldPos[n - 1] - worldPos[n - 2]).normalized;

            // One grid step = distance between adjacent dots
            float gridStep = Vector3.Distance(worldPos[0], worldPos[1]);

            // Local sliding copies of normals/types — these shift forward each step
            // so the mesh builds correctly as positions move away from their original face
            var slideNormals  = new List<List<Vector3>>(originalNormals);
            var slideDotTypes = new List<DotType>(originalDotTypes);

            // Detach from cube — localPos=0, localRot=identity preserved, so local==world after detach
            transform.SetParent(null, false);

            // Move to EjectingArrow layer — URP Render Objects feature re-renders
            // this layer after opaques with ZTest Always, so it's never clipped by the cube.
            gameObject.layer = LayerMask.NameToLayer("EjectingArrow");

            // ── Phase 1: on-cube dot-by-dot slide ─────────────────────────────────
            // Each step: body shifts forward one dot; tail drops, head advances.
            // First n steps = arrow moves its own length (still mostly on cube).
            // Extra steps push it off the edge entirely.
            int   onCubeSteps   = n;
            int   exitSteps     = 10;
            float onStepDur     = 0.10f; // seconds per step while on cube
            float exitStepDur   = 0.12f; // seconds per step past the edge

            Vector3[] stepFrom = (Vector3[])worldPos.Clone();
            Vector3[] stepTo   = new Vector3[n];

            int totalSteps = onCubeSteps + exitSteps;
            for (int step = 0; step < totalSteps; step++)
            {
                float dur = step < onCubeSteps ? onStepDur : exitStepDur;

                // Build target: each point moves to where the NEXT point was
                for (int i = 0; i < n - 1; i++) stepTo[i] = stepFrom[i + 1];
                stepTo[n - 1] = stepFrom[n - 1] + headDir * gridStep;

                // Shift normals/dotTypes forward in sync with the position shift
                slideNormals.RemoveAt(0);
                slideNormals.Add(new List<Vector3>(slideNormals[slideNormals.Count - 1]));
                slideDotTypes.RemoveAt(0);
                slideDotTypes.Add(slideDotTypes[slideDotTypes.Count - 1]);

                // Smooth interpolate from → to
                float elapsed = 0f;
                while (elapsed < dur)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / dur));
                    for (int i = 0; i < n; i++)
                        worldPos[i] = Vector3.Lerp(stepFrom[i], stepTo[i], t);
                    SetPath(new List<Vector3>(worldPos), slideNormals, slideDotTypes);
                    yield return null;
                }

                // Commit step
                System.Array.Copy(stepTo, stepFrom, n);
                System.Array.Copy(stepTo, worldPos, n);
            }

            // ── Phase 2: launch off screen ─────────────────────────────────────────
            // Arrow is already a straight line after Phase 1 — just slide the
            // transform forward. No mesh rebuild needed, avoids degenerate geometry.
            float speed    = 0.2f;
            float accel    = 1.2f;
            float elapsed2 = 0f;

            while (elapsed2 < 1.5f)
            {
                elapsed2           += Time.deltaTime;
                speed              += accel * Time.deltaTime;
                transform.position += headDir * speed * Time.deltaTime;
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

        // ── Helpers ───────────────────────────────────────────────────────────────

        // Fills the outer convex gap at a bend with a circular arc fan.
        // fromVec and toVec are the two outer-edge end vectors (already scaled by radius).
        // The arc sweeps the shortest path from fromVec to toVec in the face plane.
        static void AddBendArc(
            Vector3 center, Vector3 fromVec, Vector3 toVec,
            Vector3 faceN, float radius, float u,
            List<Vector3> verts, List<int> tris,
            List<Vector2> uvs, List<Vector3> normals)
        {
            Vector3 basisU = fromVec.normalized;
            Vector3 basisV = Vector3.Cross(faceN, basisU).normalized;

            float angleTo = Mathf.Atan2(
                Vector3.Dot(toVec, basisV),
                Vector3.Dot(toVec, basisU));

            int steps = Mathf.Max(2, Mathf.CeilToInt(Mathf.Abs(angleTo) / (Mathf.PI / 8f)));

            int centerIdx = verts.Count;
            verts.Add(center);
            uvs.Add(new Vector2(u, 0.5f));
            normals.Add(faceN);

            int arcStart = verts.Count;
            for (int s = 0; s <= steps; s++)
            {
                float   a   = Mathf.Lerp(0f, angleTo, (float)s / steps);
                Vector3 dir = basisU * Mathf.Cos(a) + basisV * Mathf.Sin(a);
                verts.Add(center + dir * radius);
                uvs.Add(new Vector2(u, 0.5f));
                normals.Add(faceN);
            }

            for (int s = 0; s < steps; s++)
                tris.AddRange(new[] { centerIdx, arcStart + s + 1, arcStart + s });
        }

        // Returns true if segment i→i+1 crosses a cube edge (fold segment).
        static bool IsFoldSeg(int i, List<List<Vector3>> allNormals, List<DotType> dotTypes)
        {
            Vector3 n1, n2;
            return IsEdgeSegment(allNormals[i], allNormals[i + 1],
                                 dotTypes[i],   dotTypes[i + 1], out n1, out n2);
        }

        // Computes the miter side-offset at the join between two segments.
        // The result replaces `right * halfW` so adjacent segments share exact corner vertices.
        static Vector3 MiterOffset(Vector3 dirIn, Vector3 dirOut, Vector3 faceN, float halfW)
        {
            Vector3 rightIn  = Vector3.Cross(faceN, dirIn ).normalized;
            Vector3 rightOut = Vector3.Cross(faceN, dirOut).normalized;
            Vector3 miter    = rightIn + rightOut;
            if (miter.sqrMagnitude < 0.001f) return rightIn * halfW; // 180° turn fallback
            miter.Normalize();
            // Scale so the miter vertex lies exactly on both segment edge lines.
            // Clamped to 4× halfW to prevent extreme spikes on very sharp angles.
            float dot = Mathf.Max(Vector3.Dot(miter, rightIn), 0.25f);
            return miter * (halfW / dot);
        }

        // Maps a cube face normal to an index 0-5 used by the shader for per-face fading.
        // Must match the order in GhostCubeController.CubeFaceNormals.
        static float FaceIndexFromNormal(Vector3 n)
        {
            if (n.y >  0.5f) return 0f; // up
            if (n.y < -0.5f) return 1f; // down
            if (n.x < -0.5f) return 2f; // left
            if (n.x >  0.5f) return 3f; // right
            if (n.z >  0.5f) return 4f; // forward
            return 5f;                   // back
        }

        static Vector3 PrimaryNormal(List<Vector3> faceNormals)
            => faceNormals != null && faceNormals.Count > 0 ? faceNormals[0] : Vector3.up;

        /// <summary>
        /// Calculates a corrected lift vector for a point shared by multiple faces (edge or corner).
        /// This ensures the mesh stays exactly 'offset' distance from ALL adjacent faces,
        /// preventing the "bisector dip" that causes visual gaps/cuts at cube seams.
        /// </summary>
        static Vector3 GetCorrectedLift(List<Vector3> normals, float offset)
        {
            if (normals == null || normals.Count == 0) return Vector3.zero;
            if (normals.Count == 1) return normals[0] * offset;

            // Combine all face normals to find the bisector direction
            Vector3 bisector = Vector3.zero;
            foreach (var n in normals) bisector += n;
            bisector.Normalize();

            // Correct the magnitude so the projection onto any face normal equals 'offset'
            // Magnitude = offset / cos(angle between normal and bisector)
            float dot = Vector3.Dot(normals[0], bisector);
            if (dot < 0.001f) return bisector * offset; // Safety fallback

            return bisector * (offset / dot);
        }

        static Vector3 GetCorrectedLift(Vector3 n1, Vector3 n2, float offset)
        {
            return GetCorrectedLift(new List<Vector3> { n1, n2 }, offset);
        }

        /// <summary>
        /// Returns true when both vertex normal lists share exactly 2 common normals
        /// (i.e., both vertices lie on the same cube edge).
        /// </summary>
        static bool IsEdgeSegment(List<Vector3> facesA, List<Vector3> facesB,
                                   DotType typeA, DotType typeB,
                                   out Vector3 n1, out Vector3 n2)
        {
            n1 = Vector3.zero; n2 = Vector3.zero;
            // Fold whenever both endpoints are on a shared cube edge/corner.
            // Face dots are the only exclusion — Edge and Corner both participate.
            if (typeA == DotType.Face || typeB == DotType.Face) return false;

            var shared = new List<Vector3>(2);
            foreach (var nA in facesA)
                foreach (var nB in facesB)
                    if (Vector3.Dot(nA, nB) > 0.99f) { shared.Add(nA); break; }

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
            // Shared edge vertices sit on the bisector with Miter Join correction
            Vector3 edgeLift   = GetCorrectedLift(n1, n2, offset);

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
        /// Folded arrowhead when the tip sits on a cube edge and the incoming segment
        /// also runs along that edge. Apex sits AT tipPos (on the edge), base is pulled
        /// back along -tipDir — everything stays on the cube surface.
        /// Two triangles (one per face) share the apex and base-center vertices.
        /// </summary>
        static void AddFoldTip(
            Vector3 tipPos, Vector3 tipDir,
            Vector3 n1, Vector3 n2, List<Vector3> tipNormals,
            float width, float length, float offset, float uBase,
            List<Vector3> verts, List<int> tris,
            List<Vector2> uvs, List<Vector3> normals)
        {
            Vector3 in1      = InwardDir(n1, tipDir, n2);
            Vector3 in2      = InwardDir(n2, tipDir, n1);
            Vector3 edgeLift = GetCorrectedLift(n1, n2, offset);

            // Apex lift uses ALL normals at the tip dot (corrected for corners)
            Vector3 apexLift = GetCorrectedLift(tipNormals, offset);

            // Apex sits on the edge/corner at tipPos; base is recessed back along the path
            Vector3 apex       = tipPos  + apexLift;
            Vector3 basePos    = tipPos  - tipDir * length;
            Vector3 baseCenter = basePos + edgeLift;
            Vector3 wing1      = basePos + in1 * width + n1 * offset;
            Vector3 wing2      = basePos + in2 * width + n2 * offset;

            float uBack = Mathf.Max(0f, uBase - length / (length + 0.001f) * uBase);

            // Face 1 triangle
            int ti = verts.Count;
            verts.AddRange(new[] { apex, wing1, baseCenter });
            uvs.AddRange(new[] { new Vector2(uBase, 0.5f), new Vector2(uBack, 0f), new Vector2(uBack, 1f) });
            // Auto-detect winding for Face 1
            if (Vector3.Dot(Vector3.Cross(wing1 - baseCenter, apex - baseCenter), n1) < 0)
                tris.AddRange(new[] { ti, ti + 1, ti + 2 });
            else
                tris.AddRange(new[] { ti + 2, ti + 1, ti }); // Default Winding (2, 1, 0)
            normals.Add(n1); normals.Add(n1); normals.Add(n1);

            // Face 2 triangle
            ti = verts.Count;
            verts.AddRange(new[] { apex, baseCenter, wing2 });
            uvs.AddRange(new[] { new Vector2(uBase, 0.5f), new Vector2(uBack, 1f), new Vector2(uBack, 0f) });
            
            // Auto-detect winding for Face 2
            if (Vector3.Dot(Vector3.Cross(baseCenter - wing2, apex - wing2), n2) < 0)
                tris.AddRange(new[] { ti, ti + 1, ti + 2 });
            else
                tris.AddRange(new[] { ti + 2, ti + 1, ti }); // Default Winding (2, 1, 0)
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
                // Lift the center of the circular cap using all available normals
                // to match the miter join of the body precisely.
                verts.Add(center + GetCorrectedLift(faces, offset));
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

                    // Determine all faces this perimeter vertex touched (for miter lift)
                    var vertexFaces = new List<Vector3> { faceN };
                    foreach (var otherN in faces)
                        if (otherN != faceN && Mathf.Abs(Vector3.Dot(dir, otherN)) < 0.05f)
                            vertexFaces.Add(otherN);

                    verts.Add(center + dir * radius + GetCorrectedLift(vertexFaces, offset));
                    uvs.Add(new Vector2(u, 0.5f));
                    normals.Add(faceN);
                    arcCount++;
                }

                for (int i = 0; i < arcCount - 1; i++)
                {
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
            // Auto-detect if standard winding is inverted relative to the face normal
            if (Vector3.Dot(Vector3.Cross(v2 - v0, v1 - v0), n0) < 0)
            {
                // Inverted winding (0, 1, 2) and (0, 2, 3)
                tris.AddRange(new[] { bi, bi+1, bi+2,  bi, bi+2, bi+3 });
            }
            else
            {
                // Standard winding (0, 2, 1) and (0, 3, 2)
                tris.AddRange(new[] { bi, bi+2, bi+1,  bi, bi+3, bi+2 });
            }
            normals.Add(n0); normals.Add(n1); normals.Add(n2); normals.Add(n3);
        }
    }
}
