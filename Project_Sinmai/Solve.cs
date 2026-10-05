using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace Project_Sinmai;

public class Solve
{
	public class Config
	{
		public string Name { get; set; } = "";

		public string Type { get; set; } = "";

		public string Version { get; set; } = "";

		public string Serial { get; set; } = "";

		public string ClientId { get; set; } = "";

		public string InstructionUrl { get; set; } = "";

		public string InstructionUa { get; set; } = "";

		public string DownloadUa { get; set; } = "";

		public byte[] Key { get; set; } = new byte[16];

		public byte[] Iv { get; set; } = new byte[16];

		public string DownloadPath { get; set; } = "";

		public string SinmaiPath { get; set; } = "";
	}

	private sealed class NaturalComparer : IComparer<string>
	{
		public static readonly NaturalComparer Instance = new NaturalComparer();

		public int Compare(string? x, string? y)
		{
			if (x == null)
			{
				if (y != null)
				{
					return -1;
				}
				return 0;
			}
			if (y == null)
			{
				return 1;
			}
			string[] array = Regex.Split(x, "(\\d+)");
			string[] array2 = Regex.Split(y, "(\\d+)");
			for (int i = 0; i < Math.Min(array.Length, array2.Length); i++)
			{
				string text = array[i];
				string text2 = array2[i];
				if (text.Length != 0 || text2.Length != 0)
				{
					bool num = text.Length > 0 && text.All(char.IsDigit);
					bool flag = text2.Length > 0 && text2.All(char.IsDigit);
					int num2 = ((num & flag) ? CompareNumeric(text, text2) : string.Compare(text, text2, StringComparison.OrdinalIgnoreCase));
					if (num2 != 0)
					{
						return num2;
					}
				}
			}
			return array.Length.CompareTo(array2.Length);
		}

		private static int CompareNumeric(string a, string b)
		{
			string text = a.TrimStart('0');
			string text2 = b.TrimStart('0');
			if (text.Length != text2.Length)
			{
				return text.Length.CompareTo(text2.Length);
			}
			int num = string.CompareOrdinal(text, text2);
			if (num != 0)
			{
				return num;
			}
			return a.Length.CompareTo(b.Length);
		}
	}

	internal static readonly HttpClient Http = CreateHttpClient();

	public Config Cfg { get; set; } = TitleProfiles.Create("SDGB");

	public Func<List<string>, string?>? UrlChooser { get; set; }

	private static HttpClient CreateHttpClient()
	{
		return new HttpClient(new SocketsHttpHandler
		{
			PooledConnectionLifetime = TimeSpan.Zero,
			AutomaticDecompression = (DecompressionMethods.GZip | DecompressionMethods.Deflate)
		})
		{
			Timeout = Timeout.InfiniteTimeSpan
		};
	}

	private string FixUrl(string u)
	{
		try
		{
			u = Regex.Replace(u, "^tps://|^ps://", "https://");
			u = Regex.Replace(u, "^ttp://", "http://");
			Uri uri = new Uri(u);
			string value = uri.Host.Replace('_', '.');
			string input = Regex.Replace(uri.AbsolutePath, "(patch|option)_(\\d+)_(\\d+)", "$1_$2.$3");
			input = Regex.Replace(input, "_([a-zA-Z0-9]+)$", ".$1");
			return $"{uri.Scheme}://{value}:{uri.Port}{input}{uri.Query}";
		}
		catch
		{
			return u;
		}
	}

	public async Task<List<FileItem>> GetFileListAsync()
	{
		_ = 3;
		try
		{
			string query = ((!(Cfg.Type == "plain")) ? (await Pipe.RequestAsync(Cfg)) : (await Feed.RequestAsync(Cfg)));
			List<string> list = (from x in (HttpUtility.ParseQueryString(query)["uri"] ?? "").Split('|')
				select x.Trim() into x
				where !string.IsNullOrEmpty(x) && x.ToLower() != "null"
				select x).Select(FixUrl).ToList();
			if (list.Count == 0)
			{
				throw new Exception(Word.T("solve.noUrl"));
			}
			string text;
			if (list.Count == 1)
			{
				text = list[0];
			}
			else
			{
				if (UrlChooser == null)
				{
					throw new Exception(Word.T("solve.multiUrl", list.Count));
				}
				string text2 = UrlChooser(list);
				if (string.IsNullOrEmpty(text2))
				{
					throw new Exception(Word.T("solve.noPick"));
				}
				text = text2;
			}
			string path = text.Split('/').Last().Split('?')[0];
			string mainFilePath = Path.Combine(Cfg.DownloadPath, path);
			await DownloadSingleFileAsync(text, mainFilePath);
			string text3 = await File.ReadAllTextAsync(mainFilePath);
			string orderTime = ParseTimeField(text3, "ORDER_TIME");
			string releaseTime = ParseTimeField(text3, "RELEASE_TIME");
			int num = text3.IndexOf("[OPTIONAL]", StringComparison.OrdinalIgnoreCase);
			List<(string, string, bool)> list2 = new List<(string, string, bool)>();
			foreach (Match item in Regex.Matches(text3, "(?m)^\\s*INSTALL\\d+=\\s*(https?://\\S+)"))
			{
				string value = item.Groups[1].Value;
				string text4 = value.Split('/').Last();
				if (!string.IsNullOrWhiteSpace(text4) && !(text4 == "null") && (!text4.Contains("?") || text4.Split('?')[0].Length != 0))
				{
					list2.Add((text4, value, num < 0 || item.Index < num));
				}
			}
			list2 = list2.OrderBy<(string, string, bool), string>(((string Name, string Url, bool IsLatest) x) => x.Name, NaturalComparer.Instance).ToList();
			return list2.Select<(string, string, bool), FileItem>(((string Name, string Url, bool IsLatest) p) => new FileItem
			{
				Name = p.Name,
				Url = p.Url,
				OrderTime = (p.IsLatest ? orderTime : ""),
				ReleaseTime = (p.IsLatest ? releaseTime : "")
			}).ToList();
		}
		catch (Exception ex)
		{
			throw new Exception(Word.T("solve.listFail", ex.Message));
		}
	}

	private static string ParseTimeField(string text, string key)
	{
		Match match = Regex.Match(text, "(?m)^\\s*" + key + "\\s*=\\s*([^\\r\\n]+)");
		if (!match.Success)
		{
			return "";
		}
		string text2 = match.Groups[1].Value.Trim();
		int num = text2.IndexOf(';');
		if (num >= 0)
		{
			text2 = text2.Substring(0, num).Trim();
		}
		if (DateTime.TryParse(text2, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
		{
			return result.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
		}
		return text2;
	}

	public async Task<bool> DownloadSingleFileAsync(string url, string filePath)
	{
		try
		{
			Progress<(double, double)> progress = new Progress<(double, double)>(delegate
			{
			});
			return await DownloadWithProgressAsync(url, filePath, progress);
		}
		catch
		{
			return false;
		}
	}

	public async Task<bool> DownloadWithProgressAsync(string url, string filePath, IProgress<(double percentage, double speed)>? progress = null)
	{
		string tempPath = filePath + ".tmp";
		bool success = false;
		try
		{
			success = await DownloadSegmentedAsync(url, tempPath, progress);
			if (!success)
			{
				try
				{
					if (File.Exists(tempPath))
					{
						File.Delete(tempPath);
					}
				}
				catch
				{
				}
				success = await DownloadWithRetryAsync(url, tempPath, progress);
			}
		}
		catch
		{
			success = false;
		}
		finally
		{
			if (success)
			{
				try
				{
					if (File.Exists(filePath))
					{
						File.Delete(filePath);
					}
					File.Move(tempPath, filePath);
				}
				catch
				{
					success = false;
				}
			}
			if (File.Exists(tempPath))
			{
				try
				{
					File.Delete(tempPath);
				}
				catch
				{
				}
			}
		}
		return success;
	}

	private async Task<bool> DownloadSegmentedAsync(string url, string filePath, IProgress<(double percentage, double speed)>? progress)
	{
		try
		{
			long totalBytes = await ProbeFileSizeAsync(url);
			if (totalBytes <= 0)
			{
				return false;
			}
			long num = 2097152L;
			int num2 = (int)Math.Min(4L, (totalBytes + num - 1) / num);
			if (num2 < 1)
			{
				num2 = 1;
			}
			long num3 = (totalBytes + num2 - 1) / num2;
			using (FileStream fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
			{
				fileStream.SetLength(totalBytes);
			}
			List<(long, long)> list = new List<(long, long)>();
			for (int i = 0; i < num2; i++)
			{
				long num4 = i * num3;
				long item = Math.Min(num4 + num3 - 1, totalBytes - 1);
				list.Add((num4, item));
			}
			long[] received = new long[1];
			CancellationTokenSource cts = new CancellationTokenSource();
			try
			{
				Task reporter = Task.Run(async delegate
				{
					Stopwatch watch = Stopwatch.StartNew();
					double lastSeconds = 0.0;
					long lastBytes = 0L;
					double totalSpeed = 0.0;
					int samples = 0;
					try
					{
						while (true)
						{
							await Task.Delay(500, cts.Token);
							long num5 = Interlocked.Read(in received[0]);
							double num6 = watch.Elapsed.TotalSeconds - lastSeconds;
							if (!(num6 <= 0.0))
							{
								double num7 = (double)(num5 - lastBytes) / num6;
								totalSpeed = ((samples == 0) ? num7 : (totalSpeed * 0.6 + num7 * 0.4));
								samples++;
								lastSeconds = watch.Elapsed.TotalSeconds;
								lastBytes = num5;
								double item2 = (double)num5 / (double)totalBytes;
								progress?.Report((item2, totalSpeed));
							}
						}
					}
					catch (OperationCanceledException)
					{
					}
				});
				bool[] results = await Task.WhenAll(list.Select<(long, long), Task<bool>>(((long Start, long End) s) => DownloadSegmentAsync(url, filePath, s.Start, s.End, received)));
				cts.Cancel();
				try
				{
					await reporter;
				}
				catch
				{
				}
				if (results.All((bool r) => r))
				{
					return true;
				}
			}
			finally
			{
				if (cts != null)
				{
					((IDisposable)cts).Dispose();
				}
			}
		}
		catch
		{
		}
		try
		{
			if (File.Exists(filePath))
			{
				File.Delete(filePath);
			}
		}
		catch
		{
		}
		return false;
	}

	private async Task<bool> DownloadSegmentAsync(string url, string filePath, long segStart, long segEnd, long[] received)
	{
		long written = 0L;
		int bytesRead = default(int);
		for (int attempt = 1; attempt <= 3; attempt++)
		{
			try
			{
				using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url))
				{
					if (!string.IsNullOrWhiteSpace(Cfg.DownloadUa))
					{
						request.Headers.TryAddWithoutValidation("User-Agent", Cfg.DownloadUa);
					}
					request.Headers.Range = new RangeHeaderValue(segStart + written, segEnd);
					using HttpResponseMessage response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
					if (response.StatusCode != HttpStatusCode.PartialContent)
					{
						return false;
					}
					await using FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Write, FileShare.Write);
					fs.Seek(segStart + written, SeekOrigin.Begin);
					await using Stream stream = await response.Content.ReadAsStreamAsync();
					byte[] buffer = new byte[81920];
					while (true)
					{
						bool flag = segStart + written <= segEnd;
						if (flag)
						{
							int num;
							bytesRead = (num = await stream.ReadAsync(buffer));
							flag = num > 0;
						}
						if (flag)
						{
							int toWrite = (int)Math.Min(bytesRead, segEnd + 1 - segStart - written);
							await fs.WriteAsync(buffer.AsMemory(0, toWrite));
							written += toWrite;
							Interlocked.Add(ref received[0], toWrite);
							continue;
						}
						break;
					}
				}
				if (segStart + written == segEnd + 1)
				{
					return true;
				}
			}
			catch
			{
			}
			if (attempt < 3)
			{
				await Task.Delay(2000);
			}
		}
		return false;
	}

	private async Task<long> ProbeFileSizeAsync(string url)
	{
		try
		{
			using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Head, url);
			if (!string.IsNullOrWhiteSpace(Cfg.DownloadUa))
			{
				request.Headers.TryAddWithoutValidation("User-Agent", Cfg.DownloadUa);
			}
			using HttpResponseMessage httpResponseMessage = await Http.SendAsync(request);
			if (!httpResponseMessage.IsSuccessStatusCode)
			{
				return 0L;
			}
			return httpResponseMessage.Content.Headers.ContentLength.GetValueOrDefault();
		}
		catch
		{
			return 0L;
		}
	}

	private async Task<bool> DownloadWithRetryAsync(string url, string filePath, IProgress<(double percentage, double speed)>? progress)
	{
		int maxRetries = 3;
		for (int attempt = 1; attempt <= maxRetries; attempt++)
		{
			try
			{
				long alreadyDownloaded = (File.Exists(filePath) ? new FileInfo(filePath).Length : 0);
				using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url))
				{
					if (!string.IsNullOrWhiteSpace(Cfg.DownloadUa))
					{
						request.Headers.TryAddWithoutValidation("User-Agent", Cfg.DownloadUa);
					}
					if (alreadyDownloaded > 0)
					{
						request.Headers.Range = new RangeHeaderValue(alreadyDownloaded, null);
					}
					using HttpResponseMessage response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
					if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
					{
						return true;
					}
					response.EnsureSuccessStatusCode();
					long totalBytes = alreadyDownloaded + response.Content.Headers.ContentLength.GetValueOrDefault();
					await using FileStream fs = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.None);
					await using Stream stream = await response.Content.ReadAsStreamAsync();
					byte[] buffer = new byte[81920];
					long currentDownloaded = alreadyDownloaded;
					long bytesSinceLastReport = 0L;
					double totalSpeed = 0.0;
					int speedSamples = 0;
					Stopwatch watch = Stopwatch.StartNew();
					double lastReportSeconds = 0.0;
					while (true)
					{
						int num;
						int bytesRead = (num = await stream.ReadAsync(buffer));
						if (num <= 0)
						{
							break;
						}
						await fs.WriteAsync(buffer.AsMemory(0, bytesRead));
						currentDownloaded += bytesRead;
						bytesSinceLastReport += bytesRead;
						double num2 = watch.Elapsed.TotalSeconds - lastReportSeconds;
						if (num2 >= 0.5)
						{
							double num3 = (double)bytesSinceLastReport / num2;
							totalSpeed = ((speedSamples == 0) ? num3 : (totalSpeed * 0.6 + num3 * 0.4));
							speedSamples++;
							lastReportSeconds = watch.Elapsed.TotalSeconds;
							bytesSinceLastReport = 0L;
							double item = ((totalBytes > 0) ? ((double)currentDownloaded / (double)totalBytes) : 0.0);
							progress?.Report((item, totalSpeed));
						}
					}
				}
				return true;
			}
			catch
			{
				if (attempt < maxRetries)
				{
					await Task.Delay(2000);
				}
			}
		}
		return false;
	}
}
