import { formatBytes, formatSpeed, formatWait } from '../../lib/util';
import React, { useEffect, useMemo, useState } from 'react';
import {
  Area,
  AreaChart,
  CartesianGrid,
  Legend,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';

export const useDarkMode = () => {
  const [isDark, setIsDark] = useState(() =>
    document.documentElement.classList.contains('dark'),
  );

  useEffect(() => {
    const observer = new MutationObserver(() => {
      setIsDark(document.documentElement.classList.contains('dark'));
    });
    observer.observe(document.documentElement, { attributeFilter: ['class'] });
    return () => observer.disconnect();
  }, []);

  return isDark;
};

const UNIT_FORMATTERS = {
  bytes: formatBytes,
  count: (v) => v.toLocaleString(),
  rate: (v) => `${v.toFixed(1)}%`,
  ratio: (v) => v.toFixed(2),
  seconds: formatWait,
  speed: formatSpeed,
};

const mixedFormatter = (v) => {
  if (v >= 1e9) return `${(v / 1e9).toFixed(1)}B`;
  if (v >= 1e6) return `${(v / 1e6).toFixed(1)}M`;
  if (v >= 1e3) return `${(v / 1e3).toFixed(1)}K`;
  return v.toLocaleString();
};

const X_TICK_COUNT = 11;
const ONE_DAY_MS = 86_400_000;

// series: [{ key, name, color, unit?, format? }]
//   key    — the data key in each data point
//   name   — label shown in the legend and tooltip
//   color  — stroke / fill color
//   unit   — one of: 'bytes' | 'count' | 'rate' | 'ratio' | 'seconds' | 'speed'
//             used to pick the Y axis formatter; omit for unitless/mixed
//   format — (value) => string used in the tooltip; defaults to toLocaleString()
const Graph = ({ data = [], defaultSeries, height = 200, series = [] }) => {
  const isDark = useDarkMode();

  const [visible, setVisible] = useState(
    () => defaultSeries ?? new Set(series.map((s) => s.key)),
  );

  const axisStyle = useMemo(
    () => ({
      fill: isDark ? '#8b949e' : '#666',
      fontSize: '0.8em',
    }),
    [isDark],
  );

  const xRange = useMemo(() => {
    if (data.length < 2) return null;
    const min = data[0].timestamp;
    const max = data[data.length - 1].timestamp;
    return { min, max };
  }, [data]);

  const xTicks = useMemo(() => {
    if (!xRange) return [];
    const { max, min } = xRange;
    const step = (max - min) / (X_TICK_COUNT - 1);
    return Array.from({ length: X_TICK_COUNT }, (_, index) =>
      Math.round(min + index * step),
    );
  }, [xRange]);

  const xFormatter = useMemo(() => {
    if (!xRange) return String;
    const isIntraday = xRange.max - xRange.min <= ONE_DAY_MS;
    if (isIntraday) {
      return (timestamp) =>
        new Date(timestamp).toLocaleTimeString(undefined, {
          hour: '2-digit',
          minute: '2-digit',
        });
    }

    const spansYears =
      new Date(xRange.min).getFullYear() !== new Date(xRange.max).getFullYear();

    return (timestamp) => {
      const d = new Date(timestamp);
      const date = d.toLocaleDateString(undefined, {
        day: 'numeric',
        month: 'short',
      });
      return spansYears ? `${date} '${String(d.getFullYear()).slice(2)}` : date;
    };
  }, [xRange]);

  const yFormatter = useMemo(() => {
    const units = series
      .filter((s) => visible.has(s.key) && s.unit)
      .map((s) => s.unit);
    const unique = [...new Set(units)];
    return unique.length === 1
      ? UNIT_FORMATTERS[unique[0]] ?? mixedFormatter
      : mixedFormatter;
  }, [series, visible]);

  const toggle = (key) => {
    setVisible((previous) => {
      const next = new Set(previous);
      if (next.has(key)) {
        next.delete(key);
      } else {
        next.add(key);
      }

      return next;
    });
  };

  const renderTooltip = ({ active, label, payload }) => {
    if (!active || !payload?.length) return null;
    const bg = isDark ? '#161b22' : '#fff';
    const border = isDark ? '#30363d' : '#d0d0d0';
    const labelColor = isDark ? '#c9d1d9' : '#333';
    const valueColor = isDark ? '#8b949e' : '#666';
    return (
      <div
        style={{
          background: bg,
          border: `1px solid ${border}`,
          borderRadius: 4,
          fontSize: '0.875em',
          padding: '6px 10px',
        }}
      >
        <div
          style={{
            color: labelColor,
            fontWeight: 'bold',
            fontSize: '1.05em',
            marginBottom: 4,
          }}
        >
          {xFormatter(label)}
        </div>
        {payload.map((entry) => {
          const s = series.find((item) => item.name === entry.name);
          const formatted = s?.format
            ? s.format(entry.value)
            : entry.value.toLocaleString();
          return (
            <div
              key={entry.dataKey}
              style={{ color: valueColor, display: 'flex', gap: '0.5em' }}
            >
              <span style={{ color: entry.color }}>&#9644;</span>
              <span>{entry.name}:</span>
              <span>{formatted}</span>
            </div>
          );
        })}
      </div>
    );
  };

  const renderLegend = ({ payload }) => (
    <div
      style={{
        display: 'flex',
        flexWrap: 'wrap',
        fontSize: '0.875em',
        gap: '1em',
        justifyContent: 'center',
      }}
    >
      {payload.map((entry) => (
        <span
          aria-checked={visible.has(entry.dataKey)}
          key={entry.dataKey}
          onClick={() => toggle(entry.dataKey)}
          onKeyDown={(event) => {
            if (event.key === 'Enter' || event.key === ' ') {
              toggle(entry.dataKey);
            }
          }}
          role="checkbox"
          style={{
            alignItems: 'center',
            cursor: 'pointer',
            display: 'flex',
            gap: '0.4em',
            opacity: visible.has(entry.dataKey) ? 1 : 0.35,
          }}
          tabIndex={0}
        >
          <svg
            height="10"
            width="16"
          >
            <line
              stroke={entry.color}
              strokeWidth="2"
              x1="0"
              x2="16"
              y1="5"
              y2="5"
            />
          </svg>
          {entry.value}
        </span>
      ))}
    </div>
  );

  return (
    <ResponsiveContainer
      height={height}
      width="100%"
    >
      <AreaChart data={data}>
        <CartesianGrid
          stroke={isDark ? 'rgba(255,255,255,0.20)' : '#d0d0d0'}
          strokeDasharray="3 3"
        />
        <XAxis
          dataKey="timestamp"
          domain={['dataMin', 'dataMax']}
          style={axisStyle}
          tickFormatter={xFormatter}
          ticks={xTicks}
          type="number"
        />
        <YAxis
          style={axisStyle}
          tickFormatter={yFormatter}
        />
        <Tooltip content={renderTooltip} />
        <Legend content={renderLegend} />
        {series.map(({ color, key, name }) => (
          <Area
            dataKey={key}
            dot={false}
            fill={color}
            fillOpacity={0.15}
            hide={!visible.has(key)}
            isAnimationActive={false}
            key={key}
            name={name}
            stroke={color}
            strokeOpacity={visible.has(key) ? 1 : 0}
            type="monotone"
          />
        ))}
      </AreaChart>
    </ResponsiveContainer>
  );
};

export default Graph;
