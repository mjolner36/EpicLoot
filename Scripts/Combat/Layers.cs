namespace EpicLoot.Combat;

/// <summary>Слои коллизий (совпадают с именами в project.godot).</summary>
public static class Layers
{
	public const uint World = 1 << 0;
	public const uint PlayerBody = 1 << 1;
	public const uint EnemyBody = 1 << 2;
	public const uint PlayerHurtbox = 1 << 3;
	public const uint EnemyHurtbox = 1 << 4;
}
