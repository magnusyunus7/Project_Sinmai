using System;
using System.Globalization;

namespace Project_Sinmai;

public static class Word
{
	private static LangKind _current;

	public static LangKind Current => _current;

	public static event Action? Changed;

	public static string Code(LangKind kind)
	{
		return kind switch
		{
			LangKind.ZhTw => "zh-TW", 
			LangKind.En => "en", 
			LangKind.Ja => "ja", 
			_ => "zh-CN", 
		};
	}

	public static LangKind Parse(string? code)
	{
		string text = (code ?? "").Trim().ToLowerInvariant();
		if (text.StartsWith("zh"))
		{
			if (text.Contains("tw") || text.Contains("hk") || text.Contains("mo") || text.Contains("hant"))
			{
				return LangKind.ZhTw;
			}
			return LangKind.ZhCn;
		}
		if (text.StartsWith("ja"))
		{
			return LangKind.Ja;
		}
		text.StartsWith("en");
		return LangKind.En;
	}

	public static LangKind Detect()
	{
		try
		{
			string text = (TimeZoneInfo.Local.Id ?? "").ToLowerInvariant();
			if (text.Contains("taipei") || text.Contains("hong") || text.Contains("macau") || text.Contains("macao"))
			{
				return LangKind.ZhTw;
			}
			if (text.Contains("tokyo") || text.Contains("japan"))
			{
				return LangKind.Ja;
			}
			if (text.Contains("china") || text.Contains("shanghai") || text.Contains("chongqing") || text.Contains("urumqi") || text.Contains("harbin") || text.Contains("kashgar"))
			{
				return LangKind.ZhCn;
			}
		}
		catch
		{
		}
		return LangKind.En;
	}

	public static void Init(LangKind kind)
	{
		_current = kind;
		Loc.Instance.Refresh();
	}

	public static void Set(LangKind kind)
	{
		_current = kind;
		Loc.Instance.Refresh();
		Changed?.Invoke();
	}

	public static string T(string key)
	{
		if (Say.Table.TryGetValue(key, out string[] value) && value != null)
		{
			int current = (int)_current;
			if (current >= 0 && current < value.Length && value[current].Length > 0)
			{
				return value[current];
			}
			if (value.Length != 0)
			{
				return value[0];
			}
		}
		return key;
	}

	public static string T(string key, params object?[] args)
	{
		return string.Format(CultureInfo.InvariantCulture, T(key), args);
	}
}
