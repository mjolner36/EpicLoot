using Godot;

namespace EpicLoot.Items;

/// <summary>Числа лута, крафта и карт. res://Data/loot_config.tres</summary>
[GlobalClass]
public partial class LootConfig : Resource
{
	[ExportGroup("Данные")]
	[Export] public string BasesDir { get; set; } = "res://Data/Items/Bases";
	[Export] public string AffixesDir { get; set; } = "res://Data/Items/Affixes";
	[Export] public string CurrencyDir { get; set; } = "res://Data/Items/Currency";
	[Export] public string MapModsDir { get; set; } = "res://Data/Atlas/MapMods";
	/// <summary>Предметы, надетые в новом профиле (id баз).</summary>
	[Export] public string[] StarterEquipment { get; set; } = System.Array.Empty<string>();

	[ExportGroup("Редкость и аффиксы")]
	/// <summary>Веса Normal, Magic, Rare при выпадении (предметы и карты). Бонус редкости умножает Magic и Rare.</summary>
	[Export] public float[] RarityWeights { get; set; } = { 70f, 25f, 5f };
	[Export] public int MagicAffixMin { get; set; } = 1;
	[Export] public int MagicAffixMax { get; set; } = 2;
	[Export] public int RareAffixMin { get; set; } = 3;
	[Export] public int RareAffixMax { get; set; } = 4;
	[Export] public int MaxPrefixes { get; set; } = 2;
	[Export] public int MaxSuffixes { get; set; } = 2;
	[Export] public int MaxTier { get; set; } = 5;
	/// <summary>Максимальный тир выпавших аффиксов по тиру карты (индекс 0 = карта T1).</summary>
	[Export] public int[] MaxAffixTierByMapTier { get; set; } = { 1, 2, 3, 3, 4 };

	[ExportGroup("Потенциал ковки")]
	/// <summary>Диапазоны ПК по выпавшей редкости: X..Y.</summary>
	[Export] public Vector2I NormalPotential { get; set; } = new(30, 40);
	[Export] public Vector2I MagicPotential { get; set; } = new(20, 30);
	[Export] public Vector2I RarePotential { get; set; } = new(10, 20);
	[Export] public int ShardCostMin { get; set; } = 1;
	[Export] public int ShardCostMax { get; set; } = 8;
	[Export] public float HopeChance { get; set; } = 0.25f;
	[Export] public int DiscoveryMin { get; set; } = 2;
	[Export] public int DiscoveryMax { get; set; } = 4;
	[Export] public int DiscoveryMaxTier { get; set; } = 2;

	[ExportGroup("Разборка")]
	/// <summary>Осколков за аффикс по тиру (индекс 0 = T1).</summary>
	[Export] public int[] DisassembleShardsByTier { get; set; } = { 1, 1, 2, 2, 3 };
	/// <summary>Шанс, что один из осколков предмета теряется.</summary>
	[Export] public float DisassembleLossChance { get; set; } = 0.3f;
	[Export] public float DisassembleGlyphRuneChance { get; set; } = 0.1f;
	/// <summary>Сколько раз бросается шанс глифа/руны у редких и возвышенных.</summary>
	[Export] public int DisassembleRareRolls { get; set; } = 2;

	[ExportGroup("Сумка и тайник")]
	[Export] public int BagSize { get; set; } = 12;
	[Export] public int StashColumns { get; set; } = 10;
	[Export] public int StashRows { get; set; } = 6;
	[Export] public float PickupRadius { get; set; } = 1.2f;
	[Export] public float ClickPickupRange { get; set; } = 4f;
	[Export] public float ClickPickupTolerance { get; set; } = 1.0f;

	[ExportGroup("Keystone-босс")]
	[Export] public int KeystoneItems { get; set; } = 3;
	[Export] public Rarity KeystoneMinRarity { get; set; } = Rarity.Rare;
	[Export] public int KeystoneShards { get; set; } = 5;
	[Export] public int KeystoneGlyphRunes { get; set; } = 1;
	[Export] public int KeystoneMaps { get; set; } = 2;

	[ExportGroup("Карты")]
	[Export] public int MapMagicModsMin { get; set; } = 1;
	[Export] public int MapMagicModsMax { get; set; } = 2;
	[Export] public int MapRareModsMin { get; set; } = 3;
	[Export] public int MapRareModsMax { get; set; } = 4;
	[Export] public int MaxMapTier { get; set; } = 5;

	public int StashSize => StashColumns * StashRows;
}
