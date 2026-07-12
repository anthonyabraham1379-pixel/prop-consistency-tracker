import React from 'react';
import { StyleSheet, Text, TouchableOpacity, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { theme } from '../config/theme';

export default function SegmentedControl({ options, value, onChange, style, lockedValues = [], onLockedPress }) {
  return (
    <View style={[styles.row, style]}>
      {options.map((option) => {
        const active = value === option.value;
        const locked = lockedValues.includes(option.value);
        return (
          <TouchableOpacity
            key={String(option.value)}
            onPress={() => (locked ? onLockedPress?.(option.value) : onChange(option.value))}
            style={[styles.segment, active && styles.segmentActive]}
          >
            {locked && (
              <Ionicons
                name="lock-closed"
                size={11}
                color={theme.colors.textMuted}
                style={styles.lockIcon}
              />
            )}
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
    flexDirection: 'row',
    justifyContent: 'center',
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
  lockIcon: { marginRight: 4 },
  segmentText: {
    fontSize: 13,
    fontWeight: '600',
    color: theme.colors.textSecondary,
  },
  segmentTextActive: {
    color: theme.colors.accent,
  },
});
