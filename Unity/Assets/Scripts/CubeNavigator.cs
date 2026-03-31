using UnityEngine;
using System.Collections.Generic;

namespace EscapeED
{
    public enum MoveDir { Up, Down, Left, Right }

    public class CubeNavigator : MonoBehaviour
    {
        private CubeGrid grid;

        void Awake()
        {
            grid = GetComponent<CubeGrid>();
        }

        public Vector3Int GetNextPoint(Vector3Int current, MoveDir dir)
        {
            Vector3 normal = grid.GetSurfaceNormal(current);
            Vector3 moveVec = GetMoveVector(normal, dir);
            
            Vector3Int next = current + Vector3Int.RoundToInt(moveVec);

            if (grid.IsSurface(next.x, next.y, next.z) && grid.GetSurfaceNormal(next) == normal)
            {
                return next;
            }

            Vector3Int wrapCandidate = next + Vector3Int.RoundToInt(-normal);
            if (grid.IsSurface(wrapCandidate.x, wrapCandidate.y, wrapCandidate.z))
            {
                return wrapCandidate;
            }
            
            return current;
        }

        private Vector3 GetMoveVector(Vector3 normal, MoveDir dir)
        {
            Vector3 up, right;

            if (Mathf.Abs(normal.y) > 0.9f)
            {
                up = Vector3.forward;
                right = Vector3.right;
            }
            else
            {
                up = Vector3.up;
                right = Vector3.Cross(Vector3.up, normal);
            }

            switch (dir)
            {
                case MoveDir.Up: return up;
                case MoveDir.Down: return -up;
                case MoveDir.Left: return -right;
                case MoveDir.Right: return right;
                default: return Vector3.zero;
            }
        }
    }
}
