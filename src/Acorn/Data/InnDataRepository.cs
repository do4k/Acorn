using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Acorn.Data;

/// <summary>
/// Loads inn data from JSON files in Data/Inns/ directory
/// </summary>
public class InnDataRepository : IInnDataRepository
{
    private readonly ILogger<InnDataRepository> _logger;
    private readonly List<InnData> _inns = [];
    private const string InnsDirectory = "Data/Inns";
    private const string DefaultHome = "Wanderer";

    public string DefaultHomeName => DefaultHome;

    public InnDataRepository(ILogger<InnDataRepository> logger)
    {
        _logger = logger;
        LoadInns();
    }

    private void LoadInns()
    {
        if (!Directory.Exists(InnsDirectory))
        {
            _logger.LogWarning("Inns directory not found at {Directory}. Creating with sample inn.", InnsDirectory);
            Directory.CreateDirectory(InnsDirectory);
            CreateSampleInn();
            return;
        }

        var jsonFiles = Directory.GetFiles(InnsDirectory, "*.json");
        if (jsonFiles.Length == 0)
        {
            _logger.LogWarning("No inn files found in {Directory}. Creating sample inn.", InnsDirectory);
            CreateSampleInn();
            return;
        }

        foreach (var file in jsonFiles)
        {
            try
            {
                var json = File.ReadAllText(file);
                var innJson = JsonSerializer.Deserialize<InnJsonModel>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (innJson == null)
                {
                    _logger.LogWarning("Failed to parse inn file: {File}", file);
                    continue;
                }

                var inn = new InnData(
                    innJson.BehaviorId,
                    innJson.Name ?? "Unknown Inn",
                    innJson.SpawnMap,
                    innJson.SpawnX,
                    innJson.SpawnY,
                    innJson.SleepMap,
                    innJson.SleepX,
                    innJson.SleepY,
                    innJson.AlternateSpawnEnabled,
                    innJson.AlternateSpawnMap,
                    innJson.AlternateSpawnX,
                    innJson.AlternateSpawnY,
                    innJson.Questions?.Select(q => new InnQuestion(
                        q.Question ?? "",
                        q.Answer ?? ""
                    )).ToList() ?? []
                );

                _inns.Add(inn);
                _logger.LogInformation("Loaded inn: {Name} (BehaviorId: {BehaviorId})",
                    inn.Name, inn.BehaviorId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading inn file: {File}", file);
            }
        }

        _logger.LogInformation("Loaded {Count} inns", _inns.Count);
    }

    private void CreateSampleInn()
    {
        var sampleInn = new
        {
            behavior_id = 1,
            name = "Aeven",
            spawn_map = 192,
            spawn_x = 7,
            spawn_y = 6,
            sleep_map = 192,
            sleep_x = 7,
            sleep_y = 6,
            alternate_spawn_enabled = false,
            alternate_spawn_map = 0,
            alternate_spawn_x = 0,
            alternate_spawn_y = 0,
            questions = new[]
            {
                new { question = "What is the name of this town?", answer = "Aeven" },
                new { question = "What color is the sky?", answer = "Blue" },
                new { question = "What is 1 + 1?", answer = "2" }
            }
        };

        var json = JsonSerializer.Serialize(sampleInn, new JsonSerializerOptions { WriteIndented = true });
        var samplePath = Path.Combine(InnsDirectory, "aeven.json");
        File.WriteAllText(samplePath, json);
        _logger.LogInformation("Created sample inn file at {Path}", samplePath);
        
        // Reload after creating sample
        LoadInns();
    }

    public InnData? GetInnByBehaviorId(int behaviorId)
    {
        return _inns.FirstOrDefault(i => i.BehaviorId == behaviorId);
    }

    public InnData? GetInnByName(string name)
    {
        return _inns.FirstOrDefault(i => i.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    public IEnumerable<InnData> GetAllInns()
    {
        return _inns;
    }

    // JSON model classes for deserialization
    private class InnJsonModel
    {
        public int BehaviorId { get; set; }
        public string? Name { get; set; }
        public int SpawnMap { get; set; }
        public int SpawnX { get; set; }
        public int SpawnY { get; set; }
        public int SleepMap { get; set; }
        public int SleepX { get; set; }
        public int SleepY { get; set; }
        public bool AlternateSpawnEnabled { get; set; }
        public int AlternateSpawnMap { get; set; }
        public int AlternateSpawnX { get; set; }
        public int AlternateSpawnY { get; set; }
        public List<QuestionJsonModel>? Questions { get; set; }
    }

    private class QuestionJsonModel
    {
        public string? Question { get; set; }
        public string? Answer { get; set; }
    }
}
