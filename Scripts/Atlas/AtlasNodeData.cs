using Godot;
using EpicLoot.Stats;

namespace EpicLoot.Atlas;

public enum AtlasNodeType
{
	Start,
	Small,
	Notable,
	Keystone,
}

public enum AtlasBranch
{
	None,
	Fire,
	Cold,
	Lightning,
}

[GlobalClass]
public partial class AtlasNodeData : Resource
{
	[Export] public string Id { get; set; } = "";
	[Export] public string DisplayName { get; set; } = "";
	[Export] public AtlasNodeType Type { get; set; } = AtlasNodeType.Small;
	[Export] public AtlasBranch Branch { get; set; }
	[Export] public string[] Neighbors { get; set; } = System.Array.Empty<string>();

	[ExportGroup("Пассивка")]
	[Export(PropertyHint.MultilineText)] public string PassiveDescription { get; set; } = "";
	[Export] public Modifier[] PassiveModifiers { get; set; } = System.Array.Empty<Modifier>();
	/// <summary>Теги, которые пассивка вешает на игрока (NoChill, NoIgnite).</summary>
	[Export] public string[] GrantedTags { get; set; } = System.Array.Empty<string>();
	/// <summary>Id особого эффекта (для читаемости данных, логика — через статы).</summary>
	[Export] public string SpecialId { get; set; } = "";

	[ExportGroup("Локация")]
	/// <summary>Тир локации (1-5): тир карт этого узла и сложность.</summary>
	[Export] public int Tier { get; set; } = 1;
	/// <summary>Позиция на экране атласа в дизайн-координатах 1200×800.</summary>
	[Export] public Vector2 ScreenPosition { get; set; }

	public string TypeName => Type switch
	{
		AtlasNodeType.Start => "Старт",
		AtlasNodeType.Small => "Малый узел",
		AtlasNodeType.Notable => "Значимый узел",
		_ => "Keystone",
	};
}
