using System.Collections.Generic;
using Godot;

namespace EpicLoot.Combat;

/// <summary>Мгновенные запросы формы удара: сфера, конус, луч. Находят Hurtbox через физику.</summary>
public static class Hitbox
{
	private static readonly SphereShape3D Sphere = new();

	public static List<ICombatant> OverlapSphere(World3D world, Vector3 center, float radius, uint hurtboxMask, int max = 64)
	{
		var result = new List<ICombatant>();
		Sphere.Radius = radius;
		var q = new PhysicsShapeQueryParameters3D
		{
			Shape = Sphere,
			Transform = new Transform3D(Basis.Identity, center),
			CollisionMask = hurtboxMask,
			CollideWithAreas = true,
			CollideWithBodies = false,
		};
		foreach (var hit in world.DirectSpaceState.IntersectShape(q, max))
		{
			if (hit["collider"].AsGodotObject() is Hurtbox hb && hb.Combatant is { IsDead: false } c && !result.Contains(c))
				result.Add(c);
		}
		return result;
	}

	/// <summary>Конус на плоскости XZ: радиус и полный угол в градусах.</summary>
	public static List<ICombatant> OverlapCone(World3D world, Vector3 origin, Vector3 forward, float radius, float angleDeg, uint hurtboxMask)
	{
		var result = new List<ICombatant>();
		forward.Y = 0;
		forward = forward.Normalized();
		float cosHalf = Mathf.Cos(Mathf.DegToRad(angleDeg * 0.5f));
		// Сфера чуть больше радиуса, чтобы зацепить края капсул.
		foreach (var c in OverlapSphere(world, origin, radius + 0.4f, hurtboxMask))
		{
			var to = c.Body.GlobalPosition - origin;
			to.Y = 0;
			if (to.Length() < 0.6f || to.Normalized().Dot(forward) >= cosHalf) result.Add(c);
		}
		return result;
	}

	public static bool RayHitsWorld(World3D world, Vector3 from, Vector3 to, out Vector3 point)
	{
		var q = PhysicsRayQueryParameters3D.Create(from, to, Layers.World);
		var hit = world.DirectSpaceState.IntersectRay(q);
		if (hit.Count > 0)
		{
			point = hit["position"].AsVector3();
			return true;
		}
		point = to;
		return false;
	}

	public static List<ICombatant> Nearest(World3D world, Vector3 center, float radius, uint mask, int count, ICollection<ICombatant> exclude = null)
	{
		var list = OverlapSphere(world, center, radius, mask);
		if (exclude != null) list.RemoveAll(exclude.Contains);
		list.Sort((a, b) => a.Body.GlobalPosition.DistanceSquaredTo(center).CompareTo(b.Body.GlobalPosition.DistanceSquaredTo(center)));
		if (list.Count > count) list.RemoveRange(count, list.Count - count);
		return list;
	}
}
