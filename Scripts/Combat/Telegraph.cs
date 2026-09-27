using System;
using Godot;

namespace EpicLoot.Combat;

/// <summary>Круг-предупреждение на земле: контур + заполняющийся диск, по окончании вызывает удар.</summary>
public partial class Telegraph : Node3D
{
	private static CylinderMesh _disc;

	private float _duration;
	private float _time;
	private MeshInstance3D _fill;
	private Action _onFire;

	public static Telegraph Spawn(Node from, Vector3 center, float radius, float duration, Action onFire)
	{
		_disc ??= new CylinderMesh { TopRadius = 1f, BottomRadius = 1f, Height = 0.02f, RadialSegments = 40, Rings = 1 };
		var t = new Telegraph { _duration = duration, _onFire = onFire };
		var outer = new MeshInstance3D { Mesh = _disc, MaterialOverride = Fx.Unshaded(new Color(1f, 0.1f, 0.05f, 0.25f)), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
		t._fill = new MeshInstance3D { Mesh = _disc, MaterialOverride = Fx.Unshaded(new Color(1f, 0.2f, 0.05f, 0.45f)), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Position = new Vector3(0, 0.01f, 0), Scale = new Vector3(0.01f, 1, 0.01f) };
		t.AddChild(outer);
		t.AddChild(t._fill);
		Fx.Root(from).AddChild(t);
		t.GlobalPosition = new Vector3(center.X, 0.04f, center.Z);
		t.Scale = new Vector3(radius, 1f, radius);
		return t;
	}

	public void Cancel()
	{
		_onFire = null;
		QueueFree();
	}

	public override void _PhysicsProcess(double delta)
	{
		_time += (float)delta;
		float k = Mathf.Clamp(_time / _duration, 0.01f, 1f);
		_fill.Scale = new Vector3(k, 1, k);
		if (_time >= _duration)
		{
			_onFire?.Invoke();
			_onFire = null;
			QueueFree();
		}
	}
}
