using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Project_Sinmai;

public static class Peel
{
	public static bool Z0(string fileName)
	{
		if (!fileName.EndsWith(".app", StringComparison.OrdinalIgnoreCase) && !fileName.EndsWith(".opt", StringComparison.OrdinalIgnoreCase))
		{
			return fileName.EndsWith(".os", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	public static async Task<(bool Attempted, bool Success, string? Error)> Go(string fileName, string filePath, Action<string>? log, string? baseFile = null, (byte[] key, byte[] iv)? keyHint = null)
	{
		if (!Z0(fileName))
		{
			return (Attempted: false, Success: false, Error: null);
		}
		try
		{
			log?.Invoke(Word.T("peel.decrypting", fileName));
			await Task.Run(delegate
			{
				Untie.Run(filePath, delegate(string msg)
				{
					if (!string.IsNullOrEmpty(msg))
					{
						log?.Invoke(msg);
					}
				}, baseFile, keyHint);
			});
			log?.Invoke(Word.T("peel.ok", fileName));
			return (Attempted: true, Success: true, Error: null);
		}
		catch (Exception ex)
		{
			log?.Invoke(Word.T("peel.err", fileName));
			return (Attempted: true, Success: false, Error: ex.Message);
		}
	}

	public static async Task<string?> Put(string optFilePath, string sinmaiRoot, Action<string>? log)
	{
		sinmaiRoot = (sinmaiRoot ?? "").Trim();
		if (string.IsNullOrWhiteSpace(sinmaiRoot) || !Directory.Exists(sinmaiRoot))
		{
			return null;
		}
		string crackedDir = Path.ChangeExtension(optFilePath, null);
		string text = await Task.Run(delegate
		{
			string text2 = LocateStreamingAssets(sinmaiRoot);
			if (text2 == null)
			{
				return Word.T("peel.noSa");
			}
			if (!Directory.Exists(crackedDir))
			{
				return Word.T("peel.noOutDir", Path.GetFileName(crackedDir));
			}
			Match match = Regex.Match(Path.GetFileName(crackedDir), "A\\d{3}(?!\\d)", RegexOptions.IgnoreCase);
			if (!match.Success)
			{
				return Word.T("peel.noAxxx", Path.GetFileName(crackedDir));
			}
			string text3 = match.Value.ToUpperInvariant();
			if (text3 == "A000")
			{
				return Word.T("peel.a000");
			}
			try
			{
				string text4 = Path.Combine(text2, text3);
				if (Directory.Exists(text4))
				{
					Directory.Delete(text4, recursive: true);
				}
				MoveDirectory(crackedDir, text4);
				return Word.T("peel.deployed", Path.GetFileName(crackedDir), text3);
			}
			catch (Exception ex)
			{
				return Word.T("peel.deployFail", text3, ex.Message);
			}
		});
		if (!string.IsNullOrEmpty(text))
		{
			log?.Invoke(text);
		}
		return text;
	}

	private static string? LocateStreamingAssets(string root)
	{
		try
		{
			string fileName = Path.GetFileName(root.TrimEnd(new char[2]
			{
				Path.DirectorySeparatorChar,
				Path.AltDirectorySeparatorChar
			}));
			if (string.Equals(fileName, "StreamingAssets", StringComparison.OrdinalIgnoreCase))
			{
				return root;
			}
			if (string.Equals(fileName, "Sinmai_Data", StringComparison.OrdinalIgnoreCase))
			{
				return FindSubDirectory(root, "StreamingAssets", 3);
			}
			string text = FindSubDirectory(root, "Package", 3);
			if (text == null)
			{
				return null;
			}
			string text2 = FindSubDirectory(text, "Sinmai_Data", 3);
			if (text2 == null)
			{
				return null;
			}
			return FindSubDirectory(text2, "StreamingAssets", 3);
		}
		catch
		{
			return null;
		}
	}

	private static string? FindSubDirectory(string startDir, string name, int maxDepth)
	{
		Queue<(string, int)> queue = new Queue<(string, int)>();
		queue.Enqueue((startDir, 0));
		while (queue.Count > 0)
		{
			var (path, num) = queue.Dequeue();
			try
			{
				foreach (string item in Directory.EnumerateDirectories(path))
				{
					if (string.Equals(Path.GetFileName(item), name, StringComparison.OrdinalIgnoreCase))
					{
						return item;
					}
					if (num + 1 < maxDepth)
					{
						queue.Enqueue((item, num + 1));
					}
				}
			}
			catch
			{
			}
		}
		return null;
	}

	private static void MoveDirectory(string src, string dst)
	{
		string? a = Path.GetPathRoot(Path.GetFullPath(src)) ?? "";
		string b = Path.GetPathRoot(Path.GetFullPath(dst)) ?? "";
		if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
		{
			Directory.Move(src, dst);
			return;
		}
		CopyDirectory(src, dst);
		Directory.Delete(src, recursive: true);
	}

	private static void CopyDirectory(string src, string dst)
	{
		Directory.CreateDirectory(dst);
		foreach (string item in Directory.EnumerateFiles(src, "*", SearchOption.AllDirectories))
		{
			string text = Path.Combine(dst, Path.GetRelativePath(src, item));
			Directory.CreateDirectory(Path.GetDirectoryName(text));
			File.Copy(item, text, overwrite: true);
		}
	}
}
