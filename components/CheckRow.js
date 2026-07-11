import React from 'react';
import { StyleSheet, Text, TouchableOpacity } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { theme } from '../config/theme';

export default function CheckRow({ label, checked, onToggle }) {
  return (
    <TouchableOpacity onPress={onToggle} style={[styles.row, checked && styles.rowActive]}>
      <Ionicons
        name={checked ? 'checkbox' : 'square-outline'}
        size={20}
        color={checked ? theme.colors.accent : theme.colors.textMuted}
      />
      <Text style={styles.label}>{label}</Text>
    </TouchableOpacity>
  );
}

const styles = StyleSheet.create({
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 10,
    paddingVertical: 12,
    paddingHorizontal: 14,
    borderRadius: theme.radius.md,
    borderWidth: 1,
    borderColor: theme.colors.border,
    backgroundColor: theme.colors.surface,
    marginBottom: 8,
  },
  rowActive: {
    borderColor: theme.colors.accent,
    backgroundColor: 'rgba(94,179,246,0.08)',
  },
  label: {
    fontSize: 13.5,
    color: theme.colors.textPrimary,
  },
});
