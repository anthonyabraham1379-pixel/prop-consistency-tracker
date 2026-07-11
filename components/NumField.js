import React from 'react';
import { StyleSheet, Text, TextInput, View } from 'react-native';
import { theme } from '../config/theme';
import FieldLabel from './FieldLabel';

export default function NumField({ label, value, onChange, prefix, suffix, style }) {
  return (
    <View style={[styles.wrap, style]}>
      <FieldLabel>{label}</FieldLabel>
      <View style={styles.inputWrap}>
        {prefix ? <Text style={styles.prefix}>{prefix}</Text> : null}
        <TextInput
          style={styles.input}
          value={value === null || value === undefined ? '' : String(value)}
          onChangeText={(text) => onChange(text.replace(/[^0-9.]/g, ''))}
          keyboardType="decimal-pad"
          placeholder="0"
          placeholderTextColor={theme.colors.textMuted}
        />
        {suffix ? <Text style={styles.suffix}>{suffix}</Text> : null}
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  wrap: { marginBottom: theme.spacing(3) },
  inputWrap: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: theme.colors.surface,
    borderWidth: 1,
    borderColor: theme.colors.border,
    borderRadius: theme.radius.md,
    paddingHorizontal: 12,
  },
  prefix: {
    color: theme.colors.textMuted,
    fontSize: 14,
    marginRight: 4,
  },
  suffix: {
    color: theme.colors.textMuted,
    fontSize: 14,
    marginLeft: 4,
  },
  input: {
    flex: 1,
    color: theme.colors.textPrimary,
    fontSize: 14,
    paddingVertical: 11,
  },
});
