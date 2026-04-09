using UnityEngine;
using System.Collections.Generic;

namespace EscapeED
{
    /// <summary>
    /// Handles arrow animations (Eject, Blocked Shake).
    /// Decoupled from Mesh generation, focuses on path sampling and interpolation.
    /// </summary>
    public class ArrowAnimator
    {
        private readonly Arrow _owner;
        
        public ArrowAnimator(Arrow owner)
        {
            _owner = owner;
        }

        public System.Collections.IEnumerator EjectSequence(
            List<Vector3> originalPositions,
            List<List<Vector3>> originalNormals,
            List<DotType> originalDotTypes)
        {
            int n = originalPositions.Count;

            // 1. CAPTURE: Freeze world positions BEFORE detaching.
            // Normals stay in local space — after detach the arrow's transform retains the cube's
            // rotation, so TransformObjectToWorldNormal in the shader converts them correctly.
            // Converting to world here would cause a double-transform: once here, once in the shader.
            var worldPositions = new List<Vector3>(n);
            foreach (var p in originalPositions)
                worldPositions.Add(_owner.transform.TransformPoint(p));

            // gridStep derived from world positions — correct regardless of cube scale.
            float gridStep = worldPositions.Count >= 2
                ? Vector3.Distance(worldPositions[0], worldPositions[1])
                : 0.28f;

            // 2. DETACH: Now safe to break the transform chain.
            // worldPositionStays=true preserves the arrow's world transform — safe for any cube position/rotation.
            _owner.transform.SetParent(null, true);
            _owner.gameObject.layer = LayerMask.NameToLayer(ArrowConstants.LAYER_EJECTING_ARROW);

            // 3. INITIALIZE BUFFER: Use the original world positions directly — sharp corners
            // are preserved exactly. Dense sub-sampling (M/subStep) gives the flexible
            // snake-like appearance without rounding the bends.
            var pathBuffer = new List<Vector3>(n * 2);
            foreach (var wp in worldPositions) pathBuffer.Add(wp);

            // Prepend a ghost point one gridStep behind the tail so the sampler has full arc on
            // frame 0 — prevents tail dots bunching at buffer[0] for the first ~100ms.
            Vector3 tailDir = (worldPositions[1] - worldPositions[0]).normalized;
            pathBuffer.Insert(0, worldPositions[0] - tailDir * gridStep);

            Vector3 headDir = (pathBuffer[pathBuffer.Count - 1] - pathBuffer[pathBuffer.Count - 2]).normalized;
            float speed = gridStep / ArrowConstants.EJECT_STEP_TIME;

            // 4. ANIMATION DATA: Sub-sample the path at 10x density for flexible snake-like movement.
            // M = n*S positions at subStep spacing. Each group of S consecutive samples inherits
            // the primary face normal of its original dot — all as Face type so ArrowMeshBuilder
            // draws simple flat quads (no fold caps on intermediate sub-samples).
            const int S       = 10;
            int       M       = (n - 1) * S + 1;  // exactly spans (n-1)*gridStep arc-length
            float     subStep = gridStep / S;

            // Use exit face normal for all snake segments.
            // Per-section normals cause perpendicular segments when face boundary slides through
            // the snake: adjacent samples get Cross(up,dir) vs Cross(right,dir) as their right
            // vectors, which are 90° apart — quads visually pop perpendicular to each other.
            // One consistent normal eliminates this entirely. Tail may sit slightly off its original
            // face plane on multi-face arrows, but that is far less visible than perpendicular quads.
            var exitNormal     = new List<Vector3> { originalNormals[n - 1].Count > 0 ? originalNormals[n - 1][0] : Vector3.up };
            var sampledPos     = new List<Vector3>(M);
            var activeNormals  = new List<List<Vector3>>(M);
            var activeDotTypes = new List<DotType>(M);
            for (int j = 0; j < M; j++)
            {
                activeNormals.Add(exitNormal);
                activeDotTypes.Add(DotType.Face);
            }

            // Track the total arc length currently held in pathBuffer so we can trim old
            // front entries that SamplePathBufferAll will never reach.
            // Maximum arc needed = (M-1)*subStep = (n-1)*gridStep, plus one gridStep safety margin.
            float bufferedArc = 0f;
            for (int i = 1; i < pathBuffer.Count; i++)
                bufferedArc += Vector3.Distance(pathBuffer[i - 1], pathBuffer[i]);
            float arcNeeded = (M - 1) * subStep + gridStep;

            float totalDist = (n + 4) * gridStep;
            float traveled  = 0f;

            while (traveled < totalDist)
            {
                float step = speed * Time.deltaTime;
                traveled += step;
                pathBuffer.Add(pathBuffer[pathBuffer.Count - 1] + headDir * step);
                bufferedArc += step;

                // Trim front entries that are beyond the arc window we'll ever sample.
                while (pathBuffer.Count > 2)
                {
                    float frontSeg = Vector3.Distance(pathBuffer[0], pathBuffer[1]);
                    if (bufferedArc - frontSeg <= arcNeeded) break;
                    bufferedArc -= frontSeg;
                    pathBuffer.RemoveAt(0);
                }

                SamplePathBufferAll(pathBuffer, M, subStep, sampledPos);
                _owner.SetPath(sampledPos, activeNormals, activeDotTypes);
                yield return null;
            }

            // Rebuild as a straight n-point arrow at the current head position so the finale
            // shows a proper arrowhead instead of the sub-sampled flat ribbon.
            // finalePath is n evenly-spaced points ending at the current buffer head.
            Vector3 finaleHead = pathBuffer[pathBuffer.Count - 1];
            var finalePath  = new List<Vector3>(n);
            var finNormals  = new List<List<Vector3>>(n);
            var finTypes    = new List<DotType>(n);
            for (int i = 0; i < n; i++)
            {
                finalePath.Add(finaleHead - headDir * (n - 1 - i) * gridStep);
                finNormals.Add(exitNormal);
                finTypes.Add(DotType.Face);
            }
            _owner.SetPath(finalePath, finNormals, finTypes);

            // Finale: launch speedup
            float speed2 = speed;
            float elapsed = 0f;
            while (elapsed < ArrowConstants.EJECT_FINAL_DURATION)
            {
                elapsed += Time.deltaTime;
                speed2 += ArrowConstants.EJECT_LAUNCH_ACCEL * Time.deltaTime;
                _owner.transform.position += headDir * speed2 * Time.deltaTime;
                yield return null;
            }

            Object.Destroy(_owner.gameObject);
        }


        public System.Collections.IEnumerator BlockedShakeSequence(
            List<Vector3> originalPositions,
            List<List<Vector3>> originalNormals,
            List<DotType> originalDotTypes)
        {
            int n = originalPositions.Count;

            var worldPositions = new List<Vector3>(n);
            for (int i = 0; i < n; i++)
                worldPositions.Add(_owner.transform.TransformPoint(originalPositions[i]));

            Vector3 originalHead = worldPositions[n - 1];
            Vector3 headDir      = (worldPositions[n - 1] - worldPositions[n - 2]).normalized;
            float   gridStep     = Vector3.Distance(worldPositions[0], worldPositions[1]);
            float   pushDist     = gridStep * ArrowConstants.SHAKE_PUSH_DIST_MULT;
            float   speed        = gridStep / ArrowConstants.SHAKE_STEP_TIME;

            const int S       = 10;
            int       M       = (n - 1) * S + 1;
            float     subStep = gridStep / S;

            // Fixed buffer: original n points + one virtual head slot.
            // Moving only the head slot avoids a V-shape in the buffer (no stretch/tear).
            var animBuffer = new List<Vector3>(n + 1);
            foreach (var wp in worldPositions) animBuffer.Add(wp);
            animBuffer.Add(originalHead); // virtual head slot (index n)

            var (primaryNormalLists, flatNormal) = BuildPrimaryNormalLists(originalNormals, n);

            var sampledPos    = new List<Vector3>(M);
            var activeNormals  = new List<List<Vector3>>(M);
            var activeDotTypes = new List<DotType>(M);
            for (int j = 0; j < M; j++) { activeNormals.Add(null); activeDotTypes.Add(DotType.Face); }
            for (int j = 0; j < M; j++)
            {
                int srcIdx        = Mathf.FloorToInt(j / (float)S);
                activeNormals[j]  = srcIdx >= n ? flatNormal : primaryNormalLists[srcIdx];
                activeDotTypes[j] = DotType.Face;
            }

            // Push — slide virtual head forward along headDir
            float headOffset = 0f;
            while (headOffset < pushDist)
            {
                float step = Mathf.Min(speed * Time.deltaTime, pushDist - headOffset);
                headOffset += step;
                animBuffer[n] = originalHead + headDir * headOffset;
                SamplePathBufferAll(animBuffer, M, subStep, sampledPos);
                _owner.SetPath(sampledPos, activeNormals, activeDotTypes);
                yield return null;
            }

            // Pull — slide virtual head back to original position
            while (headOffset > 0f)
            {
                float step = Mathf.Min(speed * Time.deltaTime, headOffset);
                headOffset -= step;
                animBuffer[n] = originalHead + headDir * headOffset;
                SamplePathBufferAll(animBuffer, M, subStep, sampledPos);
                _owner.SetPath(sampledPos, activeNormals, activeDotTypes);
                yield return null;
            }

            // Restore to perfect local coordinates (skipping world-to-local conversion)
            _owner.SetPath(originalPositions, originalNormals, originalDotTypes, false);
        }

        public static List<List<Vector3>> DeepCopyNormals(List<List<Vector3>> source)
        {
            var copy = new List<List<Vector3>>(source.Count);
            foreach (var inner in source)
                copy.Add(new List<Vector3>(inner));
            return copy;
        }

        // Shared by both EjectSequence and BlockedShakeSequence.
        // Extracts the primary (first) normal per dot into single-element lists so ArrowMeshBuilder
        // treats all sub-samples as Face type — no fold caps on intermediate dense samples.
        private static (List<List<Vector3>> primaryLists, List<Vector3> flatNormal)
            BuildPrimaryNormalLists(List<List<Vector3>> normals, int n)
        {
            var primaryLists = new List<List<Vector3>>(n);
            for (int i = 0; i < n; i++)
            {
                var primary = normals[i].Count > 0 ? normals[i][0] : Vector3.up;
                primaryLists.Add(new List<Vector3> { primary });
            }
            var flat = new List<Vector3> { normals[n - 1].Count > 0 ? normals[n - 1][0] : Vector3.up };
            return (primaryLists, flat);
        }

        private static void SamplePathBufferAll(List<Vector3> buffer, int dotCount, float gridStep, List<Vector3> results)
        {
            results.Clear();
            for (int i = 0; i < dotCount; i++) results.Add(Vector3.zero);
            results[dotCount - 1] = buffer[buffer.Count - 1];

            int   nextDot     = 1;
            float accumulated = 0f;

            for (int i = buffer.Count - 1; i > 0 && nextDot < dotCount; i--)
            {
                float segLen = Vector3.Distance(buffer[i], buffer[i - 1]);
                while (nextDot < dotCount)
                {
                    float targetDist = nextDot * gridStep;
                    if (accumulated + segLen < targetDist) break;
                    float t = (targetDist - accumulated) / segLen;
                    results[dotCount - 1 - nextDot] = Vector3.Lerp(buffer[i], buffer[i - 1], t);
                    nextDot++;
                }
                accumulated += segLen;
            }

            while (nextDot < dotCount)
            {
                results[dotCount - 1 - nextDot] = buffer[0];
                nextDot++;
            }
        }
    }
}
