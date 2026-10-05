using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace SEGADownloadTool_Sinmai;

public partial class IcfEditWindow : Window, IComponentConnector
{
	private sealed class RowVm
	{
		public LoomKind Kind { get; set; }

		public uint Marker { get; set; }

		public string Version { get; set; } = "";

		public string DateText { get; set; } = "";

		public string ReqText { get; set; } = "";

		public string SrcVerText { get; set; } = "";

		public string SrcDateText { get; set; } = "";

		public string SrcReqText { get; set; } = "";

		public string StatusText { get; set; } = "";

		public string Preview { get; set; } = "";

		public string KindText => Kind switch
		{
			LoomKind.P0 => "Pack", 
			LoomKind.P1 => "App", 
			LoomKind.P2 => "Opt", 
			_ => "App (delta)", 
		};
	}

	private readonly ObservableCollection<RowVm> _rows = new ObservableCollection<RowVm>();

	private string? _path;

	private bool _loading;

	private Point _dragStart;

	private bool _dragArmed;

	private bool _dragActive;

	private int _dragFrom = -1;

	private Vector _dragGrab;

	private Popup? _dragPopup;

	private DataGridRow? _dragRow;

	public IcfEditWindow()
	{
		InitializeComponent();
		Word.Changed += OnLanguageChanged;
		DgRows.ItemsSource = _rows;
		TxtIdA.Text = "SDEZ";
		TxtIdB.Text = "ACA";
		base.Closing += delegate
		{
			_loading = true;
			Word.Changed -= OnLanguageChanged;
		};
	}

	private void OnLanguageChanged()
	{
		DgRows.Items.Refresh();
	}

	private static string BaseDir()
	{
		return Path.GetDirectoryName(Environment.ProcessPath) ?? Environment.CurrentDirectory;
	}

	private string CurIdA()
	{
		return TxtIdA.Text.Trim().ToUpperInvariant();
	}

	private string CurIdB()
	{
		return TxtIdB.Text.Trim().ToUpperInvariant();
	}

	private static DateTime ParseSoft(string s)
	{
		if (!DateTime.TryParseExact(s, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
		{
			return new DateTime(2000, 1, 1);
		}
		return result;
	}

	private static DateTime ParseStrict(string s, int no)
	{
		if (!DateTime.TryParseExact(s, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
		{
			throw new FormatException(Word.T("dint.badDate", no, s));
		}
		return result;
	}

	private string MakePreview(RowVm vm)
	{
		return new LoomRow
		{
			v0 = vm.Marker,
			v1 = vm.Kind,
			v2 = vm.Version,
			v3 = ParseSoft(vm.DateText),
			v5 = vm.SrcVerText
		}.N0(CurIdA(), CurIdB());
	}

	private static RowVm FromRow(LoomRow r)
	{
		return new RowVm
		{
			Kind = r.v1,
			Marker = r.v0,
			Version = r.v2,
			DateText = r.v3.ToString("yyyy-MM-dd HH:mm:ss"),
			ReqText = r.v4,
			SrcVerText = r.v5,
			SrcDateText = r.v6.ToString("yyyy-MM-dd HH:mm:ss"),
			SrcReqText = r.v7,
			StatusText = LoomRow.S0(r.v0)
		};
	}

	private static LoomRow ToRow(RowVm vm, int no)
	{
		return new LoomRow
		{
			v0 = ((vm.Marker & 0xFFFF0000u) | (LoomRow.S1(vm.StatusText) & 0xFFFF)),
			v1 = vm.Kind,
			v2 = vm.Version,
			v3 = ParseStrict(vm.DateText, no),
			v4 = vm.ReqText,
			v5 = vm.SrcVerText,
			v6 = ParseStrict(vm.SrcDateText, no),
			v7 = vm.SrcReqText
		};
	}

	private void BtnOpen_Click(object sender, RoutedEventArgs e)
	{
		OpenFileDialog openFileDialog = new OpenFileDialog
		{
			Title = Word.T("dint.openTitle"),
			Filter = Word.T("dint.openFilter"),
			InitialDirectory = BaseDir()
		};
		if (openFileDialog.ShowDialog(this) == true)
		{
			LoadIcf(openFileDialog.FileName);
		}
	}

	private void LoadIcf(string path)
	{
		try
		{
			Loom loom = Loom.E0(path);
			_rows.Clear();
			foreach (LoomRow item in loom.e2)
			{
				_rows.Add(FromRow(item));
			}
			TxtIdA.Text = loom.e0;
			TxtIdB.Text = loom.e1;
			_path = path;
			base.Title = Word.T("dint.titleFmt", Path.GetFileName(path));
			TxtStatus.Text = Word.T("dint.loaded", Path.GetFileName(path), _rows.Count);
			RefreshPreviews();
			if (_rows.Count > 0)
			{
				DgRows.SelectedIndex = 0;
			}
			else
			{
				ClearDetail();
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show(ex.Message, Word.T("dint.title"), MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private void BtnSave_Click(object sender, RoutedEventArgs e)
	{
		if (_path == null)
		{
			BtnSaveAs_Click(sender, e);
		}
		else
		{
			TrySave(_path);
		}
	}

	private void BtnSaveAs_Click(object sender, RoutedEventArgs e)
	{
		SaveFileDialog saveFileDialog = new SaveFileDialog
		{
			Title = Word.T("dint.saveTitle"),
			Filter = Word.T("dint.saveFilter"),
			AddExtension = false,
			FileName = ((_path == null) ? "output.icf" : Path.GetFileName(_path)),
			InitialDirectory = ((_path == null) ? BaseDir() : (Path.GetDirectoryName(_path) ?? BaseDir()))
		};
		if (saveFileDialog.ShowDialog(this) == true)
		{
			TrySave(saveFileDialog.FileName);
		}
	}

	private void TrySave(string path)
	{
		try
		{
			Loom loom = new Loom
			{
				e0 = CurIdA(),
				e1 = CurIdB()
			};
			for (int i = 0; i < _rows.Count; i++)
			{
				loom.e2.Add(ToRow(_rows[i], i + 1));
			}
			bool flag = File.Exists(path);
			if (flag)
			{
				File.Copy(path, path + ".bak", overwrite: true);
			}
			loom.E1(path);
			_path = path;
			base.Title = Word.T("dint.titleFmt", Path.GetFileName(path));
			TxtStatus.Text = (flag ? Word.T("dint.savedBackup", path) : Word.T("dint.saved", path));
		}
		catch (Exception ex)
		{
			MessageBox.Show(ex.Message, Word.T("dint.title"), MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private void BtnAdd_Click(object sender, RoutedEventArgs e)
	{
		LoomKind kind = CmbNewKind.SelectedIndex switch
		{
			0 => LoomKind.P0, 
			2 => LoomKind.P2, 
			3 => LoomKind.P3, 
			_ => LoomKind.P1, 
		};
		RowVm rowVm = new RowVm
		{
			Kind = kind,
			Marker = 258u,
			Version = "",
			DateText = "",
			ReqText = "",
			SrcVerText = "",
			SrcDateText = "",
			SrcReqText = "",
			StatusText = ""
		};
		rowVm.Preview = MakePreview(rowVm);
		_rows.Add(rowVm);
		DgRows.Items.Refresh();
		DgRows.SelectedItem = rowVm;
		DgRows.ScrollIntoView(rowVm);
		TxtStatus.Text = Word.T("dint.added", _rows.Count);
	}

	private void BtnDelete_Click(object sender, RoutedEventArgs e)
	{
		if (DgRows.SelectedItems.Count == 0)
		{
			return;
		}
		List<RowVm> list = DgRows.SelectedItems.Cast<RowVm>().ToList();
		foreach (RowVm item in list)
		{
			_rows.Remove(item);
		}
		DgRows.Items.Refresh();
		TxtStatus.Text = Word.T("dint.deleted", list.Count, _rows.Count);
	}

	private void DgRows_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (!(DgRows.SelectedItem is RowVm rowVm))
		{
			ClearDetail();
			return;
		}
		_loading = true;
		CmbKind.SelectedIndex = (int)((rowVm.Kind == LoomKind.P4) ? LoomKind.P3 : rowVm.Kind);
		TxtVer.Text = rowVm.Version;
		TxtDate.Text = rowVm.DateText;
		TxtReq.Text = rowVm.ReqText;
		TxtSrcVer.Text = rowVm.SrcVerText;
		TxtSrcDate.Text = rowVm.SrcDateText;
		TxtSrcReq.Text = rowVm.SrcReqText;
		TxtSfx.Text = rowVm.StatusText;
		_loading = false;
		ToggleFields(rowVm);
		TxtPreview.Text = rowVm.Preview;
	}

	private void ClearDetail()
	{
		_loading = true;
		CmbKind.SelectedIndex = -1;
		TxtVer.Text = "";
		TxtDate.Text = "";
		TxtReq.Text = "";
		TxtSrcVer.Text = "";
		TxtSrcDate.Text = "";
		TxtSrcReq.Text = "";
		TxtSfx.Text = "";
		TxtPreview.Text = "";
		_loading = false;
	}

	private void Detail_TextChanged(object sender, TextChangedEventArgs e)
	{
		PushDetail();
	}

	private void Detail_KindChanged(object sender, SelectionChangedEventArgs e)
	{
		PushDetail();
	}

	private void PushDetail()
	{
		if (_loading || !(DgRows.SelectedItem is RowVm rowVm))
		{
			return;
		}
		if (rowVm.Kind == LoomKind.P4)
		{
			if (CmbKind.SelectedIndex != 3)
			{
				_loading = true;
				CmbKind.SelectedIndex = 3;
				_loading = false;
			}
		}
		else if (CmbKind.SelectedIndex >= 0)
		{
			rowVm.Kind = (LoomKind)CmbKind.SelectedIndex;
		}
		rowVm.Version = TxtVer.Text;
		rowVm.DateText = TxtDate.Text;
		rowVm.ReqText = TxtReq.Text;
		rowVm.SrcVerText = TxtSrcVer.Text;
		rowVm.SrcDateText = TxtSrcDate.Text;
		rowVm.SrcReqText = TxtSrcReq.Text;
		rowVm.StatusText = TxtSfx.Text;
		try
		{
			rowVm.Marker = (rowVm.Marker & 0xFFFF0000u) | (LoomRow.S1(TxtSfx.Text) & 0xFFFF);
		}
		catch
		{
		}
		rowVm.Preview = MakePreview(rowVm);
		DgRows.Items.Refresh();
		ToggleFields(rowVm);
	}

	private void ToggleFields(RowVm vm)
	{
		LoomKind kind = vm.Kind;
		bool flag = (uint)(kind - 3) <= 1u;
		bool flag2 = flag;
		TxtReq.IsEnabled = vm.Kind != LoomKind.P2;
		TxtSrcVer.IsEnabled = flag2;
		TxtSrcDate.IsEnabled = flag2;
		TxtSrcReq.IsEnabled = flag2;
		if (!flag2)
		{
			if (TxtSrcVer.Text.Length == 0)
			{
				TxtSrcVer.Text = "1.00.00";
			}
			if (TxtSrcDate.Text.Length == 0)
			{
				TxtSrcDate.Text = "2000-01-01 00:00:00";
			}
			if (TxtSrcReq.Text.Length == 0)
			{
				TxtSrcReq.Text = "111.01.01";
			}
		}
	}

	private void TxtId_TextChanged(object sender, TextChangedEventArgs e)
	{
		RefreshPreviews();
	}

	private void RefreshPreviews()
	{
		foreach (RowVm row in _rows)
		{
			row.Preview = MakePreview(row);
		}
		DgRows.Items.Refresh();
	}

	private void DgRows_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		_dragStart = e.GetPosition(DgRows);
		_dragArmed = true;
	}

	private void DgRows_MouseMove(object sender, MouseEventArgs e)
	{
		if (_dragActive)
		{
			if (e.LeftButton != MouseButtonState.Pressed)
			{
				DgRows.ReleaseMouseCapture();
				EndDragCore();
			}
			else if (_dragPopup != null)
			{
				Point position = e.GetPosition(DgRows);
				_dragPopup.HorizontalOffset = position.X - _dragGrab.X;
				_dragPopup.VerticalOffset = position.Y - _dragGrab.Y;
			}
		}
		else if (_dragArmed && e.LeftButton == MouseButtonState.Pressed && _rows.Count != 0)
		{
			Point position2 = e.GetPosition(DgRows);
			if (!(Math.Abs(position2.X - _dragStart.X) < 4.0) || !(Math.Abs(position2.Y - _dragStart.Y) < 4.0))
			{
				BeginDrag(position2);
			}
		}
	}

	private void DgRows_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		if (!_dragActive)
		{
			_dragArmed = false;
			return;
		}
		DgRows.ReleaseMouseCapture();
		EndDragCore();
		Point position = e.GetPosition(DgRows);
		if (!(position.X >= 0.0) || !(position.X <= DgRows.ActualWidth) || !(position.Y >= 0.0) || !(position.Y <= DgRows.ActualHeight))
		{
			e.Handled = true;
			return;
		}
		RowVm rowAtPoint = GetRowAtPoint(position);
		int num = ((rowAtPoint != null) ? _rows.IndexOf(rowAtPoint) : (_rows.Count - 1));
		if (num >= 0 && num != _dragFrom && _dragFrom >= 0)
		{
			RowVm rowVm = _rows[_dragFrom];
			_rows.Move(_dragFrom, num);
			DgRows.SelectedItem = rowVm;
			DgRows.ScrollIntoView(rowVm);
			TxtStatus.Text = Word.T("dint.moved", num + 1);
		}
		e.Handled = true;
	}

	private void BeginDrag(Point pos)
	{
		_dragArmed = false;
		RowVm rowAtPoint = GetRowAtPoint(_dragStart);
		if (rowAtPoint == null)
		{
			return;
		}
		int num = _rows.IndexOf(rowAtPoint);
		if (num >= 0)
		{
			double num2 = 24.0;
			if (DgRows.ItemContainerGenerator.ContainerFromItem(rowAtPoint) is DataGridRow { ActualHeight: >1.0 } dataGridRow)
			{
				num2 = dataGridRow.ActualHeight;
			}
			double num3 = ((DgRows.ActualWidth > 1.0) ? DgRows.ActualWidth : 640.0);
			StackPanel stackPanel = new StackPanel
			{
				Orientation = Orientation.Horizontal,
				VerticalAlignment = VerticalAlignment.Center
			};
			stackPanel.Children.Add(GhostCell(rowAtPoint.KindText, 72.0));
			stackPanel.Children.Add(GhostCell(rowAtPoint.Version, 104.0));
			stackPanel.Children.Add(GhostCell(rowAtPoint.DateText, 148.0));
			stackPanel.Children.Add(GhostCell(rowAtPoint.Preview, Math.Max(120.0, num3 - 340.0)));
			Border child = new Border
			{
				Background = Brushes.White,
				BorderBrush = new SolidColorBrush(Color.FromRgb(154, 154, 154)),
				BorderThickness = new Thickness(1.0),
				Width = num3,
				Height = num2,
				Child = stackPanel,
				IsHitTestVisible = false
			};
			_dragGrab = new Vector(_dragStart.X, num2 / 2.0);
			_dragPopup = new Popup
			{
				Placement = PlacementMode.RelativePoint,
				PlacementTarget = DgRows,
				StaysOpen = true,
				AllowsTransparency = true,
				Child = child,
				HorizontalOffset = pos.X - _dragGrab.X,
				VerticalOffset = pos.Y - _dragGrab.Y
			};
			_dragPopup.IsOpen = true;
			_dragFrom = num;
			_dragRow = DgRows.ItemContainerGenerator.ContainerFromItem(rowAtPoint) as DataGridRow;
			if (_dragRow != null)
			{
				_dragRow.Opacity = 0.35;
			}
			_dragActive = true;
			DgRows.CaptureMouse();
		}
	}

	private static TextBlock GhostCell(string text, double width)
	{
		return new TextBlock
		{
			Text = text,
			Width = width,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(6.0, 0.0, 0.0, 0.0),
			TextTrimming = TextTrimming.CharacterEllipsis
		};
	}

	private void EndDragCore()
	{
		_dragActive = false;
		_dragArmed = false;
		if (_dragPopup != null)
		{
			_dragPopup.IsOpen = false;
			_dragPopup = null;
		}
		if (_dragRow != null)
		{
			_dragRow.Opacity = 1.0;
			_dragRow = null;
		}
	}

	private RowVm? GetRowAtPoint(Point p)
	{
		DependencyObject dependencyObject = DgRows.InputHitTest(p) as DependencyObject;
		while (dependencyObject != null && !(dependencyObject is DataGridRow))
		{
			dependencyObject = VisualTreeHelper.GetParent(dependencyObject);
		}
		if (dependencyObject is DataGridRow { Item: RowVm item })
		{
			return item;
		}
		return null;
	}

	private void DgRows_FileDragOver(object sender, DragEventArgs e)
	{
		e.Effects = (e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None);
		e.Handled = true;
	}

	private async void DgRows_FileDrop(object sender, DragEventArgs e)
	{
		if (e.Data.GetData(DataFormats.FileDrop) is string[] array && array.Length != 0)
		{
			string path = array.FirstOrDefault((string p) => Path.GetExtension(p).Equals(".icf", StringComparison.OrdinalIgnoreCase) || Path.GetFileName(p).StartsWith("ICF", StringComparison.OrdinalIgnoreCase)) ?? array[0];
			await Dispatcher.Yield(DispatcherPriority.Background);
			LoadIcf(path);
		}
	}

	private void DropArea_DragOver(object sender, DragEventArgs e)
	{
		e.Effects = (e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None);
		e.Handled = true;
	}

	private async void DropArea_Drop(object sender, DragEventArgs e)
	{
		object data = e.Data.GetData(DataFormats.FileDrop);
		string[] paths = data as string[];
		if (paths != null && paths.Length != 0)
		{
			await Dispatcher.Yield(DispatcherPriority.Background);
			AddFromFiles(paths);
		}
	}

	private void AddFromFiles(string[] paths)
	{
		List<(RowVm, string)> list = new List<(RowVm, string)>();
		List<string> list2 = new List<string>();
		for (int i = 0; i < paths.Length; i++)
		{
			string fileName = Path.GetFileName(paths[i]);
			if (TryParseFileRow(fileName, out RowVm vm, out string idPrefix))
			{
				list.Add((vm, idPrefix));
			}
			else
			{
				list2.Add(fileName);
			}
		}
		if (list.Count == 0)
		{
			TxtStatus.Text = Word.T("dint.noImport");
			if (list2.Count > 0)
			{
				MessageBox.Show(Word.T("dint.badFiles") + string.Join("\n", list2), Word.T("dint.title"), MessageBoxButton.OK, MessageBoxImage.Asterisk);
			}
			return;
		}
		string cur = CurIdA();
		if (list.Any<(RowVm, string)>(((RowVm Vm, string Id) tuple) => tuple.Vm.Kind != LoomKind.P0 && !string.Equals(tuple.Id, cur, StringComparison.OrdinalIgnoreCase)))
		{
			switch (AskForeignAction())
			{
			case null:
			case "cancel":
				TxtStatus.Text = Word.T("dint.importCancelled");
				return;
			case "match":
				list = list.Where<(RowVm, string)>(((RowVm Vm, string Id) tuple) => tuple.Vm.Kind == LoomKind.P0 || string.Equals(tuple.Id, cur, StringComparison.OrdinalIgnoreCase)).ToList();
				break;
			}
		}
		int num = 0;
		foreach (var g in list)
		{
			if (g.Item1.Kind != LoomKind.P2 || !_rows.Any((RowVm r) => r.Kind == LoomKind.P2 && string.Equals(r.Version, g.Item1.Version, StringComparison.OrdinalIgnoreCase)) || MessageBox.Show(this, Word.T("dint.dupConfirm"), Word.T("dint.title"), MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
			{
				g.Item1.Preview = MakePreview(g.Item1);
				_rows.Add(g.Item1);
				num++;
			}
		}
		if (num > 0)
		{
			DgRows.Items.Refresh();
			DataGrid dgRows = DgRows;
			ObservableCollection<RowVm> rows = _rows;
			dgRows.SelectedItem = rows[rows.Count - 1];
			DataGrid dgRows2 = DgRows;
			ObservableCollection<RowVm> rows2 = _rows;
			dgRows2.ScrollIntoView(rows2[rows2.Count - 1]);
			TxtStatus.Text = Word.T("dint.imported", num, _rows.Count);
		}
		else
		{
			TxtStatus.Text = Word.T("dint.importedNone");
		}
	}

	private string? AskForeignAction()
	{
		Window window = new Window
		{
			Title = Word.T("dint.title"),
			Width = 480.0,
			SizeToContent = SizeToContent.Height,
			WindowStartupLocation = WindowStartupLocation.CenterOwner,
			Owner = this,
			ResizeMode = ResizeMode.NoResize,
			ShowInTaskbar = false
		};
		StackPanel stackPanel = new StackPanel
		{
			Margin = new Thickness(16.0)
		};
		stackPanel.Children.Add(new TextBlock
		{
			Text = Word.T("dint.foreign"),
			TextWrapping = TextWrapping.Wrap
		});
		StackPanel stackPanel2 = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right,
			Margin = new Thickness(0.0, 14.0, 0.0, 0.0)
		};
		string result = null;
		stackPanel2.Children.Add(MakeChoice(Word.T("dint.foreignCancel"), "cancel", window, delegate
		{
			result = "cancel";
		}));
		stackPanel2.Children.Add(MakeChoice(Word.T("dint.foreignMatch"), "match", window, delegate
		{
			result = "match";
		}));
		stackPanel2.Children.Add(MakeChoice(Word.T("dint.foreignAll"), "all", window, delegate
		{
			result = "all";
		}));
		stackPanel.Children.Add(stackPanel2);
		window.Content = stackPanel;
		window.ShowDialog();
		return result;
	}

	private Button MakeChoice(string text, string value, Window win, Action set)
	{
		Button button = new Button();
		button.Content = text;
		button.MinWidth = 90.0;
		button.Height = 28.0;
		button.Margin = new Thickness(6.0, 0.0, 0.0, 0.0);
		button.Click += delegate
		{
			set();
			win.Close();
		};
		return button;
	}

	private void TxtVer_LostFocus(object sender, RoutedEventArgs e)
	{
		if (_loading || _dragActive)
		{
			return;
		}
		object selectedItem = DgRows.SelectedItem;
		RowVm vm = selectedItem as RowVm;
		if (vm == null || vm.Kind != LoomKind.P2)
		{
			return;
		}
		string tag = TxtVer.Text.Trim();
		if (tag.Length != 0 && _rows.Any((RowVm r) => r != vm && r.Kind == LoomKind.P2 && string.Equals(r.Version, tag, StringComparison.OrdinalIgnoreCase)) && MessageBox.Show(this, Word.T("dint.dupConfirm"), Word.T("dint.title"), MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
		{
			int val = _rows.IndexOf(vm);
			_rows.Remove(vm);
			DgRows.Items.Refresh();
			TxtStatus.Text = Word.T("dint.dupDeleted", _rows.Count);
			if (_rows.Count > 0)
			{
				DgRows.SelectedIndex = Math.Min(val, _rows.Count - 1);
			}
			else
			{
				ClearDetail();
			}
		}
	}

	private static bool TryParseFileRow(string fileName, out RowVm vm, out string idPrefix)
	{
		vm = new RowVm
		{
			Marker = 258u
		};
		idPrefix = "";
		string text = Path.GetExtension(fileName).ToLowerInvariant();
		bool flag;
		switch (text)
		{
		case ".pack":
		case ".app":
		case ".opt":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (!flag)
		{
			return false;
		}
		string[] array = Path.GetFileNameWithoutExtension(fileName).Split('_');
		if (array.Length < 4)
		{
			return false;
		}
		if (!array[2].All(char.IsDigit) || array[2].Length != 14)
		{
			return false;
		}
		if (!DateTime.TryParseExact(array[2], "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
		{
			return false;
		}
		idPrefix = array[0];
		string srcVerText = "";
		LoomKind kind;
		string version;
		if (text == ".app" && array.Length >= 5 && array[^2] == "1")
		{
			kind = LoomKind.P3;
			version = array[1];
			srcVerText = array[^1];
		}
		else
		{
			if (!(array[^1] == "0") || array.Length != 4)
			{
				return false;
			}
			kind = ((!(text == ".pack")) ? ((!(text == ".opt")) ? LoomKind.P1 : LoomKind.P2) : LoomKind.P0);
			version = array[1];
		}
		vm.Kind = kind;
		vm.Version = version;
		vm.DateText = result.ToString("yyyy-MM-dd HH:mm:ss");
		vm.ReqText = "";
		vm.SrcVerText = srcVerText;
		vm.SrcDateText = "";
		vm.SrcReqText = "";
		vm.StatusText = "";
		return true;
	}
}
