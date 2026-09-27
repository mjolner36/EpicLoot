using Godot;

namespace EpicLoot.Core;

/// <summary>Глобальные игровые события (autoload).</summary>
public partial class EventBus : Node
{
	public static EventBus Instance { get; private set; }

	[Signal] public delegate void WaveStartedEventHandler(int wave, int totalWaves);
	[Signal] public delegate void EnemySpawnedEventHandler(Node3D enemy);
	[Signal] public delegate void EnemyDiedEventHandler(Node3D enemy);
	[Signal] public delegate void EliteSpawnedEventHandler(Node3D enemy);
	[Signal] public delegate void ArenaClearedEventHandler();
	[Signal] public delegate void PlayerDiedEventHandler();
	[Signal] public delegate void AtlasChangedEventHandler();

	public override void _EnterTree() => Instance = this;
}
