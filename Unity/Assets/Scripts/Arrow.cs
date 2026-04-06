using UnityEngine;
using System.Collections.Generic;
using EscapeED.InputHandling;

namespace EscapeED
{
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class Arrow : MonoBehaviour, IInteractable
    {
        [Header("Visuals")]
        public Material arrowEjectMaterial;

        [Header("Shape")]
        public float lineWidth      = ArrowConstants.DEFAULT_LINE_WIDTH;
        public float tipLengthMult  = ArrowConstants.DEFAULT_TIP_LEN_MULT; 
        public float tipWidthMult   = ArrowConstants.DEFAULT_TIP_WID_MULT; 
        public float surfaceOffset  = ArrowConstants.DEFAULT_SURFACE_OFFSET;

        [Header("Shading")]
        public bool  smoothShading = false;

        // Components
        private MeshFilter   mf;
        private MeshRenderer mr;

        // Specialized Handlers
        private ArrowPhysicsHandler _physics;
        private ArrowAnimator       _animator;

        // Buffers (Reference-passed to Builder to avoid allocations)
        private readonly List<Vector3> _verts       = new List<Vector3>(512);
        private readonly List<int>     _tris        = new List<int>(1024);
        private readonly List<Vector2> _uvs         = new List<Vector2>(512);
        private readonly List<Vector3> _meshNormals = new List<Vector3>(512);
        private List<Vector3>          _tipVerts    = new List<Vector3>(8);
        private readonly List<Vector3> _localScratch = new List<Vector3>(64);

        // State Snapshot — written only in SetPath when !isEjecting && !isAnimating. Never mutate in-place.
        private List<Vector3>       originalPositions;
        private List<List<Vector3>> originalNormals;
        private List<DotType>       originalDotTypes;
        private bool                isEjecting  = false;
        private bool                isAnimating = false;
        public  bool                IsEjecting => isEjecting;
        private Coroutine           activeShake;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void SetupPhysics()
        {
            int ejectLayer = LayerMask.NameToLayer(ArrowConstants.LAYER_EJECTING_ARROW);
            if (ejectLayer != -1)
                Physics.IgnoreLayerCollision(ejectLayer, ejectLayer, true);
        }

        void Awake()
        {
            mf = GetComponent<MeshFilter>();
            mr = GetComponent<MeshRenderer>();

            _physics  = new ArrowPhysicsHandler(this);
            _animator = new ArrowAnimator(this);

            int arrowLayer = LayerMask.NameToLayer(ArrowConstants.LAYER_ARROW);
            if (arrowLayer != -1) gameObject.layer = arrowLayer;

        }

        public void SetPath(List<Vector3> positions, List<List<Vector3>> allNormals, List<DotType> dotTypes, bool useWorldSpace = true)
        {
            if (positions == null || positions.Count == 0) return;

            // 1. Snapshot for non-animating state (The Local-Space Contract)
            if (!isEjecting && !isAnimating)
            {
                // Note: ArrowAnimator handles the restore via SetPath(..., false)
                // so we don't snapshot during animation/ejection to avoid corrupting the buffer.
                originalPositions = new List<Vector3>(useWorldSpace ? ProjectToLocal(positions) : positions);
                originalNormals   = ArrowAnimator.DeepCopyNormals(allNormals);
                originalDotTypes  = new List<DotType>(dotTypes);
                
                VerifyLocalSpace(originalPositions);
            }

            // 2. Setup Context
            var ctx = CreateBuildContext(positions, allNormals, dotTypes, useWorldSpace);

            // 3. Execution Pipeline (Delegated to ArrowMeshBuilder)
            ArrowMeshBuilder.BuildBody(ctx);
            ArrowMeshBuilder.BuildTailCap(ctx);
            ArrowMeshBuilder.BuildBends(ctx);
            ArrowMeshBuilder.BuildTip(ctx, out _tipVerts); 
            ArrowMeshBuilder.FinalizeMesh(mf.sharedMesh, ctx, smoothShading);

            // 4. Update Colliders (Delegated to ArrowPhysicsHandler)
            if (!isEjecting)
            {
                _physics.UpdateSegmentColliders(ctx.localPos, ctx.allNormals, ctx.dotTypes, lineWidth, surfaceOffset);
                _physics.UpdateTipCollider(_tipVerts);
            }
        }

        private List<Vector3> ProjectToLocal(List<Vector3> worldPositions)
        {
            _localScratch.Clear();
            foreach (var p in worldPositions) _localScratch.Add(transform.InverseTransformPoint(p));
            return _localScratch;
        }

        private ArrowMeshBuilder.Context CreateBuildContext(List<Vector3> positions, List<List<Vector3>> allNormals, List<DotType> dotTypes, bool useWorldSpace)
        {
            if (mf.sharedMesh == null)
            {
                mf.sharedMesh = new Mesh { name = "Arrow_" + name };
                mf.sharedMesh.MarkDynamic();
            }

            int n = positions.Count;
            float tLen = lineWidth * tipLengthMult;
            float tHalfWid = (lineWidth * tipWidthMult) * 0.5f;

            var localPos = new List<Vector3>(n);
            for (int i = 0; i < n; i++)
            {
                if (useWorldSpace)
                    localPos.Add(transform.InverseTransformPoint(positions[i]));
                else
                    localPos.Add(positions[i]);
            }

            float[] dist = new float[n];
            dist[0] = 0f;
            for (int i = 1; i < n; i++)
                dist[i] = dist[i - 1] + Vector3.Distance(localPos[i - 1], localPos[i]);
            float totalDist = dist[n - 1] + (dotTypes[n-1] == DotType.Face ? tLen : 0);

            _verts.Clear(); _tris.Clear(); _uvs.Clear(); _meshNormals.Clear();

            Vector3 preTipDir = (localPos[n - 1] - localPos[n - 2]).normalized;
            Vector3 lastValid = preTipDir.sqrMagnitude > ArrowConstants.EPS ? preTipDir : Vector3.forward;

            return new ArrowMeshBuilder.Context
            {
                positions     = positions,
                allNormals    = allNormals,
                dotTypes      = dotTypes,
                lineWidth     = lineWidth,
                surfaceOffset = surfaceOffset,
                tipLength     = tLen,
                tipHalfWidth  = tHalfWid,
                localPos      = localPos,
                dist          = dist,
                totalDist     = totalDist,
                verts         = _verts,
                tris          = _tris,
                uvs           = _uvs,
                meshNormals   = _meshNormals,
                lastValidDir  = lastValid,
                n             = n,
                isEjecting    = isEjecting
            };
        }

        [ContextMenu("Eject Arrow")]
        public void Eject()
        {
            if (isEjecting) return;
            isEjecting = true;
            _physics.DisableAllColliders();
            if (arrowEjectMaterial != null) mr.material = arrowEjectMaterial;
            float gridStep = (originalPositions.Count >= 2) ? Vector3.Distance(originalPositions[0], originalPositions[1]) : 0.28f;
            StartCoroutine(_animator.EjectSequence(originalPositions, originalNormals, originalDotTypes, gridStep));
        }

        public void PlayBlockedAnimation()
        {
            if (isEjecting) return;
            if (activeShake != null) StopCoroutine(activeShake);
            isAnimating = true;
            activeShake = StartCoroutine(RunShakeSequence());
        }

        private System.Collections.IEnumerator RunShakeSequence()
        {
            yield return StartCoroutine(_animator.BlockedShakeSequence(originalPositions, originalNormals, originalDotTypes));
            isAnimating = false;
            activeShake = null;
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
            faceNormal = transform.TransformDirection(originalNormals[n - 1].Count > 0 ? originalNormals[n - 1][0] : Vector3.up);
        }

        public void OnInteract()
        {
            if (!isEjecting) OnInteractionTriggered?.Invoke(this);
        }

        public event System.Action<Arrow> OnInteractionTriggered;
        
        private void VerifyLocalSpace(List<Vector3> localPoints)
        {
            if (localPoints == null || localPoints.Count == 0) return;
            // Typical cube size is small. If points are > 50 units from origin, 
            // they are almost certainly world coordinates being misidentified as local.
            foreach (var p in localPoints)
            {
                if (p.sqrMagnitude > 2500f) // 50^2
                {
                    Debug.LogError($"[EscapeED] Coordinate Space Violation: {name} stored world positions in local originalPositions buffer. This will cause 'Ghost Paths' after rotation.");
                    break;
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (_physics == null) return;
            Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
            foreach (var col in _physics.GetColliders())
            {
                if (col == null || !col.gameObject.activeSelf) continue;
                Gizmos.matrix = col.transform.localToWorldMatrix;
                Gizmos.DrawCube(col.center, col.size);
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(col.center, col.size);
            }
        }
    }
}
