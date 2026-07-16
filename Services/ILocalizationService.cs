using System.ComponentModel;
using System.Threading.Tasks;

namespace LauncherRoot.Services;

public interface ILocalizationService : INotifyPropertyChanged
{
    string this[string key] { get; }
    string CurrentLanguage { get; }
    Task SetLanguageAsync(string code);
}
