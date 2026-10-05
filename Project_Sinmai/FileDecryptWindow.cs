using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Project_Sinmai;

public partial class FileDecryptWindow : Window, IComponentConnector
{
	private bool _busy;

	private string? _baseFile;

	private readonly Dictionary<string, (byte[] key, byte[] iv)> _manualKeys = new Dictionary<string, (byte[], byte[])>();

	public FileDecryptWindow()
	{
		InitializeComponent();
		Word.Changed += OnLanguageChanged;
		base.Closed += delegate
		{
			Word.Changed -= OnLanguageChanged;
		};
	}

	private void OnLanguageChanged()
	{
		RefreshHint();
		if (_baseFile == null)
		{
			TxtBase.Text = Word.T("box.notSelected");
		}
	}

	private void Mode_Changed(object sender, RoutedEventArgs e)
	{
		if (PnlBase != null && RbAuto != null && RbBase != null && RbDelta != null && RbOpt != null && TxtHint != null)
		{
			PnlBase.Visibility = ((RbDelta.IsChecked != true) ? Visibility.Collapsed : Visibility.Visible);
			RefreshHint();
		}
	}

	private void RefreshHint()
	{
		TxtHint.Text = ((RbAuto.IsChecked == true) ? Word.T("box.hintAuto") : ((RbBase.IsChecked == true) ? Word.T("box.hintBase") : ((RbDelta.IsChecked == true) ? Word.T("box.hintDelta") : Word.T("box.hintOpt"))));
	}

	private void BtnBase_Click(object sender, RoutedEventArgs e)
	{
		OpenFileDialog openFileDialog = new OpenFileDialog
		{
			Title = Word.T("box.pickBase"),
			Filter = Word.T("box.filterBase")
		};
		if (openFileDialog.ShowDialog(this) == true)
		{
			if (openFileDialog.FileName.EndsWith(".app", StringComparison.OrdinalIgnoreCase) && Untie.BX(openFileDialog.FileName) != 0)
			{
				MessageBox.Show(Word.T("box.notBase"), Word.T("common.tip"), MessageBoxButton.OK, MessageBoxImage.Asterisk);
				return;
			}
			_baseFile = openFileDialog.FileName;
			TxtBase.Text = _baseFile;
		}
	}

	private void DropArea_DragOver(object sender, DragEventArgs e)
	{
		e.Effects = ((GetFiles(e).Length != 0) ? DragDropEffects.Copy : DragDropEffects.None);
		e.Handled = true;
	}

	private async void DropArea_Drop(object sender, DragEventArgs e)
	{
		string[] files = GetFiles(e);
		await Dispatcher.Yield(DispatcherPriority.Background);
		await DecryptAllAsync(files);
	}

	private async void BtnPick_Click(object sender, RoutedEventArgs e)
	{
		string filter = ((RbAuto.IsChecked == true) ? Word.T("box.filterAll") : ((RbOpt.IsChecked == true) ? Word.T("box.filterOpt") : ((RbDelta.IsChecked == true) ? Word.T("box.filterDelta") : Word.T("box.filterBaseOnly"))));
		OpenFileDialog openFileDialog = new OpenFileDialog
		{
			Multiselect = true,
			Filter = filter
		};
		if (openFileDialog.ShowDialog(this) == true)
		{
			await DecryptAllAsync(openFileDialog.FileNames.Where(IsSupported).ToArray());
		}
	}

	private async Task DecryptAllAsync(string[] files)
	{
		if (_busy)
		{
			Append(Word.T("box.busy"));
			return;
		}
		if (RbAuto.IsChecked == true)
		{
			await DecryptAllAutoAsync(files);
			return;
		}
		if (RbDelta.IsChecked == true && _baseFile == null)
		{
			Append(Word.T("box.needBase"));
			return;
		}
		List<string> list = new List<string>();
		foreach (string text in files)
		{
			string text2 = CheckFile(text);
			if (text2 == null)
			{
				list.Add(text);
			}
			else
			{
				Append(text2);
			}
		}
		if (list.Count == 0)
		{
			Append(Word.T("box.noMatch"));
			return;
		}
		_busy = true;
		try
		{
			foreach (string item in list)
			{
				await DecryptAsync(item);
			}
		}
		finally
		{
			_busy = false;
		}
	}

	private async Task DecryptAllAutoAsync(string[] files)
	{
		List<string> list = new List<string>();
		List<string> deltas = new List<string>();
		List<string> opts = new List<string>();
		foreach (string text in files)
		{
			string fileName = Path.GetFileName(text);
			if (text.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
			{
				byte? b = Untie.BX(text);
				if (!b.HasValue)
				{
					Append(Word.T("box.unknownFile", fileName));
				}
				else if (b == 0)
				{
					list.Add(text);
				}
				else
				{
					deltas.Add(text);
				}
			}
			else if (text.EndsWith(".opt", StringComparison.OrdinalIgnoreCase))
			{
				opts.Add(text);
			}
			else
			{
				Append(Word.T("box.skipUnsupported", fileName));
			}
		}
		string autoBase = ((list.Count > 0) ? list[0] : null);
		if (deltas.Count > 0 && autoBase == null && MessageBox.Show(Word.T("box.deltaNoBase"), Word.T("common.tip"), MessageBoxButton.YesNo, MessageBoxImage.Asterisk) == MessageBoxResult.Yes)
		{
			OpenFileDialog openFileDialog = new OpenFileDialog
			{
				Title = Word.T("box.pickBase"),
				Filter = Word.T("box.filterBase")
			};
			if (openFileDialog.ShowDialog(this) == true)
			{
				if (openFileDialog.FileName.EndsWith(".app", StringComparison.OrdinalIgnoreCase) && Untie.BX(openFileDialog.FileName) != 0)
				{
					MessageBox.Show(Word.T("box.notBase"), Word.T("common.tip"), MessageBoxButton.OK, MessageBoxImage.Asterisk);
				}
				else
				{
					autoBase = openFileDialog.FileName;
				}
			}
		}
		if (deltas.Count > 0 && autoBase == null)
		{
			Append(Word.T("box.skipDelta"));
		}
		_busy = true;
		try
		{
			foreach (string item in list)
			{
				await DecryptAsync(item);
			}
			if (autoBase != null)
			{
				foreach (string item2 in deltas)
				{
					await DecryptAsync(item2, autoBase);
				}
			}
			foreach (string item3 in opts)
			{
				await DecryptAsync(item3);
			}
		}
		finally
		{
			_busy = false;
		}
	}

	private string? CheckFile(string path)
	{
		string fileName = Path.GetFileName(path);
		if (RbOpt.IsChecked == true)
		{
			if (!path.EndsWith(".opt", StringComparison.OrdinalIgnoreCase))
			{
				return Word.T("box.onlyOpt", fileName);
			}
			return null;
		}
		if (!path.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
		{
			return Word.T("box.onlyApp", fileName);
		}
		byte? b = Untie.BX(path);
		if (!b.HasValue)
		{
			return Word.T("box.unknownFile", fileName);
		}
		if (RbBase.IsChecked == true && b > 0)
		{
			return Word.T("box.isDelta", fileName);
		}
		if (RbDelta.IsChecked == true && b == 0)
		{
			return Word.T("box.isBase", fileName);
		}
		return null;
	}

	private async Task DecryptAsync(string path, string? baseFile = null)
	{
		string name = Path.GetFileName(path);
		Append(Word.T("box.decrypting", name));
		try
		{
			(byte[] key, byte[] iv)? keyHint = null;
			if (RbOpt.IsChecked != true)
			{
				string text = Untie.BZ(path);
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
							Append(Word.T("box.skipNoKey", name));
							return;
						}
						value = (manualKeyWindow.KeyBytes, manualKeyWindow.IvBytes);
						_manualKeys[text] = value;
					}
					keyHint = value;
				}
			}
			if (baseFile == null && RbDelta.IsChecked == true)
			{
				baseFile = _baseFile;
			}
			await Task.Run(delegate
			{
				Untie.Run(path, Append, baseFile, keyHint);
			});
			Append(Word.T("box.decryptDone", name));
		}
		catch (Exception ex)
		{
			Append(Word.T("box.decryptFail", name, ex.Message));
		}
	}

	private string[] GetFiles(DragEventArgs e)
	{
		if (!e.Data.GetDataPresent(DataFormats.FileDrop))
		{
			return Array.Empty<string>();
		}
		if (!(e.Data.GetData(DataFormats.FileDrop) is string[] source))
		{
			return Array.Empty<string>();
		}
		if (RbAuto.IsChecked == true)
		{
			return source.Where(IsSupported).ToArray();
		}
		string ext = ((RbOpt.IsChecked == true) ? ".opt" : ".app");
		return source.Where((string p) => p.EndsWith(ext, StringComparison.OrdinalIgnoreCase)).ToArray();
	}

	private static bool IsSupported(string path)
	{
		if (!path.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
		{
			return path.EndsWith(".opt", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	private void Append(string message)
	{
		if (string.IsNullOrEmpty(message))
		{
			return;
		}
		if (!base.Dispatcher.CheckAccess())
		{
			base.Dispatcher.Invoke(delegate
			{
				Append(message);
			});
		}
		else
		{
			TxtLog.AppendText(message + Environment.NewLine);
			TxtLog.ScrollToEnd();
		}
	}
}
