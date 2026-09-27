using System.Collections.Generic;
using Godot;
using EpicLoot.Combat;
using EpicLoot.Core;
using EpicLoot.Stats;

namespace EpicLoot.Enemies;

/// <summary>Враг: тело + компоненты. Поведение — в EnemyBrain, числа — в EnemyData.</summary>
public partial class EnemyBase : CharacterBody3D, ICombatant
{
	public static readonly List<EnemyBase> Alive = new();

	[Export] public EnemyData Data { get; set; }

	public StatBlock Stats { get; private set; }
	public StatusController Status { get; private set; }
	public NavigationAgent3D Agent { get; private set; }
	public CharacterVisual Visual { get; private set; }
	public EnemyBrain Brain { get; private set; }
	public Node3D Body => this;
	public bool IsPlayer => false;
	public bool IsDead => Stats == null || Stats.IsDead;
	public bool IsElite => Stats != null && Stats.HasTag(Tags.Elite);
	/// <summary>Получал ли урон (полоса жизни показывается после первого попадания).</summary>
	public bool WasHit { get; private set; }

	private Hurtbox _hurtbox;
	private float _repathTimer;
	private Vector3 _moveTarget;
	private static readonly RandomNumberGenerator Rng = new();

	public override void _Ready()
	{
		float scale = Data.Scale;
		MotionMode = MotionModeEnum.Floating;
		CollisionLayer = Layers.EnemyBody;
		CollisionMask = Layers.World | Layers.PlayerBody | Layers.EnemyBody;
		AddChild(new CollisionShape3D
		{
			Shape = new CapsuleShape3D { Radius = 0.4f * scale, Height = 1.8f * scale },
			Position = new Vector3(0, 0.9f * scale, 0),
		});

		Stats = new StatBlock { Name = "StatBlock" };
		AddChild(Stats);
		Stats.SetBase(StatType.MaxLife, Data.MaxLife);
		Stats.SetBase(StatType.MoveSpeed, Data.MoveSpeed);
		PlayerData.ApplyBaseStats(Data.ExtraStats, Stats);
		foreach (var tag in Data.Tags ?? System.Array.Empty<string>()) Stats.AddTag(tag, "data");
		Stats.ResetLife();
		Stats.Died += OnDied;

		Status = new StatusController { Name = "StatusController" };
		AddChild(Status);
		Status.Init(this, Stats);

		_hurtbox = Hurtbox.Create(this, 0.5f * scale, 1.8f * scale, false);
		AddChild(_hurtbox);

		Agent = new NavigationAgent3D
		{
			Name = "NavigationAgent3D",
			PathDesiredDistance = 0.6f,
			TargetDesiredDistance = 0.6f,
			Radius = 0.5f,
			AvoidanceEnabled = false,
		};
		AddChild(Agent);

		Visual = CharacterVisual.Create(Data.BodyColor, Data.AccentColor, scale, Data.VisualScene);
		AddChild(Visual);
		Visual.ShowOnlyParts(Data.VisibleParts ?? System.Array.Empty<string>());
		Status.StatusesChanged += () => Visual.ShowStatuses(Status);

		Brain = new EnemyBrain { Name = "EnemyBrain" };
		AddChild(Brain);
		Brain.Init(this);

		_repathTimer = Rng.RandfRange(0f, 0.3f);
		Alive.Add(this);
	}

	public override void _ExitTree() => Alive.Remove(this);

	/// <summary>Модификаторы локации, сложности и т.п. Жизнь пересчитывается до полной.</summary>
	public void ApplyModifiers(IEnumerable<Modifier> mods, string source)
	{
		Stats.AddModifiers(mods, source);
		Stats.ResetLife();
	}

	public void OnHit(float amount, bool crit, bool isTick = false)
	{
		WasHit = true;
		if (!isTick) Visual.Flash();
	}

	private void OnDied()
	{
		Alive.Remove(this);
		_hurtbox.Disable();
		CollisionLayer = 0;
		Status.OnOwnerDied();
		Brain.OnOwnerDied();
		EventBus.Instance.EmitSignal(EventBus.SignalName.EnemyDied, this);
		SetPhysicsProcess(false);
		var tw = CreateTween();
		tw.TweenProperty(Visual, "scale", new Vector3(1.2f, 0.05f, 1.2f), 0.2f);
		tw.TweenCallback(Callable.From(QueueFree));
	}

	/// <summary>Движение к точке по навмешу (путь обновляется с троттлингом).</summary>
	public void MoveTo(Vector3 target, float speedMultiplier = 1f)
	{
		_repathTimer -= (float)GetPhysicsProcessDeltaTime();
		if (_repathTimer <= 0f || _moveTarget.DistanceSquaredTo(target) > 4f)
		{
			_repathTimer = 0.25f;
			_moveTarget = target;
			Agent.TargetPosition = target;
		}

		var next = Agent.GetNextPathPosition();
		var dir = next - GlobalPosition;
		dir.Y = 0;
		// Навмеш ещё не готов или путь пуст — идём напрямую.
		if (dir.LengthSquared() < 0.01f)
		{
			dir = target - GlobalPosition;
			dir.Y = 0;
		}
		Move(dir, speedMultiplier);
	}

	public void Move(Vector3 dir, float speedMultiplier = 1f)
	{
		dir.Y = 0;
		if (dir.LengthSquared() > 0.0001f)
		{
			dir = dir.Normalized();
			Visual.FaceDirection(dir);
		}
		Velocity = dir * Stats.Get(StatType.MoveSpeed) * speedMultiplier;
		MoveAndSlide();
		if (GlobalPosition.Y != 0f) GlobalPosition = new Vector3(GlobalPosition.X, 0f, GlobalPosition.Z);
	}

	public void Stop()
	{
		Velocity = Vector3.Zero;
	}
}
