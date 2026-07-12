import React from 'react';
import { Modal, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';

import { theme } from '../config/theme';
import { strings } from '../config/strings';
import PrimaryButton from './PrimaryButton';

export default function BreachModal({ visible, onRestart, onSave, onDelete }) {
  const insets = useSafeAreaInsets();

  return (
    <Modal visible={visible} animationType="slide" transparent>
      <View style={styles.backdrop}>
        <View style={[styles.sheet, { paddingBottom: theme.spacing(4) + insets.bottom }]}>
          <View style={styles.iconWrap}>
            <Ionicons name="warning" size={32} color={theme.colors.negative} />
          </View>
          <Text style={styles.title}>{strings.breach.title}</Text>
          <Text style={styles.body}>{strings.breach.body}</Text>

          <PrimaryButton label={strings.breach.restart} onPress={onRestart} style={styles.restartButton} />

          <Text style={styles.saveLink} onPress={onSave}>
            {strings.breach.save}
          </Text>
          <Text style={styles.deleteLink} onPress={onDelete}>
            {strings.breach.delete}
          </Text>
        </View>
      </View>
    </Modal>
  );
}

const styles = StyleSheet.create({
  backdrop: {
    flex: 1,
    backgroundColor: 'rgba(0,0,0,0.6)',
    justifyContent: 'flex-end',
  },
  sheet: {
    backgroundColor: theme.colors.background,
    borderTopLeftRadius: theme.radius.lg,
    borderTopRightRadius: theme.radius.lg,
    borderWidth: 1,
    borderColor: theme.colors.border,
    padding: theme.spacing(5),
    alignItems: 'center',
  },
  iconWrap: {
    width: 56,
    height: 56,
    borderRadius: 28,
    backgroundColor: 'rgba(248,113,113,0.12)',
    alignItems: 'center',
    justifyContent: 'center',
    marginBottom: theme.spacing(3),
  },
  title: {
    fontSize: 17,
    fontWeight: '700',
    color: theme.colors.textPrimary,
    textAlign: 'center',
    marginBottom: 8,
  },
  body: {
    fontSize: 13,
    color: theme.colors.textSecondary,
    textAlign: 'center',
    lineHeight: 18,
    marginBottom: theme.spacing(5),
  },
  restartButton: { width: '100%' },
  saveLink: {
    color: theme.colors.accent,
    fontSize: 13.5,
    fontWeight: '600',
    marginTop: theme.spacing(4),
    paddingVertical: 6,
    textAlign: 'center',
  },
  deleteLink: {
    color: theme.colors.negative,
    fontSize: 13,
    fontWeight: '600',
    marginTop: theme.spacing(3),
    paddingVertical: 6,
    textAlign: 'center',
  },
});
