using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace SEGADownloadTool_Sinmai;

public static class ScriptStore
{
	private const string Prefix = "sdtscript-";

	private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
	{
		WriteIndented = true,
		Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
	};

	public static string Folder { get; } = AppContext.BaseDirectory;

	public static string FileNameOf(ScriptTask task)
	{
		return $"{"sdtscript-"}{task.CreatedAt}-{task.Id}.json";
	}

	public static string FilePathOf(ScriptTask task)
	{
		return Path.Combine(Folder, FileNameOf(task));
	}

	public static List<ScriptTask> LoadAll()
	{
		List<ScriptTask> list = new List<ScriptTask>();
		try
		{
			foreach (string item in Directory.EnumerateFiles(Folder, "sdtscript-*.json"))
			{
				try
				{
					string json = File.ReadAllText(item, Encoding.UTF8);
					ScriptTask task = JsonSerializer.Deserialize<ScriptTask>(json, Options);
					if (task != null && !string.IsNullOrWhiteSpace(task.Id) && !string.IsNullOrWhiteSpace(task.CreatedAt) && !list.Any((ScriptTask x) => x.Id == task.Id))
					{
						if (task.IntervalSeconds <= 0)
						{
							task.IntervalSeconds = 300;
						}
						if (task.ListFiles == null)
						{
							task.ListFiles = new List<string>();
						}
						if (task.Status != "running")
						{
							task.Status = "stopped";
						}
						list.Add(task);
					}
				}
				catch
				{
				}
			}
		}
		catch
		{
		}
		return list.OrderBy<ScriptTask, string>((ScriptTask x) => x.Id, StringComparer.Ordinal).ToList();
	}

	public static bool Save(ScriptTask task, out string error)
	{
		error = "";
		try
		{
			File.WriteAllText(FilePathOf(task), JsonSerializer.Serialize(task, Options), Encoding.UTF8);
			return true;
		}
		catch (Exception ex)
		{
			error = ex.Message;
			return false;
		}
	}

	public static bool Delete(ScriptTask task, out string error)
	{
		error = "";
		try
		{
			string path = FilePathOf(task);
			if (File.Exists(path))
			{
				File.Delete(path);
			}
			return true;
		}
		catch (Exception ex)
		{
			error = ex.Message;
			return false;
		}
	}

	public static string NextId(IEnumerable<ScriptTask> existing)
	{
		HashSet<int> hashSet = new HashSet<int>();
		foreach (ScriptTask item in existing)
		{
			if (int.TryParse(item.Id, out var result))
			{
				hashSet.Add(result);
			}
		}
		for (int i = 1; i <= 999; i++)
		{
			if (!hashSet.Contains(i))
			{
				return i.ToString("D3");
			}
		}
		return "999";
	}
}
