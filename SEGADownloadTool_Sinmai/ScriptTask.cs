using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Serialization;

namespace SEGADownloadTool_Sinmai;

public class ScriptTask
{
	public string Id { get; set; } = "001";

	public string CreatedAt { get; set; } = "";

	public string Game { get; set; } = "SDGA";

	public string Version { get; set; } = "";

	public string DownloadPath { get; set; } = "";

	public string SinmaiPath { get; set; } = "";

	public bool AutoDecrypt { get; set; }

	public bool UseAdvanced { get; set; }

	public string InstructionUrl { get; set; } = "";

	public string InstructionUa { get; set; } = "";

	public string Serial { get; set; } = "";

	public string ClientId { get; set; } = "";

	public string DownloadUa { get; set; } = "";

	public int IntervalSeconds { get; set; } = 300;

	public string ListMode { get; set; } = "blacklist";

	public List<string> ListFiles { get; set; } = new List<string>();

	public string TimeWindowMode { get; set; } = "off";

	public string TimeWindowStart { get; set; } = "00:00";

	public string TimeWindowEnd { get; set; } = "00:00";

	public int MaxNotify { get; set; }

	public int NotifyCount { get; set; }

	public string Status { get; set; } = "running";

	[JsonIgnore]
	public DateTime NextRunAt { get; set; } = DateTime.Now;

	[JsonIgnore]
	public bool IsBusy { get; set; }

	[JsonIgnore]
	public int EffectiveMaxNotify
	{
		get
		{
			if (MaxNotify > 0)
			{
				return MaxNotify;
			}
			return 1;
		}
	}

	[JsonIgnore]
	public string IntervalText
	{
		get
		{
			if (IntervalSeconds <= 0 || IntervalSeconds % 60 != 0)
			{
				return Word.T("job.seconds", IntervalSeconds);
			}
			return Word.T("job.minutes", IntervalSeconds / 60);
		}
	}

	[JsonIgnore]
	public string StatusText
	{
		get
		{
			if (!(Status == "running"))
			{
				return Word.T("job.stopped");
			}
			if (!IsBusy)
			{
				return Word.T("job.running");
			}
			return Word.T("job.checking");
		}
	}

	[JsonIgnore]
	public string NextRunText
	{
		get
		{
			if (!(Status == "running"))
			{
				return "—";
			}
			return NextRunAt.ToString("MM-dd HH:mm:ss");
		}
	}

	[JsonIgnore]
	public string NotifyText => $"{NotifyCount}/{EffectiveMaxNotify}";

	public ScriptTask Clone()
	{
		ScriptTask obj = (ScriptTask)MemberwiseClone();
		obj.ListFiles = new List<string>(ListFiles);
		return obj;
	}

	public Solve.Config ToConfig()
	{
		Solve.Config config = TitleProfiles.Create(Game);
		config.Version = Version;
		config.DownloadPath = DownloadPath;
		config.SinmaiPath = SinmaiPath;
		config.DownloadUa = DownloadUa;
		if (TitleProfiles.IsSdga(Game))
		{
			config.Serial = Serial;
		}
		else
		{
			config.ClientId = ClientId;
		}
		if (UseAdvanced)
		{
			config.InstructionUrl = InstructionUrl;
			config.InstructionUa = InstructionUa;
		}
		return config;
	}

	public bool Matches(string fileName)
	{
		bool flag = ListFiles.Any((string x) => string.Equals(x, fileName, StringComparison.OrdinalIgnoreCase));
		if (!(ListMode == "whitelist"))
		{
			return !flag;
		}
		return flag;
	}

	public bool IsInActiveWindow(DateTime now)
	{
		if (TimeWindowMode == "off")
		{
			return true;
		}
		if (!TryParseTime(TimeWindowStart, out var value))
		{
			return true;
		}
		if (!TryParseTime(TimeWindowEnd, out var value2))
		{
			return true;
		}
		TimeSpan timeOfDay = now.TimeOfDay;
		bool flag = ((!(value <= value2)) ? (timeOfDay >= value || timeOfDay <= value2) : (timeOfDay >= value && timeOfDay <= value2));
		if (!(TimeWindowMode == "include"))
		{
			return !flag;
		}
		return flag;
	}

	private static bool TryParseTime(string? text, out TimeSpan value)
	{
		value = TimeSpan.Zero;
		if (string.IsNullOrWhiteSpace(text))
		{
			return false;
		}
		return TimeSpan.TryParseExact(text.Trim(), "hh\\:mm", CultureInfo.InvariantCulture, out value);
	}
}
