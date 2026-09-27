using Godot;
using EpicLoot.Combat;
using EpicLoot.Skills;
using EpicLoot.Stats;

namespace EpicLoot.Player;

/// <summary>Рывок с зарядами и неуязвимостью. Заряды = стат DashCharges.</summary>
public partial class Dash : Node
{
	private const string Source = "dash";

	private SkillData _data;
	private ICombatant _owner;
	private CharacterBody3D _body;
	private float _timer;
	private float _recharge;
	private Vector3 _velocity;
	private uint _savedMask;

	public int Charges { get; private set; }
	public int MaxCharges => Mathf.Max(1, Mathf.RoundToInt(_owner.Stats.Get(StatType.DashCharges)));
	public bool IsDashing => _timer > 0f;
	public Vector3 Velocity => _velocity;
	public float RechargeFraction => _data == null || _data.Cooldown <= 0f ? 0f : _recharge / _data.Cooldown;
	public SkillData Data => _data;

	public void Init(ICombatant owner, CharacterBody3D body, SkillData data)
	{
		_owner = owner;
		_body = body;
		_data = data;
		Charges = MaxCharges;
		_knownMax = MaxCharges;
		owner.Stats.StatChanged += OnStatChanged;
	}

	private int _knownMax;

	/// <summary>Новый заряд от пассивки доступен сразу, потерянный — снимается.</summary>
	private void OnStatChanged(StatType stat, float value)
	{
		if (stat != StatType.DashCharges) return;
		int max = MaxCharges;
		Charges = Mathf.Clamp(Charges + max - _knownMax, 0, max);
		_knownMax = max;
	}

	public bool TryDash(Vector3 direction)
	{
		if (_data == null || IsDashing || Charges <= 0) return false;
		direction.Y = 0;
		if (direction.LengthSquared() < 0.0001f) return false;
		Charges--;
		if (_recharge <= 0f) _recharge = _data.Cooldown;
		_timer = _data.DashDuration;
		_velocity = direction.Normalized() * (_data.DashDistance / _data.DashDuration);
		_owner.Stats.AddTag(Tags.Dashing, Source);
		_owner.Stats.AddTag(Tags.Invulnerable, Source);
		// Проходим сквозь врагов, но не сквозь стены.
		_savedMask = _body.CollisionMask;
		_body.CollisionMask = Layers.World;
		return true;
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		int max = MaxCharges;
		if (Charges > max) Charges = max;
		if (Charges < max)
		{
			if (_recharge <= 0f) _recharge = _data.Cooldown;
			_recharge -= dt;
			if (_recharge <= 0f)
			{
				Charges++;
				_recharge = Charges < max ? _data.Cooldown : 0f;
			}
		}

		if (_timer > 0f)
		{
			_timer -= dt;
			if (_timer <= 0.0001f) End(); // допуск на погрешность float, чтобы рывок длился ровно N кадров
		}
	}

	private void End()
	{
		_timer = 0f;
		_velocity = Vector3.Zero;
		_owner.Stats.RemoveBySource(Source);
		_body.CollisionMask = _savedMask;
	}
}
