using Godot;
using EpicLoot.Core;

namespace EpicLoot.Player;

/// <summary>Ортографическая изометрическая камера, следует за целью без вращения.</summary>
public partial class IsoCamera : Camera3D
{
	public Node3D Target { get; set; }
	private Vector3 _offset;

	public override void _Ready()
	{
		var cfg = GameState.Instance.Config;
		Projection = ProjectionType.Orthogonal;
		Size = cfg.CameraSize;
		Near = 0.1f;
		Far = 200f;
		RotationDegrees = new Vector3(cfg.CameraPitch, cfg.CameraYaw, 0);
		_offset = GlobalBasis.Z * cfg.CameraDistance;
		Current = true;
		Snap();
	}

	public void Snap()
	{
		if (Target != null) GlobalPosition = Target.GlobalPosition + _offset;
	}

	public override void _Process(double delta)
	{
		if (Target == null || !IsInstanceValid(Target)) return;
		GlobalPosition = GlobalPosition.Lerp(Target.GlobalPosition + _offset, 1f - Mathf.Exp(-12f * (float)delta));
	}

	/// <summary>Точка курсора на плоскости Y=0.</summary>
	public Vector3 MouseOnGround()
	{
		var mouse = GetViewport().GetMousePosition();
		var origin = ProjectRayOrigin(mouse);
		var dir = ProjectRayNormal(mouse);
		if (Mathf.Abs(dir.Y) < 0.0001f) return origin;
		float t = -origin.Y / dir.Y;
		return origin + dir * t;
	}

	/// <summary>Экранные "вперёд" и "вправо" на плоскости XZ.</summary>
	public (Vector3 forward, Vector3 right) ScreenAxes()
	{
		var f = -GlobalBasis.Z;
		f.Y = 0;
		var r = GlobalBasis.X;
		r.Y = 0;
		return (f.Normalized(), r.Normalized());
	}
}
