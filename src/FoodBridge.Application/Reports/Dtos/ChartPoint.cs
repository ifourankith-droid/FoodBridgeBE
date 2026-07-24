namespace FoodBridge.Application.Reports.Dtos;

/// <summary>One bar/line-chart data point. Period is "yyyy-MM" — sortable as a plain string, chart-ready as-is.</summary>
public sealed record ChartPoint(string Period, int Value);
