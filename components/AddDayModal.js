import React, { useEffect, useState } from 'react';
import { Modal, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { theme } from '../config/theme';
import { useStrings } from '../config/strings';
import IconButton from './IconButton';
import FieldLabel from './FieldLabel';
import NumField from './NumField';
import SegmentedControl from './SegmentedControl';
import CheckRow from './CheckRow';
import DateTimeField from './DateTimeField';
import PrimaryButton from './PrimaryButton';

export default function AddDayModal({ visible, onClose, onSubmit, challenges, activeChallengeId }) {
  const insets = useSafeAreaInsets();
  const strings = useStrings();
  const [sign, setSign] = useState('gain');
  const [amount, setAmount] = useState('');
  const [date, setDate] = useState(new Date());
  const [selectedIds, setSelectedIds] = useState([activeChallengeId]);

  useEffect(() => {
    if (visible) {
      setSign('gain');
      setAmount('');
      setDate(new Date());
      setSelectedIds(activeChallengeId ? [activeChallengeId] : []);
    }
  }, [visible, activeChallengeId]);

  const toggleId = (id) => {
    setSelectedIds((ids) => (ids.includes(id) ? ids.filter((i) => i !== id) : [...ids, id]));
  };

  const canSubmit = !!amount && selectedIds.length > 0;

  const SIGN_OPTIONS = [
    { label: strings.addDay.gain, value: 'gain' },
    { label: strings.addDay.loss, value: 'loss' },
  ];

  const handleSubmit = () => {
    const magnitude = Number(amount) || 0;
    if (magnitude === 0 || selectedIds.length === 0) return;
    onSubmit(sign === 'loss' ? -magnitude : magnitude, selectedIds, date.toISOString());
    onClose();
  };

  return (
    <Modal visible={visible} animationType="slide" transparent onRequestClose={onClose}>
      <View style={styles.backdrop}>
        <View style={[styles.sheet, { paddingBottom: theme.spacing(4) + insets.bottom }]}>
          <View style={styles.header}>
            <Text style={styles.title}>{strings.addDay.title}</Text>
            <IconButton name="close" onPress={onClose} />
          </View>

          <FieldLabel>{strings.addDay.resultLabel}</FieldLabel>
          <SegmentedControl options={SIGN_OPTIONS} value={sign} onChange={setSign} />

          <NumField label={strings.addDay.amountLabel} value={amount} onChange={setAmount} prefix="$" />

          <DateTimeField label={strings.addDay.dateTimeLabel} value={date} onChange={setDate} />

          {challenges.length > 1 && (
            <>
              <FieldLabel style={styles.accountsLabel}>{strings.addDay.applyToAccounts}</FieldLabel>
              {challenges.map((c) => (
                <CheckRow
                  key={c.id}
                  label={c.name}
                  checked={selectedIds.includes(c.id)}
                  onToggle={() => toggleId(c.id)}
                />
              ))}
            </>
          )}

          <PrimaryButton
            label={strings.addDay.submit}
            onPress={handleSubmit}
            style={styles.submitButton}
            disabled={!canSubmit}
          />
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
  accountsLabel: { marginTop: 6 },
  submitButton: { marginTop: theme.spacing(2) },
});
