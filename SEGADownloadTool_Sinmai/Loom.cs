using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SEGADownloadTool_Sinmai;

internal sealed class Loom
{
	public string e0 = "SDEZ";

	public string e1 = "ACA";

	public List<LoomRow> e2 = new List<LoomRow>();

	public static Loom E0(string p)
	{
		byte[] array = File.ReadAllBytes(p);
		if (array.Length < 128 || array.Length % 16 != 0)
		{
			throw new InvalidDataException(Word.T("loom.invalidLen"));
		}
		byte[] array2 = Mint.F1(array, enc: false);
		uint num = BinaryPrimitives.ReadUInt32LittleEndian(array2.AsSpan(4));
		uint num2 = BinaryPrimitives.ReadUInt32LittleEndian(array2.AsSpan(16));
		if (num != (uint)array2.Length || 64 + (long)num2 * 64L > array2.Length)
		{
			throw new InvalidDataException(Word.T("loom.badHeader"));
		}
		if (BinaryPrimitives.ReadUInt32LittleEndian(array2) != Mint.F0(array2.AsSpan(4)))
		{
			throw new InvalidDataException(Word.T("loom.crcMain"));
		}
		uint num3 = 0u;
		for (int i = 0; i < (int)num2; i++)
		{
			ReadOnlySpan<byte> readOnlySpan = array2.AsSpan(64 + i * 64, 64);
			if (BinaryPrimitives.ReadUInt32LittleEndian(readOnlySpan) == 258)
			{
				num3 ^= Mint.F0(readOnlySpan);
			}
		}
		if (num3 != BinaryPrimitives.ReadUInt32LittleEndian(array2.AsSpan(32)))
		{
			throw new InvalidDataException(Word.T("loom.crcSub"));
		}
		string text = Encoding.ASCII.GetString(array2, 24, 7);
		Loom loom = new Loom
		{
			e0 = text.Substring(0, 4).TrimEnd('\0'),
			e1 = text.Substring(4).TrimEnd('\0')
		};
		for (int j = 0; j < (int)num2; j++)
		{
			loom.e2.Add(E2(array2.AsSpan(64 + j * 64, 64)));
		}
		return loom;
	}

	public void E1(string p)
	{
		if (e0.Length != 4 || e1.Length != 3 || !e0.All(Q) || !e1.All(Q))
		{
			throw new InvalidDataException(Word.T("loom.idLen"));
		}
		byte[] array = new byte[64 + e2.Count * 64];
		BinaryPrimitives.WriteUInt32LittleEndian(array.AsSpan(4), (uint)array.Length);
		BinaryPrimitives.WriteUInt32LittleEndian(array.AsSpan(16), (uint)e2.Count);
		Encoding.ASCII.GetBytes(e0 + e1).CopyTo(array, 24);
		uint num = 0u;
		for (int i = 0; i < e2.Count; i++)
		{
			byte[] array2 = E3(e2[i]);
			array2.CopyTo(array, 64 + i * 64);
			if (e2[i].v0 == 258)
			{
				num ^= Mint.F0(array2);
			}
		}
		BinaryPrimitives.WriteUInt32LittleEndian(array.AsSpan(32), num);
		BinaryPrimitives.WriteUInt32LittleEndian(array, Mint.F0(array.AsSpan(4)));
		File.WriteAllBytes(p, Mint.F1(array, enc: true));
	}

	private static LoomRow E2(ReadOnlySpan<byte> r)
	{
		LoomRow loomRow = new LoomRow();
		loomRow.v0 = BinaryPrimitives.ReadUInt32LittleEndian(r);
		uint num = BinaryPrimitives.ReadUInt32LittleEndian(r.Slice(4, 4));
		LoomRow loomRow2 = loomRow;
		loomRow2.v1 = num switch
		{
			0u => LoomKind.P0, 
			1u => LoomKind.P1, 
			2u => LoomKind.P2, 
			257u => LoomKind.P3, 
			513u => LoomKind.P4, 
			_ => throw new InvalidDataException(Word.T("loom.badKind", num)), 
		};
		if (loomRow.v1 == LoomKind.P2)
		{
			loomRow.v2 = Encoding.ASCII.GetString(r.Slice(32, 4)).TrimEnd('\0');
			loomRow.v4 = "";
		}
		else
		{
			loomRow.v2 = E4(r.Slice(32, 3));
			loomRow.v4 = E4(r.Slice(44, 3));
		}
		loomRow.v3 = E6(r.Slice(36, 7));
		LoomKind v = loomRow.v1;
		if ((uint)(v - 3) <= 1u)
		{
			loomRow.v5 = E4(r.Slice(48, 3));
			loomRow.v6 = E6(r.Slice(52, 7));
			loomRow.v7 = E4(r.Slice(60, 3));
		}
		return loomRow;
	}

	private static byte[] E3(LoomRow x)
	{
		byte[] array = new byte[64];
		BinaryPrimitives.WriteUInt32LittleEndian(array, x.v0);
		Span<byte> destination = array.AsSpan(4);
		BinaryPrimitives.WriteUInt32LittleEndian(destination, x.v1 switch
		{
			LoomKind.P0 => 0u, 
			LoomKind.P1 => 1u, 
			LoomKind.P2 => 2u, 
			LoomKind.P3 => 257u, 
			_ => 513u, 
		});
		if (x.v1 == LoomKind.P2)
		{
			int length = x.v2.Length;
			bool flag = ((length < 1 || length > 4) ? true : false);
			if (flag || !x.v2.All(Q))
			{
				throw new FormatException(Word.T("loom.badOpt", x.v2));
			}
			Encoding.ASCII.GetBytes(x.v2).CopyTo(array, 32);
			E7(array, 36, x.v3);
		}
		else
		{
			E5(array, 32, x.v2);
			E7(array, 36, x.v3);
			E5(array, 44, x.v4);
		}
		LoomKind v = x.v1;
		if ((uint)(v - 3) <= 1u)
		{
			E5(array, 48, x.v5);
			E7(array, 52, x.v6);
			E5(array, 60, x.v7);
		}
		return array;
	}

	private static string E4(ReadOnlySpan<byte> x)
	{
		return $"{x[2]}.{x[1]:00}.{x[0]:00}";
	}

	private static void E5(byte[] b, int o, string s)
	{
		string[] array = s.Split('.');
		if (array.Length != 3 || !byte.TryParse(array[0], out var result) || !byte.TryParse(array[1], out var result2) || !byte.TryParse(array[2], out var result3))
		{
			throw new FormatException(Word.T("loom.badVer", s));
		}
		b[o] = result3;
		b[o + 1] = result2;
		b[o + 2] = result;
	}

	private static DateTime E6(ReadOnlySpan<byte> x)
	{
		try
		{
			return new DateTime(BinaryPrimitives.ReadUInt16LittleEndian(x), x[2], x[3], x[4], x[5], x[6]);
		}
		catch
		{
			return new DateTime(2000, 1, 1);
		}
	}

	private static void E7(byte[] b, int o, DateTime t)
	{
		BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(o), (ushort)t.Year);
		b[o + 2] = (byte)t.Month;
		b[o + 3] = (byte)t.Day;
		b[o + 4] = (byte)t.Hour;
		b[o + 5] = (byte)t.Minute;
		b[o + 6] = (byte)t.Second;
	}

	private static bool Q(char c)
	{
		if (c >= ' ')
		{
			return c <= '~';
		}
		return false;
	}
}
