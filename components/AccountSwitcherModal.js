import React from 'react';
import { Modal, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { theme } from '../config/theme';
import IconButton from './IconButton';
import PresetCard from './PresetCard';

export default function AccountSwitcherModal({ visible, challenges, activeChallengeId, onSelect, onClose }) {
  const insets = useSafeAreaInsets();

  return (
    <Modal visible={visible} animationType="slide" transparent onRequestClose={onClose}>
      <View style={styles.backdrop}>
        <View style={[styles.sheet, { paddingBottom: theme.spacing(4) + insets.bottom }]}>
          <View style={styles.header}>
            <Text style={styles.title}>Cambiar de cuenta</Text>
            <IconButton name="close" onPress={onClose} />
          </View>

          {challenges.map((c) => (
            <PresetCard
              key={c.id}
              label={c.name}
              active={c.id === activeChallengeId}
              onPress={() => {
                onSelect(c.id);
                onClose();
              }}
            />
          ))}
        </View>
      </View>
    </Modal>
  );
}

const styles = StyleSheet.create({
  backdrop: {
    flex: 1,
    backgroundColor: 'rgba(0,0,0,0.55)',
    justifyContent: 'flex-end',
  },
  sheet: {
    backgroundColor: theme.colors.background,
    borderTopLeftRadius: theme.radius.lg,
    borderTopRightRadius: theme.radius.lg,
    borderWidth: 1,
    borderColor: theme.colors.border,
    padding: theme.spacing(4),
  },
  header: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: theme.spacing(4),
  },
  title: {
    fontSize: 16,
    fontWeight: '700',
    color: theme.colors.textPrimary,
  },
});
