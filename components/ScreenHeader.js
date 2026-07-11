import React from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { theme } from '../config/theme';
import IconButton from './IconButton';

export default function ScreenHeader({ eyebrow, title, subtitle, onBack, right }) {
  return (
    <View style={styles.wrap}>
      <View style={styles.topRow}>
        {onBack ? <IconButton name="chevron-back" onPress={onBack} style={styles.backButton} /> : null}
        <View style={styles.titleBlock}>
          {eyebrow ? <Text style={styles.eyebrow}>{eyebrow}</Text> : null}
          <Text style={styles.title}>{title}</Text>
        </View>
        {right ?? null}
      </View>
      {subtitle ? <Text style={styles.subtitle}>{subtitle}</Text> : null}
    </View>
  );
}

const styles = StyleSheet.create({
  wrap: { marginBottom: theme.spacing(5) },
  topRow: { flexDirection: 'row', alignItems: 'center', gap: 10 },
  titleBlock: { flex: 1 },
  backButton: { marginRight: 2 },
  eyebrow: {
    fontSize: 11,
    letterSpacing: 1,
    color: theme.colors.accent,
    fontWeight: '700',
    marginBottom: 6,
  },
  title: {
    fontSize: 22,
    fontWeight: '700',
    color: theme.colors.textPrimary,
  },
  subtitle: {
    fontSize: 13,
    color: theme.colors.textSecondary,
    marginTop: 4,
  },
});
