using Godot;

namespace EpicLoot.Stats;

[GlobalClass]
public partial class Modifier : Resource
{
	[Export] public StatType Stat { get; set; }
	[Export] public ModifierType Type { get; set; }
	[Export] public float Value { get; set; }
	[Export] public string SourceId { get; set; } = "";

	public Modifier() { }

	public Modifier(StatType stat, ModifierType type, float value, string sourceId = "")
	{
		Stat = stat;
		Type = type;
		Value = value;
		SourceId = sourceId;
	}

	public string Describe()
	{
		return Type switch
		{
			ModifierType.Flat => $"{Stat} {Value:+0.##;-0.##}",
			ModifierType.Increased => $"{Value * 100:+0;-0}% increased {Stat}",
			_ => $"{Value * 100:+0;-0}% more {Stat}",
		};
	}
}
