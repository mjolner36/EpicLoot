using System.Collections.Generic;
using Godot;
using EpicLoot.Combat;
using EpicLoot.Enemies;
using EpicLoot.Stats;

namespace EpicLoot.UI;

/// <summary>
/// Виджет над врагом в экранном UI: имя (элита), полоса жизни, ряд иконок статусов
/// с круговым таймером и числом стаков. Рисуется через _Draw, берётся из пула.
/// </summary>
public partial class EnemyWidget : Control
{
	public const float IconSize = 20f;
	private const float IconGap = 3f;

	public EnemyBase Enemy { get; private set; }
	public int IconCount { get; private set; }
	public string ChillStackText { get; private set; } = "";

	private readonly List<StatusEffect> _statuses = new();
	private Font _font;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		_font = ThemeDB.FallbackFont;
	}

	public void Bind(EnemyBase enemy)
	{
		Enemy = enemy;
		Visible = false;
	}

	public void Unbind()
	{
		Enemy = null;
		Visible = false;
		_statuses.Clear();
		IconCount = 0;
	}

	private float BarWidth => Enemy != null && Enemy.IsElite ? 130f : 56f;
	private float BarHeight => Enemy != null && Enemy.IsElite ? 9f : 6f;

	/// <summary>Обновление из слоя: позиция на экране и видимость.</summary>
	public void UpdateFrom(Camera3D camera, Rect2 screen)
	{
		if (Enemy == null || !IsInstanceValid(Enemy) || Enemy.IsDead)
		{
			Visible = false;
			return;
		}
		// Полоса появляется после первого урона; у элиты — всегда.
		bool show = Enemy.IsElite || Enemy.WasHit;
		var world = Enemy.GlobalPosition + Vector3.Up * (Enemy.Visual.Height + 0.35f);
		if (!show || camera.IsPositionBehind(world))
		{
			Visible = false;
			return;
		}
		var p = camera.UnprojectPosition(world);
		if (!screen.Grow(60).HasPoint(p))
		{
			Visible = false;
			return;
		}
		Visible = true;
		Position = p.Round();

		_statuses.Clear();
		_statuses.AddRange(Enemy.Status.Active);
		IconCount = _statuses.Count;
		ChillStackText = "";
		foreach (var fx in _statuses)
			if (fx.Data.MaxStacks > 1) ChillStackText = fx.Stacks.ToString();
		QueueRedraw();
	}

	public override void _Draw()
	{
		if (Enemy == null) return;
		float w = BarWidth, h = BarHeight;
		float y = 0f;

		if (Enemy.IsElite)
		{
			DrawString(_font, new Vector2(-w * 0.5f, -6f), Enemy.Data.DisplayName, HorizontalAlignment.Center, w, 14, new Color(1f, 0.75f, 0.6f));
		}

		// Полоса жизни.
		float max = Enemy.Stats.Get(StatType.MaxLife);
		float frac = max > 0 ? Mathf.Clamp(Enemy.Stats.CurrentLife / max, 0f, 1f) : 0f;
		var bar = new Rect2(-w * 0.5f, y, w, h);
		DrawRect(bar.Grow(1f), new Color(0, 0, 0, 0.8f));
		DrawRect(new Rect2(bar.Position, new Vector2(w * frac, h)), Enemy.IsElite ? new Color(0.85f, 0.15f, 0.1f) : new Color(0.8f, 0.2f, 0.18f));
		y += h + 4f;

		// Иконки статусов.
		if (_statuses.Count == 0) return;
		float total = _statuses.Count * IconSize + (_statuses.Count - 1) * IconGap;
		float x = -total * 0.5f;
		foreach (var fx in _statuses)
		{
			var rect = new Rect2(x, y, IconSize, IconSize);
			var border = DamageInfo.ColorOf(fx.Data.DamageType);
			DrawRect(rect, new Color(0.05f, 0.05f, 0.07f, 0.9f));
			if (fx.Data.Icon != null) DrawTextureRect(fx.Data.Icon, rect, false);
			// Круговой таймер: затемнённый сектор прошедшего времени.
			float elapsed = fx.Duration > 0 ? 1f - Mathf.Clamp(fx.Remaining / fx.Duration, 0f, 1f) : 0f;
			if (elapsed > 0.01f) DrawPie(rect.GetCenter(), IconSize * 0.5f, elapsed, new Color(0, 0, 0, 0.55f));
			DrawRect(rect, border, false, 1.5f);
			if (fx.Data.MaxStacks > 1)
			{
				var pos = rect.End + new Vector2(-IconSize, -2f);
				DrawString(_font, pos + new Vector2(1, 1), fx.Stacks.ToString(), HorizontalAlignment.Right, IconSize - 1, 13, Colors.Black);
				DrawString(_font, pos, fx.Stacks.ToString(), HorizontalAlignment.Right, IconSize - 2, 13, Colors.White);
			}
			x += IconSize + IconGap;
		}
	}

	/// <summary>Сектор вписанного круга от 12 часов по часовой стрелке.</summary>
	private void DrawPie(Vector2 c, float r, float fraction, Color color)
	{
		int segs = Mathf.Max(2, Mathf.CeilToInt(32 * fraction));
		var pts = new Vector2[segs + 2];
		pts[0] = c;
		for (int i = 0; i <= segs; i++)
		{
			float a = -Mathf.Pi / 2f + Mathf.Tau * fraction * i / segs;
			pts[i + 1] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
		}
		DrawColoredPolygon(pts, color);
	}
}
