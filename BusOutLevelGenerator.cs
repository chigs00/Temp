using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.IO;

public class BusOutLevelGenerator : MonoBehaviour
{
    [System.Serializable]
    public struct Bus
    {
        public int id;
        public string color;
        public int capacity;
    }

    [System.Serializable]
    public struct Level
    {
        public List<Bus> buses;
        public List<string> passengerQueue;
        public int parkingSpots;
        public List<int[]> congestionEdges;
    }

    [System.Serializable]
    public struct GameState
    {
        public List<Bus> buses;
        public List<string> passengerQueue;
        public List<Bus> parkedBuses;
        public Dictionary<int, HashSet<int>> congestionGraph;
    }

    [System.Serializable]
    public struct LevelTemplate
    {
        public int minBuses, maxBuses;
        public int minColors, maxColors;
        public int minParkingSpots, maxParkingSpots;
        public int[] possibleCapacities;
        public int maxCongestionEdges;
        public int targetMoves;
    }

    public LevelTemplate easyTemplate = new LevelTemplate
    {
        minBuses = 4,
        maxBuses = 6, // Increased minBuses to 4
        minColors = 3,
        maxColors = 5,
        minParkingSpots = 2,
        maxParkingSpots = 6, // Increased maxParkingSpots to 6
        possibleCapacities = new[] { 4, 6, 10 },
        maxCongestionEdges = 0, // Reduced to 0 for easier solvability
        targetMoves = 5
    };

    public LevelTemplate hardTemplate = new LevelTemplate
    {
        minBuses = 6,
        maxBuses = 10,
        minColors = 4,
        maxColors = 5,
        minParkingSpots = 2,
        maxParkingSpots = 5, // Increased maxParkingSpots to 5
        possibleCapacities = new[] { 4, 6, 10 },
        maxCongestionEdges = 1, // Reduced to 1
        targetMoves = 10
    };

    private readonly string[] colors = { "R", "B", "G", "Y", "P" };
    private string outputPath;
    private const int MAX_RETRIES = 100;

    void Start()
    {
        outputPath = Path.Combine(Application.persistentDataPath, "level.json");
        Random.InitState((int)System.DateTime.Now.Ticks);
        try
        {
            GenerateAndSaveLevel(false);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to generate level: {e.Message}");
        }
    }

    public void GenerateAndSaveLevel(bool isHard)
    {
        Level level = GenerateLevel(isHard ? hardTemplate : easyTemplate);
        string json = JsonUtility.ToJson(level, true);
        File.WriteAllText(outputPath, json);
        Debug.Log($"Level saved to: {outputPath}\n{json}");
    }

    Level GenerateLevel(LevelTemplate template)
    {
        int retries = 0;
        while (retries < MAX_RETRIES)
        {
            Level level = new Level
            {
                buses = new List<Bus>(),
                passengerQueue = new List<string>(),
                congestionEdges = new List<int[]>()
            };

            int numBuses = Random.Range(template.minBuses, template.maxBuses + 1);
            int numColors = Random.Range(template.minColors, template.maxColors + 1);
            int numParkingSpots = Random.Range(template.minParkingSpots, template.maxParkingSpots + 1);

            string[] availableColors = colors.OrderBy(x => Random.value).Take(numColors).ToArray();

            Dictionary<string, int> colorCapacity = new Dictionary<string, int>();
            for (int i = 0; i < numBuses; i++)
            {
                string color = availableColors[Random.Range(0, availableColors.Length)];
                int capacity = template.possibleCapacities[Random.Range(0, template.possibleCapacities.Length)];
                level.buses.Add(new Bus { id = i + 1, color = color, capacity = capacity });
                colorCapacity[color] = colorCapacity.GetValueOrDefault(color, 0) + capacity;
            }

            int totalCapacity = colorCapacity.Values.Sum();
            int minPassengers = availableColors.Length;
            // Cap total passengers to avoid exceeding capacity
            int maxPassengers = totalCapacity * 2; // Allow up to twice the capacity for variety
            int passengerCount = Mathf.Min(minPassengers, maxPassengers);
            if (totalCapacity < minPassengers)
            {
                retries++;
                Debug.Log($"Retry {retries}: Total capacity ({totalCapacity}) < minimum passengers ({minPassengers})");
                continue;
            }

            // Generate passenger queue with controlled size
            foreach (var color in availableColors)
            {
                int baseCount = 1;
                int extraCount = colorCapacity.ContainsKey(color) ? Mathf.Min(colorCapacity[color], totalCapacity / numColors) : 0;
                int count = Mathf.Min(baseCount + extraCount, passengerCount / numColors); // Distribute evenly
                for (int i = 0; i < count; i++)
                    level.passengerQueue.Add(color);
                passengerCount -= count;
            }
            level.passengerQueue = level.passengerQueue.OrderBy(x => Random.value).ToList();

            for (int i = 0; i < template.maxCongestionEdges; i++)
            {
                int from = Random.Range(1, numBuses + 1);
                int to = Random.Range(1, numBuses + 1);
                if (from != to && !CreatesCycle(level.congestionEdges, from, to, numBuses))
                    level.congestionEdges.Add(new[] { from, to });
            }

            level.parkingSpots = numParkingSpots;

            if (SolveBusOut(level))
                return level;

            retries++;
            Debug.Log($"Retry {retries}: Level unsolvable");
        }

        throw new System.InvalidOperationException($"Failed to generate a solvable level after {MAX_RETRIES} retries. Consider adjusting template constraints.");
    }

    bool SolveBusOut(Level level)
    {
        var state = new GameState
        {
            buses = new List<Bus>(level.buses),
            passengerQueue = new List<string>(level.passengerQueue),
            parkedBuses = new List<Bus>(),
            congestionGraph = level.congestionEdges
                .GroupBy(e => e[0])
                .ToDictionary(g => g.Key, g => new HashSet<int>(g.Select(e => e[1])))
        };
        foreach (var bus in level.buses)
            if (!state.congestionGraph.ContainsKey(bus.id))
                state.congestionGraph[bus.id] = new HashSet<int>();
        return SolveBusOutRecursive(state, level.parkingSpots);
    }

    bool SolveBusOutRecursive(GameState state, int maxParkingSpots)
    {
        if (state.buses.Count == 0 && state.passengerQueue.Count == 0)
            return true;
        if (state.passengerQueue.Count > 0 && state.buses.Count == 0 || state.parkedBuses.Count > maxParkingSpots)
            return false;

        var validBuses = state.buses.Where(bus => !HasIncomingEdges(bus.id, state.congestionGraph)).ToList();
        validBuses.Sort((a, b) => CountMatchingPassengers(b.color, state.passengerQueue).CompareTo(CountMatchingPassengers(a.color, state.passengerQueue)));

        foreach (var bus in validBuses)
        {
            int passengersToTake = Mathf.Min(bus.capacity, CountMatchingPassengers(bus.color, state.passengerQueue));
            if (passengersToTake == 0 || state.parkedBuses.Count >= maxParkingSpots)
                continue;

            var newState = new GameState
            {
                buses = new List<Bus>(state.buses),
                passengerQueue = new List<string>(state.passengerQueue),
                parkedBuses = new List<Bus>(state.parkedBuses),
                congestionGraph = state.congestionGraph.ToDictionary(entry => entry.Key, entry => new HashSet<int>(entry.Value))
            };

            newState.buses.Remove(bus);
            newState.parkedBuses.Add(bus);
            for (int i = 0; i < passengersToTake; i++)
                newState.passengerQueue.RemoveAt(0);
            newState.congestionGraph = RemoveBusFromGraph(bus.id, newState.congestionGraph);
            if (passengersToTake == bus.capacity)
                newState.parkedBuses.Remove(bus);

            if (SolveBusOutRecursive(newState, maxParkingSpots))
                return true;
        }
        return false;
    }

    int CountMatchingPassengers(string color, List<string> queue)
    {
        int count = 0;
        foreach (var p in queue)
        {
            if (p != color)
                break;
            count++;
        }
        return count;
    }

    bool HasIncomingEdges(int busId, Dictionary<int, HashSet<int>> graph)
    {
        return graph.Any(entry => entry.Value.Contains(busId));
    }

    Dictionary<int, HashSet<int>> RemoveBusFromGraph(int busId, Dictionary<int, HashSet<int>> graph)
    {
        var newGraph = graph.ToDictionary(entry => entry.Key, entry => new HashSet<int>(entry.Value));
        newGraph.Remove(busId);
        foreach (var entry in newGraph)
            entry.Value.Remove(busId);
        return newGraph;
    }

    bool CreatesCycle(List<int[]> edges, int from, int to, int numBuses)
    {
        var graph = new Dictionary<int, HashSet<int>>();
        foreach (var bus in Enumerable.Range(1, numBuses + 1))
            graph[bus] = new HashSet<int>();
        foreach (var edge in edges)
            graph[edge[0]].Add(edge[1]);
        graph[from].Add(to);

        var visited = new HashSet<int>();
        var stack = new HashSet<int>();
        bool dfs(int node)
        {
            visited.Add(node);
            stack.Add(node);
            foreach (var next in graph[node])
            {
                if (!visited.Contains(next) && dfs(next))
                    return true;
                if (stack.Contains(next))
                    return true;
            }
            stack.Remove(node);
            return false;
        }
        return dfs(from);
    }
}