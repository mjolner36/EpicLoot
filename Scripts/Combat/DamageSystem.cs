using Godot;
using EpicLoot.Core;
using EpicLoot.Skills;
using EpicLoot.Stats;
using EpicLoot.UI;

namespace EpicLoot.Combat;

/// <summary>
/// Конвейер урона: база умения → множители атакующего → крит → броня и сопротивления цели → шансы статусов → применение.
/// </summary>
public static class DamageSystem
{
	private static readonly RandomNumberGenerator Rng = new();

	private static GameConfig Config => GameState.Instance.Config;

	/// <summary>Урон умения (база из SkillData).</summary>
	public static DamageInfo Build(StatBlock attacker, Node3D attackerNode, SkillData skill)
	{
		var info = new DamageInfo { SourceStats = attacker, SourceNode = attackerNode, SkillTags = skill.Tags ?? System.Array.Empty<string>() };
		info[DamageType.Physical] = skill.PhysicalDamage;
		if (skill.BaseDamageStat != StatType.None && attacker.Get(skill.BaseDamageStat) > 0f)
			info[DamageType.Physical] = attacker.Get(skill.BaseDamageStat);
		info[DamageType.Fire] = skill.FireDamage;
		info[DamageType.Cold] = skill.ColdDamage;
		info[DamageType.Lightning] = skill.LightningDamage;

		// "Добавляет X% урона как ..." считается от базы умения.
		float baseTotal = info.Total;
		if (skill.AddedLightningStat != StatType.None)
			info[DamageType.Lightning] += baseTotal * attacker.Get(skill.AddedLightningStat);
		info[DamageType.Fire] += baseTotal * attacker.Get(StatType.AddedFireAll);

		ApplyAttackerMultipliers(info, attacker, true);
		info.IgniteChance = skill.IgniteChance;
		info.ChillChance = skill.ChillChance;
		info.ShockChance = skill.ShockChance;
		AddAilmentChances(info, attacker);
		return info;
	}

	/// <summary>Урон одного типа без умения (атаки врагов, Гроза).</summary>
	public static DamageInfo BuildRaw(StatBlock attacker, Node3D attackerNode, DamageType type, float amount, bool canCrit, params string[] tags)
	{
		var info = new DamageInfo { SourceStats = attacker, SourceNode = attackerNode, SkillTags = tags };
		info[type] = amount;
		ApplyAttackerMultipliers(info, attacker, canCrit);
		AddAilmentChances(info, attacker);
		return info;
	}

	private static void ApplyAttackerMultipliers(DamageInfo info, StatBlock a, bool canCrit)
	{
		float dmg = a.Get(StatType.Damage);
		info[DamageType.Physical] *= dmg;
		info[DamageType.Fire] *= dmg * a.Get(StatType.FireDamage);
		info[DamageType.Cold] *= dmg * a.Get(StatType.ColdDamage);
		info[DamageType.Lightning] *= dmg * a.Get(StatType.LightningDamage);

		if (canCrit && Rng.Randf() < a.Get(StatType.CritChance))
		{
			info.IsCrit = true;
			float mult = a.Get(StatType.CritMultiplier);
			for (int i = 0; i < 4; i++) info.Amounts[i] *= mult;
		}
	}

	private static void AddAilmentChances(DamageInfo info, StatBlock a)
	{
		float generic = a.Get(StatType.AilmentChance);
		info.IgniteChance = info[DamageType.Fire] > 0f && !a.HasTag(Tags.NoIgnite)
			? info.IgniteChance + a.Get(StatType.IgniteChance) + generic : 0f;
		info.ChillChance = info[DamageType.Cold] > 0f && !a.HasTag(Tags.NoChill)
			? info.ChillChance + a.Get(StatType.ChillChance) + generic : 0f;
		info.ShockChance = info[DamageType.Lightning] > 0f
			? info.ShockChance + a.Get(StatType.ShockChance) + generic : 0f;
	}

	/// <summary>Применяет удар к цели. Возвращает итоговый нанесённый урон.</summary>
	public static float Apply(DamageInfo info, ICombatant target)
	{
		if (target == null || target.IsDead) return 0f;
		var t = target.Stats;
		if (t.HasTag(Tags.Invulnerable)) return 0f;

		float total = Mitigate(info, t);

		var src = info.SourceStats;
		bool wasFrozen = t.HasTag(Tags.Frozen);
		if (wasFrozen && src != null)
			total *= src.Get(StatType.FrozenHitMultiplier);

		if (total <= 0f) return 0f;

		t.TakeDamage(total);
		target.OnHit(total, info.IsCrit);
		var color = info.IsCrit ? new Color(1f, 0.85f, 0.1f) : target.IsPlayer ? new Color(1f, 0.3f, 0.3f) : Colors.White;
		FloatingText.Damage(target.Body, total, color, info.IsCrit);

		// Раскол замороженной цели (Абсолютный ноль).
		if (wasFrozen && src != null && info.AllowSecondary && src.Get(StatType.ShatterSplash) > 0f)
			PassiveProcs.Shatter(target, info, total);

		if (!t.IsDead) RollAilments(info, target, total);
		return total;
	}

	/// <summary>Броня, сопротивления и множитель получаемого урона цели.</summary>
	public static float Mitigate(DamageInfo info, StatBlock t)
	{
		float phys = info[DamageType.Physical];
		float armour = t.Get(StatType.Armour);
		if (armour > 0f && phys > 0f) phys *= 1f - armour / (armour + Config.ArmourFactor * phys);

		float total = phys
			+ info[DamageType.Fire] * (1f - Res(t, StatType.FireResistance))
			+ info[DamageType.Cold] * (1f - Res(t, StatType.ColdResistance))
			+ info[DamageType.Lightning] * (1f - Res(t, StatType.LightningResistance));
		return total * t.Get(StatType.DamageTaken);
	}

	private static float Res(StatBlock t, StatType stat) => Mathf.Clamp(t.Get(stat), -1f, Config.MaxResistance);

	private static void RollAilments(DamageInfo info, ICombatant target, float dealt)
	{
		if (target.Status == null) return;
		var src = info.SourceStats;
		if (info.IgniteChance > 0f && Rng.Randf() < info.IgniteChance)
			target.Status.Apply(Config.Ignite, src, dealt);
		if (info.ShockChance > 0f && Rng.Randf() < info.ShockChance)
			target.Status.Apply(Config.Shock, src);
		if (info.ChillChance > 0f && Rng.Randf() < info.ChillChance)
			ApplyChill(target, src);
	}

	/// <summary>Холод копит стаки; при пороге переходит в Заморозку.</summary>
	public static void ApplyChill(ICombatant target, StatBlock src)
	{
		var status = target.Status;
		var chill = Config.Chill;
		int stacks = src != null ? Mathf.Max(1, Mathf.RoundToInt(src.Get(StatType.ChillStacksPerHit))) : 1;
		int reduction = src != null ? Mathf.RoundToInt(src.Get(StatType.FreezeStackReduction)) : 0;
		int threshold = Mathf.Max(1, chill.MaxStacks - reduction);
		status.Apply(chill, src, 0f, stacks);
		var fx = status.Get(chill.Id);
		if (fx != null && fx.Stacks >= threshold)
		{
			status.Remove(chill.Id);
			status.Apply(Config.Freeze, src);
		}
	}

	/// <summary>Тик урона от статуса: только множитель получаемого урона цели. Число показывает StatusController.</summary>
	public static float ApplyTick(ICombatant target, float amount)
	{
		if (target == null || target.IsDead) return 0f;
		float total = amount * target.Stats.Get(StatType.DamageTaken);
		if (total <= 0f) return 0f;
		target.OnHit(total, false, isTick: true);
		target.Stats.TakeDamage(total);
		return total;
	}

	/// <summary>Вторичный урон без статусов, крита и процов (раскол, цепь).</summary>
	public static float ApplySecondary(DamageInfo info, ICombatant target, bool keepAilments = false)
	{
		var c = info.Clone();
		c.AllowSecondary = false;
		if (!keepAilments) c.IgniteChance = c.ChillChance = c.ShockChance = 0f;
		return Apply(c, target);
	}
}
