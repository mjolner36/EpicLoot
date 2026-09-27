using System.Collections.Generic;
using Godot;
using EpicLoot.Combat;
using EpicLoot.Stats;

namespace EpicLoot.Skills;

/// <summary>Реализации умений игрока по SkillKind.</summary>
public static class SkillExecutor
{
	/// <summary>Выполняет умение. Возвращает true, если оно сработало.</summary>
	public static bool Execute(SkillData skill, ICombatant caster, Vector3 aimPoint)
	{
		var body = caster.Body;
		var origin = body.GlobalPosition;
		var dir = aimPoint - origin;
		dir.Y = 0;
		if (dir.LengthSquared() < 0.0001f) dir = -body.GlobalBasis.Z;
		dir = dir.Normalized();

		switch (skill.Kind)
		{
			case SkillKind.MeleeCone:
				MeleeCone(skill, caster, origin, dir);
				return true;
			case SkillKind.Projectile:
				Shoot(skill, caster, origin, dir);
				return true;
			case SkillKind.GroundCircle:
				GroundCircle(skill, caster, origin, aimPoint);
				return true;
			default:
				return false;
		}
	}

	private static void MeleeCone(SkillData skill, ICombatant caster, Vector3 origin, Vector3 dir)
	{
		var body = caster.Body;
		Fx.Cone(body, origin, dir, skill.Radius, skill.Angle, WithAlpha(skill.Color, 0.45f), 0.12f);
		foreach (var target in Hitbox.OverlapCone(body.GetWorld3D(), origin + Vector3.Up, dir, skill.Radius, skill.Angle, Layers.EnemyHurtbox))
			DamageSystem.Apply(DamageSystem.Build(caster.Stats, body, skill), target);
	}

	private static void Shoot(SkillData skill, ICombatant caster, Vector3 origin, Vector3 dir)
	{
		var body = caster.Body;
		var stats = caster.Stats;
		int pierce = skill.PierceStat != StatType.None ? Mathf.RoundToInt(stats.Get(skill.PierceStat)) : 0;
		int chains = skill.ChainStat != StatType.None ? Mathf.RoundToInt(stats.Get(skill.ChainStat)) : 0;
		int count = 1 + (skill.ExtraProjectilesStat != StatType.None ? Mathf.Max(0, Mathf.RoundToInt(stats.Get(skill.ExtraProjectilesStat))) : 0);
		for (int i = 0; i < count; i++)
		{
			// Веер симметрично относительно направления прицела.
			float angle = Mathf.DegToRad(skill.ProjectileSpread * (i - (count - 1) * 0.5f));
			var d = dir.Rotated(Vector3.Up, angle);
			var p = Projectile.Spawn(body, origin + Vector3.Up * 1.1f + d * 0.6f, d, skill.Speed, skill.Range, Layers.EnemyHurtbox, skill.Color);
			p.Pierce = pierce;
			p.OnHitTarget = target =>
			{
				if (!GodotObject.IsInstanceValid(body)) return true;
				var info = DamageSystem.Build(stats, body, skill);
				DamageSystem.Apply(info, target);
				if (chains > 0) PassiveProcs.Chain(target, info, chains, skill.ChainRange);
				return true;
			};
		}
	}

	private static void GroundCircle(SkillData skill, ICombatant caster, Vector3 origin, Vector3 aimPoint)
	{
		var body = caster.Body;
		var to = aimPoint - origin;
		to.Y = 0;
		if (to.Length() > skill.Range) to = to.Normalized() * skill.Range;
		var center = origin + to;
		center.Y = 0;
		Fx.Disc(body, center, skill.Radius, WithAlpha(skill.Color, 0.5f), 0.2f);
		foreach (var target in Hitbox.OverlapSphere(body.GetWorld3D(), center + Vector3.Up * 0.5f, skill.Radius, Layers.EnemyHurtbox))
			DamageSystem.Apply(DamageSystem.Build(caster.Stats, body, skill), target);
	}

	private static Color WithAlpha(Color c, float a) => new(c.R, c.G, c.B, a);
}
