using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Acorn.Data;

/// <summary>
/// Loads skill master data from JSON files in Data/SkillMasters/ directory
/// </summary>
public class SkillMasterDataRepository : ISkillMasterDataRepository
{
    private readonly ILogger<SkillMasterDataRepository> _logger;
    private readonly List<SkillMasterData> _skillMasters = [];
    private const string SkillMastersDirectory = "Data/SkillMasters";

    public SkillMasterDataRepository(ILogger<SkillMasterDataRepository> logger)
    {
        _logger = logger;
        LoadSkillMasters();
    }

    private void LoadSkillMasters()
    {
        if (!Directory.Exists(SkillMastersDirectory))
        {
            _logger.LogWarning("SkillMasters directory not found at {Directory}. Creating with sample.", SkillMastersDirectory);
            Directory.CreateDirectory(SkillMastersDirectory);
            CreateSample();
            return;
        }

        var jsonFiles = Directory.GetFiles(SkillMastersDirectory, "*.json");
        if (jsonFiles.Length == 0)
        {
            _logger.LogWarning("No skill master files found in {Directory}. Creating sample.", SkillMastersDirectory);
            CreateSample();
            return;
        }

        foreach (var file in jsonFiles)
        {
            try
            {
                var json = File.ReadAllText(file);
                var model = JsonSerializer.Deserialize<SkillMasterJsonModel>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (model == null)
                {
                    _logger.LogWarning("Failed to parse skill master file: {File}", file);
                    continue;
                }

                var skillMaster = new SkillMasterData(
                    model.BehaviorId,
                    model.Name ?? "Unknown Skill Master",
                    model.MinLevel,
                    model.MaxLevel,
                    model.ClassRequirement,
                    model.Skills?.Select(s => new SkillMasterSkill(
                        s.Id,
                        s.LevelRequirement,
                        s.ClassRequirement,
                        s.Price,
                        s.SkillRequirements ?? [0, 0, 0, 0],
                        s.StrRequirement,
                        s.IntRequirement,
                        s.WisRequirement,
                        s.AgiRequirement,
                        s.ConRequirement,
                        s.ChaRequirement
                    )).ToList() ?? []
                );

                _skillMasters.Add(skillMaster);
                _logger.LogInformation("Loaded skill master: {Name} (BehaviorId: {BehaviorId}, {SkillCount} skills)",
                    skillMaster.Name, skillMaster.BehaviorId, skillMaster.Skills.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading skill master file: {File}", file);
            }
        }

        _logger.LogInformation("Loaded {Count} skill masters", _skillMasters.Count);
    }

    private void CreateSample()
    {
        var sample = new
        {
            behavior_id = 1,
            name = "Sample Skill Master",
            min_level = 0,
            max_level = 0,
            class_requirement = 0,
            skills = new[]
            {
                new
                {
                    id = 1,
                    level_requirement = 1,
                    class_requirement = 0,
                    price = 100,
                    skill_requirements = new[] { 0, 0, 0, 0 },
                    str_requirement = 0,
                    int_requirement = 0,
                    wis_requirement = 0,
                    agi_requirement = 0,
                    con_requirement = 0,
                    cha_requirement = 0
                }
            }
        };

        var json = JsonSerializer.Serialize(sample, new JsonSerializerOptions { WriteIndented = true });
        var samplePath = Path.Combine(SkillMastersDirectory, "sample_skill_master.json");
        File.WriteAllText(samplePath, json);
        _logger.LogInformation("Created sample skill master file at {Path}", samplePath);
    }

    public SkillMasterData? GetByBehaviorId(int behaviorId)
    {
        return _skillMasters.FirstOrDefault(s => s.BehaviorId == behaviorId);
    }

    public IEnumerable<SkillMasterData> GetAll()
    {
        return _skillMasters;
    }

    private class SkillMasterJsonModel
    {
        public int BehaviorId { get; set; }
        public string? Name { get; set; }
        public int MinLevel { get; set; }
        public int MaxLevel { get; set; }
        public int ClassRequirement { get; set; }
        public List<SkillJsonModel>? Skills { get; set; }
    }

    private class SkillJsonModel
    {
        public int Id { get; set; }
        public int LevelRequirement { get; set; }
        public int ClassRequirement { get; set; }
        public int Price { get; set; }
        public List<int>? SkillRequirements { get; set; }
        public int StrRequirement { get; set; }
        public int IntRequirement { get; set; }
        public int WisRequirement { get; set; }
        public int AgiRequirement { get; set; }
        public int ConRequirement { get; set; }
        public int ChaRequirement { get; set; }
    }
}
