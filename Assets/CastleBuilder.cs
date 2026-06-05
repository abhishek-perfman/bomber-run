using UnityEngine;
using System.Collections.Generic;

public class CastleBuilder : MonoBehaviour
{
    [Header("Brick Blueprint")]
    public GameObject brickPrefab;
    public float brickSpacing = 1.1f;

    [Header("Compound Dimensions")]
    public int compoundWidth = 12;  // Size along X
    public int compoundDepth = 12;  // Size along Z
    public int wallHeight = 4;
    public int towerHeight = 7;

    // Track spawned bricks to calculate demolition percentage
    [HideInInspector]
    public List<GameObject> spawnedBricks = new List<GameObject>();
    private List<Vector3> initialPositions = new List<Vector3>();

    void Start()
    {
        BuildCompound();
        BuildEnvironment();
    }

    void BuildCompound()
    {
        float halfW = (compoundWidth * brickSpacing) / 2f;
        float halfD = (compoundDepth * brickSpacing) / 2f;

        // 1. Build Watchtowers at the 4 corners (size 3x3 base)
        int towerSize = 3;
        BuildTower(new Vector3(-halfW, 0, -halfD), towerSize, towerHeight);
        BuildTower(new Vector3(halfW, 0, -halfD), towerSize, towerHeight);
        BuildTower(new Vector3(-halfW, 0, halfD), towerSize, towerHeight);
        BuildTower(new Vector3(halfW, 0, halfD), towerSize, towerHeight);

        // 2. Build Perimeter Walls connecting the towers
        // North Wall
        BuildWall(new Vector3(-halfW + (towerSize * brickSpacing / 2f) + brickSpacing, 0, halfD),
                  new Vector3(halfW - (towerSize * brickSpacing / 2f) - brickSpacing, 0, halfD),
                  wallHeight, true);
        // South Wall
        BuildWall(new Vector3(-halfW + (towerSize * brickSpacing / 2f) + brickSpacing, 0, -halfD),
                  new Vector3(halfW - (towerSize * brickSpacing / 2f) - brickSpacing, 0, -halfD),
                  wallHeight, true);
        // East Wall
        BuildWall(new Vector3(halfW, 0, -halfD + (towerSize * brickSpacing / 2f) + brickSpacing),
                  new Vector3(halfW, 0, halfD - (towerSize * brickSpacing / 2f) - brickSpacing),
                  wallHeight, false);
        // West Wall
        BuildWall(new Vector3(-halfW, 0, -halfD + (towerSize * brickSpacing / 2f) + brickSpacing),
                  new Vector3(-halfW, 0, halfD - (towerSize * brickSpacing / 2f) - brickSpacing),
                  wallHeight, false);

        // 3. Build a Central Fortress / Castle Keep in the middle! (5x5 base)
        BuildKeep(Vector3.zero, 5, 5);
    }

    void BuildEnvironment()
    {
        // 1. Scale and color the ground Plane
        GameObject ground = GameObject.Find("Plane");
        if (ground != null)
        {
            ground.transform.localScale = new Vector3(16f, 1f, 16f); // 160x160 units
            // Set grass green material
            ground.GetComponent<Renderer>().material = ShaderHelper.CreateSafeMaterial(new Color(0.12f, 0.38f, 0.12f), 0.05f);
        }

        // 2. Spawn a River running North-South at X = -22f
        GameObject river = GameObject.CreatePrimitive(PrimitiveType.Cube);
        river.name = "River";
        river.transform.position = new Vector3(-22f, 0.05f, 0f);
        river.transform.localScale = new Vector3(6f, 0.1f, 160f);
        Destroy(river.GetComponent<Collider>()); // No collision to prevent lag
        // Semi-transparent blue reflective water
        river.GetComponent<Renderer>().material = ShaderHelper.CreateSafeMaterial(new Color(0.08f, 0.45f, 0.85f, 0.7f), 0.9f);

        // 3. Spawn a Stone Bridge over the River at Z = 0
        GameObject bridgeGroup = new GameObject("StoneBridge");
        bridgeGroup.transform.position = new Vector3(-22f, 0f, 0f);

        Material stoneMat = ShaderHelper.CreateSafeMaterial(new Color(0.4f, 0.42f, 0.45f), 0.1f);

        // Bridge Roadway
        GameObject roadway = GameObject.CreatePrimitive(PrimitiveType.Cube);
        roadway.transform.parent = bridgeGroup.transform;
        roadway.transform.localPosition = new Vector3(0f, 0.25f, 0f);
        roadway.transform.localScale = new Vector3(7f, 0.4f, 4f);
        roadway.GetComponent<Renderer>().material = stoneMat;

        // Side Rails
        GameObject railNorth = GameObject.CreatePrimitive(PrimitiveType.Cube);
        railNorth.transform.parent = bridgeGroup.transform;
        railNorth.transform.localPosition = new Vector3(0f, 0.75f, 1.85f);
        railNorth.transform.localScale = new Vector3(7f, 0.8f, 0.3f);
        railNorth.GetComponent<Renderer>().material = stoneMat;

        GameObject railSouth = GameObject.CreatePrimitive(PrimitiveType.Cube);
        railSouth.transform.parent = bridgeGroup.transform;
        railSouth.transform.localPosition = new Vector3(0f, 0.75f, -1.85f);
        railSouth.transform.localScale = new Vector3(7f, 0.8f, 0.3f);
        railSouth.GetComponent<Renderer>().material = stoneMat;

        // 4. Spawn Forests of Trees
        GameObject treesGroup = new GameObject("ProceduralForest");
        int treeCount = 35;
        for (int i = 0; i < treeCount; i++)
        {
            // Pick a random spot within the 150x150 boundary (range -70 to 70)
            float rx = Random.Range(-70f, 70f);
            float rz = Random.Range(-70f, 70f);

            // Avoid spawning inside the compound (radius 16 units)
            float distFromCenter = Mathf.Sqrt(rx * rx + rz * rz);
            if (distFromCenter < 16f) continue;

            // Avoid spawning inside the river (X between -26 and -18)
            if (rx >= -26f && rx <= -18f) continue;

            SpawnTree(treesGroup.transform, new Vector3(rx, 0f, rz));
        }

        // 5. Spawn Clouds in the Sky
        GameObject cloudsGroup = new GameObject("ProceduralClouds");
        int cloudCount = 8;
        for (int i = 0; i < cloudCount; i++)
        {
            float rx = Random.Range(-70f, 70f);
            float rz = Random.Range(-70f, 70f);
            float ry = Random.Range(20f, 26f); // Altitudes between 20 and 26 units

            SpawnCloud(cloudsGroup.transform, new Vector3(rx, ry, rz));
        }
    }

    void SpawnTree(Transform parent, Vector3 position)
    {
        GameObject tree = new GameObject("LowPolyTree");
        tree.transform.parent = parent;
        tree.transform.position = position;

        Material trunkMat = ShaderHelper.CreateSafeMaterial(new Color(0.42f, 0.26f, 0.12f), 0.05f);
        Material foliageMat = ShaderHelper.CreateSafeMaterial(new Color(0.12f, 0.52f, 0.16f), 0.1f);

        // A. Trunk
        GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trunk.transform.parent = tree.transform;
        trunk.transform.localPosition = new Vector3(0f, 1.25f, 0f);
        trunk.transform.localScale = new Vector3(0.4f, 2.5f, 0.4f);
        Destroy(trunk.GetComponent<Collider>());
        trunk.GetComponent<Renderer>().material = trunkMat;

        // B. Foliage Spheres (stacked)
        // Bottom sphere
        GameObject foliage1 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        foliage1.transform.parent = tree.transform;
        foliage1.transform.localPosition = new Vector3(0f, 2.5f, 0f);
        foliage1.transform.localScale = new Vector3(1.6f, 1.6f, 1.6f);
        Destroy(foliage1.GetComponent<Collider>());
        foliage1.GetComponent<Renderer>().material = foliageMat;

        // Middle sphere
        GameObject foliage2 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        foliage2.transform.parent = tree.transform;
        foliage2.transform.localPosition = new Vector3(0f, 3.3f, 0f);
        foliage2.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);
        Destroy(foliage2.GetComponent<Collider>());
        foliage2.GetComponent<Renderer>().material = foliageMat;

        // Top sphere
        GameObject foliage3 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        foliage3.transform.parent = tree.transform;
        foliage3.transform.localPosition = new Vector3(0f, 4.0f, 0f);
        foliage3.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);
        Destroy(foliage3.GetComponent<Collider>());
        foliage3.GetComponent<Renderer>().material = foliageMat;
    }

    void SpawnCloud(Transform parent, Vector3 position)
    {
        GameObject cloud = new GameObject("LowPolyCloud");
        cloud.transform.parent = parent;
        cloud.transform.position = position;

        Material cloudMat = ShaderHelper.CreateSafeMaterial(new Color(1f, 1f, 1f, 0.8f), 0.1f);

        // Center sphere
        GameObject center = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        center.transform.parent = cloud.transform;
        center.transform.localPosition = Vector3.zero;
        center.transform.localScale = new Vector3(3f, 1.5f, 2.5f);
        Destroy(center.GetComponent<Collider>());
        center.GetComponent<Renderer>().material = cloudMat;

        // Left sphere
        GameObject left = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        left.transform.parent = cloud.transform;
        left.transform.localPosition = new Vector3(-1.5f, -0.2f, 0f);
        left.transform.localScale = new Vector3(2f, 1f, 1.8f);
        Destroy(left.GetComponent<Collider>());
        left.GetComponent<Renderer>().material = cloudMat;

        // Right sphere
        GameObject right = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        right.transform.parent = cloud.transform;
        right.transform.localPosition = new Vector3(1.5f, -0.2f, 0f);
        right.transform.localScale = new Vector3(2f, 1f, 1.8f);
        Destroy(right.GetComponent<Collider>());
        right.GetComponent<Renderer>().material = cloudMat;

        // Front sphere
        GameObject front = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        front.transform.parent = cloud.transform;
        front.transform.localPosition = new Vector3(0f, -0.1f, 1.2f);
        front.transform.localScale = new Vector3(1.8f, 0.9f, 1.5f);
        Destroy(front.GetComponent<Collider>());
        front.GetComponent<Renderer>().material = cloudMat;

        // Back sphere
        GameObject back = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        back.transform.parent = cloud.transform;
        back.transform.localPosition = new Vector3(0f, -0.1f, -1.2f);
        back.transform.localScale = new Vector3(1.8f, 0.9f, 1.5f);
        Destroy(back.GetComponent<Collider>());
        back.GetComponent<Renderer>().material = cloudMat;

        // Add the cloud drift physics-less script
        cloud.AddComponent<FloatingCloud>();
    }

    void BuildTower(Vector3 centerOffset, int size, int height)
    {
        float startOffset = -(size - 1) / 2f * brickSpacing;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < size; x++)
            {
                for (int z = 0; z < size; z++)
                {
                    // Hollow out the tower inside (only spawn outer walls of the tower)
                    if (y > 0 && x > 0 && x < size - 1 && z > 0 && z < size - 1)
                        continue;

                    // Leave battlements on top (alternate bricks on the top row)
                    if (y == height - 1 && (x + z) % 2 == 0)
                        continue;

                    Vector3 localPos = new Vector3(startOffset + x * brickSpacing, y * brickSpacing, startOffset + z * brickSpacing);
                    SpawnBrick(transform.position + centerOffset + localPos);
                }
            }
        }

        // Spawn an Anti-Aircraft Gun at the top center of this watchtower
        Vector3 topCenter = transform.position + centerOffset + new Vector3(0f, height * brickSpacing, 0f);
        GameObject aaGun = new GameObject("AA_Gun_Watchtower");
        aaGun.transform.position = topCenter;
        aaGun.transform.parent = this.transform;
        aaGun.AddComponent<AntiAircraftGun>();
    }

    void BuildWall(Vector3 start, Vector3 end, int height, bool isHorizontal)
    {
        Vector3 dir = (end - start).normalized;
        float distance = Vector3.Distance(start, end);
        int steps = Mathf.RoundToInt(distance / brickSpacing);

        for (int i = 0; i <= steps; i++)
        {
            Vector3 basePos = start + dir * (i * brickSpacing);
            for (int y = 0; y < height; y++)
            {
                // Alternating rows slightly offset to look like running bond brickwork!
                float offset = 0f;
                if (y % 2 == 1 && i == steps) continue; // Skip last brick on odd rows to fit bond
                if (y % 2 == 1) offset = (brickSpacing / 2f);

                Vector3 localOffset = isHorizontal ? new Vector3(offset, 0, 0) : new Vector3(0, 0, offset);
                Vector3 spawnPos = transform.position + basePos + new Vector3(0, y * brickSpacing, 0) + localOffset;
                SpawnBrick(spawnPos);
            }
        }
    }

    void BuildKeep(Vector3 centerOffset, int size, int height)
    {
        float startOffset = -(size - 1) / 2f * brickSpacing;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < size; x++)
            {
                for (int z = 0; z < size; z++)
                {
                    // Leave an arched doorway at the front (z = 0, x = middle)
                    if (y < 2 && z == 0 && x == size / 2)
                        continue;

                    // Hollow out interior
                    if (x > 0 && x < size - 1 && z > 0 && z < size - 1)
                        continue;

                    Vector3 localPos = new Vector3(startOffset + x * brickSpacing, y * brickSpacing, startOffset + z * brickSpacing);
                    SpawnBrick(transform.position + centerOffset + localPos);
                }
            }
        }

        // Add a flat roof
        for (int x = 0; x < size; x++)
        {
            for (int z = 0; z < size; z++)
            {
                Vector3 localPos = new Vector3(startOffset + x * brickSpacing, height * brickSpacing, startOffset + z * brickSpacing);
                SpawnBrick(transform.position + centerOffset + localPos);
            }
        }
    }

    void SpawnBrick(Vector3 pos)
    {
        GameObject brick = Instantiate(brickPrefab, pos, Quaternion.identity);
        brick.transform.SetParent(this.transform);
        spawnedBricks.Add(brick);
        initialPositions.Add(brick.transform.position);
    }

    public float GetDestructionPercentage()
    {
        if (spawnedBricks.Count == 0) return 0f;

        int movedCount = 0;
        for (int i = 0; i < spawnedBricks.Count; i++)
        {
            if (spawnedBricks[i] == null)
            {
                movedCount++;
                continue;
            }

            // A brick is considered destroyed if it is moved > 1.8 units, or falls/flies away
            float dist = Vector3.Distance(spawnedBricks[i].transform.position, initialPositions[i]);
            if (dist > 1.8f || spawnedBricks[i].transform.position.y < -1f)
            {
                movedCount++;
            }
        }

        return ((float)movedCount / spawnedBricks.Count) * 100f;
    }
}

public class FloatingCloud : MonoBehaviour
{
    public float speed = 2.5f;
    private Vector3 moveDir;

    void Start()
    {
        // Drift forward or slightly angled
        moveDir = new Vector3(Random.Range(0.8f, 1.0f), 0f, Random.Range(-0.2f, 0.2f)).normalized;
        speed = Random.Range(1.8f, 3.5f);
    }

    void Update()
    {
        transform.Translate(moveDir * speed * Time.deltaTime, Space.World);

        // Loop back if they drift beyond 80 units
        if (transform.position.x > 80f)
        {
            transform.position = new Vector3(-80f, transform.position.y, Random.Range(-60f, 60f));
        }
    }
}

