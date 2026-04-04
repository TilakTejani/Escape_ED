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
            float gridStep)
        {
            int n = originalPositions.Count;
            var pathBuffer = new List<Vector3>(n + 256);
            for (int i = 0; i < n; i++)
                pathBuffer.Add(_owner.transform.TransformPoint(originalPositions[i]));

            Vector3 headDir = (pathBuffer[n - 1] - pathBuffer[n - 2]).normalized;
            float speed = gridStep / 0.10f;

            // Detach and set layer
            _owner.transform.SetParent(null, false);
            _owner.gameObject.layer = LayerMask.NameToLayer(ArrowConstants.LAYER_EJECTING_ARROW);

            // Exit visuals
            Vector3 exitNormal = (originalNormals[n - 1].Count > 0) ? originalNormals[n - 1][0] : Vector3.up;
            var animNormals = new List<List<Vector3>>(n);
            var animTypes   = new List<DotType>(n);
            for (int i = 0; i < n; i++)
            {
                animNormals.Add(new List<Vector3> { exitNormal });
                animTypes.Add(DotType.Face);
            }

            var sampledPos = new List<Vector3>(n);
            float totalDist = (n + 10) * gridStep;
            float traveled = 0f;

            while (traveled < totalDist)
            {
                float step = speed * Time.deltaTime;
                traveled += step;
                pathBuffer.Add(pathBuffer[pathBuffer.Count - 1] + headDir * step);
                SamplePathBufferAll(pathBuffer, n, gridStep, sampledPos);
                _owner.SetPath(sampledPos, animNormals, animTypes);
                yield return null;
            }

            // Finale: launch speedup
            float speed2 = speed;
            float elapsed = 0f;
            while (elapsed < 1.5f)
            {
                elapsed += Time.deltaTime;
                speed2 += 1.2f * Time.deltaTime;
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
            var pathBuffer = new List<Vector3>(n + 64);
            for (int i = 0; i < n; i++)
                pathBuffer.Add(_owner.transform.TransformPoint(originalPositions[i]));

            Vector3 headDir = (pathBuffer[n - 1] - pathBuffer[n - 2]).normalized;
            float gridStep = Vector3.Distance(pathBuffer[0], pathBuffer[1]);
            float pushDist = gridStep * 0.5f;
            float speed    = gridStep / 0.08f;

            var sampledPos = new List<Vector3>(n);

            // Push
            float traveled = 0f;
            while (traveled < pushDist)
            {
                float step = Mathf.Min(speed * Time.deltaTime, pushDist - traveled);
                traveled += step;
                pathBuffer.Add(pathBuffer[pathBuffer.Count - 1] + headDir * step);
                SamplePathBufferAll(pathBuffer, n, gridStep, sampledPos);
                _owner.SetPath(sampledPos, originalNormals, originalDotTypes);
                yield return null;
            }

            // Pull
            traveled = 0f;
            while (traveled < pushDist)
            {
                float step = Mathf.Min(speed * Time.deltaTime, pushDist - traveled);
                traveled += step;
                pathBuffer.Add(pathBuffer[pathBuffer.Count - 1] - headDir * step);
                SamplePathBufferAll(pathBuffer, n, gridStep, sampledPos);
                _owner.SetPath(sampledPos, originalNormals, originalDotTypes);
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
