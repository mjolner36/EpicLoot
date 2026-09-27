using Godot;

namespace EpicLoot.Core;

/// <summary>Регистрирует действия ввода из кода (надёжнее ручной правки [input] в project.godot).</summary>
public static class InputSetup
{
	public const string MoveLeft = "move_left";
	public const string MoveRight = "move_right";
	public const string MoveUp = "move_up";
	public const string MoveDown = "move_down";
	public const string SkillPrimary = "skill_primary";
	public const string SkillSecondary = "skill_secondary";
	public const string SkillTertiary = "skill_tertiary";
	public const string Dash = "dash";
	public const string Abandon = "abandon";
	public const string DebugStress = "debug_stress";
	public const string Inventory = "inventory";
	public const string ShowBag = "show_bag";

	public static readonly string[] SkillActions = { SkillPrimary, SkillSecondary, SkillTertiary };

	public static void Register()
	{
		Key(MoveLeft, Godot.Key.A);
		Key(MoveRight, Godot.Key.D);
		Key(MoveUp, Godot.Key.W);
		Key(MoveDown, Godot.Key.S);
		Mouse(SkillPrimary, MouseButton.Left);
		Mouse(SkillSecondary, MouseButton.Right);
		Key(SkillTertiary, Godot.Key.Q);
		Key(Dash, Godot.Key.Space);
		Key(Abandon, Godot.Key.Escape);
		Key(DebugStress, Godot.Key.F9);
		Key(Inventory, Godot.Key.I);
		Key(ShowBag, Godot.Key.Tab);
	}

	private static void Ensure(string action)
	{
		if (!InputMap.HasAction(action)) InputMap.AddAction(action);
	}

	private static void Key(string action, Key key)
	{
		Ensure(action);
		InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = key });
	}

	private static void Mouse(string action, MouseButton button)
	{
		Ensure(action);
		InputMap.ActionAddEvent(action, new InputEventMouseButton { ButtonIndex = button });
	}
}
