import React from 'react';
import { StyleSheet, Text } from 'react-native';
import { theme } from '../config/theme';

export default function FieldLabel({ children, style }) {
  return <Text style={[styles.label, style]}>{children}</Text>;
}

const styles = StyleSheet.create({
  label: {
    fontSize: 12,
    color: theme.colors.textSecondary,
    marginBottom: 6,
    fontWeight: '600',
  },
});
