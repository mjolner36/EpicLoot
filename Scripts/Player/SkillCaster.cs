using Godot;
using EpicLoot.Combat;
using EpicLoot.Skills;
using EpicLoot.Stats;

namespace EpicLoot.Player;

/// <summary>Слоты умений, кулдауны, каст. Также считает удары для Грозы.</summary>
public partial class SkillCaster : Node
{
	public SkillData[] Slots { get; private set; } = System.Array.Empty<SkillData>();
	private float[] _cooldowns = System.Array.Empty<float>();
	private float[] _cooldownTotals = System.Array.Empty<float>();
	private ICombatant _owner;
	private int _hitCounter;

	public float CastTimer { get; private set; }
	public float CastMoveMultiplier { get; private set; } = 1f;
	public bool IsCasting => CastTimer > 0f;

	public void Init(ICombatant owner, SkillData[] skills)
	{
		_owner = owner;
		Slots = skills ?? System.Array.Empty<SkillData>();
		_cooldowns = new float[Slots.Length];
		_cooldownTotals = new float[Slots.Length];
	}

	public float CooldownRemaining(int slot) => slot < _cooldowns.Length ? _cooldowns[slot] : 0f;

	public float CooldownFraction(int slot) =>
		slot < _cooldowns.Length && _cooldownTotals[slot] > 0f ? _cooldowns[slot] / _cooldownTotals[slot] : 0f;

	public float EffectiveCooldown(SkillData skill)
	{
		var stats = _owner.Stats;
		float cd = skill.Cooldown;
		if (skill.CooldownStat != StatType.None) cd *= stats.Get(skill.CooldownStat);
		return cd / Mathf.Max(0.1f, stats.Get(StatType.AttackSpeed));
	}

	public bool TryCast(int slot, Vector3 aimPoint)
	{
		if (slot >= Slots.Length || Slots[slot] == null || _cooldowns[slot] > 0f || IsCasting) return false;
		var skill = Slots[slot];
		if (!SkillExecutor.Execute(skill, _owner, aimPoint)) return false;

		float cd = EffectiveCooldown(skill);
		_cooldowns[slot] = cd;
		_cooldownTotals[slot] = cd;
		CastTimer = skill.CastTime / Mathf.Max(0.1f, _owner.Stats.Get(StatType.AttackSpeed));
		CastMoveMultiplier = skill.CastMoveMultiplier;

		int every = Mathf.RoundToInt(_owner.Stats.Get(StatType.StormEveryN));
		if (every > 0 && ++_hitCounter >= every)
		{
			_hitCounter = 0;
			PassiveProcs.Storm(_owner);
		}
		return true;
	}

	/// <summary>Сброс кулдаунов и каста (дебаг и тесты).</summary>
	public void ResetCooldowns()
	{
		System.Array.Clear(_cooldowns);
		CastTimer = 0f;
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		for (int i = 0; i < _cooldowns.Length; i++)
			if (_cooldowns[i] > 0f) _cooldowns[i] = Mathf.Max(0f, _cooldowns[i] - dt);
		if (CastTimer > 0f) CastTimer = Mathf.Max(0f, CastTimer - dt);
	}
}
