using System.Collections.Generic;
using Godot;
using EpicLoot.Core;
using EpicLoot.Items;

namespace EpicLoot.UI;

/// <summary>
/// Ячейка тайника или слот куклы. Перетаскивание (данные — Variant-словарь с индексом/слотом),
/// двойной клик — надеть/снять, ПКМ — меню (крафт, разобрать), в режиме массовой разборки клик выделяет.
/// </summary>
public partial class ItemCell : Control
{
	public const float CellSize = 54f;

	public InventoryScreen Screen;
	public int StashIndex = -1;
	public ItemSlot DollSlot;
	public bool IsDoll;

	private Inventory Inv => GameState.Instance.Profile;

	public ItemInstance Item => IsDoll ? Inv.Equipped.GetValueOrDefault(DollSlot) : Inv.Stash[StashIndex];

	public static string SlotName(ItemSlot s) => s switch
	{
		ItemSlot.Weapon => "Оружие",
		ItemSlot.Helmet => "Шлем",
		ItemSlot.Chest => "Нагрудник",
		ItemSlot.Belt => "Пояс",
		ItemSlot.Boots => "Сапоги",
		ItemSlot.Ring => "Кольцо",
		_ => "Амулет",
	};

	public override void _Ready()
	{
		CustomMinimumSize = IsDoll ? new Vector2(76, 76) : new Vector2(CellSize, CellSize);
		MouseFilter = MouseFilterEnum.Stop;
	}

	public void Refresh()
	{
		TooltipText = Item != null ? " " : IsDoll ? SlotName(DollSlot) : "";
		QueueRedraw();
	}

	private bool Selected => !IsDoll && Screen != null && Screen.IsSelected(StashIndex);

	public override void _Draw()
	{
		var rect = new Rect2(Vector2.Zero, Size);
		var item = Item;
		DrawRect(rect, item != null ? new Color(0.12f, 0.12f, 0.15f) : new Color(0.09f, 0.09f, 0.1f));
		var font = ThemeDB.FallbackFont;
		if (item != null)
		{
			var c = item.Color;
			DrawRect(rect.Grow(-3), new Color(c.R, c.G, c.B, Selected ? 0.35f : 0.12f));
			DrawString(font, new Vector2(0, Size.Y * 0.5f + 5), item.Base.ShortName, HorizontalAlignment.Center, Size.X, IsDoll ? 15 : 13, c);
			DrawString(font, new Vector2(4, Size.Y - 5), item.Level.ToString(), HorizontalAlignment.Left, -1, 11, new Color(1, 1, 1, 0.5f));
			if (item.Potential <= 0) DrawString(font, new Vector2(0, Size.Y - 5), "0", HorizontalAlignment.Right, Size.X - 4, 11, new Color(0.9f, 0.4f, 0.4f));
			DrawRect(rect, Selected ? new Color(1f, 0.45f, 0.35f) : c, false, Selected ? 3f : item.Rarity == Rarity.Normal ? 1f : 2f);
		}
		else
		{
			if (IsDoll) DrawString(font, new Vector2(0, Size.Y * 0.5f + 5), SlotName(DollSlot), HorizontalAlignment.Center, Size.X, 12, new Color(1, 1, 1, 0.3f));
			DrawRect(rect, new Color(0.3f, 0.3f, 0.32f), false, 1f);
		}
	}

	public override GodotObject _MakeCustomTooltip(string forText)
	{
		var item = Item;
		if (item == null) return null;
		var label = new RichTextLabel
		{
			BbcodeEnabled = true,
			FitContent = true,
			ScrollActive = false,
			AutowrapMode = TextServer.AutowrapMode.Off,
			CustomMinimumSize = new Vector2(340, 0),
		};
		var equipped = Inv.Equipped.GetValueOrDefault(item.Slot);
		label.Text = ItemText.Describe(item) + "\n\n" + ItemText.Compare(item, equipped);
		return label;
	}

	public override Variant _GetDragData(Vector2 atPosition)
	{
		if (Item == null || Screen.MassMode) return default;
		var preview = new Label { Text = Item.Name, Modulate = Item.Color };
		SetDragPreview(preview);
		return new Godot.Collections.Dictionary { ["doll"] = IsDoll, ["index"] = StashIndex, ["slot"] = (int)DollSlot };
	}

	public override bool _CanDropData(Vector2 atPosition, Variant data) =>
		data.VariantType == Variant.Type.Dictionary && Screen.CanDrop(this, data.AsGodotDictionary());

	public override void _DropData(Vector2 atPosition, Variant data) => Screen.Drop(this, data.AsGodotDictionary());

	public override void _GuiInput(InputEvent e)
	{
		if (e is not InputEventMouseButton { Pressed: true } mb) return;
		if (Screen.MassMode)
		{
			if (mb.ButtonIndex == MouseButton.Left && !IsDoll && Item != null) Screen.ToggleSelected(StashIndex);
			AcceptEvent();
			return;
		}
		if (mb.ButtonIndex == MouseButton.Left && mb.DoubleClick)
		{
			Screen.ToggleEquip(this);
			AcceptEvent();
		}
		else if (mb.ButtonIndex == MouseButton.Right && Item != null)
		{
			Screen.ShowContextMenu(this, GetGlobalMousePosition());
			AcceptEvent();
		}
	}
}
