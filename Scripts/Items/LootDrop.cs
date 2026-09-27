using Godot;
using EpicLoot.Combat;

namespace EpicLoot.Items;

/// <summary>Предмет, карта или валюта на земле: светящийся столбик цвета редкости и подпись.</summary>
public partial class LootDrop : Node3D
{
	public static readonly Color CurrencyColor = new(0.85f, 0.7f, 1f);
	public static readonly Color MapColorTint = new(0.55f, 1f, 0.75f);

	private static CylinderMesh _beam;
	private static SphereMesh _orbMesh;
	private static BoxMesh _mapMesh;

	public ItemInstance Item { get; private set; }
	public MapInstance Map { get; private set; }
	public CraftCurrencyData Currency { get; private set; }

	/// <summary>Предметы и карты занимают ячейку сумки, валюта — нет.</summary>
	public bool TakesBagCell => Currency == null;

	public string Label => Item != null ? $"{Item.Name} (ур. {Item.Level})"
		: Map != null ? $"Карта: {Map.Name} T{Map.Tier}"
		: Currency.DisplayName;

	public Color Color => Item?.Color ?? (Map != null ? Map.Color : CurrencyColor);

	public static LootDrop SpawnItem(Node parent, Vector3 pos, ItemInstance item) => Spawn(parent, pos, new LootDrop { Item = item, Name = "ItemDrop" });
	public static LootDrop SpawnMap(Node parent, Vector3 pos, MapInstance map) => Spawn(parent, pos, new LootDrop { Map = map, Name = "MapDrop" });
	public static LootDrop SpawnCurrency(Node parent, Vector3 pos, CraftCurrencyData c) => Spawn(parent, pos, new LootDrop { Currency = c, Name = "CurrencyDrop" });

	private static LootDrop Spawn(Node parent, Vector3 pos, LootDrop d)
	{
		_beam ??= new CylinderMesh { TopRadius = 0.05f, BottomRadius = 0.25f, Height = 1.4f, RadialSegments = 8 };
		_orbMesh ??= new SphereMesh { Radius = 0.2f, Height = 0.4f, RadialSegments = 10, Rings = 5 };
		_mapMesh ??= new BoxMesh { Size = new Vector3(0.5f, 0.06f, 0.35f) };
		var color = d.Color;
		var glow = new Color(color.R, color.G, color.B, 0.55f);
		Mesh mesh = d.Item != null ? _beam : d.Map != null ? _mapMesh : _orbMesh;
		d.AddChild(new MeshInstance3D
		{
			Mesh = mesh,
			MaterialOverride = Fx.Unshaded(d.Map != null ? new Color(MapColorTint, 0.8f) : glow),
			Position = new Vector3(0, d.Item != null ? 0.7f : 0.3f, 0),
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
		});
		d.AddChild(new Label3D
		{
			Text = d.Label,
			Modulate = color,
			FontSize = 30,
			OutlineSize = 8,
			PixelSize = 0.01f,
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			NoDepthTest = true,
			Position = new Vector3(0, d.Item != null ? 1.7f : 1.0f, 0),
		});
		parent.AddChild(d);
		d.GlobalPosition = new Vector3(pos.X, 0, pos.Z);
		return d;
	}
}
