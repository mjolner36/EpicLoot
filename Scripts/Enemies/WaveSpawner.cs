using System.Collections.Generic;
using System.Linq;
using Godot;
using EpicLoot.Atlas;
using EpicLoot.Core;
using EpicLoot.Items;
using EpicLoot.Stats;

namespace EpicLoot.Enemies;

/// <summary>
/// Волны: WaveSizes из конфига, следующая стартует при живых &lt; NextWaveThreshold,
/// после последней — элитный рыцарь. Keystone: сразу усиленный рыцарь с эскортом.
/// </summary>
public partial class WaveSpawner : Node
{
	public int Wave { get; private set; }
	public int TotalWaves { get; private set; }
	public EnemyBase Elite { get; private set; }
	public bool Running { get; private set; }

	private GameConfig _cfg;
	private Node3D _parent;
	private Vector3[] _spawnPoints;
	private readonly Queue<PackedScene> _queue = new();
	private readonly List<(IEnumerable<Modifier> mods, string source)> _enemyMods = new();
	private float _countMultiplier = 1f;
	private int _eliteEscort;
	private bool _keystone;
	private bool _eliteQueued;
	private float _spawnTimer;
	private readonly RandomNumberGenerator _rng = new();

	public float ArcherShare { get; private set; }

	/// <summary>map = null — полигон (без сложности и модов).</summary>
	public void Init(Node3D parent, Vector3[] spawnPoints, MapInstance map)
	{
		_cfg = GameState.Instance.Config;
		_parent = parent;
		_spawnPoints = spawnPoints;
		TotalWaves = _cfg.WaveSizes.Length;
		ArcherShare = _cfg.ArcherShare;
		if (map == null) return;

		// Сложность: +X% жизни и урона врагов за каждый тир выше T1.
		int steps = map.Tier - 1;
		if (steps > 0)
		{
			float inc = _cfg.DifficultyPerStep * steps;
			_enemyMods.Add((new[]
			{
				new Modifier(StatType.MaxLife, ModifierType.Increased, inc),
				new Modifier(StatType.Damage, ModifierType.Increased, inc),
			}, "difficulty"));
		}

		foreach (var mod in map.Mods)
		{
			_enemyMods.Add((mod.EnemyModifiers, "location"));
			_countMultiplier *= mod.EnemyCountMultiplier;
			_eliteEscort += mod.EliteEscort;
			ArcherShare *= mod.ArcherShareMultiplier;
		}
		ArcherShare = Mathf.Clamp(ArcherShare, 0f, 1f);
		_keystone = map.Node?.Type == AtlasNodeType.Keystone;
	}

	public void Begin()
	{
		Running = true;
		if (_keystone)
		{
			TotalWaves = 0;
			QueueElite(_cfg.KeystoneEscort);
		}
		else
		{
			StartWave(0);
		}
	}

	private void StartWave(int index)
	{
		Wave = index + 1;
		int count = Mathf.RoundToInt(_cfg.WaveSizes[index] * _countMultiplier);
		int archers = Mathf.RoundToInt(count * ArcherShare);
		var list = new List<PackedScene>();
		for (int i = 0; i < count; i++) list.Add(i < archers ? _cfg.ArcherScene : _cfg.SoldierScene);
		foreach (var s in list.OrderBy(_ => _rng.Randi())) _queue.Enqueue(s);
		EventBus.Instance.EmitSignal(EventBus.SignalName.WaveStarted, Wave, TotalWaves);
	}

	/// <summary>Дебаг/тесты: сразу вызвать элиту.</summary>
	public void ForceElite()
	{
		Running = true;
		_queue.Clear();
		if (!_eliteQueued) QueueElite(0);
	}

	private void QueueElite(int extraEscort)
	{
		_eliteQueued = true;
		_queue.Enqueue(_cfg.KnightScene);
		for (int i = 0; i < _eliteEscort + extraEscort; i++) _queue.Enqueue(_cfg.SoldierScene);
		EventBus.Instance.EmitSignal(EventBus.SignalName.WaveStarted, TotalWaves + 1, TotalWaves);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!Running) return;

		if (_queue.Count > 0)
		{
			_spawnTimer -= (float)delta;
			if (_spawnTimer <= 0f)
			{
				_spawnTimer = _cfg.SpawnInterval;
				Spawn(_queue.Dequeue());
			}
			return;
		}

		if (_eliteQueued) return;
		if (EnemyBase.Alive.Count >= _cfg.NextWaveThreshold) return;
		if (Wave < TotalWaves) StartWave(Wave);
		else QueueElite(0);
	}

	public EnemyBase Spawn(PackedScene scene, Vector3? at = null)
	{
		var enemy = scene.Instantiate<EnemyBase>();
		var pos = at ?? _spawnPoints[_rng.RandiRange(0, _spawnPoints.Length - 1)] + new Vector3(_rng.RandfRange(-2f, 2f), 0, _rng.RandfRange(-2f, 2f));
		enemy.Position = pos;
		_parent.AddChild(enemy);
		foreach (var (mods, source) in _enemyMods) enemy.ApplyModifiers(mods, source);

		if (scene == _cfg.KnightScene && Elite == null && _eliteQueued)
		{
			Elite = enemy;
			if (_keystone)
				enemy.ApplyModifiers(new[] { new Modifier(StatType.MaxLife, ModifierType.More, _cfg.KeystoneLifeMultiplier - 1f) }, "keystone");
			EventBus.Instance.EmitSignal(EventBus.SignalName.EliteSpawned, enemy);
		}
		EventBus.Instance.EmitSignal(EventBus.SignalName.EnemySpawned, enemy);
		return enemy;
	}

	/// <summary>Дебаг: сразу N врагов для проверки FPS.</summary>
	public void StressSpawn(int count)
	{
		for (int i = 0; i < count; i++)
			Spawn(i % 3 == 0 ? _cfg.ArcherScene : _cfg.SoldierScene);
	}
}
