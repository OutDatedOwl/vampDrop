using UnityEngine;
using Unity.Entities;
using Vampire.DropPuzzle;
using System.Collections.Generic;

namespace Vampire.DropPuzzle
{
    public class ManualPuzzleBuilder : MonoBehaviour
    {
        [Header("Prefabs & Materials")]
        public GameObject WallPrefab;
        public Material WallMaterial;
        public Material BackgroundMaterial;
        public Color DropZoneColor = Color.yellow;
        public Color BackgroundColor = new Color(0.1f, 0.1f, 0.15f, 1f);

        [Header("Background")]
        public Vector3 BackgroundPosition = new Vector3(-2f, 1f, -50f);
        public Vector3 BackgroundScale = new Vector3(50f, 50f, 1f);

        [Header("Wall Settings")]
        public float WallWidth = 0.75f;
        public float WallHeight = 1f;
        
        private List<GameObject> spawnedObjects = new List<GameObject>();

        private void Start()
        {
            // Debug.Log("[ManualPuzzleBuilder] Starting manual puzzle build...");
            BuildPuzzle();
        }

        public void BuildPuzzle()
        {
            ClearPuzzle();
            CreateBackground();
            CreateWalls();
            CreateDropZones();
            CreateGoalZone();
        }

        private void CreateBackground()
        {
            GameObject background = GameObject.CreatePrimitive(PrimitiveType.Quad);
            background.name = "Background";
            background.transform.position = BackgroundPosition;
            background.transform.localScale = BackgroundScale;

            Renderer bgRenderer = background.GetComponent<Renderer>();
            if (BackgroundMaterial != null)
            {
                bgRenderer.material = BackgroundMaterial;
            }
            else
            {
                Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = BackgroundColor;
                bgRenderer.material = mat;
            }

            spawnedObjects.Add(background);
            // Debug.Log($"[ManualPuzzleBuilder] Created background at {BackgroundPosition}");
        }

        private void CreateWalls()
        {
            // MANUAL WALL POSITIONS - Edit these coordinates directly!
            Vector3[] wallPositions = {
                // Top row walls
                new Vector3(-4f, 8f, 0f),    // Left top wall
                new Vector3(4f, 8f, 0f),     // Right top wall
                
                // Side walls - left
                new Vector3(-2.5f, 6f, 0f), 
                new Vector3(-1f, 4f, 0f),
                new Vector3(-0.5f, 2f, 0f),
                
                // Side walls - right
                new Vector3(2.5f, 6f, 0f),
                new Vector3(1f, 4f, 0f),
                new Vector3(0.5f, 2f, 0f),
            };

            for (int i = 0; i < wallPositions.Length; i++)
            {
                CreateWall(wallPositions[i], 0f, $"Wall_{i}");
            }

            // DIAGONAL WALLS - Edit these positions and rotations!
            CreateWall(new Vector3(-3f, 7f, 0f), 45f, "DiagonalWall_Left");
            CreateWall(new Vector3(3f, 7f, 0f), -45f, "DiagonalWall_Right");
        }

        private void CreateDropZones()
        {
            // MANUAL DROP ZONE POSITIONS - Edit these coordinates!
            Vector3[] dropZonePositions = {
                new Vector3(-2.5f, 8f, 0f),
                new Vector3(-1f, 8f, 0f), 
                new Vector3(0f, 8f, 0f),
                new Vector3(1f, 8f, 0f),
                new Vector3(2.5f, 8f, 0f),
            };

            for (int i = 0; i < dropZonePositions.Length; i++)
            {
                CreateDropZone(dropZonePositions[i], $"DropZone_{i}");
            }
        }

        private void CreateGoalZone()
        {
            // MANUAL GOAL POSITION - Edit this coordinate for perfect centering!
            Vector3 goalPosition = new Vector3(0f, 0f, 0f); // Center goal at X=0

            GameObject goal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            goal.name = "GoalGate";
            goal.transform.position = goalPosition;
            goal.transform.localScale = new Vector3(3f, 0.5f, 0.5f); // Wide goal area

            // Make it a trigger
            Collider col = goal.GetComponent<Collider>();
            col.isTrigger = true;

            // Green goal material
            Renderer renderer = goal.GetComponent<Renderer>();
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = Color.green;
            mat.SetFloat("_Metallic", 0.0f);
            mat.SetFloat("_Smoothness", 0.5f);
            renderer.material = mat;

            // Add goal component
            GoalGate goalComponent = goal.AddComponent<GoalGate>();
            
            spawnedObjects.Add(goal);
            // Debug.Log($"[ManualPuzzleBuilder] Created goal at {goalPosition}");
        }

        private void CreateWall(Vector3 position, float rotationZ, string wallName)
        {
            GameObject wall;
            
            if (WallPrefab != null)
            {
                wall = Instantiate(WallPrefab, position, Quaternion.Euler(0, 0, rotationZ));
            }
            else
            {
                wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.transform.position = position;
                wall.transform.rotation = Quaternion.Euler(0, 0, rotationZ);
                
                if (WallMaterial != null) 
                {
                    wall.GetComponent<Renderer>().material = WallMaterial;
                }
            }

            wall.name = wallName;
            wall.tag = "Wall";

            // Scale based on whether it's diagonal
            bool isDiagonal = (Mathf.Abs(rotationZ) == 45f);
            Vector3 scale;
            
            if (isDiagonal)
            {
                float length = Mathf.Sqrt(2f) * 2.5f; // Diagonal length
                scale = new Vector3(WallWidth, length, 1.5f);
            }
            else
            {
                scale = new Vector3(WallWidth, WallHeight, 1.5f);
            }
            
            wall.transform.localScale = scale;
            wall.transform.SetParent(transform);
            spawnedObjects.Add(wall);
            
            // Debug.Log($"[ManualPuzzleBuilder] Created {wallName} at {position}");
        }

        private void CreateDropZone(Vector3 position, string zoneName)
        {
            GameObject dropZone = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dropZone.name = zoneName;
            dropZone.tag = "DropZone"; 
            dropZone.transform.position = position;
            dropZone.transform.localScale = new Vector3(2f, 0.1f, 1f);

            // Make it a trigger
            Collider col = dropZone.GetComponent<Collider>();
            col.isTrigger = true;

            // Yellow drop zone material
            Renderer renderer = dropZone.GetComponent<Renderer>();
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = DropZoneColor;
            mat.SetFloat("_Metallic", 0.0f);
            mat.SetFloat("_Smoothness", 0.3f);
            renderer.material = mat;

            dropZone.transform.SetParent(transform);
            spawnedObjects.Add(dropZone);
            
            // Debug.Log($"[ManualPuzzleBuilder] Created {zoneName} at {position}");
        }

        public void ClearPuzzle()
        {
            foreach (GameObject obj in spawnedObjects)
            {
                if (obj != null) DestroyImmediate(obj);
            }
            spawnedObjects.Clear();
            // Debug.Log("[ManualPuzzleBuilder] Cleared previous puzzle elements");
        }

        // Button in inspector to rebuild during development
        [ContextMenu("Rebuild Puzzle")]
        public void RebuildPuzzle()
        {
            BuildPuzzle();
        }

        private void OnValidate()
        {
            // Auto-rebuild when values change in inspector (optional)
            if (Application.isPlaying && spawnedObjects.Count > 0)
            {
                BuildPuzzle();
            }
        }
    }
}