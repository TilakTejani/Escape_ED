using UnityEngine;
using System.Collections.Generic;

namespace EscapeED
{
    /// <summary>
    /// Stores the arrow face normal (local space) on a collider child.
    /// InteractionSystem reads this to reject taps on back-facing segments.
    /// </summary>
    public class ArrowSegmentFace : MonoBehaviour
    {
        public Vector3 localFaceNormal;
    }

    /// <summary>
    /// Manages collision for the Arrow system.
    /// Handles pooling of segment colliders and dynamic mesh-based tip collider.
    /// </summary>
    public class ArrowPhysicsHandler
    {
        private readonly Arrow _owner;
        private readonly List<BoxCollider> _segmentColliders = new List<BoxCollider>();
        private readonly List<int>         _tipTris          = new List<int>(32);
        private MeshCollider _tipColliderComp;
        private Mesh         _tipColliderMesh;

        public ArrowPhysicsHandler(Arrow owner)
        {
            _owner = owner;
        }

        public void UpdateSegmentColliders(List<Vector3> localPos, List<List<Vector3>> allNormals, List<DotType> dotTypes, float lineWidth, float surfaceOffset)
        {
            int segIndex = 0;
            int n = localPos.Count;
            if (n < 2) return;

            Vector3 lastValidDir = (localPos[1] - localPos[0]).normalized;
            if (lastValidDir.sqrMagnitude < ArrowConstants.EPS) lastValidDir = Vector3.forward;

            for (int i = 0; i < n - 1; i++)
            {
                Vector3 start  = localPos[i];
                Vector3 end    = localPos[i + 1];
                float   length = Vector3.Distance(start, end);

                if (length < ArrowConstants.MIN_SEG_LEN)
                {
                    end    = start + lastValidDir * ArrowConstants.MIN_SEG_LEN;
                    length = ArrowConstants.MIN_SEG_LEN;
                }

                Vector3 center = (start + end) / 2f;
                Vector3 dir    = (end - start).normalized;
                if (dir.sqrMagnitude > ArrowConstants.EPS)
                    lastValidDir = dir;
                else
                    dir = lastValidDir;

                Vector3 faceN = (dotTypes[i] == DotType.Face) ? allNormals[i][0] : 
                                (dotTypes[i+1] == DotType.Face ? allNormals[i+1][0] : allNormals[i][0]);

                Quaternion rot = Quaternion.LookRotation(dir, faceN);

                BoxCollider col;
                if (segIndex < _segmentColliders.Count)
                {
                    col = _segmentColliders[segIndex];
                    col.gameObject.SetActive(true);
                }
                else
                {
                    var obj = new GameObject($"Seg_{segIndex}");
                    obj.transform.SetParent(_owner.transform, false);
                    obj.layer = _owner.gameObject.layer;
                    col = obj.AddComponent<BoxCollider>();
                    obj.AddComponent<ArrowSegmentFace>();
                    _segmentColliders.Add(col);
                }

                col.transform.localPosition = center;
                col.transform.localRotation = rot;
                col.center = new Vector3(0, surfaceOffset, 0);
                col.size   = new Vector3(lineWidth, lineWidth, length);

                // Always update face normal — faceN may change if path is reassigned.
                col.GetComponent<ArrowSegmentFace>().localFaceNormal = faceN;
                segIndex++;
            }

            for (int i = segIndex; i < _segmentColliders.Count; i++)
                if (_segmentColliders[i] != null) _segmentColliders[i].gameObject.SetActive(false);
        }

        public void UpdateTipCollider(List<Vector3> tipVerts)
        {
            if (tipVerts == null || tipVerts.Count < 3) return;

            if (_tipColliderComp == null)
            {
                var obj = new GameObject("TipCollider");
                obj.transform.SetParent(_owner.transform, false);
                obj.layer = _owner.gameObject.layer;
                _tipColliderComp = obj.AddComponent<MeshCollider>();
                _tipColliderComp.convex = true;
                _tipColliderMesh = new Mesh { name = "TipConvex" };
                _tipColliderMesh.MarkDynamic();
            }

            _tipColliderMesh.Clear();
            _tipColliderMesh.SetVertices(tipVerts);

            _tipTris.Clear();
            for (int i = 1; i < tipVerts.Count - 1; i++)
            {
                _tipTris.Add(0); _tipTris.Add(i); _tipTris.Add(i + 1);
            }
            if (_tipTris.Count == 0) return;
            _tipColliderMesh.SetTriangles(_tipTris, 0);

            _tipColliderComp.sharedMesh = null;
            _tipColliderComp.sharedMesh = _tipColliderMesh;
            _tipColliderComp.gameObject.SetActive(true);
        }

        public void DisableAllColliders()
        {
            foreach (var col in _segmentColliders) if (col != null) col.gameObject.SetActive(false);
            if (_tipColliderComp != null) _tipColliderComp.gameObject.SetActive(false);
        }
        
        public IEnumerable<BoxCollider> GetColliders() => _segmentColliders;
    }
}
