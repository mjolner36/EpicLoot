using Godot;
using Godot.Collections;

namespace EpicLoot.Enemies;

public enum EnemyAttackKind
{
	None,
	Melee,
	Ranged,
	Slam,
}

[GlobalClass]
public partial class EnemyData : Resource
{
	[Export] public string Id { get; set; } = "";
	[Export] public string DisplayName { get; set; } = "";
	[Export] public string[] Tags { get; set; } = System.Array.Empty<string>();

	[ExportGroup("Статы")]
	[Export] public float MaxLife { get; set; } = 30f;
	[Export] public float MoveSpeed { get; set; } = 3f;
	/// <summary>Дополнительные базовые статы: имя StatType → значение.</summary>
	[Export] public Dictionary ExtraStats { get; set; } = new();

	[ExportGroup("Атака")]
	[Export] public EnemyAttackKind AttackKind { get; set; } = EnemyAttackKind.Melee;
	[Export] public float AttackDamage { get; set; } = 8f;
	/// <summary>Дистанция, с которой враг начинает атаку.</summary>
	[Export] public float AttackRange { get; set; } = 1.6f;
	/// <summary>Полный цикл атаки (раз в N секунд).</summary>
	[Export] public float AttackInterval { get; set; } = 1.2f;
	/// <summary>Замах до удара.</summary>
	[Export] public float AttackWindup { get; set; } = 0.3f;
	[Export] public float PreferredMinRange { get; set; }
	[Export] public float PreferredMaxRange { get; set; }
	[Export] public float ProjectileSpeed { get; set; } = 14f;
	[Export] public float AoeRadius { get; set; } = 3f;
	[Export] public float TelegraphTime { get; set; } = 1f;

	[ExportGroup("Лут")]
	[Export] public float ItemDropChance { get; set; }
	[Export] public float ShardDropChance { get; set; }
	[Export] public float GlyphRuneDropChance { get; set; }
	[Export] public float MapDropChance { get; set; }
	/// <summary>Гарантированные предметы (элита). Первый — не ниже GuaranteedMinRarity.</summary>
	[Export] public int GuaranteedItems { get; set; }
	[Export] public EpicLoot.Items.Rarity GuaranteedMinRarity { get; set; }
	[Export] public int GuaranteedShards { get; set; }
	/// <summary>Глифы/руны: целая часть — гарантированно, дробная — шанс ещё одного.</summary>
	[Export] public float GuaranteedGlyphRunes { get; set; }
	[Export] public int GuaranteedMaps { get; set; }

	[ExportGroup("Вид")]
	[Export] public float Scale { get; set; } = 1f;
	[Export] public Color BodyColor { get; set; } = new(0.75f, 0.73f, 0.68f);
	[Export] public Color AccentColor { get; set; } = new(0.3f, 0.3f, 0.3f);
	[Export] public PackedScene VisualScene { get; set; }
	/// <summary>Видимые части модели (SK_Helmet, SK_Tunic, SK_Belt, SK_Boots, Sword, Bow).</summary>
	[Export] public string[] VisibleParts { get; set; } = System.Array.Empty<string>();
}
