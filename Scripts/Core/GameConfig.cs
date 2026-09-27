using Godot;
using EpicLoot.Stats;

namespace EpicLoot.Core;

/// <summary>Глобальные числа баланса и ссылки на данные. Лежит в res://Data/game_config.tres.</summary>
[GlobalClass]
public partial class GameConfig : Resource
{
	[ExportGroup("Данные")]
	[Export] public PlayerData Player { get; set; }
	[Export] public EpicLoot.Items.LootConfig Loot { get; set; }
	[Export] public StatusData Ignite { get; set; }
	[Export] public StatusData Chill { get; set; }
	[Export] public StatusData Freeze { get; set; }
	[Export] public StatusData Shock { get; set; }
	[Export] public PackedScene PlayerScene { get; set; }
	[Export] public PackedScene SoldierScene { get; set; }
	[Export] public PackedScene ArcherScene { get; set; }
	[Export] public PackedScene KnightScene { get; set; }
	[Export] public PackedScene DummyScene { get; set; }
	[Export] public string AtlasNodesDir { get; set; } = "res://Data/Atlas/Nodes";
	[Export] public string StartNodeId { get; set; } = "start";

	[ExportGroup("Бой")]
	/// <summary>Броня: снижение = A / (A + ArmourFactor × урон).</summary>
	[Export] public float ArmourFactor { get; set; } = 5f;
	[Export] public float MaxResistance { get; set; } = 0.75f;

	[ExportGroup("Волны")]
	[Export] public int[] WaveSizes { get; set; } = { 10, 15, 20 };
	/// <summary>Следующая волна стартует, когда живых врагов меньше этого числа.</summary>
	[Export] public int NextWaveThreshold { get; set; } = 3;
	[Export] public float ArcherShare { get; set; } = 0.3f;
	[Export] public float SpawnInterval { get; set; } = 0.12f;
	[Export] public float FirstWaveDelay { get; set; } = 1.0f;
	[Export] public int StressSpawnCount { get; set; } = 60;

	[ExportGroup("Сложность атласа")]
	/// <summary>+X к Increased жизни и урона врагов за каждый шаг от старта.</summary>
	[Export] public float DifficultyPerStep { get; set; } = 0.15f;
	[Export] public float KeystoneLifeMultiplier { get; set; } = 3f;
	[Export] public int KeystoneEscort { get; set; } = 5;

	[ExportGroup("Арена")]
	[Export] public float ArenaSize { get; set; } = 40f;
	[Export] public int ObstacleMin { get; set; } = 6;
	[Export] public int ObstacleMax { get; set; } = 10;
	[Export] public float SpawnInset { get; set; } = 3f;

	[ExportGroup("Камера")]
	[Export] public float CameraSize { get; set; } = 24f;
	[Export] public float CameraPitch { get; set; } = -45f;
	[Export] public float CameraYaw { get; set; } = 45f;
	[Export] public float CameraDistance { get; set; } = 30f;
}
