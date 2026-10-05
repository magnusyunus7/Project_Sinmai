using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Project_Sinmai;

public sealed class ScriptScheduler
{
	private const int MaxLogLines = 500;

	private readonly DispatcherTimer _timer = new DispatcherTimer
	{
		Interval = TimeSpan.FromSeconds(1L)
	};

	private readonly Dictionary<string, ScriptTask> _tasks = new Dictionary<string, ScriptTask>();

	private readonly Dictionary<string, List<string>> _logs = new Dictionary<string, List<string>>();

	private readonly Dictionary<string, SemaphoreSlim> _gates = new Dictionary<string, SemaphoreSlim>();

	public static ScriptScheduler Instance { get; } = new ScriptScheduler();

	public IReadOnlyList<ScriptTask> Tasks => _tasks.Values.OrderBy<ScriptTask, string>((ScriptTask x) => x.Id, StringComparer.Ordinal).ToList();

	public event Action<string, string>? LogAppended;

	public event Action? TasksChanged;

	private ScriptScheduler()
	{
		_timer.Tick += OnTick;
	}

	public void Start()
	{
		foreach (ScriptTask item in ScriptStore.LoadAll())
		{
			item.IsBusy = false;
			item.NextRunAt = DateTime.Now;
			_tasks[item.Id] = item;
			Append(item.Id, Word.T("tick.loaded", item.StatusText));
		}
		_timer.Start();
	}

	public void StopAll()
	{
		_timer.Stop();
	}

	public ScriptTask Create()
	{
		return new ScriptTask
		{
			Id = ScriptStore.NextId(_tasks.Values),
			CreatedAt = DateTime.Now.ToString("yyyyMMddHHmmss"),
			DownloadPath = MainWindow.GetDefaultDownloadPath()
		};
	}

	public bool TryGet(string id, out ScriptTask task)
	{
		bool result = _tasks.TryGetValue(id, out ScriptTask value);
		task = value;
		return result;
	}

	public IReadOnlyList<string> GetLog(string id)
	{
		if (!_logs.TryGetValue(id, out List<string> value))
		{
			return new List<string>();
		}
		return value.ToList();
	}

	public bool Add(ScriptTask task, out string error)
	{
		error = "";
		if (!ScriptStore.Save(task, out error))
		{
			return false;
		}
		task.IsBusy = false;
		task.NextRunAt = DateTime.Now;
		_tasks[task.Id] = task;
		_logs[task.Id] = new List<string>();
		Append(task.Id, Word.T("tick.created", task.Game, task.Version, task.IntervalText));
		RaiseTasksChanged();
		return true;
	}

	public bool Update(ScriptTask task, out string error)
	{
		error = "";
		if (!ScriptStore.Save(task, out error))
		{
			return false;
		}
		if (_tasks.TryGetValue(task.Id, out ScriptTask value))
		{
			task.IsBusy = value.IsBusy;
		}
		task.NextRunAt = DateTime.Now.AddSeconds(Math.Max(1, task.IntervalSeconds));
		_tasks[task.Id] = task;
		Append(task.Id, Word.T("tick.updated"));
		RaiseTasksChanged();
		return true;
	}

	public bool Delete(ScriptTask task, out string error)
	{
		error = "";
		if (!ScriptStore.Delete(task, out error))
		{
			return false;
		}
		_tasks.Remove(task.Id);
		_logs.Remove(task.Id);
		RaiseTasksChanged();
		return true;
	}

	public bool IsDuplicate(string game, string version, string downloadPath, string excludeId, out string message)
	{
		message = "";
		string b = NormalizePath(downloadPath);
		foreach (ScriptTask value in _tasks.Values)
		{
			if (!(value.Id == excludeId) && string.Equals(value.Game, game, StringComparison.OrdinalIgnoreCase) && string.Equals(value.Version, version, StringComparison.OrdinalIgnoreCase) && string.Equals(NormalizePath(value.DownloadPath), b, StringComparison.OrdinalIgnoreCase))
			{
				message = Word.T("tick.duplicate", value.Id, value.Game, value.Version, value.DownloadPath);
				return true;
			}
		}
		return false;
	}

	public void SetRunning(string id, bool running)
	{
		if (_tasks.TryGetValue(id, out ScriptTask value))
		{
			value.Status = (running ? "running" : "stopped");
			if (running)
			{
				value.NextRunAt = DateTime.Now;
			}
			Append(value.Id, running ? Word.T("tick.started") : Word.T("tick.stopped"));
			SaveTask(value);
			RaiseTasksChanged();
		}
	}

	public void Reset(string id)
	{
		if (_tasks.TryGetValue(id, out ScriptTask value))
		{
			value.NotifyCount = 0;
			value.Status = "running";
			value.NextRunAt = DateTime.Now;
			Append(value.Id, Word.T("tick.reset"));
			SaveTask(value);
			RaiseTasksChanged();
		}
	}

	private void OnTick(object? sender, EventArgs e)
	{
		DateTime now = DateTime.Now;
		foreach (ScriptTask item in _tasks.Values.ToList())
		{
			if (!(item.Status != "running") && !item.IsBusy && !(now < item.NextRunAt) && item.IsInActiveWindow(now))
			{
				RunAsync(item);
			}
		}
	}

	private async Task RunAsync(ScriptTask task)
	{
		task.IsBusy = true;
		RaiseTasksChanged();
		SemaphoreSlim gate = null;
		bool acquired = false;
		try
		{
			Append(task.Id, Word.T("tick.checking"));
			Solve.Config cfg = task.ToConfig();
			Solve core = new Solve
			{
				Cfg = cfg,
				UrlChooser = (List<string> urls) => ChooseUrl(task, urls)
			};
			if (TitleProfiles.IsSdga(cfg.Name))
			{
				IpRegion ipRegion = await Feed.GetIpRegionAsync();
				Append(task.Id, (ipRegion == null) ? Word.T("tick.ipFail") : Word.T("tick.ipOk", ipRegion.Ip, ipRegion.Country, ipRegion.Region));
			}
			if (string.IsNullOrWhiteSpace(cfg.DownloadPath))
			{
				Append(task.Id, Word.T("tick.noPath"));
				return;
			}
			if (!Directory.Exists(cfg.DownloadPath))
			{
				Directory.CreateDirectory(cfg.DownloadPath);
			}
			gate = GetGate(cfg.DownloadPath);
			await gate.WaitAsync();
			acquired = true;
			List<FileItem> list = await core.GetFileListAsync();
			Append(task.Id, Word.T("tick.serverFiles", list.Count));
			List<FileItem> list2 = new List<FileItem>();
			foreach (FileItem item2 in list)
			{
				if (task.Matches(item2.Name))
				{
					if (File.Exists(Path.Combine(cfg.DownloadPath, item2.Name)))
					{
						Append(task.Id, Word.T("tick.existsSkip", item2.Name));
					}
					else
					{
						list2.Add(item2);
					}
				}
			}
			if (list2.Count == 0)
			{
				Append(task.Id, Word.T("tick.noNew"));
				return;
			}
			int downloaded = 0;
			foreach (FileItem item in list2)
			{
				string target = Path.Combine(cfg.DownloadPath, item.Name);
				Append(task.Id, Word.T("tick.downloadStart", item.Name));
				int lastStep = 0;
				Progress<(double, double)> progress = new Progress<(double, double)>(delegate((double percentage, double speed) p)
				{
					int num = (int)(p.percentage * 4.0);
					if (num > lastStep)
					{
						lastStep = num;
						Append(task.Id, Word.T("tick.progress", item.Name, Math.Min(num, 4) * 25, FormatSpeed(p.speed)));
					}
				});
				if (!(await core.DownloadWithProgressAsync(item.Url, target, progress)))
				{
					Append(task.Id, Word.T("tick.downloadFail", item.Name));
					continue;
				}
				downloaded++;
				Append(task.Id, Word.T("tick.downloadDone", item.Name));
				if (!task.AutoDecrypt)
				{
					continue;
				}
				var (flag, flag2, text) = await Peel.Go(item.Name, target, delegate(string msg)
				{
					Append(task.Id, msg);
				});
				if (flag && !flag2)
				{
					Append(task.Id, Word.T("tick.crackFail", item.Name, string.IsNullOrEmpty(text) ? Word.T("common.unknownError") : text));
				}
				if (flag2 && item.Name.EndsWith(".opt", StringComparison.OrdinalIgnoreCase))
				{
					await Peel.Put(target, cfg.SinmaiPath, delegate(string msg)
					{
						Append(task.Id, msg);
					});
				}
			}
			if (downloaded == 0)
			{
				Append(task.Id, Word.T("tick.noneDownloaded"));
				return;
			}
			task.NotifyCount++;
			Append(task.Id, Word.T("tick.summary", downloaded, task.NotifyCount, task.EffectiveMaxNotify));
			ToastWindow.ShowAtBottomRight(Word.T("tick.toastTitle", task.Id), Word.T("tick.toastMsg", task.Game, task.Version, downloaded));
			if (task.NotifyCount >= task.EffectiveMaxNotify)
			{
				task.Status = "stopped";
				Append(task.Id, Word.T("tick.limitReached"));
			}
			SaveTask(task);
		}
		catch (Exception ex)
		{
			Append(task.Id, Word.T("tick.runFail", ex.Message));
		}
		finally
		{
			if (acquired)
			{
				gate?.Release();
			}
			task.IsBusy = false;
			task.NextRunAt = DateTime.Now.AddSeconds(Math.Max(1, task.IntervalSeconds));
			RaiseTasksChanged();
		}
	}

	private string? ChooseUrl(ScriptTask task, List<string> urls)
	{
		if (urls.Count == 0)
		{
			return null;
		}
		Append(task.Id, Word.T("tick.chooseUrl", urls.Count, urls[0]));
		return urls[0];
	}

	private SemaphoreSlim GetGate(string path)
	{
		string key = NormalizePath(path).ToLowerInvariant();
		if (!_gates.TryGetValue(key, out SemaphoreSlim value))
		{
			value = new SemaphoreSlim(1, 1);
			_gates[key] = value;
		}
		return value;
	}

	private void Append(string id, string message)
	{
		if (string.IsNullOrEmpty(message))
		{
			return;
		}
		if (Application.Current != null && !Application.Current.Dispatcher.CheckAccess())
		{
			Application.Current.Dispatcher.Invoke(delegate
			{
				Append(id, message);
			});
			return;
		}
		if (!_logs.TryGetValue(id, out List<string> value))
		{
			value = new List<string>();
			_logs[id] = value;
		}
		string text = $"[{DateTime.Now:HH:mm:ss}] {message}";
		value.Add(text);
		if (value.Count > 500)
		{
			value.RemoveRange(0, value.Count - 500);
		}
		LogAppended?.Invoke(id, text);
	}

	private void RaiseTasksChanged()
	{
		TasksChanged?.Invoke();
	}

	private static void SaveTask(ScriptTask task)
	{
		ScriptStore.Save(task, out string _);
	}

	private static string NormalizePath(string? path)
	{
		return (path ?? "").Trim().TrimEnd(new char[2]
		{
			Path.DirectorySeparatorChar,
			Path.AltDirectorySeparatorChar
		});
	}

	private static string FormatSpeed(double bytesPerSecond)
	{
		if (!(bytesPerSecond >= 1048576.0))
		{
			return $"{bytesPerSecond / 1024.0:F1} KB/s";
		}
		return $"{bytesPerSecond / 1048576.0:F2} MB/s";
	}
}
