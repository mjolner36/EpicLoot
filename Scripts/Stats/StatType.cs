namespace EpicLoot.Stats;

// Порядок значений важен: .tres хранят enum как число. Добавлять новые статы только в конец.
public enum StatType
{
	None,
	MaxLife,
	MoveSpeed,
	Damage,
	FireDamage,
	ColdDamage,
	LightningDamage,
	AttackSpeed,
	CritChance,
	CritMultiplier,
	AilmentChance,
	AilmentDuration,
	DashCharges,
	Armour,
	FireResistance,
	ColdResistance,
	LightningResistance,
	DamageTaken,
	IgniteChance,
	ChillChance,
	ShockChance,
	IgniteDuration,
	ChillStacksPerHit,
	FreezeStackReduction,
	FireBlastCooldown,
	AddedFireAll,
	MeleeAddedLightning,
	ShardAddedLightning,
	ShardPierce,
	ShardChain,
	FrozenHitMultiplier,
	ShatterSplash,
	ShatterRadius,
	IgniteSpreadRadius,
	StormEveryN,
	StormTargets,
	StormDamage,
	StormRadius,
	WeaponDamage,
	ShardProjectiles,
	ProjectileSpeed,
}

public enum ModifierType
{
	Flat,
	Increased,
	More,
}

public static class StatDefaults
{
	// Нейтральные значения: множители = 1, остальное = 0. Балансные базы задаются в .tres.
	public static float Get(StatType stat) => stat switch
	{
		StatType.Damage or StatType.FireDamage or StatType.ColdDamage or StatType.LightningDamage
			or StatType.AttackSpeed or StatType.AilmentDuration or StatType.DamageTaken
			or StatType.IgniteDuration or StatType.FireBlastCooldown or StatType.FrozenHitMultiplier
			or StatType.CritMultiplier or StatType.ProjectileSpeed => 1f,
		_ => 0f,
	};

	public static readonly int Count = System.Enum.GetValues<StatType>().Length;
}

public static class Tags
{
	public const string Frozen = "Frozen";
	public const string Chilled = "Chilled";
	public const string Shocked = "Shocked";
	public const string Ignited = "Ignited";
	public const string Dashing = "Dashing";
	public const string Invulnerable = "Invulnerable";
	public const string Elite = "Elite";
	public const string NoChill = "NoChill";
	public const string NoIgnite = "NoIgnite";
}
