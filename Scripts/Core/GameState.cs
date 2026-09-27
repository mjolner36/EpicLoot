using System.Collections.Generic;
using System.Linq;
using Godot;
using Godot.Collections;
using EpicLoot.Atlas;
using EpicLoot.Items;

namespace EpicLoot.Core;

/// <summary>Состояние игры между сценами: данные, прогресс атласа, имущество, текущий забег (autoload).</summary>
public partial class GameState : Node
{
	public const string ConfigPath = "res://Data/game_config.tres";
	public const string AtlasScene = "res://Scenes/Atlas.tscn";
	public const string ArenaScene = "res://Scenes/Arena.tscn";

	public static GameState Instance { get; private set; }

	public GameConfig Config { get; private set; }
	public AtlasGraph Atlas { get; private set; }
	public HashSet<string> Completed { get; private set; } = new();

	public ItemDatabase Items { get; private set; }
	public ItemGenerator Generator { get; private set; }
	public Inventory Profile { get; private set; }
	/// <summary>Сумка текущего забега. null вне боя.</summary>
	public RunBag Bag { get; private set; }

	public override void _EnterTree()
	{
		Instance = this;
		InputSetup.Register();
		Config = ResourceLoader.Load<GameConfig>(ConfigPath);
		if (Config == null)
		{
			GD.PushError($"Не найден {ConfigPath}");
			return;
		}
		Atlas = AtlasGraph.LoadFromDir(Config.AtlasNodesDir, Config.StartNodeId);
		Items = new ItemDatabase(Config.Loot);
		Generator = new ItemGenerator(Items);
		Profile = new Inventory(Config.Loot.StashSize);
	}

	public override void _Ready()
	{
		LoadGame();

		var args = OS.GetCmdlineUserArgs();
		if (args.Contains("--selftest"))
			CallDeferred(MethodName.RunSelfTest);
		else if (args.Contains("--shots"))
			CallDeferred(MethodName.RunShots);
	}

	/// <summary>
	/// Читает сейв. Нет сейва или сейв старше v3 (другая модель предметов) — новый профиль
	/// со стартовой экипировкой; прогресс атласа сохраняется всегда.
	/// </summary>
	public void LoadGame()
	{
		var data = SaveSystem.Instance.Load();
		Completed = new HashSet<string>();
		if (data.TryGetValue("completed", out var completed) && completed.VariantType == Variant.Type.Array)
			foreach (var id in completed.AsGodotArray()) Completed.Add(id.AsString());
		Completed.RemoveWhere(id => !Atlas.Nodes.ContainsKey(id));

		int version = data.GetValueOrDefault("version", 0).AsInt32();
		if (version >= 3 && data.TryGetValue("inventory", out var inv) && inv.VariantType == Variant.Type.Dictionary)
			Profile.LoadFrom(inv.AsGodotDictionary(), Items, Atlas);
		else
			NewProfile();
	}

	private void NewProfile()
	{
		Profile.LoadFrom(new Dictionary(), Items, Atlas);
		foreach (var id in Config.Loot.StarterEquipment)
		{
			var b = Items.Base(id);
			if (b == null) continue;
			var item = Generator.Generate(1, Rarity.Normal, b);
			item.BaseValue = b.BaseIsPercent ? b.BaseMin : Mathf.Round((b.BaseMin + b.BaseMax) * 0.5f);
			Profile.Equipped[b.Slot] = item;
		}
	}

	public void SaveGame()
	{
		var arr = new Array();
		foreach (var id in Completed) arr.Add(id);
		SaveSystem.Instance.Save(new Dictionary { ["completed"] = arr, ["inventory"] = Profile.ToDict() });
	}

	private async void RunSelfTest()
	{
		int code = await SelfTest.Run(this);
		GetTree().Quit(code);
	}

	private async void RunShots()
	{
		int code = await SelfTest.Shots(this, ProjectSettings.GlobalizePath("user://"));
		GetTree().Quit(code);
	}

	/// <summary>Карта текущего забега (из устройства карт или бесплатная T1).</summary>
	public MapInstance CurrentMap { get; private set; }
	public AtlasNodeData CurrentNode => CurrentMap?.Node;

	public AtlasNodeState StateOf(string id) => Atlas.StateOf(id, Completed);

	/// <summary>Пройденные узлы с пассивками (без старта).</summary>
	public IEnumerable<AtlasNodeData> ActivePassives =>
		Atlas.Ordered().Where(n => n.Type != AtlasNodeType.Start && Completed.Contains(n.Id));

	public bool CanOpenFree(AtlasNodeData node) => node != null && node.Type != AtlasNodeType.Start && node.Tier == 1;

	/// <summary>Устройство карт: карта из тайника расходуется и запускает забег.</summary>
	public bool OpenMap(MapInstance map)
	{
		if (map == null || !Profile.Maps.Remove(map)) return false;
		SaveGame();
		StartRun(map);
		return true;
	}

	/// <summary>Карты T1 открываются без предмета, бесплатно и без модов.</summary>
	public bool OpenFree(AtlasNodeData node)
	{
		if (!CanOpenFree(node)) return false;
		StartRun(new MapInstance { Node = node, Tier = node.Tier });
		return true;
	}

	/// <summary>Отладка и тесты: забег по узлу обычной картой его тира, без проверок.</summary>
	public void EnterNode(string id)
	{
		var node = Atlas.Get(id);
		if (node == null || node.Type == AtlasNodeType.Start) return;
		StartRun(new MapInstance { Node = node, Tier = node.Tier });
	}

	/// <summary>Отладка и тесты: забег по конкретной карте без траты из тайника.</summary>
	public void EnterMap(MapInstance map) => StartRun(map);

	private void StartRun(MapInstance map)
	{
		CurrentMap = map;
		Bag = new RunBag(Config.Loot.BagSize);
		ChangeScene(ArenaScene);
	}

	/// <summary>Арена запущена напрямую из редактора: бесплатная карта первого узла T1.</summary>
	public MapInstance EnsureMap()
	{
		if (CurrentMap != null) return CurrentMap;
		var node = Atlas.Nodes.Values.Where(n => n.Type != AtlasNodeType.Start && n.Tier == 1).OrderBy(n => n.Id).FirstOrDefault();
		return CurrentMap = new MapInstance { Node = node, Tier = node?.Tier ?? 1 };
	}

	/// <summary>Сумка текущего забега (создаётся, если арену запустили напрямую из редактора).</summary>
	public RunBag EnsureBag() => Bag ??= new RunBag(Config.Loot.BagSize);

	/// <summary>Засчитывает узел. true — если пройден впервые (пассивка открыта).</summary>
	public bool CompleteNode(string id)
	{
		if (id == null || !Atlas.Nodes.ContainsKey(id) || !Completed.Add(id)) return false;
		SaveGame();
		EventBus.Instance.EmitSignal(EventBus.SignalName.AtlasChanged);
		return true;
	}

	/// <summary>Победа: сумка переносится в тайник. Возвращает положенные и не поместившиеся предметы.</summary>
	public (List<ItemInstance> stored, List<ItemInstance> overflow) TransferBag()
	{
		var stored = new List<ItemInstance>();
		var overflow = new List<ItemInstance>();
		if (Bag == null) return (stored, overflow);
		foreach (var item in Bag.Items)
			(Profile.AddToStash(item) ? stored : overflow).Add(item);
		Profile.Maps.AddRange(Bag.Maps);
		Profile.AddCurrency(Bag.Currency);
		SaveGame();
		return (stored, overflow);
	}

	public void ResetAtlas()
	{
		Completed.Clear();
		SaveGame();
		EventBus.Instance.EmitSignal(EventBus.SignalName.AtlasChanged);
	}

	/// <summary>Возврат на атлас. Если сумка не перенесена (смерть, сдача) — она сгорает.</summary>
	public void ReturnToAtlas()
	{
		CurrentMap = null;
		Bag = null;
		ChangeScene(AtlasScene);
	}

	private void ChangeScene(string path)
	{
		GetTree().Paused = false;
		GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, path);
	}
}
