using Godot;
using EpicLoot.Stats;

namespace EpicLoot.Atlas;

/// <summary>Мод карты: усложняет локацию и увеличивает лут.</summary>
[GlobalClass]
public partial class MapModData : Resource
{
	[Export] public string Id { get; set; } = "";
	[Export] public string DisplayName { get; set; } = "";
	[Export(PropertyHint.MultilineText)] public string Description { get; set; } = "";
	/// <summary>Применяются ко всем врагам с SourceId = "location".</summary>
	[Export] public Modifier[] EnemyModifiers { get; set; } = System.Array.Empty<Modifier>();
	[Export] public float EnemyCountMultiplier { get; set; } = 1f;
	[Export] public float ArcherShareMultiplier { get; set; } = 1f;
	[Export] public int EliteEscort { get; set; }
	/// <summary>Бонус к количеству лута (0.2 = +20%).</summary>
	[Export] public float QuantityBonus { get; set; }
	/// <summary>Бонус к редкости лута (0.1 = +10%).</summary>
	[Export] public float RarityBonus { get; set; }
	[Export] public float DropWeight { get; set; } = 1f;
}
