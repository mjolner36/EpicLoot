using System.Collections.Generic;
using System.Linq;
using Godot;

namespace EpicLoot.Atlas;

public enum AtlasNodeState
{
	Locked,
	Available,
	Completed,
}

/// <summary>Граф атласа: узлы, связи, глубина от старта и состояние по набору пройденных.</summary>
public class AtlasGraph
{
	public readonly Dictionary<string, AtlasNodeData> Nodes = new();
	public readonly Dictionary<string, HashSet<string>> Links = new();
	public readonly Dictionary<string, int> Depth = new();
	public string StartId { get; private set; }

	public static AtlasGraph LoadFromDir(string dir, string startId)
	{
		var g = new AtlasGraph { StartId = startId };
		foreach (var file in DirAccess.GetFilesAt(dir))
		{
			var name = file.EndsWith(".remap") ? file[..^6] : file;
			if (!name.EndsWith(".tres") && !name.EndsWith(".res")) continue;
			var node = ResourceLoader.Load<AtlasNodeData>($"{dir}/{name}");
			if (node == null)
			{
				GD.PushError($"Не удалось загрузить узел атласа {name}");
				continue;
			}
			g.Nodes[node.Id] = node;
		}
		g.BuildLinks();
		return g;
	}

	private void BuildLinks()
	{
		foreach (var id in Nodes.Keys) Links[id] = new HashSet<string>();
		// Связи двусторонние, даже если в данных указаны с одной стороны.
		foreach (var node in Nodes.Values)
		{
			foreach (var n in node.Neighbors ?? System.Array.Empty<string>())
			{
				if (!Nodes.ContainsKey(n))
				{
					GD.PushError($"Узел {node.Id}: неизвестный сосед {n}");
					continue;
				}
				Links[node.Id].Add(n);
				Links[n].Add(node.Id);
			}
		}

		if (StartId == null || !Nodes.ContainsKey(StartId)) return;
		var queue = new Queue<string>();
		Depth[StartId] = 0;
		queue.Enqueue(StartId);
		while (queue.Count > 0)
		{
			var cur = queue.Dequeue();
			foreach (var n in Links[cur])
			{
				if (Depth.ContainsKey(n)) continue;
				Depth[n] = Depth[cur] + 1;
				queue.Enqueue(n);
			}
		}
	}

	public AtlasNodeData Get(string id) => id != null && Nodes.TryGetValue(id, out var n) ? n : null;

	public int DepthOf(string id) => Depth.GetValueOrDefault(id, 0);

	public AtlasNodeState StateOf(string id, ISet<string> completed)
	{
		if (id == StartId || completed.Contains(id)) return AtlasNodeState.Completed;
		foreach (var n in Links[id])
			if (n == StartId || completed.Contains(n)) return AtlasNodeState.Available;
		return AtlasNodeState.Locked;
	}

	public IEnumerable<(AtlasNodeData a, AtlasNodeData b)> Edges()
	{
		foreach (var (id, set) in Links)
			foreach (var n in set)
				if (string.CompareOrdinal(id, n) < 0) yield return (Nodes[id], Nodes[n]);
	}

	public IEnumerable<AtlasNodeData> Ordered() =>
		Nodes.Values.OrderBy(n => n.Branch).ThenBy(n => DepthOf(n.Id));
}
