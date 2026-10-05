using System;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Project_Sinmai;

public static class Pipe
{
	public static Solve.Config CreateConfig()
	{
		Solve.Config config = new Solve.Config();
		config.Name = "SDGB";
		config.Type = "aes";
		config.Version = "";
		config.ClientId = "";
		config.InstructionUrl = "http://at.sys-allnet.cn/net/delivery/instruction";
		config.InstructionUa = "SDGB;Windows/Lite";
		config.DownloadUa = "";
		config.Key = new byte[16]
		{
			47, 63, 106, 111, 43, 34, 76, 38, 92, 67,
			114, 57, 40, 61, 107, 71
		};
		config.Iv = new byte[16];
		return config;
	}

	public static async Task<string> RequestAsync(Solve.Config cfg)
	{
		string s = $"title_id={cfg.Name}&title_ver={cfg.Version}&client_id={cfg.ClientId}";
		byte[] bytes = Encoding.UTF8.GetBytes(s);
		byte[] array = new byte[16 + bytes.Length];
		Array.Copy(bytes, 0, array, 16, bytes.Length);
		byte[] content = AesCrypt(array, cfg.Key, cfg.Iv, encrypt: true);
		HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, cfg.InstructionUrl);
		httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", cfg.InstructionUa);
		httpRequestMessage.Headers.TryAddWithoutValidation("Pragma", "DFI");
		httpRequestMessage.Content = new ByteArrayContent(content);
		HttpResponseMessage obj = await Solve.Http.SendAsync(httpRequestMessage);
		obj.EnsureSuccessStatusCode();
		byte[] obj2 = await obj.Content.ReadAsByteArrayAsync();
		if (obj2.Length < 16)
		{
			throw new Exception(Word.T("pipe.shortResp"));
		}
		byte[] bytes2 = AesCrypt(iv: obj2.Take(16).ToArray(), data: obj2.Skip(16).ToArray(), key: cfg.Key, encrypt: false);
		return Uri.UnescapeDataString(Encoding.UTF8.GetString(bytes2));
	}

	private static byte[] AesCrypt(byte[] data, byte[] key, byte[] iv, bool encrypt)
	{
		try
		{
			using Aes aes = Aes.Create();
			aes.Key = key;
			aes.IV = iv;
			aes.Mode = CipherMode.CBC;
			aes.Padding = PaddingMode.PKCS7;
			using ICryptoTransform cryptoTransform = (encrypt ? aes.CreateEncryptor() : aes.CreateDecryptor());
			return cryptoTransform.TransformFinalBlock(data, 0, data.Length);
		}
		catch
		{
			return new byte[0];
		}
	}
}
