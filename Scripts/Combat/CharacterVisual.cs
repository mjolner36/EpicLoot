using System.Collections.Generic;
using Godot;
using EpicLoot.Stats;

namespace EpicLoot.Combat;

/// <summary>
/// Визуал сущности: серые примитивы или сцена модели (этап 10), части экипировки,
/// цвет-оверлей статусов (мерцание, вспышки), вспышка при попадании, пауза анимации при заморозке.
/// Иконки и полосы жизни рисуются в экранном UI (EnemyWidgetLayer).
/// </summary>
public partial class CharacterVisual : Node3D
{
	/// <summary>Имена частей совпадают с объектами модели из Blender.</summary>
	public static readonly string[] AllParts = { "SK_Helmet", "SK_Tunic", "SK_Belt", "SK_Boots", "Sword", "Bow" };

	private static readonly Dictionary<Color, StandardMaterial3D> BodyMaterials = new();
	private static CapsuleMesh _bodyMesh;
	private static SphereMesh _headMesh;
	private static BoxMesh _unitBox;
	private static CylinderMesh _unitCylinder;

	private readonly List<GeometryInstance3D> _meshes = new();
	private const string OverlayShaderPath = "res://Assets/Materials/status_overlay.gdshader";
	private static readonly Dictionary<StatusData, ShaderMaterial> StatusOverlays = new();
	private static ShaderMaterial _flashOverlay;

	private Node3D _model;
	private float _flash;
	private StatusData _top;
	private bool _animPaused;
	private Material _currentOverlay;

	public float Height { get; private set; }

	public static CharacterVisual Create(Color body, Color accent, float scale, PackedScene model)
	{
		var v = new CharacterVisual { Name = "Visual" };
		v.Height = 1.8f * scale;
		if (model != null)
		{
			v._model = model.Instantiate<Node3D>();
			v._model.Scale = Vector3.One * scale;
			v.AddChild(v._model);
			v.CollectMeshes(v._model);
		}
		else
		{
			v.BuildPrimitives(body, accent, scale);
		}
		v.SetProcess(false);
		return v;
	}

	private void BuildPrimitives(Color body, Color accent, float scale)
	{
		_bodyMesh ??= new CapsuleMesh { Radius = 0.35f, Height = 1.3f, RadialSegments = 12, Rings = 4 };
		_headMesh ??= new SphereMesh { Radius = 0.25f, Height = 0.5f, RadialSegments = 12, Rings = 6 };
		_unitBox ??= new BoxMesh { Size = Vector3.One };
		_unitCylinder ??= new CylinderMesh { TopRadius = 0.5f, BottomRadius = 0.5f, Height = 1f, RadialSegments = 12 };
		_model = new Node3D { Name = "Model", Scale = Vector3.One * scale };
		AddChild(_model);
		AddMesh(_model, "SK_Body", _bodyMesh, Material(body), new Vector3(0, 0.65f, 0), Vector3.One);
		AddMesh(_model, "Head", _headMesh, Material(body), new Vector3(0, 1.5f, 0), Vector3.One);
		// "Нос" и руны показывают направление взгляда (-Z).
		AddMesh(_model, "Nose", _unitBox, Material(accent), new Vector3(0, 1.5f, -0.3f), new Vector3(0.12f, 0.12f, 0.35f));

		var metal = Material(new Color(0.42f, 0.42f, 0.46f));
		AddMesh(_model, "SK_Helmet", _unitBox, metal, new Vector3(0, 1.68f, 0), new Vector3(0.56f, 0.28f, 0.56f));
		var tunic = AddMesh(_model, "SK_Tunic", _unitCylinder, Material(new Color(0.22f, 0.2f, 0.26f)), new Vector3(0, 0.98f, 0), new Vector3(0.82f, 0.62f, 0.82f));
		AddMesh(tunic, "Runes", _unitBox, Material(accent), new Vector3(0, 0, -0.5f), new Vector3(0.3f, 0.5f, 0.05f));
		AddMesh(_model, "SK_Belt", _unitCylinder, Material(new Color(0.35f, 0.22f, 0.12f)), new Vector3(0, 0.6f, 0), new Vector3(0.78f, 0.1f, 0.78f));
		var boots = new Node3D { Name = "SK_Boots" };
		_model.AddChild(boots);
		var leather = Material(new Color(0.25f, 0.17f, 0.1f));
		AddMesh(boots, "BootL", _unitBox, leather, new Vector3(-0.15f, 0.1f, -0.05f), new Vector3(0.2f, 0.22f, 0.34f));
		AddMesh(boots, "BootR", _unitBox, leather, new Vector3(0.15f, 0.1f, -0.05f), new Vector3(0.2f, 0.22f, 0.34f));
		// Меч в правой руке, лук в левой.
		AddMesh(_model, "Sword", _unitBox, Material(new Color(0.75f, 0.77f, 0.8f)), new Vector3(0.45f, 0.95f, -0.45f), new Vector3(0.08f, 0.08f, 1.0f));
		AddMesh(_model, "Bow", _unitBox, Material(new Color(0.45f, 0.3f, 0.15f)), new Vector3(-0.45f, 1.0f, -0.3f), new Vector3(0.06f, 1.1f, 0.06f));
	}

	private Node3D AddMesh(Node3D parent, string name, Mesh mesh, Material mat, Vector3 pos, Vector3 scale)
	{
		var mi = new MeshInstance3D { Name = name, Mesh = mesh, MaterialOverride = mat, Position = pos, Scale = scale };
		parent.AddChild(mi);
		_meshes.Add(mi);
		return mi;
	}

	private void CollectMeshes(Node node)
	{
		if (node is GeometryInstance3D g) _meshes.Add(g);
		foreach (var child in node.GetChildren()) CollectMeshes(child);
	}

	private static StandardMaterial3D Material(Color c)
	{
		if (!BodyMaterials.TryGetValue(c, out var m))
		{
			m = new StandardMaterial3D { AlbedoColor = c, Roughness = 0.9f };
			BodyMaterials[c] = m;
		}
		return m;
	}

	/// <summary>Включает/выключает часть модели по имени (SK_Helmet, Sword…).</summary>
	public void SetPartVisible(string part, bool visible)
	{
		if (_model?.FindChild(part, true, false) is Node3D n) n.Visible = visible;
	}

	/// <summary>Показывает только перечисленные части экипировки.</summary>
	public void ShowOnlyParts(IEnumerable<string> parts)
	{
		var set = new HashSet<string>(parts);
		foreach (var p in AllParts) SetPartVisible(p, set.Contains(p));
	}

	public bool IsPartVisible(string part) => _model?.FindChild(part, true, false) is Node3D { Visible: true };

	public void FaceDirection(Vector3 dir)
	{
		if (_animPaused) return;
		dir.Y = 0;
		if (dir.LengthSquared() < 0.0001f) return;
		Rotation = new Vector3(0, Mathf.Atan2(-dir.X, -dir.Z), 0);
	}

	public void Flash()
	{
		_flash = 0.08f;
		SetProcess(true);
		ApplyOverlay();
	}

	/// <summary>Обновляет оверлей по самому приоритетному статусу; при заморозке ставит анимацию на паузу.</summary>
	public void ShowStatuses(StatusController status)
	{
		_top = null;
		bool frozen = false;
		foreach (var fx in status.Active)
		{
			if (_top == null || fx.Data.OverlayPriority > _top.OverlayPriority) _top = fx.Data;
			if (fx.Data.Tag == Tags.Frozen) frozen = true;
		}
		SetAnimationPaused(frozen);
		ApplyOverlay();
	}

	/// <summary>Хук для этапа 10: останавливает AnimationPlayer/AnimationTree модели.</summary>
	public void SetAnimationPaused(bool paused)
	{
		if (_animPaused == paused) return;
		_animPaused = paused;
		if (_model == null) return;
		foreach (var node in _model.FindChildren("*", "AnimationPlayer", true, false))
			((AnimationPlayer)node).SpeedScale = paused ? 0f : 1f;
		foreach (var node in _model.FindChildren("*", "AnimationTree", true, false))
			((AnimationTree)node).Active = !paused;
	}

	/// <summary>Кадровая обработка нужна только для таймера вспышки попадания.</summary>
	public override void _Process(double delta)
	{
		_flash -= (float)delta;
		if (_flash > 0f) return;
		SetProcess(false);
		ApplyOverlay();
	}

	public Material CurrentOverlay => _currentOverlay;

	private void ApplyOverlay()
	{
		var overlay = _flash > 0f ? FlashMaterial() : _top != null ? StatusMaterial(_top) : null;
		if (overlay == _currentOverlay) return;
		_currentOverlay = overlay;
		foreach (var m in _meshes) m.MaterialOverlay = overlay;
	}

	private static ShaderMaterial MakeOverlay(Color color, int mode)
	{
		var m = new ShaderMaterial { Shader = GD.Load<Shader>(OverlayShaderPath) };
		m.SetShaderParameter("color", color);
		m.SetShaderParameter("mode", mode);
		return m;
	}

	private static ShaderMaterial FlashMaterial() => _flashOverlay ??= MakeOverlay(new Color(1, 1, 1, 0.7f), 0);

	/// <summary>Один общий материал на статус — все враги с ним рисуются одним батчем.</summary>
	private static ShaderMaterial StatusMaterial(StatusData s)
	{
		if (!StatusOverlays.TryGetValue(s, out var m))
		{
			var c = s.OverlayColor;
			m = MakeOverlay(new Color(c.R, c.G, c.B, 0.5f), s.OverlayEffect);
			StatusOverlays[s] = m;
		}
		return m;
	}
}
