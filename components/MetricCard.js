import React from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { theme } from '../config/theme';

export default function MetricCard({ label, value, subValue, accent, barPct }) {
  return (
    <View style={styles.card}>
      <Text style={styles.label}>{label}</Text>
      <Text style={[styles.value, { color: accent }]}>{value}</Text>
      {subValue ? <Text style={styles.subValue}>{subValue}</Text> : null}
      {typeof barPct === 'number' ? (
        <View style={styles.barTrack}>
          <View style={[styles.barFill, { width: `${Math.min(Math.max(barPct, 0), 100)}%`, backgroundColor: accent }]} />
        </View>
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  card: {
    flex: 1,
    minWidth: '45%',
    backgroundColor: theme.colors.surface,
    borderWidth: 1,
    borderColor: theme.colors.border,
    borderRadius: theme.radius.md,
    padding: 13,
  },
  label: {
    fontSize: 11,
    color: theme.colors.textSecondary,
    marginBottom: 6,
  },
  value: {
    fontSize: 18,
    fontWeight: '700',
  },
  subValue: {
    fontSize: 11,
    color: theme.colors.textSecondary,
    marginTop: 2,
  },
  barTrack: {
    height: 4,
    backgroundColor: theme.colors.border,
    borderRadius: 2,
    marginTop: 8,
    overflow: 'hidden',
  },
  barFill: {
    height: '100%',
    borderRadius: 2,
  },
});
