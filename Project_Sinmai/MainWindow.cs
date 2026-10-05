using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Navigation;
using Microsoft.Win32;

namespace Project_Sinmai;

public partial class MainWindow : Window, IComponentConnector, IStyleConnector
{
	private Solve _core = new Solve();

	private readonly Dictionary<string, Solve.Config> _configs = new Dictionary<string, Solve.Config>();

	private bool _initializing = true;

	private bool _persist;

	private ScriptListWindow? _scriptListWindow;

	private FileDecryptWindow? _fileDecryptWindow;

	private IcfEditWindow? _icfEditWindow;

	private readonly Dictionary<string, (byte[] key, byte[] iv)> _manualKeys = new Dictionary<string, (byte[], byte[])>();

	public ObservableCollection<FileItem> Files { get; set; } = new ObservableCollection<FileItem>();

	public MainWindow()
	{
		_core.UrlChooser = PickUrl;
		string[] array = new string[2] { "SDGA", "SDGB" };
		foreach (string text in array)
		{
			_configs[text] = TitleProfiles.Create(text);
		}
		string defaultDownloadPath = GetDefaultDownloadPath();
		foreach (Solve.Config value in _configs.Values)
		{
			value.DownloadPath = defaultDownloadPath;
		}
		string text2 = "SDGB";
		LangKind kind = Word.Detect();
		if (ConfigStore.TryLoad(out bool persist, out string game, _configs, out LangKind lang))
		{
			_persist = persist;
			kind = lang;
			if (_configs.ContainsKey(game))
			{
				text2 = game;
			}
		}
		Word.Init(kind);
		InitializeComponent();
		Files.Clear();
		DgFiles.ItemsSource = Files;
		ChkPersist.IsChecked = _persist;
		_core.Cfg = _configs[text2];
		CmbTitle.SelectedIndex = ((!TitleProfiles.IsSdga(text2)) ? 1 : 0);
		CmbLang.SelectedIndex = (int)Word.Current;
		ApplyConfig(text2);
		_initializing = false;
		ScriptScheduler.Instance.Start();
		base.Closed += delegate
		{
			ScriptScheduler.Instance.StopAll();
		};
	}

	private void Link_RequestNavigate(object sender, RequestNavigateEventArgs e)
	{
		try
		{
			Process.Start(new ProcessStartInfo
			{
				FileName = e.Uri.AbsoluteUri,
				UseShellExecute = true
			});
		}
		catch
		{
		}
		e.Handled = true;
	}

	private void BtnScripts_Click(object sender, RoutedEventArgs e)
	{
		if (_scriptListWindow != null && _scriptListWindow.IsLoaded)
		{
			_scriptListWindow.Activate();
			return;
		}
		_scriptListWindow = new ScriptListWindow
		{
			Owner = this
		};
		_scriptListWindow.Closed += delegate
		{
			_scriptListWindow = null;
		};
		_scriptListWindow.Show();
	}

	private void BtnFileDecrypt_Click(object sender, RoutedEventArgs e)
	{
		if (_fileDecryptWindow != null && _fileDecryptWindow.IsLoaded)
		{
			_fileDecryptWindow.Activate();
			return;
		}
		_fileDecryptWindow = new FileDecryptWindow
		{
			Owner = this
		};
		_fileDecryptWindow.Closed += delegate
		{
			_fileDecryptWindow = null;
		};
		_fileDecryptWindow.Show();
	}

	private void BtnIcfEdit_Click(object sender, RoutedEventArgs e)
	{
		if (_icfEditWindow != null && _icfEditWindow.IsLoaded)
		{
			_icfEditWindow.Activate();
			return;
		}
		_icfEditWindow = new IcfEditWindow
		{
			Owner = this
		};
		_icfEditWindow.Closed += delegate
		{
			_icfEditWindow = null;
		};
		_icfEditWindow.Show();
	}

	private void CmbTitle_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (!_initializing && CmbTitle.SelectedItem is ComboBoxItem comboBoxItem)
		{
			string titleId = (comboBoxItem.Tag as string) ?? "SDGB";
			ApplyConfig(titleId);
			SaveConfig();
		}
	}

	private void CmbLang_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (!_initializing && CmbLang.SelectedIndex >= 0)
		{
			Word.Set((LangKind)CmbLang.SelectedIndex);
			ConfigStore.Save(_persist, _core.Cfg.Name, _configs, Word.Current);
			TxtStatus.Text = Word.T("shell.ready");
			TxtSpeed.Text = Word.T("shell.speed");
		}
	}

	private async Task<bool> CheckSdgaRegionAsync()
	{
		TxtStatus.Text = Word.T("shell.checkingNetwork");
		IpRegion ipRegion = await Feed.GetIpRegionAsync();
		if (ipRegion == null)
		{
			MessageBox.Show(Word.T("shell.ipDetectFail"), Word.T("common.tip"), MessageBoxButton.OK, MessageBoxImage.Asterisk);
			return true;
		}
		if (ipRegion.Country == "CN" || ipRegion.Country == "JP")
		{
			string text = Word.T((ipRegion.Country == "CN") ? "shell.cn" : "shell.jp");
			string text2 = (string.IsNullOrWhiteSpace(ipRegion.Region) ? text : (text + " (" + ipRegion.Region + ")"));
			return MessageBox.Show(Word.T("shell.regionWarn", text2), Word.T("common.tip"), MessageBoxButton.YesNo, MessageBoxImage.Asterisk) == MessageBoxResult.Yes;
		}
		MessageBox.Show(Word.T("shell.regionOk", ipRegion.Ip, ipRegion.Region), Word.T("common.tip"), MessageBoxButton.OK, MessageBoxImage.Asterisk);
		return true;
	}

	private void ApplyConfig(string titleId)
	{
		_core.Cfg = _configs[titleId];
		bool initializing = _initializing;
		_initializing = true;
		try
		{
			TxtVersion.Text = _core.Cfg.Version;
			TxtSerial.Text = _core.Cfg.Serial;
			TxtClientId.Text = _core.Cfg.ClientId;
			TxtInstructionUrl.Text = _core.Cfg.InstructionUrl;
			TxtInstructionUa.Text = _core.Cfg.InstructionUa;
			TxtDownloadUa.Text = _core.Cfg.DownloadUa;
			TxtDownloadPath.Text = _core.Cfg.DownloadPath;
			TxtSinmaiPath.Text = _core.Cfg.SinmaiPath;
		}
		finally
		{
			_initializing = initializing;
		}
		bool flag = _core.Cfg.Type == "plain";
		PnlSerial.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
		PnlClientId.Visibility = (flag ? Visibility.Collapsed : Visibility.Visible);
	}

	private string? PickUrl(List<string> urls)
	{
		Window window = new Window
		{
			Title = Word.T("shell.pickUrlTitle"),
			Width = 620.0,
			Height = 320.0,
			Owner = this,
			WindowStartupLocation = WindowStartupLocation.CenterOwner
		};
		DockPanel dockPanel = new DockPanel
		{
			Margin = new Thickness(10.0)
		};
		Button button = new Button
		{
			Content = Word.T("common.ok"),
			Width = 80.0,
			Height = 26.0,
			Margin = new Thickness(0.0, 10.0, 0.0, 0.0),
			HorizontalAlignment = HorizontalAlignment.Right
		};
		DockPanel.SetDock(button, Dock.Bottom);
		ListBox list = new ListBox();
		foreach (string url in urls)
		{
			list.Items.Add(url);
		}
		list.SelectedIndex = 0;
		dockPanel.Children.Add(button);
		dockPanel.Children.Add(list);
		window.Content = dockPanel;
		string result = null;
		button.Click += delegate
		{
			result = list.SelectedItem as string;
			window.Close();
		};
		window.ShowDialog();
		return result;
	}

	public static string GetDefaultDownloadPath()
	{
		try
		{
			string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
			if (!string.IsNullOrEmpty(text) && Directory.Exists(text))
			{
				return text;
			}
		}
		catch
		{
		}
		return AppDomain.CurrentDomain.BaseDirectory;
	}

	private void ChkAutoUnpack_Checked(object sender, RoutedEventArgs e)
	{
		if (base.IsLoaded)
		{
			MessageBox.Show(Word.T("shell.autoUnpackWarn"), Word.T("common.tip"), MessageBoxButton.OK, MessageBoxImage.Exclamation);
		}
	}

	private void ChkPersist_Changed(object sender, RoutedEventArgs e)
	{
		if (!_initializing)
		{
			_persist = ChkPersist.IsChecked == true;
			if (_persist)
			{
				ConfigStore.Save(persist: true, _core.Cfg.Name, _configs, Word.Current);
			}
			else
			{
				ConfigStore.Delete();
			}
		}
	}

	private void SaveConfig()
	{
		if (_persist)
		{
			ConfigStore.Save(persist: true, _core.Cfg.Name, _configs, Word.Current);
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
				_core.Cfg.DownloadPath = openFolderDialog.FolderName;
				SaveConfig();
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
				_core.Cfg.SinmaiPath = openFolderDialog.FolderName;
				SaveConfig();
			}
		}
		catch
		{
		}
	}

	private void CheckBox_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			if (sender is CheckBox { DataContext: FileItem dataContext } checkBox)
			{
				dataContext.IsSelected = checkBox.IsChecked == true;
			}
		}
		catch
		{
		}
	}

	private void ConfigBox_TextChanged(object sender, TextChangedEventArgs e)
	{
		if (_core != null && !_initializing)
		{
			if (sender == TxtVersion)
			{
				_core.Cfg.Version = TxtVersion.Text.Trim();
			}
			else if (sender == TxtSerial)
			{
				_core.Cfg.Serial = TxtSerial.Text.Trim();
			}
			else if (sender == TxtClientId)
			{
				_core.Cfg.ClientId = TxtClientId.Text.Trim();
			}
			else if (sender == TxtInstructionUrl)
			{
				_core.Cfg.InstructionUrl = TxtInstructionUrl.Text.Trim();
			}
			else if (sender == TxtInstructionUa)
			{
				_core.Cfg.InstructionUa = TxtInstructionUa.Text.Trim();
			}
			else if (sender == TxtDownloadUa)
			{
				_core.Cfg.DownloadUa = TxtDownloadUa.Text.Trim();
			}
			SaveConfig();
		}
	}

	private async void BtnInit_Click(object sender, RoutedEventArgs e)
	{
		_ = 1;
		try
		{
			string mainDownloadPath = TxtDownloadPath.Text.Trim();
			if (string.IsNullOrWhiteSpace(mainDownloadPath))
			{
				TxtStatus.Text = Word.T("shell.needDownloadPath");
				MessageBox.Show(Word.T("shell.needDownloadPathMsg"), Word.T("common.tip"), MessageBoxButton.OK, MessageBoxImage.Exclamation);
				return;
			}
			if (!Directory.Exists(mainDownloadPath))
			{
				Directory.CreateDirectory(mainDownloadPath);
			}
			if (TitleProfiles.IsSdga(_core.Cfg.Name) && !(await CheckSdgaRegionAsync()))
			{
				return;
			}
			bool deleteConfigAfterInit = false;
			string initPath;
			switch (MessageBox.Show(Word.T("shell.initConfirm"), Word.T("shell.initConfirmTitle"), MessageBoxButton.YesNoCancel, MessageBoxImage.Question))
			{
			case MessageBoxResult.Cancel:
				return;
			case MessageBoxResult.Yes:
			{
				OpenFolderDialog openFolderDialog = new OpenFolderDialog();
				if (openFolderDialog.ShowDialog() == true)
				{
					initPath = openFolderDialog.FolderName;
					_core.Cfg.DownloadPath = initPath;
					break;
				}
				return;
			}
			default:
				initPath = Path.GetTempPath();
				deleteConfigAfterInit = true;
				_core.Cfg.DownloadPath = initPath;
				break;
			}
			TxtStatus.Text = Word.T("shell.initializing");
			List<FileItem> list = await _core.GetFileListAsync();
			if (deleteConfigAfterInit)
			{
				try
				{
					string[] files = Directory.GetFiles(initPath, "*.txt");
					for (int i = 0; i < files.Length; i++)
					{
						File.Delete(files[i]);
					}
				}
				catch
				{
				}
			}
			_core.Cfg.DownloadPath = mainDownloadPath;
			Files.Clear();
			foreach (FileItem item in list)
			{
				if (!string.IsNullOrWhiteSpace(item.Name))
				{
					Files.Add(item);
				}
			}
			TxtStatus.Text = Word.T("shell.initOk", Files.Count);
		}
		catch (Exception ex)
		{
			TxtStatus.Text = Word.T("shell.initFail");
			MessageBox.Show(Word.T("shell.initFailMsg", ex.Message), Word.T("common.error"), MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private async void BtnDownloadSelected_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			if (string.IsNullOrWhiteSpace(_core.Cfg.DownloadPath) || !Directory.Exists(_core.Cfg.DownloadPath))
			{
				MessageBox.Show(Word.T("shell.needDownloadPathMsg"), Word.T("common.tip"), MessageBoxButton.OK, MessageBoxImage.Exclamation);
				return;
			}
			int crackOk = 0;
			int crackFail = 0;
			List<string> failures = new List<string>();
			List<string> deployLines = new List<string>();
			foreach (FileItem item in Files)
			{
				if (!item.IsSelected)
				{
					continue;
				}
				(bool, bool, bool, string, string) obj = await DownloadFileAsync(item);
				bool item2 = obj.Item2;
				bool item3 = obj.Item3;
				string item4 = obj.Item4;
				string item5 = obj.Item5;
				if (item2)
				{
					if (item3)
					{
						crackOk++;
					}
					else
					{
						crackFail++;
						failures.Add(item.Name + " (" + (string.IsNullOrEmpty(item4) ? Word.T("common.unknownError") : item4) + ")");
					}
				}
				if (!string.IsNullOrEmpty(item5))
				{
					deployLines.Add(item5);
				}
			}
			TxtStatus.Text = Word.T("shell.selectedDone");
			ShowCrackSummary(crackOk, crackFail, failures, deployLines);
		}
		catch (Exception ex)
		{
			TxtStatus.Text = Word.T("shell.downloadError");
			MessageBox.Show(Word.T("shell.downloadErrorMsg", ex.Message), Word.T("common.error"), MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private async void BtnDownloadAll_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			if (string.IsNullOrWhiteSpace(_core.Cfg.DownloadPath) || !Directory.Exists(_core.Cfg.DownloadPath))
			{
				MessageBox.Show(Word.T("shell.needDownloadPathMsg"), Word.T("common.tip"), MessageBoxButton.OK, MessageBoxImage.Exclamation);
				return;
			}
			TxtStatus.Text = Word.T("shell.startAll");
			int crackOk = 0;
			int crackFail = 0;
			List<string> failures = new List<string>();
			List<string> deployLines = new List<string>();
			foreach (FileItem item in Files)
			{
				(bool, bool, bool, string, string) obj = await DownloadFileAsync(item);
				bool item2 = obj.Item2;
				bool item3 = obj.Item3;
				string item4 = obj.Item4;
				string item5 = obj.Item5;
				if (item2)
				{
					if (item3)
					{
						crackOk++;
					}
					else
					{
						crackFail++;
						failures.Add(item.Name + " (" + (string.IsNullOrEmpty(item4) ? Word.T("common.unknownError") : item4) + ")");
					}
				}
				if (!string.IsNullOrEmpty(item5))
				{
					deployLines.Add(item5);
				}
			}
			TxtStatus.Text = Word.T("shell.allDone");
			ShowCrackSummary(crackOk, crackFail, failures, deployLines);
		}
		catch (Exception ex)
		{
			TxtStatus.Text = Word.T("shell.downloadError");
			MessageBox.Show(Word.T("shell.downloadErrorMsg", ex.Message), Word.T("common.error"), MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private async Task<(bool DownloadOk, bool CrackAttempted, bool CrackOk, string? CrackError, string? DeployInfo)> DownloadFileAsync(FileItem item)
	{
		if (string.IsNullOrWhiteSpace(item.Name))
		{
			return (DownloadOk: false, CrackAttempted: false, CrackOk: false, CrackError: null, DeployInfo: null);
		}
		string savePath = Path.Combine(_core.Cfg.DownloadPath, item.Name);
		TxtStatus.Text = Word.T("shell.downloading", item.Name);
		Progress<(double, double)> progress = new Progress<(double, double)>(delegate((double percentage, double speed) p)
		{
			PbProgress.Value = p.percentage * 100.0;
			TxtSpeed.Text = Word.T("shell.speedFmt", FormatSpeed(p.speed));
		});
		try
		{
			if (!(await _core.DownloadWithProgressAsync(item.Url, savePath, progress)))
			{
				TxtStatus.Text = Word.T("shell.downloadFailStatus", item.Name);
				MessageBox.Show(Word.T("shell.downloadFailMsg", item.Name), Word.T("shell.downloadFailTitle"), MessageBoxButton.OK, MessageBoxImage.Exclamation);
				return (DownloadOk: false, CrackAttempted: false, CrackOk: false, CrackError: null, DeployInfo: null);
			}
		}
		catch (Exception ex)
		{
			TxtStatus.Text = Word.T("shell.downloadErrStatus", item.Name);
			MessageBox.Show(Word.T("shell.downloadErrMsg", item.Name, ex.Message), Word.T("common.error"), MessageBoxButton.OK, MessageBoxImage.Hand);
			return (DownloadOk: false, CrackAttempted: false, CrackOk: false, CrackError: null, DeployInfo: null);
		}
		TxtStatus.Text = Word.T("shell.downloadDone", item.Name);
		if (ChkAutoUnpack.IsChecked == true)
		{
			string baseFile = null;
			bool skipDelta = false;
			(byte[] key, byte[] iv)? keyHint = null;
			if (item.Name.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
			{
				string text = await Task.Run(() => Untie.BZ(savePath));
				if (text != null && !Untie.ZK(text))
				{
					if (!_manualKeys.TryGetValue(text, out (byte[], byte[]) value))
					{
						ManualKeyWindow manualKeyWindow = new ManualKeyWindow(text)
						{
							Owner = this
						};
						if (manualKeyWindow.ShowDialog() != true)
						{
							return (DownloadOk: true, CrackAttempted: false, CrackOk: false, CrackError: null, DeployInfo: null);
						}
						value = (manualKeyWindow.KeyBytes, manualKeyWindow.IvBytes);
						_manualKeys[text] = value;
					}
					keyHint = value;
				}
				if (await Task.Run(() => Untie.BX(savePath)) > 0)
				{
					string text2 = await Task.Run(() => (!keyHint.HasValue) ? Untie.BY(savePath) : Untie.BY(savePath, null, keyHint));
					if (text2 == null)
					{
						if (MessageBox.Show(Word.T("shell.noBaseMsg"), Word.T("common.tip"), MessageBoxButton.YesNo, MessageBoxImage.Asterisk) == MessageBoxResult.Yes)
						{
							OpenFolderDialog folder = new OpenFolderDialog
							{
								Title = Word.T("shell.pickBaseDir")
							};
							if (folder.ShowDialog(this) == true)
							{
								text2 = await Task.Run(() => Untie.BY(savePath, folder.FolderName));
								if (text2 == null)
								{
									MessageBox.Show(Word.T("shell.baseNotFound"), Word.T("common.tip"), MessageBoxButton.OK, MessageBoxImage.Asterisk);
								}
							}
						}
						if (text2 == null)
						{
							skipDelta = true;
						}
					}
					baseFile = text2;
				}
			}
			if (skipDelta)
			{
				return (DownloadOk: true, CrackAttempted: false, CrackOk: false, CrackError: null, DeployInfo: null);
			}
			(bool, bool, string) tuple = await CrackFileAsync(item.Name, savePath, baseFile, keyHint);
			bool attempted = tuple.Item1;
			bool crackOk = tuple.Item2;
			string crackErr = tuple.Item3;
			string text3 = null;
			if (crackOk && item.Name.EndsWith(".opt", StringComparison.OrdinalIgnoreCase))
			{
				text3 = await DeployCrackedOptAsync(savePath);
				if (text3 != null)
				{
					TxtStatus.Text = text3;
				}
			}
			return (DownloadOk: true, CrackAttempted: attempted, CrackOk: crackOk, CrackError: crackErr, DeployInfo: text3);
		}
		return (DownloadOk: true, CrackAttempted: false, CrackOk: false, CrackError: null, DeployInfo: null);
	}

	private async Task<(bool Attempted, bool Success, string? Error)> CrackFileAsync(string fileName, string filePath, string? baseFile = null, (byte[] key, byte[] iv)? keyHint = null)
	{
		return await Peel.Go(fileName, filePath, delegate(string msg)
		{
			base.Dispatcher.Invoke(() => TxtStatus.Text = msg);
		}, baseFile, keyHint);
	}

	private async Task<string?> DeployCrackedOptAsync(string optFilePath)
	{
		return await Peel.Put(optFilePath, _core.Cfg.SinmaiPath, delegate(string msg)
		{
			base.Dispatcher.Invoke(() => TxtStatus.Text = msg);
		});
	}

	private static void ShowCrackSummary(int crackOk, int crackFail, List<string> failures, List<string> deployLines)
	{
		if (crackOk + crackFail != 0 || deployLines.Count != 0)
		{
			string text = Word.T("shell.crackSummary", crackOk, crackFail);
			if (failures.Count > 0)
			{
				text = text + "\n\n" + Word.T("shell.failedFiles") + "\n" + string.Join("\n", failures);
			}
			if (deployLines.Count > 0)
			{
				text = text + "\n\n" + Word.T("shell.sinmaiDeploy") + "\n" + string.Join("\n", deployLines);
			}
			MessageBox.Show(text, Word.T("shell.crackResultTitle"), MessageBoxButton.OK, (crackFail == 0) ? MessageBoxImage.Asterisk : MessageBoxImage.Exclamation);
		}
	}

	private string FormatSpeed(double bytesPerSecond)
	{
		try
		{
			if (!(bytesPerSecond >= 1048576.0))
			{
				return $"{bytesPerSecond / 1024.0:F1} KB/s";
			}
			return $"{bytesPerSecond / 1048576.0:F2} MB/s";
		}
		catch
		{
			return "0 KB/s";
		}
	}
}
