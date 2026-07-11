import React from 'react';
import { View } from 'react-native';
import Svg, { Circle } from 'react-native-svg';
import { theme } from '../config/theme';

/**
 * Dona de dos colores. `pct` es el % (0-100) que se pinta con `colorA`;
 * el resto se pinta con `colorB`. `pct === null` dibuja un anillo gris
 * neutro (estado "sin datos").
 */
export default function DonutChart({ pct, colorA = theme.colors.positive, colorB = theme.colors.negative, size = 150 }) {
  const strokeWidth = Math.round(size * 0.15);
  const radius = (size - strokeWidth) / 2;
  const circumference = 2 * Math.PI * radius;
  const isNeutral = pct === null;
  const clamped = Math.min(Math.max(pct ?? 0, 0), 100);
  const dashA = (clamped / 100) * circumference;

  return (
    <View style={{ width: size, height: size }}>
      <Svg width={size} height={size} viewBox={`0 0 ${size} ${size}`}>
        <Circle
          cx={size / 2}
          cy={size / 2}
          r={radius}
          stroke={isNeutral ? theme.colors.border : colorB}
          strokeWidth={strokeWidth}
          fill="none"
        />
        {!isNeutral && (
          <Circle
            cx={size / 2}
            cy={size / 2}
            r={radius}
            stroke={colorA}
            strokeWidth={strokeWidth}
            strokeDasharray={`${dashA} ${circumference - dashA}`}
            fill="none"
            rotation={-90}
            origin={`${size / 2}, ${size / 2}`}
          />
        )}
      </Svg>
    </View>
  );
}
