using System.Collections.Generic;
using LauncherRoot.Models;

namespace LauncherRoot.Services;

public interface IThemeService
{
    List<Theme> GetThemes();
}
