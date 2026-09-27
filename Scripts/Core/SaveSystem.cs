using Godot;
using Godot.Collections;

namespace EpicLoot.Core;

/// <summary>Один JSON-файл в user://: пройденные узлы атласа, тайник, экипировка, сферы (autoload).</summary>
public partial class SaveSystem : Node
{
	public const string SavePath = "user://save.json";
	public const int Version = 3;

	public static SaveSystem Instance { get; private set; }

	public override void _EnterTree() => Instance = this;

	public void Save(Dictionary data)
	{
		data["version"] = Version;
		using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
		if (file == null)
		{
			GD.PushError($"Не удалось записать сейв: {FileAccess.GetOpenError()}");
			return;
		}
		file.StoreString(Json.Stringify(data, "\t"));
	}

	/// <summary>Читает сейв. Версия 1 содержала только "completed" — она читается как есть.</summary>
	public Dictionary Load()
	{
		var text = ReadRaw();
		if (text == null) return new Dictionary();
		var parsed = Json.ParseString(text);
		return parsed.VariantType == Variant.Type.Dictionary ? parsed.AsGodotDictionary() : new Dictionary();
	}

	public string ReadRaw()
	{
		if (!FileAccess.FileExists(SavePath)) return null;
		using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
		return file?.GetAsText();
	}

	public void WriteRaw(string text)
	{
		if (text == null)
		{
			if (FileAccess.FileExists(SavePath)) DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(SavePath));
			return;
		}
		using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
		file?.StoreString(text);
	}
}
