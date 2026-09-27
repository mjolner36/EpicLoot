using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using EpicLoot.Atlas;
using EpicLoot.Core;

namespace EpicLoot.UI;

/// <summary>Граф атласа: узлы-кнопки по позициям из данных, связи через _Draw().</summary>
public partial class AtlasView : Control
{
	public static readonly Vector2 DesignSize = new(1200, 800);

	public event Action<AtlasNodeData> NodeClicked;

	private readonly Dictionary<string, Button> _buttons = new();

	public static Color BranchColor(AtlasBranch b) => b switch
	{
		AtlasBranch.Fire => new Color(1f, 0.5f, 0.15f),
		AtlasBranch.Cold => new Color(0.4f, 0.75f, 1f),
		AtlasBranch.Lightning => new Color(1f, 0.88f, 0.25f),
		_ => new Color(0.9f, 0.9f, 0.9f),
	};

	public override void _Ready()
	{
		foreach (var node in GameState.Instance.Atlas.Nodes.Values)
		{
			var b = new Button { FocusMode = FocusModeEnum.None, ClipText = true };
			var captured = node;
			b.Pressed += () => NodeClicked?.Invoke(captured);
			AddChild(b);
			_buttons[node.Id] = b;
		}
		Resized += Layout;
		Refresh();
	}

	private float ViewScale => Mathf.Min(Size.X / DesignSize.X, Size.Y / DesignSize.Y);

	private Vector2 ToScreen(Vector2 design)
	{
		float s = ViewScale;
		var offset = (Size - DesignSize * s) * 0.5f;
		return offset + design * s;
	}

	private static float Diameter(AtlasNodeType t) => t switch
	{
		AtlasNodeType.Start => 64,
		AtlasNodeType.Keystone => 70,
		AtlasNodeType.Notable => 54,
		_ => 40,
	};

	private void Layout()
	{
		foreach (var (id, b) in _buttons)
		{
			var node = GameState.Instance.Atlas.Nodes[id];
			float d = Diameter(node.Type) * Mathf.Clamp(ViewScale, 0.6f, 1.4f);
			b.Size = new Vector2(d, d);
			b.Position = ToScreen(node.ScreenPosition) - b.Size * 0.5f;
		}
		QueueRedraw();
	}

	public void Refresh()
	{
		var gs = GameState.Instance;
		foreach (var (id, b) in _buttons)
		{
			var node = gs.Atlas.Nodes[id];
			var state = gs.StateOf(id);
			var branch = BranchColor(node.Branch);
			Color fill = state switch
			{
				AtlasNodeState.Completed => branch,
				AtlasNodeState.Available => new Color(0.18f, 0.18f, 0.2f),
				_ => new Color(0.22f, 0.22f, 0.22f),
			};
			Color border = state switch
			{
				AtlasNodeState.Completed => branch.Lightened(0.3f),
				AtlasNodeState.Available => branch,
				_ => new Color(0.35f, 0.35f, 0.35f),
			};
			int width = state == AtlasNodeState.Available ? 4 : 2;
			float radius = Diameter(node.Type);
			StyleBoxFlat Style(Color f, Color bc) => new()
			{
				BgColor = f,
				BorderColor = bc,
				BorderWidthBottom = width, BorderWidthTop = width, BorderWidthLeft = width, BorderWidthRight = width,
				CornerRadiusTopLeft = (int)radius, CornerRadiusTopRight = (int)radius,
				CornerRadiusBottomLeft = (int)radius, CornerRadiusBottomRight = (int)radius,
				ShadowColor = state == AtlasNodeState.Available ? new Color(branch, 0.6f) : Colors.Transparent,
				ShadowSize = state == AtlasNodeState.Available ? 10 : 0,
			};
			b.AddThemeStyleboxOverride("normal", Style(fill, border));
			b.AddThemeStyleboxOverride("hover", Style(fill.Lightened(0.2f), Colors.White));
			b.AddThemeStyleboxOverride("pressed", Style(fill.Darkened(0.2f), Colors.White));
			b.AddThemeStyleboxOverride("disabled", Style(fill, border));
			b.Disabled = !gs.CanEnter(id) && node.Type != AtlasNodeType.Start;
			b.MouseDefaultCursorShape = gs.CanEnter(id) ? CursorShape.PointingHand : CursorShape.Arrow;
			b.TooltipText = Tooltip(node, state);
			b.Text = node.Type switch
			{
				AtlasNodeType.Keystone => "K",
				AtlasNodeType.Notable => "N",
				AtlasNodeType.Start => "",
				_ => "",
			};
			b.AddThemeColorOverride("font_color", state == AtlasNodeState.Completed ? Colors.Black : border);
			b.AddThemeColorOverride("font_disabled_color", border);
		}
		Layout();
	}

	public static string Tooltip(AtlasNodeData node, AtlasNodeState state)
	{
		var gs = GameState.Instance;
		var lines = new List<string> { $"{node.DisplayName} ({node.TypeName})" };
		if (node.Type == AtlasNodeType.Start)
		{
			lines.Add("Начало пути");
			return string.Join("\n", lines);
		}
		lines.Add($"Пассивка: {node.PassiveDescription}");
		var mods = node.LocationModifiers?.Where(m => m != null).Select(m => m.DisplayName).ToArray() ?? Array.Empty<string>();
		lines.Add(mods.Length > 0 ? $"Модификаторы: {string.Join(", ", mods)}" : "Модификаторы: нет");
		lines.Add($"Шаг {gs.Atlas.DepthOf(node.Id)} от старта");
		lines.Add(state switch
		{
			AtlasNodeState.Completed => "Пройден",
			AtlasNodeState.Available => "Доступен",
			_ => "Закрыт",
		});
		return string.Join("\n", lines);
	}

	public override void _Draw()
	{
		var gs = GameState.Instance;
		foreach (var (a, b) in gs.Atlas.Edges())
		{
			bool doneA = gs.StateOf(a.Id) == AtlasNodeState.Completed;
			bool doneB = gs.StateOf(b.Id) == AtlasNodeState.Completed;
			var branch = a.Branch != AtlasBranch.None ? a.Branch : b.Branch;
			Color c = doneA && doneB ? BranchColor(branch)
				: doneA || doneB ? new Color(BranchColor(branch), 0.45f)
				: new Color(0.3f, 0.3f, 0.3f);
			DrawLine(ToScreen(a.ScreenPosition), ToScreen(b.ScreenPosition), c, doneA && doneB ? 6f : 3f, true);
		}
	}
}
