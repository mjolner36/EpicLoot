using System.Collections.Generic;
using Godot;

namespace EpicLoot.Combat;

/// <summary>Простые одноразовые эффекты на сером арте: вспышки кругов, конусов, лучей.</summary>
public static class Fx
{
	private static readonly Dictionary<Color, StandardMaterial3D> Materials = new();
	private static readonly Dictionary<int, ArrayMesh> Sectors = new();
	private static CylinderMesh _disc;
	private static BoxMesh _box;

	public static Node Root(Node from) => from.GetTree().CurrentScene ?? from.GetTree().Root;

	public static StandardMaterial3D Unshaded(Color c)
	{
		if (Materials.TryGetValue(c, out var m)) return m;
		m = new StandardMaterial3D
		{
			AlbedoColor = c,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			Transparency = c.A < 1f ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled,
		};
		Materials[c] = m;
		return m;
	}

	public static MeshInstance3D Disc(Node from, Vector3 pos, float radius, Color color, float duration)
	{
		_disc ??= new CylinderMesh { TopRadius = 1f, BottomRadius = 1f, Height = 0.04f, RadialSegments = 32, Rings = 1 };
		var mi = new MeshInstance3D { Mesh = _disc, MaterialOverride = Unshaded(color), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
		Root(from).AddChild(mi);
		mi.GlobalPosition = new Vector3(pos.X, 0.05f, pos.Z);
		mi.Scale = new Vector3(radius, 1f, radius);
		FadeAndFree(mi, duration);
		return mi;
	}

	public static void Cone(Node from, Vector3 origin, Vector3 forward, float radius, float angleDeg, Color color, float duration)
	{
		var mi = new MeshInstance3D { Mesh = Sector(angleDeg), MaterialOverride = Unshaded(color), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
		Root(from).AddChild(mi);
		mi.GlobalPosition = new Vector3(origin.X, 0.06f, origin.Z);
		mi.Scale = new Vector3(radius, 1f, radius);
		forward.Y = 0;
		if (forward.LengthSquared() > 0.0001f)
			mi.Rotation = new Vector3(0, Mathf.Atan2(forward.X, forward.Z), 0);
		FadeAndFree(mi, duration);
	}

	public static void Beam(Node from, Vector3 a, Vector3 b, Color color, float duration, float width = 0.12f)
	{
		_box ??= new BoxMesh { Size = Vector3.One };
		float len = a.DistanceTo(b);
		if (len < 0.01f) return;
		var mi = new MeshInstance3D { Mesh = _box, MaterialOverride = Unshaded(color), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
		Root(from).AddChild(mi);
		mi.GlobalPosition = (a + b) * 0.5f;
		mi.LookAt(b, Mathf.Abs((b - a).Normalized().Dot(Vector3.Up)) > 0.99f ? Vector3.Forward : Vector3.Up);
		mi.Scale = new Vector3(width, width, len);
		FadeAndFree(mi, duration);
	}

	private static void FadeAndFree(Node3D node, float duration)
	{
		var tw = node.CreateTween();
		tw.TweenInterval(duration);
		tw.TweenCallback(Callable.From(node.QueueFree));
	}

	/// <summary>Плоский сектор (вершина в 0, направление +Z), радиус 1.</summary>
	private static ArrayMesh Sector(float angleDeg)
	{
		int key = Mathf.RoundToInt(angleDeg);
		if (Sectors.TryGetValue(key, out var mesh)) return mesh;
		var st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Triangles);
		int segs = 16;
		float half = Mathf.DegToRad(angleDeg * 0.5f);
		for (int i = 0; i < segs; i++)
		{
			float a0 = -half + 2f * half * i / segs;
			float a1 = -half + 2f * half * (i + 1) / segs;
			st.AddVertex(Vector3.Zero);
			st.AddVertex(new Vector3(Mathf.Sin(a0), 0, Mathf.Cos(a0)));
			st.AddVertex(new Vector3(Mathf.Sin(a1), 0, Mathf.Cos(a1)));
		}
		mesh = st.Commit();
		Sectors[key] = mesh;
		return mesh;
	}
}
