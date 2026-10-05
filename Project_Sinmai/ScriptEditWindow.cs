using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using Microsoft.Win32;

namespace Project_Sinmai;

public partial class ScriptEditWindow : Window, IComponentConnector
{
	private readonly ScriptTask _task;

	private bool _initializing = true;

	public ScriptTask Result { get; private set; } = new ScriptTask();

	public ScriptEditWindow(ScriptTask task)
	{
		InitializeComponent();
		_task = task;
		CmbGame.SelectedIndex = ((!TitleProfiles.IsSdga(task.Game)) ? 1 : 0);
		TxtVersion.Text = task.Version;
		TxtMaxNotify.Text = ((task.MaxNotify > 0) ? task.MaxNotify.ToString() : "");
		TxtDownloadPath.Text = task.DownloadPath;
		TxtSinmaiPath.Text = task.SinmaiPath;
		ChkAutoDecrypt.IsChecked = task.AutoDecrypt;
		ChkUseAdvanced.IsChecked = task.UseAdvanced;
		if (string.IsNullOrWhiteSpace(task.InstructionUrl) && string.IsNullOrWhiteSpace(task.DownloadUa))
		{
			LoadGameDefaults(task.Game);
		}
		else
		{
			TxtInstructionUrl.Text = task.InstructionUrl;
			TxtInstructionUa.Text = task.InstructionUa;
			TxtSerial.Text = task.Serial;
			TxtClientId.Text = task.ClientId;
			TxtDownloadUa.Text = task.DownloadUa;
			ApplyGameFields(task.Game);
		}
		TxtListFiles.Text = string.Join(Environment.NewLine, task.ListFiles);
		SelectInterval(task.IntervalSeconds);
		CmbListMode.SelectedIndex = ((task.ListMode == "whitelist") ? 1 : 0);
		SelectWindowMode(task.TimeWindowMode);
		TxtWindowStart.Text = (string.IsNullOrWhiteSpace(task.TimeWindowStart) ? "00:00" : task.TimeWindowStart);
		TxtWindowEnd.Text = (string.IsNullOrWhiteSpace(task.TimeWindowEnd) ? "00:00" : task.TimeWindowEnd);
		_initializing = false;
		UpdateEnabledStates();
	}

	private void CmbGame_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (!_initializing)
		{
			LoadGameDefaults(GetSelectedGame());
		}
	}

	private void CmbInterval_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (!_initializing)
		{
			UpdateEnabledStates();
		}
	}

	private void CmbWindowMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (!_initializing)
		{
			UpdateEnabledStates();
		}
	}

	private void ChkUseAdvanced_Changed(object sender, RoutedEventArgs e)
	{
		if (!_initializing)
		{
			UpdateEnabledStates();
		}
	}

	private void BtnBrowseDownloadPath_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			OpenFolderDialog openFolderDialog = new OpenFolderDialog();
			if (openFolderDialog.ShowDialog() == true)
			{
				TxtDownloadPath.Text = openFolderDialog.FolderName;
			}
		}
		catch
		{
		}
	}

	private void BtnBrowseSinmaiPath_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			OpenFolderDialog openFolderDialog = new OpenFolderDialog();
			if (openFolderDialog.ShowDialog() == true)
			{
				TxtSinmaiPath.Text = openFolderDialog.FolderName;
			}
		}
		catch
		{
		}
	}

	private void BtnOk_Click(object sender, RoutedEventArgs e)
	{
		string selectedGame = GetSelectedGame();
		string text = TxtVersion.Text.Trim();
		string text2 = TxtDownloadPath.Text.Trim();
		if (string.IsNullOrWhiteSpace(text))
		{
			MessageBox.Show(Word.T("form.needVersion"), Word.T("common.tip"), MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		if (string.IsNullOrWhiteSpace(text2))
		{
			MessageBox.Show(Word.T("form.needPath"), Word.T("common.tip"), MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		int num = ParseInterval();
		if (num <= 0)
		{
			MessageBox.Show(Word.T("form.needInterval"), Word.T("common.tip"), MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		int result = 0;
		string text3 = TxtMaxNotify.Text.Trim();
		if (text3.Length > 0 && (!int.TryParse(text3, out result) || result <= 0))
		{
			MessageBox.Show(Word.T("form.needNotify"), Word.T("common.tip"), MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		string tag = GetTag(CmbWindowMode, "off");
		string text4 = TxtWindowStart.Text.Trim();
		string text5 = TxtWindowEnd.Text.Trim();
		if (tag != "off" && (!IsValidTime(text4) || !IsValidTime(text5)))
		{
			MessageBox.Show(Word.T("form.needWindow"), Word.T("common.tip"), MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		if (ScriptScheduler.Instance.IsDuplicate(selectedGame, text, text2, _task.Id, out string message))
		{
			MessageBox.Show(message, Word.T("common.tip"), MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		_task.Game = selectedGame;
		_task.Version = text;
		_task.DownloadPath = text2;
		_task.SinmaiPath = TxtSinmaiPath.Text.Trim();
		_task.AutoDecrypt = ChkAutoDecrypt.IsChecked == true;
		_task.UseAdvanced = ChkUseAdvanced.IsChecked == true;
		_task.InstructionUrl = TxtInstructionUrl.Text.Trim();
		_task.InstructionUa = TxtInstructionUa.Text.Trim();
		_task.Serial = TxtSerial.Text.Trim();
		_task.ClientId = TxtClientId.Text.Trim();
		_task.DownloadUa = TxtDownloadUa.Text.Trim();
		_task.IntervalSeconds = num;
		_task.ListMode = GetTag(CmbListMode, "blacklist");
		_task.ListFiles = ParseListFiles();
		_task.TimeWindowMode = tag;
		_task.TimeWindowStart = (string.IsNullOrWhiteSpace(text4) ? "00:00" : text4);
		_task.TimeWindowEnd = (string.IsNullOrWhiteSpace(text5) ? "00:00" : text5);
		_task.MaxNotify = result;
		Result = _task;
		base.DialogResult = true;
	}

	private void BtnCancel_Click(object sender, RoutedEventArgs e)
	{
		base.DialogResult = false;
	}

	private void LoadGameDefaults(string game)
	{
		Solve.Config config = TitleProfiles.Create(game);
		TxtInstructionUrl.Text = config.InstructionUrl;
		TxtInstructionUa.Text = config.InstructionUa;
		TxtSerial.Text = config.Serial;
		TxtClientId.Text = config.ClientId;
		TxtDownloadUa.Text = config.DownloadUa;
		ApplyGameFields(game);
	}

	private void ApplyGameFields(string game)
	{
		bool flag = TitleProfiles.IsSdga(game);
		PnlSerial.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
		PnlClientId.Visibility = (flag ? Visibility.Collapsed : Visibility.Visible);
	}

	private void UpdateEnabledStates()
	{
		TxtIntervalSeconds.IsEnabled = GetTag(CmbInterval, "custom") == "custom";
		bool isEnabled = GetTag(CmbWindowMode, "off") != "off";
		TxtWindowStart.IsEnabled = isEnabled;
		TxtWindowEnd.IsEnabled = isEnabled;
		GbxAdvanced.IsEnabled = ChkUseAdvanced.IsChecked == true;
	}

	private void SelectInterval(int seconds)
	{
		int selectedIndex = seconds switch
		{
			300 => 0, 
			600 => 1, 
			1200 => 2, 
			1800 => 3, 
			_ => 4, 
		};
		CmbInterval.SelectedIndex = selectedIndex;
		TxtIntervalSeconds.Text = seconds.ToString();
	}

	private void SelectWindowMode(string mode)
	{
		int num = ((mode == "exclude") ? 1 : ((mode == "include") ? 2 : 0));
		int selectedIndex = num;
		CmbWindowMode.SelectedIndex = selectedIndex;
	}

	private int ParseInterval()
	{
		string tag = GetTag(CmbInterval, "custom");
		if (tag != "custom" && int.TryParse(tag, out var result))
		{
			return result;
		}
		if (!int.TryParse(TxtIntervalSeconds.Text.Trim(), out var result2))
		{
			return 0;
		}
		return result2;
	}

	private List<string> ParseListFiles()
	{
		return (from x in TxtListFiles.Text.Split(new char[2] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
			select x.Trim() into x
			where x.Length > 0
			select x).ToList();
	}

	private string GetSelectedGame()
	{
		return GetTag(CmbGame, "SDGA");
	}

	private static string GetTag(ComboBox combo, string fallback)
	{
		return ((combo.SelectedItem as ComboBoxItem)?.Tag as string) ?? fallback;
	}

	private static bool IsValidTime(string text)
	{
		TimeSpan result;
		return TimeSpan.TryParseExact(text, "hh\\:mm", CultureInfo.InvariantCulture, out result);
	}
}
