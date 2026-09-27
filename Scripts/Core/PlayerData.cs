using Godot;
using Godot.Collections;
using EpicLoot.Skills;
using EpicLoot.Stats;

namespace EpicLoot.Core;

[GlobalClass]
public partial class PlayerData : Resource
{
	/// <summary>Базовые статы: имя StatType → значение.</summary>
	[Export] public Dictionary BaseStats { get; set; } = new();
	/// <summary>Умения по слотам: ЛКМ, ПКМ, Q.</summary>
	[Export] public SkillData[] Skills { get; set; } = System.Array.Empty<SkillData>();
	[Export] public SkillData Dash { get; set; }
	[Export] public Color BodyColor { get; set; } = new(0.85f, 0.83f, 0.78f);
	[Export] public Color AccentColor { get; set; } = new(0.3f, 0.9f, 1f);
	/// <summary>Сцена модели (этап 8). Пусто — серые примитивы.</summary>
	[Export] public PackedScene VisualScene { get; set; }

	public void ApplyTo(StatBlock stats) => ApplyBaseStats(BaseStats, stats);

	public static void ApplyBaseStats(Dictionary values, StatBlock stats)
	{
		if (values == null) return;
		foreach (var (key, value) in values)
		{
			if (System.Enum.TryParse<StatType>(key.AsString(), out var stat))
				stats.SetBase(stat, (float)value.AsDouble());
			else
				GD.PushError($"Неизвестный стат в данных: {key}");
		}
	}
}
