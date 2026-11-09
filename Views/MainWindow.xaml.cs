using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Schedule_Management_with_Notion.Services.Notion;
using Schedule_Management_with_Notion.ViewModels;

namespace Schedule_Management_with_Notion.Views;

public partial class MainWindow : Window
{
    private static readonly HttpClient NotionHttpClient = new(new HttpClientHandler { AllowAutoRedirect = false })
    {
        Timeout = TimeSpan.FromSeconds(20)
    };

    private readonly MainViewModel viewModel = new(new NotionScheduleService(NotionHttpClient));

    public MainWindow()
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.Columns)) BuildScheduleColumns();
        };
        Loaded += LoadSchedules;
    }

    private async void LoadSchedules(object sender, RoutedEventArgs e)
    {
        Loaded -= LoadSchedules;
        await viewModel.LoadSchedulesCommand.ExecuteAsync(null);
    }

    private void BuildScheduleColumns()
    {
        ScheduleGrid.Columns.Clear();
        for (var index = 0; index < viewModel.Columns.Count; index++)
        {
            var property = viewModel.Columns[index];
            var binding = new Binding($"[{index}]") { Mode = BindingMode.OneWay };
            if (property.Type is "date" or "created_time" or "last_edited_time")
            {
                binding.StringFormat = "yyyy-MM-dd";
            }

            DataGridBoundColumn column = property.Type switch
            {
                "checkbox" => new DataGridCheckBoxColumn
                {
                    ElementStyle = (Style)FindResource("ScheduleCheckBoxStyle")
                },
                "select" or "multi_select" or "status" => new ScheduleBadgeColumn(
                    (DataTemplate)FindResource("ScheduleBadgeTemplate")),
                _ => new DataGridTextColumn
                {
                    ElementStyle = (Style)FindResource(property.Type switch
                    {
                        "title" => "ScheduleTitleStyle",
                        "number" => "ScheduleNumberStyle",
                        "date" or "created_time" or "last_edited_time" => "ScheduleDateStyle",
                        _ => "ScheduleTextStyle"
                    })
                }
            };

            column.Header = property.Name;
            column.Binding = binding;
            column.MinWidth = 96;
            column.MaxWidth = 360;
            if (property.Type == "title")
            {
                column.Width = new DataGridLength(1, DataGridLengthUnitType.Star);
                column.MinWidth = 240;
                column.MaxWidth = double.PositiveInfinity;
            }

            ScheduleGrid.Columns.Add(column);
        }
    }

    private sealed class ScheduleBadgeColumn(DataTemplate template) : DataGridBoundColumn
    {
        protected override FrameworkElement GenerateElement(DataGridCell cell, object dataItem)
        {
            var badge = new ContentControl { ContentTemplate = template, Focusable = false };
            BindingOperations.SetBinding(badge, ContentControl.ContentProperty, Binding);
            return badge;
        }

        protected override FrameworkElement GenerateEditingElement(DataGridCell cell, object dataItem) =>
            GenerateElement(cell, dataItem);
    }
}
