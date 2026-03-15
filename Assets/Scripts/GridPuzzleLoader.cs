using UnityEngine;
using System.Collections.Generic;

namespace Vampire.DropPuzzle
{
    /// <summary>
    /// Grid-based puzzle layout - much easier to visualize and edit
    /// Each cell in the grid represents a position in the puzzle
    /// </summary>
    [System.Serializable]
    public class PuzzleGridLayout
    {
        public string name = "Level 1";
        public int rows = 10;
        public int columns = 7;
        public float cellSize = 1.0f; // Size of each grid cell in world units
        public string[] grid; // Flat array: index = row * columns + col
        
        // Cell types:
        // "wall" - solid wall
        // "dz" - drop zone (empty space where balls spawn)
        // "empty" - empty space
        // "gate:x2" - multiplier gate (x2, x3, x4, etc.)
        // "goal" - goal zone
        
        // Helper to get cell at row, col
        public string GetCell(int row, int col)
        {
            int index = row * columns + col;
            if (index >= 0 && index < grid.Length)
                return grid[index];
            return "empty";
        }
    }
    
    /// <summary>
    /// Loads grid-based puzzles from JSON and builds them
    /// Way easier to design than manual positioning!
    /// </summary>
    public class GridPuzzleLoader : MonoBehaviour
    {
        [Header("Grid Configuration")]
        [Tooltip("Drag puzzle JSON file here (TextAsset)")]
        public TextAsset PuzzleJsonFile;
        
        [Header("Visual Settings")]
        public float StartY = 8f; // Top of the puzzle
        public float CellHeight = 1f; // Vertical spacing between rows
        public float WallHeight = 1f; // Height of wall objects
        public float WallWidth = 0.6f; // Thickness of walls (increased from 0.2 for better collision)
        
        [Header("Prefabs")]
        public GameObject WallPrefab;
        public GameObject RiceBallPrefab; // Required for multiplier gates
        public Material WallMaterial;
        
        [Header("Colors")]
        public Color DropZoneColor = Color.yellow;
        
        [Header("Background")]
        [Tooltip("Optional: Custom background prefab (leave null for default plane)")]
        public GameObject BackgroundPrefab;
        [Tooltip("Material for background (optional)")]
        public Material BackgroundMaterial;
        [Tooltip("Background color (if no material assigned)")]
        public Color BackgroundColor = new Color(0.1f, 0.1f, 0.15f, 1f); // Dark blue-grey
        [Tooltip("Background position")]
        public Vector3 BackgroundPosition = new Vector3(-2f, 1f, 50f);
        [Tooltip("Background scale")]
        public Vector3 BackgroundScale = new Vector3(50f, 50f, 1f);
        
        private List<GameObject> spawnedObjects = new List<GameObject>();
        private PuzzleGridLayout layout;
        private GameObject backgroundObject;
        
        private void Start()
        {
            Debug.Log("[GridPuzzleLoader] Starting...");
            LoadAndBuildPuzzle();
        }
        
        public void LoadAndBuildPuzzle()
        {
            ClearPuzzle();
            
            // Load JSON from TextAsset
            if (PuzzleJsonFile == null)
            {
                Debug.LogError("[GridPuzzleLoader] No PuzzleJsonFile assigned! Drag a JSON file to the PuzzleJsonFile field");
                CreateDefaultLayout();
                return;
            }
            
            try
            {
                Debug.Log($"[GridPuzzleLoader] Loading from {PuzzleJsonFile.name}");
                layout = JsonUtility.FromJson<PuzzleGridLayout>(PuzzleJsonFile.text);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[GridPuzzleLoader] Failed to parse JSON: {e.Message}");
                CreateDefaultLayout();
                return;
            }
            
            if (layout == null || layout.grid == null)
            {
                Debug.LogError("[GridPuzzleLoader] Failed to load layout");
                return;
            }
            
            Debug.Log($"[GridPuzzleLoader] Building puzzle: {layout.name} ({layout.rows}x{layout.columns})");
            BuildFromGrid();
            
            // CRITICAL: Refresh gate cache in interaction system after rebuilding
            var gateSystem = FindObjectOfType<RiceBallGateInteractionSystem>();
            if (gateSystem != null)
            {
                gateSystem.RefreshGates();
                Debug.Log("[GridPuzzleLoader] Refreshed gate interaction system after rebuild");
            }
        }
        
        private void CreateDefaultLayout()
        {
            Debug.Log("[GridPuzzleLoader] Creating default layout");
            
            layout = new PuzzleGridLayout
            {
                name = "Default Test Level",
                rows = 8,
                columns = 7,
                cellSize = 1.0f,
                grid = new string[]
                {
                    // Row 0
                    "wall", "dz", "dz", "dz", "dz", "dz", "wall",
                    // Row 1
                    "wall", "empty", "empty", "empty", "empty", "empty", "wall",
                    // Row 2
                    "wall", "empty", "gate:x2", "empty", "gate:x2", "empty", "wall",
                    // Row 3
                    "wall", "empty", "empty", "empty", "empty", "empty", "wall",
                    // Row 4
                    "wall", "gate:x3", "empty", "empty", "empty", "gate:x4", "wall",
                    // Row 5
                    "wall", "empty", "empty", "empty", "empty", "empty", "wall",
                    // Row 6
                    "wall", "empty", "gate:x5", "empty", "gate:x6", "empty", "wall",
                    // Row 7
                    "wall", "goal", "goal", "goal", "goal", "goal", "wall"
                }
            };
            
            Debug.Log("[GridPuzzleLoader] Created default layout");
        }
        
        private void BuildFromGrid()
        {
            Debug.Log("[GridPuzzleLoader] =================== UNIFIED GRID LOGIC ===================");
            
            // Create background behind puzzle
            CreateBackground();
            
            // UNIFIED GRID: Grid is source of truth, centered on X=0
            float totalWidth = (layout.columns - 1) * layout.cellSize;
            float startX = -totalWidth / 2.0f; // Start at negative half-width for centering
            float currentY = StartY;
            
            Debug.Log($"[GridPuzzleLoader] Grid: {layout.columns} columns, cellSize={layout.cellSize}");
            Debug.Log($"[GridPuzzleLoader] Total width: {totalWidth}, startX: {startX}");
            Debug.Log($"[GridPuzzleLoader] Middle column (3) will be at X=0: {startX + 3 * layout.cellSize}");
            
            for (int row = 0; row < layout.rows; row++)
            {
                for (int col = 0; col < layout.columns; col++)
                {
                    string cellType = layout.GetCell(row, col);
                    // Every cell gets a predictable grid position
                    Vector3 calculatedPosition = new Vector3(
                        startX + col * layout.cellSize,
                        currentY,
                        0
                    );
                    
                    if (cellType == "goal")
                    {
                        Debug.Log($"[GridPuzzleLoader] 🎯 GOAL: Row {row}, Col {col}, Position {calculatedPosition}");
                    }
                    
                    BuildCell(cellType, calculatedPosition, row, col);
                }
                
                currentY -= CellHeight;
            }
            
            Debug.Log("[GridPuzzleLoader] =================== GRID COMPLETE ===================");
        }
        
        /// <summary>
        /// Create full-screen background plane behind puzzle
        /// </summary>
        private void CreateBackground()
        {
            if (BackgroundPrefab != null)
            {
                // Use custom prefab
                backgroundObject = Instantiate(BackgroundPrefab, BackgroundPosition, Quaternion.identity);
                backgroundObject.name = "PuzzleBackground";
                backgroundObject.transform.rotation = Quaternion.Euler(0, 180, 0);
                backgroundObject.transform.localScale = BackgroundScale;
            }
            else
            {
                // Create default quad
                backgroundObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
                backgroundObject.name = "PuzzleBackground";
                backgroundObject.transform.position = BackgroundPosition;
                 backgroundObject.transform.rotation = Quaternion.Euler(0, 180, 0);
                backgroundObject.transform.localScale = BackgroundScale;
                
                // Remove collider (background shouldn't interact)
                var collider = backgroundObject.GetComponent<Collider>();
                if (collider != null)
                {
                    Destroy(collider);
                }
                
                // Apply material or color
                var renderer = backgroundObject.GetComponent<Renderer>();
                if (renderer != null)
                {
                    if (BackgroundMaterial != null)
                    {
                        renderer.material = BackgroundMaterial;
                    }
                    else
                    {
                        // Create simple colored material
                        renderer.material = new Material(Shader.Find("Unlit/Color"));
                        renderer.material.color = BackgroundColor;
                    }
                }
            }
            
            spawnedObjects.Add(backgroundObject);
            Debug.Log($"[GridPuzzleLoader] Created background at {BackgroundPosition} scale {BackgroundScale}");
        }
        
        private void BuildCell(string cellType, Vector3 position, int row, int col)
        {
            if (string.IsNullOrEmpty(cellType) || cellType == "empty" || cellType == "null") return;

            GameObject obj = null;

            // UNIFIED APPROACH: All objects use grid position as base
            if (cellType == "wall")
            {
                // Walls positioned at cell edges for clean connections
                Vector3 wallPosition = position;
                wallPosition.x += layout.cellSize * 0.5f; // Move to right edge of cell
                obj = BuildWall(wallPosition, 0, row, col);
            }
            else if (cellType == "wall\\" || cellType == "wall/")
            {
                // Diagonal walls also use edge positioning for connections
                Vector3 wallPosition = position;
                wallPosition.x += layout.cellSize * 0.5f;
                
                float angle = cellType == "wall\\" ? 45f : -45f;
                obj = BuildWall(wallPosition, angle, row, col);
            }
            else if (cellType == "dz")
            {
                obj = BuildDropZone(position);
            }
            else if (cellType.StartsWith("gate:"))
            {
                obj = BuildGateWithChances(position, cellType);
            }
            else if (cellType == "goal")
            {
                // Goals use exact grid position for perfect alignment
                obj = BuildGoalCell(position);
            }

            if (obj != null)
            {
                obj.transform.SetParent(transform);
                spawnedObjects.Add(obj);
            }
        }
        
       private GameObject BuildWall(Vector3 position, float zRotation, int row, int col)
        {
            GameObject wall;
            bool isDiagonal = (Mathf.Abs(zRotation) == 45f);
            Vector3 scale;

            if (isDiagonal)
            {
                // Calculate hypotenuse so diagonal fits perfectly corner-to-corner
                float length = Mathf.Sqrt(Mathf.Pow(layout.cellSize, 2) + Mathf.Pow(CellHeight, 2));
                scale = new Vector3(WallWidth, length * 1.05f, 1.5f); // 5% bleed for physics
            }
            else
            {
                // Use clean WallWidth without extra padding
                scale = new Vector3(WallWidth, CellHeight, 1.5f);
            }

            // Create Object
            if (WallPrefab != null)
            {
                wall = Instantiate(WallPrefab, position, Quaternion.Euler(0, 0, zRotation));
            }
            else
            {
                wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.transform.position = position;
                wall.transform.rotation = Quaternion.Euler(0, 0, zRotation);
                if (WallMaterial != null) wall.GetComponent<Renderer>().material = WallMaterial;
            }

            wall.transform.localScale = scale;
            wall.name = $"Wall_{row}_{col}";
            wall.tag = "Wall";

            // PHYSICS FIX: Safely add/get Rigidbody to prevent MissingComponentException
            Rigidbody rb = wall.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = wall.AddComponent<Rigidbody>();
            }
            
            rb.isKinematic = true; // Now safe to call
            rb.useGravity = false;

            // Ensure collider is a solid wall
            if (wall.TryGetComponent<Collider>(out Collider colld))
            {
                colld.isTrigger = false;
            }

            return wall;
        }
        
        /// <summary>
        /// Determines proper wall alignment based on grid position and neighbors
        /// - Left boundary walls: Align RIGHT (toward playable area)
        /// - Right boundary walls: Align LEFT (toward playable area)
        /// - Interior walls: Align toward the side with empty/playable space
        /// </summary>
        private Vector3 GetAlignedWallPosition(Vector3 centerPosition, int row, int col)
        {
            float offset = 0f;
            
            // BOUNDARY WALLS - these form the outer edges
            if (col == 0) // Left boundary
            {
                // Align to RIGHT edge of cell (block from outside, open to inside)
                offset = layout.cellSize * 0.5f;
                Debug.Log($"[GridPuzzleLoader] Left boundary wall at row {row}, col {col}: offset RIGHT by {offset}");
            }
            else if (col == layout.columns - 1) // Right boundary
            {
                // Align to LEFT edge of cell (block from outside, open to inside)
                offset = -layout.cellSize * 0.5f;
                Debug.Log($"[GridPuzzleLoader] Right boundary wall at row {row}, col {col}: offset LEFT by {offset}");
            }
            else // INTERIOR WALLS - need to check neighbors
            {
                // Check left and right neighbors to determine which side has playable space
                string leftCell = (col > 0) ? layout.GetCell(row, col - 1) : "wall";
                string rightCell = (col < layout.columns - 1) ? layout.GetCell(row, col + 1) : "wall";
                
                bool leftIsPlayable = IsPlayableCell(leftCell);
                bool rightIsPlayable = IsPlayableCell(rightCell);
                
                if (leftIsPlayable && !rightIsPlayable)
                {
                    // Playable space on left, align RIGHT to block from right side
                    offset = layout.cellSize * 0.5f;
                    Debug.Log($"[GridPuzzleLoader] Interior wall at row {row}, col {col}: LEFT playable, align RIGHT");
                }
                else if (rightIsPlayable && !leftIsPlayable)
                {
                    // Playable space on right, align LEFT to block from left side
                    offset = -layout.cellSize * 0.5f;
                    Debug.Log($"[GridPuzzleLoader] Interior wall at row {row}, col {col}: RIGHT playable, align LEFT");
                }
                else if (leftIsPlayable && rightIsPlayable)
                {
                    // Playable space on BOTH sides - this wall divides the playing field
                    // Keep centered (no offset) to block both paths equally
                    offset = 0f;
                    Debug.Log($"[GridPuzzleLoader] Interior wall at row {row}, col {col}: BOTH sides playable, CENTER");
                }
                else
                {
                    // Neither side playable (surrounded by walls/empty) - shouldn't happen normally
                    // Default to center
                    offset = 0f;
                    Debug.LogWarning($"[GridPuzzleLoader] Interior wall at row {row}, col {col}: No playable neighbors?");
                }
            }
            
            return new Vector3(centerPosition.x + offset, centerPosition.y, centerPosition.z);
        }
        
        /// <summary>
        /// Check if a diagonal wall should connect to another diagonal above it
        /// wall\\ (45Â°): Check rows above in same or left columns
        /// wall/ (-45Â°): Check rows above in same or right columns
        /// </summary>
        private bool ShouldConnectToAbove(int currentRow, int currentCol, float rotation)
        {
            // Don't check if we're in the first row
            if (currentRow <= 0) return false;
            
            // Check up to 2 rows above for connecting diagonals
            for (int checkRow = currentRow - 1; checkRow >= Mathf.Max(0, currentRow - 2); checkRow--)
            {
                if (rotation == 45f) // wall\\ - slopes from top-left to bottom-right
                {
                    // Check same column and left column for another wall\\
                    for (int checkCol = currentCol; checkCol >= Mathf.Max(0, currentCol - 1); checkCol--)
                    {
                        string cell = layout.GetCell(checkRow, checkCol);
                        if (cell == "wall\\")
                        {
                            Debug.Log($"[GridPuzzleLoader] Found wall\\\\ above at ({checkRow},{checkCol}) for current ({currentRow},{currentCol})");
                            return true;
                        }
                    }
                }
                else if (rotation == -45f) // wall/ - slopes from top-right to bottom-left
                {
                    // Check same column and right column for another wall/
                    for (int checkCol = currentCol; checkCol <= Mathf.Min(layout.columns - 1, currentCol + 1); checkCol++)
                    {
                        string cell = layout.GetCell(checkRow, checkCol);
                        if (cell == "wall/")
                        {
                            Debug.Log($"[GridPuzzleLoader] Found wall/ above at ({checkRow},{checkCol}) for current ({currentRow},{currentCol})");
                            return true;
                        }
                    }
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Check if a cell type represents playable space (not a wall or empty border)
        /// </summary>
        private bool IsPlayableCell(string cellType)
        {
            if (string.IsNullOrEmpty(cellType)) return false;
            if (cellType == "wall" || cellType.StartsWith("wall")) return false;
            if (cellType == "empty" || cellType == "null") return true; // Empty space is playable
            if (cellType == "dz") return true; // Drop zone
            if (cellType.StartsWith("gate:")) return true; // Gates
            if (cellType == "goal") return true; // Goal
            return true; // Default to playable for unknown types
        }
        
        private GameObject BuildDropZone(Vector3 position)
        {
            // Visual indicator for drop zone (optional, just for debugging)
            GameObject dz = GameObject.CreatePrimitive(PrimitiveType.Quad);
            dz.transform.position = position;
            dz.transform.localScale = new Vector3(layout.cellSize * 0.5f, CellHeight * 0.5f, 1);
            dz.transform.rotation = Quaternion.Euler(0, 0, 0);
            
            Renderer renderer = dz.GetComponent<Renderer>();
            Material mat = new Material(Shader.Find("Unlit/Color"));
            mat.color = new Color(DropZoneColor.r, DropZoneColor.g, DropZoneColor.b, 0.3f);
            renderer.material = mat;
            
            // Remove collider (just visual)
            Destroy(dz.GetComponent<Collider>());
            
            dz.name = "DropZone";
            return dz;
        }
        
        private GameObject BuildMultiplierGate(Vector3 position, int multiplier)
        {
            GameObject gate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gate.transform.position = position;
            gate.transform.localScale = new Vector3(layout.cellSize * 0.8f, CellHeight * 0.8f, 1.0f); // Larger trigger zone
            gate.name = $"Gate_x{multiplier}"; // Set name BEFORE adding component
            
            // Make it a trigger
            Collider col = gate.GetComponent<Collider>();
            col.isTrigger = true;
            
            // Color code by multiplier
            Renderer renderer = gate.GetComponent<Renderer>();
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = GetMultiplierColor(multiplier);
            renderer.material = mat;
            
            // Validate prefab BEFORE adding component
            if (RiceBallPrefab == null)
            {
                Debug.LogError($"[GridPuzzleLoader] âŒ No RiceBallPrefab assigned in GridPuzzleLoader Inspector! Multipliers won't work.");
            }
            
            // Add multiplier component
            MultiplierGate multComponent = gate.AddComponent<MultiplierGate>();
            multComponent.Multiplier = multiplier;
            multComponent.RiceBallPrefab = RiceBallPrefab; // Assign the prefab!
            
            gate.name = $"Gate_x{multiplier}";
            return gate;
        }
        
        private GameObject BuildGoalCell(Vector3 position)
        {
            Debug.Log($"[GridPuzzleLoader] 🎯 Building GOAL at position {position} (should be evenly spaced)");
            
            GameObject goal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            goal.transform.position = position;
            goal.transform.localScale = new Vector3(layout.cellSize * 0.6f, CellHeight * 0.4f, 0.5f);
            
            Debug.Log($"[GridPuzzleLoader] 🎯 GOAL final position: {goal.transform.position}, scale: {goal.transform.localScale}");
            
            // Make it a trigger
            Collider col = goal.GetComponent<Collider>();
            col.isTrigger = true;
            
            // Green goal color with proper shader
            Renderer renderer = goal.GetComponent<Renderer>();
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (mat.shader == null)
            {
                // Fallback to Standard shader if URP not available
                mat = new Material(Shader.Find("Standard"));
            }
            mat.color = Color.green;
            mat.SetFloat("_Metallic", 0.0f);
            mat.SetFloat("_Smoothness", 0.5f);
            renderer.material = mat;
            
            // Add goal gate component (only to first goal cell)
            if (FindObjectOfType<GoalGate>() == null)
            {
                GoalGate goalComponent = goal.AddComponent<GoalGate>();
                goal.name = "GoalGate";
                Debug.Log($"[GridPuzzleLoader] Added GoalGate component to first goal");
            }
            else
            {
                goal.name = "GoalCell";
            }
            
            return goal;
        }
        
        private Color GetMultiplierColor(int multiplier)
        {
            switch (multiplier)
            {
                case 2: return new Color(1f, 0.65f, 0f); // Orange
                case 3: return Color.yellow;
                case 4: return Color.green;
                case 5: return Color.cyan;
                case 6: return Color.blue;
                case 7: return new Color(0.5f, 0f, 1f); // Purple
                default: return Color.magenta;
            }
        }
        
                
        /// <summary>
        /// Enhanced gate building with dynamic chance rolling
        /// For "gate:" entries, roll against player's gate chances
        /// For "gate:x2" etc, use specified multiplier
        /// </summary>
        private GameObject BuildGateWithChances(Vector3 position, string gateType)
        {
            if (gateType.StartsWith("gate:"))
            {
                string multiplierStr = gateType.Substring(5);
                
                if (string.IsNullOrEmpty(multiplierStr))
                {
                    // Dynamic gate - roll against player chances
                    int rolledMultiplier = RollGateMultiplier();
                    if (rolledMultiplier == 0)
                    {
                        // No gate spawned
                        Debug.Log($"[GridPuzzleLoader] Gate chance roll failed at {position} - no gate placed");
                        return null;
                    }
                    
                    Debug.Log($"[GridPuzzleLoader] Dynamic gate roll: x{rolledMultiplier} at {position}");
                    return BuildMultiplierGate(position, rolledMultiplier);
                }
                else
                {
                    // Fixed multiplier specified
                    int multiplier = ParseMultiplier(multiplierStr);
                    Debug.Log($"[GridPuzzleLoader] Fixed gate: x{multiplier} at {position}");
                    return BuildMultiplierGate(position, multiplier);
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// Roll against player's gate chances to determine gate multiplier
        /// Returns 0 if no gate should be placed, 2-4+ for multiplier gates
        /// </summary>
        private int RollGateMultiplier()
        {
            var playerData = PlayerDataManager.Instance;
            if (playerData == null)
            {
                Debug.LogWarning("[GridPuzzleLoader] No PlayerDataManager found - no gates will spawn");
                return 0;
            }
            
            var gateChances = playerData.DropPuzzle;
            
            // Roll in order of rarity (highest multiplier first)
            float roll = Random.Range(0f, 1f);
            
            // x4 Gates (rarest)
            if (roll < gateChances.x4GateChance)
            {
                Debug.Log($"[GridPuzzleLoader] 🎯 x4 Gate rolled! ({gateChances.x4GateChance:P1} chance)");
                return 4;
            }
            
            // x3 Gates
            if (roll < gateChances.x3GateChance)
            {
                Debug.Log($"[GridPuzzleLoader] 🎯 x3 Gate rolled! ({gateChances.x3GateChance:P1} chance)");
                return 3;
            }
            
            // x2 Gates (most common)
            if (roll < gateChances.x2GateChance)
            {
                Debug.Log($"[GridPuzzleLoader] 🎯 x2 Gate rolled! ({gateChances.x2GateChance:P1} chance)");
                return 2;
            }
            
            // No gate
            Debug.Log($"[GridPuzzleLoader] No gate rolled (chances: x2:{gateChances.x2GateChance:P1} x3:{gateChances.x3GateChance:P1} x4:{gateChances.x4GateChance:P1})");
            return 0;
        }
        private int ParseMultiplier(string str)
        {
            str = str.Replace("x", "").Trim();
            if (int.TryParse(str, out int result))
            {
                return result;
            }
            return 2; // Default
        }
        
        public void ClearPuzzle()
        {
            foreach (var obj in spawnedObjects)
            {
                if (obj != null)
                {
                    Destroy(obj);
                }
            }
            spawnedObjects.Clear();
            backgroundObject = null;
        }
        
        private void OnValidate()
        {
            // Reload when JSON file changes in editor - BUT NOT during Play mode!
            // Continuously destroying/rebuilding during play causes cached reference issues
            if (!Application.isPlaying && PuzzleJsonFile != null)
            {
                // Only safe to reload in Edit mode
                Debug.Log("[GridPuzzleLoader] JSON changed - reload in Play mode to see changes");
            }
        }
    }
}



