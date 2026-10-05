using System;
using System.Globalization;

namespace Project_Sinmai;

internal sealed class LoomRow
{
	public uint v0 = 258u;

	public LoomKind v1;

	public string v2 = "1.00.00";

	public DateTime v3 = DateTime.Now;

	public string v4 = "111.01.01";

	public string v5 = "1.00.00";

	public DateTime v6 = new DateTime(2000, 1, 1);

	public string v7 = "111.01.01";

	public string N0(string a, string b)
	{
		string value = v3.ToString("yyyyMMddHHmmss");
		return v1 switch
		{
			LoomKind.P0 => $"{b}_{v2}_{value}_0.pack", 
			LoomKind.P1 => $"{a}_{v2}_{value}_0.app", 
			LoomKind.P2 => $"{a}_{v2}_{value}_0.opt", 
			_ => $"{a}_{v2}_{value}_1_{v5}.app", 
		} + S0(v0);
	}

	public static string S0(uint m)
	{
		uint num = m & 0xFFFF;
		return num switch
		{
			258u => "", 
			257u => "#installing", 
			513u => "#downloading", 
			514u => "#staged", 
			_ => "#" + (((num >> 8) | (num << 8)) & 0xFFFF).ToString("x4"), 
		};
	}

	public static uint S1(string? s)
	{
		string text = (s ?? "").Trim();
		if (text.Length == 0)
		{
			return 258u;
		}
		switch (text)
		{
		case "#installing":
			return 257u;
		case "#downloading":
			return 513u;
		case "#staged":
			return 514u;
		default:
		{
			if (text.Length == 5 && text[0] == '#' && ushort.TryParse(text.Substring(1), NumberStyles.HexNumber, null, out var result))
			{
				return (uint)(((result >> 8) | (result << 8)) & 0xFFFF);
			}
			throw new FormatException(Word.T("loom.sfx"));
		}
		}
	}
}
