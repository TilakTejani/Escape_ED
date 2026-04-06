using UnityEngine;

namespace EscapeED
{
    public static class ArrowConstants
    {
        // --- Shape & Mesh Defaults ---
        public const float DEFAULT_LINE_WIDTH    = 0.08f;
        public const float DEFAULT_TIP_LEN_MULT  = 2.5f;
        public const float DEFAULT_TIP_WID_MULT  = 2.5f;
        public const float DEFAULT_SURFACE_OFFSET = 0.005f;

        // --- Geometric Thresholds ---
        public const float EPS             = 0.0001f; // Precision threshold
        public const float MIN_SEG_LEN      = 0.01f;   // Minimum segment to prevent degenerate mesh
        public const float STRAIGHT_DOT_THR = 0.99f;   // Threshold for straight line detection
        public const float FOLD_DOT_THR     = 0.99f;   // Threshold for common normal detection
        public const float BEND_ANGLE_STEP  = Mathf.PI / 8f; // Subdivision step for rounded joins
        
        // --- Physics & Layers ---
        public const string LAYER_ARROW           = "Arrow";
        public const string LAYER_EJECTING_ARROW  = "EjectingArrow";
        public const float  COLLIDER_NORMAL_EPS   = 0.02f; // Offset for collider proximity
        public const float  MITER_LIFT_THR        = 0.05f; // Dot threshold for multi-face vertex lift

        // --- Animation Timings & Speeds ---
        public const float EJECT_STEP_TIME       = 0.10f; // Seconds per grid step during ejection
        public const float SHAKE_STEP_TIME       = 0.08f; // Seconds per grid step during shake
        public const float SHAKE_PUSH_DIST_MULT  = 0.50f; // Multiplier of grid step for push depth
        public const float EJECT_LAUNCH_ACCEL    = 40.0f; // Acceleration after leaving the cube
        public const float EJECT_FINAL_DURATION  = 0.40f; // Duration of off-screen launch phase
    }
}
