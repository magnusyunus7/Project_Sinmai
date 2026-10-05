using System.Windows;

namespace Project_Sinmai;

public partial class App : Application
{
	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);
		Ward.V1();
		Ward.V2();
	}
}
