using System.Collections.Generic;
using Godot;
using EpicLoot.Core;
using EpicLoot.Enemies;

namespace EpicLoot.UI;

/// <summary>
/// Один экранный слой для полос жизни и статусов всех врагов.
/// Позиции через Camera3D.UnprojectPosition, виджеты из пула: берутся при спавне, возвращаются при смерти.
/// </summary>
public partial class EnemyWidgetLayer : CanvasLayer
{
	private Control _root;
	private readonly Stack<EnemyWidget> _pool = new();
	private readonly Dictionary<EnemyBase, EnemyWidget> _active = new();

	public Camera3D Camera { get; set; }
	public int ActiveCount => _active.Count;
	public int PooledCount => _pool.Count;

	public EnemyWidget WidgetOf(EnemyBase e) => _active.GetValueOrDefault(e);

	public override void _Ready()
	{
		Layer = 1;
		// После камеры (она сглаживает позицию в _Process), чтобы виджеты не отставали на кадр.
		ProcessPriority = 100;
		_root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
		_root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(_root);
		EventBus.Instance.EnemySpawned += OnSpawned;
		EventBus.Instance.EnemyDied += OnDied;
	}

	public override void _ExitTree()
	{
		EventBus.Instance.EnemySpawned -= OnSpawned;
		EventBus.Instance.EnemyDied -= OnDied;
	}

	private void OnSpawned(Node3D node)
	{
		if (node is not EnemyBase e || _active.ContainsKey(e)) return;
		if (!_pool.TryPop(out var w))
		{
			w = new EnemyWidget();
			_root.AddChild(w);
		}
		w.Bind(e);
		_active[e] = w;
	}

	private void OnDied(Node3D node)
	{
		if (node is not EnemyBase e || !_active.Remove(e, out var w)) return;
		w.Unbind();
		_pool.Push(w);
	}

	public override void _Process(double delta)
	{
		if (Camera == null || !IsInstanceValid(Camera)) return;
		var screen = GetViewport().GetVisibleRect();
		List<EnemyBase> gone = null;
		foreach (var (e, w) in _active)
		{
			if (!IsInstanceValid(e))
			{
				(gone ??= new()).Add(e);
				continue;
			}
			w.UpdateFrom(Camera, screen);
		}
		if (gone != null)
			foreach (var e in gone) OnDied(e);
	}
}
