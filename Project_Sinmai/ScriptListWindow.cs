using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Markup;

namespace Project_Sinmai;

public partial class ScriptListWindow : Window, IComponentConnector
{
	private readonly ObservableCollection<ScriptTask> _items = new ObservableCollection<ScriptTask>();

	private readonly Dictionary<string, ScriptLogWindow> _logWindows = new Dictionary<string, ScriptLogWindow>();

	private ScriptTask? Selected => DgTasks.SelectedItem as ScriptTask;

	public ScriptListWindow()
	{
		InitializeComponent();
		DgTasks.ItemsSource = _items;
		ScriptScheduler.Instance.TasksChanged += RefreshList;
		Word.Changed += RefreshList;
		RefreshList();
	}

	private void RefreshList()
	{
		string text = Selected?.Id;
		_items.Clear();
		foreach (ScriptTask task in ScriptScheduler.Instance.Tasks)
		{
			_items.Add(task);
		}
		if (text == null)
		{
			return;
		}
		foreach (ScriptTask item in _items)
		{
			if (item.Id == text)
			{
				DgTasks.SelectedItem = item;
				break;
			}
		}
	}

	private void BtnNew_Click(object sender, RoutedEventArgs e)
	{
		ScriptEditWindow scriptEditWindow = new ScriptEditWindow(ScriptScheduler.Instance.Create())
		{
			Owner = this
		};
		if (scriptEditWindow.ShowDialog() == true)
		{
			if (!ScriptScheduler.Instance.Add(scriptEditWindow.Result, out string error))
			{
				MessageBox.Show(Word.T("jobs.saveFail", error), Word.T("common.error"), MessageBoxButton.OK, MessageBoxImage.Hand);
			}
			else
			{
				OpenLogWindow(scriptEditWindow.Result.Id);
			}
		}
	}

	private void BtnEdit_Click(object sender, RoutedEventArgs e)
	{
		if (Selected == null)
		{
			MessageBox.Show(Word.T("jobs.selectFirst"), Word.T("common.tip"), MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		ScriptEditWindow scriptEditWindow = new ScriptEditWindow(Selected.Clone())
		{
			Owner = this
		};
		if (scriptEditWindow.ShowDialog() == true && !ScriptScheduler.Instance.Update(scriptEditWindow.Result, out string error))
		{
			MessageBox.Show(Word.T("jobs.saveFail", error), Word.T("common.error"), MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private void BtnDelete_Click(object sender, RoutedEventArgs e)
	{
		if (Selected == null)
		{
			MessageBox.Show(Word.T("jobs.selectFirst"), Word.T("common.tip"), MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		ScriptTask selected = Selected;
		if (MessageBox.Show(Word.T("jobs.deleteConfirm", selected.Id, selected.Game, selected.Version), Word.T("jobs.deleteTitle"), MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes && !ScriptScheduler.Instance.Delete(selected, out string error))
		{
			MessageBox.Show(Word.T("jobs.deleteFail", error), Word.T("common.error"), MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private void BtnOpen_Click(object sender, RoutedEventArgs e)
	{
		if (Selected == null)
		{
			MessageBox.Show(Word.T("jobs.selectFirst"), Word.T("common.tip"), MessageBoxButton.OK, MessageBoxImage.Exclamation);
		}
		else
		{
			OpenLogWindow(Selected.Id);
		}
	}

	private void DgTasks_MouseDoubleClick(object sender, MouseButtonEventArgs e)
	{
		if (Selected != null)
		{
			OpenLogWindow(Selected.Id);
		}
	}

	private void BtnClose_Click(object sender, RoutedEventArgs e)
	{
		Close();
	}

	private void OpenLogWindow(string id)
	{
		if (_logWindows.TryGetValue(id, out ScriptLogWindow value))
		{
			value.Activate();
			return;
		}
		ScriptLogWindow scriptLogWindow = new ScriptLogWindow(id)
		{
			Owner = this
		};
		_logWindows[id] = scriptLogWindow;
		scriptLogWindow.Closed += delegate
		{
			_logWindows.Remove(id);
		};
		scriptLogWindow.Show();
	}

	protected override void OnClosed(EventArgs e)
	{
		ScriptScheduler.Instance.TasksChanged -= RefreshList;
		Word.Changed -= RefreshList;
		base.OnClosed(e);
	}
}
