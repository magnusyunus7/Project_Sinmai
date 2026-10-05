using System;
using System.Security.Cryptography;

namespace SEGADownloadTool_Sinmai;

internal static class Mint
{
	private static readonly byte[] f0 = new byte[8] { 62, 253, 105, 202, 7, 254, 157, 216 };

	private static readonly byte[] f1 = new byte[8] { 15, 51, 231, 144, 212, 205, 70, 23 };

	private static readonly byte[] f2 = new byte[8] { 134, 98, 245, 27, 25, 72, 51, 166 };

	private static readonly byte[] f3 = new byte[8] { 205, 72, 56, 235, 22, 77, 200, 167 };

	private static byte[]? f4;

	private static byte[]? f5;

	private static byte[] F2(byte[] a, byte[] b)
	{
		byte[] array = new byte[a.Length + b.Length];
		for (int i = 0; i < a.Length; i++)
		{
			array[i] = (byte)(a[i] ^ 0x37);
		}
		for (int j = 0; j < b.Length; j++)
		{
			array[a.Length + j] = (byte)(b[j] ^ 0x37);
		}
		return array;
	}

	public static uint F0(ReadOnlySpan<byte> d)
	{
		uint num = uint.MaxValue;
		for (int i = 0; i < d.Length; i++)
		{
			num ^= d[i];
			for (int j = 0; j < 8; j++)
			{
				num = (num >> 1) ^ (0xEDB88320u & (0 - (num & 1)));
			}
		}
		return ~num;
	}

	public static byte[] F1(byte[] d, bool enc)
	{
		if (d.Length == 0 || (d.Length & 0xF) != 0)
		{
			throw new InvalidOperationException(Word.T("mint.badLen"));
		}
		if (f4 == null)
		{
			f4 = F2(f0, f1);
		}
		if (f5 == null)
		{
			f5 = F2(f2, f3);
		}
		using Aes aes = Aes.Create();
		aes.Key = f4;
		aes.IV = f5;
		aes.Mode = CipherMode.CBC;
		aes.Padding = PaddingMode.None;
		using ICryptoTransform cryptoTransform = (enc ? aes.CreateEncryptor() : aes.CreateDecryptor());
		return cryptoTransform.TransformFinalBlock(d, 0, d.Length);
	}
}
