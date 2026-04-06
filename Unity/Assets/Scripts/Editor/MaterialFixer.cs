using UnityEngine;
using UnityEditor;
using System.IO;

namespace EscapeED.Editor
{
    public class ProjectFixer : EditorWindow
    {
        [MenuItem("EscapeED/Fix Materials & Auto-Wire References")]
        public static void PerformFullSetup()
        {
            Debug.Log("EscapeED: Starting Full Project Repair...");

            // 1. Scene Object Setup
            LevelManager manager = FindAnyObjectByType<LevelManager>();
            CubeGrid grid = FindAnyObjectByType<CubeGrid>();

            if (manager != null)
            {
                // Auto-Wire Local References
                if (grid != null) manager.grid = grid;
                
                // Ensure Rotator exists
                CubeRotator rotator = manager.GetComponent<CubeRotator>();
                if (rotator == null) rotator = manager.gameObject.AddComponent<CubeRotator>();
                Debug.Log("✅ Validated Interaction: CubeRotator added/found.");

                // Find and Assign 5x5x5 Level Json
                string[] jsonGuids = AssetDatabase.FindAssets("5x5x5 t:TextAsset");
                if (jsonGuids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(jsonGuids[0]);
                    manager.levelJsonFile = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                    Debug.Log($"✅ Assigned Level Data: {path}");
                }

                // Find and Assign Prefabs if missing
                if (manager.arrowPrefab == null) {
                    string[] prefabGuids = AssetDatabase.FindAssets("ArrowContainer t:Prefab");
                    if (prefabGuids.Length > 0) manager.arrowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(prefabGuids[0]));
                }
                
                // Auto-assign ArrowPulseMat directly to the MeshRenderer on the prefab
                if (manager.arrowPrefab != null)
                {
                    MeshRenderer mr = manager.arrowPrefab.GetComponent<MeshRenderer>();
                    if (mr != null && mr.sharedMaterial == null)
                    {
                        Material pulseMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/ArrowPulseMat.mat");
                        if (pulseMat != null)
                        {
                            mr.sharedMaterial = pulseMat;
                            EditorUtility.SetDirty(manager.arrowPrefab);
                            Debug.Log("✅ Assigned ArrowPulseMat to ArrowContainer MeshRenderer.");
                        }
                    }
                }

                EditorUtility.SetDirty(manager);
            }

            // 2. Material Repair (Shader Fixing)
            Shader arrowShader = Shader.Find("EscapeED/ArrowPulsing");
            Material arrowMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/ArrowPulseMat.mat");
            if (arrowMat != null && arrowShader != null) {
                arrowMat.shader = arrowShader;
                EditorUtility.SetDirty(arrowMat);
            }

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            Material whiteMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/WhiteMat.mat");
            if (whiteMat != null && urpLit != null) {
                whiteMat.shader = urpLit;
                whiteMat.SetColor("_BaseColor", Color.white);
                EditorUtility.SetDirty(whiteMat);
                
                if (grid != null) {
                    grid.whiteMaterial = whiteMat;
                    EditorUtility.SetDirty(grid);
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log("EscapeED: Full Project Repair Complete! You can now Right-Click LevelManager -> Generate.");
        }
    }
}
