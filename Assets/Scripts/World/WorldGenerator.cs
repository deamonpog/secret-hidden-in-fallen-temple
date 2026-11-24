using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Builds a level from a seed and keeps its squirrels topped up. It places a border ring,
/// the spawn point, the Secret and the shrine chains, then scenic spots, trees, stones,
/// bushes, flower plants and fruit on jittered grids, bakes the NavMesh, checks that every
/// shrine can be reached from the spawn point, and then places monks, pilgrims, squirrel
/// groups, lions and the player. Each part is created under a switched-off root and
/// switched on at once, so every object is fully set up before its Awake runs. The same
/// seed always gives the same layout. When a shrine finds no free spot or cannot be
/// reached, it clears the level and tries the next sub-seed. During play, every squirrel
/// that is lost is replaced out of the player's sight.
/// </summary>
public class WorldGenerator : MonoBehaviour
{
    // A place already taken, with the free distance it keeps around itself.
    private struct Spot
    {
        public Vector3 position;
        public float radius;

        public Spot(Vector3 position, float radius)
        {
            this.position = position;
            this.radius = radius;
        }
    }

    // Grid cells made per object, so taken or blocked cells can be skipped.
    private const float SpareCells = 1.5f;

    // Random points tried for the Secret, for a free spot near a target, or for a respawn.
    private const int SearchTries = 30;

    // How far from a point the NavMesh is searched when something is placed on it.
    private const float NavSampleDistance = 3f;

    // Height of the invisible walls around the map.
    private const float WallHeight = 5f;

    [Header("Scene")]
    [SerializeField] private Player player;
    [SerializeField] private GameManager gameManager;
    // The NavMeshSurface on the floor. It is rebuilt after the level is placed.
    [SerializeField] private NavMeshSurface surface;
    // When not zero, this seed is used instead of the run's seed, to replay one layout.
    [SerializeField] private int debugSeed;

    [Header("Map")]
    // Side length of the square play area, centered on this object. This object should sit
    // at the center of the floor's top surface.
    [SerializeField] private float mapSize = 100f;
    // Width of the band along each edge that holds the border ring.
    [SerializeField] private float borderWidth = 5f;
    // Distance between border objects along an edge.
    [SerializeField] private float borderSpacing = 3f;
    // Invisible walls just outside the ring, so the player cannot slip through a gap.
    [SerializeField] private bool addWalls = true;
    // Layouts tried before the last one is used even when it has a problem.
    [SerializeField] private int maxAttempts = 10;
    // How far a grid point may move from its cell centre, as a share of the cell size.
    [SerializeField, Range(0f, 1f)] private float gridJitter = 0.8f;

    [Header("Shrines")]
    [SerializeField] private Shrine normalShrinePrefab;
    [SerializeField] private Shrine secretShrinePrefab;
    [SerializeField] private int chains = 3;
    [SerializeField] private int shrinesPerChain = 3;
    // Distance between the tiers of a chain, so a chain reaches about
    // shrinesPerChain times this from the Secret.
    [SerializeField] private float tierSpacing = 12f;
    // Distance from the spawn point the Secret tries to keep.
    [SerializeField] private float minSecretDistance = 40f;
    // The Secret stays at least this far inside the border band, so its chains have room.
    [SerializeField] private float secretEdgeMargin = 15f;

    [Header("Level objects")]
    [SerializeField] private GameObject[] borderPrefabs;
    [SerializeField] private GameObject[] treePrefabs;
    [SerializeField] private GameObject[] stonePrefabs;
    [SerializeField] private GameObject[] bushPrefabs;
    [SerializeField] private GameObject flowerPlantPrefab;
    [SerializeField] private GameObject fruitPrefab;
    [SerializeField] private int trees = 70;
    [SerializeField] private int stones = 35;
    [SerializeField] private int bushes = 40;
    [SerializeField] private int flowerPlants = 30;
    // Loose fruit on top of one for each pilgrim and monk.
    [SerializeField] private int extraFruit = 10;
    // Empty places pilgrims visit to rest and look around. Nothing else is placed on them.
    [SerializeField] private int scenicSpots = 8;

    [Header("Characters")]
    [SerializeField] private Npc pilgrimPrefab;
    [SerializeField] private Npc monkPrefab;
    [SerializeField] private Squirrel squirrelPrefab;
    [SerializeField] private Lion lionPrefab;
    [SerializeField] private int pilgrims = 6;
    [SerializeField] private int monks = 2;
    [SerializeField] private int squirrels = 14;
    [SerializeField] private int lions = 4;
    [SerializeField] private int minGroupSize = 3;
    [SerializeField] private int maxGroupSize = 5;
    // Squirrels start within this distance of their group's centre.
    [SerializeField] private float groupSpread = 2f;
    // No lion starts closer to the spawn point than this.
    [SerializeField] private float lionSafeDistance = 25f;

    [Header("Squirrel respawn")]
    // Seconds after a squirrel is lost before a new one appears.
    [SerializeField] private float respawnSeconds = 20f;
    // A new squirrel appears at least this far beyond the player's vision radius.
    [SerializeField] private float respawnMargin = 5f;

    [Header("Start")]
    [SerializeField] private int startingClues = 2;
    // Nothing else is placed this close to the spawn point.
    [SerializeField] private float spawnClearRadius = 4f;

    [Header("Free distance around each kind")]
    [SerializeField] private float shrineSpacing = 4f;
    [SerializeField] private float treeSpacing = 1.5f;
    [SerializeField] private float stoneSpacing = 1.5f;
    [SerializeField] private float bushSpacing = 1f;
    [SerializeField] private float smallSpacing = 0.5f;
    [SerializeField] private float characterSpacing = 2f;

    private readonly List<Spot> taken = new List<Spot>();
    private readonly List<Shrine> placedShrines = new List<Shrine>();
    private readonly List<float> respawnTimes = new List<float>();

    private System.Random rng;
    private Transform levelRoot;
    private Transform creatureRoot;
    private Transform stagingRoot;
    private Vector3 spawn;
    private Vector3 secretPosition;

    private void Awake()
    {
        // Respawned squirrels are made here first, switched off, so their Awake waits
        // until the player and the group are set.
        stagingRoot = NewRoot("Staging");
    }

    private void OnEnable()
    {
        Squirrel.Lost += OnSquirrelLost;
    }

    private void OnDisable()
    {
        Squirrel.Lost -= OnSquirrelLost;
    }

    private void Update()
    {
        for (int i = respawnTimes.Count - 1; i >= 0; i--)
        {
            if (Time.time < respawnTimes[i])
                continue;

            respawnTimes.RemoveAt(i);
            RespawnSquirrel();
        }
    }

    // Builds the level for the seed. GameManager calls this once at the start of a run.
    public void Generate(int seed)
    {
        if (!ReferencesReady())
            return;

        if (debugSeed != 0)
            seed = debugSeed;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            rng = new System.Random(seed + attempt);
            ClearLevel();

            if (!BuildLevel())
            {
                Debug.Log("WorldGenerator: a shrine found no free spot on attempt " +
                          (attempt + 1) + ", trying the next sub-seed.", this);
                continue;
            }

            surface.BuildNavMesh();
            if (ShrinesReachable())
            {
                PlaceCreatures();
                PlacePlayer();
                return;
            }

            Debug.Log("WorldGenerator: a shrine could not be reached on attempt " +
                      (attempt + 1) + ", trying the next sub-seed.", this);
        }

        Debug.LogError("WorldGenerator found no layout without a problem. " +
                       "The last one is used.", this);
        levelRoot.gameObject.SetActive(true);
        surface.BuildNavMesh();
        PlaceCreatures();
        PlacePlayer();
    }

    // Keeps the tuning values in a sensible range when they are edited in the inspector.
    private void OnValidate()
    {
        mapSize = Mathf.Max(20f, mapSize);
        borderWidth = Mathf.Max(0.5f, borderWidth);
        borderSpacing = Mathf.Max(0.5f, borderSpacing);
        maxAttempts = Mathf.Max(1, maxAttempts);
        chains = Mathf.Max(1, chains);
        shrinesPerChain = Mathf.Max(1, shrinesPerChain);
        tierSpacing = Mathf.Max(1f, tierSpacing);
        minSecretDistance = Mathf.Max(0f, minSecretDistance);
        secretEdgeMargin = Mathf.Max(0f, secretEdgeMargin);
        trees = Mathf.Max(0, trees);
        stones = Mathf.Max(0, stones);
        bushes = Mathf.Max(0, bushes);
        flowerPlants = Mathf.Max(0, flowerPlants);
        extraFruit = Mathf.Max(0, extraFruit);
        scenicSpots = Mathf.Max(0, scenicSpots);
        pilgrims = Mathf.Max(0, pilgrims);
        monks = Mathf.Max(0, monks);
        squirrels = Mathf.Max(0, squirrels);
        lions = Mathf.Max(0, lions);
        minGroupSize = Mathf.Max(1, minGroupSize);
        maxGroupSize = Mathf.Max(minGroupSize, maxGroupSize);
        groupSpread = Mathf.Max(0f, groupSpread);
        lionSafeDistance = Mathf.Max(0f, lionSafeDistance);
        respawnSeconds = Mathf.Max(0f, respawnSeconds);
        respawnMargin = Mathf.Max(0f, respawnMargin);
        startingClues = Mathf.Max(0, startingClues);
        spawnClearRadius = Mathf.Max(0f, spawnClearRadius);
        shrineSpacing = Mathf.Max(0.1f, shrineSpacing);
        treeSpacing = Mathf.Max(0.1f, treeSpacing);
        stoneSpacing = Mathf.Max(0.1f, stoneSpacing);
        bushSpacing = Mathf.Max(0.1f, bushSpacing);
        smallSpacing = Mathf.Max(0.1f, smallSpacing);
        characterSpacing = Mathf.Max(0.1f, characterSpacing);
    }

    private bool ReferencesReady()
    {
        if (player != null && gameManager != null && surface != null
            && normalShrinePrefab != null && secretShrinePrefab != null)
            return true;

        Debug.LogError("WorldGenerator needs the player, the GameManager, the NavMeshSurface " +
                       "and both shrine prefabs.", this);
        return false;
    }

    // Removes the previous attempt and makes two new switched-off roots. An old root is
    // switched off before it is destroyed, because Destroy waits until the end of the frame
    // and the next NavMesh bake in this frame must not see its colliders.
    private void ClearLevel()
    {
        RemoveRoot(levelRoot);
        RemoveRoot(creatureRoot);
        taken.Clear();
        placedShrines.Clear();
        levelRoot = NewRoot("Level");
        creatureRoot = NewRoot("Creatures");
    }

    private static void RemoveRoot(Transform root)
    {
        if (root == null)
            return;

        root.gameObject.SetActive(false);
        Destroy(root.gameObject);
    }

    private Transform NewRoot(string rootName)
    {
        GameObject root = new GameObject(rootName);
        root.SetActive(false);
        root.transform.SetParent(transform, false);
        return root.transform;
    }

    // Places everything that never moves, then switches it on. Returns false, and leaves
    // the level switched off, when a shrine found no free spot.
    private bool BuildLevel()
    {
        BuildBorder();
        PlaceSpawn();
        if (!PlaceShrines())
            return false;

        PlaceScenicSpots();
        Scatter(treePrefabs, trees, treeSpacing);
        Scatter(stonePrefabs, stones, stoneSpacing);
        Scatter(bushPrefabs, bushes, bushSpacing);
        Scatter(flowerPlantPrefab, flowerPlants, smallSpacing);
        Scatter(fruitPrefab, pilgrims + monks + extraFruit, smallSpacing);
        levelRoot.gameObject.SetActive(true);
        return true;
    }

    // Lines the four edges with border objects at random depths inside the border band.
    private void BuildBorder()
    {
        float half = mapSize * 0.5f;

        if (borderPrefabs != null && borderPrefabs.Length > 0)
        {
            int perSide = Mathf.Max(1, Mathf.RoundToInt(mapSize / borderSpacing));
            float step = mapSize / perSide;

            for (int side = 0; side < 4; side++)
            {
                for (int i = 0; i < perSide; i++)
                {
                    float along = -half + (i + 0.5f) * step;
                    float depth = RandomRange(half - borderWidth, half - 0.5f);
                    Vector3 offset = side switch
                    {
                        0 => new Vector3(along, 0f, depth),
                        1 => new Vector3(along, 0f, -depth),
                        2 => new Vector3(depth, 0f, along),
                        _ => new Vector3(-depth, 0f, along)
                    };
                    Spawn(Pick(borderPrefabs), transform.position + offset, levelRoot);
                }
            }
        }

        if (addWalls)
            BuildWalls(half);
    }

    // Four invisible box colliders just outside the map edges.
    private void BuildWalls(float half)
    {
        GameObject walls = new GameObject("Walls");
        walls.transform.SetParent(levelRoot, false);

        float y = WallHeight * 0.5f;
        float length = mapSize + 2f;
        AddWall(walls, new Vector3(0f, y, half + 0.5f), new Vector3(length, WallHeight, 1f));
        AddWall(walls, new Vector3(0f, y, -half - 0.5f), new Vector3(length, WallHeight, 1f));
        AddWall(walls, new Vector3(half + 0.5f, y, 0f), new Vector3(1f, WallHeight, length));
        AddWall(walls, new Vector3(-half - 0.5f, y, 0f), new Vector3(1f, WallHeight, length));
    }

    private static void AddWall(GameObject owner, Vector3 center, Vector3 size)
    {
        BoxCollider box = owner.AddComponent<BoxCollider>();
        box.center = center;
        box.size = size;
    }

    private void PlaceSpawn()
    {
        spawn = RandomInnerPoint(spawnClearRadius);
        Reserve(spawn, spawnClearRadius);
    }

    // Places the Secret away from the edges and far from the spawn point, then one chain
    // per sector around it. A chain's outermost shrine lies in the middle part of its own
    // sector, and the shrines after it lie along the line toward the Secret, so the chains
    // spread over the map and all lead inward. Returns false when a shrine finds no free spot.
    private bool PlaceShrines()
    {
        secretPosition = FindSecretSpot();
        Reserve(secretPosition, shrineSpacing);
        Shrine secret = SpawnShrine(secretShrinePrefab, secretPosition);
        secret.Configure(gameManager, 0, null, false);

        float sector = 360f / chains;
        float baseAngle = RandomRange(0f, 360f);
        float reach = shrinesPerChain * tierSpacing;

        for (int c = 0; c < chains; c++)
        {
            float angle = baseAngle + (c + RandomRange(0.2f, 0.8f)) * sector;
            Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            Vector3 outer = ClampInside(secretPosition + direction * RandomRange(reach * 0.85f, reach * 1.1f),
                                        shrineSpacing);

            Vector3 along = outer - secretPosition;
            along.y = 0f;
            Vector3 side = new Vector3(-along.z, 0f, along.x).normalized;

            Shrine next = secret;
            for (int tier = 1; tier <= shrinesPerChain; tier++)
            {
                float share = (float)tier / shrinesPerChain;
                Vector3 target = Vector3.Lerp(secretPosition, outer, share) + side * RandomRange(-3f, 3f);
                target = ClampInside(target, shrineSpacing);

                if (!FindFreeNear(target, shrineSpacing, out Vector3 position))
                    return false;

                Reserve(position, shrineSpacing);
                Shrine shrine = SpawnShrine(normalShrinePrefab, position);
                shrine.Configure(gameManager, tier, next, tier == shrinesPerChain);
                next = shrine;
            }
        }

        return true;
    }

    // The farthest of several random points from the spawn, kept secretEdgeMargin inside the
    // border band, stopping early at one that is at least minSecretDistance away.
    private Vector3 FindSecretSpot()
    {
        float reach = Mathf.Max(0f, mapSize * 0.5f - borderWidth - secretEdgeMargin);
        Vector3 best = transform.position;
        float bestDistance = -1f;

        for (int i = 0; i < SearchTries; i++)
        {
            Vector3 candidate = transform.position +
                                new Vector3(RandomRange(-reach, reach), 0f, RandomRange(-reach, reach));
            if (!IsFree(candidate, shrineSpacing))
                continue;

            float distance = FlatDistance(candidate, spawn);
            if (distance > bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }

            if (distance >= minSecretDistance)
                break;
        }

        return best;
    }

    private Shrine SpawnShrine(Shrine prefab, Vector3 position)
    {
        Shrine shrine = Instantiate(prefab, position, RandomYaw(), levelRoot);
        placedShrines.Add(shrine);
        return shrine;
    }

    // Marks empty spots that pilgrims walk to and rest at. They are kept clear like a shrine.
    private void PlaceScenicSpots()
    {
        List<Vector3> spots = GridSpots(scenicSpots, shrineSpacing);
        for (int i = 0; i < scenicSpots; i++)
        {
            if (!TakeSpot(spots, shrineSpacing, 0f, false, out Vector3 position))
                return;

            GameObject spot = new GameObject("ScenicSpot");
            spot.transform.SetParent(levelRoot, false);
            spot.transform.position = position;
            spot.AddComponent<InterestPoint>().Configure(false, 2f);
        }
    }

    private void Scatter(GameObject prefab, int count, float radius)
    {
        if (prefab != null)
            Scatter(new[] { prefab }, count, radius);
    }

    // Places count copies of random prefabs from the list on a jittered grid.
    private void Scatter(GameObject[] prefabs, int count, float radius)
    {
        if (prefabs == null || prefabs.Length == 0)
            return;

        List<Vector3> spots = GridSpots(count, radius);
        for (int i = 0; i < count; i++)
        {
            if (!TakeSpot(spots, radius, 0f, false, out Vector3 position))
            {
                Debug.LogWarning("WorldGenerator ran out of room after placing " + i +
                                 " of " + count + " " + prefabs[0].name + ".", this);
                return;
            }

            Spawn(Pick(prefabs), position, levelRoot);
        }
    }

    // True when every shrine has a complete NavMesh path from the spawn point.
    private bool ShrinesReachable()
    {
        if (!NavMesh.SamplePosition(spawn, out NavMeshHit start, NavSampleDistance, NavMesh.AllAreas))
            return false;

        NavMeshPath path = new NavMeshPath();
        for (int i = 0; i < placedShrines.Count; i++)
        {
            if (!NavMesh.SamplePosition(placedShrines[i].transform.position, out NavMeshHit end,
                                        shrineSpacing, NavMesh.AllAreas))
                return false;

            if (!NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path)
                || path.status != NavMeshPathStatus.PathComplete)
                return false;
        }

        return true;
    }

    // Places everything that walks on the NavMesh, after the bake, then switches it on.
    private void PlaceCreatures()
    {
        PlaceNpcs(monkPrefab, monks, true);
        PlaceNpcs(pilgrimPrefab, pilgrims, false);

        if (squirrelPrefab != null)
            SpawnSquirrelGroups();

        if (lionPrefab != null)
        {
            List<Vector3> spots = GridSpots(lions, characterSpacing);
            for (int i = 0; i < lions; i++)
            {
                if (!TakeSpot(spots, characterSpacing, lionSafeDistance, true, out Vector3 position))
                    break;

                Spawn(lionPrefab.gameObject, position, creatureRoot);
            }
        }

        creatureRoot.gameObject.SetActive(true);
    }

    // Places monks or pilgrims on the NavMesh. A monk gives its own bonus clue (D25),
    // a pilgrim a clue point.
    private void PlaceNpcs(Npc prefab, int count, bool isMonk)
    {
        if (prefab == null)
            return;

        List<Vector3> spots = GridSpots(count, characterSpacing);
        for (int i = 0; i < count; i++)
        {
            if (!TakeSpot(spots, characterSpacing, 0f, true, out Vector3 position))
                return;

            ItemType reward = !isMonk ? ItemType.CluePoint
                : i % 2 == 0 ? ItemType.BonusClue1 : ItemType.BonusClue2;
            Npc npc = Instantiate(prefab, position, RandomYaw(), creatureRoot);
            npc.Configure(player, reward);
        }
    }

    // Places the squirrels in groups of minGroupSize to maxGroupSize, each around its own
    // SquirrelGroup centre on the NavMesh.
    private void SpawnSquirrelGroups()
    {
        float groupRadius = characterSpacing + groupSpread;
        List<Vector3> spots = GridSpots(squirrels, groupRadius);
        int placed = 0;

        while (placed < squirrels)
        {
            if (!TakeSpot(spots, groupRadius, 0f, true, out Vector3 center))
                return;

            GameObject groupObject = new GameObject("SquirrelGroup");
            groupObject.transform.SetParent(creatureRoot, false);
            groupObject.transform.position = center;
            SquirrelGroup group = groupObject.AddComponent<SquirrelGroup>();

            int size = Mathf.Min(rng.Next(minGroupSize, maxGroupSize + 1), squirrels - placed);
            for (int i = 0; i < size; i++)
            {
                Vector3 direction = Quaternion.Euler(0f, RandomRange(0f, 360f), 0f) * Vector3.forward;
                Vector3 position = center + direction * RandomRange(0f, groupSpread);
                if (NavMesh.SamplePosition(position, out NavMeshHit hit, NavSampleDistance, NavMesh.AllAreas))
                    position = hit.position;

                GameObject copy = Spawn(squirrelPrefab.gameObject, position, creatureRoot);
                copy.GetComponent<Squirrel>().SetGroup(group);
            }

            placed += size;
        }
    }

    // Moves the player to the spawn point on the NavMesh and gives the starting clue points.
    private void PlacePlayer()
    {
        Vector3 position = spawn;
        if (NavMesh.SamplePosition(spawn, out NavMeshHit hit, NavSampleDistance, NavMesh.AllAreas))
            position = hit.position;

        player.Movement.TeleportTo(position);

        if (startingClues > 0)
            player.Add(ItemType.CluePoint, startingClues);
    }

    private void OnSquirrelLost(Squirrel squirrel)
    {
        respawnTimes.Add(Time.time + respawnSeconds);
    }

    // Adds one squirrel to the group with the fewest living members, at a NavMesh spot the
    // player cannot see. With no hidden spot found, it tries again later.
    private void RespawnSquirrel()
    {
        if (squirrelPrefab == null || creatureRoot == null || rng == null)
            return;

        SquirrelGroup group = SmallestGroup();
        if (group == null)
            return;

        if (!FindHiddenSpot(group.Center, out Vector3 position))
        {
            respawnTimes.Add(Time.time + respawnSeconds);
            return;
        }

        // Made under the switched-off staging root, so Awake runs only after the move below.
        GameObject copy = Instantiate(squirrelPrefab.gameObject, position, RandomYaw(), stagingRoot);
        GivePlayer(copy);
        copy.GetComponent<Squirrel>().SetGroup(group);
        copy.transform.SetParent(creatureRoot, true);
    }

    private static SquirrelGroup SmallestGroup()
    {
        SquirrelGroup smallest = null;
        IReadOnlyList<SquirrelGroup> groups = SquirrelGroup.Active;
        for (int i = 0; i < groups.Count; i++)
        {
            if (smallest == null || groups[i].LivingCount < smallest.LivingCount)
                smallest = groups[i];
        }

        return smallest;
    }

    // A NavMesh spot beyond the player's vision radius plus respawnMargin, as near to the
    // target as possible. The first half of the tries look near the target, the rest
    // anywhere inside the border.
    private bool FindHiddenSpot(Vector3 near, out Vector3 position)
    {
        float hidden = player.Vision.Value + respawnMargin;
        float bestDistance = float.MaxValue;
        position = near;

        for (int i = 0; i < SearchTries; i++)
        {
            Vector3 candidate;
            if (i < SearchTries / 2)
            {
                Vector3 direction = Quaternion.Euler(0f, RandomRange(0f, 360f), 0f) * Vector3.forward;
                candidate = near + direction * RandomRange(0f, groupSpread * 3f);
            }
            else
            {
                candidate = RandomInnerPoint(characterSpacing);
            }

            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, NavSampleDistance, NavMesh.AllAreas))
                continue;

            if (FlatDistance(hit.position, player.transform.position) < hidden)
                continue;

            float distance = FlatDistance(hit.position, near);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                position = hit.position;
            }
        }

        return bestDistance < float.MaxValue;
    }

    // Copies the prefab at the position with a random turn, and gives the copy the player.
    private GameObject Spawn(GameObject prefab, Vector3 position, Transform parent)
    {
        GameObject copy = Instantiate(prefab, position, RandomYaw(), parent);
        GivePlayer(copy);
        return copy;
    }

    private void GivePlayer(GameObject copy)
    {
        foreach (FearSource fear in copy.GetComponentsInChildren<FearSource>(true))
            fear.SetPlayer(player);

        foreach (Animal animal in copy.GetComponentsInChildren<Animal>(true))
            animal.SetPlayer(player);
    }

    // Jittered-grid points for count objects: the inner square is cut into about
    // SpareCells times count square cells, each gives one point offset up to gridJitter
    // of its cell, and the points come back shuffled.
    private List<Vector3> GridSpots(int count, float margin)
    {
        List<Vector3> spots = new List<Vector3>();
        if (count <= 0)
            return spots;

        float reach = mapSize * 0.5f - borderWidth - margin;
        int perSide = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(count * SpareCells)));
        float cell = 2f * reach / perSide;
        float wiggle = cell * gridJitter * 0.5f;

        for (int x = 0; x < perSide; x++)
        {
            for (int z = 0; z < perSide; z++)
            {
                float px = -reach + (x + 0.5f) * cell + RandomRange(-wiggle, wiggle);
                float pz = -reach + (z + 0.5f) * cell + RandomRange(-wiggle, wiggle);
                spots.Add(transform.position + new Vector3(px, 0f, pz));
            }
        }

        for (int i = spots.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            Vector3 swap = spots[i];
            spots[i] = spots[j];
            spots[j] = swap;
        }

        return spots;
    }

    // Takes grid points from the end of the list until one is free, far enough from the
    // spawn point and, when asked, on the NavMesh. That spot is reserved.
    private bool TakeSpot(List<Vector3> spots, float radius, float minFromSpawn, bool onNavMesh,
                          out Vector3 position)
    {
        while (spots.Count > 0)
        {
            int last = spots.Count - 1;
            Vector3 candidate = spots[last];
            spots.RemoveAt(last);

            if (!IsFree(candidate, radius) || FlatDistance(candidate, spawn) < minFromSpawn)
                continue;

            if (onNavMesh)
            {
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, NavSampleDistance, NavMesh.AllAreas))
                    continue;

                candidate = hit.position;
            }

            Reserve(candidate, radius);
            position = candidate;
            return true;
        }

        position = transform.position;
        return false;
    }

    // Finds the target, or a free random spot near it, that keeps its free distance.
    private bool FindFreeNear(Vector3 target, float radius, out Vector3 position)
    {
        position = target;
        if (IsFree(target, radius))
            return true;

        for (int i = 0; i < SearchTries; i++)
        {
            Vector3 direction = Quaternion.Euler(0f, RandomRange(0f, 360f), 0f) * Vector3.forward;
            Vector3 candidate = ClampInside(target + direction * RandomRange(radius, radius * 3f), radius);
            if (IsFree(candidate, radius))
            {
                position = candidate;
                return true;
            }
        }

        return false;
    }

    // True when the spot keeps both free distances to every taken spot.
    private bool IsFree(Vector3 position, float radius)
    {
        for (int i = 0; i < taken.Count; i++)
        {
            if (FlatDistance(position, taken[i].position) < taken[i].radius + radius)
                return false;
        }

        return true;
    }

    private void Reserve(Vector3 position, float radius)
    {
        taken.Add(new Spot(position, radius));
    }

    // A random point inside the border band, keeping the margin from the band.
    private Vector3 RandomInnerPoint(float margin)
    {
        float reach = mapSize * 0.5f - borderWidth - margin;
        return transform.position + new Vector3(RandomRange(-reach, reach), 0f, RandomRange(-reach, reach));
    }

    // Moves a point inside the border band, keeping the margin from the band.
    private Vector3 ClampInside(Vector3 point, float margin)
    {
        float reach = mapSize * 0.5f - borderWidth - margin;
        Vector3 center = transform.position;
        point.x = Mathf.Clamp(point.x, center.x - reach, center.x + reach);
        point.z = Mathf.Clamp(point.z, center.z - reach, center.z + reach);
        point.y = center.y;
        return point;
    }

    private float RandomRange(float min, float max)
    {
        return min + (float)rng.NextDouble() * (max - min);
    }

    private Quaternion RandomYaw()
    {
        return Quaternion.Euler(0f, RandomRange(0f, 360f), 0f);
    }

    private GameObject Pick(GameObject[] prefabs)
    {
        return prefabs[rng.Next(prefabs.Length)];
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}