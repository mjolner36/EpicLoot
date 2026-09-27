using System.Collections.Generic;
using Godot;
using EpicLoot.Combat;
using EpicLoot.Core;
using EpicLoot.Skills;

namespace EpicLoot.Stats;

/// <summary>Держит активные статусы сущности: длительность, тики, стаки, модификаторы.</summary>
public partial class StatusController : Node
{
	[Signal] public delegate void StatusesChangedEventHandler();

	private readonly Dictionary<string, StatusEffect> _active = new();
	private StatBlock _stats;
	private ICombatant _owner;

	public IEnumerable<StatusEffect> Active => _active.Values;

	public void Init(ICombatant owner, StatBlock stats)
	{
		_owner = owner;
		_stats = stats;
	}

	public StatusEffect Get(string id) => _active.GetValueOrDefault(id);

	public bool Has(string id) => _active.ContainsKey(id);

	/// <summary>Накладывает статус. hitDamage нужен для урона в секунду (Поджог).</summary>
	public void Apply(StatusData data, StatBlock source, float hitDamage = 0f, int stacks = 1)
	{
		if (data == null || _stats == null || _stats.IsDead) return;

		float duration = data.Duration;
		if (source != null)
		{
			duration *= source.Get(StatType.AilmentDuration);
			if (data.DurationStat != StatType.None) duration *= source.Get(data.DurationStat);
		}

		if (!_active.TryGetValue(data.Id, out var fx))
		{
			fx = new StatusEffect { Data = data, SourceStats = source };
			_active[data.Id] = fx;
			_stats.AddModifiers(data.Modifiers, fx.SourceId);
			if (!string.IsNullOrEmpty(data.Tag)) _stats.AddTag(data.Tag, fx.SourceId);
			fx.TickTimer = data.TickInterval;
			if (!string.IsNullOrEmpty(data.ApplyText) && _owner?.Body != null)
				UI.FloatingText.Spawn(_owner.Body, _owner.Body.GlobalPosition + Vector3.Up * 2.6f, data.ApplyText, data.OverlayColor, 44, 0.8f);
		}

		// Повторное наложение обновляет длительность.
		if (duration >= fx.Remaining)
		{
			fx.Remaining = duration;
			fx.Duration = duration;
		}
		fx.SourceStats = source ?? fx.SourceStats;
		fx.Stacks = data.MaxStacks > 1 ? Mathf.Min(fx.Stacks + stacks, data.MaxStacks) : 1;
		if (data.DamagePerSecondFraction > 0f)
			fx.DamagePerSecond = Mathf.Max(fx.DamagePerSecond, hitDamage * data.DamagePerSecondFraction);

		EmitSignal(SignalName.StatusesChanged);
	}

	public void Remove(string id)
	{
		if (!_active.Remove(id, out var fx)) return;
		_stats.RemoveBySource(fx.SourceId);
		EmitSignal(SignalName.StatusesChanged);
	}

	public void Clear()
	{
		foreach (var id in new List<string>(_active.Keys)) Remove(id);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_active.Count == 0 || _stats == null || _stats.IsDead) return;
		float dt = (float)delta;
		List<string> expired = null;
		foreach (var fx in _active.Values)
		{
			if (fx.DamagePerSecond > 0f)
			{
				fx.TickTimer -= dt;
				if (fx.TickTimer <= 0f)
				{
					fx.TickTimer += fx.Data.TickInterval;
					fx.PendingNumber += DamageSystem.ApplyTick(_owner, fx.DamagePerSecond * fx.Data.TickInterval);
					fx.NumberTimer += fx.Data.TickInterval;
					// Мелкое число стихии раз в секунду (и при смерти), чтобы не путать с ударами и не спамить.
					if (fx.NumberTimer >= 1f || _stats.IsDead)
					{
						UI.FloatingText.Damage(_owner.Body, fx.PendingNumber, DamageInfo.ColorOf(fx.Data.DamageType), false, small: true);
						fx.PendingNumber = 0f;
						fx.NumberTimer = 0f;
					}
					if (_stats.IsDead) return;
				}
			}
			fx.Remaining -= dt;
			if (fx.Remaining <= 0f) (expired ??= new()).Add(fx.Data.Id);
		}
		if (expired != null)
			foreach (var id in expired) Remove(id);
	}

	/// <summary>Вызывается при смерти владельца: эффекты "при смерти" (распространение Поджога).</summary>
	public void OnOwnerDied()
	{
		var ignite = Get(GameState.Instance.Config.Ignite.Id);
		if (ignite?.SourceStats != null && IsInstanceValid(ignite.SourceStats))
		{
			float radius = ignite.SourceStats.Get(StatType.IgniteSpreadRadius);
			if (radius > 0f && _owner?.Body != null)
				PassiveProcs.SpreadIgnite(_owner, ignite, radius);
		}
		_active.Clear();
	}
}
