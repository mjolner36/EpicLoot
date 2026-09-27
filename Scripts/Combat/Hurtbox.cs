using Godot;

namespace EpicLoot.Combat;

/// <summary>Зона, по которой попадают удары. Ссылается на владельца-ICombatant.</summary>
public partial class Hurtbox : Area3D
{
	public ICombatant Combatant { get; private set; }

	public static Hurtbox Create(ICombatant owner, float radius, float height, bool isPlayer)
	{
		var hb = new Hurtbox
		{
			Name = "Hurtbox",
			Combatant = owner,
			Monitoring = false,
			Monitorable = true,
			CollisionLayer = isPlayer ? Layers.PlayerHurtbox : Layers.EnemyHurtbox,
			CollisionMask = 0,
		};
		var shape = new CollisionShape3D
		{
			Shape = new CapsuleShape3D { Radius = radius, Height = Mathf.Max(height, radius * 2f) },
			Position = new Vector3(0, height * 0.5f, 0),
		};
		hb.AddChild(shape);
		return hb;
	}

	public void Disable()
	{
		SetDeferred(Area3D.PropertyName.Monitorable, false);
		SetDeferred(CollisionObject3D.PropertyName.CollisionLayer, 0);
	}
}
