import React from 'react';
import { StyleSheet, Text, TouchableOpacity, View } from 'react-native';
import { theme } from '../config/theme';

export default function SegmentedControl({ options, value, onChange, style }) {
  return (
    <View style={[styles.row, style]}>
      {options.map((option) => {
        const active = value === option.value;
        return (
          <TouchableOpacity
            key={String(option.value)}
            onPress={() => onChange(option.value)}
            style={[styles.segment, active && styles.segmentActive]}
          >
            <Text style={[styles.segmentText, active && styles.segmentTextActive]}>{option.label}</Text>
          </TouchableOpacity>
        );
      })}
    </View>
  );
}

const styles = StyleSheet.create({
  row: { flexDirection: 'row', gap: 8, marginBottom: theme.spacing(3) },
  segment: {
    flex: 1,
    paddingVertical: 10,
    borderRadius: theme.radius.md,
    borderWidth: 1,
    borderColor: theme.colors.border,
    backgroundColor: theme.colors.surface,
    alignItems: 'center',
  },
  segmentActive: {
    borderColor: theme.colors.accent,
    backgroundColor: 'rgba(94,179,246,0.12)',
  },
  segmentText: {
    fontSize: 13,
    fontWeight: '600',
    color: theme.colors.textSecondary,
  },
  segmentTextActive: {
    color: theme.colors.accent,
  },
});
