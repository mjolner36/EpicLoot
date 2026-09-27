using System.Collections.Generic;
using Godot;
using EpicLoot.Atlas;
using EpicLoot.Combat;
using EpicLoot.Core;
using EpicLoot.Stats;

namespace EpicLoot.Player;

/// <summary>Игрок: собирается из компонентов (StatBlock, StatusController, Hurtbox, SkillCaster, Dash, Visual).</summary>
public partial class PlayerController : CharacterBody3D, ICombatant
{
	public static PlayerController Current { get; private set; }

	public StatBlock Stats { get; private set; }
	public StatusController Status { get; private set; }
	public SkillCaster Skills { get; private set; }
	public Dash Dash { get; private set; }
	public CharacterVisual Visual { get; private set; }
	public Node3D Body => this;
	public bool IsPlayer => true;
	public bool IsDead => Stats == null || Stats.IsDead;

	public IsoCamera Camera { get; set; }
	public Vector3 AimPoint { get; private set; }
	/// <summary>Отключает ввод (экран результата, дебаг).</summary>
	public bool InputEnabled { get; set; } = true;
	/// <summary>Клик ушёл на подбор лута — ЛКМ не бьёт, пока кнопку не отпустят.</summary>
	public bool SuppressPrimaryUntilRelease { get; set; }

	public override void _Ready()
	{
		Current = this;
		var data = GameState.Instance.Config.Player;

		MotionMode = MotionModeEnum.Floating;
		CollisionLayer = Layers.PlayerBody;
		CollisionMask = Layers.World | Layers.EnemyBody;
		AddChild(new CollisionShape3D
		{
			Shape = new CapsuleShape3D { Radius = 0.4f, Height = 1.8f },
			Position = new Vector3(0, 0.9f, 0),
		});

		Stats = new StatBlock { Name = "StatBlock" };
		AddChild(Stats);
		data.ApplyTo(Stats);
		Stats.ResetLife();
		Stats.Died += OnDied;

		Status = new StatusController { Name = "StatusController" };
		AddChild(Status);
		Status.Init(this, Stats);

		AddChild(Hurtbox.Create(this, 0.45f, 1.8f, true));

		Skills = new SkillCaster { Name = "SkillCaster" };
		AddChild(Skills);
		Skills.Init(this, data.Skills);

		Dash = new Dash { Name = "Dash" };
		AddChild(Dash);
		Dash.Init(this, this, data.Dash);

		Visual = CharacterVisual.Create(data.BodyColor, data.AccentColor, 1f, data.VisualScene);
		AddChild(Visual);
		Visual.ShowOnlyParts(System.Array.Empty<string>());
		Status.StatusesChanged += () => Visual.ShowStatuses(Status);
	}

	public override void _ExitTree()
	{
		if (Current == this) Current = null;
	}

	/// <summary>Навешивает пассивки пройденных узлов атласа.</summary>
	public void ApplyPassives(IEnumerable<AtlasNodeData> nodes)
	{
		foreach (var node in nodes)
		{
			var source = "passive:" + node.Id;
			Stats.AddModifiers(node.PassiveModifiers, source);
			foreach (var tag in node.GrantedTags ?? System.Array.Empty<string>())
				Stats.AddTag(tag, source);
		}
		Stats.ResetLife();
	}

	/// <summary>Надевает экипировку: модификаторы с SourceId предмета и видимые части модели.</summary>
	public void ApplyEquipment(IEnumerable<Items.ItemInstance> items)
	{
		var parts = new List<string>();
		foreach (var item in items)
		{
			Stats.AddModifiers(item.Modifiers(), item.SourceId);
			if (!string.IsNullOrEmpty(item.Base.ModelPart)) parts.Add(item.Base.ModelPart);
		}
		Visual.ShowOnlyParts(parts);
		Stats.ResetLife();
	}

	public void OnHit(float amount, bool crit, bool isTick = false)
	{
		if (!isTick) Visual.Flash();
	}

	private void OnDied()
	{
		Velocity = Vector3.Zero;
		Visual.RotationDegrees = new Vector3(-80, Visual.RotationDegrees.Y, 0);
		EventBus.Instance.EmitSignal(EventBus.SignalName.PlayerDied);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsDead || Camera == null) return;

		AimPoint = Camera.MouseOnGround();
		var toAim = AimPoint - GlobalPosition;
		Visual.FaceDirection(toAim);

		var (forward, right) = Camera.ScreenAxes();
		var input = InputEnabled ? Input.GetVector(InputSetup.MoveLeft, InputSetup.MoveRight, InputSetup.MoveDown, InputSetup.MoveUp) : Vector2.Zero;
		var moveDir = right * input.X + forward * input.Y;
		if (moveDir.LengthSquared() > 1f) moveDir = moveDir.Normalized();

		if (InputEnabled)
		{
			if (Input.IsActionJustPressed(InputSetup.Dash))
				Dash.TryDash(moveDir.LengthSquared() > 0.01f ? moveDir : toAim);

			if (SuppressPrimaryUntilRelease && !Input.IsActionPressed(InputSetup.SkillPrimary))
				SuppressPrimaryUntilRelease = false;
			for (int i = 0; i < InputSetup.SkillActions.Length; i++)
			{
				if (i == 0 && SuppressPrimaryUntilRelease) continue;
				if (Input.IsActionPressed(InputSetup.SkillActions[i]) && Skills.TryCast(i, AimPoint))
					break;
			}
		}

		if (Dash.IsDashing)
		{
			Velocity = Dash.Velocity;
		}
		else
		{
			float speed = Stats.Get(StatType.MoveSpeed);
			if (Skills.IsCasting) speed *= Skills.CastMoveMultiplier;
			if (Stats.HasTag(Tags.Frozen)) speed = 0f;
			Velocity = moveDir * speed;
		}
		MoveAndSlide();
		if (GlobalPosition.Y != 0f) GlobalPosition = new Vector3(GlobalPosition.X, 0f, GlobalPosition.Z);
	}
}
