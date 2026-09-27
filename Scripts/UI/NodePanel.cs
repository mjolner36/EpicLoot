using System.Linq;
using Godot;
using EpicLoot.Atlas;
using EpicLoot.Core;

namespace EpicLoot.UI;

/// <summary>Панель выбранного узла: пассивка, модификаторы, сложность, "Войти" / "Закрыть".</summary>
public partial class NodePanel : PanelContainer
{
	private Label _title;
	private Label _type;
	private RichTextLabel _body;
	private Button _enter;
	private AtlasNodeData _node;

	public override void _Ready()
	{
		CustomMinimumSize = new Vector2(440, 0);
		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 10);
		AddChild(box);

		_title = new Label { ThemeTypeVariation = "HeaderLarge" };
		box.AddChild(_title);
		_type = new Label { Modulate = new Color(1, 1, 1, 0.7f) };
		box.AddChild(_type);
		_body = new RichTextLabel { BbcodeEnabled = true, FitContent = true, CustomMinimumSize = new Vector2(420, 0), ScrollActive = false };
		box.AddChild(_body);

		var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
		buttons.AddThemeConstantOverride("separation", 10);
		box.AddChild(buttons);
		var close = new Button { Text = "Закрыть", CustomMinimumSize = new Vector2(130, 40) };
		close.Pressed += Hide;
		buttons.AddChild(close);
		_enter = new Button { Text = "Войти", CustomMinimumSize = new Vector2(130, 40) };
		_enter.Pressed += () =>
		{
			if (_node != null) GameState.Instance.EnterNode(_node.Id);
		};
		buttons.AddChild(_enter);
		Hide();
	}

	public void ShowNode(AtlasNodeData node)
	{
		var gs = GameState.Instance;
		_node = node;
		var state = gs.StateOf(node.Id);
		int depth = gs.Atlas.DepthOf(node.Id);
		var color = AtlasView.BranchColor(node.Branch).ToHtml(false);

		_title.Text = node.DisplayName;
		_title.Modulate = AtlasView.BranchColor(node.Branch);
		_type.Text = node.TypeName;

		var text = $"[b]Пассивка за прохождение[/b]\n[color=#{color}]{node.PassiveDescription}[/color]\n\n";
		var mods = node.LocationModifiers?.Where(m => m != null).ToArray() ?? System.Array.Empty<LocationModifierData>();
		text += "[b]Модификаторы локации[/b]\n";
		text += mods.Length == 0 ? "нет\n" : string.Join("\n", mods.Select(m => $"• {m.DisplayName}: {m.Description}")) + "\n";
		text += $"\n[b]Сложность[/b]\nШаг {depth}: +{Mathf.RoundToInt(gs.Config.DifficultyPerStep * depth * 100)}% жизни и урона врагов\n";
		text += $"Уровень предметов: {gs.ItemLevelOf(node.Id)}\n";
		if (node.Type == AtlasNodeType.Keystone)
			text += $"Вместо волн — элитный рыцарь (жизнь ×{gs.Config.KeystoneLifeMultiplier:0.#}) с эскортом\n";
		if (state == AtlasNodeState.Completed)
			text += "\n[color=#aaaaaa]Узел уже пройден: можно перепройти, но без награды.[/color]";
		_body.Text = text;

		_enter.Disabled = !gs.CanEnter(node.Id);
		_enter.Text = state == AtlasNodeState.Completed ? "Перепройти" : "Войти";
		Show();
	}
}
