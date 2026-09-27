using System;
using System.Collections.Generic;
using Godot;

namespace EpicLoot.Combat;

/// <summary>
/// Снаряд, движется по XZ. Стены проверяются лучом, цели — сферой по слою Hurtbox
/// (без Area3D-сигналов: у Jolt зоны по умолчанию не видят статику).
/// </summary>
public partial class Projectile : Node3D
{
	private static SphereMesh _mesh;

	public Vector3 Direction;
	public float Speed = 10f;
	public float MaxDistance = 15f;
	public float Radius = 0.35f;
	public uint TargetMask = Layers.EnemyHurtbox;
	public int Pierce;
	public Func<ICombatant, bool> OnHitTarget; // true = снаряд засчитал попадание
	public Color Color = Colors.White;

	private float _travelled;
	private readonly HashSet<ICombatant> _hit = new();

	public static Projectile Spawn(Node from, Vector3 origin, Vector3 direction, float speed, float range, uint mask, Color color, float scale = 1f)
	{
		_mesh ??= new SphereMesh { Radius = 0.18f, Height = 0.36f, RadialSegments = 8, Rings = 4 };
		direction.Y = 0;
		var p = new Projectile
		{
			Direction = direction.Normalized(),
			Speed = speed,
			MaxDistance = range,
			TargetMask = mask,
			Color = color,
		};
		var mi = new MeshInstance3D { Mesh = _mesh, MaterialOverride = Fx.Unshaded(color), Scale = new Vector3(scale, scale, scale * 2.2f) };
		p.AddChild(mi);
		Fx.Root(from).AddChild(p);
		p.GlobalPosition = origin;
		if (p.Direction.LengthSquared() > 0.001f) p.LookAt(origin + p.Direction, Vector3.Up);
		return p;
	}

	public override void _PhysicsProcess(double delta)
	{
		var world = GetWorld3D();
		var from = GlobalPosition;
		var step = Direction * Speed * (float)delta;
		var to = from + step;

		if (Hitbox.RayHitsWorld(world, from, to, out _))
		{
			QueueFree();
			return;
		}
		GlobalPosition = to;
		_travelled += step.Length();

		foreach (var target in Hitbox.OverlapSphere(world, to, Radius, TargetMask, 8))
		{
			if (!_hit.Add(target)) continue;
			bool counted = OnHitTarget?.Invoke(target) ?? true;
			if (!counted) continue;
			if (Pierce-- <= 0)
			{
				QueueFree();
				return;
			}
		}

		if (_travelled >= MaxDistance) QueueFree();
	}
}
