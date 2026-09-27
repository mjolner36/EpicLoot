using Godot;
using EpicLoot.Stats;

namespace EpicLoot.Combat;

/// <summary>Всё, что можно ударить: игрок и враги. Реализуется корневой нодой сущности.</summary>
public interface ICombatant
{
	StatBlock Stats { get; }
	StatusController Status { get; }
	Node3D Body { get; }
	bool IsPlayer { get; }
	bool IsDead { get; }
	/// <summary>isTick — урон от статуса: без вспышки попадания.</summary>
	void OnHit(float amount, bool crit, bool isTick = false);
}
