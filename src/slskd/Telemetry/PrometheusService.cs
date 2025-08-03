// <copyright file="PrometheusService.cs" company="slskd Team">
//     Copyright (c) slskd Team. All rights reserved.
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU Affero General Public License as published
//     by the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU Affero General Public License for more details.
//
//     You should have received a copy of the GNU Affero General Public License
//     along with this program.  If not, see https://www.gnu.org/licenses/.
// </copyright>

namespace slskd;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

public class PrometheusService
{
    private static Regex PrometheusLineSplittingRegex { get; } = new Regex(@"([a-zA-Z_:][a-zA-Z0-9_:]*)(?:{(.*)})?\s+([^\s]+)", RegexOptions.Compiled);
    private static Regex PrometheusLabelSplittingRegex { get; } = new Regex("([a-zA-Z_][a-zA-Z0-9_]*)\\s*=\\s*\"([^\"]*)\"", RegexOptions.Compiled);

    /// <summary>
    ///     Get the current list of Prometheus metrics in Prometheus exposition format (text based).
    /// </summary>
    /// <returns></returns>
    public virtual async Task<string> GetMetricsAsString()
    {
        using var stream = new MemoryStream();
        using var reader = new StreamReader(stream);

        await Prometheus.Metrics.DefaultRegistry.CollectAndExportAsTextAsync(stream);
        stream.Position = 0;

        return await reader.ReadToEndAsync();
    }

    /// <summary>
    ///     Get the current list of Prometheus metrics in object format.
    /// </summary>
    /// <param name="include">An optional list of filters. If omitted, all metrics are included.</param>
    /// <returns></returns>
    public virtual async Task<Dictionary<string, PrometheusMetric>> GetMetricsAsObject(List<Regex> include = null)
    {
        var prometheusText = await GetMetricsAsString();

        var metrics = new Dictionary<string, PrometheusMetric>();

        if (string.IsNullOrWhiteSpace(prometheusText))
        {
            return metrics;
        }

        var lines = prometheusText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var helpLineIndexes = lines
            .Select((line, index) => new { line, index })
            .Where(x => x.line.StartsWith("# HELP", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.index).ToArray();

        var offsets = helpLineIndexes.Select((startIndex, i) =>
        {
            var endIndex = i < helpLineIndexes.Length - 1
                ? helpLineIndexes[i + 1] - 1
                : lines.Length - 1;
            return (startIndex, endIndex);
        });

        foreach (var (startIndex, endIndex) in offsets)
        {
            var header = lines[startIndex].Substring(7).Split(' ', 2);
            var name = header[0];
            var help = header[1];

            if (include is not null && !include.Any(regex => regex.IsMatch(name)))
            {
                continue;
            }

            var type = lines[startIndex + 1].Substring(7).Split(' ', 2)[1];
            var metric = new PrometheusMetric() { Name = name, Help = help, Type = type };

            for (int i = startIndex + 2; i <= endIndex; i++)
            {
                var match = PrometheusLineSplittingRegex.Match(lines[i]);

                if (match.Success)
                {
                    var groups = match.Groups;
                    var sampleName = groups[1].Value;
                    var sampleLabels = groups[2].Value;
                    var sampleValue = double.Parse(groups[3].Value);
                    var labels = ParseLabels(sampleLabels);

                    if (type.Equals("counter") || type.Equals("gauge"))
                    {
                        metric.Samples ??= [];
                        metric.Samples.Add(new PrometheusMetricSample() { Labels = labels, Value = sampleValue });
                    }
                    else if (type.Equals("histogram"))
                    {
                        if (sampleName.EndsWith("sum"))
                        {
                            metric.Sum = sampleValue;
                        }
                        else if (sampleName.EndsWith("count"))
                        {
                            metric.Count = sampleValue;
                        }
                        else
                        {
                            var le = labels.FirstOrDefault(label => label.Key == "le");

                            metric.Buckets ??= [];
                            metric.Buckets.Add(le.Value, new PrometheusMetricSample() { Labels = labels, Value = sampleValue });
                        }
                    }
                    else if (type.Equals("summary"))
                    {
                        if (sampleName.EndsWith("sum"))
                        {
                            metric.Sum = sampleValue;
                        }
                        else if (sampleName.EndsWith("count"))
                        {
                            metric.Count = sampleValue;
                        }
                        else
                        {
                            var quantile = labels.FirstOrDefault(label => label.Key == "quantile");

                            metric.Quantiles ??= [];
                            metric.Quantiles.Add(quantile.Value, sampleValue);
                        }
                    }
                }
            }

            metrics[name] = metric;
        }

        return metrics;
    }

    private static Dictionary<string, string> ParseLabels(string labelString)
    {
        if (string.IsNullOrEmpty(labelString))
        {
            return null;
        }

        var matches = PrometheusLabelSplittingRegex.Matches(labelString);

        if (matches.Count == 0)
        {
            return null;
        }

        Dictionary<string, string> labels = [];

        foreach (Match match in matches)
        {
            var key = match.Groups[1].Value;
            var value = match.Groups[2].Value;
            labels[key] = value;
        }

        return labels;
    }
}