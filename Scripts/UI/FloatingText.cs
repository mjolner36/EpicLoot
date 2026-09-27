using Godot;
using EpicLoot.Combat;

namespace EpicLoot.UI;

/// <summary>Всплывающие числа урона и короткие надписи над целью.</summary>
public static class FloatingText
{
	private const int MaxAlive = 120;
	private static int _alive;
	private static readonly RandomNumberGenerator Rng = new();

	public static void Damage(Node3D body, float amount, Color color, bool crit, bool small = false)
	{
		if (body == null || !body.IsInsideTree() || _alive >= MaxAlive) return;
		var text = Mathf.RoundToInt(amount).ToString();
		Spawn(body, body.GlobalPosition + new Vector3(Rng.RandfRange(-0.4f, 0.4f), small ? 1.8f : 2.2f, 0), crit ? text + "!" : text, color,
			crit ? 72 : small ? 32 : 52);
	}

	public static void Spawn(Node from, Vector3 pos, string text, Color color, int fontSize, float duration = 0.7f)
	{
		if (from == null || !from.IsInsideTree()) return;
		var label = new Label3D
		{
			Text = text,
			Modulate = color,
			FontSize = fontSize,
			OutlineSize = 10,
			OutlineModulate = new Color(0, 0, 0, 0.85f),
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			NoDepthTest = true,
			PixelSize = 0.01f,
			RenderPriority = 10,
			OutlineRenderPriority = 9,
		};
		Fx.Root(from).AddChild(label);
		label.GlobalPosition = pos;
		_alive++;
		label.TreeExiting += () => _alive--;

		var tw = label.CreateTween();
		tw.SetParallel();
		tw.TweenProperty(label, "position", label.Position + new Vector3(0, 1.2f, 0), duration).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
		tw.TweenProperty(label, "transparency", 1f, duration * 0.4f).SetDelay(duration * 0.6f);
		tw.Chain().TweenCallback(Callable.From(label.QueueFree));
	}
}
