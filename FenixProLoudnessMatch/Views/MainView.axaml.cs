using Avalonia.Controls;
using ReactiveUI;
using System.IO;
using System.Reactive;
using System;
using System.Reactive.Linq;
using Avalonia.Threading;
using Avalonia.ReactiveUI;
using FenixProLoudnessMatch.ViewModels;
using Avalonia.Platform.Storage;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using System.Globalization;

namespace FenixProLoudnessMatch.Views;

public partial class MainView : ReactiveUserControl<MainViewModel>
{
    public MainView()
    {
        InitializeComponent();

        //float.Parse("-14.25".Replace(',', '.'), CultureInfo.InvariantCulture);

        if (Design.IsDesignMode)
            return;

        this.WhenActivated(d =>
        {
            d(this
                .ViewModel!
                .PickAFolder
                .RegisterHandler(async i =>
                {
                    // Get top level from the current control. Alternatively, you can use Window reference instead.
                    var topLevel = TopLevel.GetTopLevel(this);

                    if (topLevel == null)
                        return;

                    // Start async operation to open the dialog.
                    var folder = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions()
                    {
                        AllowMultiple = false,
                        Title = i.Input
                    });

                    if (folder.Count == 1)
                    {
                        i.SetOutput(folder[0].Path.LocalPath);
                    }
                    else
                    {
                        i.SetOutput(string.Empty);
                    }
                }));

            d(this
                .ViewModel!
                .MsgBoxError
                .RegisterHandler(async i =>
                {
                    var box = MessageBoxManager
                      .GetMessageBoxStandard(i.Input[0], i.Input[1], ButtonEnum.Ok, Icon.Error);

                    await box.ShowAsync();
                    i.SetOutput(Unit.Default);
                }));

            d(this
                .ViewModel!
                .MsgBoxInfo
                .RegisterHandler(async i =>
                {
                    var box = MessageBoxManager
                      .GetMessageBoxStandard(i.Input[0], i.Input[1], ButtonEnum.Ok, Icon.Info);

                    await box.ShowAsync();
                    i.SetOutput(Unit.Default);
                }));

            d(this
                .ViewModel!
                .MsgBoxYesNo
                .RegisterHandler(async i =>
                {
                    var box = MessageBoxManager
                      .GetMessageBoxStandard(i.Input[0], i.Input[1], ButtonEnum.YesNo, Icon.Question);

                    var result = await box.ShowAsync();

                    i.SetOutput(result == ButtonResult.Yes);
                }));

            d(this
                .ViewModel!
                .AddConsoleLine
                .RegisterHandler(async i =>
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        this.Console.Children.Add(new TextBlock()
                        {
                            Text = i.Input,
                            Padding = new Avalonia.Thickness(5, 0, 5, 0),
                            FontSize = 12,
                            FontFamily = "Verdana"
                        });

                        this.ConsoleScroll.ScrollToEnd();
                    });

                    i.SetOutput(Unit.Default);
                }));

            d(this
                .ViewModel!
                .ClearConsole
                .RegisterHandler(async i =>
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        this.Console.Children.Clear();
                    });

                    i.SetOutput(Unit.Default);
                }));
        });
    }
}
