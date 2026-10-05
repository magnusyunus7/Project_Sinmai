using System;
using System.Windows;
using System.Windows.Markup;

namespace Project_Sinmai;

public partial class ScriptLogWindow : Window, IComponentConnector
{
	private readonly string _taskId;

	public ScriptLogWindow(string taskId)
	{
		InitializeComponent();
		_taskId = taskId;
		ScriptScheduler.Instance.LogAppended += OnLogAppended;
		ScriptScheduler.Instance.TasksChanged += OnTasksChanged;
		Word.Changed += RefreshInfo;
		TxtLog.Text = string.Join(Environment.NewLine, ScriptScheduler.Instance.GetLog(_taskId));
		TxtLog.ScrollToEnd();
		RefreshInfo();
	}

	private void OnLogAppended(string id, string line)
	{
		if (!(id != _taskId))
		{
			TxtLog.AppendText(line + Environment.NewLine);
			TxtLog.ScrollToEnd();
		}
	}

	private void OnTasksChanged()
	{
		RefreshInfo();
	}

	private void RefreshInfo()
	{
		if (!ScriptScheduler.Instance.TryGet(_taskId, out ScriptTask task))
		{
			base.Title = Word.T("view.titleFmt", _taskId);
			TxtInfo.Text = Word.T("view.deleted");
			BtnToggle.IsEnabled = false;
			return;
		}
		base.Title = Word.T("view.titleFmt2", task.Id, task.Game, task.Version);
		TxtInfo.Text = $"{task.Game} {task.Version}\u3000{Word.T("view.intervalLabel")}{task.IntervalText}\u3000{Word.T("view.statusLabel")}{task.StatusText}\u3000{Word.T("view.notifyLabel")}{task.NotifyText}\u3000{Word.T("view.nextLabel")}{task.NextRunText}\u3000{Word.T("view.pathLabel")}{task.DownloadPath}";
		BtnToggle.Content = ((task.Status == "running") ? Word.T("view.stop") : Word.T("view.start"));
		BtnToggle.IsEnabled = true;
	}

	private void BtnToggle_Click(object sender, RoutedEventArgs e)
	{
		if (ScriptScheduler.Instance.TryGet(_taskId, out ScriptTask task))
		{
			ScriptScheduler.Instance.SetRunning(_taskId, task.Status != "running");
		}
	}

	private void BtnReset_Click(object sender, RoutedEventArgs e)
	{
		ScriptScheduler.Instance.Reset(_taskId);
	}

	protected override void OnClosed(EventArgs e)
	{
		ScriptScheduler.Instance.LogAppended -= OnLogAppended;
		ScriptScheduler.Instance.TasksChanged -= OnTasksChanged;
		Word.Changed -= RefreshInfo;
		base.OnClosed(e);
	}
}
