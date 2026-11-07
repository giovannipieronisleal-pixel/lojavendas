using System.Globalization;
using Microsoft.Maui.Graphics;

namespace lojavendas.Pages;

internal sealed class RevenueChartDrawable(DateTime startDate, IReadOnlyList<decimal> dailyRevenue) : IDrawable
{
    private static readonly Color AxisColor = Color.FromArgb("#D7DBD2");
    private static readonly Color TextColor = Color.FromArgb("#727871");
    private static readonly Color LineColor = Color.FromArgb("#293F35");
    private static readonly CultureInfo BrazilianCulture = CultureInfo.GetCultureInfo("pt-BR");

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (dirtyRect.Width < 100 || dirtyRect.Height < 100 || dailyRevenue.Count == 0)
            return;

        const float left = 58;
        const float right = 12;
        const float top = 14;
        const float bottom = 34;
        var chartLeft = left;
        var chartRight = Math.Max(chartLeft + 1, dirtyRect.Width - right);
        var chartTop = top;
        var chartBottom = Math.Max(chartTop + 1, dirtyRect.Height - bottom);
        var maxValue = (double)dailyRevenue.Max();
        var yMax = maxValue <= 0 ? 1 : maxValue * 1.15;

        canvas.StrokeSize = 1;
        canvas.StrokeColor = AxisColor;
        canvas.FontColor = TextColor;
        canvas.FontSize = 10;

        const int horizontalLines = 4;
        for (var index = 0; index <= horizontalLines; index++)
        {
            var ratio = index / (float)horizontalLines;
            var y = chartTop + (chartBottom - chartTop) * ratio;
            var value = yMax * (1 - ratio);
            canvas.DrawLine(chartLeft, y, chartRight, y);
            canvas.DrawString(
                FormatAxisValue(value),
                0,
                y - 8,
                chartLeft - 7,
                16,
                HorizontalAlignment.Right,
                VerticalAlignment.Center);
        }

        var pointCount = dailyRevenue.Count;
        var xStep = pointCount <= 1 ? 0 : (chartRight - chartLeft) / (pointCount - 1);
        var tickCount = Math.Min(5, pointCount);
        for (var tick = 0; tick < tickCount; tick++)
        {
            var dataIndex = tickCount <= 1
                ? 0
                : (int)Math.Round(tick * (pointCount - 1d) / (tickCount - 1));
            var x = chartLeft + dataIndex * xStep;
            canvas.DrawString(
                startDate.AddDays(dataIndex).ToString("dd/MM", BrazilianCulture),
                x - 24,
                chartBottom + 8,
                48,
                18,
                HorizontalAlignment.Center,
                VerticalAlignment.Center);
        }

        canvas.StrokeColor = LineColor;
        canvas.StrokeSize = 2.5f;
        for (var index = 1; index < pointCount; index++)
        {
            var previousX = chartLeft + (index - 1) * xStep;
            var currentX = chartLeft + index * xStep;
            var previousY = chartBottom - (float)((double)dailyRevenue[index - 1] / yMax) * (chartBottom - chartTop);
            var currentY = chartBottom - (float)((double)dailyRevenue[index] / yMax) * (chartBottom - chartTop);
            canvas.DrawLine(previousX, previousY, currentX, currentY);
        }

        canvas.FillColor = LineColor;
        for (var index = 0; index < pointCount; index++)
        {
            var x = chartLeft + index * xStep;
            var y = chartBottom - (float)((double)dailyRevenue[index] / yMax) * (chartBottom - chartTop);
            canvas.FillCircle(x, y, pointCount > 40 ? 2 : 3);
        }
    }

    private static string FormatAxisValue(double value) =>
        value.ToString("N0", BrazilianCulture);
}

internal sealed class PaymentChartDrawable(IReadOnlyList<KeyValuePair<string, decimal>> amountsByMethod) : IDrawable
{
    private static readonly Color BarColor = Color.FromArgb("#52675E");
    private static readonly Color TrackColor = Color.FromArgb("#E9EBE5");
    private static readonly Color TextColor = Color.FromArgb("#727871");
    private static readonly CultureInfo BrazilianCulture = CultureInfo.GetCultureInfo("pt-BR");

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (dirtyRect.Width < 120 || dirtyRect.Height < 80 || amountsByMethod.Count == 0)
            return;

        const float labelWidth = 94;
        const float valueWidth = 74;
        const float verticalPadding = 12;
        var maxValue = (double)amountsByMethod.Max(item => item.Value);
        var rowHeight = Math.Max(26, (dirtyRect.Height - verticalPadding * 2) / amountsByMethod.Count);
        var barLeft = labelWidth;
        var barRight = Math.Max(barLeft + 1, dirtyRect.Width - valueWidth);
        var barWidth = barRight - barLeft;

        canvas.FontSize = 11;
        for (var index = 0; index < amountsByMethod.Count; index++)
        {
            var item = amountsByMethod[index];
            var centerY = verticalPadding + rowHeight * (index + 0.5f);
            var barHeight = Math.Min(16, rowHeight * 0.55f);
            var barY = centerY - barHeight / 2;
            var fillWidth = maxValue <= 0 ? 0 : barWidth * (float)((double)item.Value / maxValue);

            canvas.FontColor = TextColor;
            canvas.DrawString(
                TrimLabel(item.Key, 13),
                0,
                centerY - 10,
                labelWidth - 8,
                20,
                HorizontalAlignment.Left,
                VerticalAlignment.Center);

            canvas.FillColor = TrackColor;
            canvas.FillRectangle(barLeft, barY, barWidth, barHeight);
            canvas.FillColor = BarColor;
            canvas.FillRectangle(barLeft, barY, fillWidth, barHeight);

            canvas.FontColor = Color.FromArgb("#293F35");
            canvas.FontSize = 10;
            canvas.DrawString(
                item.Value.ToString("C0", BrazilianCulture),
                barRight + 6,
                centerY - 10,
                valueWidth - 6,
                20,
                HorizontalAlignment.Left,
                VerticalAlignment.Center);
            canvas.FontSize = 11;
        }
    }

    private static string TrimLabel(string value, int maxLength) =>
        value.Length <= maxLength ? value : $"{value[..(maxLength - 1)]}…";
}
