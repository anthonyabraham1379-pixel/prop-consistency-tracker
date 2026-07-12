import React from 'react';
import { StyleSheet, Text, TouchableOpacity } from 'react-native';
import { Feather } from '@expo/vector-icons';
import { theme } from '../config/theme';

export default function PresetCard({ label, active, onPress, grid }) {
  return (
    <TouchableOpacity
      onPress={onPress}
      style={[styles.card, grid && styles.cardGrid, active && (grid ? styles.cardGridActive : styles.cardActive)]}
    >
      <Text style={[styles.label, grid && styles.labelGrid, active && grid && styles.labelGridActive]}>{label}</Text>
      {active && !grid ? <Feather name="check" size={16} color={theme.colors.accent} /> : null}
    </TouchableOpacity>
  );
}

const styles = StyleSheet.create({
  card: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingVertical: 12,
    paddingHorizontal: 14,
    borderRadius: theme.radius.md,
    borderWidth: 1,
    borderColor: theme.colors.border,
    backgroundColor: theme.colors.surface,
    marginBottom: 8,
  },
  cardActive: {
    borderColor: theme.colors.accent,
    backgroundColor: 'rgba(94,179,246,0.08)',
  },
  label: {
    fontSize: 13.5,
    color: theme.colors.textPrimary,
  },
  cardGrid: {
    flexBasis: '48%',
    flexGrow: 1,
    justifyContent: 'center',
    paddingVertical: 16,
  },
  cardGridActive: {
    borderColor: theme.colors.accent,
    backgroundColor: 'rgba(94,179,246,0.18)',
  },
  labelGrid: {
    fontWeight: '600',
    textAlign: 'center',
  },
  labelGridActive: {
    color: theme.colors.accent,
  },
});
