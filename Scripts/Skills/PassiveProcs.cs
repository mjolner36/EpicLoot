using System.Collections.Generic;
using Godot;
using EpicLoot.Combat;
using EpicLoot.Core;
using EpicLoot.Stats;

namespace EpicLoot.Skills;

/// <summary>Особые эффекты пассивок атласа. Все числа приходят из статов атакующего.</summary>
public static class PassiveProcs
{
	private static uint MaskOf(ICombatant target) => target.IsPlayer ? Layers.PlayerHurtbox : Layers.EnemyHurtbox;

	/// <summary>Разряд: удар перескакивает на ближайшие цели.</summary>
	public static void Chain(ICombatant first, DamageInfo info, int count, float range)
	{
		if (first?.Body == null || !first.Body.IsInsideTree()) return;
		var visited = new List<ICombatant> { first };
		var from = first.Body;
		var fromPos = from.GlobalPosition + Vector3.Up;
		var color = DamageInfo.ColorOf(DamageType.Lightning);
		for (int i = 0; i < count; i++)
		{
			var next = Hitbox.Nearest(from.GetWorld3D(), fromPos, range, MaskOf(first), 1, visited);
			if (next.Count == 0) break;
			var target = next[0];
			visited.Add(target);
			var toPos = target.Body.GlobalPosition + Vector3.Up;
			Fx.Beam(from, fromPos, toPos, color, 0.15f);
			DamageSystem.ApplySecondary(info, target, keepAilments: true);
			fromPos = toPos;
		}
	}

	/// <summary>Абсолютный ноль: раскол замороженной цели, доля урона соседям.</summary>
	public static void Shatter(ICombatant target, DamageInfo info, float dealt)
	{
		var src = info.SourceStats;
		float radius = src.Get(StatType.ShatterRadius);
		float fraction = src.Get(StatType.ShatterSplash);
		target.Status?.Remove(GameState.Instance.Config.Freeze.Id);
		var body = target.Body;
		if (body == null || !body.IsInsideTree()) return;
		var center = body.GlobalPosition;
		Fx.Disc(body, center, radius, new Color(0.8f, 0.95f, 1f, 0.5f), 0.2f);
		var splash = DamageSystem.BuildRaw(src, info.SourceNode, DamageType.Cold, dealt * fraction, false);
		foreach (var other in Hitbox.OverlapSphere(body.GetWorld3D(), center + Vector3.Up * 0.5f, radius, MaskOf(target)))
			if (other != target) DamageSystem.ApplySecondary(splash, other);
	}

	/// <summary>Пожарище: Поджог переходит на соседей при смерти цели.</summary>
	public static void SpreadIgnite(ICombatant dying, StatusEffect ignite, float radius)
	{
		var body = dying.Body;
		if (body == null || !body.IsInsideTree()) return;
		var center = body.GlobalPosition;
		Fx.Disc(body, center, radius, new Color(1f, 0.5f, 0.1f, 0.35f), 0.25f);
		float hit = ignite.DamagePerSecond / Mathf.Max(0.01f, ignite.Data.DamagePerSecondFraction);
		foreach (var other in Hitbox.OverlapSphere(body.GetWorld3D(), center + Vector3.Up * 0.5f, radius, MaskOf(dying)))
			if (other != dying) other.Status?.Apply(ignite.Data, ignite.SourceStats, hit);
	}

	/// <summary>Гроза: молния по нескольким врагам рядом с атакующим.</summary>
	public static void Storm(ICombatant caster)
	{
		var stats = caster.Stats;
		var body = caster.Body;
		int targets = Mathf.RoundToInt(stats.Get(StatType.StormTargets));
		float radius = stats.Get(StatType.StormRadius);
		var color = DamageInfo.ColorOf(DamageType.Lightning);
		foreach (var target in Hitbox.Nearest(body.GetWorld3D(), body.GlobalPosition + Vector3.Up, radius, Layers.EnemyHurtbox, targets))
		{
			var p = target.Body.GlobalPosition;
			Fx.Beam(body, p + Vector3.Up * 8f, p, color, 0.18f, 0.25f);
			DamageSystem.Apply(DamageSystem.BuildRaw(stats, body, DamageType.Lightning, stats.Get(StatType.StormDamage), true, "Lightning"), target);
		}
	}
}
