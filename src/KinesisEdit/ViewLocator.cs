using Avalonia.Controls;
using Avalonia.Controls.Templates;
using KinesisEdit.ViewModels;

namespace KinesisEdit
{
    /// <summary>
    /// Resolves a view model to its view by name — <c>KinesisEdit.ViewModels.XxxViewModel</c>
    /// becomes <c>KinesisEdit.Views.XxxView</c> — so the shell can swap views by assigning a view
    /// model (specs/10-apps-and-ui.md, "Opening a device"). Registered in
    /// <c>App.axaml</c>'s <c>DataTemplates</c>, which makes it apply to every
    /// <c>ContentControl</c> and item container in the app.
    /// </summary>
    public sealed class ViewLocator : IDataTemplate
    {
        private const string ViewModelSuffix = "ViewModel";
        private const string ViewSuffix = "View";

        /// <summary>
        /// Returns the view type paired with <paramref name="viewModelType"/>, or null when the
        /// name maps to no type in the view model's assembly.
        /// </summary>
        public static Type? ResolveViewType(Type? viewModelType)
        {
            var viewModelName = viewModelType?.FullName;

            if (viewModelName is null)
            {
                return null;
            }

            var viewName = viewModelName.Replace(ViewModelSuffix, ViewSuffix, StringComparison.Ordinal);

            return viewModelType!.Assembly.GetType(viewName, throwOnError: false);
        }

        /// <summary>Creates the view for <paramref name="param"/>, or a diagnostic placeholder when none exists.</summary>
        public Control? Build(object? param)
        {
            if (param is null)
            {
                return null;
            }

            var viewType = ResolveViewType(param.GetType());

            if (viewType is null)
            {
                return new TextBlock
                {
                    Text = $"View not found for {param.GetType().FullName}"
                };
            }

            return (Control)Activator.CreateInstance(viewType)!;
        }

        /// <summary>Only the app's own view models are resolved this way.</summary>
        public bool Match(object? data)
        {
            return data is ViewModelBase;
        }
    }
}
