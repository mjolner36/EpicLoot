using Godot;
using EpicLoot.Combat;
using EpicLoot.Player;
using EpicLoot.Stats;

namespace EpicLoot.Enemies;

/// <summary>Конечный автомат врага: Idle → Chase → Attack → Cooldown → Chase.</summary>
public partial class EnemyBrain : Node
{
	public enum State
	{
		Idle,
		Chase,
		Attack,
		Cooldown,
	}

	public State Current { get; private set; } = State.Idle;

	private EnemyBase _e;
	private EnemyData _d;
	private float _timer;
	private bool _struck;
	private Telegraph _telegraph;

	public void Init(EnemyBase owner)
	{
		_e = owner;
		_d = owner.Data;
		_timer = 0.3f;
	}

	private float AttackSpeed => Mathf.Max(0.1f, _e.Stats.Get(StatType.AttackSpeed));

	public override void _PhysicsProcess(double delta)
	{
		if (_e.IsDead) return;
		float dt = (float)delta;

		// Заморозка: не двигается и не атакует, таймеры стоят.
		if (_e.Stats.HasTag(Tags.Frozen))
		{
			_e.Stop();
			_telegraph?.Cancel();
			_telegraph = null;
			if (Current == State.Attack) Enter(State.Cooldown, 0.3f);
			return;
		}

		var player = PlayerController.Current;
		bool hasTarget = player != null && !player.IsDead && _d.AttackKind != EnemyAttackKind.None;
		var toPlayer = hasTarget ? player.GlobalPosition - _e.GlobalPosition : Vector3.Zero;
		toPlayer.Y = 0;
		float dist = toPlayer.Length();

		switch (Current)
		{
			case State.Idle:
				_e.Stop();
				_timer -= dt;
				if (hasTarget && _timer <= 0f) Enter(State.Chase);
				break;

			case State.Chase:
				if (!hasTarget)
				{
					Enter(State.Idle, 0.5f);
					break;
				}
				if (Chase(player, toPlayer, dist)) Enter(State.Attack, _d.AttackWindup / AttackSpeed);
				break;

			case State.Attack:
				_e.Stop();
				if (hasTarget) _e.Visual.FaceDirection(toPlayer);
				_timer -= dt;
				if (_timer <= 0f && !_struck)
				{
					_struck = true;
					Strike(player);
				}
				if (_struck && _telegraph == null)
					Enter(State.Cooldown, Mathf.Max(0.1f, (_d.AttackInterval - _d.AttackWindup) / AttackSpeed));
				break;

			case State.Cooldown:
				_timer -= dt;
				// Солдаты продолжают прижиматься к игроку, лучники держат дистанцию.
				if (hasTarget && _d.AttackKind != EnemyAttackKind.Slam) Chase(player, toPlayer, dist);
				else _e.Stop();
				if (_timer <= 0f) Enter(State.Chase);
				break;
		}
	}

	private void Enter(State s, float timer = 0f)
	{
		Current = s;
		_timer = timer;
		_struck = false;
	}

	/// <summary>Двигается к позиции атаки. true — можно атаковать.</summary>
	private bool Chase(PlayerController player, Vector3 toPlayer, float dist)
	{
		if (_d.AttackKind == EnemyAttackKind.Ranged)
		{
			if (dist < _d.PreferredMinRange)
			{
				_e.MoveTo(_e.GlobalPosition - toPlayer.Normalized() * 3f);
				return false;
			}
			if (dist > _d.PreferredMaxRange)
			{
				_e.MoveTo(player.GlobalPosition);
				return false;
			}
			_e.Stop();
			_e.Visual.FaceDirection(toPlayer);
			return Current == State.Chase && dist <= _d.AttackRange;
		}

		if (dist > _d.AttackRange)
		{
			_e.MoveTo(player.GlobalPosition);
			return false;
		}
		_e.Stop();
		return Current == State.Chase;
	}

	private void Strike(PlayerController player)
	{
		if (player == null || player.IsDead) return;
		switch (_d.AttackKind)
		{
			case EnemyAttackKind.Melee:
			{
				var to = player.GlobalPosition - _e.GlobalPosition;
				to.Y = 0;
				Fx.Cone(_e, _e.GlobalPosition, to, _d.AttackRange + 0.3f, 70f, new Color(1f, 0.3f, 0.2f, 0.35f), 0.1f);
				if (to.Length() <= _d.AttackRange + 0.5f) Hit(player);
				break;
			}
			case EnemyAttackKind.Ranged:
			{
				var dir = player.GlobalPosition - _e.GlobalPosition;
				var p = Projectile.Spawn(_e, _e.GlobalPosition + Vector3.Up * 1.2f + dir.Normalized() * 0.6f, dir, _d.ProjectileSpeed * _e.Stats.Get(StatType.ProjectileSpeed),
					_d.AttackRange + 4f, Layers.PlayerHurtbox, new Color(0.55f, 0.4f, 0.25f), 0.8f);
				var stats = _e.Stats;
				var src = (Node3D)_e;
				p.OnHitTarget = target =>
				{
					var info = DamageSystem.BuildRaw(stats, GodotObject.IsInstanceValid(src) ? src : null, DamageType.Physical, _d.AttackDamage, false, "Projectile");
					DamageSystem.Apply(info, target);
					return true;
				};
				break;
			}
			case EnemyAttackKind.Slam:
			{
				var center = _e.GlobalPosition;
				_telegraph = Telegraph.Spawn(_e, center, _d.AoeRadius, _d.TelegraphTime / AttackSpeed, () =>
				{
					_telegraph = null;
					if (!GodotObject.IsInstanceValid(_e) || _e.IsDead) return;
					Fx.Disc(_e, center, _d.AoeRadius, new Color(1f, 0.35f, 0.1f, 0.6f), 0.15f);
					var pl = PlayerController.Current;
					if (pl == null || pl.IsDead) return;
					var d = pl.GlobalPosition - center;
					d.Y = 0;
					if (d.Length() <= _d.AoeRadius + 0.3f) Hit(pl);
				});
				break;
			}
		}
	}

	private void Hit(ICombatant target)
	{
		var info = DamageSystem.BuildRaw(_e.Stats, _e, DamageType.Physical, _d.AttackDamage, false, "Melee");
		DamageSystem.Apply(info, target);
	}

	public void OnOwnerDied()
	{
		_telegraph?.Cancel();
		_telegraph = null;
	}
}
