import React from 'react';
import { StyleSheet, Text, TouchableOpacity, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { theme } from '../config/theme';
import IconButton from './IconButton';

export default function ScreenHeader({ eyebrow, title, subtitle, onBack, right, onEyebrowPress }) {
  return (
    <View style={styles.wrap}>
      <View style={styles.topRow}>
        {onBack ? <IconButton name="chevron-back" onPress={onBack} style={styles.backButton} /> : null}
        <View style={styles.titleBlock}>
          {eyebrow ? (
            onEyebrowPress ? (
              <TouchableOpacity style={styles.eyebrowRow} onPress={onEyebrowPress}>
                <Text style={styles.eyebrow}>{eyebrow}</Text>
                <Ionicons name="chevron-down" size={12} color={theme.colors.accent} />
              </TouchableOpacity>
            ) : (
              <Text style={styles.eyebrow}>{eyebrow}</Text>
            )
          ) : null}
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
  eyebrowRow: { flexDirection: 'row', alignItems: 'center', gap: 4, marginBottom: 6, alignSelf: 'flex-start' },
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
