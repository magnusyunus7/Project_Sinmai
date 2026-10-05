using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SEGADownloadTool_Sinmai;

public static class ConfigStore
{
	public static string FilePath { get; } = Path.Combine(AppContext.BaseDirectory, "sdtconfig.cfg");

	public static void Save(bool persist, string game, Dictionary<string, Solve.Config> configs, LangKind lang)
	{
		try
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine("[General]");
			stringBuilder.AppendLine("persist=" + (persist ? "1" : "0"));
			stringBuilder.AppendLine("lang=" + Word.Code(lang));
			if (persist)
			{
				stringBuilder.AppendLine("game=" + game);
				foreach (KeyValuePair<string, Solve.Config> config in configs)
				{
					stringBuilder.AppendLine();
					stringBuilder.AppendLine("[" + config.Key + "]");
					stringBuilder.AppendLine("Version=" + config.Value.Version);
					stringBuilder.AppendLine("Serial=" + config.Value.Serial);
					stringBuilder.AppendLine("ClientId=" + config.Value.ClientId);
					stringBuilder.AppendLine("InstructionUrl=" + config.Value.InstructionUrl);
					stringBuilder.AppendLine("InstructionUa=" + config.Value.InstructionUa);
					stringBuilder.AppendLine("DownloadUa=" + config.Value.DownloadUa);
					stringBuilder.AppendLine("DownloadPath=" + config.Value.DownloadPath);
					stringBuilder.AppendLine("SinmaiPath=" + config.Value.SinmaiPath);
				}
			}
			File.WriteAllText(FilePath, stringBuilder.ToString(), Encoding.UTF8);
		}
		catch
		{
		}
	}

	public static void Delete()
	{
		try
		{
			if (File.Exists(FilePath))
			{
				File.Delete(FilePath);
			}
		}
		catch
		{
		}
	}

	public static bool TryLoad(out bool persist, out string game, Dictionary<string, Solve.Config> configs, out LangKind lang)
	{
		persist = false;
		game = "SDGB";
		lang = Word.Detect();
		try
		{
			if (!File.Exists(FilePath))
			{
				return false;
			}
			string text = "";
			string[] array = File.ReadAllLines(FilePath, Encoding.UTF8);
			for (int i = 0; i < array.Length; i++)
			{
				string text2 = array[i].Trim();
				if (text2.Length == 0)
				{
					continue;
				}
				if (text2.StartsWith("[") && text2.EndsWith("]"))
				{
					text = text2.Substring(1, text2.Length - 2).Trim();
					continue;
				}
				int num = text2.IndexOf('=');
				if (num <= 0)
				{
					continue;
				}
				string text3 = text2.Substring(0, num).Trim();
				string text4 = text2.Substring(num + 1).Trim();
				Solve.Config value;
				if (text == "General")
				{
					switch (text3)
					{
					case "persist":
						persist = text4 == "1";
						break;
					case "game":
						game = text4;
						break;
					case "lang":
						lang = Word.Parse(text4);
						break;
					}
				}
				else if (configs.TryGetValue(text, out value) && value != null)
				{
					switch (text3)
					{
					case "Version":
						value.Version = text4;
						break;
					case "Serial":
						value.Serial = text4;
						break;
					case "ClientId":
						value.ClientId = text4;
						break;
					case "InstructionUrl":
						value.InstructionUrl = text4;
						break;
					case "InstructionUa":
						value.InstructionUa = text4;
						break;
					case "DownloadUa":
						value.DownloadUa = text4;
						break;
					case "DownloadPath":
						value.DownloadPath = text4;
						break;
					case "SinmaiPath":
						value.SinmaiPath = text4;
						break;
					}
				}
			}
			return true;
		}
		catch
		{
			return false;
		}
	}
}
