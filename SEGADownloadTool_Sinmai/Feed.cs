using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SEGADownloadTool_Sinmai;

public static class Feed
{
	public static Solve.Config CreateConfig()
	{
		return new Solve.Config
		{
			Name = "SDGA",
			Type = "plain",
			Version = "",
			Serial = "",
			InstructionUrl = "http://naominet.jp/sys/servlet/DownloadOrder",
			InstructionUa = "ALL.Net",
			DownloadUa = ""
		};
	}

	public static async Task<string> RequestAsync(Solve.Config cfg)
	{
		string s = $"game_id={cfg.Name}&ver={cfg.Version}&serial={cfg.Serial}";
		byte[] inArray;
		using (MemoryStream memoryStream = new MemoryStream())
		{
			using (ZLibStream zLibStream = new ZLibStream(memoryStream, CompressionLevel.Optimal, leaveOpen: true))
			{
				byte[] bytes = Encoding.UTF8.GetBytes(s);
				zLibStream.Write(bytes, 0, bytes.Length);
			}
			inArray = memoryStream.ToArray();
		}
		string s2 = Convert.ToBase64String(inArray);
		HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, cfg.InstructionUrl);
		httpRequestMessage.Headers.TryAddWithoutValidation("Pragma", "DFI");
		httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", cfg.InstructionUa);
		httpRequestMessage.Headers.TryAddWithoutValidation("Accept-Encoding", "identity");
		httpRequestMessage.Content = new ByteArrayContent(Encoding.ASCII.GetBytes(s2));
		httpRequestMessage.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
		HttpResponseMessage obj = await Solve.Http.SendAsync(httpRequestMessage);
		obj.EnsureSuccessStatusCode();
		Encoding aSCII = Encoding.ASCII;
		byte[] buffer = Convert.FromBase64String(aSCII.GetString(await obj.Content.ReadAsByteArrayAsync()).Trim());
		using MemoryStream input = new MemoryStream(buffer);
		using ZLibStream decompressed = new ZLibStream(input, CompressionMode.Decompress);
		using StreamReader reader = new StreamReader(decompressed, Encoding.UTF8);
		return await reader.ReadToEndAsync();
	}

	public static async Task<IpRegion?> GetIpRegionAsync()
	{
		_ = 1;
		try
		{
			using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(10L));
			using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "https://ipinfo.io/json");
			using HttpResponseMessage response = await Solve.Http.SendAsync(request, cts.Token);
			if (!response.IsSuccessStatusCode)
			{
				return null;
			}
			using JsonDocument jsonDocument = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cts.Token));
			string text = ReadString(jsonDocument.RootElement, "country");
			if (string.IsNullOrWhiteSpace(text))
			{
				return null;
			}
			return new IpRegion
			{
				Ip = ReadString(jsonDocument.RootElement, "ip"),
				Region = ReadString(jsonDocument.RootElement, "region"),
				Country = text.Trim().ToUpperInvariant()
			};
		}
		catch
		{
			return null;
		}
	}

	private static string ReadString(JsonElement root, string name)
	{
		if (root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
		{
			return value.GetString() ?? "";
		}
		return "";
	}
}
