using System.Collections.Generic;
using System.Linq;
using Godot;
using EpicLoot.Atlas;
using EpicLoot.Core;
using EpicLoot.Enemies;
using EpicLoot.Items;
using EpicLoot.Player;
using EpicLoot.UI;

namespace EpicLoot.Arena;

/// <summary>
/// Корень сцены боя: строит арену, игрока, камеру, HUD и спавнер, лут и подбор,
/// следит за победой и смертью. SandboxMode — полигон с манекенами (без лута).
/// </summary>
public partial class ArenaScene : Node3D
{
	[Export] public bool SandboxMode { get; set; }

	public PlayerController Player { get; private set; }
	public WaveSpawner Spawner { get; private set; }
	public NavigationRegion3D Navigation { get; private set; }
	public AtlasNodeData Node { get; private set; }
	public EnemyWidgetLayer Widgets { get; private set; }
	public RunBag Bag { get; private set; }
	public MapInstance Map { get; private set; }
	public LootSystem Loot => _loot;
	public readonly List<LootDrop> Drops = new();
	/// <summary>Гарантированный лут элиты, ушедший сразу в сумку, и то, что в неё не влезло.</summary>
	public readonly List<ItemInstance> BossOverflow = new();
	public readonly List<MapInstance> BossMapOverflow = new();

	private LootSystem _loot;

	private Hud _hud;
	private ResultScreen _result;
	private bool _finished;
	private float _startDelay = -1f;
	private int _framesUntilStart = 2;
	private Vector3[] _dummySpots = System.Array.Empty<Vector3>();
	private readonly RandomNumberGenerator _rng = new();

	public override void _Ready()
	{
		var gs = GameState.Instance;
		var cfg = gs.Config;
		// Запуск Arena.tscn напрямую из редактора — бесплатная карта T1.
		if (!SandboxMode)
		{
			Map = gs.EnsureMap();
			Node = Map.Node;
		}

		BuildEnvironment();

		float inset = cfg.ArenaSize * 0.5f - cfg.SpawnInset;
		var spawnPoints = new[]
		{
			new Vector3(0, 0, -inset), new Vector3(0, 0, inset),
			new Vector3(-inset, 0, 0), new Vector3(inset, 0, 0),
		};
		_dummySpots = SandboxMode ? new[] { new Vector3(-4, 0, -5), new Vector3(0, 0, -6), new Vector3(4, 0, -5) } : _dummySpots;
		var keepClear = new[] { Vector3.Zero }.Concat(spawnPoints).Concat(_dummySpots).ToArray();
		Navigation = ArenaBuilder.Build(this, cfg.ArenaSize, SandboxMode ? 0 : cfg.ObstacleMin, SandboxMode ? 0 : cfg.ObstacleMax, keepClear, _rng);

		Player = cfg.PlayerScene.Instantiate<PlayerController>();
		AddChild(Player);
		if (!SandboxMode) Player.ApplyPassives(gs.ActivePassives);
		Player.ApplyEquipment(gs.Profile.EquippedItems);

		if (!SandboxMode)
		{
			Bag = gs.EnsureBag();
			_loot = new LootSystem(gs.Items, gs.Generator, new LootContext
			{
				MapTier = Map.Tier,
				Quantity = Map.Quantity,
				RarityBonus = Map.RarityBonus,
				MapNodes = LootSystem.EligibleMapNodes(gs.Atlas, gs.Completed, Map.Tier),
			});
		}

		var camera = new IsoCamera { Name = "Camera", Target = Player };
		AddChild(camera);
		Player.Camera = camera;

		Spawner = new WaveSpawner { Name = "WaveSpawner" };
		AddChild(Spawner);
		Spawner.Init(this, spawnPoints, SandboxMode ? null : Map);

		_hud = new Hud { Name = "HUD" };
		AddChild(_hud);
		var title = SandboxMode ? "Полигон" : $"{Node?.DisplayName} · T{Map.Tier}";
		if (!SandboxMode && Map.Mods.Count > 0) title += "\n" + string.Join(", ", Map.Mods.Select(m => m.DisplayName));
		_hud.Init(Player, Spawner, Bag, title);

		Widgets = new EnemyWidgetLayer { Name = "EnemyWidgets", Camera = camera };
		AddChild(Widgets);

		_result = new ResultScreen { Name = "ResultScreen" };
		AddChild(_result);

		EventBus.Instance.EnemyDied += OnEnemyDied;
		EventBus.Instance.PlayerDied += OnPlayerDied;
	}

	public override void _ExitTree()
	{
		EventBus.Instance.EnemyDied -= OnEnemyDied;
		EventBus.Instance.PlayerDied -= OnPlayerDied;
	}

	private void BuildEnvironment()
	{
		var env = new Godot.Environment
		{
			BackgroundMode = Godot.Environment.BGMode.Color,
			BackgroundColor = new Color(0.08f, 0.08f, 0.1f),
			AmbientLightSource = Godot.Environment.AmbientSource.Color,
			AmbientLightColor = new Color(0.55f, 0.55f, 0.6f),
			AmbientLightEnergy = 0.6f,
			TonemapMode = Godot.Environment.ToneMapper.Filmic,
		};
		AddChild(new WorldEnvironment { Environment = env });
		AddChild(new DirectionalLight3D
		{
			RotationDegrees = new Vector3(-55, 30, 0),
			LightEnergy = 1.1f,
			ShadowEnabled = true,
			DirectionalShadowMaxDistance = 60f,
		});
	}

	public override void _PhysicsProcess(double delta)
	{
		// Навмеш синхронизируется с картой на следующем кадре физики — ждём, потом спавним.
		if (_framesUntilStart > 0)
		{
			if (--_framesUntilStart == 0)
			{
				if (SandboxMode) SpawnDummies();
				else _startDelay = GameState.Instance.Config.FirstWaveDelay;
			}
			return;
		}
		if (_startDelay > 0f)
		{
			_startDelay -= (float)delta;
			if (_startDelay <= 0f) Spawner.Begin();
		}
		if (!_finished && Bag != null) PickupByTouch();
	}

	private void PickupByTouch()
	{
		float r = GameState.Instance.Config.Loot.PickupRadius;
		var p = Player.GlobalPosition;
		for (int i = Drops.Count - 1; i >= 0; i--)
		{
			var d = Drops[i];
			if (!IsInstanceValid(d)) { Drops.RemoveAt(i); continue; }
			var to = d.GlobalPosition - p;
			to.Y = 0;
			if (to.Length() <= r) TryPickup(d);
		}
	}

	/// <summary>Подбор: сферы всегда, предметы — пока сумка не полна.</summary>
	public bool TryPickup(LootDrop d)
	{
		if (d.Currency != null) Bag.AddCurrency(d.Currency.Id);
		else if (d.Map != null ? !Bag.TryAdd(d.Map) : !Bag.TryAdd(d.Item)) return false;
		FloatingText.Spawn(this, d.GlobalPosition + Vector3.Up * 1.5f, "+ " + d.Label, d.Color, 36, 0.8f);
		Drops.Remove(d);
		d.QueueFree();
		return true;
	}

	/// <summary>Клик рядом с лутом подбирает его, если игрок недалеко.</summary>
	private bool TryClickPickup()
	{
		if (Bag == null) return false;
		return TryClickPickupAt(Player.Camera.MouseOnGround());
	}

	/// <summary>Подбор кликом по точке на земле. true — клик ушёл на лут (меч не бьёт).</summary>
	public bool TryClickPickupAt(Vector3 aim)
	{
		if (Bag == null) return false;
		var cfg = GameState.Instance.Config.Loot;
		LootDrop best = null;
		float bestDist = cfg.ClickPickupTolerance;
		foreach (var d in Drops)
		{
			if (!IsInstanceValid(d)) continue;
			float dist = new Vector2(d.GlobalPosition.X - aim.X, d.GlobalPosition.Z - aim.Z).Length();
			if (dist <= bestDist) { best = d; bestDist = dist; }
		}
		if (best == null) return false;
		if (best.GlobalPosition.DistanceTo(Player.GlobalPosition) > cfg.ClickPickupRange)
		{
			FloatingText.Spawn(this, Player.GlobalPosition + Vector3.Up * 2.4f, "Слишком далеко", Colors.Gray, 30, 0.6f);
			return true;
		}
		if (!TryPickup(best))
			FloatingText.Spawn(this, Player.GlobalPosition + Vector3.Up * 2.4f, "Сумка полна", new Color(1f, 0.5f, 0.4f), 34, 0.8f);
		return true;
	}

	public override void _UnhandledInput(InputEvent e)
	{
		if (_finished) return;
		if (e.IsActionPressed(InputSetup.DebugStress))
			Spawner.StressSpawn(GameState.Instance.Config.StressSpawnCount);
		else if (e.IsActionPressed(InputSetup.Abandon))
			Abandon();
		else if (e.IsActionPressed(InputSetup.SkillPrimary) && TryClickPickup())
			Player.SuppressPrimaryUntilRelease = true;
	}

	private void SpawnDummies()
	{
		foreach (var spot in _dummySpots)
			Spawner.Spawn(GameState.Instance.Config.DummyScene, spot);
	}

	private void OnEnemyDied(Node3D enemy)
	{
		if (_finished) return;
		if (SandboxMode)
		{
			// Манекены возрождаются на своём месте.
			if (enemy is EnemyBase e && e.Data.AttackKind == EnemyAttackKind.None)
			{
				var pos = e.GlobalPosition;
				GetTree().CreateTimer(1.0).Timeout += () =>
				{
					if (IsInstanceValid(this)) Spawner.Spawn(GameState.Instance.Config.DummyScene, pos);
				};
			}
			return;
		}
		if (enemy is not EnemyBase dead) return;
		if (enemy != Spawner.Elite)
		{
			if (_loot != null) Drops.AddRange(_loot.RollGround(dead.Data, this, dead.GlobalPosition));
			return;
		}

		// Гарантированный лут элиты/босса — сразу в сумку: после его смерти бой ставится на паузу.
		var cfg = GameState.Instance.Config.Loot;
		bool keystone = Node?.Type == AtlasNodeType.Keystone;
		var bundle = _loot.RollGuaranteed(
			keystone ? cfg.KeystoneItems : dead.Data.GuaranteedItems,
			keystone ? cfg.KeystoneMinRarity : dead.Data.GuaranteedMinRarity,
			keystone ? cfg.KeystoneShards : dead.Data.GuaranteedShards,
			keystone ? cfg.KeystoneGlyphRunes : dead.Data.GuaranteedGlyphRunes,
			keystone ? cfg.KeystoneMaps : dead.Data.GuaranteedMaps);
		foreach (var item in bundle.Items)
			if (!Bag.TryAdd(item)) BossOverflow.Add(item);
		foreach (var map in bundle.Maps)
			if (!Bag.TryAdd(map)) BossMapOverflow.Add(map);
		foreach (var (id, n) in bundle.Currency) Bag.AddCurrency(id, n);

		_finished = true;
		bool firstTime = GameState.Instance.CompleteNode(Node?.Id);
		var (stored, overflow) = GameState.Instance.TransferBag();
		overflow.AddRange(BossOverflow);
		EventBus.Instance.EmitSignal(EventBus.SignalName.ArenaCleared);
		Finish();
		_result.ShowVictory(Node, firstTime, stored, overflow, Bag.Maps, BossMapOverflow, Bag.Currency);
	}

	private void OnPlayerDied()
	{
		if (_finished) return;
		_finished = true;
		Finish();
		_result.ShowDeath(Node, Bag, "Погиб");
	}

	/// <summary>Esc: сдаться. Как смерть — без награды, сумка сгорает.</summary>
	private void Abandon()
	{
		if (SandboxMode)
		{
			GameState.Instance.ReturnToAtlas();
			return;
		}
		_finished = true;
		Finish();
		_result.ShowDeath(Node, Bag, "Забег прерван");
	}

	private void Finish()
	{
		Player.InputEnabled = false;
		GetTree().Paused = true;
	}
}
