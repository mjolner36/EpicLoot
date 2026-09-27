using Godot;
using EpicLoot.Stats;

namespace EpicLoot.Skills;

public enum SkillKind
{
	MeleeCone,
	Projectile,
	GroundCircle,
	Dash,
}

[GlobalClass]
public partial class SkillData : Resource
{
	[Export] public string Id { get; set; } = "";
	[Export] public string DisplayName { get; set; } = "";
	[Export] public string KeyLabel { get; set; } = "";
	[Export] public SkillKind Kind { get; set; }
	[Export] public string[] Tags { get; set; } = System.Array.Empty<string>();
	[Export] public Color Color { get; set; } = Colors.White;

	[ExportGroup("Урон")]
	[Export] public float PhysicalDamage { get; set; }
	[Export] public float FireDamage { get; set; }
	[Export] public float ColdDamage { get; set; }
	[Export] public float LightningDamage { get; set; }
	[Export] public float IgniteChance { get; set; }
	[Export] public float ChillChance { get; set; }
	[Export] public float ShockChance { get; set; }

	[ExportGroup("Тайминги")]
	[Export] public float Cooldown { get; set; } = 1f;
	[Export] public float CastTime { get; set; } = 0.2f;
	/// <summary>Множитель скорости движения во время каста.</summary>
	[Export] public float CastMoveMultiplier { get; set; } = 0.5f;

	[ExportGroup("Форма")]
	[Export] public float Radius { get; set; } = 1f;
	[Export] public float Angle { get; set; } = 90f;
	[Export] public float Speed { get; set; } = 10f;
	[Export] public float Range { get; set; } = 10f;
	[Export] public float DashDistance { get; set; } = 5f;
	[Export] public float DashDuration { get; set; } = 0.15f;
	[Export] public float ChainRange { get; set; } = 8f;

	[ExportGroup("Связь с пассивками")]
	/// <summary>Стат-множитель кулдауна (например FireBlastCooldown).</summary>
	[Export] public StatType CooldownStat { get; set; } = StatType.None;
	/// <summary>Стат "добавляет X% урона как молния".</summary>
	[Export] public StatType AddedLightningStat { get; set; } = StatType.None;
	[Export] public StatType PierceStat { get; set; } = StatType.None;
	[Export] public StatType ChainStat { get; set; } = StatType.None;
	/// <summary>Стат, заменяющий физ. урон умения, если он больше 0 (урон надетого оружия).</summary>
	[Export] public StatType BaseDamageStat { get; set; } = StatType.None;
	/// <summary>Стат дополнительных снарядов (веером).</summary>
	[Export] public StatType ExtraProjectilesStat { get; set; } = StatType.None;
	[Export] public float ProjectileSpread { get; set; } = 12f;

	public float TotalBaseDamage => PhysicalDamage + FireDamage + ColdDamage + LightningDamage;
}
