using Godot;
using EpicLoot.Skills;

namespace EpicLoot.UI;

/// <summary>Иконка умения: клавиша, название, затемнение по кулдауну.</summary>
public partial class SkillSlotView : Panel
{
	private readonly ColorRect _shade;
	private readonly Label _cd;

	public SkillSlotView() { }

	public SkillSlotView(SkillData skill)
	{
		CustomMinimumSize = new Vector2(84, 84);
		var color = skill?.Color ?? Colors.Gray;
		AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(color.R * 0.35f, color.G * 0.35f, color.B * 0.35f),
			BorderColor = color,
			BorderWidthBottom = 2, BorderWidthTop = 2, BorderWidthLeft = 2, BorderWidthRight = 2,
		});
		var key = new Label { Text = skill?.KeyLabel ?? "", Position = new Vector2(6, 2) };
		AddChild(key);
		var name = new Label
		{
			Text = skill?.DisplayName ?? "",
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			HorizontalAlignment = HorizontalAlignment.Center,
			Position = new Vector2(2, 30),
			Size = new Vector2(80, 50),
		};
		name.AddThemeFontSizeOverride("font_size", 13);
		AddChild(name);
		_shade = new ColorRect { Color = new Color(0, 0, 0, 0.65f), MouseFilter = MouseFilterEnum.Ignore };
		AddChild(_shade);
		_cd = new Label { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Size = new Vector2(84, 84) };
		_cd.AddThemeFontSizeOverride("font_size", 26);
		AddChild(_cd);
	}

	public void SetCooldown(float remaining, float fraction)
	{
		fraction = Mathf.Clamp(fraction, 0f, 1f);
		_shade.Position = new Vector2(0, 84 * (1f - fraction));
		_shade.Size = new Vector2(84, 84 * fraction);
		_cd.Text = remaining > 0.05f ? remaining.ToString(remaining < 1f ? "0.0" : "0") : "";
	}
}
