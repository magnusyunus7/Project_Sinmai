using System.ComponentModel;

namespace Project_Sinmai;

public sealed class Loc : INotifyPropertyChanged
{
	public static Loc Instance { get; } = new Loc();

	public string this[string key] => Word.T(key);

	public event PropertyChangedEventHandler? PropertyChanged;

	private Loc()
	{
	}

	internal void Refresh()
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
	}
}
