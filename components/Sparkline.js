import React from 'react';
import Svg, { Polygon, Polyline } from 'react-native-svg';
import { theme } from '../config/theme';

const VIEW_WIDTH = 300;

/**
 * Curva simple (área + línea) a partir de una serie de números.
 * Pensada para la curva de equity acumulado.
 */
export default function Sparkline({ data, color = theme.colors.positive, height = 60 }) {
  if (!data || data.length < 2) {
    return <Svg width="100%" height={height} viewBox={`0 0 ${VIEW_WIDTH} ${height}`} />;
  }

  const min = Math.min(...data, 0);
  const max = Math.max(...data, 0);
  const range = max - min || 1;
  const step = VIEW_WIDTH / (data.length - 1);
  const points = data.map((v, i) => {
    const x = i * step;
    const y = height - ((v - min) / range) * (height - 8) - 4;
    return [x, y];
  });
  const line = points.map((p) => p.join(',')).join(' ');
  const area = `0,${height} ${line} ${VIEW_WIDTH},${height}`;

  return (
    <Svg width="100%" height={height} viewBox={`0 0 ${VIEW_WIDTH} ${height}`} preserveAspectRatio="none">
      <Polygon points={area} fill={color} fillOpacity={0.15} />
      <Polyline points={line} fill="none" stroke={color} strokeWidth={2} strokeLinejoin="round" strokeLinecap="round" />
    </Svg>
  );
}
